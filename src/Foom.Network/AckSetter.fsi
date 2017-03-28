namespace Foom.Network

[<Sealed>]
type AckSetter =

    new : AckManager -> AckSetter

    interface IPass
