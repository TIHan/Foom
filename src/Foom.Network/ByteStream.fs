namespace Foom.Network

open System
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
        data.[offset] <- byte value

    let inline write16 (data: byte []) offset value =
        data.[offset] <- byte value
        data.[offset + 1] <- byte (value >>> 8)

    let inline write32 (data: byte []) offset value =
        data.[offset] <- byte value
        data.[offset + 1] <- byte (value >>> 8)
        data.[offset + 2] <- byte (value >>> 16)
        data.[offset + 3] <- byte (value >>> 24)

    let inline read8 (data: byte []) offset =
        data.[offset]

    let inline read16 (data: byte []) offset =
        (uint16 data.[offset]) |||
        ((uint16 data.[offset + 1]) <<< 8)

    let inline read32 (data: byte []) offset =
        (uint32 data.[offset]) |||
        ((uint32 data.[offset + 1]) <<< 8) |||
        ((uint32 data.[offset + 2]) <<< 16) |||
        ((uint32 data.[offset + 3]) <<< 24)

module private InternalStream =

    let inline checkBounds offset length position =
        position + offset <= length

    //let inline checkBoundsNoException offset (data: byte []) length position =
    //    if position + offset >= length then
    //        false
    //    else
    //        true

    let setLength value (data: byte []) (position: byref<int>) (length: byref<int>) =
        if value > data.Length || value < 0 then
            failwith "Length cannot be set because it is outside the bounds of the byte array."

        if position > value then
            position <- value

        length <- value

    let inline setPosition value (data: byte []) length (position: byref<int>) =
        if value < 0 || value > length then
            failwith "Position cannot be set because it is greater than or equal to the length."

        position <- value

open InternalStream

type ByteStream (data : byte []) =

    [<DefaultValue>] val mutable length : int
    [<DefaultValue>] val mutable position : int

    member this.CheckBounds offset = 
        if checkBounds offset data.Length this.position |> not then
            failwith "Outside the bounds of the array."

    member this.CheckBoundsLength offset = 
        if checkBounds offset this.length this.position |> not then
            failwith "Outside the bounds of the stream."

    //member this.TryResize offset =
    //    if not (checkBoundsNoException offset data this.position) then
    //        let newLength = uint32 data.Length * 2u
    //        let newLength =
    //            if newLength < uint32 offset then
    //                uint32 offset
    //            else
    //                newLength

    //        if newLength >= uint32 Int32.MaxValue then
    //            failwith "Length is bigger than the maximum number of elements in the array"

    //        let newData = Array.zeroCreate (int newLength)
    //        Array.Copy (data, newData, data.Length)
    //        data <- newData

    member this.Raw = data

    member this.Length 
        with get () = this.length
        and set value = 
            let lengthToClear = value
            setLength value data &this.position &this.length
            Array.Clear (data, lengthToClear, this.length - value)

    member this.Position
        with get () = this.position
        and set value = setPosition value data this.length &this.position

[<Sealed>]
type ByteWriter (byteStream: ByteStream) =

    member this.WriteByte (value: byte) =
        byteStream.CheckBounds 1

        LitteEndian.write8 byteStream.Raw byteStream.position value
        let len = byteStream.position + 1
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 1

    member this.WriteSByte (value: sbyte) =
        byteStream.CheckBounds 1

        LitteEndian.write8 byteStream.Raw byteStream.position value
        let len = byteStream.position + 1
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 1

    member this.WriteInt16 (value: int16) =
        byteStream.CheckBounds 2

        LitteEndian.write16 byteStream.Raw byteStream.position value
        let len = byteStream.position + 2
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 2

    member this.WriteUInt16 (value: uint16) =
        byteStream.CheckBounds 2

        LitteEndian.write16 byteStream.Raw byteStream.position value
        let len = byteStream.position + 2
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 2

    member this.WriteInt (value: int) =
        byteStream.CheckBounds 4

        LitteEndian.write32 byteStream.Raw byteStream.position value
        let len = byteStream.position + 4
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 4

    member this.WriteUInt32 (value: uint32) =
        byteStream.CheckBounds 4

        LitteEndian.write32 byteStream.Raw byteStream.position value
        let len = byteStream.position + 4
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + 4

    member this.WriteSingle (value: single) =
        let mutable s = SingleUnion ()
        s.SingleValue <- value
        this.WriteUInt32 (s.Value)

    member this.WriteBytes (bytes: byte []) =
        byteStream.CheckBounds bytes.Length

        Buffer.BlockCopy (bytes, 0, byteStream.Raw, byteStream.position, bytes.Length)
        let len = byteStream.position + bytes.Length
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + bytes.Length

    member this.WriteRawBytes (bytes: byte [], startIndex, size) =
        byteStream.CheckBounds size

        Buffer.BlockCopy (bytes, startIndex, byteStream.Raw, byteStream.position, size)
        let len = byteStream.position + size
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + size

    member this.WriteInts (values: int [], startIndex, size) =
        this.WriteInt size
        let size = size * 4

        Buffer.BlockCopy (values, startIndex, byteStream.Raw, byteStream.position, size)
        let len = byteStream.position + size
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + size

    member this.Write<'T when 'T : unmanaged> (value: 'T) =
        let mutable value = value
        let size = sizeof<'T>

        byteStream.CheckBounds size

        let ptr = &&value |> NativePtr.toNativeInt

        Marshal.Copy (ptr, byteStream.Raw, byteStream.position, size)
        let len = byteStream.position + size
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + size

    member this.Write<'T when 'T : unmanaged> (value: byref<'T>) =
        let size = sizeof<'T>

        byteStream.CheckBounds size

        let ptr = &&value |> NativePtr.toNativeInt

        Marshal.Copy (ptr, byteStream.Raw, byteStream.position, size)
        let len = byteStream.position + size
        if len > byteStream.length then
            byteStream.length <- len
        byteStream.position <- byteStream.position + size

[<Sealed>]
type ByteReader (byteStream: ByteStream) =

    member this.ReadByte () : byte =
        byteStream.CheckBoundsLength 1

        let value = LitteEndian.read8 byteStream.Raw byteStream.position |> byte
        byteStream.position <- byteStream.position + 1
        value

    member this.ReadSByte () : sbyte =
        byteStream.CheckBoundsLength 1

        let value = LitteEndian.read8 byteStream.Raw byteStream.position |> sbyte
        byteStream.position <- byteStream.position + 1
        value

    member this.ReadInt16 () : int16 =
        byteStream.CheckBoundsLength 2

        let value = LitteEndian.read16 byteStream.Raw byteStream.position |> int16
        byteStream.position <- byteStream.position + 2
        value

    member this.ReadUInt16 () : uint16 =
        byteStream.CheckBoundsLength 2

        let value = LitteEndian.read16 byteStream.Raw byteStream.position |> uint16
        byteStream.position <- byteStream.position + 2
        value

    member this.ReadInt () : int =
        byteStream.CheckBoundsLength 4

        let value = LitteEndian.read32 byteStream.Raw byteStream.position |> int
        byteStream.position <- byteStream.position + 4
        value

    member this.ReadUInt32 () : uint32 =
        byteStream.CheckBoundsLength 4

        let value = LitteEndian.read32 byteStream.Raw byteStream.position |> uint32
        byteStream.position <- byteStream.position + 4
        value

    member this.ReadSingle () : single =
        let value = this.ReadUInt32 ()
        let mutable s = SingleUnion ()
        s.Value <- value
        s.SingleValue

    member this.Read<'T when 'T : unmanaged> () : 'T =
        let size = sizeof<'T>

        byteStream.CheckBoundsLength size

        let mutable value = Unchecked.defaultof<'T>
        let ptr = &&value |> NativePtr.toNativeInt
        Marshal.Copy (byteStream.Raw, byteStream.position, ptr, size)
        byteStream.position <- byteStream.position + size
        value

    member this.Read<'T when 'T : unmanaged> (value: byref<'T>) =
        let size = sizeof<'T>

        byteStream.CheckBoundsLength size

        let ptr = &&value |> NativePtr.toNativeInt
        Marshal.Copy (byteStream.Raw, byteStream.position, ptr, size)
        byteStream.position <- byteStream.position + size

    member this.ReadInts (buffer: int []) =
        let len = this.ReadInt ()

        if buffer.Length < len then
            failwith "Buffer is too small for reading an array of ints."

        let size = len * 4

        byteStream.CheckBoundsLength size

        Buffer.BlockCopy (byteStream.Raw, byteStream.position, buffer, 0, size)
        byteStream.position <- byteStream.position + size
        len

    member this.IsEndOfStream = byteStream.position = byteStream.length

    member this.Position = byteStream.position


