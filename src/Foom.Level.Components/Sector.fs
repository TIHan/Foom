namespace Foom.Level

type Sector =
    {
        lightLevel: int
        floorHeight: int
        ceilingHeight: int

        upperMiddleHeight: int option

        floorTextureName: string
        ceilingTextureName: string
    }
