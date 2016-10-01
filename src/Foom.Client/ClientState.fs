namespace Foom.Client

type ClientState = 
    {
        Window: nativeint
        Update: (float32 * float32 -> unit)
        RenderUpdate: (float32 * float32 -> unit)
        ClientWorld: ClientWorld
    }
