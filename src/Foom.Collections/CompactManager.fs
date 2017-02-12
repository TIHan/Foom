namespace Foom.Collections

open System.Collections.Generic

[<Struct>]
type CompactId =

    val Index : int

    val Version : uint32

    new (index, version) = { Index = index; Version = version }

    static member Zero = CompactId (0, 0u)

type CompactManager<'T> =
    {
        mutable nextIndex: int

        maxSize: int
        versions: uint32 []
        nextIndexQueue: Queue<int>
        dataIndexLookup: int []

        dataIds: CompactId ResizeArray
        data: 'T ResizeArray
    }

    static member Create (maxSize) =
        {
            nextIndex = 0
            maxSize = maxSize
            versions = Array.init maxSize (fun _ -> 1u)
            nextIndexQueue = Queue ()
            dataIndexLookup = Array.init maxSize (fun _ -> -1)
            dataIds = ResizeArray ()
            data = ResizeArray ()
        }

    member this.Count =
        this.data.Count

    member this.Add datum =
        if this.nextIndex >= this.maxSize then
            System.Diagnostics.Debug.WriteLine ("Unable to add datum. Reached max size, " + string this.maxSize + ".")
            CompactId (0, 0u)
        else

        let id =
            if this.nextIndexQueue.Count > 0 then
                let index = this.nextIndexQueue.Dequeue ()
                let version = this.versions.[index] + 1u
                CompactId (index, version)
            else
                let index = this.nextIndex
                this.nextIndex <- this.nextIndex + 1
                CompactId (index, 1u)

        let index = this.dataIds.Count

        this.dataIds.Add id
        this.data.Add datum

        this.dataIndexLookup.[id.Index] <- index
        this.versions.[id.Index] <- id.Version

        id

    member this.RemoveById (id: CompactId) =
        if this.IsValid id then

            this.nextIndexQueue.Enqueue id.Index

            let index = this.dataIndexLookup.[id.Index]

            let lastIndex = this.data.Count - 1
            let lastId = this.dataIds.[lastIndex]

            this.dataIds.[index] <- this.dataIds.[lastIndex]
            this.data.[index] <- this.data.[lastIndex]

            this.dataIds.RemoveAt (lastIndex)
            this.data.RemoveAt (lastIndex)

            this.dataIndexLookup.[id.Index] <- -1
            this.dataIndexLookup.[lastId.Index] <- index 

        else
            failwithf "Not a valid id, %A." id

    member this.IsValid (id: CompactId) =

        if id.Index < this.dataIndexLookup.Length && id.Version = this.versions.[id.Index] then

            let index = this.dataIndexLookup.[id.Index]

            index <> -1

        else
            false    

    member this.FindById (id: CompactId) =

        if this.IsValid id then

            let index = this.dataIndexLookup.[id.Index]

            if index <> -1 then
                this.data.[index]
            else
                failwithf "Unable to find datum with id, %A." id

        else
            failwithf "Not a valid id, %A." id

    member this.TryFindById (id: CompactId) =

        if this.IsValid id then
            let index = this.dataIndexLookup.[id.Index]

            if index <> -1 then
                Some this.data.[index]
            else
                None
        else
            None

    member this.ForEach f =

        for i = 0 to this.data.Count - 1 do
            
            f this.dataIds.[i] this.data.[i]

    member this.IsFull =
        this.data.Count = this.maxSize

