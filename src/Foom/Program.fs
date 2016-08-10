open System
open System.IO
open System.Diagnostics
open System.Numerics

open Foom.Client

[<EntryPoint>]
let main argv =
    let client = Client.init ()

    GameLoop.start 30.
        (fun time interval ->
            GC.Collect (0)
        )
        (fun t ->
            Client.draw t client client
        )
    0
