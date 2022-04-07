using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersMap
{
    public class SEMapOptions
    {
        public string SaveDirectory { get; set; }
        public string ContentDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string WorkshopDirectory { get; set; }
        public string PlanetName { get; set; } = "EarthLike";
        public string[] ChapterParts { get; set; }
        public bool ShowHelp { get; set; }
        public bool CropTileMap { get; set; }
        public bool CropTexture { get; set; }
        public bool CropEnd { get; set; }
        public bool OnSave { get; set; }
        public bool Rotate45 { get; set; }
        public bool ContourLines { get; set; } = true;
        public bool SlopeShading { get; set; } = false;
        public bool ReliefShading { get; set; } = false;
        public int EndTextureSize { get; set; } = 256;
        public int EpisodeTextureSize { get; set; } = 512;
        public int FullMapTextureSize { get; set; } = 1024;
        public CubeFace[][] TileFaces { get; set; }
        public Dictionary<CubeFace, RotateFlipType> FaceRotations { get; set; }

        public static SEMapOptions FromArguments(string[] args)
        {
            var opts = new SEMapOptions
            {
                SaveDirectory = null,
                ContentDirectory = null,
                OutputDirectory = null,
                ShowHelp = false,
                CropTileMap = false,
                CropTexture = false,
                Rotate45 = false,
                ChapterParts = null,
                TileFaces = new[]
                {
                    new[] { CubeFace.None, CubeFace.Down, CubeFace.None, CubeFace.None },
                    new[] { CubeFace.Back, CubeFace.Right, CubeFace.Front, CubeFace.Left },
                    new[] { CubeFace.None, CubeFace.Up, CubeFace.None, CubeFace.None }
                },
                FaceRotations = new Dictionary<CubeFace, RotateFlipType>
                {
                    [CubeFace.Up] = RotateFlipType.Rotate270FlipNone,
                    [CubeFace.Down] = RotateFlipType.Rotate270FlipNone,
                    [CubeFace.Left] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Front] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Right] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Back] = RotateFlipType.Rotate180FlipNone
                }
            };

            var chapterparts = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--savedir" && i < args.Length - 1)
                {
                    opts.SaveDirectory = args[i + 1];
                    i++;
                }
                else if (args[i] == "--contentdir" && i < args.Length - 1)
                {
                    opts.ContentDirectory = args[i + 1];
                    i++;
                }
                else if (args[i] == "--workshopdir" && i < args.Length - 1)
                {
                    opts.WorkshopDirectory = args[i + 1];
                    i++;
                }
                else if (args[i] == "--planetname" && i < args.Length - 1)
                {
                    opts.PlanetName = args[i + 1];
                    i++;
                }
                else if (args[i] == "--outdir" && i < args.Length - 1)
                {
                    opts.OutputDirectory = args[i + 1];
                    i++;
                }
                else if (args[i] == "--chapter" && i < args.Length - 1)
                {
                    chapterparts.Add(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--tile" && i < args.Length - 1)
                {
                    opts.TileFaces = args[i + 1].Split(',').Select(p => p.Split(':').Select(f => MapUtils.GetFace(f)).ToArray()).ToArray();
                    i++;
                }
                else if (args[i] == "--rotate" && i < args.Length - 1)
                {
                    var rots = args[i + 1].Split(',');
                    foreach (var xrot in rots)
                    {
                        var rot = xrot.Split(':');
                        var face = MapUtils.GetFace(rot[0]);
                        switch (rot[1])
                        {
                            case "0":
                                opts.FaceRotations[face] = RotateFlipType.RotateNoneFlipNone;
                                break;
                            case "cw":
                            case "90":
                                opts.FaceRotations[face] = RotateFlipType.Rotate90FlipNone;
                                break;
                            case "ccw":
                            case "270":
                                opts.FaceRotations[face] = RotateFlipType.Rotate270FlipNone;
                                break;
                            case "180":
                                opts.FaceRotations[face] = RotateFlipType.Rotate180FlipNone;
                                break;
                        }
                    }
                    i++;
                }
                else if (args[i] == "--crop")
                {
                    opts.CropTileMap = true;
                }
                else if (args[i] == "--croptexture")
                {
                    opts.CropTexture = true;
                }
                else if (args[i] == "--rotate45")
                {
                    opts.Rotate45 = true;
                }
                else if (args[i] == "--texturesize" && i < args.Length - 1)
                {
                    opts.EpisodeTextureSize = Int32.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--fullmaptexturesize" && i < args.Length - 1)
                {
                    opts.FullMapTextureSize = Int32.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--cropend")
                {
                    opts.CropEnd = true;
                }
                else if (args[i] == "--endsize" && i < args.Length - 1)
                {
                    opts.EndTextureSize = Int32.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--onsave")
                {
                    opts.OnSave = true;
                }
                else if (args[i] == "--contourlines")
                {
                    opts.ContourLines = true;
                }
                else if (args[i] == "--nocontourlines")
                {
                    opts.ContourLines = false;
                }
                else if (args[i] == "--slopeshading")
                {
                    opts.SlopeShading = true;
                }
                else if (args[i] == "--noslopeshading")
                {
                    opts.SlopeShading = false;
                }
                else if (args[i] == "--reliefshading")
                {
                    opts.ReliefShading = true;
                }
                else if (args[i] == "--noreliefshading")
                {
                    opts.ReliefShading = false;
                }
                else if (args[i] == "--help" || args[i] == "/?")
                {
                    opts.ShowHelp = true;
                    break;
                }
                else if (Directory.Exists(args[i]))
                {
                    opts.SaveDirectory = args[i];
                }
                else if (File.Exists(args[i]))
                {
                    opts.SaveDirectory = Path.GetDirectoryName(args[i]);
                }
                else
                {
                    Console.WriteLine($"Unrecognised option {args[i]}");
                    opts.ShowHelp = true;
                    break;
                }
            }

            opts.ChapterParts = chapterparts.ToArray();

            return opts;
        }
    }
}
