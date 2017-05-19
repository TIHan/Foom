namespace Foom.Game.Level

open Foom.Ecs

[<Sealed>]
type LevelWorld (world : World) =

    let updates = ResizeArray ()

    member val Subworld = world.CreateSubworld ()

    member this.AddBehaviors behaviors =
        behaviors
        |> Seq.map this.Subworld.AddBehavior
        |> Seq.iter updates.Add

    member this.Update data =
        for i = 0 to updates.Count - 1 do
            let update = updates.[i]
            update data
