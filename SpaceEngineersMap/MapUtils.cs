using MathNet.Spatial.Euclidean;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public static class MapUtils
    {
        private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static string[] Faces { get; } = { "down", "back", "left", "front", "right", "up" };

        public static FontCollection Fonts = new FontCollection().AddSystemFonts();

        public static CubeFace GetFace(string face)
        {
            switch (face?.ToLowerInvariant())
            {
                case "up":
                    return CubeFace.Up;
                case "down":
                    return CubeFace.Down;
                case "left":
                    return CubeFace.Left;
                case "right":
                    return CubeFace.Right;
                case "front":
                    return CubeFace.Front;
                case "back":
                    return CubeFace.Back;
                case "mercator":
                    return CubeFace.Mercator;
                default:
                    return CubeFace.None;
            }
        }

        public static (Vector3D Position, Quaternion Rotation) GetPlanetPositionAndOrientation(string savedir, string planetname)
        {
            foreach (var savefile in Directory.EnumerateFiles(savedir, "SANDBOX_*.sbs"))
            {
                var xdoc = XDocument.Load(savefile);
                var objects = xdoc.Root.Element("SectorObjects").Elements("MyObjectBuilder_EntityBase");
                foreach (var obj in objects)
                {
                    if (obj.Attribute(xsi + "type")?.Value == "MyObjectBuilder_Planet" && string.Equals(obj.Element("PlanetGenerator")?.Value, planetname, StringComparison.OrdinalIgnoreCase))
                    {
                        var positionAndOrientation = obj.Element("PositionAndOrientation");
                        var positionElement = positionAndOrientation.Element("Position");
                        var orientationElement = positionAndOrientation.Element("Orientation");
                        var radius = double.Parse(obj.Element("MaximumHillRadius").Value);
                        double size = 128;
                        while (size < radius) size *= 2;

                        var pos = new Vector3D(
                            double.Parse(positionElement.Attribute("x").Value) + size + 0.5,
                            double.Parse(positionElement.Attribute("y").Value) + size + 0.5,
                            double.Parse(positionElement.Attribute("z").Value) + size + 0.5
                        );
                        var rotation = new Quaternion(
                            double.Parse(orientationElement.Element("W").Value),
                            double.Parse(orientationElement.Element("X").Value),
                            double.Parse(orientationElement.Element("Y").Value),
                            double.Parse(orientationElement.Element("Z").Value)
                        );
                        return (pos, rotation);
                    }
                }
            }

            return (new Vector3D(0, 0, 0), new Quaternion(1, 0, 0, 0));
        }

        public static Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> GetGPSEntries(string savedir, string planetname, bool rotate45, Vector3D planetPos, Quaternion planetRot, out string endname, double minMercatorLon = -180, double maxMercatorLon = 180, bool northIsYMinus = false)
        {
            var namere = new Regex(@"^P\d\d\w?\.\d\d\.\d\d\.\d\d");
            var xdoc = XDocument.Load(System.IO.Path.Combine(savedir, "Sandbox.sbc"));

            var playerEntity =
                xdoc.Root
                    .Element("ControlledObject")
                   ?.Value;

            var idents =
                xdoc.Root
                    .Element("Identities")
                    .Elements("MyObjectBuilder_Identity")
                    .ToDictionary(
                        e => e.Element("IdentityId").Value,
                        e => (
                            Name: e.Element("DisplayName").Value,
                            EntityId: e.Element("CharacterEntityId").Value
                        )
                    );

            var items =
                xdoc.Root
                    .Element("Gps")
                    .Element("dictionary")
                    .Elements("item")
                    .ToList();

            var entlists =
                items
                    .Select(i => (
                        IdentityId: i.Element("Key").Value,
                        Identity: idents.TryGetValue(i.Element("Key").Value, out var ident) ? ident : default,
                        Entries: i.Element("Value").Element("Entries").Elements("Entry").ToList()
                    ))
                    .Select(i => i.Entries.Select(e => GpsEntry.FromXML(e, i.Identity.Name, i.Identity.EntityId != "0")))
                    .ToList();
            var gpsentlists = entlists.Select(ent => ent.Where(e => namere.IsMatch(e.Name)).OrderBy(e => e.Name).ToList()).ToList();

            endname = gpsentlists.Where(e => e.Count >= 1 && e.Any(g => g.IsPlayer == true)).Select(e => e.Last().Name).OrderByDescending(e => e).FirstOrDefault();

            var entsByFace =
                Faces
                    .Select(f => GetFace(f))
                    .ToDictionary(
                        f => f,
                        f => gpsentlists
                                .Select(glist =>
                                    glist.Select(g => g.Project(1024, 1024, planetPos, planetRot, f, rotate45))
                                         .Where(e => e != null)
                                         .ToList()
                                )
                                .ToList()
                    );

            var latLonMult = 1024 * 360.0 / Math.PI / 90;
            maxMercatorLon *= Math.PI / 180.0;
            minMercatorLon *= Math.PI / 180.0;

            entsByFace[CubeFace.Mercator] =
                gpsentlists
                    .Select(glist =>
                        glist.Select(g => g.ProjectMercator(1024, planetPos, planetRot, minMercatorLon, maxMercatorLon, latLonMult, latLonMult, northIsYMinus))
                             .Where(e => e != null)
                             .ToList()
                    )
                    .ToList();

            return entsByFace;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (CubeFace face, Vector3D projected) Project(Vector3D coords, int mult, int maxval, bool rotate45)
        {
            if (coords.Length < maxval / mult)
            {
                return (CubeFace.None, Vector3D.NaN);
            }

            if (rotate45)
            {
                var invsqrt2 = Math.Sqrt(0.5);
                coords = new Vector3D((coords.X + coords.Z) * invsqrt2, coords.Y, (coords.Z - coords.X) * invsqrt2);
            }

            var xzmax = Math.Max(Math.Abs(coords.X), Math.Abs(coords.Z));
            CubeFace face;
            double v;
            double x;
            double y;

            if (coords.Y > xzmax)
            {
                face = CubeFace.Up;
                v = coords.Y;
                x = -coords.X;
                y = -coords.Z;
            }
            else if (coords.Y < -xzmax)
            {
                face = CubeFace.Down;
                v = -coords.Y;
                x = coords.X;
                y = -coords.Z;
            }
            else if (Math.Abs(coords.X) > Math.Abs(coords.Z))
            {
                if (coords.X > 0)
                {
                    face = CubeFace.Left;
                    v = coords.X;
                    x = -coords.Z;
                    y = -coords.Y;
                }
                else
                {
                    face = CubeFace.Right;
                    v = -coords.X;
                    x = coords.Z;
                    y = -coords.Y;
                }
            }
            else
            {
                if (coords.Z > 0)
                {
                    face = CubeFace.Back;
                    v = coords.Z;
                    x = coords.X;
                    y = -coords.Y;
                }
                else
                {
                    face = CubeFace.Front;
                    v = -coords.Z;
                    x = -coords.X;
                    y = -coords.Y;
                }
            }

            double div = mult / v;
            x *= div;
            y *= div;

            if (Math.Abs(x) > mult)
            {
                double mult2 = mult / Math.Abs(x);
                x = mult * (2 - mult2) * Math.Sign(x);
                y *= mult2;
            }

            if (Math.Abs(y) > mult)
            {
                double mult2 = mult / Math.Abs(y);
                x *= mult2;
                y = mult * (2 - mult2) * Math.Sign(y);
            }

            return (face, new Vector3D(x, y, mult));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3D ProjectFace(CubeFace face, double x, double y, int mult, int maxval)
        {
            switch (face)
            {
                case CubeFace.Up:
                    return new Vector3D(-x, mult, -y);
                case CubeFace.Down:
                    return new Vector3D(x, -mult, -y);
                case CubeFace.Left:
                    return new Vector3D(mult, -y, -x);
                case CubeFace.Right:
                    return new Vector3D(-mult, -y, x);
                case CubeFace.Front:
                    return new Vector3D(-x, -y, -mult);
                case CubeFace.Back:
                    return new Vector3D(x, -y, mult);
                default:
                    throw new InvalidOperationException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dictionary<string, Map> RotateMap45(Dictionary<string, Map> maps)
        {
            int w = maps["front"].Heights.Length - 2;
            int mult = w / 2;
            var projected = new Dictionary<string, Map>();

            foreach (var facename in maps.Keys)
            {
                var face = GetFace(facename);
                var map = new Map
                {
                    Heights = new ushort[w + 2][],
                    Materials = new MapMaterial[w + 2][],
                    Name = facename,
                    Face = face
                };

                map.Heights[0] = new ushort[w + 2];
                map.Heights[w + 1] = new ushort[w + 2];
                map.Materials[0] = new MapMaterial[w + 2];
                map.Materials[w + 1] = new MapMaterial[w + 2];

                for (int i = 0; i < w; i++)
                {
                    var hrow = map.Heights[i + 1] = new ushort[w + 2];
                    var mrow = map.Materials[i + 1] = new MapMaterial[w + 2];

                    for (int j = 0; j < w; j++)
                    {
                        var point = ProjectFace(face, j + 0.5 - mult, i + 0.5 - mult, mult, mult);
                        var (pface, ppoint) = Project(point, mult, mult, true);
                        var x = ppoint.X + mult + 0.5;
                        var xi = (int)x;
                        var xf = x - xi;
                        var y = ppoint.Y + mult + 0.5;
                        var yi = (int)y;
                        var yf = y - yi;
                        var pmap = maps[pface.ToString().ToLower()];
                        var hgt = (pmap.Heights[yi + 0][xi + 0] * (1 - xf) + pmap.Heights[yi + 0][xi + 1] * xf) * (1 - yf)
                                + (pmap.Heights[yi + 1][xi + 0] * (1 - xf) + pmap.Heights[yi + 1][xi + 1] * xf) * yf;
                        var mat = pmap.Materials[(int)(x + 0.5)][(int)(y + 0.5)];
                        hrow[j + 1] = (ushort)hgt;
                        mrow[j + 1] = mat;
                    }
                }

                projected[facename] = map;
            }

            return projected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyMapEdges(Dictionary<string, Map> maps)
        {
            int w = maps["front"].Heights.Length - 2;

            maps["front"].SetEdge(0, 1, 0, 1, maps["left"], w, 1, 0, 1);
            maps["left"].SetEdge(w + 1, 1, 0, 1, maps["front"], 1, 1, 0, 1);
            maps["left"].SetEdge(0, 1, 0, 1, maps["back"], w, 1, 0, 1);
            maps["back"].SetEdge(w + 1, 1, 0, 1, maps["left"], 1, 1, 0, 1);
            maps["back"].SetEdge(0, 1, 0, 1, maps["right"], w, 1, 0, 1);
            maps["right"].SetEdge(w + 1, 1, 0, 1, maps["back"], 1, 1, 0, 1);
            maps["right"].SetEdge(0, 1, 0, 1, maps["front"], w, 1, 0, 1);
            maps["front"].SetEdge(w + 1, 1, 0, 1, maps["right"], 1, 1, 0, 1);
            maps["up"].SetEdge(1, w + 1, 1, 0, maps["front"], 1, 1, 1, 0);
            maps["up"].SetEdge(0, 1, 0, 1, maps["left"], 1, 1, 1, 0);
            maps["up"].SetEdge(w, 0, -1, 0, maps["back"], 1, 1, 1, 0);
            maps["up"].SetEdge(w + 1, w, 0, -1, maps["right"], 1, 1, 1, 0);
            maps["front"].SetEdge(1, 0, 1, 0, maps["up"], 1, w, 1, 0);
            maps["left"].SetEdge(1, 0, 1, 0, maps["up"], 1, 1, 0, 1);
            maps["back"].SetEdge(1, 0, 1, 0, maps["up"], w, 1, -1, 0);
            maps["right"].SetEdge(1, 0, 1, 0, maps["up"], w, w, 0, -1);
            maps["down"].SetEdge(1, 0, 1, 0, maps["back"], 1, w, 1, 0);
            maps["down"].SetEdge(0, w, 0, -1, maps["right"], 1, w, 1, 0);
            maps["down"].SetEdge(w, w + 1, -1, 0, maps["front"], 1, w, 1, 0);
            maps["down"].SetEdge(w + 1, 1, 0, 1, maps["left"], 1, w, 1, 0);
            maps["back"].SetEdge(1, w + 1, 1, 0, maps["down"], 1, 1, 1, 0);
            maps["right"].SetEdge(1, w + 1, 1, 0, maps["down"], 1, w, 0, -1);
            maps["front"].SetEdge(1, w + 1, 1, 0, maps["down"], w, w, -1, 0);
            maps["left"].SetEdge(1, w + 1, 1, 0, maps["down"], w, 1, 0, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3D Project(Vector3D coords, int mult, int maxval, CubeFace face, bool rotate45)
        {
            if (rotate45)
            {
                var invsqrt2 = Math.Sqrt(0.5);
                coords = new Vector3D((coords.X - coords.Z) * invsqrt2, coords.Y, (coords.Z + coords.X) * invsqrt2);
            }

            double v = 0;
            double x = 0;
            double y = 0;

            switch (face)
            {
                case CubeFace.Up:
                    v = coords.Y;
                    x = -coords.X;
                    y = -coords.Z;
                    break;
                case CubeFace.Down:
                    v = -coords.Y;
                    x = coords.X;
                    y = -coords.Z;
                    break;
                case CubeFace.Left:
                    v = coords.X;
                    x = -coords.Z;
                    y = -coords.Y;
                    break;
                case CubeFace.Right:
                    v = -coords.X;
                    x = coords.Z;
                    y = -coords.Y;
                    break;
                case CubeFace.Front:
                    v = -coords.Z;
                    x = -coords.X;
                    y = -coords.Y;
                    break;
                case CubeFace.Back:
                    v = coords.Z;
                    x = coords.X;
                    y = -coords.Y;
                    break;
            }

            if (v > maxval / mult)
            {
                double div = mult / v;
                x *= div;
                y *= div;

                if (Math.Abs(x) > mult)
                {
                    double mult2 = mult / Math.Abs(x);
                    x = mult * (2 - mult2) * Math.Sign(x);
                    y *= mult2;
                }

                if (Math.Abs(y) > mult)
                {
                    double mult2 = mult / Math.Abs(y);
                    x *= mult2;
                    y = mult * (2 - mult2) * Math.Sign(y);
                }

                return new Vector3D(x, y, mult);
            }
            else
            {
                return Vector3D.NaN;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3D ProjectMercator(int mult, Vector3D coords, double minMercatorLon, double maxMercatorLon, double latMult, double lonMult, bool northIsYMinus)
        {
            if (coords.X == 0 && coords.Z == 0)
            {
                return Vector3D.NaN;
            }

            var y = coords.Y / Math.Sqrt(coords.X * coords.X + coords.Z * coords.Z);
            var lon = Math.Atan2(-coords.X, coords.Z) - (minMercatorLon + maxMercatorLon) / 2;

            if (northIsYMinus)
            {
                lon = -lon;
                y = -y;
            }

            while (lon < -Math.PI)
            {
                lon += Math.PI * 2;
            }

            while (lon >= Math.PI)
            {
                lon -= Math.PI * 2;
            }

            return new Vector3D(lon * lonMult, y * latMult, mult);
        }

        private static Image<Argb32> CreateMercatorContourMap(Dictionary<string, Map> maps, SEMapOptions options)
        {
            var map = Map.CreateMercatorMap(maps, options.MinMercatorLongitude, options.MaxMercatorLongitude, options.NorthIsYMinus);
            return map.CreateContourMap(options);
        }

        public static Dictionary<CubeFace, Image<Argb32>> GetContourMaps(SEMapOptions options)
        {
            var planetdir = options.PlanetDirectory;
            var maps = Faces.ToDictionary(f => f, f => Map.Load(planetdir, f));

            if (options.Rotate45)
            {
                CopyMapEdges(maps);
                maps = RotateMap45(maps);
            }

            CopyMapEdges(maps);

            var contourmaps = maps.ToDictionary(kvp => GetFace(kvp.Key), kvp => kvp.Value.CreateContourMap(options));

            contourmaps[CubeFace.Mercator] = CreateMercatorContourMap(maps, options);
            return contourmaps;
        }

        public static Image<Argb32> CreateTileMap(Dictionary<CubeFace, Image<Argb32>> maps, CubeFace[][] tiles, Dictionary<CubeFace, Bounds> mapbounds, bool cropmap, bool croptexture, int texturesize)
        {
            var tilewidth = maps.Values.Max(v => v.Width);
            var tileheight = maps.Values.Max(v => v.Height);
            var xtiles = tiles.Max(t => t.Length);
            var ytiles = tiles.Length;
            var bounds = new Bounds(new RectangleF(0, 0, tilewidth * xtiles, tileheight * ytiles));
            var margin = 64;

            for (int y = 0; y < tiles.Length; y++)
            {
                var tilestrip = tiles[y];
                for (int x = 0; x < tilestrip.Length; x++)
                {
                    var tile = tilestrip[x];

                    if (mapbounds.TryGetValue(tile, out var tilebounds) && tilebounds.IsValid)
                    {
                        bounds.AddRectangle(tilebounds.GetBounds(), new PointF(x * tilewidth, y * tileheight));
                    }
                }
            }

            var boundsrect = new Rectangle(0, 0, tilewidth * xtiles, tileheight * ytiles);

            if (cropmap && bounds.IsValid)
            {
                var rect = bounds.GetBounds();
                boundsrect = new Rectangle((int)Math.Floor(rect.X - margin), (int)Math.Floor(rect.Y - margin), (int)Math.Ceiling(rect.Width + margin * 2), (int)Math.Ceiling(rect.Height + margin * 2));
            }
            else if (croptexture && bounds.IsValid)
            {
                var rect = bounds.GetBounds();
                var texwidth = (int)Math.Floor((rect.Width + texturesize - 1) / texturesize);
                var texheight = (int)Math.Floor((rect.Height + texturesize - 1) / texturesize);
                var xmargin = (int)Math.Floor((texwidth * texturesize - rect.Width) / 2);
                var ymargin = (int)Math.Floor((texheight * texturesize - rect.Height) / 2);
                boundsrect = new Rectangle((int)Math.Floor(rect.X - xmargin), (int)Math.Floor(rect.Y - ymargin), texwidth * texturesize, texheight * texturesize);
            }

            var bmp = new Image<Argb32>(boundsrect.Width, boundsrect.Height);

            bmp.Mutate(g =>
            {
                for (int y = 0; y < tiles.Length; y++)
                {
                    var tilestrip = tiles[y];

                    for (int x = 0; x < tilestrip.Length; x++)
                    {
                        var tile = tilestrip[x];

                        if (maps.TryGetValue(tile, out var tilebmp))
                        {
                            g.DrawImage(tilebmp, new Point(tilewidth * x - boundsrect.X, tileheight * y - boundsrect.Y), 1.0f);
                        }
                    }
                }
            });

            return bmp;
        }

        public static void SaveBitmap(Image bmp, string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            bmp.Save(filename);
        }

        public static void SaveTextures(Image bmp, string basename, string extension, int texturesize)
        {
            for (int x = 0; x < bmp.Width / texturesize; x++)
            {
                for (int y = 0; y < bmp.Height / texturesize; y++)
                {
                    var filename = $"{basename}+{x + 1}+{y + 1}.{extension}";

                    using var texbmp = new Image<Argb32>(texturesize, texturesize);

                    texbmp.Mutate(g =>
                    {
                        var destrect = new Rectangle(0, 0, texturesize, texturesize);
                        var srcrect = new Rectangle(x * texturesize, y * texturesize, texturesize, texturesize);
                        g.DrawImage(bmp, Point.Empty, srcrect, 1.0f);
                    });

                    SaveBitmap(texbmp, filename);
                }
            }
        }

        public static void SaveMaps(Dictionary<CubeFace, Image<Argb32>> contourmaps, Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, SEMapOptions opts, string[] segments, string outdir, string endname)
        {
            Dictionary<CubeFace, Image<Argb32>> maps = new Dictionary<CubeFace, Image<Argb32>>();
            Dictionary<CubeFace, Image<Argb32>> basemaps = new Dictionary<CubeFace, Image<Argb32>>();
            Dictionary<CubeFace, Image<Argb32>> ovlmaps = new Dictionary<CubeFace, Image<Argb32>>();
            Image<Argb32> tilebmp = null;
            Image<Argb32> basetilebmp = null;
            Image<Argb32> ovltilebmp = null;

            try
            {
                var gpsbounds = new Dictionary<CubeFace, Bounds>();

                foreach (var kvp in contourmaps)
                {
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Processing face {kvp.Key} for {System.IO.Path.GetFileName(outdir)}");

                    var bmp = kvp.Value.Clone();

                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} base map cloned for {System.IO.Path.GetFileName(outdir)}");

                    var basemap = bmp;
                    basemaps[kvp.Key] = bmp;
                    //bmp.RotateFlip(opts.FaceRotations[kvp.Key]);
                    var mapbounds = new Bounds(new RectangleF(0, 0, bmp.Width, bmp.Height));

                    var griddrawer = new MapDrawer(bmp, new List<ProjectedGpsEntry>(), opts.FaceRotations[kvp.Key], kvp.Key, segments, opts.IncludeAuxTravels, opts.MinMercatorLongitude, opts.MaxMercatorLongitude);
                    griddrawer.Open(Fonts);
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} map drawer opened for {System.IO.Path.GetFileName(outdir)}");
                    griddrawer.DrawEdges();
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} edges drawn for {System.IO.Path.GetFileName(outdir)}");
                    griddrawer.DrawLatLonLines();
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} lat/lon lines for {System.IO.Path.GetFileName(outdir)}");

                    var ovlbmp = new Image<Argb32>(bmp.Width, bmp.Height);
                    ovlmaps[kvp.Key] = ovlbmp;
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} overlay map created for {System.IO.Path.GetFileName(outdir)}");

                    var drawers = new List<MapDrawer>();

                    foreach (var gpsents in gpsentlists[kvp.Key].OrderBy(e => e.FirstOrDefault()?.IsPlayer))
                    {
                        if (gpsents.Count >= 2)
                        {
                            var drawer = new MapDrawer(ovlbmp, gpsents, opts.FaceRotations[kvp.Key], kvp.Key, segments, opts.IncludeAuxTravels, opts.MinMercatorLongitude, opts.MaxMercatorLongitude);
                            drawers.Add(drawer);
                            drawer.Open(Fonts);
                            drawer.DrawLatLonLines();
                        }
                    }

                    var textpaths = new List<(Brush TextBrush, Pen OutlinePen, IPathCollection Paths)>();

                    foreach (var drawer in drawers)
                    {
                        textpaths.AddRange(drawer.GetPOITextPaths().Where(e => e != default));
                    }

                    ovlbmp.Mutate(g =>
                    {
                        foreach (var (textBrush, outlinePen, path) in textpaths)
                        {
                            g.Draw(outlinePen, path);
                        }
                    });

                    foreach (var drawer in drawers)
                    {
                        drawer.DrawPOIs();
                    }

                    foreach (var drawer in drawers)
                    {
                        drawer.DrawPath();
                    }

                    foreach (var (textBrush, outlinePen, path) in textpaths)
                    {
                        ovlbmp.Mutate(g =>
                        {
                            g.Fill(textBrush, path);
                        });

                        if (!opts.CropEnd)
                        {
                            var bounds = path.Bounds;

                            bounds.Inflate(outlinePen.StrokeWidth, outlinePen.StrokeWidth);

                            if (bounds.Left < bmp.Width && bounds.Right >= 0 && bounds.Top < bmp.Height && bounds.Bottom >= 0)
                            {
                                mapbounds.AddRectangle(bounds);
                            }
                        }
                    }

                    bmp = bmp.Clone(p => p.DrawImage(ovlbmp, 1.0f));
                    maps[kvp.Key] = bmp;
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} map cloned and overlayed for {System.IO.Path.GetFileName(outdir)}");

                    if (!opts.CropEnd)
                    {
                        foreach (var drawer in drawers)
                        {
                            var bounds = drawer.GetBounds();

                            if (bounds?.Left < bmp.Width && bounds?.Right >= 0 && bounds?.Top < bmp.Height && bounds?.Bottom >= 0)
                            {
                                mapbounds.AddRectangle(bounds);
                            }
                        }
                    }

                    if (opts.CropEnd)
                    {
                        var endgps = gpsentlists[kvp.Key].Select(e => e.FirstOrDefault(g => g.Name == endname)).FirstOrDefault();

                        if (endgps != null)
                        {
                            endgps = endgps.RotateFlip2D(opts.FaceRotations[kvp.Key]);
                            var x = (float)(endgps.X + bmp.Width / 2);
                            var y = (float)(endgps.Y + bmp.Height / 2);

                            if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                            {
                                mapbounds.AddRectangle(x - 1, y - 1, 2, 2);
                            }
                        }
                    }

                    mapbounds.Clamp(0, 0, bmp.Width, bmp.Height);

                    gpsbounds[kvp.Key] = mapbounds;

                    if (!opts.CropEnd)
                    {
                        SaveBitmap(bmp, System.IO.Path.Combine(outdir, kvp.Key.ToString() + ".png"));
                        Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} map saved for {System.IO.Path.GetFileName(outdir)}");
                    }

                    if (!File.Exists(System.IO.Path.Combine(outdir, kvp.Key.ToString() + "_base.png")))
                    {
                        SaveBitmap(basemap, System.IO.Path.Combine(outdir, kvp.Key.ToString() + "_base.png"));
                        Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} base map saved for {System.IO.Path.GetFileName(outdir)}");
                    }

                    SaveBitmap(ovlbmp, System.IO.Path.Combine(outdir, kvp.Key.ToString() + "_overlay.png"));
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Face {kvp.Key} overlay map saved for {System.IO.Path.GetFileName(outdir)}");
                }

                ovltilebmp = MapUtils.CreateTileMap(ovlmaps, opts.TileFaces, gpsbounds, false, false, segments.Length != 1 ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                SaveBitmap(ovltilebmp, System.IO.Path.Combine(outdir, "tilemap_overlay.png"));
                Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Tile overlay map saved for {System.IO.Path.GetFileName(outdir)}");

                if (!File.Exists(System.IO.Path.Combine(outdir, "tilemap_base.png")))
                {
                    basetilebmp = MapUtils.CreateTileMap(basemaps, opts.TileFaces, gpsbounds, false, false, segments.Length != 1 ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                    SaveBitmap(basetilebmp, System.IO.Path.Combine(outdir, "tilemap_base.png"));
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Tile base map saved for {System.IO.Path.GetFileName(outdir)}");
                }

                if (opts.CropEnd)
                {
                    tilebmp = MapUtils.CreateTileMap(maps, opts.TileFaces, gpsbounds, false, opts.CropTexture, opts.EndTextureSize);
                    SaveBitmap(tilebmp, System.IO.Path.Combine(outdir, "endmap.png"));
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: End map saved for {System.IO.Path.GetFileName(outdir)}");
                }
                else
                {
                    tilebmp = MapUtils.CreateTileMap(maps, opts.TileFaces, gpsbounds, opts.CropTileMap, opts.CropTexture, segments.Length != 1 ? opts.FullMapTextureSize : opts.EpisodeTextureSize);

                    SaveBitmap(tilebmp, System.IO.Path.Combine(outdir, "tilemap.png"));
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Tile map saved for {System.IO.Path.GetFileName(outdir)}");

                    if (opts.CropTexture)
                    {
                        SaveTextures(tilebmp, System.IO.Path.Combine(outdir, "texture"), "png", segments.Length != 1 ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                        Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Textures saved for {System.IO.Path.GetFileName(outdir)}");
                    }
                }
            }
            finally
            {
                tilebmp?.Dispose();
                basetilebmp?.Dispose();
                ovltilebmp?.Dispose();

                foreach (var kvp in maps)
                {
                    kvp.Value.Dispose();
                }

                foreach (var kvp in basemaps)
                {
                    kvp.Value.Dispose();
                }

                foreach (var kvp in ovlmaps)
                {
                    kvp.Value.Dispose();
                }
            }
        }
    }
}
