namespace Foom.Ecs

/// A marker for event data.
type IEvent = interface end

/// Responsible for publishing events.
/// Used for decoupling and communication between systems.
[<Sealed>]
type EventAggregator =

    static member internal Create : unit -> EventAggregator

    /// Publishes an event to underlying subscribers.
    member Publish<'T when 'T :> IEvent and 'T : not struct> : 'T -> unit

    member internal GetEvent<'T when 'T :> IEvent> : unit -> Event<'T>
