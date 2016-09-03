namespace Foom.Physics

type PhysicsEngine =
    {
        SpatialHash: SpatialHash
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    let findWithPoint p eng =
        eng.SpatialHash
        |> SpatialHash.findWithPoint p

    let iterTestedTrianglesWithPoint p f eng =
        eng.SpatialHash
        |> SpatialHash.iterTestedTrianglesWithPoint p f

    let addTriangle tri data eng =
        eng.SpatialHash
        |> SpatialHash.addTriangle tri data
