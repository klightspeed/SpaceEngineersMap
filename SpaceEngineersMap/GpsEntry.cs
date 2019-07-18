using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class GpsEntry
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Description { get; set; }
        public bool ShowOnHud { get; set; }

        public GpsEntry Project(int mult, int maxval, string face)
        {
            double v = 0;
            double x = 0;
            double y = 0;
            
            switch (face)
            {
                case "up":
                    v = Y;
                    x = X;
                    y = Z;
                    break;
                case "down":
                    v = -Y;
                    x = X;
                    y = -Z;
                    break;
                case "left":
                    v = X;
                    x = -Z;
                    y = -Y;
                    break;
                case "right":
                    v = -X;
                    x = Z;
                    y = -Y;
                    break;
                case "front":
                    v = -Z;
                    x = -X;
                    y = Y;
                    break;
                case "back":
                    v = Z;
                    x = X;
                    y = Y;
                    break;
            }

            if (v > maxval / mult)
            {
                double div = mult / v;

                return new GpsEntry
                {
                    Name = Name,
                    X = x * div,
                    Y = y * div,
                    Z = mult,
                    Description = Description,
                    ShowOnHud = ShowOnHud
                };
            }
            else
            {
                return null;
            }
        }

        public static GpsEntry FromXML(XElement xe)
        {
            var coords = xe.Element("coords");

            return new GpsEntry
            {
                Name = xe.Element("name").Value,
                X = double.Parse(coords.Element("X").Value),
                Y = double.Parse(coords.Element("Y").Value),
                Z = double.Parse(coords.Element("Z").Value),
                Description = xe.Element("description").Value,
                ShowOnHud = xe.Element("showOnHud").Value == "true"
            };
        }
    }
}
