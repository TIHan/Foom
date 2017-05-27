module Foom.Network.Network

open System
open System.Collections.Generic
open System.Reflection

type Pickler =
    {
        type': Type
        serialize: obj -> ByteWriter -> unit
        deserialize: obj -> ByteReader -> unit
        ctor: ByteReader -> obj
    }

let internal lookup = Dictionary<Type, int> ()
let internal typeArray = ResizeArray<Pickler> ()

let RegisterType<'T> (serialize: 'T -> ByteWriter -> unit, deserialize: 'T -> ByteReader -> unit, ctor: ByteReader -> 'T) =
    let t = typeof<'T>

    let pickler =
        {
            type' = t
            serialize = fun o x -> serialize (o :?> 'T) x
            deserialize = fun o x -> deserialize (o :?> 'T) x
            ctor = (fun reader -> (ctor reader) :> obj)
        }

    lookup.Add (t, typeArray.Count)
    typeArray.Add pickler

let FindTypeById id =
    if id >= typeArray.Count then
        failwithf "oh shit %A" id
    typeArray.[id]
