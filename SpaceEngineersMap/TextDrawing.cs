using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace SpaceEngineersMap
{
    public static partial class TextDrawing
    {
        private record struct StringAttach(PointF AttachPoint, SizeF AlignPoint)
        {
            public readonly HorizontalAlignment HorizontalAlignment
                => AlignPoint switch
                {
                    { Width: <= 0.25f } => HorizontalAlignment.Left,
                    { Width: >= 0.75f } => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Center
                };

            public readonly TextAlignment TextAlignment
                => AlignPoint switch
                {
                    { Width: <= 0.25f } => TextAlignment.Start,
                    { Width: >= 0.75f } => TextAlignment.End,
                    _ => TextAlignment.Center
                };

            public readonly VerticalAlignment VerticalAlignment
                => AlignPoint switch
                {
                    { Height: <= 0.25f } => VerticalAlignment.Top,
                    { Height: >= 0.75f } => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Center
                };
        }

        private static class Attach
        {
            public static PointF Top = new(0, -1);
            public static PointF Left = new(-1, 0);
            public static PointF Right = new(1, 0);
            public static PointF Bottom = new(0, 1);
            public static PointF Center = new(0, 0);
        }

        private static class Align
        {
            public static SizeF TopLeft = new(0, 0);
            public static SizeF TopCentre = new(0.5f, 0);
            public static SizeF TopRight = new(1, 0);
            public static SizeF MiddleLeft = new(0, 0.5f);
            public static SizeF MiddleRight = new(1, 0.5f);
            public static SizeF BottomLeft = new(0, 1);
            public static SizeF BottomCentre = new(0.5f, 1);
            public static SizeF BottomRight = new(1, 1);
            public static SizeF Center = new(0.5f, 0.5f);
        }

        private static readonly Dictionary<string, StringAttach> CommandAttachments = new()
        {
            ["^>"] = new StringAttach(Attach.Top, Align.BottomLeft),
            ["¯>"] = new StringAttach(Attach.Right, Align.TopLeft),
            ["->"] = new StringAttach(Attach.Right, Align.MiddleLeft),
            ["_>"] = new StringAttach(Attach.Right, Align.BottomLeft),
            ["v>"] = new StringAttach(Attach.Bottom, Align.TopLeft),
            ["<^"] = new StringAttach(Attach.Top, Align.BottomRight),
            ["<¯"] = new StringAttach(Attach.Left, Align.TopRight),
            ["<-"] = new StringAttach(Attach.Left, Align.MiddleRight),
            ["<_"] = new StringAttach(Attach.Left, Align.BottomRight),
            ["<v"] = new StringAttach(Attach.Bottom, Align.TopRight),
            ["^^"] = new StringAttach(Attach.Top, Align.BottomCentre),
            ["vv"] = new StringAttach(Attach.Bottom, Align.TopCentre),
            ["xx"] = new StringAttach(Attach.Center, Align.Center)
        };

        public static (Brush TextBrush, Pen OutlinePen, IPathCollection Paths, string Text) GetTextPath(string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var lrmargin = 0;

            if (GetPipeSpacingRE().IsMatch(desc))
            {
                var split = desc.Split([' '], 2);
                lrmargin = split[0].Length;
                desc = split[1].TrimStart();
            }

            var cmdarg = desc.Split([' '], 2);
            var margin = 10;
            var linetomargin = 8;
            var linefrommargin = 5;

            if (CommandAttachments.TryGetValue(cmdarg[0], out var attach))
            {
                var talign = attach.TextAlignment;
                var halign = attach.HorizontalAlignment;
                var valign = attach.VerticalAlignment;
                var format = new TextOptions(font)
                {
                    TextAlignment = talign,
                    HorizontalAlignment = halign,
                    VerticalAlignment = valign
                };

                var spacewidth = (TextMeasurer.MeasureBounds("|" + new string(' ', 100) + "|", format).Width - TextMeasurer.MeasureBounds("||",format).Width) / 100;
                var fontheight = TextMeasurer.MeasureAdvance(string.Join('\n', Enumerable.Repeat('|', 100)), format).Height / 100;
                var attachpos = new PointF(0, 0);
                var alignpoint = (PointF)attach.AlignPoint;

                var sections =
                    cmdarg[1]
                        .Split([" / ", "\n----\n"], StringSplitOptions.None)
                        .Select(e =>
                            e.Trim(' ')
                             .Split(["  ", "\n"], StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.Trim(' '))
                             .ToArray()
                        )
                        .ToArray();

                if (sections.Length == 2)
                {
                    if (hidepart1 && !hidepart2)
                    {
                        sections = [
                            [.. sections[0].TakeWhile(e => e == "|"), .. sections[1]]
                        ];
                    }
                    else if (hidepart2 && !hidepart1)
                    {
                        sections = [
                            [.. sections[0], .. sections[1].Reverse().TakeWhile(e => e == "|").Reverse()]
                        ];
                    }
                }

                var lines = sections.SelectMany(l => l).ToArray();
                var hrules = sections.Select(s => s.Length).ToArray();

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == "|")
                    {
                        lines[i] = "";
                    }
                    else if (lines[i].StartsWith("| "))
                    {
                        lines[i] = string.Concat("  ", lines[i].AsSpan(2));
                    }
                    else if (lines[i].EndsWith(" |"))
                    {
                        lines[i] = string.Concat(lines[i].AsSpan(0, lines[i].Length - 2), "  ");
                    }
                }

                attachpos.X -= lrmargin * (attach.AlignPoint.Width - 0.5f) * spacewidth * 2;

                if (lines.All(e => e == "" || e.StartsWith("  ") || e.EndsWith("  ")))
                {
                    attachpos.X -= (attach.AlignPoint.Width - 0.5f) * spacewidth * 4;
                    lines = [.. lines.Select(e => e.Trim())];
                }

                int blanklines = 0;

                if (valign == VerticalAlignment.Top)
                {
                    blanklines = lines.TakeWhile(e => e == "").Count();
                    hrules = [.. hrules.Select(e => e - blanklines)];
                }
                else if (valign == VerticalAlignment.Bottom)
                {
                    blanklines = lines.Reverse().TakeWhile(e => e == "").Count();
                }

                attachpos.Y -= fontheight * blanklines * (attach.AlignPoint.Height - 0.5f) * 2;

                lines = [.. lines.SkipWhile(e => e == "").Reverse().SkipWhile(e => e == "").Reverse()];

                string text = string.Join("\r\n", lines);
                var glyphs = TextBuilder.GenerateGlyphs(text, format);
                var rect = glyphs.Bounds;
                var paths = glyphs.ToList();

                for (int i = 0; i < hrules.Length - 1; i++)
                {
                    var rulepos = fontheight * hrules[i];
                    var uline = hrules[i] <= 0 ? "" : lines[hrules[i] - 1];
                    var lline = hrules[i] >= lines.Length ? "" : lines[hrules[i]];
                    var hrulebounds = TextMeasurer.MeasureBounds(uline + "\n" + lline, format);
                    var hrulelen = hrulebounds.Width;
                    var hrulemargin = hrulebounds.X;

                    switch (format.VerticalAlignment)
                    {
                        case VerticalAlignment.Bottom:
                            rulepos -= fontheight * lines.Length;
                            break;
                        case VerticalAlignment.Center:
                            rulepos -= fontheight * lines.Length / 2;
                            break;
                    }

                    paths.Add(new RectangularPolygon(new RectangleF(hrulemargin, rulepos - 0.5f, hrulelen, 1.0f)));
                }

                var attachpoint = new PointF(attach.AttachPoint.X * margin, attach.AttachPoint.Y * margin) + attachpos;
                var lineto = new PointF(attach.AttachPoint.X * linetomargin, attach.AttachPoint.Y * linetomargin) + attachpos;

                if (halign != HorizontalAlignment.Center || valign != VerticalAlignment.Center)
                {
                    if (attach.AttachPoint.X == 0)
                    {
                        lineto.X -= (attach.AlignPoint.Width - 0.5f) * spacewidth * 2;
                    }
                    else
                    {
                        lineto.Y -= (attach.AlignPoint.Height - 0.5f) * fontheight * lines.Length * 0.5f;
                    }

                    PointF linefrom;

                    if (Math.Abs(lineto.X) > Math.Abs(lineto.Y))
                    {
                        linefrom = new PointF(Math.Sign(lineto.X) * linefrommargin, (lineto.Y / Math.Abs(lineto.X)) * linefrommargin);
                    }
                    else
                    {
                        linefrom = new PointF((lineto.X / Math.Abs(lineto.Y)) * linefrommargin, Math.Sign(lineto.Y) * linefrommargin);
                    }

                    if (Math.Abs(linefrom.Y - lineto.Y) > 100)
                    {

                    }

                    var linepath = new Path(new LinearLineSegment(linefrom - attachpoint, lineto - attachpoint)).GenerateOutline(1.0f, JointStyle.Round, EndCapStyle.Round);
                    paths.Add(linepath);
                }

                var topleft = pos + attachpoint;
                var path = new PathCollection(paths).Translate(topleft);

                return (textbrush, outlinepen, path, desc);
            }
            else
            {
                return default;
            }
        }

        public static void DrawText(
                List<(DrawingOps.IDrawingOp Op, bool IncludeInBounds, object? info)> ops,
                string desc,
                PointF pos,
                Font font,
                Brush textbrush,
                Pen outlinepen,
                bool hidepart1,
                bool hidepart2,
                bool includeInBounds = false,
                bool noOutline = false,
                bool noFill = false
            )
        {
            if (GetTextPath(desc, pos, font, textbrush, outlinepen, hidepart1, hidepart2) is (Brush brush, Pen pen, IPathCollection path, string text))
            {
                if (!noOutline)
                {
                    ops.Add((
                        new DrawingOps.StrokedPath(pen, path),
                        includeInBounds,
                        new
                        {
                            Type = "text_outline",
                            Text = text
                        }
                    ));
                }

                if (!noFill)
                {
                    ops.Add((
                        new DrawingOps.FilledPath(brush, path),
                        noOutline && includeInBounds,
                        new
                        {
                            Type = "text_fill",
                            Text = text
                        }
                    ));
                }
            }
        }

        [GeneratedRegex("^[|]+ ")]
        private static partial Regex GetPipeSpacingRE();
    }
}
