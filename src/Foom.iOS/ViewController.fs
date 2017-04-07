namespace Foom.iOS

open System

open Foundation
open CoreAnimation
open CoreGraphics
open UIKit
open GLKit
open OpenGLES
open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.ES30
open System.Threading.Tasks

open Foom.Renderer
open Foom.Input

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
    inherit UIViewController (handle)

    let mutable PreUpdate = id

    let mutable Update = (fun _ _ -> true)

    let mutable Render = (fun _ _ -> ())

    let mutable gameLoop = GameLoop.create 30.

    let mutable displayLink = null

    let inputEvents = ResizeArray ()

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()

        let context = new EAGLContext (EAGLRenderingAPI.OpenGLES3)
        let view = x.View :?> GLKView
        view.Context <- context
        view.DrawableDepthFormat <- GLKViewDrawableDepthFormat.Format24
        view.DrawableStencilFormat <- GLKViewDrawableStencilFormat.Format8

        EAGLContext.SetCurrentContext context |> ignore

        let mutable vertexArray = 0
        GL.GenVertexArrays (1, &vertexArray)
        GL.BindVertexArray (vertexArray)

        view.EnableSetNeedsDisplay <- false

        let button = UIButton.FromType (UIButtonType.System)
        button.SetTitle ("Turn Left", UIControlState.Normal)
        button.Frame <- CGRect (nfloat 0.f, (x.View.Frame.Height / nfloat 2.f), nfloat 128.f, nfloat 64.f)
        button.TouchUpInside.Add (fun _ -> inputEvents.Add (MouseMoved (0, 0, -128, 0)))
        x.View.AddSubview button

        let button = UIButton.FromType (UIButtonType.System)
        button.SetTitle ("Turn Right", UIControlState.Normal)
        button.Frame <- CGRect (nfloat 128.f, (x.View.Frame.Height / nfloat 2.f), nfloat 128.f, nfloat 64.f)
        button.TouchUpInside.Add (fun _ -> inputEvents.Add (MouseMoved (0, 0, 128, 0)))
        x.View.AddSubview button

    override x.ViewDidAppear animating =
        base.ViewDidAppear animating

        let view = x.View :?> GLKView

        let gl = OpenTKGL (fun () -> view.Display ())

        let (preUpdate, update, render) = Foom.Program.start x gl (new Task (fun () -> ()) |> ref)
        PreUpdate <- preUpdate
        Update <- update
        Render <- render

        displayLink <- CADisplayLink.Create (Action (fun () -> x.Tick ()))
        displayLink.AddToRunLoop (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode)

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true

    override x.TouchesBegan (touches, evt) =
        inputEvents.Add (KeyPressed ('w'))

    override x.TouchesEnded (touches, evt) =
        inputEvents.Add (KeyReleased ('w'))

    member x.Tick () : unit =
        gameLoop <- GameLoop.tick PreUpdate Update Render gameLoop

    interface IInput with

        member x.PollEvents () =
            ()

        member x.GetMousePosition () =
            MousePosition ()

        member x.GetState () =
            let events = inputEvents |> Seq.toList
            inputEvents.Clear ()
            { Events = events }