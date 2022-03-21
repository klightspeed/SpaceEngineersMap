using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows.Media.Imaging;

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

            using (var f = File.OpenRead(Path.Combine(path, name + ".png")))
            {
                BitmapDecoder hmapdecoder = BitmapDecoder.Create(f, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapSource hmapsource = hmapdecoder.Frames[0];
                int pixsize = hmapsource.Format.BitsPerPixel / 8;

                hmaprows = new ushort[hmapsource.PixelHeight + 2][];
                hmaprows[0] = new ushort[hmapsource.PixelWidth + 2];
                hmaprows[hmapsource.PixelHeight + 1] = new ushort[hmapsource.PixelWidth + 2];

                var hmapdata = new byte[hmapsource.PixelWidth * hmapsource.PixelHeight * pixsize];
                hmapsource.CopyPixels(hmapdata, hmapsource.PixelWidth * pixsize, 0);

                for (int i = 0; i < hmapsource.PixelHeight; i++)
                {
                    hmaprows[i + 1] = new ushort[hmapsource.PixelWidth + 2];

                    for (int j = 0; j < hmapsource.PixelWidth; j++)
                    {
                        hmaprows[i + 1][j + 1] = BitConverter.ToUInt16(hmapdata, i * hmapsource.PixelWidth * pixsize + j * pixsize);
                    }
                }
            }

            using (var f = File.OpenRead(Path.Combine(path, name + "_mat.png")))
            {
                BitmapDecoder mmapdecoder = BitmapDecoder.Create(f, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapSource mmapsource = mmapdecoder.Frames[0];
                int pixsize = mmapsource.Format.BitsPerPixel / 8;

                mmaprows = new MapMaterial[mmapsource.PixelHeight + 2][];
                mmaprows[0] = new MapMaterial[mmapsource.PixelWidth + 2];
                mmaprows[mmapsource.PixelHeight + 1] = new MapMaterial[mmapsource.PixelWidth + 2];

                var mmapdata = new byte[mmapsource.PixelWidth * mmapsource.PixelHeight * pixsize];

                mmapsource.CopyPixels(mmapdata, mmapsource.PixelWidth * pixsize, 0);

                for (int i = 0; i < mmapsource.PixelHeight; i++)
                {
                    mmaprows[i + 1] = new MapMaterial[mmapsource.PixelWidth + 2];

                    for (int j = 0; j < mmapsource.PixelWidth; j++)
                    {
                        int ofs = i * mmapsource.PixelWidth * pixsize + j * pixsize;
                        mmaprows[i + 1][j + 1] = MapMaterial.FromARGB(mmapdata, ofs);
                    }
                }
            }

            return new Map
            {
                Face = MapUtils.GetFace(name),
                Name = name,
                Heights = hmaprows,
                Materials = mmaprows
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

        public Bitmap CreateContourMap(SEMapOptions options)
        {
            Color ice = options.SlopeShading || options.ReliefShading ? Color.Aqua : Color.Aquamarine;

            int w = Heights.Length - 2;
            var bmp = new Bitmap(w, w, PixelFormat.Format24bppRgb);
            var bmpdata = bmp.LockBits(new Rectangle(0, 0, w, w), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            var row = new byte[w * 3];
            var rotation = options.FaceRotations[Face];
            var heights = RotateFlip(Heights, rotation);
            var materials = RotateFlip(Materials, rotation);

            for (int i = 0; i < heights.Length - 2; i++)
            {
                var hrow = heights[i + 1];
                var hup = heights[i];
                var hdn = heights[i + 2];
                var mrow = materials[i + 1];

                for (int j = 0; j < hrow.Length - 2; j++)
                {
                    byte pxr;
                    byte pxg;
                    byte pxb;

                    var lvs = new List<ushort> { hrow[j], hrow[j + 1], hrow[j + 2], hup[j], hup[j + 1], hup[j + 2], hdn[j], hdn[j + 1], hdn[j + 2] };
                    lvs.Sort();
                    int lo = lvs[0] * 78 / 65536 + 2;
                    int hi = lvs[8] * 78 / 65536 + 2;
                    int mid = hrow[j + 1] * 78 / 65536 + 2;
                    int height = hrow[j + 1];
                    int slope = lvs[8] - lvs[0];

                    byte mat = mrow[j + 1].ComplexMaterial;

                    if ((mat == 82 && slope < 16) || (mat == 0 && height <= 16 && IsSeaLevelIce(j, i, w, w)))
                    {
                        pxr = ice.R;
                        pxg = ice.G;
                        pxb = ice.B;
                    }
                    else if (options.SlopeShading)
                    {
                        int px1 = (int)Math.Floor(((hrow[j + 1] * 78.0 / 65536 + 2) / 4) * 6);
                        int px3 = 127 - (int)Math.Min(127, (lvs[8] - lvs[0]) / 6.0);
                        pxr = (byte)(px1 + px3 * 3 / 4 + 32);
                        pxg = (byte)(px3 + 128);
                        pxb = (byte)(px3 * 3 / 4 + 96 - px1 / 2);
                    }
                    else if (options.ReliefShading)
                    {
                        var v1 = (hrow[j + 2] * 4 + hup[j + 2] * 2 + hup[j + 1] + hdn[j + 2]) / 8;
                        var v2 = (hrow[j + 0] * 4 + hdn[j + 0] * 2 + hdn[j + 1] + hup[j + 0]) / 8;
                        int px1 = (int)Math.Floor(((hrow[j + 1] * 78.0 / 65536 + 2) / 4) * 6);
                        int px3 = 127 - (int)Math.Max(0, Math.Min(127, (v1 - v2) / 6.0 + 64));
                        pxr = (byte)(px1 + px3 * 3 / 4 + 32);
                        pxg = (byte)(px3 + 128);
                        pxb = (byte)(px3 * 3 / 4 + 96 - px1 / 2);
                    }
                    else
                    {
                        int px1 = (int)Math.Floor(((hrow[j + 1] * 78.0 / 65536 + 2) / 4) * 6);
                        int px2 = ((hrow[j + 1] * 78 / 1024 + 2 * 64) / 4) % 64;
                        pxr = (byte)(px1 + px2 + 64);
                        pxg = (byte)(px2 + 160);
                        pxb = (byte)(px2 + 128 - px1 / 2);
                    }

                    if (mid / 4 != hi / 4 && options.ContourLines)
                    {
                        if (mid < 8)
                        {
                            pxr = (byte)(pxr / 2 + 32);
                            pxg = (byte)(pxr / 2 + 64);
                            pxb = (byte)(pxr / 2 + 48);
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
                            pxr = (byte)(pxr / 2 + 32);
                            pxg = (byte)(pxg / 2 + 64);
                            pxb = (byte)(pxb / 2 + 32);
                        }
                    }

                    row[j * 3] = pxb;
                    row[j * 3 + 1] = pxg;
                    row[j * 3 + 2] = pxr;
                }

                Marshal.Copy(row, 0, bmpdata.Scan0 + bmpdata.Stride * i, row.Length);
            }

            bmp.UnlockBits(bmpdata);

            return bmp;
        }
    }
}
