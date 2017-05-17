#r @"packages/FAKE/tools/FakeLib.dll"
#r @"build/Ferop.dll"
#r @"build/Ferop.Core.dll"
open Fake
open Ferop

Target "Default" (fun _ ->
    Ferop.run "build/Foom.Renderer.Desktop.dll" |> ignore
    Ferop.run "build/Foom.Input.Desktop.dll" |> ignore
)

Target "Ferop" (fun _ ->
    !! "src/Ferop/src/Ferop.Core/Ferop.Core.fsproj"
    |> MSBuildDebug "build" "Build"
    |> Log "FeropBuild-Output: "
)

RunTargetOrDefault "Default"