[<RequireQualifiedAccess>]
module Foom.Client.Client

open Foom.Ecs
open Foom.Renderer
open System.IO
open System.Numerics

open Foom.Renderer
open Foom.Client.Sprite
open Foom.Client.Sky
open Foom.Game.Assets

type SpriteInput (program: ShaderProgram) =
    inherit MeshInput (program)

    member val Center = program.CreateVertexAttributeVector3 ("in_center")

    member val Positions = program.CreateInstanceAttributeVector3 ("instance_position")

    member val LightLevels = program.CreateInstanceAttributeVector4 ("instance_lightLevel")


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

            do! runProgramWithMesh "Sprite" SpriteInput noOutput (fun (sprite: Sprite) input draw ->
                input.Center.Set sprite.Center
                input.Positions.Set sprite.Positions
                input.LightLevels.Set sprite.LightLevels
                draw ()
            )
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
    let clientSystemUpdate = ClientSystem.create app clientWorld |> clientSubworld.AddBehavior

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom1.wad", "e1m1"))
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