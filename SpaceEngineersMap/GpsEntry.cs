using MathNet.Spatial.Euclidean;
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

        private Vector3D Rotate(Vector3D vector, Quaternion rotation)
        {
            var u = new Vector3D(rotation.ImagX, rotation.ImagY, rotation.ImagZ);
            var s = rotation.Real;

            return 2 * u.DotProduct(vector) * u + (s * s - u.DotProduct(u)) * vector + 2 * s * u.CrossProduct(vector);
        }

        public GpsEntry Project(int mult, int maxval, Vector3D planetPos, Quaternion planetRotation, CubeFace face, bool rotate45)
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.Project(coords, mult, maxval, face, rotate45);

            if (double.IsNaN(projected.X))
            {
                return null;
            }
            else
            {
                return new GpsEntry
                {
                    Name = Name,
                    X = projected.X,
                    Y = projected.Y,
                    Z = projected.Z,
                    Description = Description,
                    ShowOnHud = ShowOnHud
                };
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
