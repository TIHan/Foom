[<RequireQualifiedAccess>]
module Foom.Client.Client

open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Renderer
open Foom.Client.Sky

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module Pipelines =
    open Pipeline

    let skyWall =
        pipeline {
            do! runProgramWithMesh "TextureMesh" MeshInput noOutput (fun () input draw ->
                draw ()
            )
        }

    let sky =
        pipeline {
            do! runProgramWithMesh "Sky" SkyInput noOutput (fun (sky: Sky) input draw ->
                input.Model.Set sky.Model
                draw ()
            )
        }

    let skyPipeline =
        pipeline {
            do! setStencil skyWall 1
            do! useStencil sky 1
        }

    let worldPipeline =
        pipeline {
            do! runProgramWithMesh "TextureMesh" MeshInput (fun _ -> ()) (fun () input draw ->
                draw ()
            )

            do! Sprite.pipeline
        }

    let renderPipeline =
        pipeline {

            do! clear
            do! runSubPipeline "World"
            do! runSubPipeline "Sky"

        }

let init (world: World) =
    let app = Backend.init ()
    let gl = DesktopGL (app)

    let assetLoader =
        {
            new IAssetLoader with

                member this.LoadTextureFile (assetPath) =
                    new BitmapTextureFile (assetPath) :> TextureFile
        }

    let am = AssetManager (assetLoader)
    let renderSystem = 
        RendererSystem.create
            Pipelines.renderPipeline
            [
                ("World", Pipelines.worldPipeline)
                ("Sky", Pipelines.skyPipeline)
            ]
            gl
            (fun filePath -> File.ReadAllText filePath |> System.Text.Encoding.UTF8.GetBytes)
            am
           

    let renderSystemUpdate = world.AddBehavior (Behavior.merge [ renderSystem ])

    let clientSubworld = world.CreateSubworld ()
    let clientWorld = ClientWorld.Create (clientSubworld, world.SpawnEntity ())
    let clientSystemUpdate = ClientSystem.create app clientWorld am |> clientSubworld.AddBehavior

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom1.wad", "e1m3"))
   // world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom2.wad", "map10"))

    let willQuit = ref false
    let inputUpdate = world.AddBehavior (Player.preUpdate willQuit app)

    {
        Window = app.Window
        AlwaysUpdate = fun () -> inputUpdate ()
        Update = fun x ->
            clientSystemUpdate x
            !willQuit
        RenderUpdate = renderSystemUpdate
        ClientWorld = clientWorld
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =
    curr.RenderUpdate (currentTime, t)