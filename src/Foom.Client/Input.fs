﻿namespace Foom.Client

open Ferop

open System.Runtime.InteropServices

type MouseButtonType =
    | Left = 1
    | Middle = 2
    | Right = 3
    | X1 = 4
    | X2 = 5

type InputEvent =
    | KeyPressed of char
    | KeyReleased of char
    | MouseButtonPressed of MouseButtonType
    | MouseButtonReleased of MouseButtonType
    | MouseWheelScrolled of 
        x: int * y: int
    | MouseMoved of
        x: int * y: int * xrel: int * yrel: int

type InputState = { Events: InputEvent list }

[<Struct>]
type KeyboardEvent =
    val IsPressed : int
    val KeyCode : int

[<Struct>]
type MouseButtonEvent =
    val IsPressed : int
    val Clicks : int
    val Button : MouseButtonType
    val X : int
    val Y : int

[<Struct>]
type MouseWheelEvent =
    val X : int
    val Y : int

[<Struct>]
type MouseMoveEvent =
    val X : int
    val Y : int
    val XRel : int
    val YRel : int

[<Struct>]
type MousePosition =
    val X : int
    val Y : int
    val XRel : int
    val YRel : int

[<Ferop>]
[<ClangOsx (
    "-DGL_GLEXT_PROTOTYPES -I/Library/Frameworks/SDL2.framework/Headers",
    "-F/Library/Frameworks -framework Cocoa -framework OpenGL -framework IOKit -framework SDL2"
)>]
[<GccLinux ("-I../include/SDL2", "-lSDL2")>]
#if __64BIT__
[<MsvcWin (""" /I ..\..\include\SDL2 /I ..\..\include ..\..\lib\win\x64\SDL2.lib ..\..\lib\win\x64\SDL2main.lib ..\..\lib\win\x64\glew32.lib opengl32.lib """)>]
#else
[<MsvcWin (""" /I ..\..\include\SDL2 /I ..\..\include ..\..\lib\win\x86\SDL2.lib ..\..\lib\win\x86\SDL2main.lib ..\..\lib\win\x86\glew32.lib opengl32.lib """)>]
#endif
[<Header ("""
#include <stdio.h>
#include "SDL.h"
""")>]
module Input =

    let inputEvents = ResizeArray<InputEvent> ()

    [<Export>]
    let dispatchKeyboardEvent (kbEvt: KeyboardEvent) : unit =
        inputEvents.Add (
            if kbEvt.IsPressed = 0 then 
                InputEvent.KeyReleased (char kbEvt.KeyCode) 
            else 
                InputEvent.KeyPressed (char kbEvt.KeyCode))

    [<Export>]
    let dispatchMouseButtonEvent (mbEvt: MouseButtonEvent) : unit =
        inputEvents.Add (
            if mbEvt.IsPressed = 0 then
                InputEvent.MouseButtonReleased (mbEvt.Button)
            else
                InputEvent.MouseButtonPressed (mbEvt.Button))

    [<Export>]
    let dispatchMouseWheelEvent (evt: MouseWheelEvent) : unit =
        inputEvents.Add (InputEvent.MouseWheelScrolled (evt.X, evt.Y))

    [<Export>]
    let dispatchMouseMoveEvent (evt: MouseMoveEvent) : unit =
        inputEvents.Add (InputEvent.MouseMoved (evt.X, evt.Y, evt.XRel, evt.YRel))

    [<Import; MI (MIO.NoInlining)>]
    let pollEvents (window: nativeint) : unit =
        C """
SDL_Event e;
while (SDL_PollEvent (&e))
{
    if (e.type == SDL_KEYDOWN)
    {
        SDL_KeyboardEvent* event = (SDL_KeyboardEvent*)&e;
        if (event->repeat != 0) continue;

        Input_KeyboardEvent evt;
        evt.IsPressed = 1;
        evt.KeyCode = event->keysym.sym;

        Input_dispatchKeyboardEvent (evt);
    }
    else if (e.type == SDL_KEYUP)
    {
        SDL_KeyboardEvent* event = (SDL_KeyboardEvent*)&e;
        if (event->repeat != 0) continue;

        Input_KeyboardEvent evt;
        evt.IsPressed = 0;
        evt.KeyCode = event->keysym.sym;

        Input_dispatchKeyboardEvent (evt);
    }
    else if (e.type == SDL_MOUSEBUTTONDOWN)
    {
        SDL_MouseButtonEvent* event = (SDL_MouseButtonEvent*)&e;
        
        Input_MouseButtonEvent evt;
        evt.IsPressed = 1;
        evt.Clicks = event->clicks;
        evt.Button = event->button;
        evt.X = event->x;
        evt.Y = event->y;

        Input_dispatchMouseButtonEvent (evt);
    }
    else if (e.type == SDL_MOUSEBUTTONUP)
    {
        SDL_MouseButtonEvent* event = (SDL_MouseButtonEvent*)&e;
        
        Input_MouseButtonEvent evt;
        evt.IsPressed = 0;
        evt.Clicks = event->clicks;
        evt.Button = event->button;
        evt.X = event->x;
        evt.Y = event->y;

        Input_dispatchMouseButtonEvent (evt);
    }
    else if (e.type == SDL_MOUSEWHEEL)
    {
        SDL_MouseWheelEvent* event = (SDL_MouseWheelEvent*)&e;
        
        Input_MouseWheelEvent evt;
        evt.X = event->x;
        evt.Y = event->y;

        Input_dispatchMouseWheelEvent (evt);
    }
    else if (e.type == SDL_MOUSEMOTION)
    {
        SDL_MouseMotionEvent* event = (SDL_MouseMotionEvent*)&e;

        Input_MouseMoveEvent evt;
        evt.X = event->x;
        evt.Y = event->y;
        evt.XRel = event->xrel;
        evt.YRel = event->yrel;

        Input_dispatchMouseMoveEvent (evt);
    }
} 
        """

    [<Import; MI (MIO.NoInlining)>]
    let getMousePosition () : MousePosition =
        C """
        int32_t x;
        int32_t y;
        int32_t xrel;
        int32_t yrel;

        Input_MousePosition pos;

        SDL_GetMouseState (&pos.X, &pos.Y);
        SDL_GetRelativeMouseState (&pos.XRel, &pos.YRel);

        return pos;
        """

    let getState () : InputState =
        let events = inputEvents |> List.ofSeq
        inputEvents.Clear ()
        { Events = events }
        