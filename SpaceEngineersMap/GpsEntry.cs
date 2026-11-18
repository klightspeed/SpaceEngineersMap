using MathNet.Spatial.Euclidean;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public partial record class GpsEntry(
            string Owner,
            bool IsPlayer,
            string Name,
            double X,
            double Y,
            double Z,
            string Description,
            bool ShowOnHud,
            string? StartPart,
            TimeSpan? StartTime,
            string? EndPart,
            TimeSpan? EndTime
        )
    {
        private static readonly Regex PartRE = GetPartRE();

        private static Vector3D Rotate(Vector3D vector, Quaternion rotation)
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

        public ProjectedGpsEntry? ProjectMercator(
                int mult,
                Vector3D planetPos,
                Quaternion planetRotation,
                double minMercatorLon,
                double maxMercatorLon,
                double latMult,
                double lonMult,
                bool northIsYMinus
            )
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.ProjectMercator(mult, coords, minMercatorLon, maxMercatorLon, latMult, lonMult, northIsYMinus);

            if (double.IsNaN(projected.X))
            {
                return null;
            }
            else
            {
                return new ProjectedGpsEntry(this, projected.X, projected.Y, projected.Z);
            }
        }

        public ProjectedGpsEntry? ProjectTransverseMercator(
                int mult,
                Vector3D planetPos,
                Quaternion planetRotation,
                double minMercatorLon,
                double maxMercatorLon,
                double latMult,
                double lonMult,
                bool northIsYMinus,
                double transverseMercatorMeridian
            )
        {
            var coords = Rotate(new Vector3D(X, Y, Z) - planetPos, planetRotation);
            var projected = MapUtils.ProjectTransverseMercator(mult, coords, minMercatorLon, maxMercatorLon, latMult, lonMult, northIsYMinus, transverseMercatorMeridian);

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

        [GeneratedRegex(@"^(?<start>P\d\d\w?\.\d\d\.\d\d\.\d\d)(-(?<end>P\d\d\w?\.\d\d\.\d\d\.\d\d))?")]
        private static partial Regex GetPartRE();
    }

    public record class ProjectedGpsEntry(GpsEntry Original, double X, double Y, double Z)
        : GpsEntry(
            Owner: Original.Owner,
            IsPlayer: Original.IsPlayer,
            Name: Original.Name,
            X: X,
            Y: Y,
            Z: Z,
            Description: Original.Description,
            ShowOnHud: Original.ShowOnHud,
            StartPart: Original.StartPart,
            StartTime: Original.StartTime,
            EndPart: Original.EndPart,
            EndTime: Original.EndTime
        )
    {
        public GpsEntry OriginalEntry { get; } = Original;

        public ProjectedGpsEntry RotateFlip2D(RotateFlipType rotation)
        {
            return rotation switch
            {
                RotateFlipType.Rotate90FlipNone => new ProjectedGpsEntry(OriginalEntry, -Y, X, Z),
                RotateFlipType.Rotate180FlipNone => new ProjectedGpsEntry(OriginalEntry, -X, -Y, Z),
                RotateFlipType.Rotate270FlipNone => new ProjectedGpsEntry(OriginalEntry, Y, -X, Z),
                RotateFlipType.RotateNoneFlipX => new ProjectedGpsEntry(OriginalEntry, -X, Y, -Z),
                RotateFlipType.Rotate90FlipX => new ProjectedGpsEntry(OriginalEntry, Y, X, -Z),
                RotateFlipType.Rotate180FlipX => new ProjectedGpsEntry(OriginalEntry, X, -Y, -Z),
                RotateFlipType.Rotate270FlipX => new ProjectedGpsEntry(OriginalEntry, -Y, -X, -Z),
                _ => new ProjectedGpsEntry(OriginalEntry, X, Y, Z),
            };
        }
    }
}
