namespace Foom.Client

type ClientState = 
    {
        Window: nativeint
        AlwaysUpdate: unit -> unit
        Update: (float32 * float32 -> unit)
        RenderUpdate: (float32 * float32 -> unit)
        ClientWorld: ClientWorld
    }
