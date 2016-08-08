namespace Foom.Wad

open System
open System.IO
open System.Numerics
open System.Runtime.InteropServices

open Foom.Wad.Pickler

open Foom.Wad.Level
open Foom.Wad.Level.Structures

open Foom.Pickler.Core
open Foom.Pickler.Unpickle
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type FlatTexture =
    {
        Pixels: Pixel []
        Name: string
    }

type Wad = 
    {
        stream: Stream
        wadData: WadData
        defaultPaletteData: PaletteData option
        flats: FlatTexture []
    }

// TODO: Holy crap using async here is just not useful, stop it.
[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wad =

    open Foom.Wad.Pickler.UnpickleWad       

    let inline (>=>) (f1: 'a -> Async<'b>) (f2: 'b -> Async<'c>) : 'a -> Async<'c> =
        fun a -> async {
            let! a = f1 a
            return! f2 a
        }

    let runUnpickle u stream = async { 
        return u_run u (LiteReadStream.ofStream stream) 
    }

    let runUnpickles us stream = async {
        let stream = LiteReadStream.ofStream stream
        return us |> Array.map (fun u -> u_run u stream)
    }

    let loadLump u (header: LumpHeader) fileName = 
        runUnpickle (u header.Size (int64 header.Offset)) fileName

    let loadLumpMarker u (markerStart: LumpHeader) (markerEnd: LumpHeader) fileName =
        runUnpickle (u (markerEnd.Offset - markerStart.Offset) (int64 markerStart.Offset)) fileName

    let loadLumps u (headers: LumpHeader []) fileName =
        let us =
            headers
            |> Array.map (fun h -> u h.Size (int64 h.Offset))
        runUnpickles us fileName

    let filterLumpHeaders (headers: LumpHeader []) =
        headers
        |> Array.filter (fun x ->
            match x.Name.ToUpper () with
            | "F1_START" -> false
            | "F2_START" -> false
            | "F3_START" -> false
            | "F1_END" -> false
            | "F2_END" -> false
            | "F3_END" -> false
            | _ -> true)

    let loadPalettes wad =
        match wad.wadData.LumpHeaders |> Array.tryFind (fun x -> x.Name.ToUpper () = "PLAYPAL") with
        | None -> async { return wad }
        | Some lumpPaletteHeader -> async {
            let! lumpPalettes = loadLump u_lumpPalettes lumpPaletteHeader wad.stream
            return { wad with defaultPaletteData = Some lumpPalettes.[0] }
        }
        
    let loadFlats wad =
        match wad.defaultPaletteData with
        | None ->
            // printf "Warning: Unable to load flat textures because there is no default palette."
            async { return wad }
        | Some palette ->
            let stream = wad.stream
            let lumpHeaders = wad.wadData.LumpHeaders

            let lumpFlatsHeaderStartIndex = lumpHeaders |> Array.tryFindIndex (fun x -> x.Name.ToUpper () = "F_START" || x.Name.ToUpper () = "FF_START")
            let lumpFlatsHeaderEndIndex = lumpHeaders |> Array.tryFindIndex (fun x -> x.Name.ToUpper () = "F_END" || x.Name.ToUpper () = "FF_END")

            match lumpFlatsHeaderStartIndex, lumpFlatsHeaderEndIndex with
            | None, None -> 
                async { return wad }

            | Some _, None ->
                // printfn """Warning: Unable to load flat textures because "F_END" lump was not found."""
                async { return wad }

            | None, Some _ ->
                // printfn """Warning: Unable to load flat textures because "F_START" lump was not found."""
                async { return wad }

            | Some lumpFlatsHeaderStartIndex, Some lumpFlatsHeaderEndIndex ->
                let lumpFlatHeaders =
                    lumpHeaders.[(lumpFlatsHeaderStartIndex + 1)..(lumpFlatsHeaderEndIndex - 1)]
                    |> filterLumpHeaders

                // Assert Flat Headers are valid
                lumpFlatHeaders
                |> Array.iter (fun h ->
                    if h.Offset.Equals 0 then failwithf "Invalid flat header, %A. Offset is 0." h
                    if not (h.Size.Equals 4096) then failwithf "Invalid flat header, %A. Size is not 4096." h)

                async {
                    let! lumpFlats = loadLumps u_lumpRaw lumpFlatHeaders stream

                    let flats =
                        lumpFlats
                        |> Array.map (fun x ->
                            x |> Array.map (fun y -> palette.Pixels.[int y])
                        )
                        |> Array.mapi (fun i pixels ->
                            { Pixels = pixels; Name = lumpFlatHeaders.[i].Name }
                        )

                    return { wad with flats = flats }
                }

    let loadPatches wad =
        let findLump (str: string) =
            wad.wadData.LumpHeaders
            |> Array.find (fun x -> x.Name.ToUpper() = str.ToUpper())

        let texture1Lump =
            wad.wadData.LumpHeaders
            |> Array.find (fun x -> x.Name.ToUpper() = "TEXTURE1")

        let texture2Lump =
            wad.wadData.LumpHeaders
            |> Array.find (fun x -> x.Name.ToUpper() = "TEXTURE2")

        let pnamesLump =
            wad.wadData.LumpHeaders
            |> Array.find (fun x -> x.Name.ToUpper() = "PNAMES")

        let textureHeader = runUnpickle (uTextureHeader texture1Lump) wad.stream |> Async.RunSynchronously

        let textureInfos = runUnpickle (uTextureInfos texture1Lump textureHeader) wad.stream |> Async.RunSynchronously

        let patchNames = runUnpickle (uPatchNames pnamesLump) wad.stream |> Async.RunSynchronously

        let textures =
            patchNames
            |> Array.map (fun x ->
                runUnpickle (uTexture (findLump x)) wad.stream |> Async.RunSynchronously
            )
        ()

    let create stream = async {
        let! wadData = runUnpickle u_wad stream

        return!
            { stream = stream; wadData = wadData; defaultPaletteData = None; flats = [||] }
            |> (loadPalettes >=> loadFlats)
    }

    let createFromWad (wad: Wad) stream =
        async {
            let! wadData = runUnpickle u_wad stream

            return!
                { stream = stream; wadData = wadData; defaultPaletteData = wad.defaultPaletteData; flats = [||] }
                |> (loadFlats)
        }

    let flats (wad: Wad) = wad.flats

    let findLevel (levelName: string) wad =
        let stream = wad.stream
        let name = levelName.ToLower ()

        match
            wad.wadData.LumpHeaders
            |> Array.tryFindIndex (fun x -> x.Name.ToLower () = name.ToLower ()) with
        | None -> async { return failwithf "Unable to find level, %s." name }
        | Some lumpLevelStartIndex ->

        // printfn "Found Level: %s" name
        let lumpHeaders = wad.wadData.LumpHeaders.[lumpLevelStartIndex..]

        // Note: This seems to work, but may be possible to get invalid data for the level.
        let lumpLinedefsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "LINEDEFS".ToLower ())
        let lumpSidedefsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "SIDEDEFS".ToLower ())
        let lumpVerticesHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "VERTEXES".ToLower ())
        let lumpSectorsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "SECTORS".ToLower ())

        async {
            let! lumpVertices = loadLump u_lumpVertices lumpVerticesHeader stream
            let! lumpSidedefs = loadLump u_lumpSidedefs lumpSidedefsHeader stream
            let! lumpLinedefs = loadLump (u_lumpLinedefs lumpVertices.Vertices lumpSidedefs.Sidedefs) lumpLinedefsHeader stream
            let! lumpSectors = loadLump (u_lumpSectors lumpLinedefs.Linedefs) lumpSectorsHeader stream

            let sectors : Sector [] =
                lumpSectors.Sectors
                |> Array.mapi (fun i sector ->
                    let lines =
                        sector.Linedefs
                        |> Array.map (
                            function 
                            | LinedefData.Doom (x, y, f, b, data) -> 
                                { Start = x
                                  End = y
                                  FrontSidedef = match f with | Some f when f.SectorNumber = i -> Some (Sidedef ()) | _ -> None
                                  BackSidedef = match b with | Some b when b.SectorNumber = i -> Some (Sidedef ()) | _ -> None }
                                |> Some)
                        |> Array.filter (fun x -> x.IsSome)
                        |> Array.map (fun x -> x.Value)
                    { 
                        Linedefs = lines; 
                        FloorTextureName = sector.FloorTextureName
                        FloorHeight = sector.FloorHeight
                        LightLevel = sector.LightLevel
                    }
                )

            return { Sectors = sectors }
        }