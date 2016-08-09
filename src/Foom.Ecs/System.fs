namespace Foom.Ecs

open System

[<AbstractClass>]
type EntitySystemEvent () = 

    abstract Handle : EventManager -> IDisposable

[<AutoOpen>]
module EntityEventOperators =

    let handle<'T when 'T :> IEntitySystemEvent> (f: 'T -> unit) = 
        {
            new EntitySystemEvent () with

                override this.Handle eventManager =
                    eventManager.GetEvent<'T>().Publish.Subscribe (f)
        }

type InitializeResult<'Update> =
    | Update of name: string * ('Update -> unit)
    | Merged of name: string * IEntitySystem<'Update> list
    | NoResult

and IEntitySystem<'Update> =

    abstract Events : EntitySystemEvent list

    abstract Shutdown : unit -> unit

    abstract Initialize : EntityManager -> EventManager -> InitializeResult<'Update>

type EntitySystem<'Update> = EntitySystem of (unit -> IEntitySystem<'Update>)

[<RequireQualifiedAccess>]
module EntitySystem =

    let build (f: unit -> EntitySystem<_>) =
        EntitySystem (fun () ->
            match f () with
            | EntitySystem g -> g ()
        )

    let merge name (systems: EntitySystem<'T> list) =
        EntitySystem (
            fun () ->
                let systems =
                    systems
                    |> List.map (fun (EntitySystem sysCtor) -> sysCtor ())
                {
                    new IEntitySystem<'T> with

                        member this.Events  =
                            systems
                            |> List.map (fun sys -> sys.Events)
                            |> List.reduce (@)

                        member this.Shutdown () =
                            systems
                            |> List.iter (fun sys -> sys.Shutdown ())

                        member this.Initialize entityManager eventManager =
                            Merged (name, systems)
                }
        )

module Systems =

    let system name init =
        EntitySystem (fun () -> 
            {
                new IEntitySystem<'Update> with

                    member this.Events = []

                    member this.Shutdown () = ()

                    member this.Initialize entityManager eventManager =
                        Update (name, init entityManager eventManager)
            }
        )

    let eventListener<'Update, 'Event when 'Event :> IEntitySystemEvent and 'Event : not struct> f =
        EntitySystem (fun () ->
            {
                new IEntitySystem<'Update> with

                    member this.Events = [ handle<'Event> f ]

                    member this.Shutdown () = ()

                    member this.Initialize _ _ = NoResult
            }
        )

    let getTypeName (t: Type) =
        let name = t.Name
        let index = name.IndexOf ('`')
        if index = -1 then name else name.Substring (0, index)

    let getTypeNameWithTypeArgNames (t: Type) =
        let rec getTypeArgNames (t: Type) typeArgNames = function
            | [] -> 
                let name = getTypeName t
                let typeArgNames = typeArgNames |> List.rev

                match typeArgNames with
                | [] -> name
                | _ ->
                    sprintf "%s<%s>" name
                        (
                            typeArgNames
                            |> List.reduce (fun x y -> x + ", " + y)
                        )

            | (typeArg: Type) :: typeArgs ->
                let typeArgName = 
                    getTypeArgNames typeArg [] (typeArg.GenericTypeArguments |> List.ofArray)

                getTypeArgNames t (typeArgName :: typeArgNames) typeArgs

        getTypeArgNames t [] (t.GenericTypeArguments |> List.ofArray)

    let eventQueue<'Update, 'Event when 'Event :> IEntitySystemEvent and 'Event : not struct> f =
        EntitySystem (fun () ->
            let name = sprintf "Event Queue of `%s`" (getTypeNameWithTypeArgNames typeof<'Event>)
            let queue = System.Collections.Concurrent.ConcurrentQueue ()
            {
                new IEntitySystem<'Update> with

                    member this.Events = [ handle<'Event> queue.Enqueue ]

                    member this.Shutdown () = ()

                    member this.Initialize entityManager eventManager =
                        let f = f entityManager eventManager
                        Update (
                            name,
                            fun data ->
                                let mutable event = Unchecked.defaultof<'Event>
                                while queue.TryDequeue (&event) do
                                    f data event
                        )
            }
        )

    let shutdown<'Update> f =
        EntitySystem (fun () ->
            {
                new IEntitySystem<'Update> with

                    member this.Events = []

                    member this.Shutdown () = f ()

                    member this.Initialize _ _ = NoResult
            }
        )
