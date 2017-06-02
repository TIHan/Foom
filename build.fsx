#r @"packages/FAKE/tools/FakeLib.dll"
#r @"build/Ferop.dll"
#r @"build/Ferop.Core.dll"

open Fake
open Ferop

Target "DesktopDebug" (fun _ ->
    MSBuildDebug "build" "Build" [ "src/Foom/Foom.fsproj" ] |> ignore
)

Target "Ferop" (fun _ ->

    Ferop.run "build/Foom.Renderer.Desktop.dll" |> ignore
    Ferop.run "build/Foom.Input.Desktop.dll" |> ignore
)

RunTargetOrDefault "DesktopDebug"