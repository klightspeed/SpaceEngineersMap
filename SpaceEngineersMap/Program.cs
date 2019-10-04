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
        static string[] GetSegmentPrefixes(Dictionary<CubeFace, List<List<GpsEntry>>> gpsentries)
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
            Console.WriteLine("--crop");
            Console.WriteLine("    Crop map to visited area, with padding");
            Console.WriteLine("--croptexture");
            Console.WriteLine("    Crop map to visited area, padded to texture size");
            Console.WriteLine("--texturesize <size>");
            Console.WriteLine("    Set episode map texture size");
            Console.WriteLine("--fullmaptexturesize <size>");
            Console.WriteLine("    Set full map texture size");
            Console.WriteLine();
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

            Console.WriteLine("Getting GPS entries");
            var gpsentlists = MapUtils.GetGPSEntries(opts.SaveDirectory);
            var segments = GetSegmentPrefixes(gpsentlists);
            Console.WriteLine("Creating contour maps");
            var contourmaps = MapUtils.GetContourMaps(opts.ContentDirectory);

            foreach (var segment in segments)
            {
                Console.WriteLine($"Processing segment {segment}");
                string segdir = opts.OutputDirectory;

                if (segment != "")
                {
                    segdir = Path.Combine(segdir, segment);
                }

                if (!Directory.Exists(segdir))
                {
                    Directory.CreateDirectory(segdir);
                }

                MapUtils.SaveMaps(contourmaps, gpsentlists, opts, segment, segdir);
            }
        }
    }
}
