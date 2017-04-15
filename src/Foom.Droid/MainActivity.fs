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

type GLRenderer () =
    inherit Java.Lang.Object ()

    let mutable vaoId = 0

    interface GLSurfaceView.IRenderer with

        member x.OnDrawFrame _ =
            ()

        member x.OnSurfaceChanged (gl, width, height) =
            GL.Viewport (0, 0, width, height)

        member x.OnSurfaceCreated (gl, config) =
            GL.GenVertexArrays (1, &vaoId)
            GL.BindVertexArray (vaoId)

type GLView (context) as this =
    inherit GLSurfaceView (context)

    let renderer = new GLRenderer ()

    do
        this.SetEGLContextClientVersion (3)
        this.SetEGLConfigChooser (8, 8, 8, 8, 16, 0)
        this.SetRenderer (renderer)

[<Activity (Label = "Foom", MainLauncher = true, Icon = "@mipmap/icon")>]
type MainActivity () =
    inherit Activity ()

    let mutable count:int = 1

    override this.OnCreate (bundle) =

        base.OnCreate (bundle)

        // Set our view from the "main" layout resource
        this.SetContentView (Resources.Layout.Main)

        // Get our button from the layout resource, and attach an event to it
        let button = this.FindViewById<Button>(Resources.Id.myButton)
        button.Click.Add (fun args -> 
            button.Text <- sprintf "%d clicks!" count
            count <- count + 1
        )

