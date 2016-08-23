namespace Foom.Geometry

type Polygon2DTree = 
    {
        Polygon: Polygon2D
        Children: Polygon2DTree list
    }
