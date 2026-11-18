using MathNet.Spatial.Euclidean;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public record class GpsEntry(
            string? Owner,
            bool IsPlayer,
            string? Name,
            double X,
            double Y,
            double Z,
            string? Description,
            bool ShowOnHud,
            string? StartPart,
            TimeSpan? StartTime,
            string? EndPart,
            TimeSpan? EndTime
        )
    {
        private static Regex PartRE = new Regex(@"^(?<start>P\d\d\w?\.\d\d\.\d\d\.\d\d)(-(?<end>P\d\d\w?\.\d\d\.\d\d\.\d\d))?");

        private Vector3D Rotate(Vector3D vector, Quaternion rotation)
        {
            var u = new Vector3D(rotation.ImagX, rotation.ImagY, rotation.ImagZ);
            var s = rotation.Real;

            return 2 * u.DotProduct(vector) * u + (s * s - u.DotProduct(u)) * vector + 2 * s * u.CrossProduct(vector);
        }

        public ProjectedGpsEntry? Project(int mult, int maxval, Vector3D planetPos, Quaternion planetRotation, CubeFace face, bool rotate45)
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.Project(coords, mult, maxval, face, rotate45);

            if (double.IsNaN(projected.X))
            {
                return null;
            }
            else
            {
                return new ProjectedGpsEntry(this, projected.X, projected.Y, projected.Z);
            }
        }

        public ProjectedGpsEntry? ProjectMercator(int mult, Vector3D planetPos, Quaternion planetRotation, double minMercatorLon, double maxMercatorLon, double latMult, double lonMult, bool northIsYMinus, bool transverseMercator, double transverseMercatorMeridian)
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.ProjectMercator(mult, coords, minMercatorLon, maxMercatorLon, latMult, lonMult, northIsYMinus, transverseMercator, transverseMercatorMeridian);

            if (double.IsNaN(projected.X))
            {
                return null;
            }
            else
            {
                return new ProjectedGpsEntry(this, projected.X, projected.Y, projected.Z);
            }
        }

        public static GpsEntry? FromXML(XElement xe, string owner, bool isPlayer)
        {
            var coords = xe.Element("coords");

            if (coords?.Element("X")?.Value is not string xval
                || coords.Element("Y")?.Value is not string yval
                || coords.Element("Z")?.Value is not string zval
                || xe.Element("name")?.Value is not string name)
            {
                return null;
            }

            string? startpart = null;
            TimeSpan? starttime = null;
            string? endpart = null;
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

            return new GpsEntry(
                Owner: owner,
                IsPlayer: isPlayer,
                Name: name,
                X: double.Parse(xval),
                Y: double.Parse(yval),
                Z: double.Parse(zval),
                Description: xe.Element("description")?.Value ?? "",
                ShowOnHud: xe.Element("showOnHud")?.Value == "true",
                StartPart: startpart,
                StartTime: starttime,
                EndPart: endpart,
                EndTime: endtime
            );
        }
    }

    public record class ProjectedGpsEntry(GpsEntry original, double x, double y, double z)
        : GpsEntry(
            Owner: original.Owner,
            IsPlayer: original.IsPlayer,
            Name: original.Name,
            X: x,
            Y: y,
            Z: z,
            Description: original.Description,
            ShowOnHud: original.ShowOnHud,
            StartPart: original.StartPart,
            StartTime: original.StartTime,
            EndPart: original.EndPart,
            EndTime: original.EndTime
        )
    {
        public GpsEntry OriginalEntry { get; } = original;

        public ProjectedGpsEntry RotateFlip2D(RotateFlipType rotation)
        {
            switch (rotation)
            {
                case RotateFlipType.RotateNoneFlipNone:
                default:
                    return new ProjectedGpsEntry(OriginalEntry, X, Y, Z);
                case RotateFlipType.Rotate90FlipNone:
                    return new ProjectedGpsEntry(OriginalEntry, -Y, X, Z);
                case RotateFlipType.Rotate180FlipNone:
                    return new ProjectedGpsEntry(OriginalEntry, -X, -Y, Z);
                case RotateFlipType.Rotate270FlipNone:
                    return new ProjectedGpsEntry(OriginalEntry, Y, -X, Z);
                case RotateFlipType.RotateNoneFlipX:
                    return new ProjectedGpsEntry(OriginalEntry, -X, Y, -Z);
                case RotateFlipType.Rotate90FlipX:
                    return new ProjectedGpsEntry(OriginalEntry, Y, X, -Z);
                case RotateFlipType.Rotate180FlipX:
                    return new ProjectedGpsEntry(OriginalEntry, X, -Y, -Z);
                case RotateFlipType.Rotate270FlipX:
                    return new ProjectedGpsEntry(OriginalEntry, -Y, -X, -Z);
            }
        }
    }
}
