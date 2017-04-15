namespace Foom.Droid

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Opengl
open Javax.Microedition.Khronos.Opengles;
open OpenTK.Graphics
open OpenTK.Graphics.ES30

type Resources = Foom.Droid.Resource

type GLRenderer (surfaceView: GLSurfaceView) =
    inherit Java.Lang.Object ()

    let mutable vaoId = 0

    interface GLSurfaceView.IRenderer with

        member x.OnDrawFrame _ =
            GL.Clear (ClearBufferMask.ColorBufferBit)

        member x.OnSurfaceChanged (gl, width, height) =
            GL.Viewport (0, 0, width, height)

        member x.OnSurfaceCreated (gl, config) =
            GL.ClearColor (Color4.Green)
            GL.GenVertexArrays (1, &vaoId)
            GL.BindVertexArray (vaoId)

type GLView (context) as x =
    inherit RelativeLayout (context)

    let surfaceView = new GLSurfaceView (context)
    let renderer = new GLRenderer (surfaceView)

    do
        surfaceView.SetEGLContextClientVersion (3)
        surfaceView.SetEGLConfigChooser (8, 8, 8, 8, 16, 8)
        surfaceView.SetRenderer (renderer)

        x.AddView surfaceView

[<Activity (Label = "Foom", MainLauncher = true, Icon = "@mipmap/icon")>]
type MainActivity () =
    inherit Activity ()

    override this.OnCreate (bundle) =

        base.OnCreate (bundle)

        let glView = new GLView (this :> Context)

        let a = new Button (this)
        a.Text <- "YOOOPAC"

        glView.AddView a
       
        this.SetContentView (glView)
