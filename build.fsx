#r @"packages/FAKE/tools/FakeLib.dll"
#r @"build/Ferop.dll"
#r @"build/Ferop.Core.dll"
open Fake
open Ferop

Target "DesktopDebug" (fun _ ->
    MSBuildDebug "build" "Build" [ "src/Foom/Foom.fsproj" ] |> ignore
)

Target "Ferop" (fun _ ->
    !! "src/Ferop/src/Ferop.Core/Ferop.Core.fsproj"
    |> MSBuildDebug "build" "Build"
    |> Log "FeropBuild-Output: "

    Ferop.run "build/Foom.Renderer.Desktop.dll" |> ignore
    Ferop.run "build/Foom.Input.Desktop.dll" |> ignore
)

RunTargetOrDefault "DesktopDebug"