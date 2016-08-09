namespace Foom.Ecs

open System.Runtime.CompilerServices

/// Contains a description of what components an entity will have when spawned.
[<Sealed>]
type EntityPrototype

/// Contains functions on creating an extending an Entity Prototype.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    /// An empty Entity Prototype.
    val empty : EntityPrototype

    /// Combines two Entity Prototypes into one.
    val combine : EntityPrototype -> EntityPrototype -> EntityPrototype

    /// Adds a component description to an Entity Prototype.
    /// Lambda gets evaluated when spawning an entity was successful.
    val addComponent<'T when 'T :> IEntityComponent and 'T : not struct> : (unit -> 'T) -> EntityPrototype -> EntityPrototype 

/// Entity Manager extension functions.
[<AutoOpen>]
module EntityManagerExtensions =

    type EntityManager with

        /// Defers to spawn a given Entity Prototype.
        member Spawn : EntityPrototype -> Entity 
