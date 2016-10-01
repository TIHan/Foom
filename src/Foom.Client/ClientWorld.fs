namespace Foom.Client

open Foom.Ecs
open Foom.Renderer

type ClientWorld =
    {
        subworld: Subworld
        entity: Entity
    }

    member this.Entity = this.entity

    member this.DestroyEntities () =
        this.subworld.DestroyEntities ()

    static member Create (subworld, entity) =
        {
            subworld = subworld
            entity = entity
        }
