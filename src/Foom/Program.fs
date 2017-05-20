#if __IOS__ || __ANDROID__
module Foom.Program
#else
#endif

open System
open System.IO
open System.Diagnostics
open System.Numerics
open System.Threading.Tasks

open Foom.Client
open Foom.Ecs
open Foom.Network
open Foom.Renderer
open Foom.Input
open Foom.Game.Assets
open Foom.Wad
open Foom.Export

let world = World (65536)

open OpenTK
open OpenTK.Graphics

#if __ANDROID__
open Android.App
open Android.Content.Res
#endif

#if __IOS__
let documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments)
#endif

#if __ANDROID__
let documents = Environment.GetFolderPath (Environment.SpecialFolder.Personal)
#endif

let start (input : IInput) (gl : IGL) (invoke: Task ref) =
#if __ANDROID__
    let assets = Android.App.Application.Context.Assets
#endif

    let assetLoader =
        {
            new IAssetLoader with

                member this.LoadTextureFile (assetPath) =
#if __IOS__ || __ANDROID__

#if __ANDROID__
                    try
                        use stream = assets.Open (assetPath)
                        let copyPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), assetPath)
                        use fs = File.Create (copyPath)
                        stream.CopyTo (fs)
                        fs.Dispose ()
                    with | _ -> ()
#endif
                    try
                        new SkiaTextureFile (Path.Combine (documents, assetPath)) :> TextureFile
                    with | _ ->
                        new SkiaTextureFile (assetPath) :> TextureFile
#else
                    new SkiaTextureFile (assetPath) :> TextureFile
#endif

        }
#if __ANDROID__
    let loadTextFile = (fun filePath -> 
        let mutable content = ""
        use sr = new StreamReader (assets.Open (filePath))
        content <- sr.ReadToEnd ()
        content
    )
#else
    let loadTextFile = (fun filePath -> File.ReadAllText filePath)
#endif

#if __ANDROID__
    let openWad = (fun name -> 
        let copyPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), name)
        let fs = File.Create (copyPath)
        use stream = assets.Open (name)
        stream.CopyTo (fs)
        fs.Dispose ()
        File.Open (copyPath, FileMode.Open, FileAccess.Read) :> Stream
    )
#else
    let openWad = (fun name -> System.IO.File.Open (name, FileMode.Open, FileAccess.Read) :> Stream)
#endif
    let exportTextures =
        (fun wad _ ->
            wad |> exportFlatTextures
            wad |> exportTextures
            wad |> exportSpriteTextures
        )

    let client = Client.init (printfn "%s") gl assetLoader loadTextFile openWad exportTextures input world

    let stopwatch = System.Diagnostics.Stopwatch ()

    let update =
        (fun time interval ->
            stopwatch.Stop ()
            stopwatch.Reset ()
            stopwatch.Start ()

            GC.Collect (0)

            (!invoke).RunSynchronously ()
            invoke := (new Task (fun () -> ()))

            let result = 
                client.Update (
                    TimeSpan.FromTicks(time).TotalSeconds |> single, 
                    TimeSpan.FromTicks(interval).TotalSeconds |> single
                )

            stopwatch.Stop ()

            //printfn "FPS: %A" (int (1000. / stopwatch.Elapsed.TotalMilliseconds))
           // if stopwatch.Elapsed.TotalMilliseconds > 20. then
            printfn "MS: %A" stopwatch.Elapsed.TotalMilliseconds

            result
        )

    let render =
        (fun currentTime t ->
            Client.draw (TimeSpan.FromTicks(currentTime).TotalSeconds |> single) t client client
        )

    (client.AlwaysUpdate, update, render)

#if __IOS__ || __ANDROID__
#else
[<EntryPoint>]
let main argv =
    printfn "Foom - Initialized"
    let gameWindow = new GameWindow (1280, 720, GraphicsMode.Default, "Foommmmm", GameWindowFlags.FixedWindow, DisplayDevice.Default, 3, 2, GraphicsContextFlags.Default)
    let app = Backend.init ()
    let gl = OpenTKGL (fun () -> Backend.draw app)
    let input = DesktopInput (app.Window)
    let (preUpdate, update, render) = start input gl (new Task (fun () -> ()) |> ref)
    GameLoop.start 30. preUpdate update render
    0
#endif
