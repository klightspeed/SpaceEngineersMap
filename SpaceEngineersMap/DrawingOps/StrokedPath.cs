using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class StrokedPath(Pen pen, IPathCollection path) : IDrawingOp
    {
        public IPathCollection Path { get; set; } = path;
        public Pen Pen { get; set; } = pen;
        
        public void Draw(IImageProcessingContext context) => context.Draw(Pen, Path);
        
        public IPath GetBoundsPath()
            => new RectangularPolygon(Path.Bounds).GenerateOutline(Pen.StrokeWidth);
        
        public IPathCollection ToPaths() => Path;
    }
}
