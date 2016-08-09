namespace Foom.Ecs.World

open System
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Foom.Ecs

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)

    let initEvents = ResizeArray<unit -> unit> ()
    let inits = ResizeArray<unit -> unit> ()

    member this.AddSystem<'Update> (sys: EntitySystem<'Update>) =
        sys.InitEvents eventManager
        sys.Init entityManager eventManager
        
