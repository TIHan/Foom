namespace Foom.Network

open System.Collections.Generic

type FragmentBuffer =
    {
        packets : Packet []
        mutable count : int
    }

    static member Create () =
        {
            packets = Array.zeroCreate 256
            count = 0
        }
    
    member this.Reset () =
        System.Array.Clear (this.packets, 0, this.packets.Length)
        this.count <- 0

type FragmentBufferPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> FragmentBuffer.Create ()))

    member this.Count = pool.Count

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : FragmentBuffer) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet


type FragmentAssembler =
    {
        fragmentBufferPool : FragmentBufferPool
        fragmentBuffers : FragmentBuffer []
    }

    member this.Mark (packet : Packet, f) =

        let mutable seqId = 0us
        if tryGetSequenceEntryId packet &seqId then

            if obj.ReferenceEquals (this.fragmentBuffers.[int seqId], null) |> not then

                let fragmentBuffer = this.fragmentBuffers.[int seqId]
                fragmentBuffer.packets.[int packet.FragmentId] <- packet
                fragmentBuffer.count <- fragmentBuffer.count + 1

                if int packet.FragmentCount = fragmentBuffer.count then
                    for i = 1 to fragmentBuffer.count do
                        f (fragmentBuffer.packets.[i])
                    this.fragmentBufferPool.Recycle fragmentBuffer
                    this.fragmentBuffers.[int seqId] <- Unchecked.defaultof<FragmentBuffer>

            else
                let fragmentBuffer = this.fragmentBufferPool.Get ()
                this.fragmentBuffers.[int seqId] <- fragmentBuffer

                fragmentBuffer.packets.[int packet.FragmentId] <- packet
                fragmentBuffer.count <- fragmentBuffer.count + 1

        else
            failwith "bad packet"

    static member Create () =
        {
            fragmentBufferPool = FragmentBufferPool (256)
            fragmentBuffers = Array.init 65536 (fun _ -> Unchecked.defaultof<FragmentBuffer>)
        }