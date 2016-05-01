namespace Foom

open Urho
open Urho.IO
open Urho.Gui

open Foom.Wad
open Foom.Wad.Level

type App () =
    inherit Urho.Application (ApplicationOptions ("Data")) 

    override this.Start () = 
        let cache = this.ResourceCache
        let helloText = new Text ()

        helloText.Value <- "Foom - F# and Doom"
        helloText.HorizontalAlignment <- HorizontalAlignment.Center
        helloText.VerticalAlignment <- VerticalAlignment.Center

        helloText.SetColor (new Color (0.f, 1.f, 0.f))
        let f = cache.GetFont ("Fonts/Anonymous Pro.ttf")

        helloText.SetFont (f, 30) |> ignore

        this.UI.Root.AddChild (helloText)

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

        ()