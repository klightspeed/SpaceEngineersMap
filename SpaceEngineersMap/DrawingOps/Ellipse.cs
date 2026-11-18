using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class Ellipse(Pen pen, float cx, float cy, float rx, float ry) : IDrawingOp
    {
        public float CX { get; set; } = cx;
        public float CY { get; set; } = cy;
        public float RX { get; set; } = rx;
        public float RY { get; set; } = ry;
        public Pen Pen { get; set; } = pen;
        
        public void Draw(IImageProcessingContext context) => context.Draw(Pen, new EllipsePolygon(CX, CY, RX, RY));
        
        public IPath GetBoundsPath()
            => new EllipsePolygon(CX, CY, RX, RY).GenerateOutline(Pen.StrokeWidth);
        
        public IPathCollection ToPaths() => new PathCollection(new EllipsePolygon(CX, CY, RX, RY));
    }
}
