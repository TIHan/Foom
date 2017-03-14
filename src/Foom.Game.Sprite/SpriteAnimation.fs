namespace Foom.Game.Sprite

open System
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Renderer
open Foom.Collections
open Foom.Renderer
open Foom.Renderer.RendererSystem
open Foom.Game.Assets
open Foom.Game.Core

type SpriteAnimationComponent (time: TimeSpan, frames: int list) =
    inherit Component ()

    member val Time = time

    member val Frames = frames

    member val TimeSeconds = single time.TotalSeconds

    member val FrameArray = frames |> Array.ofList

    member val FrameTime = single time.TotalSeconds / single frames.Length

    member val CurrentTime = 0.f with get, set

module SpriteAnimation =

    let update : Behavior<float32 * float32> =
        Behavior.update (fun (time, deltaTime) em ea ->
            em.ForEach<SpriteAnimationComponent, SpriteComponent> (fun _ spriteAnimComp spriteComp ->
                let frameIndex = int (spriteAnimComp.CurrentTime / spriteAnimComp.FrameTime)

                let frameIndex =
                    if frameIndex < 0 || frameIndex >= spriteAnimComp.FrameArray.Length then 0
                    else frameIndex

                spriteComp.Frame <- spriteAnimComp.FrameArray.[frameIndex]

                spriteAnimComp.CurrentTime <- spriteAnimComp.CurrentTime + deltaTime
                if spriteAnimComp.CurrentTime > spriteAnimComp.TimeSeconds then
                    spriteAnimComp.CurrentTime <- spriteAnimComp.CurrentTime - spriteAnimComp.TimeSeconds
            )
        )
