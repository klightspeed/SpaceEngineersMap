using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersMap
{
    public class Bounds
    {
        public float XMin { get; set; } = float.MaxValue;
        public float XMax { get; set; } = float.MinValue;
        public float YMin { get; set; } = float.MaxValue;
        public float YMax { get; set; } = float.MinValue;
        public float CropLeft { get; set; }
        public float CropRight { get; set; }
        public float CropTop { get; set; }
        public float CropBottom { get; set; }

        public Bounds(RectangleF crop)
        {
            CropLeft = crop.Left;
            CropRight = crop.Right;
            CropBottom = crop.Bottom;
            CropTop = crop.Top;
        }

        public bool IsValid => XMin < XMax && YMin < YMax;

        public RectangleF GetBounds()
        {
            return new RectangleF(XMin, YMin, XMax - XMin, YMax - YMin);
        }

        private void AddPoint(float x, float y)
        {
            if (x < XMin) XMin = x;
            if (x > XMax) XMax = x;
            if (y < YMin) YMin = y;
            if (y > YMax) YMax = y;
        }

        public void AddRectangle(float x, float y, float width, float height)
        {
            AddPoint(x, y);
            AddPoint(x + width, y + height);
        }

        public void AddRectangle(RectangleF? rect)
        {
            if (rect is RectangleF r)
            {
                AddRectangle(r.X, r.Y, r.Width, r.Height);
            }
        }

        public void AddRectangle(RectangleF? rect, PointF origin)
        {
            if (rect is RectangleF r)
            {
                AddRectangle(r.X + origin.X, r.Y + origin.Y, r.Width, r.Height);
            }
        }
    }
}
