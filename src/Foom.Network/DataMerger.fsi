namespace Foom.Network

open System

type IFilter<'Input, 'Output> =

    abstract Enqueue : 'Input -> unit

    abstract Flush : TimeSpan * ('Output -> unit) -> unit

    abstract Reset : unit -> unit

module Filter =

    val combine : IFilter<'Output, 'NewOutput> -> IFilter<'Input, 'Output> -> IFilter<'Input, 'NewOutput>

    val outputMap : (TimeSpan -> 'Output -> 'NewOutput) -> IFilter<'Input, 'Output> -> IFilter<'Input, 'NewOutput>

    val inputMap : ('NewInput -> 'Input) -> IFilter<'Input, 'Output> -> IFilter<'NewInput, 'Output>

    val reset : (unit -> unit) -> IFilter<'Input, 'Output> -> IFilter<'Input, 'Output>

    val flush : (TimeSpan -> ('Output -> unit) -> unit) -> IFilter<'Input, 'Output> -> IFilter<'Input, 'Output>

[<Sealed>]
type internal DataMerger =

    interface IFilter<struct (byte [] * int * int), Packet>

    static member Create : PacketPool -> DataMerger