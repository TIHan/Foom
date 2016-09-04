namespace Foom.Physics

open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Ecs
open Foom.Geometry

type CharacterControllerComponent (position: Vector3, radius: float32, height: float32) =

    let circle =
        {
            Circle = Circle2D (Vector2 (position.X, position.Y), radius)
            Z = position.Z
            Height = height
            Hashes = HashSet ()
        }

    member this.Circle = circle

    interface IComponent

type PhysicsEngineComponent =
    {
        PhysicsEngine: PhysicsEngine
    }

    static member Create (cellSize) =
        {
            PhysicsEngine = PhysicsEngine.create cellSize
        }

    interface IComponent
