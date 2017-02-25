namespace Foom.Ecs

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

#nowarn "9"

/// This is internal use only.
module DataStructures =

    [<ReferenceEquality>]
    type UnsafeResizeArray<'T> =
        {
            mutable count: int
            mutable buffer: 'T []
        }

        static member Create capacity =
            if capacity <= 0 then
                failwith "Capacity must be greater than 0"

            {
                count = 0
                buffer = Array.zeroCreate<'T> capacity
            }

        member this.IncreaseCapacity () =
            let newLength = uint32 this.buffer.Length * 2u
            if newLength >= uint32 Int32.MaxValue then
                failwith "Length is bigger than the maximum number of elements in the array"

            let newBuffer = Array.zeroCreate<'T> (int newLength)
            Array.Copy (this.buffer, newBuffer, this.count)
            this.buffer <- newBuffer
             

        member inline this.Add item =
            if this.count >= this.buffer.Length then
                this.IncreaseCapacity ()
            
            this.buffer.[this.count] <- item
            this.count <- this.count + 1

        member inline this.LastItem = this.buffer.[this.count - 1]

        member inline this.SwapRemoveAt index =
            if index >= this.count then
                failwith "Index out of bounds"

            let lastIndex = this.count - 1

            this.buffer.[index] <- this.buffer.[lastIndex]
            this.buffer.[lastIndex] <- Unchecked.defaultof<'T>
            this.count <- lastIndex

        member inline this.Count = this.count
        member inline this.Buffer = this.buffer

open DataStructures

[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

    [<FieldOffset (0)>]
    val Index : int

    [<FieldOffset (4)>]
    val Version : uint32

    [<FieldOffset (0); DefaultValue>]
    val Id : uint64

    new (index, version) = { Index = index; Version = version }

    member this.IsZero = this.Id = 0UL

    override this.ToString () = String.Format ("(Entity #{0}.{1})", this.Index, this.Version)

type IComponent = interface end

module Events =

    type ComponentAdded<'T when 'T :> IComponent and 'T : not struct> = 
        { entity: Entity }

        member this.Entity = this.entity

        interface IEvent

    type ComponentRemoved<'T when 'T :> IComponent and 'T : not struct> = 
        { entity: Entity }

        member this.Entity = this.entity

        interface IEvent

    type AnyComponentAdded =
        { entity: Entity; componentType: Type }

        member this.Entity = this.entity

        member this.ComponentType = this.componentType

        interface IEvent

    type AnyComponentRemoved =
        { entity: Entity; componentType: Type }

        member this.Entity = this.entity

        member this.ComponentType = this.componentType

        interface IEvent

    type EntitySpawned =
        { entity: Entity }

        member this.Entity = this.entity

        interface IEvent

    type EntityDestroyed =
        { entity: Entity }

        member this.Entity = this.entity

        interface IEvent

open Events

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

    abstract GetIndex : int -> int

    abstract GetComponent : int -> IComponent

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> IComponent and 'T : not struct> =
    {
        ComponentAddedEvent: Event<ComponentAdded<'T>>
        ComponentRemovedEvent: Event<ComponentRemoved<'T>>

        RemoveComponent: Entity -> unit

        Active: bool []
        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

        member this.GetIndex id = this.IndexLookup.[id]

        member this.GetComponent index = this.Components.Buffer.[index] :> IComponent

[<ReferenceEquality>]
type EntityManager =
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

        AnyComponentAddedEvent: Event<AnyComponentAdded>
        AnyComponentRemovedEvent: Event<AnyComponentRemoved>

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

        let anyComponentAddedEvent = eventManager.GetEvent<AnyComponentAdded> ()
        let anyComponentRemovedEvent = eventManager.GetEvent<AnyComponentRemoved> ()

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
            AnyComponentAddedEvent = anyComponentAddedEvent
            AnyComponentRemovedEvent = anyComponentRemovedEvent
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

    member this.GetEntityLookupData<'T when 'T :> IComponent and 'T : not struct> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        match this.Lookup.TryGetValue(t, &data) with
        | true -> data :?> EntityLookupData<'T>
        | _ ->
            let factory t =
                let data =
                    {
                        ComponentAddedEvent = this.EventAggregator.GetEvent<ComponentAdded<'T>> ()
                        ComponentRemovedEvent = this.EventAggregator.GetEvent<ComponentRemoved<'T>> ()

                        RemoveComponent = fun entity -> this.Remove<'T> entity

                        Active = Array.zeroCreate<bool> this.MaxEntityAmount
                        IndexLookup = Array.init this.MaxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                        Entities = UnsafeResizeArray.Create this.MaxEntityAmount
                        Components = UnsafeResizeArray.Create this.MaxEntityAmount
                    }

                data :> IEntityLookupData

            this.Lookup.GetOrAdd(t, factory) :?> EntityLookupData<'T>

    member inline this.Iterate<'T when 'T :> IComponent and 'T : not struct> (f) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]

                if data.Active.[entity.Index] && this.ActiveIndices.[entity.Index] then
                    f entity data.Components.Buffer.[i]

            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent and 'T1 : not struct and 'T2 : not struct> (f) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]
    
                if this.ActiveIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]
    
                    if comp1Index >= 0 && comp2Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> (f) : unit =
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
    
                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index] data3.Components.Buffer.[comp3Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> (f) : unit =
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
    
                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && comp4Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] && data4.Active.[entity.Index] then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index] data3.Components.Buffer.[comp3Index] data4.Components.Buffer.[comp4Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i

    // Components

    member this.Add<'T when 'T :> IComponent and 'T : not struct> (entity: Entity, comp: 'T) =
        if this.CurrentIterations > 0 then
            let data = this.GetEntityLookupData<'T> ()
            this.PendingQueue.Enqueue (fun () -> this.Add (entity, comp))
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, already added to {1}.", typeof<'T>.Name, entity))
                else
                    this.EntityRemovals.[entity.Index].Add (data.RemoveComponent)

                    data.Active.[entity.Index] <- true
                    data.IndexLookup.[entity.Index] <- data.Entities.Count

                    data.Components.Add comp
                    data.Entities.Add entity

                    this.AnyComponentAddedEvent.Trigger ({ entity = entity; componentType = typeof<'T> })
                    data.ComponentAddedEvent.Trigger ({ entity = entity })
            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot add component, {1}", entity, typeof<'T>.Name))

    member this.Remove<'T when 'T :> IComponent and 'T : not struct> (entity: Entity) =
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

                    data.Active.[entity.Index] <- false
                    data.IndexLookup.[entity.Index] <- -1

                    if not (entity.Index.Equals swappingEntity.Index) then
                        data.IndexLookup.[swappingEntity.Index] <- index

                    this.AnyComponentRemovedEvent.Trigger ({ entity = entity; componentType = typeof<'T> })
                    data.ComponentRemovedEvent.Trigger ({ entity = entity })
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

            this.EntitySpawnedEvent.Trigger ({ entity = entity })

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

                this.EntityDestroyedEvent.Trigger ({ entity = entity })
            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot destroy.", entity))

    // Component Query

    //************************************************************************************************************************

    member this.TryGet<'T when 'T :> IComponent and 'T : not struct> (entity: Entity) : 'T option =
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

    member this.TryGet (entity: Entity, typ: Type) : IComponent option =
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

    member this.Has<'T when 'T :> IComponent and 'T : not struct> (entity: Entity) =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            data.Active.[entity.Index]
        else
            false   

    //************************************************************************************************************************

    member this.ForEach<'T when 'T :> IComponent and 'T : not struct> (f: Entity -> 'T -> unit) : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent and 'T1 : not struct and 'T2 : not struct> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2, 'T3> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> f : unit =
        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2, 'T3, 'T4> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.TryFind<'T when 'T :> IComponent and 'T : not struct> (predicate: (Entity -> 'T -> bool)) : (Entity * 'T) option =
        let mutable item = None

        this.ForEach<'T> (fun entity comp ->
            if item.IsNone && predicate entity comp then
                item <- Some (entity, comp)
        )

        item

    member this.TryFind<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent and 'T1 : not struct and 'T2 : not struct> (predicate: (Entity -> 'T1 -> 'T2 -> bool)) : (Entity * 'T1 * 'T2) option =
        let mutable item = None

        this.ForEach<'T1, 'T2> (fun entity comp1 comp2 ->
            if item.IsNone && predicate entity comp1 comp2 then
                item <- Some (entity, comp1, comp2)
        )

        item

    member this.MaxNumberOfEntities = this.MaxEntityAmount - 1