namespace Foom.Physics

open Foom.Ecs

type PhysicsEngineComponent =
    {
        PhysicsEngine: PhysicsEngine
    }

    static member Create (cellSize) =
        {
            PhysicsEngine =
                {
                    SpatialHash = SpatialHash.create cellSize
                }
        }

    interface IComponent
