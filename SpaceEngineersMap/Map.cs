using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices.Marshalling;
using MathNet.Spatial.Euclidean;

namespace SpaceEngineersMap
{
    public class Map
    {
        public CubeFace Face { get; set; }
        public string Name { get; set; }
        public ushort[][] Heights { get; set; }
        public MapMaterial[][] Materials { get; set; }

        public static Map Load(string path, string name)
        {
            ushort[][] hmaprows;
            MapMaterial[][] mmaprows;

            using (var hmap = Image.Load<L16>(Path.Combine(path, name + ".png")))
            {
                hmaprows = new ushort[hmap.Height + 2][];
                hmaprows[0] = new ushort[hmap.Width + 2];
                hmaprows[hmap.Height + 1] = new ushort[hmap.Width + 2];

                hmap.ProcessPixelRows(p =>
                {
                    for (int i = 0; i < hmap.Height; i++)
                    {
                        var prow = p.GetRowSpan(i);
                        var row = hmaprows[i + 1] = new ushort[prow.Length + 2];
                        MemoryMarshal.Cast<L16, ushort>(prow).CopyTo(row.AsSpan()[1..]);
                    }
                });
            }

            using (var mmap = Image.Load<Argb32>(Path.Combine(path, name + "_mat.png")))
            {
                mmaprows = new MapMaterial[mmap.Height + 2][];
                mmaprows[0] = new MapMaterial[mmap.Width + 2];
                mmaprows[mmap.Height + 1] = new MapMaterial[mmap.Width + 2];

                mmap.ProcessPixelRows(p =>
                {
                    for (int i = 0; i < mmap.Width; i++)
                    {
                        var prow = p.GetRowSpan(i);
                        var row = mmaprows[i + 1] = new MapMaterial[mmap.Width + 2];

                        for (int j = 0; j < mmap.Width; j++)
                        {
                            row[j + 1] = MapMaterial.FromARGB(prow[j]);
                        }
                    }
                });
            }

            return new Map
            {
                Face = MapUtils.GetFace(name),
                Name = name,
                Heights = hmaprows,
                Materials = mmaprows
            };
        }

        public static Map CreateMercatorMap(Dictionary<string, Map> maps, double minMercatorLon, double maxMercatorLon)
        {
            var width = (int)((maxMercatorLon - minMercatorLon) * 2048 / 90) + 2;

            var heights = new ushort[width][];
            var materials = new MapMaterial[width][];
            var torad = Math.PI / 4096;
            var mult = 1024;
            var radius = 50000;
            minMercatorLon *= Math.PI / 180;

            for (int py = 0; py < heights.Length; py++)
            {
                var lineheights = heights[py] = new ushort[width];
                var linematerials = materials[py] = new MapMaterial[width];

                for (int px = 0; px < lineheights.Length; px++)
                {
                    var lon = (px - 1) * torad + minMercatorLon;
                    var lattan = (py - heights.Length / 2) * torad;

                    var pos = new Vector3D(-Math.Sin(lon) * radius, lattan * radius, Math.Cos(lon) * radius);
                    var (pface, ppoint) = MapUtils.Project(pos, mult, mult, false);
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
                    lineheights[px] = (ushort)hgt;
                    linematerials[px] = mat;
                }
            }

            return new Map
            {
                Face = CubeFace.Mercator,
                Name = "mercator",
                Heights = heights,
                Materials = materials
            };
        }

        public void SetEdge(int x0a, int y0a, int dxa, int dya, Map other, int x0b, int y0b, int dxb, int dyb)
        {

            for (int i = 0; i < Heights.Length - 2; i++)
            {
                int xa = x0a + dxa * i;
                int ya = y0a + dya * i;
                int xb = x0b + dxb * i;
                int yb = y0b + dyb * i;

                this.Heights[ya][xa] = other.Heights[yb][xb];
                this.Materials[ya][xa] = other.Materials[yb][xb];
            }
        }

        private bool IsSeaLevelIce(int x, int y, int width, int height)
        {
            return !new[] { "up", "down" }.Any(e => e.Equals(Name, StringComparison.OrdinalIgnoreCase)) &&
                   Math.Pow(y - height / 2, 2) * 3 < Math.Pow(width / 2, 2) + Math.Pow(x - width / 2, 2);
        }

        private T[][] RotateFlip<T>(T[][] input, RotateFlipType rotation)
        {
            var w = input.Length;
            var output = new T[w][];

            for (int i = 0; i < w; i++)
            {
                output[i] = new T[w];
            }

            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    int x;
                    int y;

                    switch (rotation)
                    {
                        case RotateFlipType.RotateNoneFlipNone:
                        default:
                            x = j;
                            y = i;
                            break;
                        case RotateFlipType.Rotate90FlipNone:
                            x = w - i - 1;
                            y = j;
                            break;
                        case RotateFlipType.Rotate180FlipNone:
                            x = w - j - 1;
                            y = w - i - 1;
                            break;
                        case RotateFlipType.Rotate270FlipNone:
                            x = i;
                            y = w - j - 1;
                            break;
                        case RotateFlipType.RotateNoneFlipX:
                            x = w - j - 1;
                            y = i;
                            break;
                        case RotateFlipType.Rotate90FlipX:
                            x = i;
                            y = j;
                            break;
                        case RotateFlipType.Rotate180FlipX:
                            x = j;
                            y = w - i - 1;
                            break;
                        case RotateFlipType.Rotate270FlipX:
                            x = w - i - 1;
                            y = w - j - 1;
                            break;
                    }

                    output[y][x] = input[i][j];
                }
            }

            return output;
        }

        public Image<Argb32> CreateContourMap(SEMapOptions options)
        {
            var ice = options.SlopeShading || options.ReliefShading ? Color.Aqua.ToPixel<Rgb24>() : Color.Aquamarine.ToPixel<Rgb24>();

            int w = Heights.Length - 2;
            var bmp = new Image<Argb32>(w, w);
            var rotation = options.FaceRotations[Face];
            var heights = RotateFlip(Heights, rotation);
            var materials = RotateFlip(Materials, rotation);
            var heightmul = (options.PlanetMaxRadius - options.PlanetMinRadius) / 100;
            var sealevel = Math.Max(options.PlanetSeaLevel - options.PlanetMinRadius, 0) / 100;
            var heightofs = Math.Floor(sealevel) + 2 - sealevel;
            sealevel = (options.PlanetSeaLevel - options.PlanetMinRadius) / 100 + heightofs;

            bmp.ProcessPixelRows(p =>
            {
                for (int i = 0; i < heights.Length - 2; i++)
                {
                    var hrow = heights[i + 1];
                    var hup = heights[i];
                    var hdn = heights[i + 2];
                    var mrow = materials[i + 1];
                    var row = p.GetRowSpan(i);

                    for (int j = 0; j < hrow.Length - 2; j++)
                    {
                        byte pxr;
                        byte pxg;
                        byte pxb;

                        var lvs = new List<ushort> { hrow[j], hrow[j + 1], hrow[j + 2], hup[j], hup[j + 1], hup[j + 2], hdn[j], hdn[j + 1], hdn[j + 2] };
                        lvs.Sort();
                        int lo = (int)Math.Floor(lvs[0] * heightmul / 65536 + heightofs);
                        int hi = (int)Math.Floor(lvs[8] * heightmul / 65536 + heightofs);
                        int mid = (int)Math.Floor(hrow[j + 1] * heightmul / 65536 + heightofs);
                        int height = hrow[j + 1];
                        int slope = lvs[8] - lvs[0];

                        byte mat = mrow[j + 1].ComplexMaterial;

                        if (mid > sealevel && ((mat == 82 && slope < 16) || (mat == 0 && height <= 16 && IsSeaLevelIce(j, i, w, w))))
                        {
                            pxr = ice.R;
                            pxg = ice.G;
                            pxb = ice.B;
                        }
                        else if (options.SlopeShading)
                        {
                            int px1 = (int)Math.Floor(((hrow[j + 1] * heightmul / 65536 + heightofs) / 4) * 6);
                            int px3 = 127 - (int)Math.Min(127, (lvs[8] - lvs[0]) / 6.0);
                            pxr = (byte)(px1 + px3 * 3 / 4 + 32);
                            pxg = (byte)(px3 + 128);
                            pxb = (byte)(px3 * 3 / 4 + 96 - px1 / 2);
                        }
                        else if (options.ReliefShading)
                        {
                            var v1 = (hrow[j + 2] * 4 + hup[j + 2] * 2 + hup[j + 1] + hdn[j + 2]) / 8;
                            var v2 = (hrow[j + 0] * 4 + hdn[j + 0] * 2 + hdn[j + 1] + hup[j + 0]) / 8;
                            var v = Math.Atan((v1 - v2) / 192.0) * 128.0 / Math.PI;
                            int px1 = (int)Math.Floor(((hrow[j + 1] * heightmul / 65536 + heightofs) / 4) * 6);
                            int px3 = 127 - (int)Math.Max(0, Math.Min(127, v + 64));
                            pxr = (byte)(px1 + px3 * 3 / 4 + 32);
                            pxg = (byte)(px3 + 128);
                            pxb = (byte)(px3 * 3 / 4 + 96 - px1 / 2);
                        }
                        else
                        {
                            int px1 = (int)Math.Floor(((hrow[j + 1] * heightmul / 65536 + heightofs) / 4) * 6);
                            int px2 = ((int)Math.Floor((hrow[j + 1] * heightmul / 1024) + heightofs * 64) / 4) % 64;
                            pxr = (byte)(px1 + px2 + 64);
                            pxg = (byte)(px2 + 160);
                            pxb = (byte)(px2 + 128 - px1 / 2);
                        }

                        if (mid < sealevel)
                        {
                            pxr /= 2;
                            pxg /= 2;
                        }

                        if (mid / 4 != hi / 4 && options.ContourLines)
                        {
                            if (mid < sealevel)
                            {
                                pxr = (byte)(pxr / 2 + 8);
                                pxg = (byte)(pxg / 2 + 32);
                                pxb = (byte)(pxb / 2 + 64);
                            }
                            else if (mid < 8)
                            {
                                pxr = (byte)(pxr / 2 + 32);
                                pxg = (byte)(pxg / 2 + 64);
                                pxb = (byte)(pxb / 2 + 48);
                            }
                            else if (hi >= 72)
                            {
                                pxr = (byte)(pxr / 2 + 127);
                                pxg = (byte)(pxg / 2 + 0);
                                pxb = (byte)(pxb / 2 + 0);
                            }
                            else if (hi >= 32)
                            {
                                pxr = (byte)(pxr / 2 + 96);
                                pxg = (byte)(pxg / 2 + 32);
                                pxb = (byte)(pxb / 2 + 0);
                            }
                            else if (hi >= 24)
                            {
                                pxr = (byte)(pxr / 2 + 64);
                                pxg = (byte)(pxg / 2 + 32);
                                pxb = (byte)(pxb / 2 + 0);
                            }
                            else
                            {
                                pxr = (byte)(pxr / 2 + 16);
                                pxg = (byte)(pxg / 2 + 64);
                                pxb = (byte)(pxb / 2 + 16);
                            }
                        }

                        row[j] = new Argb32(pxr, pxg, pxb, 255);
                    }
                }
            });

            return bmp;
        }
    }
}
