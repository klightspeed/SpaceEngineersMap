using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class SEMapOptions
    {
        public string SaveDirectory { get; set; }
        public string ContentDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string WorkshopDirectory { get; set; }
        public string PlanetName { get; set; } = "EarthLike";
        public long PlanetEntityId { get; set; }
        public string PlanetDataDirectory { get; set; }
        public string PlanetDirectory { get; set; }
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
        public bool IncludeAuxTravels { get; set; } = false;
        public int EndTextureSize { get; set; } = 256;
        public int EpisodeTextureSize { get; set; } = 512;
        public int FullMapTextureSize { get; set; } = 1024;
        public double PlanetSeaLevel { get; set; } = 0;
        public double PlanetMinRadius { get; set; } = 59400;
        public double PlanetMaxRadius { get; set; } = 67200;
        public CubeFace[][] TileFaces { get; set; }
        public Dictionary<CubeFace, RotateFlipType> FaceRotations { get; set; }
        public Vector3D PlanetPosition { get; set; } = new Vector3D(0, 0, 0);
        public Quaternion PlanetRotation { get; set; } = new Quaternion(1, 0, 0, 0);

        private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private void FillPlanetDetails()
        {
            PlanetDataDirectory = FindPlanetDir(SaveDirectory, ContentDirectory, WorkshopDirectory, PlanetName);
            PlanetDirectory = Path.Combine(PlanetDataDirectory, "PlanetDataFiles", PlanetName);

            foreach (var savefile in Directory.EnumerateFiles(SaveDirectory, "SANDBOX_*.sbs"))
            {
                var xdoc = XDocument.Load(savefile);
                var objects = xdoc.Root.Element("SectorObjects").Elements("MyObjectBuilder_EntityBase");
                foreach (var obj in objects)
                {
                    if (obj.Attribute(xsi + "type")?.Value == "MyObjectBuilder_Planet" && string.Equals(obj.Element("PlanetGenerator")?.Value, PlanetName, StringComparison.OrdinalIgnoreCase))
                    {
                        var positionAndOrientation = obj.Element("PositionAndOrientation");
                        var positionElement = positionAndOrientation.Element("Position");
                        var orientationElement = positionAndOrientation.Element("Orientation");
                        var entityid = long.Parse(obj.Element("EntityId").Value);
                        var maxradius = double.Parse(obj.Element("MaximumHillRadius").Value);
                        var minradius = double.Parse(obj.Element("MinimumSurfaceRadius").Value);

                        double size = 128;
                        while (size < maxradius) size *= 2;

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

                        PlanetPosition = pos;
                        PlanetRotation = rotation;
                        PlanetMaxRadius = maxradius;
                        PlanetMinRadius = minradius;
                        PlanetEntityId = entityid;
                        return;
                    }
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
                            if (mod.Element("PublishedFileId")?.Value is string modid)
                            {
                                modids.Add(modid);
                            }
                            else if (mod.Element("Name")?.Value is string modname && long.TryParse(modname.Replace(".sbm", ""), out _))
                            {
                                modids.Add(modname.Replace(".sbm", ""));
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
                var planetdir = Path.Combine(workshopdir, modid, "Data");
                if (MapUtils.Faces.All(f => File.Exists(Path.Combine(planetdir, "PlanetDataFiles", planetname, $"{f}.png"))))
                {
                    return planetdir;
                }
            }

            return Path.Combine(contentdir, "Data");
        }

        private void FillWaterModSeaLevel()
        {
            var xdoc = XDocument.Load(Path.Combine(SaveDirectory, "Sandbox.sbc"));
            var scriptmgrdata = xdoc.Root.Element("ScriptManagerData");
            var scriptdata = xdoc.Root.Element("ScriptManagerData")?.Element("variables")?.Element("dictionary")?.Elements("item").ToArray() ?? Array.Empty<XElement>();
            foreach (var item in scriptdata)
            {
                var key = item.Element("Key").Value;
                var value = item.Element("Value").Value;

                if (key == "JWater2")
                {
                    var jwater2doc = XDocument.Parse(value);
                    var waters = jwater2doc.Root.Elements("Waters");

                    foreach (var water in waters)
                    {
                        if (!long.TryParse(water.Element("EntityId").Value, out var entityId)) continue;
                        if (entityId != PlanetEntityId) continue;
                        if (!double.TryParse(water.Element("Settings")?.Element("Radius")?.Value, out var radius)) continue;
                        PlanetSeaLevel = radius * PlanetMinRadius;
                        return;
                    }
                }
            }
        }

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
                else if (args[i] == "--includeauxtravels")
                {
                    opts.IncludeAuxTravels = true;
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
                else if (args[i] == "--sealevel" && i < args.Length - 1)
                {
                    opts.PlanetSeaLevel = Double.Parse(args[i + 1]);
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

            opts.FillPlanetDetails();
            opts.FillWaterModSeaLevel();

            return opts;
        }
    }
}
