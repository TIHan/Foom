namespace Foom.Network

[<Sealed>]
type Sequencer () =

    let mutable seqN = 0us

    member this.Assign (packet : Packet) =
        packet.SequenceId <- seqN
        seqN <- seqN + 1us