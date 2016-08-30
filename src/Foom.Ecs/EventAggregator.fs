namespace Foom.Ecs

open System
open System.Collections.Concurrent

type IEvent = interface end

[<ReferenceEquality>]
type EventAggregator  =
    {
        Lookup: ConcurrentDictionary<Type, obj>
    }

    static member Create () =
        {
            Lookup = ConcurrentDictionary<Type, obj> ()
        }

    member this.Publish (event: 'T when 'T :> IEvent and 'T : not struct) =
        let mutable value = Unchecked.defaultof<obj>
        if this.Lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member this.GetEvent<'T when 'T :> IEvent> () =
       this.Lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>
