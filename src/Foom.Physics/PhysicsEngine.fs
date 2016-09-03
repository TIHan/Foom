﻿namespace Foom.Physics

type PhysicsEngine =
    {
        SpatialHash: SpatialHash
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    let findWithPoint p eng =
        eng.SpatialHash
        |> SpatialHash.findWithPoint p

    let iterWithPoint p f g eng =
        eng.SpatialHash
        |> SpatialHash.iterWithPoint p f g

    let addTriangle tri data eng =
        eng.SpatialHash
        |> SpatialHash.addTriangle tri data

    let addLineDefinition lined eng =
        eng.SpatialHash
        |> SpatialHash.addLineDefinition lined
