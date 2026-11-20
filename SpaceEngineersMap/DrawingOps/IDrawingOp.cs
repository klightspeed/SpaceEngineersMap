using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public interface IDrawingOp
    {
        void Draw(DrawingCanvas canvas);
        IPath GetBoundsPath();
        IPathCollection ToPaths();
    }
}
