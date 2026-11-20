using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;

namespace SpaceEngineersMap.DrawingOps
{
    public class Rectangle(Pen pen, float x, float y, float width, float height) : IDrawingOp
    {
        public float X { get; set; } = x;
        public float Y { get; set; } = y;
        public float Width { get; set; } = width;
        public float Height { get; set; } = height;
        public Pen Pen { get; set; } = pen;

        public void Draw(DrawingCanvas canvas) => canvas.Draw(Pen, new RectanglePolygon(X, Y, Width, Height));
        
        public IPath GetBoundsPath()
            => new RectanglePolygon(X, Y, Width, Height).GenerateOutline(Pen.StrokeWidth);

        public IPathCollection ToPaths() => new PathCollection(new RectanglePolygon(X, Y, Width, Height));
    }
}
