using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class Layer
    {
        public string Material { get; set; }
        public double Depth { get; set; }

        public static Layer FromXML(XElement el)
        {
            return new Layer
            {
                Depth = double.TryParse(el.Attribute("Depth")?.Value, out double depth) ? depth : 0,
                Material = el.Attribute("Material")?.Value
            };
        }
    }
}
