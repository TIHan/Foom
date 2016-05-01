namespace Foom

open Urho
open Urho.IO
open Urho.Gui

open Foom.Wad
open Foom.Wad.Level
open Foom.Wad.Geometry

type App () =
    inherit Urho.Application (ApplicationOptions ("Data")) 

    let mutable yaw = 0.f
    let mutable pitch = 0.f
    let touchSensitivity = 2.f
    let mutable camera = Unchecked.defaultof<Camera>

    override this.Start () = 
        base.Start ()

        let file = this.ResourceCache.GetFile ("freedoom1.wad", false)
        let mutable bytes = Array.zeroCreate<byte> 1
        let mutable length = 0
        while file.Read ((&&bytes.[0]) |> FSharp.NativeInterop.NativePtr.toNativeInt, 1u) <> 0u do 
            length <- length + 1
        file.Dispose ()

        let file = this.ResourceCache.GetFile ("freedoom1.wad", false)
        let mutable bytes = Array.zeroCreate<byte> length
        while file.Read ((&&bytes.[0]) |> FSharp.NativeInterop.NativePtr.toNativeInt, uint32 length) <> 0u do ()
        file.Dispose ()

        use ms = new System.IO.MemoryStream (bytes)

        let wad = Wad.create ms |> Async.RunSynchronously

        let e1m1Level = Wad.findLevel "e1m1" wad |> Async.RunSynchronously

        let polygonTrees =
            e1m1Level.Sectors
            |> Array.map (fun sector -> LinedefTracer.run sector.Linedefs)
            |> Array.map (fun sectorLinedefs ->
                let polygons =
                    sectorLinedefs
                    |> List.map Polygon.ofLinedefs
                
                let rec f (polygons: Polygon list) (trees: PolygonTree list) =
                    match trees, polygons with
                    | _, [] ->
                        trees

                    | [], poly :: polygons -> 
                        f polygons [PolygonTree (poly, [])]

                    | _, poly :: polygons ->
                        trees
                        |> List.map (fun (PolygonTree (poly2, children) as tree) ->
                            if (Polygon.isPointInside (Polygon.vertices poly).[0] poly2) then
                                [PolygonTree (poly2, f [poly] children)]
                            elif (Polygon.isPointInside (Polygon.vertices poly2).[0] poly) then
                                [PolygonTree (poly, [tree])]
                            else
                                PolygonTree (poly, []) :: [tree]
                        )
                        |> List.reduce (@)
                        |> f polygons
                f polygons []
            )
            |> Array.reduce (@)

        this.Renderer.DrawDebugGeometry (true)
        let scene = new Scene ()
        scene.CreateComponent<Octree> () |> ignore
        let debugRenderer = scene.CreateComponent<DebugRenderer> ()

        // Camera
        let cameraNode = scene.CreateChild ()
        cameraNode.Position <- (new Vector3 (0.0f, 0.0f, -0.2f))
        camera <- cameraNode.CreateComponent<Camera> ()

        this.Renderer.SetViewport (0u, new Viewport (this.Context, scene, cameraNode.GetComponent<Camera> (), null))

        // Lights:
        let lightNode1 = scene.CreateChild ()
        lightNode1.Position <- new Vector3 (0.f, -5.f, -40.f)
        lightNode1.AddComponent (new Light (Range = 120.f, Brightness = 1.5f))

        let lightNode2 = scene.CreateChild ()
        lightNode2.Position <- new Vector3 (10.f, 15.f, -12.f)
        lightNode2.AddComponent (new Light (Range = 30.0f, Brightness = 1.5f))

        let cache = this.ResourceCache
        let helloText = new Text ()

        helloText.Value <- "Foom - Urho, F# and Doom"
        helloText.HorizontalAlignment <- HorizontalAlignment.Center
        helloText.VerticalAlignment <- VerticalAlignment.Top

        helloText.SetColor (new Color (0.f, 1.f, 0.f))
        let f = cache.GetFont ("Fonts/Anonymous Pro.ttf")

        helloText.SetFont (f, 30) |> ignore

        this.UI.Root.AddChild (helloText)

        this.Engine.SubscribeToPostRenderUpdate (fun _ ->
            polygonTrees
            |> List.iter (fun (PolygonTree (Polygon (vertices), _)) ->
                let mutable i = 0
                while (i < vertices.Length) do
                    let v1 = vertices.[i]
                    i <- i + 1
                    let v2 = 
                        if i < vertices.Length then
                            vertices.[i]
                        else
                            vertices.[0]

                    debugRenderer.AddLine (new Vector3 (v1.X, v1.Y, 0.f), new Vector3 (v2.X, v2.Y, 0.f), Color.Red, true)
            )
        ) |> ignore

    member this.MoveCameraByTouches (timeStep: float32) =
        let input = this.Input

        for i = 0 to int input.NumTouches - 1 do
            let state = input.GetTouch (uint32 i)
            if (state.TouchedElement = null) then
                if (state.Delta.X <> 0 || state.Delta.Y <> 0) then
                    yaw <- yaw + touchSensitivity * camera.Fov / (float32 this.Graphics.Height) * float32 state.Delta.X
                    pitch <- pitch + touchSensitivity * camera.Fov / (float32 this.Graphics.Height) * float32 state.Delta.Y
                    camera.Node.Rotation <- new Quaternion (pitch, yaw, 0.f);

    override this.OnUpdate timeStep =
        this.MoveCameraByTouches (timeStep)