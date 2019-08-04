using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    class Program
    {
        private static string SEPlanetDataDir = "Data/PlanetDataFiles/EarthLike";

        private static string[] Faces = { "down", "back", "left", "front", "right", "up" };

        static Dictionary<string, List<List<GpsEntry>>> ProcessBookmarks(string savedir)
        {
            var namere = new Regex(@"^P\d\d\.\d\d\.\d\d\.\d\d");
            var xdoc = XDocument.Load(Path.Combine(savedir, "Sandbox.sbc"));
            var items = xdoc.Root.Element("Gps").Element("dictionary").Elements("item").ToList();
            var entlists = items.Select(i => i.Element("Value").Element("Entries").Elements("Entry").Select(e => GpsEntry.FromXML(e)).ToList()).ToList();
            var gpesentlists = entlists.Select(ent => ent.Where(e => namere.IsMatch(e.Name)).OrderBy(e => e.Name).ToList()).ToList();

            return Faces.ToDictionary(f => f, f => gpesentlists.Select(glist => glist.Select(g => g.Project(1024, 1024, f)).Where(e => e != null).ToList()).ToList());
        }

        static RectangleF? DrawGPS(Bitmap bmp, List<GpsEntry> entries, RotateFlipType rotation, string prefix)
        {
            if (entries.Count >= 2)
            {
                Graphics g = null;
                Pen travelpen = null;
                Pen altpen = null;
                Brush poibrush = null;
                Brush poi2brush = null;
                Font textfont = null;
                Brush textbrush = null;
                Pen textoutline = null;
                Region boundsregion = null;

                try
                {
                    g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    travelpen = new Pen(Color.DarkBlue, 2.0f);
                    altpen = new Pen(Color.DarkRed, 2.0f);
                    poibrush = new SolidBrush(Color.DarkViolet);
                    poi2brush = new SolidBrush(Color.DarkGreen);
                    textfont = new Font(FontFamily.GenericSansSerif, 12.0f, GraphicsUnit.Pixel);
                    textbrush = new SolidBrush(Color.Black);
                    textoutline = new Pen(Color.White, 2.0f);
                    boundsregion = new Region();
                    boundsregion.MakeEmpty();

                    var width = bmp.Width;
                    var height = bmp.Height;

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

                    var ent0 = entries[0].RotateFlip2D(rotation);
                    var point = new PointF((float)(ent0.X + width / 2), (float)(ent0.Y + height / 2));
                    var altpoint = point;

                    for (int i = 1; i < entries.Count; i++)
                    {
                        var ent = entries[i].RotateFlip2D(rotation);
                        var nextpoint = new PointF((float)(ent.X + width / 2), (float)(ent.Y + height / 2));
                        if (ent.Name.EndsWith("@"))
                        {
                            if (ent.Name.StartsWith(prefix))
                            {
                                g.DrawLine(altpen, altpoint, nextpoint);
                                using (var path = new GraphicsPath())
                                {
                                    path.AddLine(altpoint, nextpoint);
                                    boundsregion.Union(path);
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
                            if (ent.Name.StartsWith(prefix))
                            {
                                g.DrawLine(travelpen, point, nextpoint);
                                using (var path = new GraphicsPath())
                                {
                                    path.AddLine(point, nextpoint);
                                    boundsregion.Union(path);
                                }
                            }
                            altpoint = point = nextpoint;
                        }
                    }

                    for (int i = 0; i < entries.Count; i++)
                    {
                        var ent = entries[i].RotateFlip2D(rotation);
                        if (!string.IsNullOrWhiteSpace(ent.Description) && ent.Description != "Current position")
                        {
                            var nextpoint = new PointF((float)(ent.X + width / 2), (float)(ent.Y + height / 2));
                            if (ent.Name.StartsWith(prefix) || ent.Name.Contains("-" + prefix))
                            {
                                var textbounds = TextDrawing.DrawText(g, ent.Description, nextpoint, textfont, textbrush, textoutline);
                                if (textbounds is RectangleF rect)
                                {
                                    boundsregion.Union(rect);
                                }
                            }
                        }
                    }

                    boundsregion.Intersect(new RectangleF(0, 0, width, height));

                    if (boundsregion.IsEmpty(g))
                    {
                        return null;
                    }
                    else
                    {
                        return boundsregion.GetBounds(g);
                    }
                }
                finally
                {
                    boundsregion?.Dispose();
                    textfont?.Dispose();
                    textbrush?.Dispose();
                    textoutline?.Dispose();
                    travelpen?.Dispose();
                    altpen?.Dispose();
                    poibrush?.Dispose();
                    poi2brush?.Dispose();
                    g?.Dispose();
                }
            }
            else
            {
                return null;
            }
        }

        static Dictionary<string, Bitmap> ProcessPlanetDefs(string contentdir)
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

            var contourmaps = maps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.CreateContourMap());
            return contourmaps;
        }

        static Bitmap CreateTileMap(Dictionary<string, Bitmap> maps, string[][] tiles, Dictionary<string, Bounds> mapbounds, bool cropmap)
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

        static void SaveBitmap(Bitmap bmp, string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            bmp.Save(filename);
        }

        static string[] GetSegmentPrefixes(Dictionary<string, List<List<GpsEntry>>> gpsentries)
        {
            var entries = gpsentries.Values.SelectMany(l1 => l1.SelectMany(l2 => l2)).ToList();
            return new[] { "" }.Concat(entries.Select(e => e.Name.Substring(0, 3)).Distinct()).ToArray();
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: SpaceEngineersMap {options} [savedirectory]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("--savedir <filename>");
            Console.WriteLine("    Saved Game Directory Path");
            Console.WriteLine("    Default: working directory");
            Console.WriteLine("--contentdir <path>");
            Console.WriteLine("    Path to Space Engineers content directory");
            Console.WriteLine("    Required");
            Console.WriteLine("--outdir <path>");
            Console.WriteLine("    Directory in which to output maps");
            Console.WriteLine("    Default: working directory");
            Console.WriteLine("--rotate <map>:{cw|ccw|90|180|270}");
            Console.WriteLine("    Rotate maps selected direction (clockwise (90) / counter-clockwise (270) / 180 degrees)");
            Console.WriteLine("    Affects text draw rotation");
            Console.WriteLine("    Defaults: up:90 down:270 left:180 front:180 right:180 back:180");
            Console.WriteLine("--tile <topleft>[:<right>[:...]][,<down>[:...][,...]]");
            Console.WriteLine("    Tile map top to bottom, left to right");
            Console.WriteLine("    Use blank ('') to signify an empty tile");
            Console.WriteLine("    Default: :down::,back:right:front:left,:up::");

            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            string savedir = null;
            string contentdir = null;
            string outputdir = null;
            bool showhelp = false;
            bool croptilemap = false;
            string[][] tileparts = new[] {
                new[] { "", "down", "", "" },
                new[] { "back", "right", "front", "left" },
                new[] { "", "up", "", "" }
            };
            Dictionary<string, RotateFlipType> rotations = new Dictionary<string, RotateFlipType>
            {
                ["up"] = RotateFlipType.Rotate90FlipNone,
                ["down"] = RotateFlipType.Rotate270FlipNone,
                ["left"] = RotateFlipType.Rotate180FlipNone,
                ["front"] = RotateFlipType.Rotate180FlipNone,
                ["right"] = RotateFlipType.Rotate180FlipNone,
                ["back"] = RotateFlipType.Rotate180FlipNone
            };

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--savedir" && i < args.Length - 1)
                {
                    savedir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--contentdir" && i < args.Length - 1)
                {
                    contentdir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--outdir" && i < args.Length - 1)
                {
                    outputdir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--tile" && i < args.Length - 1)
                {
                    tileparts = args[i + 1].Split(',').Select(p => p.Split(':')).ToArray();
                    i++;
                }
                else if (args[i] == "--rotate" && i < args.Length - 1)
                {
                    var rot = args[i + 1].Split(':');
                    switch (rot[1])
                    {
                        case "cw":
                        case "90":
                            rotations[rot[0]] = RotateFlipType.Rotate90FlipNone;
                            break;
                        case "ccw":
                        case "270":
                            rotations[rot[0]] = RotateFlipType.Rotate270FlipNone;
                            break;
                        case "180":
                            rotations[rot[0]] = RotateFlipType.Rotate180FlipNone;
                            break;
                    }
                }
                else if (args[i] == "--crop")
                {
                    croptilemap = true;
                }
                else if (args[i] == "--help" || args[i] == "/?")
                {
                    showhelp = true;
                    break;
                }
                else if (Directory.Exists(args[i]))
                {
                    savedir = args[i];
                }
                else if (File.Exists(args[i]))
                {
                    savedir = Path.GetDirectoryName(args[i]);
                }
                else
                {
                    Console.WriteLine($"Unrecognised option {args[i]}");
                    showhelp = true;
                    break;
                }
            }

            if (savedir == null)
            {
                savedir = Environment.CurrentDirectory;
            }

            if (outputdir == null)
            {
                outputdir = Environment.CurrentDirectory;
            }

            if (showhelp || contentdir == null)
            {
                ShowHelp();
                return;
            }

            Console.WriteLine("Getting GPS entries");
            var gpsentlists = ProcessBookmarks(savedir);
            var segments = GetSegmentPrefixes(gpsentlists);
            Console.WriteLine("Creating contour maps");
            var contourmaps = ProcessPlanetDefs(contentdir);

            foreach (var segment in segments)
            {
                Console.WriteLine($"Processing segment {segment}");
                string segdir = outputdir;

                if (segment != "")
                {
                    segdir = Path.Combine(segdir, segment);
                }

                if (!Directory.Exists(segdir))
                {
                    Directory.CreateDirectory(segdir);
                }

                Dictionary<string, Bitmap> maps = new Dictionary<string, Bitmap>();
                Bitmap tilebmp = null;

                try
                {
                    var gpsbounds = new Dictionary<string, Bounds>();

                    foreach (var kvp in contourmaps)
                    {
                        var bmp = (Bitmap)kvp.Value.Clone();
                        maps[kvp.Key] = bmp;
                        bmp.RotateFlip(rotations[kvp.Key]);
                        var mapbounds = new Bounds(new RectangleF(0, 0, bmp.Width, bmp.Height));

                        foreach (var gpsents in gpsentlists[kvp.Key])
                        {
                            var gpsboundsrect = DrawGPS(bmp, gpsents, rotations[kvp.Key], segment);
                            mapbounds.AddRectangle(gpsboundsrect);
                        }
                        gpsbounds[kvp.Key] = mapbounds;
                        SaveBitmap(bmp, Path.Combine(segdir, kvp.Key + ".png"));
                    }

                    tilebmp = CreateTileMap(maps, tileparts, gpsbounds, croptilemap);
                    SaveBitmap(tilebmp, Path.Combine(segdir, "tilemap.png"));
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
}
