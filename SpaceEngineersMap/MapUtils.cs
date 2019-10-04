using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public static class MapUtils
    {
        public const string SEPlanetDataDir = "Data/PlanetDataFiles/EarthLike";

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

        public static Dictionary<CubeFace, List<List<GpsEntry>>> GetGPSEntries(string savedir)
        {
            var namere = new Regex(@"^P\d\d\.\d\d\.\d\d\.\d\d");
            var xdoc = XDocument.Load(Path.Combine(savedir, "Sandbox.sbc"));
            var items = xdoc.Root.Element("Gps").Element("dictionary").Elements("item").ToList();
            var entlists = items.Select(i => i.Element("Value").Element("Entries").Elements("Entry").Select(e => GpsEntry.FromXML(e)).ToList()).ToList();
            var gpesentlists = entlists.Select(ent => ent.Where(e => namere.IsMatch(e.Name)).OrderBy(e => e.Name).ToList()).ToList();

            return Faces.Select(f => GetFace(f)).ToDictionary(f => f, f => gpesentlists.Select(glist => glist.Select(g => g.Project(1024, 1024, f)).Where(e => e != null).ToList()).ToList());
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

        public static Dictionary<CubeFace, Bitmap> GetContourMaps(string contentdir)
        {
            var maps = Faces.ToDictionary(f => f, f => Map.Load(Path.Combine(contentdir, SEPlanetDataDir), f));

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

            var contourmaps = maps.ToDictionary(kvp => GetFace(kvp.Key), kvp => kvp.Value.CreateContourMap());
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

        public static void SaveMaps(Dictionary<CubeFace, Bitmap> contourmaps, Dictionary<CubeFace, List<List<GpsEntry>>> gpsentlists, SEMapOptions opts, string segment, string outdir)
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
                    bmp.RotateFlip(opts.FaceRotations[kvp.Key]);
                    var mapbounds = new Bounds(new RectangleF(0, 0, bmp.Width, bmp.Height));

                    foreach (var gpsents in gpsentlists[kvp.Key])
                    {
                        var gpsboundsrect = MapUtils.DrawGPS(bmp, gpsents, opts.FaceRotations[kvp.Key], kvp.Key, segment);
                        mapbounds.AddRectangle(gpsboundsrect);
                    }
                    gpsbounds[kvp.Key] = mapbounds;
                    SaveBitmap(bmp, Path.Combine(outdir, kvp.Key.ToString() + ".png"));
                }

                tilebmp = MapUtils.CreateTileMap(maps, opts.TileFaces, gpsbounds, opts.CropTileMap, opts.CropTexture, segment == "" ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
                SaveBitmap(tilebmp, Path.Combine(outdir, "tilemap.png"));

                if (opts.CropTexture)
                {
                    SaveTextures(tilebmp, Path.Combine(outdir, "texture"), "png", segment == "" ? opts.FullMapTextureSize : opts.EpisodeTextureSize);
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
