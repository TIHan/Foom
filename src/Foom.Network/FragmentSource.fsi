namespace Foom.Network

[<Sealed>]
type FragmentSource =

    interface ISource

    static member Create : PacketPool -> FragmentSource
