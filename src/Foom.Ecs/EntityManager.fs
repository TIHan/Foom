namespace Foom.Ecs

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

open Foom.Collections

#nowarn "9"

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

    abstract GetIndex : int -> int

    abstract GetComponent : int -> Component

open Events

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> Component> =
    {
        ComponentRemovedEvent: Event<ComponentRemoved<'T>>

        ComponentAddedTriggers : ResizeArray<'T -> unit>

        RemoveComponent: Entity -> unit

        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

        member this.GetIndex id = this.IndexLookup.[id]

        member this.GetComponent index = this.Components.Buffer.[index] :> Component

type EntityBuilder = EntityBuilder of (Entity -> EntityManager -> unit)

and [<ReferenceEquality>] EntityManager =
    {
        EventAggregator: EventAggregator

        MaxEntityAmount: int
        Lookup: ConcurrentDictionary<Type, IEntityLookupData>

        ActiveVersions: uint32 []
        ActiveIndices: bool []

        mutable nextEntityIndex: int
        RemovedEntityQueue: Queue<Entity>

        EntityRemovals: ((Entity -> unit) ResizeArray) []

        EntitySpawnedEvent: Event<EntitySpawned>
        EntityDestroyedEvent: Event<EntityDestroyed>

        mutable CurrentIterations: int
        PendingQueue: Queue<unit -> unit>
    }

    static member Create (eventManager: EventAggregator, maxEntityAmount) =
        if maxEntityAmount <= 0 then
            failwith "Max entity amount must be greater than 0."

        let maxEntityAmount = maxEntityAmount + 1
        let lookup = ConcurrentDictionary<Type, IEntityLookupData> ()

        let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)
        let activeIndices = Array.zeroCreate<bool> maxEntityAmount

        let mutable nextEntityIndex = 1
        let removedEntityQueue = Queue<Entity> () 

        let entityRemovals : ((Entity -> unit) ResizeArray) [] = Array.init maxEntityAmount (fun _ -> ResizeArray ())

        let entitySpawnedEvent = eventManager.GetEvent<EntitySpawned> ()
        let entityDestroyedEvent = eventManager.GetEvent<EntityDestroyed> ()

        {
            EventAggregator = eventManager
            MaxEntityAmount = maxEntityAmount
            Lookup = lookup
            ActiveVersions = activeVersions
            ActiveIndices = activeIndices
            nextEntityIndex = nextEntityIndex
            RemovedEntityQueue = removedEntityQueue
            EntityRemovals = entityRemovals
            EntitySpawnedEvent = entitySpawnedEvent
            EntityDestroyedEvent = entityDestroyedEvent
            CurrentIterations = 0
            PendingQueue = Queue ()
        }

    member inline this.IsValidEntity (entity: Entity) =
        not (entity.Index.Equals 0u) && this.ActiveVersions.[entity.Index].Equals entity.Version

    member inline this.ResolvePendingQueues () =
        if this.CurrentIterations = 0 then

            while this.PendingQueue.Count > 0 do
                let action = this.PendingQueue.Dequeue ()
                action ()

    member this.GetEntityLookupData<'T when 'T :> Component> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        match this.Lookup.TryGetValue(t, &data) with
        | true -> data :?> EntityLookupData<'T>
        | _ ->
            let triggers = ResizeArray ()

            let added = this.EventAggregator.GetComponentAddedEvent<'T> ()
            triggers.Add (fun o -> added.Trigger o)

            if t.GetTypeInfo().BaseType <> typeof<Component> then
                let t = t.GetTypeInfo().BaseType
                let mutable trigger = Unchecked.defaultof<obj -> unit>
                if this.EventAggregator.TryGetComponentAddedTrigger (t, &trigger) then
                    triggers.Add (fun o -> trigger o)

                if t.GetTypeInfo().BaseType <> typeof<Component> then
                    let t = t.GetTypeInfo().BaseType
                    let mutable trigger = Unchecked.defaultof<obj -> unit>
                    if this.EventAggregator.TryGetComponentAddedTrigger (t, &trigger) then
                        triggers.Add (fun o -> trigger o)

            let factory t =
                let data =
                    {
                        ComponentRemovedEvent = this.EventAggregator.GetEvent<ComponentRemoved<'T>> ()

                        ComponentAddedTriggers = triggers

                        RemoveComponent = fun entity -> this.Remove<'T> entity

                        IndexLookup = Array.init this.MaxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                        Entities = UnsafeResizeArray.Create this.MaxEntityAmount
                        Components = UnsafeResizeArray.Create this.MaxEntityAmount
                    }

                data :> IEntityLookupData

            this.Lookup.GetOrAdd(t, factory) :?> EntityLookupData<'T>

    member inline this.Iterate<'T when 'T :> Component> (f) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let entities = data.Entities.Buffer
            let activeIndices = this.ActiveIndices
            let components = data.Components.Buffer

            let inline iter i =
                let entity = entities.[i]

                if activeIndices.[entity.Index] then
                    f entity components.[i]

            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> (f) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let entities = data.Entities.Buffer
            let activeIndices = this.ActiveIndices
            let components1 = data1.Components.Buffer
            let components2 = data2.Components.Buffer
            let lookup1 = data1.IndexLookup
            let lookup2 = data2.IndexLookup

            let inline iter i =
                let entity = entities.[i]
    
                if activeIndices.[entity.Index] then
                    let comp1Index = lookup1.[entity.Index]
                    let comp2Index = lookup2.[entity.Index]
    
                    if comp1Index >= 0 && comp2Index >= 0 then
                        f entity components1.[comp1Index] components2.[comp2Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2, 'T3 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component> (f) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) && 
           this.Lookup.TryGetValue (typeof<'T3>, &data3) then
            let data = [|data1;data2;data3|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]
    
                if this.ActiveIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]
                    let comp3Index = data3.IndexLookup.[entity.Index]
    
                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index] data3.Components.Buffer.[comp3Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2, 'T3, 'T4 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component and 'T4 :> Component> (f) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        let mutable data4 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) && 
           this.Lookup.TryGetValue (typeof<'T3>, &data3) && this.Lookup.TryGetValue (typeof<'T4>, &data4) then
            let data = [|data1;data2;data3;data4|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]
    
                if this.ActiveIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]
                    let comp3Index = data3.IndexLookup.[entity.Index]
                    let comp4Index = data4.IndexLookup.[entity.Index]
    
                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && comp4Index >= 0 then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index] data3.Components.Buffer.[comp3Index] data4.Components.Buffer.[comp4Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    // Components

    member this.Add<'T when 'T :> Component> (entity: Entity, comp: 'T) =
        if this.CurrentIterations > 0 then
            let data = this.GetEntityLookupData<'T> ()
            this.PendingQueue.Enqueue (fun () -> this.Add (entity, comp))
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, already added to {1}.", typeof<'T>.Name, entity))
                else
                    if not comp.Owner.IsZero then
                        Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, has already been assigned to {1}.", typeof<'T>.Name, comp.Owner))
                    else
                        comp.Owner <- entity

                        this.EntityRemovals.[entity.Index].Add (data.RemoveComponent)

                        data.IndexLookup.[entity.Index] <- data.Entities.Count

                        data.Components.Add comp
                        data.Entities.Add entity

                        for i = 0 to data.ComponentAddedTriggers.Count - 1 do
                            let f = data.ComponentAddedTriggers.[i]
                            f comp
            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot add component, {1}", entity, typeof<'T>.Name))

    member this.Remove<'T when 'T :> Component> (entity: Entity) =
        if this.CurrentIterations > 0 then
            let data = this.GetEntityLookupData<'T> ()
            this.PendingQueue.Enqueue (fun () -> data.RemoveComponent (entity))
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    let index = data.IndexLookup.[entity.Index]
                    let swappingEntity = data.Entities.LastItem

                    data.Entities.SwapRemoveAt index
                    data.Components.SwapRemoveAt index

                    data.IndexLookup.[entity.Index] <- -1

                    if not (entity.Index.Equals swappingEntity.Index) then
                        data.IndexLookup.[swappingEntity.Index] <- index

                    data.ComponentRemovedEvent.Trigger (ComponentRemoved<'T> (entity))
                else
                    Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, does not exist on {1}", typeof<'T>.Name, entity))

            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot remove component, {1}", entity, typeof<'T>.Name))

    member this.Spawn () =                         
        if this.RemovedEntityQueue.Count = 0 && this.nextEntityIndex >= this.MaxEntityAmount then
            Debug.WriteLine (String.Format ("ECS WARNING: Unable to spawn entity. Max entity amount hit: {0}", (this.MaxEntityAmount - 1)))
            Entity ()
        else
            let entity =
                if this.RemovedEntityQueue.Count > 0 then
                    let entity = this.RemovedEntityQueue.Dequeue ()
                    Entity (entity.Index, entity.Version + 1u)
                else
                    let index = this.nextEntityIndex
                    this.nextEntityIndex <- index + 1
                    Entity (index, 1u)

            this.ActiveVersions.[entity.Index] <- entity.Version
            this.ActiveIndices.[entity.Index] <- true

            this.EntitySpawnedEvent.Trigger (EntitySpawned entity)

            entity

    member this.Destroy (entity: Entity) =
        if this.CurrentIterations > 0 then
            this.PendingQueue.Enqueue (fun () -> this.Destroy entity)
        else
            if this.IsValidEntity entity then
                let removals = this.EntityRemovals.[entity.Index]
                removals |> Seq.iter (fun f -> f entity)
                removals.Clear ()
                this.RemovedEntityQueue.Enqueue entity  

                this.ActiveVersions.[entity.Index] <- 0u
                this.ActiveIndices.[entity.Index] <- false

                this.EntityDestroyedEvent.Trigger (EntityDestroyed entity)
            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot destroy.", entity))

    // Component Query

    //************************************************************************************************************************

    member this.TryGet<'T when 'T :> Component> (entity: Entity) : 'T option =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if this.IsValidEntity entity then
                let index = data.IndexLookup.[entity.Index]
                if index >= 0 then
                    Some data.Components.Buffer.[index]
                else
                    None
            else
                None
        else
            None

    member this.TryGet (entity: Entity, typ: Type) : Component option =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typ, &data) then
            if this.IsValidEntity entity then
                let index = data.GetIndex (entity.Index)
                if index >= 0 then
                    Some (data.GetComponent index)
                else
                    None
            else
                None
        else
            None

    member this.IsValid entity =
        this.IsValidEntity entity

    member this.Has<'T when 'T :> Component> (entity: Entity) =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            data.IndexLookup.[entity.Index] >= 0
        else
            false   

    //************************************************************************************************************************

    member this.ForEach<'T when 'T :> Component> (f: Entity -> 'T -> unit) : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2, 'T3> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component and 'T4 :> Component> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2, 'T3, 'T4> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.TryFind<'T when 'T :> Component> (predicate: (Entity -> 'T -> bool)) : (Entity * 'T) option =
        let mutable item = None

        this.ForEach<'T> (fun entity comp ->
            if item.IsNone && predicate entity comp then
                item <- Some (entity, comp)
        )

        item

    member this.TryFind<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> (predicate: (Entity -> 'T1 -> 'T2 -> bool)) : (Entity * 'T1 * 'T2) option =
        let mutable item = None

        this.ForEach<'T1, 'T2> (fun entity comp1 comp2 ->
            if item.IsNone && predicate entity comp1 comp2 then
                item <- Some (entity, comp1, comp2)
        )

        item

    member this.MaxNumberOfEntities = this.MaxEntityAmount - 1

[<AutoOpen>]
module EntityPrototype =

    [<Struct>]
    type EntityPrototype = EntityPrototype of (Entity -> EntityManager -> unit)

    [<Sealed>]
    type EntityPrototypeBuilder () =

        member x.Yield (a : 'T) = EntityPrototype (fun _ _ -> ())

        member x.AddComponent (EntityPrototype x, [<ProjectionParameter>] f : unit -> #Component) =
            EntityPrototype (
                fun ent em ->
                    x ent em
                    em.Add (ent, f ())
            )

    let entity = EntityPrototypeBuilder ()

type EntityManager with

    member x.Spawn (entProto : EntityPrototype) =
        let ent = x.Spawn ()
        match entProto with
        | EntityPrototype f -> f ent x
        ent
