﻿[<RequireQualifiedAccess>]
module Foom.Client.Client

open Foom.Ecs
open Foom.Renderer

let init (world: World) =
    let app = Renderer.init ()
    let renderSystem = RendererSystem.create (app)
    let renderSystemUpdate = world.AddESystem renderSystem

    let clientSubworld = world.CreateSubworld ()
    let clientWorld = ClientWorld.Create (clientSubworld, world.SpawnEntity ())
    let clientSystemUpdate = ClientSystem.create app clientWorld |> clientSubworld.AddESystem

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom.wad", "e1m1"))
   // world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom2.wad", "map01"))

    {
        Window = app.Window
        Update = clientSystemUpdate
        RenderUpdate = renderSystemUpdate
        ClientWorld = clientWorld
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =
    curr.RenderUpdate (currentTime, t)