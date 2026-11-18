using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;

namespace SpaceEngineersMap.DrawingOps
{
    public class FilledPolygon(Brush brush, params IEnumerable<PointF> points) : IDrawingOp
    {
        public PointF[] Points { get; set; } = [.. points];
        public Brush Brush { get; set; } = brush;
        
        public void Draw(IImageProcessingContext context) => context.Fill(Brush, new Polygon(Points));
        
        public IPath GetBoundsPath()
            => new Polygon(Points);
        
        public IPathCollection ToPaths() => new PathCollection(new Polygon(Points));
    }
}
