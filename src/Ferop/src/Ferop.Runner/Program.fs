open Ferop

[<EntryPoint>]
let main argv = 
    Ferop.run "Foom.Renderer.Desktop.dll"
    Ferop.run "Foom.Input.Desktop.dll"
    0
