using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class FilledPath(Brush brush, IPathCollection path) : IDrawingOp
    {
        public IPathCollection Path { get; set; } = path;
        public Brush Brush { get; set; } = brush;
        
        public void Draw(DrawingCanvas canvas) => canvas.Fill(Brush, Path);
        
        public IPath GetBoundsPath()
            => new RectanglePolygon(Path.Bounds);
        
        public IPathCollection ToPaths() => Path;
    }
}
