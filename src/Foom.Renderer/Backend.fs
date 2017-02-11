namespace Foom.Renderer

open System
open System.IO
open System.Drawing
open System.Diagnostics
open System.Numerics
open System.Runtime.InteropServices

open Ferop

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
    "-DGL_GLEXT_PROTOTYPES -I/usr/local/include/SDL2",
    "-F/Library/Frameworks -framework Cocoa -framework OpenGL -framework IOKit -L/usr/local/lib/ -lSDL2"
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
module Backend =

    [<Import; MI (MIO.NoInlining)>]
    let init () : Application =
        C """
        SDL_Init (SDL_INIT_VIDEO);

        Backend_Application app;

        app.Window = 
            SDL_CreateWindow(
                "Foom",
                SDL_WINDOWPOS_UNDEFINED,
                SDL_WINDOWPOS_UNDEFINED,
                1280, 720,
                SDL_WINDOW_OPENGL);

        //SDL_SetRelativeMouseMode(1);

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

        GLuint vao;
        glGenVertexArrays (1, &vao);

        glBindVertexArray (vao);

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
    let clear () : unit = C """ glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT); """

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
    let bufferVboVector3 (data: Vector3 []) (size: int) (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
        glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bufferVboVector4 (data: Vector4 []) (size: int) (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
        glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bufferVboMatrix4x4 (data: Matrix4x4 []) (size: int) (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
        glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
        """

    [<Import; MI (MIO.NoInlining)>]
    let getMaxTextureSize () : int =
        C """
        int intmax = 0;
        glGetIntegerv (GL_MAX_TEXTURE_SIZE, &intmax);
        return intmax;
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindArrayBuffer (bufferId: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, bufferId);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindVbo (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
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
    let drawTriangles (first: int) (count : int) : unit =
        C """
        glDrawArrays (GL_TRIANGLES, first, count);
        """

    [<Export>]
    let printFSharp (bytes: nativeptr<sbyte>) =
        let str = String (bytes)
        printfn "%s" str

    [<Import; MI (MIO.NoInlining)>]
    let loadShaders (vertexSource: byte[]) (fragmentSource: byte[]) : int =
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
            Backend_printFSharp (&VertexShaderErrorMessage[0]);
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
            Backend_printFSharp (&FragmentShaderErrorMessage[0]);
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
            Backend_printFSharp (&ProgramErrorMessage[0]);
            for (int i = 0; i < 65536; ++i) { ProgramErrorMessage[i] = '\0'; }
        }

        /******************************************************/

        return ProgramID;
        """

    [<Import; MI (MIO.NoInlining)>]
    let useProgram (programId: int) : unit =
        C """
        glUseProgram (programId);
        """

    [<Import; MI (MIO.NoInlining)>]
    let _getAttributeLocation (programId: int) (name: byte []) : int =
        C """
        return glGetAttribLocation (programId, name);
        """

    let getAttributeLocation programId (name: string) =
        System.Text.Encoding.UTF8.GetBytes (name)
        |> _getAttributeLocation programId

    [<Import; MI (MIO.NoInlining)>]
    let bindVertexAttributePointer_Float (location: int) (size: int) : unit =
        C """
        glVertexAttribPointer (location, size, GL_FLOAT, GL_FALSE, 0, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    let enableVertexAttribute (location: int) : unit =
        C """
        glEnableVertexAttribArray (location);
        """

    [<Import; MI (MIO.NoInlining)>]
    let disableVertexAttribute (location: int) : unit =
        C """
        glDisableVertexAttribArray (location);
        """

    [<Import; MI (MIO.NoInlining)>]
    let _getUniformLocation (programId: int) (name: byte []) : int =
        C """
        return glGetUniformLocation (programId, name);
        """

    let getUniformLocation programId (name: string) =
        System.Text.Encoding.UTF8.GetBytes (name)
        |> _getUniformLocation programId

    //

    [<Import; MI (MIO.NoInlining)>]
    let bindVertexAttributeVector3 (id: int) : unit =
        C """
        glVertexAttribPointer (id, 3, GL_FLOAT, GL_FALSE, 0, 0);
        glEnableVertexAttribArray (id);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindVertexAttributeVector2 (id: int) : unit =
        C """
        glVertexAttribPointer (id, 2, GL_FLOAT, GL_FALSE, 0, 0);
        glEnableVertexAttribArray (id);
        """

    //

    [<Import; MI (MIO.NoInlining)>]
    let bindUniformMatrix4x4 (id: int) (value: Matrix4x4) : unit =
        C """
        glUniformMatrix4fv (id, 1, GL_FALSE, &value);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindUniformVector4 (id: int) (value: Vector4) : unit =
        C """
        glUniform4f (id, value.X, value.Y, value.Z, value.W);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindUniformInt (id: int) (value: int) : unit =
        C """
        glUniform1i (id, value);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindUniform_float (id: int) (value: float32) : unit =
        C """
        glUniform1f (id, value);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindUniformVector2 (id: int) (value: Vector2) : unit =
        C """
        glUniform2f (id, value.X, value.Y);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindTexture2D (textureId: int) (textureNumber: int) : unit =
        C """
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, textureId);
        """

    ///

    [<Import; MI (MIO.NoInlining)>]
    let bindPosition (programID: int) : unit =
        C """
        GLint posAttrib = glGetAttribLocation (programID, "position");

        glVertexAttribPointer (posAttrib, 3, GL_FLOAT, GL_FALSE, 0, 0);

        glEnableVertexAttribArray (posAttrib);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindUv (programID: int) : unit =
        C """
        GLint posAttrib = glGetAttribLocation (programID, "in_uv");

        glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);

        glEnableVertexAttribArray (posAttrib);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindColor (programID: int) : unit =
        C """
        GLint posAttrib = glGetAttribLocation (programID, "in_color");

        glVertexAttribPointer (posAttrib, 4, GL_FLOAT, GL_FALSE, 0, 0);

        glEnableVertexAttribArray (posAttrib);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindCenter (programID: int) : unit =
        C """
        GLint posAttrib = glGetAttribLocation (programID, "in_center");

        glVertexAttribPointer (posAttrib, 3, GL_FLOAT, GL_FALSE, 0, 0);

        glEnableVertexAttribArray (posAttrib);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindModel (programID: int) : unit =
        C """
        GLint pos = glGetAttribLocation (programID, "in_model");

        int pos1 = pos + 0; 
        int pos2 = pos + 1; 
        int pos3 = pos + 2; 
        int pos4 = pos + 3; 
        glEnableVertexAttribArray(pos1);
        glEnableVertexAttribArray(pos2);
        glEnableVertexAttribArray(pos3);
        glEnableVertexAttribArray(pos4);
        glVertexAttribPointer(pos1, 4, GL_FLOAT, GL_FALSE, sizeof(GLfloat) * 4 * 4, (void*)(0));
        glVertexAttribPointer(pos2, 4, GL_FLOAT, GL_FALSE, sizeof(GLfloat) * 4 * 4, (void*)(sizeof(float) * 4));
        glVertexAttribPointer(pos3, 4, GL_FLOAT, GL_FALSE, sizeof(GLfloat) * 4 * 4, (void*)(sizeof(float) * 8));
        glVertexAttribPointer(pos4, 4, GL_FLOAT, GL_FALSE, sizeof(GLfloat) * 4 * 4, (void*)(sizeof(float) * 12));
        glVertexAttribDivisor(pos1, 1);
        glVertexAttribDivisor(pos2, 1);
        glVertexAttribDivisor(pos3, 1);
        glVertexAttribDivisor(pos4, 1);
        """

    [<Import; MI (MIO.NoInlining)>]
    let getUniformProjection (program: int) : int =
        C """
        return glGetUniformLocation (program, "uni_projection");
        """

    [<Import; MI (MIO.NoInlining)>]
    let setUniformMatrix4x4 (uni: int) (m: Matrix4x4)  : unit =
        C """
        glUniformMatrix4fv (uni, 1, GL_FALSE, &m);
        """

    [<Import; MI (MIO.NoInlining)>]
    let getUniformColor (program: int) : int =
        C """
        GLint uni_color = glGetUniformLocation (program, "uni_color");
        return uni_color;
        """

    [<Import; MI (MIO.NoInlining)>]
    let setUniformColor (uniformColor: int) (color: RenderColor) : unit =
        C """
        glUniform4f (uniformColor, color.R, color.G, color.B, color.A);
        """

    [<Import; MI (MIO.NoInlining)>]
    let setUniformVector4 (v: Vector4) (uniformId: int) : unit =
        C """
        glUniform4f (uniformId, v.X, v.Y, v.Z, v.W);
        """

    [<Import; MI (MIO.NoInlining)>]
    let setTexture (shaderProgram: int) (textureId: int) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_texture");
        glUniform1i(textureId, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindTexture (textureId: int) : unit =
        C """
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, textureId);
        """

    [<Import; MI (MIO.NoInlining)>]
    let createTexture (width: int) (height: int) (data: nativeint) : int =
        C """
        // Create one OpenGL texture
        GLuint textureID;
        glGenTextures(1, &textureID);
         
        // "Bind" the newly created texture : all future texture functions will modify this texture
        glBindTexture(GL_TEXTURE_2D, textureID);
         
        // Give the image to OpenGL
        glTexImage2D(GL_TEXTURE_2D, 0,GL_RGBA, width, height, 0, GL_BGRA, GL_UNSIGNED_BYTE, data);
         
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        glTexParameteri( GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT );
        glTexParameteri( GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT );
        return textureID;
        """

    [<Import; MI (MIO.NoInlining)>]
    let enableDepth () : unit =
        C """
        // Enable depth test
        glEnable(GL_DEPTH_TEST);
        // Accept fragment if it closer to the camera than the former one
        glDepthFunc(GL_LESS); 
        // Cull triangles which normal is not towards the camera
        glEnable(GL_CULL_FACE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let disableDepth () : unit =
        C """
        glDisable(GL_CULL_FACE);
        glDisable(GL_DEPTH_TEST);
        """

    [<Import; MI (MIO.NoInlining)>]
    let enableStencilTest () : unit =
        C """
        glEnable(GL_STENCIL_TEST);
        """

    [<Import; MI (MIO.NoInlining)>]
    let disableStencilTest () : unit =
        C """
        glDisable(GL_STENCIL_TEST);
        """

    [<Import; MI (MIO.NoInlining)>]
    let enableBlend () : unit =
        C """
        glEnable (GL_BLEND);
        glBlendFunc (GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        """

    [<Import; MI (MIO.NoInlining)>]
    let disableBlend () : unit =
        C """
        glDisable (GL_BLEND);
        """

    [<Import; MI (MIO.NoInlining)>]
    let createFramebuffer () : int =
        C """
        GLuint id = 0;
        glGenFramebuffers (1, &id);
        return id;
        """

    [<Import; MI (MIO.NoInlining)>]
    let bindFramebuffer (id: int) : unit =
        C """
        glBindFramebuffer (GL_FRAMEBUFFER, id);
        """

    [<Import; MI (MIO.NoInlining)>]
    let createFramebufferTexture (width: int) (height: int) (data: nativeint) : int =
        C """
        // Create one OpenGL texture
        GLuint textureID;
        glGenTextures(1, &textureID);
         
        // "Bind" the newly created texture : all future texture functions will modify this texture
        glBindTexture(GL_TEXTURE_2D, textureID);
         
        // Give the image to OpenGL
        //glTexImage2D(GL_TEXTURE_2D, 0, GL_DEPTH32F_STENCIL8, width, height, 0, GL_DEPTH_STENCIL, GL_FLOAT_32_UNSIGNED_INT_24_8_REV, data);
        glTexImage2D (GL_TEXTURE_2D, 0, GL_RGB, width, height, 0, GL_RGB, GL_UNSIGNED_BYTE, data);
         
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

        return textureID;
        """

    [<Import; MI (MIO.NoInlining)>]
    let createRenderbuffer (width: int) (height: int) : int =
        C """
        GLuint depthrenderbuffer;
        glGenRenderbuffers(1, &depthrenderbuffer);
        glBindRenderbuffer(GL_RENDERBUFFER, depthrenderbuffer);
        glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH32F_STENCIL8, width, height);
        glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_STENCIL_ATTACHMENT, GL_RENDERBUFFER, depthrenderbuffer);
        glBindRenderbuffer (GL_RENDERBUFFER, 0);
        return depthrenderbuffer;
        """

    [<Import; MI (MIO.NoInlining)>]
    let setFramebufferTexture  (textureId: int) : unit =
        C """
        // Set "renderedTexture" as our colour attachement #0
        glFramebufferTexture (GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, textureId, 0);

        // Set the list of draw buffers.
        GLenum DrawBuffers[1] = {GL_COLOR_ATTACHMENT0};
        glDrawBuffers(1, DrawBuffers); // "1" is the size of DrawBuffers
        """

    [<Import; MI (MIO.NoInlining)>]
    let framebufferRender (width: int) (height: int) (id: int) : unit =
        C """
        glBindFramebuffer (GL_FRAMEBUFFER, id);
        glViewport(0,0,width,height); // Render on the whole framebuffer, complete from the lower left corner to the upper right
        """

    [<Import; MI (MIO.NoInlining)>]
    let enableCullFace () : unit =
        C """
        glEnable(GL_CULL_FACE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let disableCullFace () : unit =
        C """
        glDisable(GL_CULL_FACE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let clearDepth () : unit =
        C """
        glClear(GL_DEPTH_BUFFER_BIT);
        """

    [<Import; MI (MIO.NoInlining)>]
    let clearColor () : unit =
        C """
        glClear(GL_COLOR_BUFFER_BIT);
        """

    [<Import; MI (MIO.NoInlining)>]
    let clearStencil () : unit =
        C """
        glClear(GL_STENCIL_BUFFER_BIT);
        """

    [<Import; MI (MIO.NoInlining)>]
    let deleteBuffer (id: int) : unit =
        C """
        glDeleteBuffers (1, &id);
        """

    [<Import; MI (MIO.NoInlining)>]
    let deleteTexture (id: int) : unit =
        C """
        glDeleteTextures (1, &id);
        """

    [<Import; MI (MIO.NoInlining)>]
    let colorMaskFalse () : unit =
        C """
        glColorMask(GL_FALSE, GL_FALSE, GL_FALSE, GL_FALSE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let colorMaskTrue () : unit =
        C """
        glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let stencil1 () : unit =
        C """
        glStencilFunc(GL_ALWAYS, 1, 0xFF); // Set any stencil to 1
        glStencilOp(GL_KEEP, GL_ZERO, GL_REPLACE);
        glStencilMask(0xFF); // Write to stencil buffer
        glClear(GL_STENCIL_BUFFER_BIT); // Clear stencil buffer (0 by default)
        """

    [<Import; MI (MIO.NoInlining)>]
    let stencil2 () : unit =
        C """
        glStencilFunc(GL_EQUAL, 1, 0xFF); // Pass test if stencil value is 1
        glStencilMask(0x00); // Don't write anything to stencil buffer
        """

    [<Import; MI (MIO.NoInlining)>]
    let depthMaskFalse () : unit =
        C """
        glDepthMask(GL_FALSE);
        """

    [<Import; MI (MIO.NoInlining)>]
    let depthMaskTrue () : unit =
        C """
        glDepthMask(GL_TRUE);
        """
