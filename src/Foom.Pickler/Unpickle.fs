module Foom.Pickler.Unpickle

open System
open System.IO
open System.Text
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type LiteReadStream = private {
    mutable bytes: byte []
    mutable position: int
    Stream: Stream option }

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module LiteReadStream =
    let ofBytes bytes = { bytes = bytes; position = 0; Stream = None }

    let ofStream stream = { bytes = Array.empty; position = -1; Stream = Some stream }

    let position lstream =
        match lstream.Stream with
        | None -> int64 lstream.position
        | Some stream -> stream.Position

    let seek offset lstream =
        match lstream.Stream with
        | None -> lstream.position <- int offset
        | Some stream -> stream.Position <- offset

    let skip (n: int64) lstream = 
        match lstream.Stream with
        | None -> lstream.position <- lstream.position + int n
        | Some stream -> stream.Position <- stream.Position + n

    let readByte lstream =
        match lstream.Stream with
        | None ->
            let result = lstream.bytes.[int lstream.position]
            lstream.position <- lstream.position + 1
            result
        | Some stream ->
            match stream.ReadByte () with
            | -1 -> failwith "Unable to read byte from stream."
            | result -> byte result

    let readBytes n lstream =
        match lstream.Stream with
        | None ->
            let i = lstream.position
            lstream.position <- lstream.position + n
            lstream.bytes.[int i..int lstream.position]
        | Some stream ->
            let mutable bytes = Array.zeroCreate<byte> n
            stream.Read (bytes, 0, n) |> ignore
            bytes

    let readString (n: int) lstream =
        match lstream.Stream with
        | None ->
            let s : nativeptr<char> = (NativePtr.ofNativeInt <| NativePtr.toNativeInt &&lstream.bytes.[int lstream.position])
            let result = String (s, 0, n * sizeof<char>)
            lstream.position <- lstream.position + n
            result
        | Some stream ->
            let mutable bytes = Array.zeroCreate<byte> (n)
            stream.Read (bytes, 0, n) |> ignore
            let s : nativeptr<char> = NativePtr.ofNativeInt <| NativePtr.toNativeInt &&bytes.[0]
            String (s, 0, n * sizeof<char>)

    let read<'a when 'a : unmanaged> lstream =
        match lstream.Stream with
        | None ->
            let result = NativePtr.read (NativePtr.ofNativeInt<'a> <| NativePtr.toNativeInt &&lstream.bytes.[int lstream.position])
            lstream.position <- lstream.position + sizeof<'a>
            result
        | Some stream ->
            let n = sizeof<'a>
            let mutable bytes = Array.zeroCreate<byte> n 
            stream.Read (bytes, 0, n) |> ignore
            NativePtr.read (NativePtr.ofNativeInt<'a> <| NativePtr.toNativeInt &&bytes.[0])

type Unpickle<'a> = LiteReadStream -> 'a

let u_byte : Unpickle<byte> =
    fun stream -> LiteReadStream.readByte stream

let u_bytes n : Unpickle<byte []> =
    fun stream -> LiteReadStream.readBytes n stream

let u_int16 : Unpickle<int16> =
    fun stream -> LiteReadStream.read stream

let u_uint16 : Unpickle<uint16> =
    fun stream -> LiteReadStream.read stream

let u_int32 : Unpickle<int> =
    fun stream -> LiteReadStream.read stream

let u_uint32 : Unpickle<uint32> =
    fun stream -> LiteReadStream.read stream

let u_single : Unpickle<single> =
    fun stream -> LiteReadStream.read stream

let u_int64 : Unpickle<int64> =
    fun stream -> LiteReadStream.read stream

let u_uint64 : Unpickle<uint64> =
    fun stream -> LiteReadStream.read stream

let inline u_string n : Unpickle<string> =
    fun stream -> LiteReadStream.readString n stream

let inline u_pipe2 a b f : Unpickle<_> =
    fun stream -> f (a stream) (b stream)

let inline u_pipe3 a b c f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream)

let inline u_pipe4 a b c d f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream)

let inline u_pipe5 a b c d e f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream)

let inline u_pipe6 a b c d e g f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream)

let inline u_pipe7 a b c d e g h f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream)

let inline u_pipe8 a b c d e g h i f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream)

let inline u_pipe9 a b c d e g h i j f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream) (j stream)

let inline u_pipe10 a b c d e g h i j k f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream) (j stream) (k stream)

let inline u_pipe11 a b c d e g h i j k l f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream) (j stream) (k stream) (l stream)

let inline u_pipe12 a b c d e g h i j k l m f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream) (j stream) (k stream) (l stream) (m stream)

let inline u_pipe13 a b c d e g h i j k l m n f : Unpickle<_> =
    fun stream -> f (a stream) (b stream) (c stream) (d stream) (e stream) (g stream) (h stream) (i stream) (j stream) (k stream) (l stream) (m stream) (n stream)

let inline u<'a when 'a : unmanaged> : Unpickle<_> =
    fun stream -> LiteReadStream.read<'a> stream

let inline u_array n (p: Unpickle<'a>) =
    fun stream ->
        match n with
        | 0 -> [||]
        | _ -> Array.init n (fun _ -> p stream)

let inline u_skipBytes n : Unpickle<_> =
    fun stream -> LiteReadStream.skip n stream

let inline u_lookAhead (p: Unpickle<'a>) : Unpickle<'a> =
    fun lstream ->
        let prevPosition = LiteReadStream.position lstream
        let result = p lstream
        LiteReadStream.seek prevPosition lstream
        result

// fmap
let inline (|>>) (u: Unpickle<'a>) (f: 'a -> 'b) : Unpickle<'b> =
    fun stream -> f (u stream)

let inline (<*>) (u1: Unpickle<'a -> 'b>) (u2: Unpickle<'a>) : Unpickle<'b> =
    fun stream -> u1 stream (u2 stream)

let inline (>>=) (u: Unpickle<'a>) (f: 'a -> Unpickle<'b>) : Unpickle<'b> =
    fun stream -> f (u stream) stream

let inline (>>.) (u1: Unpickle<'a>) (u2: Unpickle<'b>) =
    fun stream ->
        u1 stream |> ignore
        u2 stream

let inline (.>>) (u1: Unpickle<'a>) (u2: Unpickle<'b>) =
    fun stream ->
        let result = u1 stream
        u2 stream |> ignore
        result

let inline (.>>.) (u1: Unpickle<'a>) (u2: Unpickle<'b>) =
    fun stream ->
        u1 stream,
        u2 stream

let inline u_run (p: Unpickle<_>) x = p x