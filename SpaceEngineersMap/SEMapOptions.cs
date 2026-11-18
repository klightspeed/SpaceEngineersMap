using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class SEMapOptions
    {
        public string? SaveDirectory { get; set; }
        public string? ContentDirectory { get; set; }
        public string? OutputDirectory { get; set; }
        public string? WorkshopDirectory { get; set; }
        public string PlanetName { get; set; } = "EarthLike";
        public long PlanetEntityId { get; set; }
        public string? PlanetDataDirectory { get; set; }
        public string? PlanetDirectory { get; set; }
        public string[] ChapterParts { get; set; } = [];
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
        public double MinMercatorLongitude { get; set; } = -180;
        public double MaxMercatorLongitude { get; set; } = 180;
        public bool TransverseMercator { get; set; } = false;
        public double TransverseMercatorMeridian { get; set; } = 0;
        public bool NorthIsYMinus { get; set; } = false;
        public CubeFace[][] TileFaces { get; set; } = [];
        public Dictionary<CubeFace, RotateFlipType> FaceRotations { get; set; } = [];
        public Vector3D PlanetPosition { get; set; } = new Vector3D(0, 0, 0);
        public Quaternion PlanetRotation { get; set; } = new Quaternion(1, 0, 0, 0);

        private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private void FillPlanetDetails()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(SaveDirectory, nameof(SaveDirectory));
            ArgumentException.ThrowIfNullOrWhiteSpace(ContentDirectory, nameof(ContentDirectory));
            ArgumentException.ThrowIfNullOrWhiteSpace(WorkshopDirectory, nameof(WorkshopDirectory));
            ArgumentException.ThrowIfNullOrWhiteSpace(PlanetName, nameof(PlanetName));

            PlanetDataDirectory = FindPlanetDir(SaveDirectory, ContentDirectory, WorkshopDirectory, PlanetName);
            PlanetDirectory = Path.Combine(PlanetDataDirectory, "PlanetDataFiles", PlanetName);

            foreach (var savefile in Directory.EnumerateFiles(SaveDirectory, "SANDBOX_*.sbs"))
            {
                var xdoc = XDocument.Load(savefile);
                var objects = xdoc.Root?.Element("SectorObjects")?.Elements("MyObjectBuilder_EntityBase");

                foreach (var obj in objects ?? [])
                {
                    if (obj.Attribute(xsi + "type")?.Value == "MyObjectBuilder_Planet"
                        && string.Equals(obj.Element("PlanetGenerator")?.Value, PlanetName, StringComparison.OrdinalIgnoreCase)
                        && obj.Element("EntityId")?.Value is string entityidval
                        && long.TryParse(entityidval, out var entityid)
                        && obj.Element("PositionAndOrientation") is { } positionAndOrientation
                        && positionAndOrientation.Element("Position") is { } positionElement
                        && positionAndOrientation.Element("Orientation") is { } orientationElement
                        && obj.Element("MaximumHillRadius")?.Value is string maxRadiusValue
                        && double.TryParse(maxRadiusValue, out var maxRadius)
                        && obj.Element("MinimumSurfaceRadius")?.Value is string minRadiusValue
                        && double.TryParse(minRadiusValue, out var minRadius)
                        && positionElement.Attribute("x")?.Value is string xval
                        && double.TryParse(xval, out var x)
                        && positionElement.Attribute("y")?.Value is string yval
                        && double.TryParse(yval, out var y)
                        && positionElement.Attribute("z")?.Value is string zval
                        && double.TryParse(zval, out var z)
                        && orientationElement.Attribute("W")?.Value is string rwval
                        && double.TryParse(rwval, out var rw)
                        && orientationElement.Attribute("X")?.Value is string rxval
                        && double.TryParse(rxval, out var rx)
                        && orientationElement.Attribute("Y")?.Value is string ryval
                        && double.TryParse(ryval, out var ry)
                        && orientationElement.Attribute("Z")?.Value is string rzval
                        && double.TryParse(rzval, out var rz))
                    {
                        double size = 128;
                        while (size < maxRadius) size *= 2;

                        var pos = new Vector3D(x + size + 0.5, y + size + 0.5, z + size + 0.5);
                        var rotation = new Quaternion(rw, rx, ry, rz);

                        PlanetPosition = pos;
                        PlanetRotation = rotation;
                        PlanetMaxRadius = maxRadius;
                        PlanetMinRadius = minRadius;
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
                    using var file = File.Open(saveconfig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var xml = XDocument.Load(file);
                    var root = xml.Root;
                    var mods = root?.Element("Mods")?.Elements("ModItem") ?? [];

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
            ArgumentException.ThrowIfNullOrWhiteSpace(SaveDirectory, nameof(SaveDirectory));

            var xdoc = XDocument.Load(Path.Combine(SaveDirectory, "Sandbox.sbc"));
            var scriptdata = xdoc.Root?.Element("ScriptManagerData")?.Element("variables")?.Element("dictionary")?.Elements("item").ToArray() ?? [];
            foreach (var item in scriptdata)
            {
                var key = item.Element("Key")?.Value;
                var value = item.Element("Value")?.Value;

                if (key == "JWater2" && value != null)
                {
                    var jwater2doc = XDocument.Parse(value);
                    var waters = jwater2doc.Root?.Elements("Waters") ?? [];

                    foreach (var water in waters)
                    {
                        if (!long.TryParse(water.Element("EntityId")?.Value, out var entityId)) continue;
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
                ChapterParts = [],
                TransverseMercator = false,
                TransverseMercatorMeridian = 0,
                TileFaces =
                [
                    [CubeFace.None, CubeFace.Down, CubeFace.None, CubeFace.None],
                    [CubeFace.Back, CubeFace.Right, CubeFace.Front, CubeFace.Left],
                    [CubeFace.None, CubeFace.Up, CubeFace.None, CubeFace.None]
                ],
                FaceRotations = new Dictionary<CubeFace, RotateFlipType>
                {
                    [CubeFace.Up] = RotateFlipType.Rotate270FlipNone,
                    [CubeFace.Down] = RotateFlipType.Rotate270FlipNone,
                    [CubeFace.Left] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Front] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Right] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Back] = RotateFlipType.Rotate180FlipNone,
                    [CubeFace.Mercator] = RotateFlipType.RotateNoneFlipNone
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
                    opts.TileFaces = [.. args[i + 1].Split(',').Select(p => p.Split(':').Select(f => MapUtils.GetFace(f)).ToArray())];
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
                else if (args[i] == "--transverse")
                {
                    opts.TransverseMercator = true;
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
                else if (args[i] == "--minlon" && i < args.Length - 1)
                {
                    opts.MinMercatorLongitude = double.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--maxlon" && i < args.Length - 1)
                {
                    opts.MaxMercatorLongitude = double.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--meridian" && i < args.Length - 1)
                {
                    opts.TransverseMercatorMeridian = double.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--north" && i < args.Length - 1)
                {
                    opts.NorthIsYMinus = args[i + 1] == "up" || args[i + 1] == "Y-";
                    i++;
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

            opts.ChapterParts = [.. chapterparts];

            opts.FillPlanetDetails();
            opts.FillWaterModSeaLevel();

            return opts;
        }
    }
}
