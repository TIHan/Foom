namespace Foom.Wad

[<Struct>]
type Pixel =
    val R : byte
    val G : byte
    val B : byte

    new (r, g, b) = { R = r; G = g; B = b }

    static member Cyan = Pixel (0uy, 255uy, 255uy)
