namespace Foom.Wad

[<Struct>]
type Pixel =
    val R : byte
    val G : byte
    val B : byte

    new : byte * byte * byte -> Pixel

    static member Cyan : Pixel
