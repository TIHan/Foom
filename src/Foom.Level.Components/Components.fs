module Foom.Level.Components

open Foom.Ecs

type LevelComponent =
    {
        sectors: Sector []
    }

    interface IComponent
