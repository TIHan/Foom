namespace Foom.Input

open OpenTK

type DesktopInput (window : GameWindow) =

    interface IInput with

        member x.PollEvents () =
            ()
          //  Input.pollEvents window

        member x.GetMousePosition () =
            MousePosition ()
            //Input.getMousePosition ()

        member x.GetState () =
            Unchecked.defaultof<InputState>
           // Input.getState ()