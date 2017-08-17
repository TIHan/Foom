open System
open System.Collections.Generic

open Foom.Network

[<Struct>]
type TestStruct =
    { 
        x: int
        y: int
    }

type TestMessage =
    {
        mutable a : int
        mutable b : int
    }

    static member Serialize (msg: TestMessage) (byteWriter: ByteWriter) =
        byteWriter.WriteInt (msg.a)
        byteWriter.WriteInt (msg.b)

    static member Deserialize (msg: TestMessage) (byteReader: ByteReader) =
        msg.a <- byteReader.ReadInt ()
        msg.b <- byteReader.ReadInt ()

    static member Ctor _ =
        {
            a = 0
            b = 0
        }

type TestMessage2 =
    {
        mutable c : int
        mutable d : int
    }

    static member Serialize (msg: TestMessage2) (byteWriter: ByteWriter) =
        byteWriter.WriteInt (msg.c)
        byteWriter.WriteInt (msg.d)

    static member Deserialize (msg: TestMessage2) (byteReader: ByteReader) =
        msg.c <- byteReader.ReadInt ()
        msg.d <- byteReader.ReadInt ()

    static member Ctor _ =
        {
            c = 0
            d = 0
        }

module TestMessage3 =

    let buffer = Array.zeroCreate<int> 65536

type TestMessage3 =
    {
        mutable arr : int []
        mutable len : int
    }



    static member Serialize (msg: TestMessage3) (byteWriter: ByteWriter) =
        byteWriter.WriteInts (msg.arr, 0, msg.len)

    static member Deserialize (msg: TestMessage3) (byteReader: ByteReader) =
        msg.len <- byteReader.ReadInts (msg.arr)

    static member Ctor _ =
        {
            arr = TestMessage3.buffer
            len = 0
        }

[<Struct>]
type EntityState =
    {
        mutable id : uint16
        mutable x : int
        mutable y : int
        mutable z : int
        mutable angle : int16
        mutable anim : byte
        mutable materialIndex : int
    }

type Snapshot =
    {
        mutable stateLength : uint16
        states : EntityState []
    }

    static member Create () =
        {
            stateLength = 0us
            states = Array.zeroCreate 65536
        }

type SnapshotPool (poolAmount) =

    let pool : Stack<Snapshot> = Stack (Array.init poolAmount (fun _ -> Snapshot.Create ()))

    member this.Count = pool.Count

    member this.MaxCount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (state : Snapshot) =
        Array.Clear (state.states, 0, int state.stateLength)
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push state

let pool = SnapshotPool 60

let clientStates = Array.zeroCreate<EntityState> 65536
//let lastAckStates = Array.init 65536 (fun _ -> { id = 0us; x = 0; y = 0; z = 0; angle = 0s; frame = 0us; materialIndex = 0 })

let mutable lastAckSnapshot = pool.Get ()

type Snapshot with

    static member Serialize (msg: Snapshot) (w: ByteWriter) =
        let prev = lastAckSnapshot

        w.WriteUInt16 msg.stateLength
        for i = 0 to int msg.stateLength - 1 do
            let state = &msg.states.[i]
            let prev = &prev.states.[i]

            w.WriteUInt16 state.id
            w.WriteDeltaInt (prev.x, state.x)
            w.WriteDeltaInt (prev.y, state.y)
            w.WriteDeltaInt (prev.z, state.z)
            w.WriteDeltaInt16 (prev.angle, state.angle)
            w.WriteDeltaByte (prev.anim, state.anim)
            w.WriteDeltaInt (prev.materialIndex, state.materialIndex)


        pool.Recycle prev
        lastAckSnapshot <- msg

    static member Deserialize (msg: Snapshot) (r: ByteReader) =
        pool.Recycle msg

        let length = int <| r.ReadUInt16 ()

        for i = 0 to int length - 1 do
            let id = int <| r.ReadUInt16 ()

            let current = &clientStates.[id]

            current.id <- uint16 id
            current.x <- r.ReadDeltaInt (current.x)
            current.y <- r.ReadDeltaInt (current.y)
            current.z <- r.ReadDeltaInt (current.z)
            current.angle <- r.ReadDeltaInt16 (current.angle)
            current.anim <- r.ReadDeltaByte (current.anim)
            current.materialIndex <- r.ReadDeltaInt (current.materialIndex)

    static member Ctor _ = pool.Get ()

Network.RegisterType (TestMessage.Serialize, TestMessage.Deserialize, TestMessage.Ctor)
Network.RegisterType (TestMessage2.Serialize, TestMessage2.Deserialize, TestMessage2.Ctor)
Network.RegisterType (TestMessage3.Serialize, TestMessage3.Deserialize, TestMessage3.Ctor)
Network.RegisterType (Snapshot.Serialize, Snapshot.Deserialize, Snapshot.Ctor)

let perf title f =
    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    stopwatch.Stop ()
    printfn "%s: %A" title stopwatch.Elapsed.TotalMilliseconds

[<EntryPoint>]
let main argv = 
    let testSize = 4096 * 4

    let udpClients = Array.init 8 (fun _ -> 
        let client = new Client (new UdpClient ())

        let mutable value = 0
        client.Subscribe<TestMessage> (fun msg ->
            value <- msg.b
        )



        let values = Array.zeroCreate testSize

        client.Subscribe<TestMessage3> (fun msg ->
            for i = 0 to msg.len - 1 do
                values.[i] <- msg.arr.[i]
        )

        client.Subscribe<Snapshot> (fun msg ->
            ()
        )

        client
    )

    use udpServer = new UdpServer (29015) :> IUdpServer

    let server = new Server (udpServer)

    udpClients
    |> Array.iter (fun client -> client.Connect ("127.0.0.1", 29015))

    udpClients
    |> Array.iter (fun client -> client.Update TimeSpan.Zero)

    Threading.Thread.Sleep 100

    server.Update TimeSpan.Zero

    Threading.Thread.Sleep 100

    udpClients
    |> Array.iter (fun client -> client.Update TimeSpan.Zero)


    let data = { arr = Array.init testSize (fun i -> i + 1); len = testSize }

    
    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
    for i = 1 to 50 do
        perf "Server Send" (fun () ->
            server.PublishReliableOrdered data
            server.Update stopwatch.Elapsed
        )
        printfn "Server Sent: %A bytes" server.BytesSentSinceLastUpdate
        Threading.Thread.Sleep 10

        perf "Client Receive" (fun () ->
            udpClients
            |> Array.iter (fun client -> client.Update stopwatch.Elapsed)
        )

        //printfn "%A = %A" client.PacketPoolMaxCount client.PacketPoolCount

    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
    let rng = System.Random ()
    for i = 1 to 50 do

        perf "Server Send Snapshot" (fun () ->
            let snapshot = pool.Get ()

            snapshot.stateLength <- 500us

            for j = 0 to 500 - 1 do
                let state = &snapshot.states.[j]
                state.id <- uint16 j
                state.x <- (1 + rng.Next())
                state.y <- (2 + rng.Next())
                state.z <- (3 + j)
                state.angle <- int16 (4 + j)
                state.anim <- byte (5 + rng.Next ())
                state.materialIndex <- int (6 + j)
            server.PublishReliableOrdered snapshot
            server.Update stopwatch.Elapsed
        )
        printfn "Server Sent: %A kb/sec" (float32 server.BytesSentSinceLastUpdate / 1024.f * 30.f)
        Threading.Thread.Sleep 10

        perf "Client Receive Snapshot" (fun () ->
            udpClients
            |> Array.iter (fun client -> client.Update stopwatch.Elapsed)
        )

       // printfn "%A = %A" client.PacketPoolMaxCount client.PacketPoolCount
      

    Console.ReadLine () |> ignore
    0

