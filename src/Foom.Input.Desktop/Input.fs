namespace Foom.Input

open System.Collections.Concurrent
open OpenTK

type DesktopInput (window : GameWindow) =

    let queue = ConcurrentQueue<InputEvent> ()
    let mutable mousePosition = Unchecked.defaultof<MousePosition>

    do
        window.KeyDown.Add (fun key ->
            match key.Key with
            | Input.Key.W ->
                queue.Enqueue (KeyPressed ('w'))
            | _ -> ()
        )

        window.KeyUp.Add (fun key ->
            match key.Key with
            | Input.Key.W ->
                queue.Enqueue (KeyReleased ('w'))
            | _ -> ()
        )

        window.MouseMove.Add (fun args ->
            queue.Enqueue (MouseMoved (args.X, args.Y, args.XDelta, args.YDelta))
        )

    interface IInput with

        member x.PollEvents () =
            ()
          //  Input.pollEvents window

        member x.GetMousePosition () =
            mousePosition
            //Input.getMousePosition ()

        member x.GetState () =
            let arr = ResizeArray ()
            let mutable evt = Unchecked.defaultof<InputEvent>
            while queue.TryDequeue (&evt) do
                arr.Add (evt)
            {
                Events = arr |> Seq.toList
            }
           // Input.getState ()