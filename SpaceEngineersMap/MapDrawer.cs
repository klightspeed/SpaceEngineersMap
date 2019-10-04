using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersMap
{
    public class MapDrawer : IDisposable
    {
        private readonly Bitmap Bitmap;
        private readonly List<GpsEntry> Entries;
        private readonly RotateFlipType Rotation;
        private readonly CubeFace Face;
        private readonly string Prefix;
        private Graphics Graphics;
        private Region BoundsRegion;
        private Pen GridPen;
        private Pen TravelPen;
        private Pen AltPen;
        private Brush POIBrush;
        private Brush POI2Brush;
        private Font TextFont;
        private Brush TextBrush;
        private Pen TextOutlinePen;

        public MapDrawer(Bitmap bmp, List<GpsEntry> entries, RotateFlipType rotation, CubeFace face, string prefix)
        {
            Bitmap = bmp;
            Entries = entries;
            Rotation = rotation;
            Prefix = prefix;
            Face = face;
        }

        public void Open()
        {
            Graphics = Graphics.FromImage(Bitmap);
            Graphics.SmoothingMode = SmoothingMode.HighQuality;
            GridPen = new Pen(Color.FromArgb(64, 0, 0, 0))
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            TravelPen = new Pen(Color.DarkBlue, 2.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            AltPen = new Pen(Color.DarkRed, 2.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            POIBrush = new SolidBrush(Color.DarkViolet);
            POI2Brush = new SolidBrush(Color.DarkGreen);
            TextFont = new Font(FontFamily.GenericSansSerif, 12.0f, GraphicsUnit.Pixel);
            TextBrush = new SolidBrush(Color.Black);
            TextOutlinePen = new Pen(Color.White, 4.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            BoundsRegion = new Region();
            BoundsRegion.MakeEmpty();
        }

        public void DrawEdges()
        {
            Graphics.DrawRectangle(GridPen, 0, 0, Bitmap.Width, Bitmap.Height);
        }

        private void DrawPolarLatLonLines()
        {
            var lats = new[] { 75, 60, 45 };
            var lons = new[] { -30, -15, 0, 15, 30, 45 };
            var latrads = lats.Select(l => (float)Math.Tan((90 - l) * Math.PI / 180) * Bitmap.Width / 2).ToArray();
            var lontans = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Bitmap.Width).ToArray();
            var mid = Bitmap.Width / 2;

            foreach (var latrad in latrads)
            {
                Graphics.DrawEllipse(GridPen, new RectangleF(mid - latrad, mid - latrad, latrad * 2, latrad * 2));
            }

            foreach (var lontan in lontans)
            {
                Graphics.DrawLine(GridPen, mid - lontan, mid - Bitmap.Width, mid + lontan, mid + Bitmap.Width);
                Graphics.DrawLine(GridPen, mid - Bitmap.Width, mid + lontan, mid + Bitmap.Width, mid - lontan);
            }
        }

        private void DrawEquatorialLatLonLines()
        {
            var lats = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lons = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lonrads = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Bitmap.Width / 2).ToArray();
            var lonincoss = lons.Select(l => 1.0f / (float)Math.Cos(l * Math.PI / 180)).ToArray();
            var lattans = lats.Select(l => (float)Math.Tan(l * Math.PI / 180) * Bitmap.Width / 2).ToArray();
            var mid = Bitmap.Width / 2;

            foreach (var lonrad in lonrads)
            {
                Graphics.DrawLine(GridPen, mid - lonrad, mid - Bitmap.Width, mid - lonrad, mid + Bitmap.Width);
            }

            foreach (var lattan in lattans)
            {
                var points = Enumerable.Range(0, lons.Length).Select(i => new PointF(mid + lonrads[i], mid + lattan * lonincoss[i])).ToArray();
                Graphics.DrawCurve(GridPen, points, 0.5f);
            }
        }

        public void DrawLatLonLines()
        {
            switch (Face)
            {
                case CubeFace.Up:
                case CubeFace.Down:
                    DrawPolarLatLonLines();
                    break;
                case CubeFace.Front:
                case CubeFace.Back:
                case CubeFace.Left:
                case CubeFace.Right:
                    DrawEquatorialLatLonLines();
                    break;
            }
        }

        public void DrawPOIs()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                var nextpoint = new PointF((float)(ent.X + Bitmap.Width / 2), (float)(ent.Y + Bitmap.Height / 2));
                if (ent.Name.StartsWith(Prefix) || ent.Name.Contains("-" + Prefix))
                {
                    if (ent.Name.EndsWith("@$"))
                    {
                        if (Prefix != "")
                        {
                            Graphics.FillEllipse(POI2Brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                            BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                        }
                    }
                    else if (ent.Name.EndsWith("$"))
                    {
                        Graphics.FillEllipse(POI2Brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                    }
                    else if (ent.Name.EndsWith("@%"))
                    {
                        if (Prefix != "")
                        {
                            Graphics.FillEllipse(POIBrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                            BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                        }
                    }
                    else if (ent.Name.EndsWith("%"))
                    {
                        Graphics.FillEllipse(POIBrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                    }
                }
            }
        }

        public void DrawPath()
        {
            var ent0 = Entries[0].RotateFlip2D(Rotation);
            var point = new PointF((float)(ent0.X + Bitmap.Width / 2), (float)(ent0.Y + Bitmap.Height / 2));
            var altpoint = point;

            for (int i = 1; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                var nextpoint = new PointF((float)(ent.X + Bitmap.Width / 2), (float)(ent.Y + Bitmap.Height / 2));
                if (ent.Name.EndsWith("@") || ent.Name.EndsWith("@%"))
                {
                    if (ent.Name.StartsWith(Prefix) && Prefix != "")
                    {
                        Graphics.DrawLine(AltPen, altpoint, nextpoint);
                        using (var path = new GraphicsPath())
                        {
                            path.AddLine(altpoint, nextpoint);
                            BoundsRegion.Union(path.GetBounds(new Matrix(), AltPen));
                        }
                    }
                    altpoint = nextpoint;
                }
                else if (ent.Name.EndsWith("^"))
                {
                    altpoint = point = nextpoint;
                }
                else if (!ent.Name.EndsWith("$") && !ent.Name.EndsWith("#"))
                {
                    if (ent.Name.StartsWith(Prefix))
                    {
                        Graphics.DrawLine(TravelPen, point, nextpoint);
                        using (var path = new GraphicsPath())
                        {
                            path.AddLine(point, nextpoint);
                            BoundsRegion.Union(path.GetBounds(new Matrix(), TravelPen));
                        }
                    }
                    altpoint = point = nextpoint;
                }
            }
        }

        public void DrawPOIText()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                {
                    var nextpoint = new PointF((float)(ent.X + Bitmap.Width / 2), (float)(ent.Y + Bitmap.Height / 2));
                    if ((ent.Name.StartsWith(Prefix) || ent.Name.Contains("-" + Prefix)) && (Prefix != "" || !ent.Name.Contains("@")))
                    {
                        bool hidepart1 = !ent.Name.StartsWith(Prefix);
                        bool hidepart2 = !ent.Name.Contains("-" + Prefix);

                        var textbounds = TextDrawing.DrawText(Graphics, ent.Description, nextpoint, TextFont, TextBrush, TextOutlinePen, hidepart1, hidepart2);
                        if (textbounds is RectangleF rect)
                        {
                            BoundsRegion.Union(rect);
                        }
                    }
                }
            }
        }

        public RectangleF? GetBounds()
        {
            BoundsRegion.Intersect(new RectangleF(0, 0, Bitmap.Width, Bitmap.Height));

            if (BoundsRegion.IsEmpty(Graphics))
            {
                return null;
            }
            else
            {
                return BoundsRegion.GetBounds(Graphics);
            }
        }

        public void Dispose()
        {
            TextOutlinePen?.Dispose();
            TextBrush?.Dispose();
            TextFont?.Dispose();
            POIBrush?.Dispose();
            POI2Brush?.Dispose();
            AltPen?.Dispose();
            TravelPen?.Dispose();
            BoundsRegion?.Dispose();
            Graphics?.Dispose();
        }
    }
}
