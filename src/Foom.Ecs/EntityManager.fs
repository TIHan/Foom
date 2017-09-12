namespace Foom.Ecs

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Runtime.Serialization
open System.Linq

open Foom.Collections

open Newtonsoft.Json

#nowarn "9"

type EcsContractResolver () =
    inherit Newtonsoft.Json.Serialization.DefaultContractResolver ()
   
    member this.CreateNewProperty(memberInfo, memberSerialization) =
        let property = base.CreateProperty(memberInfo, memberSerialization)

        let ctorInfos = memberInfo.DeclaringType.GetTypeInfo().DeclaredConstructors
        let isPartOfCtor = ctorInfos.Any (fun ctorInfo -> ctorInfo.GetParameters().Any (fun x -> 
            x.Name.ToLowerInvariant() = property.PropertyName.ToLowerInvariant()
        ))

        let pInfo = memberInfo :> obj :?> PropertyInfo
        if not isPartOfCtor && not (pInfo.CanRead && pInfo.CanWrite) then
            property.Ignored <- true

        if property.PropertyType.GetTypeInfo().IsAssignableFrom (typeof<Component>.GetTypeInfo()) then
            property.TypeNameHandling <- Nullable TypeNameHandling.All

        if property.PropertyType.GetTypeInfo().IsAssignableFrom (typeof<Component seq>.GetTypeInfo()) then
            property.ItemTypeNameHandling <- Nullable TypeNameHandling.All

        property

    override this.CreateProperty(memberInfo, memberSerialization) =
        this.CreateNewProperty(memberInfo, memberSerialization)

    override this.CreateProperties (typ, memberSerialization) =
        let props = ResizeArray () :> IList<Serialization.JsonProperty>

        typ.GetRuntimeProperties ()
        |> Seq.iter (fun p ->
            let memberInfo = p :> MemberInfo
            let p = this.CreateNewProperty (memberInfo, memberSerialization)
            p.Readable <- true
            p.Writable <- true
            props.Add p
        )
        props

    override this.CreateObjectContract (typ) =
        let contract = base.CreateObjectContract (typ)
        contract.DefaultCreatorNonPublic <- true
        contract

[<CLIMutable>]
type SerializedEntity =
    {
        Entity : Entity
        Components : obj seq
    }

type ByrefFunc<'TTarget, 'TReturn> = delegate of byref<'TTarget> -> 'TReturn
type ByrefAction<'TTarget, 'TParam> = delegate of byref<'TTarget> * 'TParam -> unit

[<Sealed>]
type Clone private () =

    static member GetHelper<'TTarget, 'TReturn> (meth : MethodInfo) =
        let func = meth.CreateDelegate (typeof<Func<'TTarget, 'TReturn>>) :?> Func<'TTarget, 'TReturn>
        Func<'TTarget, obj> (fun t -> func.Invoke (t) :> obj)

    static member GetMethod (meth : MethodInfo) =
        let genericHelper = typeof<Clone>.GetRuntimeMethods() |> Seq.find (fun x -> x.Name = "GetHelper")

        let constructedHelper = genericHelper.MakeGenericMethod ([|typeof<'T>; meth.ReturnType|])

        let ret = constructedHelper.Invoke (null, [|meth|])
        ret :?> Func<'T, obj>

    static member SetHelper<'TTarget, 'TParam> (meth : MethodInfo) =
        let func = meth.CreateDelegate (typeof<Action<'TTarget, 'TParam>>) :?> Action<'TTarget, 'TParam>
        Action<'TTarget, obj> (fun t (o : obj) -> func.Invoke (t, o :?> 'TParam))

    static member SetMethod (meth : MethodInfo) =
        let genericHelper = typeof<Clone>.GetRuntimeMethods() |> Seq.find (fun x -> x.Name = "SetHelper")

        let constructedHelper = genericHelper.MakeGenericMethod ([|typeof<'T>; meth.GetParameters().[0].ParameterType|])
        
        let ret = constructedHelper.Invoke (null, [|meth|])
        ret :?> Action<'T, obj>

    static member GetStructHelper<'TTarget, 'TReturn> (meth : MethodInfo) =
        let func = meth.CreateDelegate (typeof<ByrefFunc<'TTarget, 'TReturn>>) :?> ByrefFunc<'TTarget, 'TReturn>
        ByrefFunc (fun t -> func.Invoke (&t) :> obj)

    static member GetStructMethod (meth : MethodInfo) =
        let genericHelper = typeof<Clone>.GetRuntimeMethods() |> Seq.find (fun x -> x.Name = "GetStructHelper")

        let constructedHelper = genericHelper.MakeGenericMethod ([|typeof<'T>; meth.ReturnType|])

        let ret = constructedHelper.Invoke (null, [|meth|])
        ret :?> ByrefFunc<'T, obj>

    static member SetStructHelper<'TTarget, 'TParam> (meth : MethodInfo) =
        let func = meth.CreateDelegate (typeof<ByrefAction<'TTarget, 'TParam>>) :?> ByrefAction<'TTarget, 'TParam>
        ByrefAction<'TTarget, obj> (fun t (o : obj) -> func.Invoke (&t, o :?> 'TParam))

    static member SetStructMethod (meth : MethodInfo) =
        let genericHelper = typeof<Clone>.GetRuntimeMethods() |> Seq.find (fun x -> x.Name = "SetHelper")

        let constructedHelper = genericHelper.MakeGenericMethod ([|typeof<'T>; meth.GetParameters().[0].ParameterType|])
        
        let ret = constructedHelper.Invoke (null, [|meth|])
        ret :?> ByrefAction<'T, obj>

[<AutoOpen>]
module CloneHelpers =

    let allNestedRuntimeFieldTypes (typ: Type) =
        let f (x: Type) : Type list =
            (x.GetRuntimeFields ())
            |> Seq.map (fun x -> x.FieldType)
            |> Seq.distinctBy (fun x -> x.FullName)
            |> List.ofSeq

        let rec fr (types: Type list) = function
            | [] -> types
            | x :: xs as nested ->
                let typesToCover = types @ nested
                let refs =
                    (f x)
                    |> List.filter (fun x -> not (typesToCover |> List.exists (fun y -> y.FullName = x.FullName)))
                fr (x :: types) (xs @ refs)
                    
        fr [] (f typ)  

    let rec isTypeBlittable (typ: Type) =
        let check (x: Type) = 
            (x.GetTypeInfo().IsValueType && not (x.GetTypeInfo().IsGenericType)) ||
            (x.IsPointer && (x.GenericTypeArguments |> Array.forall (isTypeBlittable)))
        match check typ with
        | false -> false
        | _ -> allNestedRuntimeFieldTypes typ |> List.forall check

    let isImmutableType<'T> (typ : Type) =
        match typ with
        | x when 
            x = typeof<byte> ||
            x = typeof<sbyte> ||
            x = typeof<uint16> ||
            x = typeof<int16> ||
            x = typeof<uint32> ||
            x = typeof<int32> ||
            x = typeof<uint64> ||
            x = typeof<int64> ||
            x = typeof<single> ||
            x = typeof<double> ||
            x = typeof<bool> ||
            isTypeBlittable x ||
            x = typeof<string> -> true
        | _ -> false

    type internal Marker = interface end
    let moduleType = typeof<Marker>.DeclaringType

#if ECS_CLONING_EMIT_IL
    open System.Reflection.Emit

    type GetMethod =
        | Normal of getMethod : MethodInfo
        | Clone of cloningMethod : MethodInfo * getMethod : MethodInfo

    let createGet<'T> (meth : MethodInfo) =
        let typ = meth.ReturnType
        match isImmutableType typ with
        | true -> Normal meth
        | _ -> //failwithf "Component, %s, has an invalid property type. Property Name: %s. Type: %s." typeof<'T>.Name meth.Name typ.Name

            let helperMethods = moduleType.GetRuntimeMethods ()
            let createCloneMeth = helperMethods |> Seq.find (fun x -> x.Name = "createCloneMethod")

            let generic = createCloneMeth.MakeGenericMethod ([|typ|])
            let ret = generic.Invoke (null, [||])
            let ret = ret :?> Delegate
            Clone (ret.Method, meth)

    let createSet<'T> (meth : MethodInfo) = meth

    let createCloneMethod<'T> () =
        let typ = typeof<'T>

        let ctors = typ.GetTypeInfo().DeclaredConstructors

        if ctors.Count () > 1 then
            failwithf "Type, %s, has more than one constructor. Cloneable types can only have one constructor." typ.Name

        let ctor = ctors.ElementAt (0)
        let ctorParams = ctor.GetParameters ()

        let runtimeProps = typ.GetRuntimeProperties ()

        let ctorProps = 
            ctorParams
            |> Seq.map (fun param ->
                let propOpt =
                    runtimeProps 
                    |> Seq.tryFind (fun x -> x.Name.ToLowerInvariant() = param.Name.ToLowerInvariant() && x.PropertyType = param.ParameterType)
                match propOpt with
                | Some prop -> prop
                | _ -> failwithf "Type, %s, has a constructor parameter that doesn't match a property with a getter. Parameter Name: %s. Parameter Type: %s." typ.Name param.Name param.ParameterType.Name
            )
            |> Seq.toArray

        let props =
            runtimeProps
            |> Seq.choose (fun prop ->
                if prop.CanRead && prop.CanWrite && not (ctorProps.Contains prop) && not (prop.Name = "Owner") then
                    Some prop
                else
                    None
            )
            |> Seq.toArray

        let ctorGets =
            ctorProps
            |> Array.map (fun x -> createGet<'T> x.GetMethod)

        let sets =
            props
            |> Array.map (fun x -> createSet<'T> x.SetMethod)

        let gets =
            props
            |> Array.map (fun x -> createGet<'T> x.GetMethod)

        /////////////////////////////////////////////////////
        ()

        let cloneMeth = DynamicMethod ("Clone", typ, [|typ|])
        let il = cloneMeth.GetILGenerator ()

        il.DeclareLocal (typ) |> ignore

        if ctorGets.Length > 0 then
            ctorGets |> Array.iteri (fun i ctorGet ->
                il.Emit (OpCodes.Ldarg_0)
                match ctorGet with
                | Normal ctorGet ->
                    il.Emit (OpCodes.Callvirt, ctorGet)
                | Clone (clone, ctorGet) ->
                    il.Emit (OpCodes.Callvirt, ctorGet)
                    il.Emit (OpCodes.Call, clone)
            )
        il.Emit (OpCodes.Newobj, ctor)
        il.Emit (OpCodes.Stloc_0)

        gets
        |> Array.iteri (fun i get ->
            let set = sets.[i]
            il.Emit (OpCodes.Ldloc_0)
            il.Emit (OpCodes.Ldarg_0)

            match get with
            | Normal get ->
                il.Emit (OpCodes.Callvirt, get)
            | Clone (clone, get) ->
                il.Emit (OpCodes.Callvirt, get)
                il.Emit (OpCodes.Call, clone)

            il.Emit (OpCodes.Callvirt, set)
        )

        il.Emit (OpCodes.Ldloc_0)
        il.Emit (OpCodes.Ret)

        cloneMeth.CreateDelegate (typeof<Func<'T, 'T>>) :?> Func<'T, 'T>
#else
    type GetMethod<'T> =
        | GetReference of Func<'T, obj>
        | GetValue of ByrefFunc<'T, obj>

    type SetMethod<'T> =
        | SetReference of Action<'T, obj>
        | SetValue of ByrefAction<'T, obj>

    let createGet<'T> (meth : MethodInfo) =
        let typ = meth.ReturnType
        match isImmutableType typ with
        | true ->
            if typeof<'T>.GetTypeInfo().IsValueType then
                GetValue (Clone.GetStructMethod<'T> meth)
            else
                GetReference (Clone.GetMethod<'T> meth)
        | _ -> 
            let helperMethods = moduleType.GetRuntimeMethods ()
            let createCloneMeth = helperMethods |> Seq.find (fun x -> x.Name = "createCloneMethodObj")

            let generic = createCloneMeth.MakeGenericMethod ([|typ|])

            let ret = generic.Invoke (null, [||])
            let ret = ret :?> (obj -> obj)

            if typeof<'T>.GetTypeInfo().IsValueType then
                let copy = Clone.GetStructMethod<'T> meth
                GetValue (ByrefFunc<'T, obj> (fun (t : byref<'T>) -> ret (copy.Invoke (&t))))
             else
                let copy = Clone.GetMethod<'T> meth
                GetReference (Func<'T, obj> (fun t -> ret (copy.Invoke (t))))

    let createSet<'T> (meth : MethodInfo) = 
        if typeof<'T>.GetTypeInfo().IsValueType then
            Clone.SetStructMethod<'T> meth
            |> SetValue
        else
            Clone.SetMethod<'T> meth
            |> SetReference

    let createCloneMethodObj<'T> () =
        let typ = typeof<'T>
        let ctors = typ.GetTypeInfo().DeclaredConstructors
        let ctor = ctors.ElementAt (0)
        let ctorParams = ctor.GetParameters ()

        let runtimeProps = typ.GetRuntimeProperties ()

        let ctorProps = 
            ctorParams
            |> Seq.map (fun param ->
                runtimeProps 
                |> Seq.find (fun x -> x.Name.ToLowerInvariant() = param.Name.ToLowerInvariant())
            )
            |> Seq.toArray

        let props =
            runtimeProps
            |> Seq.choose (fun prop ->
                if prop.CanRead && prop.CanWrite && not (ctorProps.Contains prop) && not (prop.Name = "Owner") then
                    Some prop
                else
                    None
            )
            |> Seq.toArray

        let ctorGets =
            ctorProps
            |> Array.map (fun x -> createGet<'T> x.GetMethod)

        let sets =
            props
            |> Array.map (fun x -> createSet<'T> x.SetMethod)

        let gets =
            props
            |> Array.map (fun x -> createGet<'T> x.GetMethod)


        if typeof<'T>.GetTypeInfo().IsValueType then
            let ctorGets =
                ctorGets
                |> Array.map (function
                    | GetValue (x) -> x
                    | _ -> failwith "should not happen"
                )

            let sets =
                sets
                |> Array.map (function
                    | SetValue (x) -> x
                    | _ -> failwith "should not happen"
                )

            let gets =
                gets
                |> Array.map (function
                    | GetValue (x) -> x
                    | _ -> failwith "should not happen"
                )

            fun (comp : obj) ->
                let mutable typedComp = comp :?> 'T
                let finalO = ctor.Invoke (ctorGets |> Array.map (fun x -> x.Invoke (&typedComp)))
                let mutable typedFinalO = finalO :?> 'T
                sets
                |> Array.iteri (fun i setMeth ->
                    let get = gets.[i]
                    setMeth.Invoke (&typedFinalO, (get.Invoke (&typedComp))) |> ignore
                )

                finalO
        else

            let ctorGets =
                ctorGets
                |> Array.map (function
                    | GetReference (x) -> x
                    | _ -> failwith "should not happen"
                )

            let sets =
                sets
                |> Array.map (function
                    | SetReference (x) -> x
                    | _ -> failwith "should not happen"
                )

            let gets =
                gets
                |> Array.map (function
                    | GetReference (x) -> x
                    | _ -> failwith "should not happen"
                )

            fun (comp : obj) ->
                let typedComp = comp :?> 'T
                let finalO = ctor.Invoke (ctorGets |> Array.map (fun x -> x.Invoke (typedComp)))
                let typedFinalO = finalO :?> 'T
                sets
                |> Array.iteri (fun i setMeth ->
                    let get = gets.[i]
                    setMeth.Invoke (typedFinalO, (get.Invoke (typedComp))) |> ignore
                )

                finalO

    let createCloneMethod<'T> () =
        let f = createCloneMethodObj<'T> ()
        Func<'T, 'T> (fun comp -> f (comp :> obj) :?> 'T)
#endif

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

    abstract GetIndex : int -> int

    abstract GetComponent : int -> Component

    abstract TryGetComponent : int -> Component option

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> Component> =
    {
        ComponentRemovedEvent: Event<'T>

        ComponentAddedTriggers : ResizeArray<'T -> unit>

        RemoveComponent: Entity -> unit

        AddComponent: Entity -> 'T -> unit

        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
        Clone : Func<'T, 'T>
        CloneComponent : obj -> obj
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

        member this.GetIndex id = this.IndexLookup.[id]

        member this.GetComponent index = this.Components.Buffer.[index] :> Component

        member this.TryGetComponent entIndex =
            let index = this.IndexLookup.[entIndex]

            if index >= 0 then
                this.Components.Buffer.[index] :> Component
                |> Some
            else
                None

type EntityBuilder = EntityBuilder of (Entity -> EntityManager -> unit)

and [<ReferenceEquality>] EntityManager =
    {
        EventAggregator: EventAggregator

        MaxEntityAmount: int
        Lookup: ConcurrentDictionary<Type, IEntityLookupData>

        ActiveVersions: uint32 []
        EntityComponents : ResizeArray<struct (obj * (obj -> obj) * (Entity -> unit))> []

        mutable nextEntityIndex: int
        RemovedEntityQueue: Queue<Entity>

        EntitySpawnedEvent: Event<Entity>
        EntityDestroyedEvent: Event<Entity>

        mutable CurrentIterations: int
        PendingQueue: Queue<unit -> unit>
    }

    static member Create (eventManager: EventAggregator, maxEntityAmount) =
        if maxEntityAmount <= 0 then
            failwith "Max entity amount must be greater than 0."

        let maxEntityAmount = maxEntityAmount + 1
        let lookup = ConcurrentDictionary<Type, IEntityLookupData> ()

        let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)

        let mutable nextEntityIndex = 1
        let removedEntityQueue = Queue<Entity> () 

        let entitySpawnedEvent = eventManager.GetEntitySpawnedEvent ()
        let entityDestroyedEvent = eventManager.GetEntityDestroyedEvent ()

        {
            EventAggregator = eventManager
            MaxEntityAmount = maxEntityAmount
            Lookup = lookup
            ActiveVersions = activeVersions
            EntityComponents = Array.init maxEntityAmount (fun _ -> ResizeArray ())
            nextEntityIndex = nextEntityIndex
            RemovedEntityQueue = removedEntityQueue
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
                let clone = createCloneMethod<'T> ()
                let data =
                    {
                        ComponentRemovedEvent = this.EventAggregator.GetComponentRemovedEvent<'T> ()

                        ComponentAddedTriggers = triggers

                        RemoveComponent = fun entity -> this.Remove<'T> entity
                        AddComponent = fun ent comp -> this.Add<'T> (ent, comp)

                        IndexLookup = Array.init this.MaxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                        Entities = UnsafeResizeArray.Create this.MaxEntityAmount
                        Components = UnsafeResizeArray.Create this.MaxEntityAmount
                        Clone = clone
                        CloneComponent = fun o -> clone.Invoke (o :?> 'T) :> obj
                    }

                data :> IEntityLookupData

            this.Lookup.GetOrAdd(t, factory) :?> EntityLookupData<'T>

    member inline this.Iterate<'T when 'T :> Component> (f) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let entities = data.Entities.Buffer
            let components = data.Components.Buffer

            let inline iter i =
                let entity = entities.[i]
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
            let components1 = data1.Components.Buffer
            let components2 = data2.Components.Buffer
            let lookup1 = data1.IndexLookup
            let lookup2 = data2.IndexLookup

            let inline iter i =
                let entity = entities.[i]
    
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

            let entities = data.Entities.Buffer
            let components1 = data1.Components.Buffer
            let components2 = data2.Components.Buffer
            let components3 = data3.Components.Buffer
            let lookup1 = data1.IndexLookup
            let lookup2 = data2.IndexLookup
            let lookup3 = data3.IndexLookup

            let inline iter i =
                let entity = entities.[i]
    
                let comp1Index = lookup1.[entity.Index]
                let comp2Index = lookup2.[entity.Index]
                let comp3Index = lookup3.[entity.Index]

                if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 then
                    f entity components1.[comp1Index] components2.[comp2Index] components3.[comp3Index]
    
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

            let entities = data.Entities.Buffer
            let components1 = data1.Components.Buffer
            let components2 = data2.Components.Buffer
            let components3 = data3.Components.Buffer
            let components4 = data4.Components.Buffer
            let lookup1 = data1.IndexLookup
            let lookup2 = data2.IndexLookup
            let lookup3 = data3.IndexLookup
            let lookup4 = data4.IndexLookup

            let inline iter i =
                let entity = entities.[i]
    
                let comp1Index = lookup1.[entity.Index]
                let comp2Index = lookup2.[entity.Index]
                let comp3Index = lookup3.[entity.Index]
                let comp4Index = lookup4.[entity.Index]

                if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && comp4Index >= 0 then
                    f entity components1.[comp1Index] components2.[comp2Index] components3.[comp3Index] components4.[comp4Index]
    
            for i = 0 to data.Entities.Count - 1 do iter i
            //Parallel.For (0, data.Entities.Count - 1, fun i _ -> iter i) |> ignore

    // Components


    member inline this.AddInline2<'T when 'T :> Component> 
            (
                entity: Entity, 
                comp : 'T, 
                indexLookup : int [], 
                components : UnsafeResizeArray<'T>,
                entities : UnsafeResizeArray<Entity>,
                componentAddedTriggers : ResizeArray<('T -> unit)>,
                removeComponent : Entity -> unit,
                cloneComponent : obj -> obj
            ) =
        if indexLookup.[entity.Index] >= 0 then
            Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, already added to {1}.", typeof<'T>.Name, entity))
        else
            if not comp.Owner.IsZero then
                Debug.WriteLine (String.Format ("ECS WARNING: Component, {0}, has already been assigned to {1}.", typeof<'T>.Name, comp.Owner))
            else
                comp.Owner <- entity

                this.EntityComponents.[entity.Index].Add struct (comp :> obj, cloneComponent, removeComponent)

                indexLookup.[entity.Index] <- entities.Count

                components.Add comp
                entities.Add entity

                for i = 0 to componentAddedTriggers.Count - 1 do
                    let f = componentAddedTriggers.[i]
                    f comp

    member inline this.AddInline<'T when 'T :> Component> (entity: Entity, comp: 'T) =
        let data = this.GetEntityLookupData<'T> ()

        this.AddInline2<'T> (entity, comp, data.IndexLookup, data.Components, data.Entities, data.ComponentAddedTriggers, data.RemoveComponent, data.CloneComponent)

    member this.Add<'T when 'T :> Component> (entity: Entity, comp: 'T) =
        if this.CurrentIterations > 0 then
            this.PendingQueue.Enqueue (fun () -> this.Add (entity, comp))
        else
            if this.IsValidEntity entity then
                this.AddInline<'T> (entity, comp)
            else
                Debug.WriteLine (String.Format ("ECS WARNING: {0} is invalid. Cannot add component, {1}", entity, typeof<'T>.Name))

    member this.Remove<'T when 'T :> Component> (entity: Entity) =
        if this.CurrentIterations > 0 then
            this.PendingQueue.Enqueue (fun () -> this.Remove<'T> (entity))
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    let index = data.IndexLookup.[entity.Index]
                    let swappingEntity = data.Entities.LastItem

                    let comp = data.Components.Buffer.[index]

                    data.Entities.SwapRemoveAt index
                    data.Components.SwapRemoveAt index

                    data.IndexLookup.[entity.Index] <- -1

                    if not (entity.Index.Equals swappingEntity.Index) then
                        data.IndexLookup.[swappingEntity.Index] <- index

                    data.ComponentRemovedEvent.Trigger (comp)
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

            this.EntitySpawnedEvent.Trigger (entity)

            entity

    member this.Destroy (entity: Entity) =
        if this.CurrentIterations > 0 then
            this.PendingQueue.Enqueue (fun () -> this.Destroy entity)
        else
            if this.IsValidEntity entity then
                let entComps = this.EntityComponents.[entity.Index]
                entComps |> Seq.iter (fun struct (_, _, remove) -> remove entity)
                entComps.Clear ()
                entComps.TrimExcess ()

                this.RemovedEntityQueue.Enqueue entity  

                this.ActiveVersions.[entity.Index] <- 0u

                this.EntityDestroyedEvent.Trigger (entity)
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

    member this.TryGet<'T when 'T :> Component> (entity: Entity, [<Out>] comp : byref<'T>) : bool =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if this.IsValidEntity entity then
                let index = data.IndexLookup.[entity.Index]
                if index >= 0 then
                    comp <- data.Components.Buffer.[index]
                    true
                else
                    false
            else
                false
        else
            false

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

    member this.ForEach<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> (f : Entity -> 'T1 -> 'T2 -> unit) : unit =
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

    member this.DestroyAll () =
        this.ActiveVersions
        |> Array.iteri (fun index version ->
            if version > 0u then
                this.Destroy (Entity (index, version))
        )

    member this.Save () =

        let fullEntities = Array.zeroCreate this.MaxEntityAmount

        this.EntityComponents
        |> Array.Parallel.iteri (fun i comps ->
            if comps.Count > 0 then
                let clones = ResizeArray (comps.Count)
                for i = 0 to comps.Count - 1 do
                    let struct (comp, clone, remove) = comps.[i]
                    clones.Add (clone comp)

                fullEntities.[i] <- { Entity = Entity (i, this.ActiveVersions.[i]); Components = clones }
        )
        ""
        //let settings = JsonSerializerSettings ()
        //settings.ContractResolver <- EcsContractResolver ()
        //settings.Formatting <- Formatting.Indented

        //JsonConvert.SerializeObject (fullEntities |> Seq.filter (fun x -> obj.ReferenceEquals (x, null) |> not), settings)

    member this.Load (json : string) =
        ()
        //let hasData = String.IsNullOrEmpty json |> not
        //if hasData then
            //let emTyp = typeof<EntityManager>

            //let settings = JsonSerializerSettings ()
            //settings.ContractResolver <- EcsContractResolver ()
            //settings.TypeNameHandling <- TypeNameHandling.All
            //let fullEntities = JsonConvert.DeserializeObject<SerializedEntity seq> (json, settings)

            //fullEntities
            //|> Seq.iter (fun serialized ->
            //    let ent = this.Spawn ()

            //    serialized.Components
            //    |> Seq.iter (fun comp ->
            //        let meth = 
            //            let meth =
            //                emTyp.GetRuntimeMethods() 
            //                |> Seq.find(fun x -> x.Name = "Add")
            //            meth.MakeGenericMethod (comp.GetType ())
            //        meth.Invoke (this, [| ent; comp |]) |> ignore
            //    )
            //)


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
