namespace Foom.Droid

open System
open System.IO

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

        App.ApplicationDirectory <- System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal)

        App.SaveBitmap <- fun pixels name ->
            let width = pixels.Length
            let height = pixels.[0].Length
            let bitmap = Android.Graphics.Bitmap.CreateBitmap (width, height, Android.Graphics.Bitmap.Config.Rgb565)
            
            for i = 0 to pixels.Length - 1 do
                let pixelI = pixels.[i]
                for j = 0 to pixelI.Length - 1 do
                    let pixel = pixelI.[j]
                    bitmap.SetPixel (i, j, Android.Graphics.Color (pixel.R, pixel.G, pixel.B))

            let sdCardPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal)
            let filePath = System.IO.Path.Combine (sdCardPath, name + ".png");
            let stream = new FileStream (filePath, FileMode.Create);
            let yopac = bitmap.Compress (Android.Graphics.Bitmap.CompressFormat.Png, 100, stream)
            stream.Dispose ()
            bitmap.Dispose ()

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