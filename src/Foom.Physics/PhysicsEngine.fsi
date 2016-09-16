namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

[<Sealed>]
type PhysicsEngine

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    val create : cellSize: int -> PhysicsEngine

    val warpRigidBody : Vector3 -> RigidBody -> PhysicsEngine -> unit

    val addRigidBody : RigidBody -> PhysicsEngine -> unit

    val addTriangle : Triangle2D -> obj -> PhysicsEngine -> unit

    val addLineSegment : LineSegment2D -> isWall: bool -> PhysicsEngine -> unit

    val iterLineSegmentByAABB : AABB2D -> (LineSegment2D -> unit) -> PhysicsEngine -> unit

    val findWithPoint : Vector2 -> PhysicsEngine -> obj

    val iterWithPoint : Vector2 -> (Triangle2D -> unit) -> (LineSegment2D -> unit) -> PhysicsEngine -> unit

    val debugFindSpacesByRigidBody : RigidBody -> PhysicsEngine -> AABB2D seq