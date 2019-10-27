using System;
using System.Collections.Generic;
using System.Drawing;
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

        public GpsEntry Project(int mult, int maxval, CubeFace face)
        {
            double v = 0;
            double x = 0;
            double y = 0;
            
            switch (face)
            {
                case CubeFace.Up:
                    v = Y;
                    x = -X;
                    y = -Z;
                    break;
                case CubeFace.Down:
                    v = -Y;
                    x = X;
                    y = -Z;
                    break;
                case CubeFace.Left:
                    v = X;
                    x = -Z;
                    y = -Y;
                    break;
                case CubeFace.Right:
                    v = -X;
                    x = Z;
                    y = -Y;
                    break;
                case CubeFace.Front:
                    v = -Z;
                    x = -X;
                    y = Y;
                    break;
                case CubeFace.Back:
                    v = Z;
                    x = X;
                    y = Y;
                    break;
            }

            if (v > maxval / mult)
            {
                double div = mult / v;
                x *= div;
                y *= div;

                if (Math.Abs(x) > mult)
                {
                    double mult2 = mult / Math.Abs(x);
                    x = mult * (2 - mult2) * Math.Sign(x);
                    y *= mult2;
                }

                if (Math.Abs(y) > mult)
                {
                    double mult2 = mult / Math.Abs(y);
                    x *= mult2;
                    y = mult * (2 - mult2) * Math.Sign(y);
                }

                return new GpsEntry
                {
                    Name = Name,
                    X = x,
                    Y = y,
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

        public GpsEntry RotateFlip2D(RotateFlipType rotation)
        {
            switch (rotation)
            {
                case RotateFlipType.RotateNoneFlipNone:
                default:
                    return new GpsEntry { Name = Name, X = X, Y = Y, Z = Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate90FlipNone:
                    return new GpsEntry { Name = Name, X = -Y, Y = X, Z = Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate180FlipNone:
                    return new GpsEntry { Name = Name, X = -X, Y = -Y, Z = Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate270FlipNone:
                    return new GpsEntry { Name = Name, X = Y, Y = -X, Z = Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.RotateNoneFlipX:
                    return new GpsEntry { Name = Name, X = -X, Y = Y, Z = -Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate90FlipX:
                    return new GpsEntry { Name = Name, X = Y, Y = X, Z = -Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate180FlipX:
                    return new GpsEntry { Name = Name, X = X, Y = -Y, Z = -Z, Description = Description, ShowOnHud = ShowOnHud };
                case RotateFlipType.Rotate270FlipX:
                    return new GpsEntry { Name = Name, X = -Y, Y = -X, Z = -Z, Description = Description, ShowOnHud = ShowOnHud };
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
