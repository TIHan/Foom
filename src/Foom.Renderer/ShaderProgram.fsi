namespace Foom.Renderer

open System.Numerics

[<Sealed>]
type Uniform<'T> =

    member Name : string

    member Location : int

    member Value : 'T

    member Set : 'T -> unit

    member internal IsDirty : bool with get, set

[<Sealed>]
type VertexAttribute<'T> =

    member Name : string

    member Location : int

    member Value : 'T

    member Set : 'T -> unit

[<Sealed>]
type InstanceAttribute<'T> =

    member VertexAttribute : VertexAttribute<'T>

    member Set : 'T -> unit

[<Sealed>]
type ShaderProgram =

    static member Create : IGL * programId: int -> ShaderProgram

    member CreateUniform : name: string -> Uniform<'T>

    member CreateVertexAttribute : name: string -> VertexAttribute<'T>

    member CreateInstanceAttribute : name: string -> InstanceAttribute<'T>

    member Id : int

    member Unbind : unit -> unit

    member Run : unit -> unit
