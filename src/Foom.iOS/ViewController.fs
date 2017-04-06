namespace Foom.iOS

open System

open Foundation
open CoreAnimation
open UIKit
open GLKit
open OpenGLES
open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.ES30
open System.Threading.Tasks

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
    inherit UIViewController (handle)

    let mutable PreUpdate = id

    let mutable Update = (fun _ _ -> true)

    let mutable Render = (fun _ _ -> ())

    let mutable gameLoop = GameLoop.create 30.

    let mutable displayLink = null

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

        EAGLContext.SetCurrentContext context |> ignore

        let mutable vertexArray = 0
        GL.GenVertexArrays (1, &vertexArray)
        GL.BindVertexArray (vertexArray)

        view.EnableSetNeedsDisplay <- false

    override x.ViewDidAppear animating =
        base.ViewDidAppear animating

        let view = x.View :?> GLKView

        //GL.ClearColor (new Color4 (0.f, 1.f, 0.f, 1.f))
        //GL.Clear (ClearBufferMask.ColorBufferBit)
        //view.Display ()

        //GL.ClearColor (new Color4 (0.f, 1.f, 0.f, 1.f))
        //GL.Clear (ClearBufferMask.ColorBufferBit)
        //view.Display ()

        let swapBuffers =
            fun () ->
                GL.ClearColor (new Color4 (1.f, 0.f, 0.f, 1.f))
                GL.Clear (ClearBufferMask.ColorBufferBit)
                view.Display ()

        let (preUpdate, update, render) = Foom.Program.start swapBuffers (new Task (fun () -> ()) |> ref)
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

    member x.Tick () : unit =
        gameLoop <- GameLoop.tick PreUpdate Update Render gameLoop