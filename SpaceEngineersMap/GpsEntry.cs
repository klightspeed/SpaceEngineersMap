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
        public virtual string Owner { get; private set; }
        public virtual bool IsPlayer { get; private set; }
        public virtual string Name { get; private set; }
        public double X { get; protected set; }
        public double Y { get; protected set; }
        public double Z { get; protected set; }
        public virtual string Description { get; private set; }
        public virtual bool ShowOnHud { get; private set; }

        private Vector3D Rotate(Vector3D vector, Quaternion rotation)
        {
            var u = new Vector3D(rotation.ImagX, rotation.ImagY, rotation.ImagZ);
            var s = rotation.Real;

            return 2 * u.DotProduct(vector) * u + (s * s - u.DotProduct(u)) * vector + 2 * s * u.CrossProduct(vector);
        }

        public ProjectedGpsEntry Project(int mult, int maxval, Vector3D planetPos, Quaternion planetRotation, CubeFace face, bool rotate45)
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.Project(coords, mult, maxval, face, rotate45);

            if (double.IsNaN(projected.X))
            {
                return null;
            }
            else
            {
                return new ProjectedGpsEntry
                {
                    Owner = Owner,
                    IsPlayer = IsPlayer,
                    Name = Name,
                    X = projected.X,
                    Y = projected.Y,
                    Z = projected.Z,
                    Description = Description,
                    ShowOnHud = ShowOnHud,
                    OriginalEntry = this
                };
            }
        }

        public static GpsEntry FromXML(XElement xe, string owner, bool isPlayer)
        {
            var coords = xe.Element("coords");

            return new GpsEntry
            {
                Owner = owner,
                IsPlayer = isPlayer,
                Name = xe.Element("name").Value,
                X = double.Parse(coords.Element("X").Value),
                Y = double.Parse(coords.Element("Y").Value),
                Z = double.Parse(coords.Element("Z").Value),
                Description = xe.Element("description").Value,
                ShowOnHud = xe.Element("showOnHud").Value == "true"
            };
        }
    }

    public class ProjectedGpsEntry : GpsEntry
    {
        public GpsEntry OriginalEntry { get; set; }
        public override string Name => OriginalEntry.Name;
        public override string Owner => OriginalEntry.Owner;
        public override bool IsPlayer => OriginalEntry.IsPlayer;
        public override string Description => OriginalEntry.Description;
        public override bool ShowOnHud => OriginalEntry.ShowOnHud;

        public ProjectedGpsEntry RotateFlip2D(RotateFlipType rotation)
        {
            switch (rotation)
            {
                case RotateFlipType.RotateNoneFlipNone:
                default:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = X, Y = Y, Z = Z };
                case RotateFlipType.Rotate90FlipNone:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = -Y, Y = X, Z = Z };
                case RotateFlipType.Rotate180FlipNone:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = -X, Y = -Y, Z = Z };
                case RotateFlipType.Rotate270FlipNone:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = Y, Y = -X, Z = Z };
                case RotateFlipType.RotateNoneFlipX:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = -X, Y = Y, Z = -Z };
                case RotateFlipType.Rotate90FlipX:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = Y, Y = X, Z = -Z };
                case RotateFlipType.Rotate180FlipX:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = X, Y = -Y, Z = -Z };
                case RotateFlipType.Rotate270FlipX:
                    return new ProjectedGpsEntry { OriginalEntry = OriginalEntry, X = -Y, Y = -X, Z = -Z };
            }
        }
    }
}
