using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class FilledEllipse(Brush brush, float cx, float cy, float rx, float ry) : IDrawingOp
    {
        public float CX { get; set; } = cx;
        public float CY { get; set; } = cy;
        public float RX { get; set; } = rx;
        public float RY { get; set; } = ry;
        public Brush Brush { get; set; } = brush;
        
        public void Draw(DrawingCanvas canvas) => canvas.Fill(Brush, new EllipsePolygon(CX, CY, RX, RY));
        
        public IPath GetBoundsPath()
            => new EllipsePolygon(CX, CY, RX, RY);
        
        public IPathCollection ToPaths() => new PathCollection(new EllipsePolygon(CX, CY, RX, RY));
    }
}
