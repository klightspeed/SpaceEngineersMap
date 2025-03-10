using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        static Regex ChapterRE = new Regex(@"P(\d\d\w?)-(\d\d\w?)");

        static (string name, string[] prefixes)[] GetSegmentPrefixes(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentries, string[] chapterparts)
        {
            var entries = gpsentries.Values.SelectMany(l1 => l1.SelectMany(l2 => l2)).ToList();
            var segments = entries.Select(e => e.StartPart).Concat(entries.Select(e => e.EndPart)).OfType<string>().Distinct().ToArray();
            List<(string name, string[] prefixes)> prefixlists =
            [
                ("", Array.Empty<string>()), .. segments.Select(e => (e, new string[] { e }))
            ];

            foreach (var chapter in chapterparts)
            {
                if (ChapterRE.Match(chapter) is Match match && match.Success)
                {
                    prefixlists.Add((chapter, segments.Where(e => e.CompareTo($"P{match.Groups[1].Value}") >= 0 && e.CompareTo($"P{match.Groups[2].Value}") <= 0).ToArray()));
                }
            }
            return prefixlists.ToArray();
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: SpaceEngineersMap {options} [savedirectory]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("--savedir <directory>");
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
            Console.WriteLine("--crop");
            Console.WriteLine("    Crop map to visited area, with padding");
            Console.WriteLine("--croptexture");
            Console.WriteLine("    Crop map to visited area, padded to texture size");
            Console.WriteLine("--texturesize <size>");
            Console.WriteLine("    Set episode map texture size");
            Console.WriteLine("--fullmaptexturesize <size>");
            Console.WriteLine("    Set full map texture size");
            Console.WriteLine("--cropend");
            Console.WriteLine("    Export only the area around the end position");
            Console.WriteLine("--endsize");
            Console.WriteLine("    Size of the exported end area");
            Console.WriteLine("--onsave");
            Console.WriteLine("    Wait for the save game to be updated, and loop while the game is running");
            Console.WriteLine();
        }

        static void SavePOIList(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, string[] segments, string segdir, Vector3D planetPos)
        {
            var gpsents =
                gpsentlists
                    .Values
                    .SelectMany(e => e)
                    .SelectMany(e => e)
                    .Select(e => (e.Name, e.Description))
                    .OrderBy(e => e.Name)
                    .Distinct()
                    .ToList();

            var distance = GetTravelDistance(gpsentlists, segments);
            var flightdistance = GetFlyingDistance(gpsentlists, segments);
            var distoverground = GetDistanceOverGround(gpsentlists, segments, planetPos);
            var elevchange = GetElevationChange(gpsentlists, segments, planetPos);

            using (var logfile = File.Open(Path.Combine(segdir, "Stats.txt"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                using (var logwriter = new StreamWriter(logfile))
                {
                    logwriter.WriteLine($"Distance travelled: {distance / 1000:0.00}km");
                    logwriter.WriteLine($"Distance over ground: {distoverground / 1000:0.00}km");
                    logwriter.WriteLine($"Elevation change: {elevchange / 1000:0.00}km");
                    logwriter.WriteLine($"Distance flying: {flightdistance / 1000:0.00}km");
                }
            }

            using (var logfile = File.Open(Path.Combine(segdir, "POIs.txt"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                using (var logwriter = new StreamWriter(logfile))
                {
                    logwriter.WriteLine($"Distance travelled: {distance / 1000:0.00}km");
                    logwriter.WriteLine($"Distance over ground: {distoverground / 1000:0.00}km");
                    logwriter.WriteLine($"Elevation change: {elevchange / 1000:0.00}km");
                    logwriter.WriteLine($"Distance flying: {flightdistance / 1000:0.00}km");

                    foreach (var (name, desc) in gpsents)
                    {
                        if (!string.IsNullOrWhiteSpace(desc) && desc != "Current position")
                        {
                            if (segments.Length == 0 || segments.Any(p => name.StartsWith(p) || name.Contains("-" + p)))
                            {
                                bool hidepart1 = segments.Length != 0 && !segments.Any(p => name.StartsWith(p));
                                bool hidepart2 = segments.Length != 0 && !segments.Any(p => name.Contains("-" + p));
                                var timestamp = name.TrimEnd('%', '=', '@', '$', '^');
                                var description = desc;

                                if (description.StartsWith("[Bot]"))
                                {
                                    description = description.Substring(5).TrimStart();
                                }

                                var cmdarg = description.Split(new[] { ' ' }, 2);

                                if (cmdarg.Length == 2)
                                {
                                    var sections = cmdarg[1].Split(new[] { " / ", "\n----\n" }, StringSplitOptions.None).Select(e => e.Trim()).ToArray();

                                    if (hidepart1 && !hidepart2 && sections.Length > 1)
                                    {
                                        sections = sections.Skip(1).ToArray();
                                    }
                                    else if (hidepart2 && !hidepart1)
                                    {
                                        sections = new[] { sections[0] };
                                    }

                                    description = string.Join(" / ", sections).Replace("\n", " ").Replace("  ", " ");

                                    logwriter.WriteLine($"[{timestamp}] {description}");
                                }
                            }
                        }
                    }
                }
            }
        }

        static double GetDistance(GpsEntry left, GpsEntry right)
        {
            var dx = right.X - left.X;
            var dy = right.Y - left.Y;
            var dz = right.Z - left.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static double GetDistanceOverGround(GpsEntry left, GpsEntry right, Vector3D planetPos)
        {
            var x1 = left.X - planetPos.X;
            var y1 = left.Y - planetPos.Y;
            var z1 = left.Z - planetPos.Z;
            var x2 = right.X - planetPos.X;
            var y2 = right.Y - planetPos.Y;
            var z2 = right.Z - planetPos.Z;
            var r1 = Math.Sqrt(x1 * x1 + y1 * y1 + z1 * z1);
            var r2 = Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
            var rm = (r1 + r2) / 2;
            var m1 = rm / r1;
            var m2 = rm / r2;
            var dx = x2 * m2 - x1 * m1;
            var dy = y2 * m2 - y1 * m1;
            var dz = z2 * m2 - z1 * m1;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static double GetElevationChange(GpsEntry left, GpsEntry right, Vector3D planetPos)
        {
            var x1 = left.X - planetPos.X;
            var y1 = left.Y - planetPos.Y;
            var z1 = left.Z - planetPos.Z;
            var x2 = right.X - planetPos.X;
            var y2 = right.Y - planetPos.Y;
            var z2 = right.Z - planetPos.Z;
            var r1 = Math.Sqrt(x1 * x1 + y1 * y1 + z1 * z1);
            var r2 = Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
            return Math.Abs(r2 - r1);
        }

        static double GetTravelDistance(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, string[] segments)
        {
            var gpsents =
                gpsentlists
                    .Values
                    .SelectMany(e => e)
                    .SelectMany(e => e)
                    .Select(e => e.OriginalEntry)
                    .Where(e =>
                        e.IsPlayer == true &&
                        !new[] { "$", "=", "@", "~" }.Any(v => e.Name.Contains(v)) &&
                        (segments.Length == 0 || segments.Any(p => e.StartPart == p || e.EndPart == p))
                    )
                    .OrderBy(e => e.Name)
                    .Distinct()
                    .ToList();

            var distance = 0.0;
            GpsEntry lastentry = gpsents.FirstOrDefault();

            for (int i = 1; i < gpsents.Count; i++)
            {
                var entry = gpsents[i];

                if (!entry.Name.Contains("^"))
                {
                    distance += GetDistance(lastentry, entry);
                }

                lastentry = entry;
            }

            return distance;
        }

        static double GetFlyingDistance(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, string[] segments)
        {
            var gpsents =
                gpsentlists
                    .Values
                    .SelectMany(e => e)
                    .SelectMany(e => e)
                    .Select(e => e.OriginalEntry)
                    .Where(e =>
                        e.IsPlayer == true &&
                        !new[] { "$", "=", "@" }.Any(v => e.Name.Contains(v)) &&
                        (segments.Length == 0 || segments.Any(p => e.StartPart == p || e.EndPart == p))
                    )
                    .OrderBy(e => e.Name)
                    .Distinct()
                    .ToList();

            var distance = 0.0;
            GpsEntry lastentry = gpsents.FirstOrDefault();

            for (int i = 1; i < gpsents.Count; i++)
            {
                var entry = gpsents[i];

                if (entry.Name.Contains("~"))
                {
                    distance += GetDistance(lastentry, entry);
                }

                lastentry = entry;
            }

            return distance;
        }

        static double GetDistanceOverGround(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, string[] segments, Vector3D planetPos)
        {
            var gpsents =
                gpsentlists
                    .Values
                    .SelectMany(e => e)
                    .SelectMany(e => e)
                    .Select(e => e.OriginalEntry)
                    .Where(e =>
                        e.IsPlayer == true &&
                        !new[] { "$", "=", "@", "~" }.Any(v => e.Name.Contains(v)) &&
                        (segments.Length == 0 || segments.Any(p => e.StartPart == p || e.EndPart == p))
                    )
                    .OrderBy(e => e.Name)
                    .Distinct()
                    .ToList();

            var distance = 0.0;
            GpsEntry lastentry = gpsents.FirstOrDefault();

            for (int i = 1; i < gpsents.Count; i++)
            {
                var entry = gpsents[i];

                if (!entry.Name.Contains("^"))
                {
                    distance += GetDistanceOverGround(lastentry, entry, planetPos);
                }

                lastentry = entry;
            }

            return distance;
        }

        static double GetElevationChange(Dictionary<CubeFace, List<List<ProjectedGpsEntry>>> gpsentlists, string[] segments, Vector3D planetPos)
        {
            var gpsents =
                gpsentlists
                    .Values
                    .SelectMany(e => e)
                    .SelectMany(e => e)
                    .Select(e => e.OriginalEntry)
                    .Where(e =>
                        e.IsPlayer == true &&
                        !new[] { "$", "=", "@", "~" }.Any(v => e.Name.Contains(v)) &&
                        (segments.Length == 0 || segments.Any(p => e.StartPart == p || e.EndPart == p))
                    )
                    .OrderBy(e => e.Name)
                    .Distinct()
                    .ToList();

            var distance = 0.0;
            GpsEntry lastentry = gpsents.FirstOrDefault();

            for (int i = 1; i < gpsents.Count; i++)
            {
                var entry = gpsents[i];

                if (!entry.Name.Contains("^"))
                {
                    distance += GetElevationChange(lastentry, entry, planetPos);
                }

                lastentry = entry;
            }

            return distance;
        }

        static bool WaitForSave(string path, ref DateTime filesavetime)
        {
            while (true)
            {
                var lastsave = File.GetLastWriteTimeUtc(Path.Combine(path, "Sandbox.sbc"));
                var configsave = File.GetLastWriteTimeUtc(Path.Combine(path, "Sandbox_config.sbc"));

                if (lastsave != filesavetime && configsave > lastsave)
                {
                    filesavetime = lastsave;
                    return true;
                }

                var seprocesses = System.Diagnostics.Process.GetProcessesByName("SpaceEngineers");

                if (seprocesses.Length == 0)
                {
                    return false;
                }

                System.Threading.Thread.Sleep(5000);
            }
        }

        static void Main(string[] args)
        {
            var opts = SEMapOptions.FromArguments(args);

            if (opts.SaveDirectory == null)
            {
                opts.SaveDirectory = Environment.CurrentDirectory;
            }

            if (opts.OutputDirectory == null)
            {
                opts.OutputDirectory = Environment.CurrentDirectory;
            }

            if (opts.ShowHelp == true || opts.ContentDirectory == null)
            {
                ShowHelp();
                return;
            }

            Console.WriteLine("Creating contour maps");
            var contourmaps = MapUtils.GetContourMaps(opts);
            DateTime filesavetime = File.GetLastWriteTimeUtc(Path.Combine(opts.SaveDirectory, "Sandbox.sbc"));

            do
            {
                Console.WriteLine("Getting GPS entries");
                var gpsentlists = MapUtils.GetGPSEntries(opts.SaveDirectory, opts.PlanetName, opts.Rotate45, opts.PlanetPosition, opts.PlanetRotation, out var endname);
                var segments = GetSegmentPrefixes(gpsentlists, opts.ChapterParts);

                foreach (var segment in segments)
                {
                    if (segment.prefixes.Length == 0 || !opts.CropEnd)
                    {
                        var distance = GetTravelDistance(gpsentlists, segment.prefixes);
                        var distoverground = GetDistanceOverGround(gpsentlists, segment.prefixes, opts.PlanetPosition);
                        var elevchange = GetElevationChange(gpsentlists, segment.prefixes, opts.PlanetPosition);
                        Console.WriteLine($"Processing segment {segment.name} (Total {distance / 1000:0.00}km / Ground {distoverground / 1000:0.00}km / Elev {elevchange / 1000:0.00}km)");
                        Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}: Processing segment {segment.name} (Total {distance / 1000:0.00}km / Ground {distoverground / 1000:0.00}km / Elev {elevchange / 1000:0.00}km)");
                        string segdir = opts.OutputDirectory;

                        if (segment.prefixes.Length != 0)
                        {
                            segdir = Path.Combine(segdir, segment.name);
                        }

                        if (!Directory.Exists(segdir))
                        {
                            Directory.CreateDirectory(segdir);
                        }

                        MapUtils.SaveMaps(contourmaps, gpsentlists, opts, segment.prefixes, segdir, endname);
                        SavePOIList(gpsentlists, segment.prefixes, segdir, opts.PlanetPosition);
                    }
                }

                Console.WriteLine("Done");

                if (!opts.OnSave)
                {
                    break;
                }
            }
            while (WaitForSave(opts.SaveDirectory, ref filesavetime));

            Console.WriteLine("Exiting");
        }
    }
}
