using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public class Line(Pen pen, float x1, float y1, float x2, float y2) : IDrawingOp
    {
        public float X1 { get; set; } = x1;
        public float Y1 { get; set; } = y1;
        public float X2 { get; set; } = x2;
        public float Y2 { get; set; } = y2;
        public Pen Pen { get; set; } = pen;
        
        public void Draw(DrawingCanvas canvas) => canvas.DrawLine(Pen, new PointF(X1, Y1), new PointF(X2, Y2));
        
        public IPath GetBoundsPath()
            => new Path(new LinearLineSegment(new PointF(X1, Y1), new PointF(X2, Y2))).GenerateOutline(Pen.StrokeWidth);

        public IPathCollection ToPaths()
            => new PathCollection(new Path(new LinearLineSegment(new PointF(X1, Y1), new PointF(X2, Y2))));
    }
}
