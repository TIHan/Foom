module Foom.Level.Components

open Foom.Wad.Level
open Foom.Ecs

type LevelComponent =
    {
        level: Level
        sectors: Sector []
    }

    interface IComponent
