open System
open System.IO
open System.Diagnostics
open System.Numerics

open Foom.Client
open Foom.Ecs
open Foom.Ecs.World
open Foom.Common.Components

[<Literal>]
let DoomUnit = 64.f

[<EntryPoint>]
let main argv =
    let world = World (65536)

    let client = Client.init (world)

    GameLoop.start 30.
        (fun time interval ->
            GC.Collect (0)
            Input.pollEvents ()
            let inputState = Input.getState ()

            inputState.Events
            |> List.iter (function
                | MouseWheelScrolled (x, y) ->

                    world.EntityManager.TryFind<CameraComponent> (fun _ _ -> true)
                    |> Option.iter (fun (ent, cameraComp) ->
                        world.EntityManager.TryGet<TransformComponent> (ent)
                        |> Option.iter (fun (transformComp) ->

                            let zoom =
                                if y < 0 then DoomUnit * 5.f else DoomUnit * -5.f

                            transformComp.Position <- transformComp.Position + Vector3 (0.f, 0.f, zoom)
                            transformComp.RotateX (zoom / 64.f)
                        )
                     )

                | _ -> ()
            )
        )
        (fun t ->
            Client.draw t client client
        )
    0
