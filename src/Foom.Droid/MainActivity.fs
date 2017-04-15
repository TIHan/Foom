namespace Foom.Droid

open System
open System.Threading.Tasks

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Opengl
open Android.Content.PM
open Javax.Microedition.Khronos.Opengles;
open OpenTK.Graphics
open OpenTK.Graphics.ES30

open Foom.Renderer
open Foom.Input

type GLRenderer (surfaceView: GLSurfaceView) =
    inherit Java.Lang.Object ()

    let mutable vaoId = 0

    let mutable PreUpdate = id

    let mutable Update = (fun _ _ -> true)

    let mutable Render = (fun _ _ -> ())

    let mutable gameLoop = GameLoop.create 30.

    let inputEvents = ResizeArray ()

    interface GLSurfaceView.IRenderer with

        member x.OnDrawFrame _ =
            gameLoop <- GameLoop.tick PreUpdate Update Render gameLoop

        member x.OnSurfaceChanged (gl, width, height) =
            GL.Viewport (0, 0, width, height)

        member x.OnSurfaceCreated (gl, config) =
            GL.GenVertexArrays (1, &vaoId)
            GL.BindVertexArray (vaoId)

            let gl = OpenTKGL (fun () -> ())

            let (preUpdate, update, render) = Foom.Program.start x gl (new Task (fun () -> ()) |> ref)
            PreUpdate <- preUpdate
            Update <- update
            Render <- render

    interface IInput with

        member x.PollEvents () =
            ()

        member x.GetMousePosition () =
            MousePosition ()

        member x.GetState () =
            let events = inputEvents |> Seq.toList
            inputEvents.Clear ()
            { Events = events }

type GLView (context) as x =
    inherit RelativeLayout (context)

    let surfaceView = new GLSurfaceView (context)
    let renderer = new GLRenderer (surfaceView)

    do
        surfaceView.SetEGLContextClientVersion (3)
        surfaceView.SetEGLConfigChooser (8, 8, 8, 8, 24, 8)
        surfaceView.SetRenderer (renderer)
        surfaceView.PreserveEGLContextOnPause <- true

        x.AddView surfaceView

[<Activity (
    Label = "Foom", 
    MainLauncher = true, 
    Icon = "@mipmap/icon", 
    ConfigurationChanges = (ConfigChanges.KeyboardHidden ||| ConfigChanges.Keyboard ||| ConfigChanges.Orientation ||| ConfigChanges.ScreenSize), 
    ScreenOrientation = ScreenOrientation.SensorLandscape
)>]
type MainActivity () =
    inherit Activity ()

    override this.OnCreate (bundle) =

        base.OnCreate (bundle)

        this.Window.SetFlags (WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn)

        let glView = new GLView (this :> Context)

        let a = new Button (this)
        a.Text <- "YOOOPAC"

        glView.AddView a
       
        this.SetContentView (glView)
