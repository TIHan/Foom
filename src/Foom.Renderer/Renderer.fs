namespace Foom.Renderer

open System
open System.IO
open System.Drawing
open System.Diagnostics
open System.Numerics

open Ferop

[<Measure>] type program
[<Measure>] type uniform

[<Struct>]
type Application =
    val Window : nativeint
    val GLContext : nativeint

[<Struct>]
type RenderColor =
    val R : single
    val G : single
    val B : single
    val A : single

    new (r, g, b, a) = { R = r; G = g; B = b; A = a }

    static member OfColor (color: Color) =
        RenderColor (
            single color.R / 255.f,
            single color.G / 255.f,
            single color.B / 255.f,
            single color.A / 255.f)

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
#if defined(__GNUC__)
#   include "SDL.h"
#   include "SDL_opengl.h"
#else
#   include "SDL.h"
#   include <GL/glew.h>
#   include <GL/wglew.h>
#endif
""")>]
[<Source ("""
char VertexShaderErrorMessage[65536];
char FragmentShaderErrorMessage[65536];
char ProgramErrorMessage[65536];
""")>]
module Renderer =

    [<Import; MI (MIO.NoInlining)>]
    let init () : Application =
        C """
SDL_Init (SDL_INIT_VIDEO);

Renderer_Application app;

app.Window = 
    SDL_CreateWindow(
        "Foom",
        SDL_WINDOWPOS_UNDEFINED,
        SDL_WINDOWPOS_UNDEFINED,
        1280, 720,
        SDL_WINDOW_OPENGL);

SDL_GL_SetAttribute (SDL_GL_CONTEXT_MAJOR_VERSION, 3);
SDL_GL_SetAttribute (SDL_GL_CONTEXT_MINOR_VERSION, 2);
SDL_GL_SetAttribute (SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);

app.GLContext = SDL_GL_CreateContext ((SDL_Window*)app.Window);
SDL_GL_SetSwapInterval (0);

#if defined(__GNUC__)
#else
glewExperimental = GL_TRUE;
glewInit ();
#endif

return app;
        """

    [<Import; MI (MIO.NoInlining)>]
    let exit (app: Application) : int =
        C """
SDL_GL_DeleteContext (app.GLContext);
SDL_DestroyWindow ((SDL_Window*)app.Window);
SDL_Quit ();
return 0;
        """
    
    [<Import; MI (MIO.NoInlining)>]
    let clear () : unit = C """ glClear (GL_COLOR_BUFFER_BIT); """

    [<Import; MI (MIO.NoInlining)>]
    let draw (app: Application) : unit = C """ SDL_GL_SwapWindow ((SDL_Window*)app.Window); """

    [<Import; MI (MIO.NoInlining)>]
    let makeVbo () : int =
        C """
GLuint vbo;
glGenBuffers (1, &vbo);
glBindBuffer (GL_ARRAY_BUFFER, vbo);
glBufferData (GL_ARRAY_BUFFER, 0, NULL, GL_DYNAMIC_DRAW);
return vbo;
        """

    [<Import; MI (MIO.NoInlining)>]
    let bufferVbo (data: Vector2 []) (size: int) (vbo: int) : unit =
        C """
glBindBuffer (GL_ARRAY_BUFFER, vbo);
glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawArrays (first: int) (count: int) : unit =
        C """
glDrawArrays (GL_LINES, first, count);
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawArraysLoop (first: int) (count : int) : unit =
        C """
glDrawArrays (GL_LINE_LOOP, first, count);
        """

    [<Import; MI (MIO.NoInlining)>]
    let loadShaders (vertexSource: byte[]) (fragmentSource: byte[]) : int<program> =
        C """
// Create the shaders
GLuint VertexShaderID = glCreateShader(GL_VERTEX_SHADER);
GLuint FragmentShaderID = glCreateShader(GL_FRAGMENT_SHADER);

GLint Result = GL_FALSE;
int InfoLogLength;



// Compile Vertex Shader
glShaderSource(VertexShaderID, 1, &vertexSource, NULL);
glCompileShader(VertexShaderID);

// Check Vertex Shader
glGetShaderiv(VertexShaderID, GL_COMPILE_STATUS, &Result);
glGetShaderiv(VertexShaderID, GL_INFO_LOG_LENGTH, &InfoLogLength);
if ( InfoLogLength > 0 ){
    glGetShaderInfoLog(VertexShaderID, InfoLogLength, NULL, &VertexShaderErrorMessage[0]);
    printf("%s\n", &VertexShaderErrorMessage[0]);
    for (int i = 0; i < 65536; ++i) { VertexShaderErrorMessage[i] = '\0'; }
}



// Compile Fragment Shader
glShaderSource(FragmentShaderID, 1, &fragmentSource, NULL);
glCompileShader(FragmentShaderID);

// Check Fragment Shader
glGetShaderiv(FragmentShaderID, GL_COMPILE_STATUS, &Result);
glGetShaderiv(FragmentShaderID, GL_INFO_LOG_LENGTH, &InfoLogLength);
if ( InfoLogLength > 0 ){
    glGetShaderInfoLog(FragmentShaderID, InfoLogLength, NULL, &FragmentShaderErrorMessage[0]);
    printf("%s\n", &FragmentShaderErrorMessage[0]);
    for (int i = 0; i < 65536; ++i) { FragmentShaderErrorMessage[i] = '\0'; }
}



// Link the program
printf("Linking program\n");
GLuint ProgramID = glCreateProgram();
glAttachShader(ProgramID, VertexShaderID);
glAttachShader(ProgramID, FragmentShaderID);
glLinkProgram(ProgramID);

// Check the program
glGetProgramiv(ProgramID, GL_LINK_STATUS, &Result);
glGetProgramiv(ProgramID, GL_INFO_LOG_LENGTH, &InfoLogLength);
if ( InfoLogLength > 0 ){
    glGetProgramInfoLog(ProgramID, InfoLogLength, NULL, &ProgramErrorMessage[0]);
    printf("%s\n", &ProgramErrorMessage[0]);
    for (int i = 0; i < 65536; ++i) { ProgramErrorMessage[i] = '\0'; }
}

glUseProgram (ProgramID);

/******************************************************/

GLuint vao;
glGenVertexArrays (1, &vao);

glBindVertexArray (vao);

GLint posAttrib = glGetAttribLocation (ProgramID, "position");

glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);

glEnableVertexAttribArray (posAttrib);

return ProgramID;
        """

    [<Import; MI (MIO.NoInlining)>]
    let getUniformProjection (program: int<program>) : int<uniform> =
        C """
return glGetUniformLocation (program, "uni_projection");
        """

    [<Import; MI (MIO.NoInlining)>]
    let setUniformProjection (uni: int<uniform>) (m: Matrix4x4)  : unit =
        C """
glUniformMatrix4fv (uni, 1, GL_FALSE, &m);
"""

    [<Import; MI (MIO.NoInlining)>]
    let getUniformColor (program: int<program>) : int<uniform> =
        C """
GLint uni_color = glGetUniformLocation (program, "uni_color");
return uni_color;
        """

    [<Import; MI (MIO.NoInlining)>]
    let setUniformColor (uniformColor: int<uniform>) (color: RenderColor) : unit =
        C """
glUniform4f (uniformColor, color.R, color.G, color.B, color.A);
        """

module Backend =
    let loadShaders () =
        let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes ("v.vertex"))
        let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes ("f.fragment"))

        Renderer.loadShaders vertexFile fragmentFile