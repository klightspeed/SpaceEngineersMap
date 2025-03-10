using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        public virtual string StartPart { get; private set; }
        public virtual TimeSpan? StartTime { get; private set; }
        public virtual string EndPart { get; private set; }
        public virtual TimeSpan? EndTime { get; private set; }

        private static Regex PartRE = new Regex(@"^(?<start>P\d\d\w?\.\d\d\.\d\d\.\d\d)(-(?<end>P\d\d\w?\.\d\d\.\d\d\.\d\d))?");

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
                    OriginalEntry = this,
                    StartPart = StartPart,
                    StartTime = StartTime,
                    EndPart = EndPart,
                    EndTime = EndTime,
                };
            }
        }

        public ProjectedGpsEntry ProjectMercator(int mult, Vector3D planetPos, Quaternion planetRotation, double minMercatorLon, double maxMercatorLon, double latMult, double lonMult)
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.ProjectMercator(mult, coords, minMercatorLon, maxMercatorLon, latMult, lonMult);

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
                    OriginalEntry = this,
                    StartPart = StartPart,
                    StartTime = StartTime,
                    EndPart = EndPart,
                    EndTime = EndTime,
                };
            }
        }

        public static GpsEntry FromXML(XElement xe, string owner, bool isPlayer)
        {
            var coords = xe.Element("coords");

            var name = xe.Element("name").Value;
            string startpart = null;
            TimeSpan? starttime = null;
            string endpart = null;
            TimeSpan? endtime = null;

            if (PartRE.Match(name) is Match partmatch && partmatch.Success)
            {
                var startparts = partmatch.Groups["start"].Value.Split('.');
                var endparts = partmatch.Groups["end"]?.Value.Split(".");
                startpart = startparts[0];
                starttime = new TimeSpan(int.Parse(startparts[1]), int.Parse(startparts[2]), int.Parse(startparts[3]));

                if (endparts?.Length == 4)
                {
                    endpart = endparts[0];
                    endtime = new TimeSpan(int.Parse(endparts[1]), int.Parse(endparts[2]), int.Parse(endparts[3]));
                }
            }

            return new GpsEntry
            {
                Owner = owner,
                IsPlayer = isPlayer,
                Name = xe.Element("name").Value,
                X = double.Parse(coords.Element("X").Value),
                Y = double.Parse(coords.Element("Y").Value),
                Z = double.Parse(coords.Element("Z").Value),
                Description = xe.Element("description").Value,
                ShowOnHud = xe.Element("showOnHud").Value == "true",
                StartPart = startpart,
                StartTime = starttime,
                EndPart = endpart,
                EndTime = endtime,
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
        public override string StartPart => OriginalEntry.StartPart;
        public override TimeSpan? StartTime => OriginalEntry.StartTime;
        public override string EndPart => OriginalEntry.EndPart;
        public override TimeSpan? EndTime => OriginalEntry.EndTime;

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
