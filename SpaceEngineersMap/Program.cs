using System;
using System.Collections.Generic;
using System.Drawing;
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

        static void DrawGPS(Bitmap bmp, List<GpsEntry> entries)
        {
            if (entries.Count >= 2)
            {
                Graphics g = null;
                Pen travelpen = null;
                Pen altpen = null;
                Brush poibrush = null;
                Brush poi2brush = null;

                try
                {
                    g = Graphics.FromImage(bmp);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    travelpen = new Pen(Color.DarkBlue, 2.0f);
                    altpen = new Pen(Color.DarkRed, 2.0f);
                    poibrush = new SolidBrush(Color.DarkViolet);
                    poi2brush = new SolidBrush(Color.DarkGreen);
                    var ent = entries[0];
                    var point = new PointF((float)(ent.X + 1024), (float)(ent.Y + 1024));
                    var altpoint = point;

                    if (ent.Name.EndsWith("%"))
                    {
                        g.FillEllipse(poibrush, point.X - 3.5f, point.Y - 3.5f, 7, 7);
                    }

                    for (int i = 1; i < entries.Count; i++)
                    {
                        ent = entries[i];
                        var nextpoint = new PointF((float)(ent.X + 1024), (float)(ent.Y + 1024));
                        if (ent.Name.EndsWith("$"))
                        {
                            g.FillEllipse(poi2brush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        }
                        else if (ent.Name.EndsWith("%"))
                        {
                            g.FillEllipse(poibrush, nextpoint.X - 3.5f, nextpoint.Y - 3.5f, 7, 7);
                        }
                    }

                    for (int i = 1; i < entries.Count; i++)
                    {
                        ent = entries[i];
                        var nextpoint = new PointF((float)(ent.X + 1024), (float)(ent.Y + 1024));
                        if (ent.Name.EndsWith("@"))
                        {
                            g.DrawLine(altpen, altpoint, nextpoint);
                            altpoint = nextpoint;
                        }
                        else if (ent.Name.EndsWith("^"))
                        {
                            altpoint = point = nextpoint;
                        }
                        else if (!ent.Name.EndsWith("$") && !ent.Name.EndsWith("#"))
                        {
                            g.DrawLine(travelpen, point, nextpoint);
                            altpoint = point = nextpoint;
                        }
                    }
                }
                finally
                {
                    travelpen?.Dispose();
                    altpen?.Dispose();
                    poibrush?.Dispose();
                    poi2brush?.Dispose();
                    g?.Dispose();
                }
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
            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            string savedir = null;
            string contentdir = null;
            string outputdir = null;
            bool showhelp = false;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--savedir")
                {
                    savedir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--contentdir")
                {
                    contentdir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--outputdir")
                {
                    outputdir = args[i + 1];
                    i++;
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

            var gpsentlists = ProcessBookmarks(savedir);
            var contourmaps = ProcessPlanetDefs(contentdir);

            foreach (var kvp in contourmaps)
            {
                foreach (var gpsents in gpsentlists[kvp.Key])
                {
                    DrawGPS(kvp.Value, gpsents);
                }
                kvp.Value.Save(Path.Combine(outputdir, kvp.Key + ".png"));
            }
        }
    }
}
