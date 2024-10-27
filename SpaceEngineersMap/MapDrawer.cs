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
        private readonly int Width;
        private readonly int Height;
        private readonly List<ProjectedGpsEntry> Entries;
        private readonly RotateFlipType Rotation;
        private readonly CubeFace Face;
        private readonly string[] Prefixes;
        private readonly bool IncludeAuxTravels;
        private Graphics Graphics;
        private Region BoundsRegion;
        private Pen GridPen;
        private Pen TravelPen;
        private Pen TravelPen2;
        private Pen AltPen;
        private Pen MissilePen;
        private Pen BotPen;
        private Brush TickBrush;
        private Brush POIBrush;
        private Brush POI2Brush;
        private Brush POI3Brush;
        private Brush POI4Brush;
        private Font TextFont;
        private Brush TextBrush;
        private Font Text2Font;
        private Brush Text2Brush;
        private Pen TextOutlinePen;

        public MapDrawer(Bitmap bmp, Graphics graphics, List<ProjectedGpsEntry> entries, RotateFlipType rotation, CubeFace face, string[] prefixes, bool includeAuxTravels)
        {
            Width = bmp.Width;
            Height = bmp.Height;
            Graphics = graphics;
            Entries = entries;
            Rotation = rotation;
            Prefixes = prefixes;
            Face = face;
            IncludeAuxTravels = includeAuxTravels && prefixes.Length == 0;
        }

        public void Open()
        {
            GridPen = new Pen(Color.FromArgb(64, 0, 0, 0))
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            TravelPen = new Pen(Color.Blue, 2.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            TravelPen2 = new Pen(Color.Black, 2.0f)
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
            MissilePen = new Pen(Color.OrangeRed, 1.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            BotPen = new Pen(Color.FromArgb(192, 64, 0), 2.0f)
            {
                LineJoin = LineJoin.Round,
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };
            POIBrush = new SolidBrush(Color.DarkViolet);
            POI2Brush = new SolidBrush(Color.DarkGreen);
            POI3Brush = new SolidBrush(Color.DarkRed);
            POI4Brush = new SolidBrush(Color.DarkOrange);
            TickBrush = new SolidBrush(Color.LightCyan);
            TextFont = new Font(FontFamily.GenericSansSerif, 12.0f, GraphicsUnit.Pixel);
            TextBrush = new SolidBrush(Color.Black);
            Text2Font = new Font(FontFamily.GenericSerif, 12.0f, GraphicsUnit.Pixel);
            Text2Brush = new SolidBrush(Color.DarkRed);
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
            Graphics.DrawRectangle(GridPen, 0, 0, Width, Height);
        }

        private void DrawPolarLatLonLines()
        {
            var lats = new[] { 75, 60, 45 };
            var lons = new[] { -30, -15, 0, 15, 30, 45 };
            var latrads = lats.Select(l => (float)Math.Tan((90 - l) * Math.PI / 180) * Width / 2).ToArray();
            var lontans = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width).ToArray();
            var mid = Width / 2;

            foreach (var latrad in latrads)
            {
                Graphics.DrawEllipse(GridPen, new RectangleF(mid - latrad, mid - latrad, latrad * 2, latrad * 2));
            }

            foreach (var lontan in lontans)
            {
                Graphics.DrawLine(GridPen, mid - lontan, mid - Width, mid + lontan, mid + Width);
                Graphics.DrawLine(GridPen, mid - Width, mid + lontan, mid + Width, mid - lontan);
            }
        }

        private void DrawEquatorialLatLonLines()
        {
            var lats = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lons = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lonrads = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
            var lonincoss = lons.Select(l => 1.0f / (float)Math.Cos(l * Math.PI / 180)).ToArray();
            var lattans = lats.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
            var mid = Width / 2;

            foreach (var lonrad in lonrads)
            {
                Graphics.DrawLine(GridPen, mid - lonrad, mid - Width, mid - lonrad, mid + Width);
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
                var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                if (Prefixes.Length == 0 || Prefixes.Any(p => ent.Name.StartsWith(p) || ent.Name.Contains("-" + p)))
                {
                    if (ent.Name.Contains("@"))
                    {
                        if (IncludeAuxTravels || Prefixes.Any(p => ent.Name.StartsWith(p) || ent.Name.Contains("-" + p)))
                        {
                            if (ent.Name.Contains("%"))
                            {
                                Graphics.FillEllipse(POIBrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                                BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                            }
                            else if (ent.Name.Contains("$"))
                            {
                                Graphics.FillEllipse(POI2Brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                                BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                            }
                        }
                    }
                    else
                    {
                        if (ent.Name.Contains("%"))
                        {
                            Graphics.FillEllipse(POIBrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                            BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                        }
                        else if (ent.Name.Contains("$") || ent.Name.Contains("[Base]") || ent.Name.Contains("[Empl]"))
                        {
                            var brush = POI2Brush;
                            if (ent.Description.StartsWith("[Bot]"))
                            {
                                brush = POI3Brush;
                            }

                            if (ent.Name.Contains("[Base]"))
                            {
                                Graphics.FillPolygon(brush, new[] {
                                    new PointF(nextpoint.X, nextpoint.Y - 4.5f),
                                    new PointF(nextpoint.X + 4.5f, nextpoint.Y + 4.5f),
                                    new PointF(nextpoint.X - 4.5f, nextpoint.Y + 4.5f),
                                });
                            }
                            else if (ent.Name.Contains("[Empl]"))
                            {
                                Graphics.FillPolygon(brush, new[] {
                                    new PointF(nextpoint.X, nextpoint.Y - 3.5f),
                                    new PointF(nextpoint.X + 3.5f, nextpoint.Y + 3.5f),
                                    new PointF(nextpoint.X - 3.5f, nextpoint.Y + 3.5f),
                                });
                            }
                            else
                            {
                                Graphics.FillEllipse(brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                            }

                            BoundsRegion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                        }
                    }
                }
            }
        }

        private PointF CalculateControlPoint(PointF rev, PointF point, PointF fwd)
        {
            double x1 = point.X - rev.X;
            double y1 = point.Y - rev.Y;
            double d1 = Math.Sqrt(x1 * x1 + y1 * y1);

            double x2 = point.X - fwd.X;
            double y2 = point.Y - fwd.Y;
            double d2 = Math.Sqrt(x2 * x2 + y2 * y2);

            if (d1 < 0.2 || d2 < 0.2)
            {
                return point;
            }

            double dm = Math.Min(d1, d2);

            x1 *= dm / d1;
            y1 *= dm / d1;
            x2 *= dm / d2;
            y2 *= dm / d2;

            return new PointF((float)(point.X + (x1 - x2) / 4), (float)(point.Y + (y1 - y2) / 4));
        }

        private (PointF, PointF) ExtendControlPoints(PointF start, PointF ctrl1, PointF ctrl2, PointF end)
        {
            if (ctrl1 == start && ctrl2 == end)
            {
                ctrl1 = new PointF((start.X * 3 + end.X) / 4, (start.Y * 3 + end.Y) / 4);
                ctrl2 = new PointF((end.X * 3 + start.X) / 4, (end.Y * 3 + start.Y) / 4);
            }
            else if (ctrl1 == start)
            {
                var ep2 = new PointF(ctrl2.X * 2 - end.X, ctrl2.Y * 2 - end.Y);
                ctrl1 = new PointF((start.X * 3 + ep2.X) / 4, (start.Y * 3 + ep2.Y) / 4);
            }
            else if (ctrl2 == end)
            {
                var ep1 = new PointF(ctrl1.X * 2 - start.X, ctrl1.Y * 2 - start.Y);
                ctrl2 = new PointF((end.X * 3 + ep1.X) / 4, (end.Y * 3 + ep1.Y) / 4);
            }

            return (ctrl1, ctrl2);
        }

        public void DrawPath()
        {
            var ent0 = Entries[0].RotateFlip2D(Rotation);
            var point = new PointF((float)(ent0.X + Width / 2), (float)(ent0.Y + Height / 2));
            var altpoint = point;
            var pathsegs = new List<(bool Draw, Pen Pen, PointF Start, PointF End)>();
            var altpathsegs = new List<(bool Draw, Pen Pen, PointF Start, PointF End)>();

            for (int i = 1; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));

                var pen = ent.Name.Contains("@") ? AltPen : ((ent.Name[8] - '0') % 2 == 0 ? TravelPen2 : TravelPen);

                if (ent.Name.Contains(">"))
                {
                    pen = MissilePen;
                }
                else if (!ent.IsPlayer)
                {
                    pen = BotPen;
                }

                if (!ent.Name.Contains("$") && !ent.Name.Contains("="))
                {
                    if (!ent.Name.Contains("^"))
                    {
                        if (ent.Name.Contains("@"))
                        {
                            var draw = Math.Abs(nextpoint.X - altpoint.X) < Width && Math.Abs(nextpoint.Y - altpoint.Y) < Height && (IncludeAuxTravels || Prefixes.Any(p => ent.Name.StartsWith(p)));
                            altpathsegs.Add((draw, pen, altpoint, nextpoint));

                            if (draw)
                            {
                                using (var path = new GraphicsPath())
                                {
                                    path.AddLine(altpoint, nextpoint);
                                    BoundsRegion.Union(path.GetBounds(new Matrix(), AltPen));
                                }
                            }
                        }
                        else
                        {
                            bool draw = Math.Abs(nextpoint.X - point.X) < Width && Math.Abs(nextpoint.Y - point.Y) < Height && (Prefixes.Length == 0 || Prefixes.Any(p => ent.Name.StartsWith(p)));

                            pathsegs.Add((draw, pen, point, nextpoint));

                            if (draw)
                            {
                                using (var path = new GraphicsPath())
                                {
                                    path.AddLine(point, nextpoint);
                                    BoundsRegion.Union(path.GetBounds(new Matrix(), TravelPen));
                                }
                            }
                        }
                    }

                    if (ent.Name.Contains("@"))
                    {
                        altpoint = nextpoint;
                    }
                    else
                    {
                        altpoint = point = nextpoint;
                    }
                }
            }

            var pathbezier = new List<(bool Draw, Pen Pen, PointF Start, PointF Ctrl1, PointF Ctrl2, PointF End)>();

            for (int i = 0; i < altpathsegs.Count; i++)
            {
                var pathseg = altpathsegs[i];
                var prev = i <= 0 ? pathseg : altpathsegs[i - 1];
                var next = i >= altpathsegs.Count - 1 ? pathseg : altpathsegs[i + 1];
                PointF ctrl1 = prev.End == pathseg.Start ? CalculateControlPoint(prev.Start, pathseg.Start, pathseg.End) : pathseg.Start;
                PointF ctrl2 = next.Start == pathseg.End ? CalculateControlPoint(next.End, pathseg.End, pathseg.Start) : pathseg.End;
                (ctrl1, ctrl2) = ExtendControlPoints(pathseg.Start, ctrl1, ctrl2, pathseg.End);
                pathbezier.Add((pathseg.Draw, pathseg.Pen, pathseg.Start, ctrl1, ctrl2, pathseg.End));
            }

            for (int i = 0; i < pathsegs.Count; i++)
            {
                var pathseg = pathsegs[i];
                var prev = i <= 0 ? pathseg : pathsegs[i - 1];
                var next = i >= pathsegs.Count - 1 ? pathseg : pathsegs[i + 1];
                PointF ctrl1 = prev.End == pathseg.Start ? CalculateControlPoint(prev.Start, pathseg.Start, pathseg.End) : pathseg.Start;
                PointF ctrl2 = next.Start == pathseg.End ? CalculateControlPoint(next.End, pathseg.End, pathseg.Start) : pathseg.End;
                (ctrl1, ctrl2) = ExtendControlPoints(pathseg.Start, ctrl1, ctrl2, pathseg.End);
                pathbezier.Add((pathseg.Draw, pathseg.Pen, pathseg.Start, ctrl1, ctrl2, pathseg.End));
            }

            foreach (var (draw, pen, start, ctrl1, ctrl2, end) in pathbezier)
            {
                if (draw)
                {
                    var dc0 = new SizeF(end.X - start.X, end.Y - start.Y);
                    var dc1 = new SizeF(ctrl1.X - start.X, ctrl1.Y - start.Y);
                    var dc2 = new SizeF(ctrl2.X - end.X, ctrl2.Y - end.Y);

                    if (dc0.Width * dc0.Width + dc0.Height * dc0.Height < 1 || dc1.Width * dc1.Width + dc1.Height * dc1.Height < 1 || dc2.Width * dc2.Width + dc2.Height * dc2.Height < 1)
                    {
                        Graphics.DrawLine(pen, start, end);
                    }
                    else
                    {
                        Graphics.DrawBezier(pen, start, ctrl1, ctrl2, end);
                    }
                }
            }
        }

        public void DrawTenMinutePoints()
        {
            var ent0 = Entries[0].RotateFlip2D(Rotation);
            var point = new PointF((float)(ent0.X + Width / 2), (float)(ent0.Y + Height / 2));
            var prefix = ent0.Name.Substring(0, 9);
            double sincelast = 0;

            for (int i = 1; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));

                if ((Prefixes.Length == 0 || Prefixes.Any(p => ent.Name.StartsWith(p) || ent.Name.Contains("-" + p)))
                    && !ent.Name.Contains("$")
                    && !ent.Name.Contains("=")
                    && !ent.Name.Contains("@"))
                {
                    sincelast++;

                    if (!ent.Name.StartsWith(prefix))
                    {
                        if (sincelast >= 6)
                        {
                            Graphics.FillEllipse(TickBrush, nextpoint.X - 1f, nextpoint.Y - 1f, 2, 2);
                            sincelast = 0;
                        }

                        prefix = ent.Name.Substring(0, 9);
                    }
                }
            }
        }

        public List<(Brush TextBrush, Pen OutlinePen, GraphicsPath Path)> GetPOITextPaths()
        {
            var paths = new List<(Brush TextBrush, Pen OutlinePen, GraphicsPath Path)>();

            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                {
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                    if ((Prefixes.Length == 0 && (IncludeAuxTravels || !ent.Name.Contains("@"))) || Prefixes.Any(p => ent.Name.StartsWith(p) || ent.Name.Contains("-" + p)))
                    {
                        bool hidepart1 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.Name.StartsWith(p));
                        bool hidepart2 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.Name.Contains("-" + p));

                        var font = TextFont;
                        var brush = TextBrush;
                        var outlinepen = TextOutlinePen;
                        var description = ent.Description;

                        if (description.StartsWith("[Bot]"))
                        {
                            font = Text2Font;
                            brush = Text2Brush;
                            description = description.Substring(5).TrimStart();
                        }

                        paths.Add(TextDrawing.GetTextPath(Graphics, description, nextpoint, font, brush, outlinepen, hidepart1, hidepart2));
                    }
                }
            }

            return paths;
        }

        public void DrawPOIText()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                {
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                    if ((Prefixes.Length == 0 && (IncludeAuxTravels || !ent.Name.Contains("@"))) || Prefixes.Any(p => ent.Name.StartsWith(p) || ent.Name.Contains("-" + p)))
                    {
                        bool hidepart1 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.Name.StartsWith(p));
                        bool hidepart2 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.Name.Contains("-" + p));

                        var font = TextFont;
                        var brush = TextBrush;
                        var outlinepen = TextOutlinePen;
                        var description = ent.Description;

                        if (description.StartsWith("[Bot]"))
                        {
                            font = Text2Font;
                            brush = Text2Brush;
                            description = description.Substring(5);
                        }

                        var textbounds = TextDrawing.DrawText(Graphics, description, nextpoint, font, brush, outlinepen, hidepart1, hidepart2);
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
            BoundsRegion.Intersect(new RectangleF(0, 0, Width, Height));

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
        }
    }
}
