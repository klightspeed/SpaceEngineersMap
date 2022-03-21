using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        public static Dictionary<CubeFace, List<List<GpsEntry>>> GetGPSEntries(string savedir, string planetname, bool rotate45, out string endname)
        {
            var (planetPos, planetRot) = GetPlanetPositionAndOrientation(savedir, planetname);
            var namere = new Regex(@"^P\d\d\.\d\d\.\d\d\.\d\d");
            var xdoc = XDocument.Load(Path.Combine(savedir, "Sandbox.sbc"));
            var items = xdoc.Root.Element("Gps").Element("dictionary").Elements("item").ToList();
            var entlists = items.Select(i => i.Element("Value").Element("Entries").Elements("Entry").Select(e => GpsEntry.FromXML(e)).ToList()).ToList();
            var gpsentlists = entlists.Select(ent => ent.Where(e => namere.IsMatch(e.Name)).OrderBy(e => e.Name).ToList()).ToList();

            endname = gpsentlists.Where(e => e.Count >= 1).Select(e => e.Last().Name).FirstOrDefault();

            return Faces.Select(f => GetFace(f)).ToDictionary(f => f, f => gpsentlists.Select(glist => glist.Select(g => g.Project(1024, 1024, planetPos, planetRot, f, rotate45)).Where(e => e != null).ToList()).ToList());
        }

        private static void DrawPOIs(Graphics g, List<GpsEntry> entries, RotateFlipType rotation, Region boundsregion, string prefix, float width, float height, Brush poibrush, Brush poi2brush)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var ent = entries[i].RotateFlip2D(rotation);
                var nextpoint = new PointF((float)(ent.X + width / 2), (float)(ent.Y + height / 2));
                if (ent.Name.StartsWith(prefix) || ent.Name.Contains("-" + prefix))
                {
                    if (ent.Name.EndsWith("$"))
                    {
                        g.FillEllipse(poi2brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        boundsregion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                    }
                    else if (ent.Name.EndsWith("%"))
                    {
                        g.FillEllipse(poibrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        boundsregion.Union(new RectangleF(nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7));
                    }
                }
            }
        }

        public static RectangleF? DrawGPS(Bitmap bmp, List<GpsEntry> entries, RotateFlipType rotation, CubeFace face, string prefix)
        {
            using (var drawer = new MapDrawer(bmp, entries, rotation, face, prefix))
            {
                drawer.Open();
                drawer.DrawEdges();
                drawer.DrawLatLonLines();
                if (entries.Count >= 2)
                {
                    drawer.DrawPOIs();
                    drawer.DrawPath();
                    drawer.DrawPOIText();
                    return drawer.GetBounds();
                }
                else
                {
                    return null;
                }
            }
        }

        private static List<string> GetInstalledModIds(string savedir)
        {
            var modids = new List<string>();
            if (savedir != null)
            {
                var saveconfig = Path.Combine(savedir, "Sandbox_config.sbc");

                if (File.Exists(saveconfig))
                {
                    using (var file = File.Open(saveconfig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var xml = XDocument.Load(file);
                        var root = xml.Root;
                        var mods = root.Element("Mods").Elements("ModItem");

                        foreach (var mod in mods)
                        {
                            var modid = mod.Element("PublishedFileId")?.Value;

                            if (modid != null)
                            {
                                modids.Add(modid);
                            }
                        }
                    }
                }
            }

            return modids;
        }

        private static string FindPlanetDir(string savedir, string contentdir, string workshopdir, string planetname)
        {
            var modids = GetInstalledModIds(savedir);

            foreach (var modid in modids)
            {
                var planetdir = Path.Combine(workshopdir, modid, "Data", "PlanetDataFiles", planetname);
                if (Faces.All(f => File.Exists(Path.Combine(planetdir, $"{f}.png"))))
                {
                    return planetdir;
                }
            }

            return Path.Combine(contentdir, "Data", "PlanetDataFiles", planetname);
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

        public static Dictionary<CubeFace, Bitmap> GetContourMaps(SEMapOptions options)
        {
            var planetdir = FindPlanetDir(options.SaveDirectory, options.ContentDirectory, options.WorkshopDirectory, options.PlanetName);
            var maps = Faces.ToDictionary(f => f, f => Map.Load(planetdir, f));

            if (options.Rotate45)
            {
                CopyMapEdges(maps);
                maps = RotateMap45(maps);
            }

            CopyMapEdges(maps);

            var contourmaps = maps.ToDictionary(kvp => GetFace(kvp.Key), kvp => kvp.Value.CreateContourMap(options));
            return contourmaps;
        }

        public static Bitmap CreateTileMap(Dictionary<CubeFace, Bitmap> maps, CubeFace[][] tiles, Dictionary<CubeFace, Bounds> mapbounds, bool cropmap, bool croptexture, int texturesize)
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

            var bmp = new Bitmap(boundsrect.Width, boundsrect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                for (int y = 0; y < tiles.Length; y++)
                {
                    var tilestrip = tiles[y];
                    for (int x = 0; x < tilestrip.Length; x++)
                    {
                        var tile = tilestrip[x];

                        if (maps.TryGetValue(tile, out var tilebmp))
                        {
                            g.DrawImage(tilebmp, new Point(tilewidth * x - boundsrect.X, tileheight * y - boundsrect.Y));
                        }
                    }
                }
            }

            return bmp;
        }

        public static void SaveBitmap(Bitmap bmp, string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            bmp.Save(filename);
        }

        public static void SaveTextures(Bitmap bmp, string basename, string extension, int texturesize)
        {
            for (int x = 0; x < bmp.Width / texturesize; x++)
            {
                for (int y = 0; y < bmp.Height / texturesize; y++)
                {
                    var filename = $"{basename}+{x + 1}+{y + 1}.{extension}";
                    using (var texbmp = new Bitmap(texturesize, texturesize, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(texbmp))
                        {
                            var destrect = new Rectangle(0, 0, texturesize, texturesize);
                            var srcrect = new Rectangle(x * texturesize, y * texturesize, texturesize, texturesize);
                            g.DrawImage(bmp, destrect, srcrect, GraphicsUnit.Pixel);
                        }
                        SaveBitmap(texbmp, filename);
                    }
                }
            }
        }

        public static void SaveMaps(Dictionary<CubeFace, Bitmap> contourmaps, Dictionary<CubeFace, List<List<GpsEntry>>> gpsentlists, SEMapOptions opts, string segment, string outdir, string endname)
        {
            Dictionary<CubeFace, Bitmap> maps = new Dictionary<CubeFace, Bitmap>();
            Bitmap tilebmp = null;

            try
            {
                var gpsbounds = new Dictionary<CubeFace, Bounds>();

                foreach (var kvp in contourmaps)
                {
                    var bmp = (Bitmap)kvp.Value.Clone();
                    maps[kvp.Key] = bmp;
                    //bmp.RotateFlip(opts.FaceRotations[kvp.Key]);
                    var mapbounds = new Bounds(new RectangleF(0, 0, bmp.Width, bmp.Height));

                    foreach (var gpsents in gpsentlists[kvp.Key])
                    {
                        var gpsboundsrect = MapUtils.DrawGPS(bmp, gpsents, opts.FaceRotations[kvp.Key], kvp.Key, segment);

                        if (!opts.CropEnd)
                        {
                            mapbounds.AddRectangle(gpsboundsrect);
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

                    gpsbounds[kvp.Key] = mapbounds;

                    if (!opts.CropEnd)
                    {
                        SaveBitmap(bmp, Path.Combine(outdir, kvp.Key.ToString() + ".png"));
                    }
                }

                if (opts.CropEnd)
                {
                    tilebmp = MapUtils.CreateTileMap(maps, opts.TileFaces, gpsbounds, false, opts.CropTexture, opts.EndTextureSize);
                    SaveBitmap(tilebmp, Path.Combine(outdir, "endmap.png"));
                }
                else
                {
                    tilebmp = MapUtils.CreateTileMap(maps, opts.TileFaces, gpsbounds, opts.CropTileMap, opts.CropTexture, segment == "" ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                    SaveBitmap(tilebmp, Path.Combine(outdir, "tilemap.png"));

                    if (opts.CropTexture)
                    {
                        SaveTextures(tilebmp, Path.Combine(outdir, "texture"), "png", segment == "" ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                    }
                }
            }
            finally
            {
                tilebmp?.Dispose();
                foreach (var kvp in maps)
                {
                    kvp.Value.Dispose();
                }
            }
        }
    }
}
