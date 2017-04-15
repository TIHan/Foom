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

#if __IOS__ || __ANDROID__
let documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments)
#endif

let start (input : IInput) (gl : IGL) (invoke: Task ref) =
    let assetLoader =
        {
            new IAssetLoader with

                member this.LoadTextureFile (assetPath) =
#if __IOS__ || __ANDROID__
                    try
                        new SkiaTextureFile (Path.Combine (documents, assetPath)) :> TextureFile
                    with | _ ->
                        new SkiaTextureFile (assetPath) :> TextureFile
#else
                    new SkiaTextureFile (assetPath) :> TextureFile
#endif

        }
#if __ANDROID__
    let assets = Android.App.Application.Context.Assets
    let loadTextFile = (fun filePath -> 
        let mutable content = ""
        use sr = new StreamReader (assets.Open (filePath))
        content <- sr.ReadToEnd ()
        content
    )
#else
    let loadTextFile = (fun filePath -> File.ReadAllText filePath)
#endif
    let openWad = (fun name -> System.IO.File.Open (name, FileMode.Open, FileAccess.Read) :> Stream)
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
    let gameWindow = new GameWindow (1280, 720, GraphicsMode.Default, "Foommmmm", GameWindowFlags.FixedWindow, DisplayDevice.Default, 3, 2, GraphicsContextFlags.Default)
    let app = Backend.init ()
    let gl = OpenTKGL (fun () -> Backend.draw app)
    let input = DesktopInput (app.Window)
    let (preUpdate, update, render) = start input gl (new Task (fun () -> ()) |> ref)
    GameLoop.start 30. preUpdate update render
    0
#endif
