namespace Foom.Collections

open System.Collections.Generic

[<Struct>]
type CompactId =

    val Index : int ref

    new (index) = { Index = index }

    static member Zero = CompactId (ref -1)

type CompactManager<'T> =
    {
        dataIds: CompactId ResizeArray
        data: 'T ResizeArray
    }

    static member Create (capacity: int) =
        {
            dataIds = ResizeArray (capacity)
            data = ResizeArray (capacity)
        }

    member this.Count = this.data.Count

    member this.Add datum =     
        let index = this.dataIds.Count

        let indexRef = ref index

        let id = CompactId (indexRef)

        this.dataIds.Add id
        this.data.Add datum

        id

    member this.RemoveById (id: CompactId) =
        if this.IsValid id then
            let count = this.dataIds.Count
            let lastIndex = count - 1
            let index = !id.Index

            id.Index := -1

            let lastId = this.dataIds.[lastIndex]
            let last = this.data.[lastIndex]

            this.dataIds.[index]
        else
            failwithf "Not a valid id, %A." id

    member this.IsValid (id: CompactId) =

        if !id.Index < this.dataIds.Count && !id.Index <> -1 then
            true
        else
            false    

    member this.FindById (id: CompactId) =

        if this.IsValid id then
            this.data.[!id.Index]
        else
            failwithf "Not a valid id, %A." id.Index

    member this.TryFindById (id: CompactId) =

        if this.IsValid id then
            Some this.data.[!id.Index]
        else
            None

    member this.ForEach f =

        for i = 0 to this.data.Count - 1 do
            
            f this.dataIds.[i] this.data.[i]