using System;
using System.Collections.Generic;
using System.Text;

namespace SpaceEngineersMap
{
    public struct MapMaterial
    {
        public byte Ore { get; set; }
        public byte Biome { get; set; }
        public byte ComplexMaterial { get; set; }
        public byte Ignore { get; set; }

        public static MapMaterial FromARGB(int argb)
        {
            return new MapMaterial
            {
                Ore = (byte)(argb & 0xFF),
                Biome = (byte)((argb >> 8) & 0xFF),
                ComplexMaterial = (byte)((argb >> 16) & 0xFF),
                Ignore = 0
            };
        }

        public static MapMaterial FromARGB(byte[] data, int ofs)
        {
            return new MapMaterial
            {
                Ore = data[ofs],
                Biome = data[ofs + 1],
                ComplexMaterial = data[ofs + 2],
                Ignore = 0
            };
        }
    }
}
