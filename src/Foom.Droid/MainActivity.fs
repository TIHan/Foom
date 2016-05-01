namespace Foom.Droid

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget

open Urho
open Urho.Droid

open Foom

[<Activity (Label = "Foom.Droid", Theme = "@android:style/Theme.NoTitleBar.Fullscreen", MainLauncher = true)>]
type MainActivity () =
    inherit Activity ()

    override this.OnCreate (bundle) =
        base.OnCreate (bundle)

        let layout = new RelativeLayout (this)
        let surface = UrhoSurface.CreateSurface<App> (this)
        layout.AddView (surface)
        this.SetContentView (layout)

    override this.OnResume () =
        UrhoSurface.OnResume ()
        base.OnResume ()

    override this.OnPause () =
        UrhoSurface.OnPause ()
        base.OnPause ()

    override this.OnLowMemory () =
        UrhoSurface.OnLowMemory ()
        base.OnLowMemory ()

    override this.OnDestroy () =
        UrhoSurface.OnDestroy ()
        base.OnDestroy ()

    override this.DispatchKeyEvent e =
        if not (UrhoSurface.DispatchKeyEvent (e)) then
            false
        else
            base.DispatchKeyEvent (e)

    override this.OnWindowFocusChanged hasFocus =
        UrhoSurface.OnWindowFocusChanged (hasFocus)
        base.OnWindowFocusChanged (hasFocus)