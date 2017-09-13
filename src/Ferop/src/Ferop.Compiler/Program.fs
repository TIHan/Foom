open Ferop

[<EntryPoint>]
let main argv = 
    argv
    |> Seq.iter (fun x -> Ferop.run x |> ignore)
    //Ferop.run "Foom.Renderer.Desktop.dll"
    //Ferop.run "Foom.Input.Desktop.dll"
    0
