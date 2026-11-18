using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap.DrawingOps
{
    public interface IDrawingOp
    {
        void Draw(IImageProcessingContext context);
        IPath GetBoundsPath();
        IPathCollection ToPaths();
    }
}
