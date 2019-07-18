using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class Rule
    {
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public double MinLatitude { get; set; }
        public double MaxLatitude { get; set; }
        public double MinSlope { get; set; }
        public double MaxSlope { get; set; }
        public List<Layer> Layers { get; set; }

        public static Rule FromXML(XElement el)
        {
            var height = el.Element("Height");
            var latitude = el.Element("Latitude");
            var slope = el.Element("Slope");
            var layers = el.Element("Layers");

            return new Rule
            {
                MinHeight = double.TryParse(height?.Attribute("Min")?.Value, out double minheight) ? minheight : 0,
                MaxHeight = double.TryParse(height?.Attribute("Max")?.Value, out double maxheight) ? maxheight : 10,
                MinLatitude = double.TryParse(latitude?.Attribute("Min")?.Value, out double minlatitude) ? minlatitude : 0,
                MaxLatitude = double.TryParse(latitude?.Attribute("Max")?.Value, out double maxlatitude) ? maxlatitude : 90,
                MinSlope = double.TryParse(slope?.Attribute("Min")?.Value, out double minslope) ? minslope : 0,
                MaxSlope = double.TryParse(slope?.Attribute("Max")?.Value, out double maxslope) ? maxslope : 90,
                Layers = layers?.Elements("Layer")?.Select(e => Layer.FromXML(e))?.ToList()
            };
        }
    }
}
