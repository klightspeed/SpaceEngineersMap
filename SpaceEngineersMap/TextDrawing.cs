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
            ["vv"] = new StringAttach(Attach.Bottom, Align.TopCentre)
        };


        public static RectangleF? DrawText(Graphics graphics, string desc, PointF pos, Font font, Brush textbrush, Pen outlinepen, bool hidepart1, bool hidepart2)
        {
            var cmdarg = desc.Split(new[] { ' ' }, 2);
            var margin = 10;

            if (CommandAttachments.TryGetValue(cmdarg[0], out var attach))
            {
                var attachpoint = new PointF(attach.AttachPoint.X * margin, attach.AttachPoint.Y * margin);
                var alignpoint = attach.AlignPoint.ToPointF();
                var halign = attach.TextAlignment;
                var valign = attach.LineAlignment;

                var format = new StringFormat
                {
                    Alignment = halign,
                    LineAlignment = valign
                };

                var sections = cmdarg[1].Split(new[] { " / " }, StringSplitOptions.None);

                if (hidepart1 && !hidepart2 && sections.Length > 1)
                {
                    sections = sections.Skip(1).ToArray();
                }
                else if (hidepart2 && !hidepart1)
                {
                    sections = new[] { sections[0] };
                }

                var sectionlines = sections.Select(s => s.Split(new[] { "  ", }, StringSplitOptions.None)).ToArray();
                var lines = sectionlines.SelectMany(l => l).ToArray();
                var hrules = sectionlines.Select(s => s.Length).ToArray();

                using (var path = new GraphicsPath())
                {
                    string text = string.Join("\r\n", lines);
                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new PointF(0, 0), format);
                    var fontheight = font.GetHeight(graphics);
                    var rect = path.GetBounds();

                    float rulepos = rect.Top;
                    for (int i = 0; i < hrules.Length - 1; i++)
                    {
                        rulepos += hrules[i] * fontheight;
                        path.AddRectangle(new RectangleF(rect.X, rulepos - 2.5f, rect.Width, 1.0f));
                    }

                    var topleft = new PointF(pos.X + attachpoint.X, pos.Y + attachpoint.Y);
                    var transform = new Matrix();
                    transform.Translate(topleft.X, topleft.Y);
                    path.Transform(transform);

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
