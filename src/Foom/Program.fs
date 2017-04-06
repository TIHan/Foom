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
                    new SkiaTextureFile (assetPath) :> TextureFile

        }

    let loadTextFile = (fun filePath -> File.ReadAllText filePath)
    let openWad = (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)
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

#if __IOS__
#else
[<EntryPoint>]
let main argv =
    let (preUpdate, update, render) = start id (new Task (fun () -> ()) |> ref)
    GameLoop.start 30. preUpdate update render
    0
#endif
