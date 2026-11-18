using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class FilledPath(Brush brush, IPathCollection path) : IDrawingOp
    {
        public IPathCollection Path { get; set; } = path;
        public Brush Brush { get; set; } = brush;
        
        public void Draw(IImageProcessingContext context) => context.Fill(Brush, Path);
        
        public IPath GetBoundsPath()
            => new RectangularPolygon(Path.Bounds);
        
        public IPathCollection ToPaths() => Path;
    }
}
