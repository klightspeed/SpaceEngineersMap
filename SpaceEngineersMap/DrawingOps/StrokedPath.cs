using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class StrokedPath(Pen pen, IPathCollection path) : IDrawingOp
    {
        public IPathCollection Path { get; set; } = path;
        public Pen Pen { get; set; } = pen;
        
        public void Draw(DrawingCanvas canvas) => canvas.Draw(Pen, Path);
        
        public IPath GetBoundsPath()
            => new RectanglePolygon(Path.Bounds).GenerateOutline(Pen.StrokeWidth);
        
        public IPathCollection ToPaths() => Path;
    }
}
