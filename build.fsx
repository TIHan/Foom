// include Fake lib
#r @"packages/FAKE/tools/FakeLib.dll"
open Fake

// Default target
Target "Default" (fun _ ->
    trace "Hello World from FAKE"
)

Target "CustomTarget" (fun _ ->
    trace "BEEF"
)

// start build
RunTargetOrDefault "Default"