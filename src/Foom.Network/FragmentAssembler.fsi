namespace Foom.Network

[<Sealed>]
type FragmentAssembler =

    member Mark : Packet * (Packet -> unit) -> unit

    static member Create : unit -> FragmentAssembler