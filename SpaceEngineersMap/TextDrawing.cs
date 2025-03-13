using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpaceEngineersMap
{
    public static class TextDrawing
    {
        private struct StringAttach
        {
            public PointF AttachPoint { get; set; }
            public SizeF AlignPoint { get; set; }

            public HorizontalAlignment HorizontalAlignment
            {
                get
                {
                    if (AlignPoint.Width <= 0.25)
                    {
                        return HorizontalAlignment.Left;
                    }
                    else if (AlignPoint.Width >= 0.75)
                    {
                        return HorizontalAlignment.Right;
                    }
                    else
                    {
                        return HorizontalAlignment.Center;
                    }
                }
            }

            public TextAlignment TextAlignment
            {
                get
                {
                    if (AlignPoint.Width <= 0.25)
                    {
                        return TextAlignment.Start;
                    }
                    else if (AlignPoint.Width >= 0.75)
                    {
                        return TextAlignment.End;
                    }
                    else
                    {
                        return TextAlignment.Center;
                    }
                }
            }

            public VerticalAlignment VerticalAlignment
            {
                get
                {
                    if (AlignPoint.Height <= 0.25)
                    {
                        return VerticalAlignment.Top;
                    }
                    else if (AlignPoint.Height >= 0.75)
                    {
                        return VerticalAlignment.Bottom;
                    }
                    else
                    {
                        return VerticalAlignment.Center;
                    }
                }
            }

            public StringAttach(PointF attach, SizeF align)
            {
                AttachPoint = attach;
                AlignPoint = align;
            }
        }

        private static class Attach
        {
            public static PointF Top = new PointF(0, -1);
            public static PointF Left = new PointF(-1, 0);
            public static PointF Right = new PointF(1, 0);
            public static PointF Bottom = new PointF(0, 1);
            public static PointF Center = new PointF(0, 0);
        }

        private static class Align
        {
            public static SizeF TopLeft = new SizeF(0, 0);
            public static SizeF TopCentre = new SizeF(0.5f, 0);
            public static SizeF TopRight = new SizeF(1, 0);
            public static SizeF MiddleLeft = new SizeF(0, 0.5f);
            public static SizeF MiddleRight = new SizeF(1, 0.5f);
            public static SizeF BottomLeft = new SizeF(0, 1);
            public static SizeF BottomCentre = new SizeF(0.5f, 1);
            public static SizeF BottomRight = new SizeF(1, 1);
            public static SizeF Center = new SizeF(0.5f, 0.5f);
        }

        private static Dictionary<string, StringAttach> CommandAttachments = new Dictionary<string, StringAttach>
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

        public static (Brush TextBrush, Pen OutlinePen, IPathCollection Paths) GetTextPath(string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var lrmargin = 0;

            if (Regex.IsMatch(desc, "^[|]+ "))
            {
                var split = desc.Split(new[] { ' ' }, 2);
                lrmargin = split[0].Length;
                desc = split[1].TrimStart();
            }

            var cmdarg = desc.Split(new[] { ' ' }, 2);
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
                        .Split(new[] { " / ", "\n----\n" }, StringSplitOptions.None)
                        .Select(e =>
                            e.Trim(' ')
                             .Split(new[] { "  ", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.Trim(' '))
                             .ToArray()
                        )
                        .ToArray();

                if (sections.Length == 2)
                {
                    if (hidepart1 && !hidepart2)
                    {
                        sections = [
                            sections[0].TakeWhile(e => e == "|").Concat(sections[1]).ToArray()
                        ];
                    }
                    else if (hidepart2 && !hidepart1)
                    {
                        sections = [
                            sections[0].Concat(sections[1].Reverse().TakeWhile(e => e == "|").Reverse()).ToArray()
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
                    lines = lines.Select(e => e.Trim()).ToArray();
                }

                int blanklines = 0;

                if (valign == VerticalAlignment.Top)
                {
                    blanklines = lines.TakeWhile(e => e == "").Count();
                    hrules = hrules.Select(e => e - blanklines).ToArray();
                }
                else if (valign == VerticalAlignment.Bottom)
                {
                    blanklines = lines.Reverse().TakeWhile(e => e == "").Count();
                }

                attachpos.Y -= fontheight * blanklines * (attach.AlignPoint.Height - 0.5f) * 2;

                lines = lines.SkipWhile(e => e == "").Reverse().SkipWhile(e => e == "").Reverse().ToArray();

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

                return (textbrush, outlinepen, path);
            }
            else
            {
                return default;
            }
        }

        public static RectangleF? DrawText(Image<Argb32> img, string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var (_, _, path) = GetTextPath(desc, pos, font, textbrush, outlinepen, hidepart1, hidepart2);

            if (path != null)
            {
                img.Mutate(g =>
                {
                    g.Draw(outlinepen, path);
                    g.Fill(textbrush, path);
                });

                var bounds = path.Bounds;

                bounds.Inflate(outlinepen.StrokeWidth, outlinepen.StrokeWidth);

                return bounds;
            }
            else
            {
                return null;
            }
        }
    }
}
