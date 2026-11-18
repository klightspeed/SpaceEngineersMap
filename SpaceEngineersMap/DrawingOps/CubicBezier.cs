using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class CubicBezier(Pen pen, PointF p1, PointF p2, PointF p3, PointF p4) : IDrawingOp
    {
        public PointF P1 { get; set; } = p1;
        public PointF P2 { get; set; } = p2;
        public PointF P3 { get; set; } = p3;
        public PointF P4 { get; set; } = p4;
        public Pen Pen { get; set; } = pen;
        
        public void Draw(IImageProcessingContext context) => context.DrawBeziers(Pen, P1, P2, P3, P4);
        
        public IPath GetBoundsPath()
        {
            return new Path(new CubicBezierLineSegment(P1, P2, P3, P4)).GenerateOutline(Pen.StrokeWidth);
        }

        public IPathCollection ToPaths()
        {
            return new PathCollection(new Path(new CubicBezierLineSegment(P1, P2, P3, P4)));
        }
    }
}
