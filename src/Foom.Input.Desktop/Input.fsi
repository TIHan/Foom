namespace Foom.Input

open OpenTK

type DesktopInput =

    new : window : GameWindow -> DesktopInput

    interface IInput