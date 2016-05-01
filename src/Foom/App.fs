namespace Foom

open Urho
open Urho.Gui

type App (o : ApplicationOptions) =
    inherit Urho.Application (o) 

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