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

let init print (gl: IGL) (assetLoader: IAssetLoader) loadTextFile openWad exportTextures input (world: World) =
    let am = AssetManager (assetLoader)
    let renderSystem = 
        RendererSystem.create
            gl
            loadTextFile
            am
           

    let renderSystemUpdate = world.AddBehavior (Behavior.Merge [ renderSystem ])

    let clientSubworld = world.CreateSubworld ()
    let clientWorld = ClientWorld.Create (clientSubworld, world.SpawnEntity ())
    let clientSystemUpdate = ClientSystem.create openWad exportTextures clientWorld am |> clientSubworld.AddBehavior

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("DOOM1.WAD", "e1m1"))

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