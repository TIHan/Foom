namespace Foom.Network

open System
open System.IO
open System.Runtime.InteropServices

open FSharp.NativeInterop

// Disable native interop warnings
#nowarn "9"
#nowarn "51"

[<Struct; StructLayout (LayoutKind.Explicit)>]
type DoubleUnion =

    [<FieldOffset (0)>]
    val mutable Value : uint32

    [<FieldOffset (0)>]
    val mutable DoubleValue : double

[<Struct; StructLayout (LayoutKind.Explicit)>]
type SingleUnion =

    [<FieldOffset (0)>]
    val mutable Value : uint32

    [<FieldOffset (0)>]
    val mutable SingleValue : single

module LitteEndian =

    let inline write8 (data: byte []) offset value =
        data.[int offset] <- byte value

    let inline write16 (data: byte []) offset value =
        let offset = int offset
        data.[offset] <- byte value
        data.[offset + 1] <- byte (value >>> 8)

    let inline write32 (data: byte []) offset value =
        let offset = int offset
        data.[offset] <- byte value
        data.[offset + 1] <- byte (value >>> 8)
        data.[offset + 2] <- byte (value >>> 16)
        data.[offset + 3] <- byte (value >>> 24)

    let inline read8 (data: byte []) offset =
        data.[int offset]

    let inline read16 (data: byte []) offset =
        let offset = int offset
        (uint16 data.[offset]) |||
        ((uint16 data.[offset + 1]) <<< 8)

    let inline read32 (data: byte []) offset =
        let offset = int offset
        (uint32 data.[offset]) |||
        ((uint32 data.[offset + 1]) <<< 8) |||
        ((uint32 data.[offset + 2]) <<< 16) |||
        ((uint32 data.[offset + 3]) <<< 24)

[<AutoOpen>]
module DeltaHelpers =

    let inline clampBitOffset bitOffset =
        if bitOffset = 7 then 0
        else bitOffset + 1
    

type ByteStream (data : byte []) as this =
    inherit Stream ()

    let writer = ByteWriter this
    let reader = ByteReader this

    let mutable length = 0L
    let mutable position = 0L

    member this.Raw = data

    member this.Writer = writer

    member this.Reader = reader

    // Stream Implementation

    override this.CanWrite = true

    override this.Length = length

    override this.SetLength value =
        if position > value then
            position <- value
        length <- value

    override this.Position
        with get () = position
        and set value = position <- value

    override this.Flush () = ()

    override this.CanRead = true

    override this.CanSeek = false

    override this.Read (bytes, offset, count) =
        let remaining = int (length - position)
        let count = if count > remaining then remaining else count
        Buffer.BlockCopy (data, int position, bytes, offset, count)
        position <- position + int64 count
        count

    override this.Seek (offset, origin) = raise <| NotImplementedException ()

    override this.Write (bytes, offset, count) =
        Buffer.BlockCopy (bytes, offset, data, int position, count)
        position <- position + int64 count
        if position > length then
            length <- position

and [<Sealed>] ByteWriter (byteStream: ByteStream) =

    let mutable deltaByte = 0uy
    let mutable deltaIndex = 0
    let mutable bitOffset = 0

    member this.WriteDeltaByte (prev, next) =
        if bitOffset = 0 then
            deltaByte <- 0uy
            deltaIndex <- int this.Position
            this.WriteByte deltaByte

        if prev <> next then
            deltaByte <- deltaByte ||| (1uy <<< bitOffset)
            byteStream.Raw.[deltaIndex] <- deltaByte
            this.WriteByte next

        bitOffset <- clampBitOffset bitOffset

    member this.WriteDeltaInt16 (prev, next) =
        if bitOffset = 0 then
            deltaByte <- 0uy
            deltaIndex <- int this.Position
            this.WriteByte deltaByte

        if prev <> next then
            deltaByte <- deltaByte ||| (1uy <<< bitOffset)
            byteStream.Raw.[deltaIndex] <- deltaByte
            this.WriteInt16 next

        bitOffset <- clampBitOffset bitOffset

    member this.WriteDeltaUInt16 (prev, next) =
        if bitOffset = 0 then
            deltaByte <- 0uy
            deltaIndex <- int this.Position
            this.WriteByte deltaByte

        if prev <> next then
            deltaByte <- deltaByte ||| (1uy <<< bitOffset)
            byteStream.Raw.[deltaIndex] <- deltaByte
            this.WriteUInt16 next

        bitOffset <- clampBitOffset bitOffset

    member this.WriteDeltaInt (prev : int, next : int) =
        if bitOffset = 0 then
            deltaByte <- 0uy
            deltaIndex <- int this.Position
            this.WriteByte deltaByte

        if prev <> next then
            deltaByte <- deltaByte ||| (1uy <<< bitOffset)
            byteStream.Raw.[deltaIndex] <- deltaByte
            this.WriteInt next

        bitOffset <- clampBitOffset bitOffset

    member this.WriteByte (value: byte) =
        let len = byteStream.Position + 1L
        if len > byteStream.Length then
            byteStream.SetLength len

        LitteEndian.write8 byteStream.Raw byteStream.Position value

        byteStream.Position <- len

    member this.WriteSByte (value: sbyte) =
        let len = byteStream.Position + 1L
        if len > byteStream.Length then
            byteStream.SetLength len

        LitteEndian.write8 byteStream.Raw byteStream.Position value

        byteStream.Position <- byteStream.Position + 1L

    member this.WriteInt16 (value: int16) =
        let len = byteStream.Position + 2L
        if len > byteStream.Length then
            byteStream.SetLength len

        LitteEndian.write16 byteStream.Raw byteStream.Position value

        byteStream.Position <- byteStream.Position + 2L

    member this.WriteUInt16 (value: uint16) =
        let len = byteStream.Position + 2L
        if len > byteStream.Length then
            byteStream.SetLength  len

        LitteEndian.write16 byteStream.Raw byteStream.Position value

        byteStream.Position <- byteStream.Position + 2L

    member this.WriteInt (value: int) =
        let len = byteStream.Position + 4L
        if len > byteStream.Length then
            byteStream.SetLength  len

        LitteEndian.write32 byteStream.Raw byteStream.Position value

        byteStream.Position <- byteStream.Position + 4L

    member this.WriteUInt32 (value: uint32) =
        let len = byteStream.Position + 4L
        if len > byteStream.Length then
            byteStream.SetLength len

        LitteEndian.write32 byteStream.Raw byteStream.Position value

        byteStream.Position <- byteStream.Position + 4L

    member this.WriteSingle (value: single) =
        let mutable s = SingleUnion ()
        s.SingleValue <- value
        this.WriteUInt32 (s.Value)

    member this.WriteInts (values: int [], startIndex, size) =
        this.WriteInt size
        let size = size * 4

        let len = byteStream.Position + int64 size
        if len > byteStream.Length then
            byteStream.SetLength len

        Buffer.BlockCopy (values, startIndex, byteStream.Raw, int byteStream.Position, size)

        byteStream.Position <- byteStream.Position + int64 size

    member this.Write<'T when 'T : unmanaged> (value: 'T) =
        let mutable value = value
        let size = sizeof<'T>

        let ptr = &&value |> NativePtr.toNativeInt

        let len = byteStream.Position + int64 size
        if len > byteStream.Length then
            byteStream.SetLength len

        Marshal.Copy (ptr, byteStream.Raw, int byteStream.Position, size)

        byteStream.Position <- byteStream.Position + int64 size

    member this.Write<'T when 'T : unmanaged> (value: byref<'T>) =
        let size = sizeof<'T>

        let ptr = &&value |> NativePtr.toNativeInt

        let len = byteStream.Position + int64 size
        if len > byteStream.Length then
            byteStream.SetLength len

        Marshal.Copy (ptr, byteStream.Raw, int byteStream.Position, size)

        byteStream.Position <- byteStream.Position + int64 size

    member this.Position = byteStream.Position

and [<Sealed>] ByteReader (byteStream: ByteStream) =

    let mutable deltaByte = 0uy
    let mutable deltaIndex = 0
    let mutable bitOffset = 0

    member this.ReadDeltaByte (current) =
        if bitOffset = 0 then
            deltaIndex <- int this.Position
            deltaByte <- this.ReadByte ()

        if (deltaByte &&& (1uy <<< bitOffset)) > 0uy then
            bitOffset <- clampBitOffset bitOffset
            this.ReadByte ()
        else
            bitOffset <- clampBitOffset bitOffset
            current

    member this.ReadDeltaInt16 (current) =
        if bitOffset = 0 then
            deltaIndex <- int this.Position
            deltaByte <- this.ReadByte ()

        if (deltaByte &&& (1uy <<< bitOffset)) > 0uy then
            bitOffset <- clampBitOffset bitOffset
            this.ReadInt16 ()
        else
            bitOffset <- clampBitOffset bitOffset
            current

    member this.ReadDeltaUInt16 (current) =
        if bitOffset = 0 then
            deltaIndex <- int this.Position
            deltaByte <- this.ReadByte ()

        if (deltaByte &&& (1uy <<< bitOffset)) > 0uy then
            bitOffset <- clampBitOffset bitOffset
            this.ReadUInt16 ()
        else
            bitOffset <- clampBitOffset bitOffset
            current

    member this.ReadDeltaInt (current : int) =
        if bitOffset = 0 then
            deltaIndex <- int this.Position
            deltaByte <- this.ReadByte ()

        if (deltaByte &&& (1uy <<< bitOffset)) > 0uy then
            bitOffset <- clampBitOffset bitOffset
            this.ReadInt ()
        else
            bitOffset <- clampBitOffset bitOffset
            current

    member this.ReadByte () : byte =
        let value = LitteEndian.read8 byteStream.Raw byteStream.Position
        byteStream.Position <- byteStream.Position + 1L
        value

    member this.ReadSByte () : sbyte =
        let value = LitteEndian.read8 byteStream.Raw byteStream.Position |> sbyte
        byteStream.Position <- byteStream.Position + 1L
        value

    member this.ReadInt16 () : int16 =
        let value = LitteEndian.read16 byteStream.Raw byteStream.Position |> int16
        byteStream.Position <- byteStream.Position + 2L
        value

    member this.ReadUInt16 () : uint16 =
        let value = LitteEndian.read16 byteStream.Raw byteStream.Position |> uint16
        byteStream.Position <- byteStream.Position + 2L
        value

    member this.ReadInt () : int =
        let value = LitteEndian.read32 byteStream.Raw byteStream.Position |> int
        byteStream.Position <- byteStream.Position + 4L
        value

    member this.ReadUInt32 () : uint32 =
        let value = LitteEndian.read32 byteStream.Raw byteStream.Position |> uint32
        byteStream.Position <- byteStream.Position + 4L
        value

    member this.ReadSingle () : single =
        let value = this.ReadUInt32 ()
        let mutable s = SingleUnion ()
        s.Value <- value
        s.SingleValue

    member this.Read<'T when 'T : unmanaged> () : 'T =
        let size = sizeof<'T>

        let mutable value = Unchecked.defaultof<'T>
        let ptr = &&value |> NativePtr.toNativeInt
        Marshal.Copy (byteStream.Raw, int byteStream.Position, ptr, size)
        byteStream.Position <- byteStream.Position + int64 size
        value

    member this.Read<'T when 'T : unmanaged> (value: byref<'T>) =
        let size = sizeof<'T>
        let ptr = &&value |> NativePtr.toNativeInt
        Marshal.Copy (byteStream.Raw, int byteStream.Position, ptr, size)
        byteStream.Position <- byteStream.Position + int64 size

    member this.ReadInts (buffer: int []) =
        let len = this.ReadInt ()

        if buffer.Length < len then
            failwith "Buffer is too small for reading an array of ints."

        let size = len * 4
        Buffer.BlockCopy (byteStream.Raw, int byteStream.Position, buffer, 0, size)
        byteStream.Position <- byteStream.Position + int64 size
        len

    member this.IsEndOfStream = byteStream.Position = byteStream.Length

    member this.Position = byteStream.Position
