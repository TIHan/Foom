namespace Foom.Wad

open System
open System.IO
open System.Numerics
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Diagnostics

open Foom.Wad.Pickler

open Foom.Wad.Level
open Foom.Wad.Level.Structures

open Foom.Pickler.Core
open Foom.Pickler.Unpickle
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type Texture =
    {
        Data: Pixel [,]
        Name: string
    }

type Wad = 
    {
        mutable TextureInfoLookup: Dictionary<string, TextureInfo> option
        mutable FlatHeaderLookup: Dictionary<string, LumpHeader> option
        stream: Stream
        wadData: WadData
        mutable defaultPaletteData: PaletteData option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wad =

    open Foom.Wad.Pickler.UnpickleWad

    let runUnpickle u stream =
        u_run u (LiteReadStream.ofStream stream) 

    let runUnpickles us stream =
        let stream = LiteReadStream.ofStream stream
        us |> Array.map (fun u -> u_run u stream)

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
        wad.wadData.LumpHeaders |> Array.tryFind (fun x -> x.Name.ToUpper () = "PLAYPAL")
        |> Option.iter (fun lumpPaletteHeader ->
            let lumpPalettes = loadLump u_lumpPalettes lumpPaletteHeader wad.stream
            wad.defaultPaletteData <- Some lumpPalettes.[0]
        )

    let loadFlatHeaders wad =
        let stream = wad.stream
        let lumpHeaders = wad.wadData.LumpHeaders

        let lumpFlatsHeaderStartIndex = lumpHeaders |> Array.tryFindIndex (fun x -> x.Name.ToUpper () = "F_START" || x.Name.ToUpper () = "FF_START")
        let lumpFlatsHeaderEndIndex = lumpHeaders |> Array.tryFindIndex (fun x -> x.Name.ToUpper () = "F_END" || x.Name.ToUpper () = "FF_END")

        match lumpFlatsHeaderStartIndex, lumpFlatsHeaderEndIndex with
        | None, None -> ()

        | Some _, None ->
            Debug.WriteLine """Warning: Unable to load flat textures because "F_END" lump was not found."""

        | None, Some _ ->
            Debug.WriteLine """Warning: Unable to load flat textures because "F_START" lump was not found."""

        | Some lumpFlatsHeaderStartIndex, Some lumpFlatsHeaderEndIndex ->
            let lumpFlatHeaders =
                lumpHeaders.[(lumpFlatsHeaderStartIndex + 1)..(lumpFlatsHeaderEndIndex - 1)]
                |> filterLumpHeaders

            let dict = Dictionary ()

            lumpFlatHeaders
            |> Array.iter (fun x ->
                dict.[x.Name.ToUpper ()] <- x
            )

            wad.FlatHeaderLookup <- Some dict

    let tryFindLump (str: string) wad =
        wad.wadData.LumpHeaders
        |> Array.tryFind (fun x -> x.Name.ToUpper() = str.ToUpper())

    let loadTextureInfos (wad: Wad) =
        let textureInfoLookup = Dictionary ()

        let readTextureLump (textureLump: LumpHeader) =
            let textureHeader = runUnpickle (uTextureHeader textureLump) wad.stream
            let textureInfos = runUnpickle (uTextureInfos textureLump textureHeader) wad.stream
            textureInfos
            |> Array.iter (fun x -> 
                textureInfoLookup.[x.Name.ToUpper ()] <- x
            )

        wad.wadData.LumpHeaders
        |> Array.filter (fun x ->
            let name = x.Name.ToUpper ()
            name = "TEXTURE1" || name = "TEXTURE2"
        )
        |> Array.iter (readTextureLump)

        wad.TextureInfoLookup <- Some textureInfoLookup

    let tryFindFlatTexture (name: string) wad =
        let name = name.ToUpper ()
        if wad.FlatHeaderLookup.IsNone then
            loadFlatHeaders wad

        match wad.defaultPaletteData with
        | None ->
            Debug.WriteLine "Warning: Unable to load flat textures because there is no default palette."
            None
        | Some palette ->

            match wad.FlatHeaderLookup.Value.TryGetValue (name) with
            | false, _ -> None
            | true, h ->

                // Assert Flat Headers are valid
                if h.Offset.Equals 0 then failwithf "Invalid flat header, %A. Offset is 0." h
                if not (h.Size.Equals 4096) then failwithf "Invalid flat header, %A. Size is not 4096." h

                let bytes = loadLump u_lumpRaw h wad.stream

                {
                    Name = h.Name
                    Data =
                        let pixels = Array2D.zeroCreate<Pixel> 64 64
                        for i = 0 to 64 - 1 do
                            for j = 0 to 64 - 1 do
                                pixels.[i, j] <- palette.Pixels.[int bytes.[i + j * 64]]
                        pixels
                } |> Some

    let tryFindPatch patchName wad =
        match tryFindLump patchName wad with
        | Some header ->
            (
                runUnpickle (uDoomPicture header wad.defaultPaletteData.Value) wad.stream 
            ) |> Some
        | _ -> None

    let tryFindTexture (name: string) wad =
        let name = name.ToUpper ()
        if wad.TextureInfoLookup.IsNone then
            loadTextureInfos wad

        match wad.TextureInfoLookup.Value.TryGetValue (name) with
        | false, _ -> None
        | true, info ->

            let tex = Array2D.init info.Width info.Height (fun _ _ -> Pixel.Cyan)

            let pnamesLump =
                wad.wadData.LumpHeaders
                |> Array.find (fun x -> x.Name.ToUpper() = "PNAMES")

            let patchNames = runUnpickle (uPatchNames pnamesLump) wad.stream

            let mutable previousOffsetX = 0
            let mutable previousOffsetY = 0
            info.Patches
            |> Array.iter (fun patch ->
                match tryFindPatch patchNames.[patch.PatchNumber] wad with
                | Some pic ->

                    pic.Data
                    |> Array2D.iteri (fun i j pixel ->
                        let i = i + patch.OriginX
                        let j = j + patch.OriginY

                        if i < info.Width && j < info.Height && i >= 0 && j >= 0 && pixel <> Pixel.Cyan then
                            tex.[i, j] <- pixel
                               
                    )

                | _ -> ()
            )

            {
                Data = tex
                Name = info.Name
            } |> Some

    let create stream =
        let wadData = runUnpickle u_wad stream

        let wad =
            { 
                TextureInfoLookup = None
                FlatHeaderLookup = None
                stream = stream
                wadData = wadData
                defaultPaletteData = None
            }
        wad |> loadPalettes
        wad

    let extend stream (wad: Wad) =
        let wadData = runUnpickle u_wad stream

        let extendedWad =
            {
                wad with 
                    wadData = wadData
                    stream = stream
            }
        extendedWad

    let findLevel (levelName: string) wad =
        let stream = wad.stream
        let name = levelName.ToLower ()

        match
            wad.wadData.LumpHeaders
            |> Array.tryFindIndex (fun x -> x.Name.ToLower () = name.ToLower ()) with
        | None -> failwithf "Unable to find level, %s." name
        | Some lumpLevelStartIndex ->

        // printfn "Found Level: %s" name
        let lumpHeaders = wad.wadData.LumpHeaders.[lumpLevelStartIndex..]

        // Note: This seems to work, but may be possible to get invalid data for the level.
        let lumpThingsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "THINGS".ToLower ())
        let lumpLinedefsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "LINEDEFS".ToLower ())
        let lumpSidedefsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "SIDEDEFS".ToLower ())
        let lumpVerticesHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "VERTEXES".ToLower ())
        let lumpSectorsHeader = lumpHeaders |> Array.find (fun x -> x.Name.ToLower () = "SECTORS".ToLower ())

        let lumpThings = loadLump (u_lumpThings ThingFormat.Doom) lumpThingsHeader stream
        let lumpVertices = loadLump u_lumpVertices lumpVerticesHeader stream
        let lumpSidedefs = loadLump u_lumpSidedefs lumpSidedefsHeader stream
        let lumpLinedefs = loadLump (u_lumpLinedefs lumpVertices.Vertices lumpSidedefs.Sidedefs) lumpLinedefsHeader stream
        let lumpSectors = loadLump (u_lumpSectors lumpLinedefs.Linedefs) lumpSectorsHeader stream

        Level.Create (lumpSectors.Sectors, lumpThings.Things)

    let iterFlatTextureName f wad =
        if wad.FlatHeaderLookup.IsNone then
            loadFlatHeaders wad

        match wad.FlatHeaderLookup with
        | Some lookup ->
            lookup.Keys
            |> Seq.iter f
        | _ -> ()

    let iterTextureName f wad =
        if wad.TextureInfoLookup.IsNone then
            loadTextureInfos wad

        match wad.TextureInfoLookup with
        | Some lookup ->
            lookup.Keys
            |> Seq.iter f
        | _ -> ()