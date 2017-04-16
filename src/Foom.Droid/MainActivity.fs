namespace rec Foom.Droid

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
open OpenTK.Platform.Android

open Foom.Renderer
open Foom.Input

type GLRenderer (surfaceView: GLSurfaceView, input) =
    inherit Java.Lang.Object ()

    let mutable vaoId = 0

    let mutable PreUpdate = id

    let mutable Update = (fun _ _ -> true)

    let mutable Render = (fun _ _ -> ())

    let mutable gameLoop = GameLoop.create 30.

    interface GLSurfaceView.IRenderer with

        member x.OnDrawFrame _ =
            gameLoop <- GameLoop.tick PreUpdate Update Render gameLoop

        member x.OnSurfaceChanged (gl, width, height) =
            GL.Viewport (0, 0, width, height)

        member x.OnSurfaceCreated (gl, config) =
            GL.GenVertexArrays (1, &vaoId)
            GL.BindVertexArray (vaoId)

            let gl = OpenTKGL (fun () -> ())

            let (preUpdate, update, render) = Foom.Program.start input gl (new Task (fun () -> ()) |> ref)
            PreUpdate <- preUpdate
            Update <- update
            Render <- render

type GLView (context) as x =
    inherit RelativeLayout (context)

    let surfaceView = new GLSurfaceView (context)
    let renderer = new GLRenderer (surfaceView, x)

    let mutable lastTouchX = 0.f
    let mutable lastTouchY = 0.f

    let inputEvents = ResizeArray ()

    do
        surfaceView.SetEGLContextClientVersion (3)
        surfaceView.SetEGLConfigChooser (8, 8, 8, 8, 24, 8)
        surfaceView.SetRenderer (renderer)
        surfaceView.PreserveEGLContextOnPause <- true

        x.AddView surfaceView

        x.Touch.Add (fun args ->
            let ev = args.Event
            let touchX = ev.GetX ()
            let touchY = ev.GetY ()

            match args.Event.Action with

            | MotionEventActions.Down ->
                lastTouchX <- touchX
                lastTouchY <- touchY

            | MotionEventActions.Up ->
                inputEvents.Add (KeyReleased ('w'))

            | _ -> ()

            if x.Width / 2 < int touchX then 
                match args.Event.Action with

                | MotionEventActions.Down ->
                    inputEvents.Add (KeyPressed ('w'))

                | MotionEventActions.Up ->
                    inputEvents.Add (KeyReleased ('w'))

                | _ -> ()

            else
                match args.Event.Action with

                | MotionEventActions.Move ->
                    let xrel = touchX - lastTouchX
                    let yrel = touchY - lastTouchY
                    inputEvents.Add (MouseMoved (0, 0, int xrel, int yrel))

                | _ -> ()
        )

    interface IInput with

        member x.PollEvents () =
            ()

        member x.GetMousePosition () =
            MousePosition ()

        member x.GetState () =
            let events = inputEvents |> Seq.toList
            inputEvents.Clear ()
            { Events = events }

type GameLoopView (context, input) as this =
    inherit AndroidGameView (context)

    let mutable PreUpdate = id

    let mutable Update = (fun _ _ -> true)

    let mutable Render = (fun _ _ -> ())

    let mutable gameLoop = GameLoop.create 30.

    let mutable willSetViewport = true

    let mutable isInit = false

    do
        this.AutoSetContextOnRenderFrame <- false
        this.RenderOnUIThread <- false

        this.Resize.Add (fun _ ->
            willSetViewport <- true
        )

    override x.OnLoad e =
        base.OnLoad e

        x.Run ()

    override x.CreateFrameBuffer () =
        x.ContextRenderingApi <- GLVersion.ES3
        x.GraphicsMode <- new AndroidGraphicsMode (ColorFormat (8, 8, 8, 8), 24, 8, 0, 0, false)
        base.CreateFrameBuffer ()

    override x.OnRenderFrame e =
        base.OnRenderFrame e

        if not isInit then
            isInit <- true

            let gl = OpenTKGL (fun () -> ())

            let (preUpdate, update, render) = Foom.Program.start input gl (new Task (fun () -> ()) |> ref)
            PreUpdate <- preUpdate
            Update <- update
            Render <- render

        if willSetViewport then
            willSetViewport <- false
            GL.Viewport (0, 0, x.Width, x.Height)

        gameLoop <- GameLoop.tick PreUpdate Update Render gameLoop

        x.SwapBuffers ()

type GameView (context) as this =
    inherit RelativeLayout (context)

    let mutable lastTouchX = 0.f
    let mutable lastTouchY = 0.f

    let inputEvents = ResizeArray ()

    do
        let gameLoopView = new GameLoopView (context, this)
        this.AddView gameLoopView

        this.Touch.Add (fun args ->
            let ev = args.Event
            let touchX = ev.GetX ()
            let touchY = ev.GetY ()

            match args.Event.Action with

            | MotionEventActions.Down ->
                lastTouchX <- touchX
                lastTouchY <- touchY

            | MotionEventActions.Up ->
                inputEvents.Add (KeyReleased ('w'))

            | _ -> ()

            if this.Width / 2 < int touchX then 
                match args.Event.Action with

                | MotionEventActions.Down ->
                    inputEvents.Add (KeyPressed ('w'))

                | MotionEventActions.Up ->
                    inputEvents.Add (KeyReleased ('w'))

                | _ -> ()

            else
                match args.Event.Action with

                | MotionEventActions.Move ->
                    let xrel = touchX - lastTouchX
                    let yrel = touchY - lastTouchY
                    inputEvents.Add (MouseMoved (0, 0, int xrel, int yrel))

                | _ -> ()
        )

    interface IInput with

        member x.PollEvents () =
            ()

        member x.GetMousePosition () =
            MousePosition ()

        member x.GetState () =
            let events = inputEvents |> Seq.toList
            inputEvents.Clear ()
            { Events = events }

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

        let glView = new GameView (this :> Context)

        let a = new Button (this)
        a.Text <- "YOOOPAC"

        glView.AddView a
       
        this.SetContentView (glView)
