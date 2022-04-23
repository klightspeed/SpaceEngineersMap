using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SpaceEngineersMap
{
    public static class TextDrawing
    {
        private struct StringAttach
        {
            public PointF AttachPoint { get; set; }
            public SizeF AlignPoint { get; set; }

            public StringAlignment TextAlignment
            {
                get
                {
                    if (AlignPoint.Width <= 0.25)
                    {
                        return StringAlignment.Near;
                    }
                    else if (AlignPoint.Width >= 0.75)
                    {
                        return StringAlignment.Far;
                    }
                    else
                    {
                        return StringAlignment.Center;
                    }
                }
            }

            public StringAlignment LineAlignment
            {
                get
                {
                    if (AlignPoint.Height <= 0.25)
                    {
                        return StringAlignment.Near;
                    }
                    else if (AlignPoint.Height >= 0.75)
                    {
                        return StringAlignment.Far;
                    }
                    else
                    {
                        return StringAlignment.Center;
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

        public static (Brush TextBrush, Pen OutlinePen, GraphicsPath Path) GetTextPath(Graphics graphics, string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var cmdarg = desc.Split(new[] { ' ' }, 2);
            var margin = 10;
            var linetomargin = 8;
            var linefrommargin = 5;

            if (CommandAttachments.TryGetValue(cmdarg[0], out var attach))
            {
                var fontheight = font.GetHeight(graphics);
                var spacewidth = graphics.MeasureString("| |", font).Width - graphics.MeasureString("||", font).Width;
                var attachpoint = new PointF(attach.AttachPoint.X * margin, attach.AttachPoint.Y * margin);
                var alignpoint = attach.AlignPoint.ToPointF();
                var halign = attach.TextAlignment;
                var valign = attach.LineAlignment;

                var format = new StringFormat
                {
                    Alignment = halign,
                    LineAlignment = valign
                };

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
                        sections = new[] {
                            sections[0].TakeWhile(e => e == "|").Concat(sections[1]).ToArray()
                        };
                    }
                    else if (hidepart2 && !hidepart1)
                    {
                        sections = new[] {
                            sections[0].Concat(sections[1].Reverse().TakeWhile(e => e == "|").Reverse()).ToArray()
                        };
                    }
                }

                var lines = sections.SelectMany(l => l).ToArray();
                var hrules = sections.Select(s => s.Length).ToArray();
                var path = new GraphicsPath();

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == "|")
                    {
                        lines[i] = "";
                    }
                    else if (lines[i].StartsWith("| "))
                    {
                        lines[i] = "  " + lines[i].Substring(2);
                    }
                    else if (lines[i].EndsWith(" |"))
                    {
                        lines[i] = lines[i].Substring(0, lines[i].Length - 2) + "  ";
                    }
                }

                var lineto = new PointF(attach.AttachPoint.X * linetomargin, attach.AttachPoint.Y * margin);

                if (lines.All(e => e == "" || e.StartsWith("  ") || e.EndsWith("  ")))
                {
                    lineto.X -= (attach.AlignPoint.Width - 0.5f) * spacewidth * 4;
                    attachpoint.X -= (attach.AlignPoint.Width - 0.5f) * spacewidth * 4;
                    lines = lines.Select(e => e.Trim()).ToArray();
                }

                int blanklines = 0;

                if (valign == StringAlignment.Near)
                {
                    blanklines = lines.TakeWhile(e => e == "").Count();
                    hrules = hrules.Select(e => e - blanklines).ToArray();
                }
                else if (valign == StringAlignment.Far)
                {
                    blanklines = lines.Reverse().TakeWhile(e => e == "").Count();
                }

                attachpoint.Y -= fontheight * blanklines * (attach.AlignPoint.Height - 0.5f) * 2;
                lineto.Y -= (attach.AlignPoint.Height - 0.5f) * fontheight * blanklines * 2;

                lines = lines.SkipWhile(e => e == "").Reverse().SkipWhile(e => e == "").Reverse().ToArray();

                string text = string.Join("\r\n", lines);
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new PointF(0, 0), format);
                var rect = path.GetBounds();

                float rulepos = rect.Top;
                for (int i = 0; i < hrules.Length - 1; i++)
                {
                    rulepos += hrules[i] * fontheight;
                    path.AddRectangle(new RectangleF(rect.X, rulepos - 2.5f, rect.Width, 1.0f));
                }

                if (halign != StringAlignment.Center || valign != StringAlignment.Center)
                {
                    if (attachpoint.X == 0)
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

                    using (var linepath = new GraphicsPath())
                    {
                        using (var linepen = new Pen(Color.Black, 1.0f))
                        {
                            linepath.AddLine(linefrom, lineto);
                            linepath.Widen(linepen);
                            var linetransform = new Matrix();
                            linetransform.Translate(-attachpoint.X, -attachpoint.Y);
                            linepath.Transform(linetransform);
                            path.AddPath(linepath, false);
                        }
                    }
                }

                var topleft = new PointF(pos.X + attachpoint.X, pos.Y + attachpoint.Y);
                var transform = new Matrix();
                transform.Translate(topleft.X, topleft.Y);
                path.Transform(transform);
                return (textbrush, outlinepen, path);
            }
            else
            {
                return default;
            }
        }

        public static RectangleF? DrawText(Graphics graphics, string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var (_, _, path) = GetTextPath(graphics, desc, pos, font, textbrush, outlinepen, hidepart1, hidepart2);

            if (path != null)
            {
                using (path)
                {
                    graphics.DrawPath(outlinepen, path);
                    graphics.FillPath(textbrush, path);

                    return path.GetBounds(new Matrix(), outlinepen);
                }
            }
            else
            {
                return null;
            }
        }
    }
}
