namespace Foom.iOS

open System

open Foundation
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

        //while true do
        view.EnableSetNeedsDisplay <- false

       // Foom.Program.start (fun () -> view.SetNeedsDisplay ()) (new Task (fun () -> ()) |> ref)

    override x.ViewDidAppear animating =
        base.ViewDidAppear animating

        let view = x.View :?> GLKView

        GL.ClearColor (new Color4 (0.f, 1.f, 0.f, 1.f))
        GL.Clear (ClearBufferMask.ColorBufferBit)
        view.Display ()

        GL.ClearColor (new Color4 (0.f, 1.f, 0.f, 1.f))
        GL.Clear (ClearBufferMask.ColorBufferBit)
        view.Display ()

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true
