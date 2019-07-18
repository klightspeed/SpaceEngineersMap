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

                hmaprows = new ushort[hmapsource.PixelHeight + 2][];
                hmaprows[0] = new ushort[hmapsource.PixelWidth + 2];
                hmaprows[hmapsource.PixelHeight + 1] = new ushort[hmapsource.PixelWidth + 2];

                var hmapdata = new byte[hmapsource.PixelWidth * hmapsource.PixelHeight * 2];
                hmapsource.CopyPixels(hmapdata, hmapsource.PixelWidth * 2, 0);

                for (int i = 0; i < hmapsource.PixelHeight; i++)
                {
                    hmaprows[i + 1] = new ushort[hmapsource.PixelWidth + 2];

                    for (int j = 0; j < hmapsource.PixelWidth; j++)
                    {
                        hmaprows[i + 1][j + 1] = BitConverter.ToUInt16(hmapdata, i * hmapsource.PixelWidth * 2 + j * 2);
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

        public Bitmap CreateContourMap()
        {
            Color ice = Color.Aquamarine;

            int w = Heights.Length - 2;
            var bmp = new Bitmap(w, w, PixelFormat.Format24bppRgb);
            var bmpdata = bmp.LockBits(new Rectangle(0, 0, w, w), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            var row = new byte[w * 3];

            for (int i = 0; i < Heights.Length - 2; i++)
            {
                var hrow = Heights[i + 1];
                var hup = Heights[i];
                var hdn = Heights[i + 2];
                var mrow = Materials[i + 1];

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

                    byte mat = mrow[j + 1].ComplexMaterial;

                    if (mid / 4 != hi / 4)
                    {
                        pxr = pxg = pxb = 64;
                    }
                    else if (mat == 82)
                    {
                        pxr = ice.R;
                        pxg = ice.G;
                        pxb = ice.B;
                    }
                    else
                    {
                        int px1 = ((hrow[j + 1] * 78 / 65536 + 2) / 4) * 6;
                        int px2 = ((hrow[j + 1] * 78 / 1024 + 2 * 64) / 4) % 64;
                        pxr = (byte)(px1 + px2 + 64);
                        pxg = (byte)(px2 + 160);
                        pxb = (byte)(px2 + 128 - px1 / 2);
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
