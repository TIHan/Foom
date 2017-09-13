namespace Foom.Collections

open System.Collections.Generic

[<Struct>]
type CompactId =

    val Index : int ref

    new (index) = { Index = index }

    static member Zero = CompactId (ref -1)

[<Sealed>]
type CompactManager<'T> (capacity : int) =

    let dataIds = ResizeArray (capacity)
    let data = ResizeArray<'T> (capacity)

    member this.Count = data.Count

    member this.Add datum =     
        let index = dataIds.Count

        let indexRef = ref index

        let id = CompactId (indexRef)

        dataIds.Add id
        data.Add datum

        id

    member this.RemoveById (id: CompactId) =
        if this.IsValid id then
            let count = dataIds.Count
            let lastIndex = count - 1
            let index = !id.Index

            id.Index := -1

            let lastId = dataIds.[lastIndex]
            let last = data.[lastIndex]

            dataIds.[index]
        else
            failwithf "Not a valid id, %A." id

    member this.IsValid (id: CompactId) =

        if !id.Index < dataIds.Count && !id.Index <> -1 then
            true
        else
            false    

    member this.FindById (id: CompactId) =

        if this.IsValid id then
            data.[!id.Index]
        else
            failwithf "Not a valid id, %A." id.Index

    member this.TryFindById (id: CompactId) =

        if this.IsValid id then
            Some data.[!id.Index]
        else
            None

    member this.ForEach f =

        for i = 0 to data.Count - 1 do
            
            f dataIds.[i] data.[i]