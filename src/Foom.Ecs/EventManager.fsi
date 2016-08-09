namespace Foom.Ecs

/// A marker for event data.
type IEntitySystemEvent = interface end

/// Responsible for publishing events.
/// Used for decoupling and communication between systems.
[<Sealed>]
type EventManager =

    static member internal Create : unit -> EventManager

    /// Publishes an event to underlying subscribers.
    member Publish<'T when 'T :> IEntitySystemEvent and 'T : not struct> : 'T -> unit

    member internal GetEvent<'T when 'T :> IEntitySystemEvent> : unit -> Event<'T>
