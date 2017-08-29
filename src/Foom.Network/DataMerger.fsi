namespace Foom.Network

open System

type IFilter<'Input, 'Output> =

    abstract Enqueue : 'Input -> unit

    abstract Flush : TimeSpan * ('Output -> unit) -> unit

module Filter =

    val combine : IFilter<'Output, 'NewOutput> -> IFilter<'Input, 'Output> -> IFilter<'Input, 'NewOutput>

    val map : ('Output -> 'NewOutput) -> IFilter<'Input, 'Output> -> IFilter<'Input, 'NewOutput>

[<Sealed>]
type internal DataMerger =

    interface IFilter<struct (byte [] * int * int), Packet>

    static member Create : PacketPool -> DataMerger