namespace Foom.iOS

open System

open Foundation
open UIKit
open GLKit
open OpenGLES
open OpenTK
open OpenTK.Graphics.ES30

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
    inherit GLKViewController (handle)

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

        //GL.Oes.GenVertexArrays(1, out vertexArray);
        //GL.Oes.BindVertexArray(vertexArray);

        //GL.GenBuffers(1, out vertexBuffer);
        //GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
        //GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(cubeVertexData.Length * sizeof(float)), cubeVertexData, BufferUsage.StaticDraw);

        //GL.EnableVertexAttribArray((int)GLKVertexAttrib.Position);
        //GL.VertexAttribPointer((int)GLKVertexAttrib.Position, 3, VertexAttribPointerType.Float, false, 24, new IntPtr(0));
        //GL.EnableVertexAttribArray((int)GLKVertexAttrib.Normal);
        //GL.VertexAttribPointer((int)GLKVertexAttrib.Normal, 3, VertexAttribPointerType.Float, false, 24, new IntPtr(12));

        //GL.Oes.BindVertexArray(0);

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true

    interface IGLKViewDelegate with

        member x.DrawInRect (view, rect) =
            ()