module Foom.Pickler.Pickle

open System
open System.IO
open System.Text
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type LiteWriteStream = private {
    mutable bytes: byte []
    mutable position: int
    Stream: Stream option }

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module LiteWriteStream =
    let ofBytes bytes = { bytes = bytes; position = 0; Stream = None }

    let ofStream stream = { bytes = Array.empty; position = -1; Stream = Some stream }

    let position lstream =
        match lstream.Stream with
        | None -> int64 lstream.position
        | Some stream -> stream.Position

    let seek offset lstream =
        match lstream.Stream with
        | None ->  lstream.position <- int offset
        | Some stream -> stream.Position <- offset

    let skip n lstream = 
        match lstream.Stream with
        | None -> lstream.position <- lstream.position + int n
        | Some stream -> stream.Position <- stream.Position + n

    let writeByte x lstream =
        match lstream.Stream with
        | None ->
            lstream.bytes.[int lstream.position] <- x
            lstream.position <- lstream.position + 1
        | Some stream ->
            stream.WriteByte x

    let writeBytes (n: int) (xs: byte []) lstream =
        match lstream.Stream with
        | None ->
            for i = 0 to n - 1 do
                lstream.bytes.[int lstream.position] <- xs.[i]
                lstream.position <- lstream.position + 1
        | Some stream ->
            stream.Write (xs, 0, int n)

    let writeString (n: int) kind (str: string) lstream =
        let encoding =
            match kind with
            | BigEndianUnicode -> System.Text.Encoding.BigEndianUnicode
            | Unicode -> System.Text.Encoding.Unicode
            | UTF8 -> System.Text.Encoding.UTF8

        let bytes = encoding.GetBytes (str)
        let length = bytes.Length

        for i = 0 to n - 1 do
            if i >= length
            then writeByte (0uy) lstream
            else writeByte (bytes.[i]) lstream

    let write<'a when 'a : unmanaged> (x: 'a) lstream =
        let mutable x = x
        let size = sizeof<'a>
        let ptr : nativeptr<byte> = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt

        for i = 1 to size do
            writeByte (NativePtr.get ptr (i - 1)) lstream

type Pickle<'a> = 'a -> LiteWriteStream -> unit

let p_byte : Pickle<byte> =
    fun x stream -> LiteWriteStream.writeByte x stream

let inline p_bytes n : Pickle<byte []> =
    fun xs stream -> LiteWriteStream.writeBytes n xs stream

let p_int16 : Pickle<int16> =
    fun x stream -> LiteWriteStream.write x stream

let p_int32 : Pickle<int32> =
    fun x stream -> LiteWriteStream.write x stream

let p_single : Pickle<single> =
    fun x stream -> LiteWriteStream.write x stream

let inline p_string n kind : Pickle<string> =
    fun x stream -> LiteWriteStream.writeString n kind x stream

let inline p_pipe2 a b f : Pickle<_> =
    fun x stream -> 
        let a',b' = f x
        (a a' stream)
        (b b' stream)

let inline p_pipe3 a b c f : Pickle<_> =
    fun x stream -> 
        let a',b',c' = f x
        (a a' stream)
        (b b' stream)
        (c c' stream)

let inline p_pipe4 a b c d f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')

let inline p_pipe5 a b c d e f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e' = f x
        (a a' stream)
        (b b' stream)
        (c c' stream)
        (d d' stream)
        (e e' stream)

let inline p_pipe6 a b c d e g f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')

let inline p_pipe7 a b c d e g h f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')

let inline p_pipe8 a b c d e g h i f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')
        (i stream i')

let inline p_pipe9 a b c d e g h i j f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i',j' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')
        (i stream i')
        (j stream j')

let inline p_pipe10 a b c d e g h i j k f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i',j',k' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')
        (i stream i')
        (j stream j')
        (k stream k')

let inline p_pipe11 a b c d e g h i j k l f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i',j',k', l' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')
        (i stream i')
        (j stream j')
        (k stream k')
        (l stream l')

let inline p_pipe12 a b c d e g h i j k l m f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i',j',k', l',m' = f x
        (a a' stream)
        (b b' stream)
        (c c' stream)
        (d d' stream)
        (e e' stream)
        (g g' stream)
        (h h' stream)
        (i i' stream)
        (j j' stream)
        (k k' stream)
        (l l' stream)
        (m m' stream)

let inline p_pipe13 a b c d e g h i j k l m n f : Pickle<_> =
    fun x stream -> 
        let a',b',c',d',e',g',h',i',j',k', l',m',n' = f x
        (a stream a')
        (b stream b')
        (c stream c')
        (d stream d')
        (e stream e')
        (g stream g')
        (h stream h')
        (i stream i')
        (j stream j')
        (k stream k')
        (l stream l')
        (m stream m')
        (n stream n')

let p : Pickle<_> =
    fun x stream -> LiteWriteStream.write x stream

let inline p_array n (p: Pickle<'a>) : Pickle<'a[]> =
    fun xs stream ->
        match n with
        | 0 -> ()
        | _ -> for i = 0 to n - 1 do p xs.[i] stream

let inline p_skipBytes n : Pickle<_> =
    fun _ stream -> LiteWriteStream.skip n stream

let inline p_lookAhead (p: Pickle<_>) : Pickle<_> =
    fun x stream ->
        let prevPosition = LiteWriteStream.position stream
        p x stream
        LiteWriteStream.seek prevPosition stream

// contramap
// Note: This might be bad.
let inline (-|>>) (p: Pickle<'a>) (f: 'b -> 'a) : Pickle<'b> =
    fun b' stream -> p (f b') stream

// ?
// Note: This might be bad.
let inline (->>=) (p: Pickle<'a>) (f: 'b -> 'a * Pickle<'b>) : Pickle<'b> =
    fun b' stream ->
        let a', p2 = f b'
        p a' stream
        p2 b' stream

let inline p_run (p: Pickle<_>) x stream = p x stream
