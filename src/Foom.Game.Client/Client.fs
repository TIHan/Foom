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
            do! drawGroup RenderGroup.World
            do! drawGroup RenderGroup.Sky

        }

let init print (gl: IGL) (assetLoader: IAssetLoader) loadTextFile openWad exportTextures input (world: World) =
   // let app = Backend.init ()

    //let assetLoader =
    //    {
    //        new IAssetLoader with

    //            member this.LoadTextureFile (assetPath) =
    //                new BitmapTextureFile (assetPath) :> TextureFile
    //    }

    let am = AssetManager (assetLoader)
    let renderSystem = 
        RendererSystem.create
            Pipelines.renderPipeline
            [
                (RenderGroup.World, Pipelines.worldPipeline)
                (RenderGroup.Sky, Pipelines.skyPipeline)
            ]
            gl
            loadTextFile
           // (fun filePath -> File.ReadAllText filePath |> System.Text.Encoding.UTF8.GetBytes)
            am
           

    let renderSystemUpdate = world.AddBehavior (Behavior.merge [ renderSystem ])

    let clientSubworld = world.CreateSubworld ()
    let clientWorld = ClientWorld.Create (clientSubworld, world.SpawnEntity ())
    let clientSystemUpdate = ClientSystem.create openWad exportTextures clientWorld am |> clientSubworld.AddBehavior

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("DOOM1.WAD", "e1m1"))
   // world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom2.wad", "map10"))

    let willQuit = ref false
    let inputUpdate = world.AddBehavior (Player.preUpdate print willQuit input)

    {
        AlwaysUpdate = fun () -> inputUpdate ()
        Update = fun x ->
            clientSystemUpdate x
            !willQuit
        RenderUpdate = renderSystemUpdate
        ClientWorld = clientWorld
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =
    curr.RenderUpdate (currentTime, t)