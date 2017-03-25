module Foom.Network.Network

open System
open System.Collections.Generic
open System.Reflection

let private typeArray = ResizeArray<Type> ()
let internal lookup = Dictionary<Type, int * (obj -> ByteWriter -> unit) * (obj -> ByteReader -> unit)> ()

let RegisterType<'T when 'T : (new : unit -> 'T)> (serialize: 'T -> ByteWriter -> unit, deserialize: 'T -> ByteReader -> unit) =
    let t = typeof<'T>

    lookup.Add (t, (typeArray.Count, (fun o x -> serialize (o :?> 'T) x), (fun o x -> deserialize (o :?> 'T) x)))
    typeArray.Add t

let FindTypeById id =
    typeArray.[id]
