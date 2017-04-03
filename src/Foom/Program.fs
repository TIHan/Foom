#if __IOS__
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

type NoInput () =

    interface IInput with

        member x.PollEvents () =
            ()

        member x.GetMousePosition () =
            MousePosition ()

        member x.GetState () =
            { Events = [] }

open OpenTK
open OpenTK.Graphics

let start f (invoke: Task ref) =
#if __IOS__
    let gl = OpenTKGL (f)
    let input = NoInput ()
#else
    let gameWindow = new GameWindow (1280, 720, GraphicsMode.Default, "Foommmmm", GameWindowFlags.FixedWindow, DisplayDevice.Default, 3, 2, GraphicsContextFlags.Default)
    let app = Backend.init ()
   // let gl = DesktopGL (app)
  //  let input = DesktopInput (app.Window)
  //  let gameWindow = new GameWindow (1280, 720, GraphicsMode.Default, "Foom", GameWindowFlags.FixedWindow, DisplayDevice.Default, 3, 2, GraphicsContextFlags.Default)
    let gl = OpenTKGL (fun () -> Backend.draw app)
    let input = DesktopInput (app.Window)//NoInput ()
#endif
    let assetLoader =
        {
            new IAssetLoader with

                member this.LoadTextureFile (assetPath) =
#if __IOS__
                    new iOSTextureFile (assetPath) :> TextureFile
#else
                    new SkiaTextureFile (assetPath) :> TextureFile
#endif

        }

    let loadTextFile = (fun filePath -> File.ReadAllText filePath)
    let openWad = (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)
    let exportTextures =
        (fun wad _ ->
#if __IOS__
            ()
#else
            wad |> exportFlatTextures
            wad |> exportTextures
            wad |> exportSpriteTextures
#endif
        )

    let client = Client.init (printfn "%s") gl assetLoader loadTextFile openWad exportTextures input world

    let stopwatch = System.Diagnostics.Stopwatch ()

    GameLoop.start 30.
        client.AlwaysUpdate
        (fun time interval ->
            stopwatch.Reset ()
            stopwatch.Start ()

            System.Threading.Thread.Sleep(1)
            GC.Collect (0)

            (!invoke).RunSynchronously ()
            invoke := (new Task (fun () -> ()))

            client.Update (
                TimeSpan.FromTicks(time).TotalSeconds |> single, 
                TimeSpan.FromTicks(interval).TotalSeconds |> single
            )

        )
        (fun currentTime t ->
            Client.draw (TimeSpan.FromTicks(currentTime).TotalSeconds |> single) t client client

            if stopwatch.IsRunning then
                stopwatch.Stop ()

                printfn "FPS: %A" (int (1000. / stopwatch.Elapsed.TotalMilliseconds))
        )

#if __IOS__
#else
[<EntryPoint>]
let main argv =
    start id (new Task (fun () -> ()) |> ref)
    0
#endif
