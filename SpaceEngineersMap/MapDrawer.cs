using System;
using System.Collections.Generic;
using MathNet.Spatial.Euclidean;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using System.Linq;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap
{
    public class MapDrawer(
            Image<Argb32> bmp,
            List<(DrawingOps.IDrawingOp Op, bool IncludeInBounds, object? Info)> drawingOps,
            FontCollection fonts,
            List<ProjectedGpsEntry> entries,
            RotateFlipType rotation,
            CubeFace face,
            string[] prefixes,
            bool includeAuxTravels,
            double minMercatorLongitude,
            double maxMercatorLongitude,
            bool transverseMercator,
            double transverseMercatorMeridian
        )
    {
        private readonly int Width = bmp.Width;
        private readonly int Height = bmp.Height;
        private readonly List<ProjectedGpsEntry> Entries = entries;
        private readonly RotateFlipType Rotation = rotation;
        private readonly CubeFace Face = face;
        private readonly string[] Prefixes = prefixes;
        private readonly bool IncludeAuxTravels = includeAuxTravels && prefixes.Length == 0;
        private readonly double MinMercatorLongitude = minMercatorLongitude;
        private readonly double MaxMercatorLongitude = maxMercatorLongitude;
        private readonly bool TransverseMercator = transverseMercator;
        private readonly double TransverseMercatorMeridian = transverseMercatorMeridian;
        private readonly List<(DrawingOps.IDrawingOp Op, bool IncludeInBounds, object? Info)> DrawingOps = drawingOps;
        private static readonly Pen GridPen = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(64, 64, 64, 64)), 1.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen GridMinorPen = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(64, 64, 64, 32)), 0.5f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen TravelPen = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(0, 0, 255, 255)), 2.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen TravelPen2 = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(0, 0, 0, 255)), 2.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen TravelPenProx = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(32, 32, 64, 255)), 1.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen AltPen = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(139, 0, 0, 255)), 2.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen MissilePen = new SolidPen(new PenOptions(Color.OrangeRed, 1.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Pen BotPen = new SolidPen(new PenOptions(Color.FromPixel(new Rgba32(192, 64, 0, 255)), 2.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private static readonly Brush TickBrush = new SolidBrush(Color.LightCyan);
        private static readonly Brush POIBrush = new SolidBrush(Color.DarkViolet);
        private static readonly Brush POI2Brush = new SolidBrush(Color.DarkGreen);
        private static readonly Brush POI3Brush = new SolidBrush(Color.DarkRed);
        private static readonly Brush POI4Brush = new SolidBrush(Color.DarkOrange);
        private static readonly Brush TextBrush = new SolidBrush(Color.Black);
        private static readonly Brush Text2Brush = new SolidBrush(Color.DarkRed);
        private static readonly Pen TextOutlinePen = new SolidPen(new PenOptions(Color.White, 4.0f) { StrokeOptions = new StrokeOptions { LineCap = LineCap.Round, LineJoin = LineJoin.Round } });
        private readonly Font TextFont = new(fonts.Get("Microsoft Sans Serif"), 12.0f);
        private readonly Font Text2Font = new(fonts.Get("Microsoft Sans Serif"), 12.0f);

        private void AddMercatorFaceEdges()
        {
            var minofs = MinMercatorLongitude % 30;

            if (minofs < 0)
            {
                minofs += 30;
            }

            minofs *= 2048.0 / 90;

        }

        public void AddEdges()
        {
            if (Face != CubeFace.Mercator)
            {
                DrawingOps.Add((new DrawingOps.Rectangle(GridPen, 0, 0, Width, Height), false, new { Type = "edge" }));
            }
            else
            {
                AddMercatorFaceEdges();
            }
        }

        private void AddPolarLatLonLines()
        {
            var lats = new[] { 75, 60, 45 };
            var lons = new[] { -30, -15, 0, 15, 30, 45 };
            var mid = Width / 2;

            foreach (var lat in lats)
            {
                var latrad = (float)Math.Tan((90 - lat) * Math.PI / 180) * Width / 2;

                DrawingOps.Add((
                    new DrawingOps.Ellipse(GridPen, mid, mid, latrad, latrad),
                    false,
                    new
                    {
                        Type = "polar_lat",
                        Latitude = lat
                    }
                ));
            }

            foreach (var lon in lons)
            {
                var lontan = (float)Math.Tan(lon * Math.PI / 180) * Width;

                DrawingOps.Add((
                    new DrawingOps.Line(GridPen, mid - lontan, mid - Width, mid + lontan, mid + Width),
                    false,
                    new
                    {
                        Type = "polar_lon1",
                        Longitude = lon
                    }
                ));

                DrawingOps.Add((
                    new DrawingOps.Line(GridPen, mid - Width, mid + lontan, mid + Width, mid - lontan),
                    false,
                    new
                    {
                        Type = "polar_lon2",
                        Longitude = lon
                    }
                ));
            }
        }

        private void AddEquatorialLatLonLines()
        {
            var lats = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lons = new[] { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
            var lonrads = lons.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
            var lonincoss = lons.Select(l => 1.0f / (float)Math.Cos(l * Math.PI / 180)).ToArray();
            var lattans = lats.Select(l => (float)Math.Tan(l * Math.PI / 180) * Width / 2).ToArray();
            var mid = Width / 2;

            foreach (var lon in lons)
            {
                var lonrad = (float)Math.Tan(lon * Math.PI / 180) * Width / 2;

                DrawingOps.Add((
                    new DrawingOps.Line(GridPen, mid - lonrad, mid - Width, mid - lonrad, mid + Width),
                    false,
                    new
                    {
                        Type = "equ_lon",
                        Longitude = lon
                    }
                ));
            }

            foreach (var lat in lats)
            {
                var lattan = (float)Math.Tan(lat * Math.PI / 180) * Width / 2;

                DrawingOps.Add((
                    new DrawingOps.CubicBezier(
                        GridPen,
                        new PointF(0, (float)(mid + lattan * Math.Sqrt(2))),
                        new PointF(mid * 0.25f, mid + lattan * 1.06f),
                        new PointF(mid * 0.787f, mid + lattan),
                        new PointF(mid, mid + lattan)
                    ),
                    false,
                    new
                    {
                        Type = "equ_lat1",
                        Latitude = lat
                    }
                ));

                DrawingOps.Add((
                    new DrawingOps.CubicBezier(
                        GridPen,
                        new PointF(mid * 2, (float)(mid + lattan * Math.Sqrt(2))),
                        new PointF(mid * 1.75f, mid + lattan * 1.06f),
                        new PointF(mid * 1.213f, mid + lattan),
                        new PointF(mid, mid + lattan)
                    ),
                    false,
                    new
                    {
                        Type = "equ_lat2",
                        Latitude = lat
                    }
                ));
            }
        }

        private void AddMercatorLatLonLines()
        {
            var step = 5;
            var majstep = 15;
            var mid = Height / 2;

            var minofs = -MinMercatorLongitude % majstep;

            if (minofs > 0)
            {
                minofs -= majstep;
            }

            var maxofs = Width * 90.0 / 2048;

            for (int i = 0; i < maxofs - minofs; i += step)
            {
                var x = (float)((minofs + i) * 2048.0 / 90);
                var pen = i % majstep == 0 ? GridPen : GridMinorPen;
                DrawingOps.Add((
                    new DrawingOps.Line(pen, x, 0, x, Height),
                    false,
                    new
                    {
                        Type = "merc_lon",
                        Offset = i
                    }
                ));
            }

            for (int i = 0; i < 90; i += step)
            {
                var lattan = (float)(Math.Tan(i * Math.PI / 180) * Math.PI * 2048 / 4);
                var pen = i % majstep == 0 ? GridPen : GridMinorPen;

                DrawingOps.Add((
                    new DrawingOps.Line(pen, 0, mid + lattan, Width, mid + lattan),
                    false,
                    new
                    {
                        Type = "merc_lat1",
                        Latitude = -i
                    }
                ));

                DrawingOps.Add((
                    new DrawingOps.Line(pen, 0, mid - lattan, Width, mid - lattan),
                    false,
                    new
                    {
                        Type = "merc_lat2",
                        Latitude = i
                    }
                ));
            }
        }

        private void AddTransverseMercatorLatLonLines()
        {
            const int step = 5;
            const int majstep = 15;
            const double sampleStep = 1;
            const double minSampleStep = 1.0 / 16;
            var minMercatorLon = MinMercatorLongitude * Math.PI / 180.0;
            var maxMercatorLon = MaxMercatorLongitude * Math.PI / 180.0;
            var latLonMult = 1024 * 360.0 / Math.PI / 90;
            var limit = Math.Max(Width, Height) * 16;
            var maxDelta = Math.Min(Width, Height) / 16;

            bool TryProjectTransverseMercator(double latDeg, double lonDeg, out PointF point)
            {
                var lat = latDeg * Math.PI / 180.0;
                var lon = lonDeg * Math.PI / 180.0;

                var coords = new Vector3D(
                    -Math.Sin(lon) * Math.Cos(lat),
                    Math.Sin(lat),
                    Math.Cos(lon) * Math.Cos(lat)
                );

                var projected = MapUtils.ProjectTransverseMercator(
                    1024,
                    coords,
                    minMercatorLon,
                    maxMercatorLon,
                    latLonMult,
                    latLonMult,
                    false,
                    TransverseMercatorMeridian * Math.PI / 180.0
                );

                if (double.IsNaN(projected.X) || double.IsNaN(projected.Y))
                {
                    point = default;
                    return false;
                }

                point = new PointF(
                    Math.Clamp((float)(projected.X + Width / 2.0), -limit, limit),
                    Math.Clamp((float)(projected.Y + Height / 2.0), -limit, limit)
                );

                return true;
            }

            void DrawBezierSegment(Pen pen, List<(PointF Point, string Type, double Lat, double Lon)> points)
            {
                if (points.Count < 2)
                {
                    return;
                }

                if (points.Count == 2)
                {
                    DrawingOps.Add((
                        new DrawingOps.Line(pen, points[0].Point.X, points[0].Point.Y, points[1].Point.X, points[1].Point.Y),
                        false,
                        new
                        {
                            points[0].Type,
                            Lat1 = points[0].Lat,
                            Lon1 = points[0].Lon,
                            Lat2 = points[1].Lat,
                            Lon2 = points[1].Lon
                        }
                    ));

                    return;
                }

                for (var i = 0; i < points.Count - 1; i++)
                {
                    var p0 = i == 0 ? points[i] : points[i - 1];
                    var p1 = points[i];
                    var p2 = points[i + 1];
                    var p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];

                    if (float.IsNaN(p1.Point.X) || float.IsNaN(p1.Point.Y) || float.IsNaN(p2.Point.X) || float.IsNaN(p2.Point.Y))
                    {
                        continue;
                    }

                    var c1 = float.IsNaN(p0.Point.X) || float.IsNaN(p0.Point.Y)
                           ? p1.Point
                           : new PointF(
                                 Math.Clamp(p1.Point.X + (p2.Point.X - p0.Point.X) / 6f, -limit, limit),
                                 Math.Clamp(p1.Point.Y + (p2.Point.Y - p0.Point.Y) / 6f, -limit, limit)
                             );

                    var c2 = float.IsNaN(p3.Point.X) || float.IsNaN(p3.Point.Y)
                           ? p2.Point
                           : new PointF(
                                 Math.Clamp(p2.Point.X - (p3.Point.X - p1.Point.X) / 6f, -limit, limit),
                                 Math.Clamp(p2.Point.Y - (p3.Point.Y - p1.Point.Y) / 6f, -limit, limit)
                             );

                    DrawingOps.Add((
                        new DrawingOps.CubicBezier(pen, p1.Point, c1, c2, p2.Point),
                        false,
                        new
                        {
                            p1.Type,
                            Lat1 = p1.Lat,
                            Lon1 = p1.Lon,
                            Lat2 = p2.Lat,
                            Lon2 = p2.Lon
                        }
                    ));
                }
            }

            void TryAddSegment(List<(PointF Point, string Type, double Lat, double Lon)> points, string type, double lat, double lon)
            {
                if (TryProjectTransverseMercator(lat, lon, out var pt))
                {
                    if (points is [.., { Point: { } prevPoint, Lat: double prevLat, Lon: double prevLon }]
                        && (Math.Abs(pt.X - prevPoint.X) > maxDelta || Math.Abs(pt.Y - prevPoint.Y) > maxDelta))
                    {
                        if (Math.Abs(lat - prevLat) <= minSampleStep || Math.Abs(lon - prevLon) <= minSampleStep)
                        {
                            points.Add((new PointF(float.NaN, float.NaN), type, (prevLat + lat) / 2, (prevLon + lon) / 2));
                        }
                        else
                        {
                            TryAddSegment(points, type, (prevLat + lat) / 2, (prevLon + lon) / 2);
                        }
                    }

                    points.Add((pt, type, lat, lon));
                }
                else
                {
                    if (points is [.., { Lat: double prevLat, Lon: double prevLon }]
                        && (Math.Abs(lat - prevLat) > minSampleStep || Math.Abs(lon - prevLon) > minSampleStep))
                    {
                        TryAddSegment(points, type, (prevLat + lat) / 2, (prevLon + lon) / 2);
                    }

                    points.Add((new PointF(float.NaN, float.NaN), type, lat, lon));
                    return;
                }
            }

            for (var lat = -90.0 + step; lat <= 90.0 - step; lat += step)
            {
                var pen = lat % majstep == 0 ? GridPen : GridMinorPen;
                var segment = new List<(PointF Point, string Type, double Lat, double Lon)>();

                for (var lon = -180.0; lon <= 180.0; lon += sampleStep)
                {
                    TryAddSegment(segment, "trans_merc_lat", lat, lon);
                }

                if (segment.Count > 1)
                {
                    DrawBezierSegment(
                        pen,
                        segment
                    );
                }
            }

            for (var lon = -180.0; lon < 180.0; lon += step)
            {
                var pen = lon % majstep == 0 ? GridPen : GridMinorPen;
                var segment = new List<(PointF Point, string Type, double Lat, double Lon)>();

                var minLat = Math.Round(lon % 90) == 0 ? -90.0 : -89.0;
                var maxLat = Math.Round(lon % 90) == 0 ? 90.0 : 89.0;

                for (var lat = minLat; lat <= maxLat; lat += sampleStep)
                {
                    TryAddSegment(segment, "trans_merc_lon", lat, lon);
                }

                if (segment.Count > 1)
                {
                    DrawBezierSegment(
                        pen,
                        segment
                    );
                }
            }
        }

        public void AddLatLonLines()
        {
            switch (Face)
            {
                case CubeFace.Up:
                case CubeFace.Down:
                    AddPolarLatLonLines();
                    break;
                case CubeFace.Front:
                case CubeFace.Back:
                case CubeFace.Left:
                case CubeFace.Right:
                    AddEquatorialLatLonLines();
                    break;
                case CubeFace.Mercator when !TransverseMercator:
                    AddMercatorLatLonLines();
                    break;
                case CubeFace.Mercator:
                    AddTransverseMercatorLatLonLines();
                    break;
            }
        }

        public void AddPOIs()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                if (Prefixes.Length == 0 || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                {
                    if (ent.Name.Contains('@'))
                    {
                        if (IncludeAuxTravels || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
                        {
                            if (ent.Name.Contains('%'))
                            {
                                DrawingOps.Add((
                                    new DrawingOps.Ellipse(GridPen, nextpoint.X, nextpoint.Y, 3.5f, 3.5f),
                                    true,
                                    new
                                    {
                                        Type = "aux_poi_%",
                                        Entry = ent
                                    }
                                ));
                            }
                            else if (ent.Name.Contains('$'))
                            {
                                DrawingOps.Add((
                                    new DrawingOps.Ellipse(GridPen, nextpoint.X, nextpoint.Y, 3.5f, 3.5f),
                                    true,
                                    new
                                    {
                                        Type = "aux_poi_$",
                                        Entry = ent
                                    }
                                ));
                            }
                        }
                    }
                    else
                    {
                        if (ent.Name.Contains('%'))
                        {
                            DrawingOps.Add((
                                new DrawingOps.Ellipse(GridPen, nextpoint.X, nextpoint.Y, 3.5f, 3.5f),
                                true,
                                new
                                {
                                    type = "poi_%",
                                    Entry = ent
                                }
                            ));
                        }
                        else if (ent.Name.Contains('$') || ent.Name.Contains("[Base]") || ent.Name.Contains("[Empl]"))
                        {
                            var brush = POI2Brush;
                            if (ent.Description.StartsWith("[Bot]"))
                            {
                                brush = POI3Brush;
                            }

                            if (ent.Name.Contains("[Base]"))
                            {
                                DrawingOps.Add((
                                    new DrawingOps.FilledPolygon(brush, [
                                        new PointF(nextpoint.X, nextpoint.Y - 4.5f),
                                        new PointF(nextpoint.X + 4.5f, nextpoint.Y + 4.5f),
                                        new PointF(nextpoint.X - 4.5f, nextpoint.Y + 4.5f),
                                    ]),
                                    true,
                                    new
                                    {
                                        Type = "poi_base",
                                        Entry = ent
                                    }
                                ));
                            }
                            else if (ent.Name.Contains("[Empl]"))
                            {
                                DrawingOps.Add((
                                    new DrawingOps.FilledPolygon(brush, [
                                        new PointF(nextpoint.X, nextpoint.Y - 3.5f),
                                        new PointF(nextpoint.X + 3.5f, nextpoint.Y + 3.5f),
                                        new PointF(nextpoint.X - 3.5f, nextpoint.Y + 3.5f),
                                    ]),
                                    true,
                                    new
                                    {
                                        Type = "poi_empl",
                                        Entry = ent
                                    }
                                ));
                            }
                            else
                            {
                                DrawingOps.Add((
                                    new DrawingOps.FilledEllipse(brush, nextpoint.X, nextpoint.Y, 3.5f, 3.5f),
                                    true,
                                    new
                                    {
                                        Type = "poi_$",
                                        Entry = ent
                                    }
                                ));
                            }
                        }
                    }
                }
            }
        }

        private static PointF CalculateControlPoint(PointF rev, PointF point, PointF fwd)
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

        private static (PointF, PointF) ExtendControlPoints(PointF start, PointF ctrl1, PointF ctrl2, PointF end)
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

        public void AddPath()
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


                if (!ent.Name.Contains('$') && !ent.Name.Contains('='))
                {
                    if (!ent.Name.Contains('^'))
                    {
                        if (ent.Name.Contains('@'))
                        {
                            var draw = Math.Abs(nextpoint.X - altpoint.X) < Width && Math.Abs(nextpoint.Y - altpoint.Y) < Height && (IncludeAuxTravels || Prefixes.Any(p => ent.StartPart == p));
                            altpathsegs.Add((draw, pen, altpoint, nextpoint));
                        }
                        else
                        {
                            bool draw = Math.Abs(nextpoint.X - point.X) < Width && Math.Abs(nextpoint.Y - point.Y) < Height && (Prefixes.Length == 0 || Prefixes.Any(p => ent.StartPart == p));
                            pathsegs.Add((draw, pen, point, nextpoint));
                        }
                    }

                    if (ent.Name.Contains('@'))
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
                        DrawingOps.Add((
                            new DrawingOps.Line(pen, start.X, start.Y, end.X, end.Y),
                            false,
                            new
                            {
                                Type = "path_line"
                            }
                        ));
                    }
                    else
                    {
                        DrawingOps.Add((
                            new DrawingOps.CubicBezier(pen, start, ctrl1, ctrl2, end),
                            false,
                            new
                            {
                                Type = "path_bezier"
                            }
                        ));
                    }
                }
            }
        }

        public void AddTenMinutePoints()
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
                    && !ent.Name.Contains('$')
                    && !ent.Name.Contains('=')
                    && !ent.Name.Contains('@'))
                {
                    sincelast++;
                    var enttime = Math.Floor(ent.StartTime?.TotalMinutes / 10 ?? 0);

                    if (ent.StartPart != prefix || enttime != time)
                    {
                        if (sincelast >= 6)
                        {
                            DrawingOps.Add((
                                new DrawingOps.FilledEllipse(TickBrush, nextpoint.X, nextpoint.Y, 2f, 2f),
                                false,
                                new
                                {
                                    Type = "tem_min"
                                }
                            ));
                            sincelast = 0;
                        }

                        prefix = ent.StartPart;
                        time = Math.Floor(ent.StartTime?.TotalMinutes / 10 ?? 0);
                    }
                }
            }
        }

        public void AddPOITextPaths(Action<string, PointF, Font, Brush, Pen, bool, bool> addTextPath)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var ent = Entries[i].RotateFlip2D(Rotation);
                
                if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                {
                    var nextpoint = new PointF((float)(ent.X + Width / 2), (float)(ent.Y + Height / 2));
                    
                    if ((Prefixes.Length == 0 && (IncludeAuxTravels || !ent.Name.Contains('@'))) || Prefixes.Any(p => ent.StartPart == p || ent.EndPart == p))
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
                            description = description[5..].TrimStart();
                        }

                        addTextPath(description, nextpoint, font, brush, outlinepen, hidepart1, hidepart2);
                    }
                }
            }
        }

        public List<(Brush TextBrush, Pen OutlinePen, IPathCollection Path, string Text)> GetPOITextPaths()
        {
            var paths = new List<(Brush TextBrush, Pen OutlinePen, IPathCollection Path, string Text)>();

            AddPOITextPaths((d, np, f, b, o, h1, h2) => paths.Add(TextDrawing.GetTextPath(d, np, f, b, o, h1, h2)));

            return paths;
        }

        public void Draw(Image<Argb32> image) => Draw(image, DrawingOps);

        public static void Draw(Image<Argb32> image, List<(DrawingOps.IDrawingOp Op, bool IncludeInBounds, object? info)> ops)
        {
            image.Mutate(g => g.Paint(c =>
            {
                foreach (var (op, _, _) in ops)
                {
                    op.Draw(c);
                }
            }));
        }

        public RectangleF? GetBounds() => GetBounds(DrawingOps, new RectangleF(0, 0, Width, Height));

        public static RectangleF? GetBounds(List<(DrawingOps.IDrawingOp Op, bool IncludeInBounds, object? info)> ops, RectangleF bounds)
        {
            var boundsOps = ops.Where(e => e.IncludeInBounds).Select(e => e.Op.GetBoundsPath()).OfType<IPath>().ToList();

            var clipPath = new RectanglePolygon(bounds);
            var paths = new PathCollection(boundsOps.Select(e => e.Clip(clipPath)).Where(e => e.Bounds.Width != 0 && e.Bounds.Height != 0));

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
