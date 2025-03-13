using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using System.Linq;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap
{
    public class MapDrawer
    {
        private readonly int Width;
        private readonly int Height;
        private readonly List<ProjectedGpsEntry> Entries;
        private readonly RotateFlipType Rotation;
        private readonly CubeFace Face;
        private readonly string[] Prefixes;
        private readonly bool IncludeAuxTravels;
        private readonly double MinMercatorLongitude;
        private readonly double MaxMercatorLongitude;
        private readonly Image<Argb32> Image;
        private List<IPath> BoundsPaths;
        private Pen GridPen;
        private Pen TravelPen;
        private Pen TravelPen2;
        private Pen TravelPenProx;
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

        public MapDrawer(Image<Argb32> bmp, List<ProjectedGpsEntry> entries, RotateFlipType rotation, CubeFace face, string[] prefixes, bool includeAuxTravels, double minMercatorLongitude, double maxMercatorLongitude)
        {
            Image = bmp;
            Width = bmp.Width;
            Height = bmp.Height;
            Entries = entries;
            Rotation = rotation;
            Prefixes = prefixes;
            Face = face;
            IncludeAuxTravels = includeAuxTravels && prefixes.Length == 0;
            MinMercatorLongitude = minMercatorLongitude;
            MaxMercatorLongitude = maxMercatorLongitude;
        }

        public void Open(FontCollection fonts)
        {
            GridPen = new SolidPen(new PenOptions(Color.FromRgba(0, 0, 0, 64), 1.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            TravelPen = new SolidPen(new PenOptions(Color.Blue, 2.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            TravelPen2 = new SolidPen(new PenOptions(Color.Black, 2.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            TravelPenProx = new SolidPen(new PenOptions(Color.FromRgba(32, 32, 64, 255), 1.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            AltPen = new SolidPen(new PenOptions(Color.DarkRed, 2.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            MissilePen = new SolidPen(new PenOptions(Color.OrangeRed, 1.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            BotPen = new SolidPen(new PenOptions(Color.FromRgba(192, 64, 0, 255), 2.0f) { EndCapStyle = EndCapStyle.Round, JointStyle = JointStyle.Round });
            POIBrush = new SolidBrush(Color.DarkViolet);
            POI2Brush = new SolidBrush(Color.DarkGreen);
            POI3Brush = new SolidBrush(Color.DarkRed);
            POI4Brush = new SolidBrush(Color.DarkOrange);
            TickBrush = new SolidBrush(Color.LightCyan);
            TextFont = new Font(fonts.Get("Microsoft Sans Serif"), 12.0f);
            TextBrush = new SolidBrush(Color.Black);
            Text2Font = new Font(fonts.Get("Microsoft Sans Serif"), 12.0f);
            Text2Brush = new SolidBrush(Color.DarkRed);
            TextOutlinePen = new SolidPen(new PenOptions(Color.White, 4.0f) { EndCapStyle = EndCapStyle.Round, JointStyle= JointStyle.Round });
            BoundsPaths = new List<IPath>();
        }

        private void DrawMercatorFaceEdges(IImageProcessingContext g)
        {
            var minofs = MinMercatorLongitude % 30;

            if (minofs < 0)
            {
                minofs += 30;
            }

            minofs *= 2048.0 / 90;

        }

        public void DrawEdges()
        {
            Image.Mutate(g =>
            {
                if (Face != CubeFace.Mercator)
                {
                    g.Draw(GridPen, new RectangleF(0, 0, Width, Height));
                }
                else
                {
                    DrawMercatorFaceEdges(g);
                }
            });
        }

        private void DrawPolarLatLonLines()
        {
            Image.Mutate(g =>
            {
                var lats = new[] { 75, 60, 45 };
                var lons = new[] { -30, -15, 0, 15, 30, 45 };
                var latrads = lats.Select(l => (float)Math.Tan((90 - l) * Math.PI / 180) * Width / 2).ToArray();
                var lontans = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width).ToArray();
                var mid = Width / 2;

                foreach (var latrad in latrads)
                {
                    g.Draw(GridPen, new EllipsePolygon(mid, mid, latrad));
                }

                foreach (var lontan in lontans)
                {
                    g.DrawLine(GridPen, new PointF(mid - lontan, mid - Width), new PointF(mid + lontan, mid + Width));
                    g.DrawLine(GridPen, new PointF(mid - Width, mid + lontan), new PointF(mid + Width, mid - lontan));
                }

            });
        }

        private void DrawEquatorialLatLonLines()
        {
            Image.Mutate(g =>
            {
                var lats = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
                var lons = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
                var lonrads = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
                var lonincoss = lons.Select(l => 1.0f / (float)Math.Cos(l * Math.PI / 180)).ToArray();
                var lattans = lats.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
                var mid = Width / 2;

                foreach (var lonrad in lonrads)
                {
                    g.DrawLine(GridPen, new PointF(mid - lonrad, mid - Width), new PointF(mid - lonrad, mid + Width));
                }

                foreach (var lattan in lattans)
                {
                    g.DrawBeziers(
                        GridPen,
                        new PointF(0, (float)(mid + lattan * Math.Sqrt(2))),
                        new PointF(mid * 0.25f, mid + lattan * 1.06f),
                        new PointF(mid * 0.787f, mid + lattan),
                        new PointF(mid, mid + lattan)
                    );

                    g.DrawBeziers(
                        GridPen,
                        new PointF(mid * 2, (float)(mid + lattan * Math.Sqrt(2))),
                        new PointF(mid * 1.75f, mid + lattan * 1.06f),
                        new PointF(mid * 1.213f, mid + lattan),
                        new PointF(mid, mid + lattan)
                    );
                }
            });
        }

        private void DrawMercatorLatLonLines()
        {
            Image.Mutate(g =>
            {
                var lats = new[] { -75, -60, -45, -30, -15, 0, 15, 30, 45, 60, 75 };
                var lattans = lats.Select(l => (float)(Math.Tan(l * Math.PI / 180) * Math.PI * 2048 / 4)).ToArray();
                var mid = Height / 2;

                var minofs = MinMercatorLongitude % 30;

                if (minofs < 0)
                {
                    minofs += 30;
                }

                minofs *= 2048.0 / 90;

                while (minofs < Width)
                {
                    g.DrawLine(GridPen, new PointF((float)minofs, 0), new PointF((float)minofs, Height));

                    minofs += 2048.0 / 6;
                }

                foreach (var lattan in lattans)
                {
                    g.DrawLine(GridPen, new PointF(0, lattan + mid), new PointF(Width, lattan + mid));
                }
            });
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
                case CubeFace.Mercator:
                    DrawMercatorLatLonLines();
                    break;
            }
        }

        public void DrawPOIs()
        {
            Image.Mutate(g =>
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    var ent = Entries[i].RotateFlip2D(Rotation);
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                    if (Prefixes.Length == 0 || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                    {
                        if (ent.Name.Contains("@"))
                        {
                            if (IncludeAuxTravels || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                            {
                                if (ent.Name.Contains("%"))
                                {
                                    var path = new EllipsePolygon(nextpoint, 3.5f);
                                    g.Fill(POIBrush, path);
                                    BoundsPaths.Add(path);
                                }
                                else if (ent.Name.Contains("$"))
                                {
                                    var path = new EllipsePolygon(nextpoint, 3.5f);
                                    g.Fill(POI2Brush, path);
                                    BoundsPaths.Add(path);
                                }
                            }
                        }
                        else
                        {
                            if (ent.Name.Contains("%"))
                            {
                                var path = new EllipsePolygon(nextpoint, 3.5f);
                                g.Fill(POIBrush, path);
                                BoundsPaths.Add(path);
                            }
                            else if (ent.Name.Contains("$") || ent.Name.Contains("[Base]") || ent.Name.Contains("[Empl]"))
                            {
                                var brush = POI2Brush;
                                if (ent.Description.StartsWith("[Bot]"))
                                {
                                    brush = POI3Brush;
                                }

                                IPath path;

                                if (ent.Name.Contains("[Base]"))
                                {
                                    path = new Polygon([
                                        new PointF(nextpoint.X, nextpoint.Y - 4.5f),
                                        new PointF(nextpoint.X + 4.5f, nextpoint.Y + 4.5f),
                                        new PointF(nextpoint.X - 4.5f, nextpoint.Y + 4.5f),
                                    ]);
                                }
                                else if (ent.Name.Contains("[Empl]"))
                                {
                                    path = new Polygon([
                                        new PointF(nextpoint.X, nextpoint.Y - 3.5f),
                                        new PointF(nextpoint.X + 3.5f, nextpoint.Y + 3.5f),
                                        new PointF(nextpoint.X - 3.5f, nextpoint.Y + 3.5f),
                                    ]);
                                }
                                else
                                {
                                    path = new EllipsePolygon(nextpoint, 3.5f);
                                }

                                g.Fill(brush, path);
                                BoundsPaths.Add(path);
                            }
                        }
                    }
                }
            });
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
            Image.Mutate(g =>
            {
                var ent0 = Entries[0].RotateFlip2D(Rotation);
                var point = new PointF((float)(ent0.X + Width / 2), (float)(ent0.Y + Height / 2));
                var time = ent0.EndTime ?? ent0.StartTime;
                var altpoint = point;
                var pathsegs = new List<(bool Draw, Pen Pen, PointF Start, PointF End)>();
                var altpathsegs = new List<(bool Draw, Pen Pen, PointF Start, PointF End)>();

                for (int i = 1; i < Entries.Count; i++)
                {
                    var ent = Entries[i].RotateFlip2D(Rotation);
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));

                    Pen pen;

                    if (ent.Name.Contains('>'))
                    {
                        pen = MissilePen;
                    }
                    else if (!ent.IsPlayer)
                    {
                        pen = BotPen;
                    }
                    else if (ent.Name.Contains('@'))
                    {
                        pen = AltPen;
                    }
                    else if (ent.Name.Contains('~'))
                    {
                        pen = TravelPenProx;
                    }
                    else if ((time?.Minutes ?? 0) % 2 == 0)
                    {
                        pen = TravelPen2;
                    }
                    else
                    {
                        pen = TravelPen;
                    }


                    if (!ent.Name.Contains("$") && !ent.Name.Contains("="))
                    {
                        if (!ent.Name.Contains("^"))
                        {
                            if (ent.Name.Contains("@"))
                            {
                                var draw = Math.Abs(nextpoint.X - altpoint.X) < Width && Math.Abs(nextpoint.Y - altpoint.Y) < Height && (IncludeAuxTravels || Prefixes.Any(p => ent.StartPart == p));
                                altpathsegs.Add((draw, pen, altpoint, nextpoint));

                                if (draw)
                                {
                                    BoundsPaths.Add(new Path(new LinearLineSegment(altpoint, nextpoint)).GenerateOutline(AltPen.StrokeWidth));
                                }
                            }
                            else
                            {
                                bool draw = Math.Abs(nextpoint.X - point.X) < Width && Math.Abs(nextpoint.Y - point.Y) < Height && (Prefixes.Length == 0 || Prefixes.Any(p => ent.StartPart == p));

                                pathsegs.Add((draw, pen, point, nextpoint));

                                if (draw)
                                {
                                    BoundsPaths.Add(new Path(new LinearLineSegment(altpoint, point)).GenerateOutline(AltPen.StrokeWidth));
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
                            time = ent.EndTime ?? ent.StartTime;
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
                            g.DrawLine(pen, start, end);
                        }
                        else
                        {
                            g.DrawBeziers(pen, start, ctrl1, ctrl2, end);
                        }
                    }
                }
            });
        }

        public void DrawTenMinutePoints()
        {
            Image.Mutate(g =>
            {
                var ent0 = Entries[0].RotateFlip2D(Rotation);
                var point = new PointF((float)(ent0.X + Width / 2), (float)(ent0.Y + Height / 2));
                var prefix = ent0.StartPart;
                var time = Math.Floor(ent0.StartTime?.TotalMinutes / 10 ?? 0);
                double sincelast = 0;

                for (int i = 1; i < Entries.Count; i++)
                {
                    var ent = Entries[i].RotateFlip2D(Rotation);
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));

                    if ((Prefixes.Length == 0 || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                        && !ent.Name.Contains("$")
                        && !ent.Name.Contains("=")
                        && !ent.Name.Contains("@"))
                    {
                        sincelast++;
                        var enttime = Math.Floor(ent.StartTime?.TotalMinutes / 10 ?? 0);

                        if (ent.StartPart != prefix || enttime != time)
                        {
                            if (sincelast >= 6)
                            {
                                g.Fill(TickBrush, new EllipsePolygon(nextpoint, 2f));
                                sincelast = 0;
                            }

                            prefix = ent.StartPart;
                            time = Math.Floor(ent.StartTime?.TotalMinutes / 10 ?? 0);
                        }
                    }
                }
            });
        }

        public List<(Brush TextBrush, Pen OutlinePen, IPathCollection Path)> GetPOITextPaths()
        {
            var paths = new List<(Brush TextBrush, Pen OutlinePen, IPathCollection Path)>();

            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                {
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                    if ((Prefixes.Length == 0 && (IncludeAuxTravels || !ent.Name.Contains("@"))) || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                    {
                        bool hidepart1 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.StartPart == p);
                        bool hidepart2 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.EndPart == p);

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

                        paths.Add(TextDrawing.GetTextPath(description, nextpoint, font, brush, outlinepen, hidepart1, hidepart2));
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
                    if ((Prefixes.Length == 0 && (IncludeAuxTravels || !ent.Name.Contains("@"))) || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                    {
                        bool hidepart1 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.StartPart == p);
                        bool hidepart2 = Prefixes.Length != 0 && !Prefixes.Any(p => ent.EndPart == p);

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

                        var textbounds = TextDrawing.DrawText(Image, description, nextpoint, font, brush, outlinepen, hidepart1, hidepart2);

                        if (textbounds is RectangleF rect)
                        {
                            BoundsPaths.Add(new RectangularPolygon(rect));
                        }
                    }
                }
            }
        }

        public RectangleF? GetBounds()
        {
            var clipPath = new RectangularPolygon(new RectangleF(0, 0, Width, Height));
            var paths = new PathCollection(BoundsPaths.Select(e => e.Clip(clipPath)).Where(e => e.Bounds.Width != 0 && e.Bounds.Height != 0));

            if (paths.Bounds.Width == 0 || paths.Bounds.Height == 0)
            {
                return null;
            }
            else
            {
                return paths.Bounds;
            }
        }
    }
}
