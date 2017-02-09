[<RequireQualifiedAccess>]
module Foom.Client.Client

open Foom.Ecs
open Foom.Renderer

let init (world: World) =
    let app = Backend.init ()
    let renderSystem = 
        app
        |> RendererSystem.create
            [
                ("Sprite",
                    (fun em ent renderer ->
                        match em.TryGet<RendererSystem.SpriteComponent> (ent), em.TryGet<RendererSystem.MeshRenderComponent> (ent) with
                        | Some spriteComp, Some meshRenderComp ->
                            let vertices = meshRenderComp.RenderInfo.MeshInfo.Position

                            let center =
                                vertices
                                |> Seq.chunkBySize 6
                                |> Seq.map (fun quadVerts ->
                                    let min = 
                                        quadVerts
                                        |> Array.sortBy (fun x -> x.X)
                                        |> Array.sortBy (fun x -> x.Z)
                                        |> Array.head
                                    let max =
                                        quadVerts
                                        |> Array.sortByDescending (fun x -> x.X)
                                        |> Array.sortByDescending (fun x -> x.Z)
                                        |> Array.head
                                    let mid = min + ((max - min) / 2.f)
                                    Array.init quadVerts.Length (fun _ -> mid)
                                )
                                |> Seq.reduce Array.append

                            em.Add (ent, RendererSystem.SpriteComponent (center))

                            spriteComp :> obj
                        | _ -> null
                    ),
                    (fun shaderProgram ->

                        let in_center = shaderProgram.CreateVertexAttributeVector3 ("in_center")

                        fun o run ->
                            match o with
                            | null -> ()
                            | :? RendererSystem.SpriteComponent as spriteComp -> in_center.Set spriteComp.Center
                            | _ -> ()

                            run RenderPass.Depth
                    )
                )
            ]

    let renderSystemUpdate = world.AddBehavior (Behavior.merge [ renderSystem ])
    let inputUpdate = world.AddBehavior (Camera.playerUpdate app)

    let clientSubworld = world.CreateSubworld ()
    let clientWorld = ClientWorld.Create (clientSubworld, world.SpawnEntity ())
    let clientSystemUpdate = ClientSystem.create app clientWorld |> clientSubworld.AddBehavior

    world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom.wad", "e1m1"))
   // world.Publish (ClientSystem.LoadWadAndLevelRequested ("doom2.wad", "map10"))

    {
        Window = app.Window
        AlwaysUpdate = fun () -> inputUpdate ()
        Update = clientSystemUpdate
        RenderUpdate = renderSystemUpdate
        ClientWorld = clientWorld
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =
    curr.RenderUpdate (currentTime, t)