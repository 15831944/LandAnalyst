using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web;

namespace Loowoo.LandAnalyst.WebInterface
{
    public class ImageGenerator
    {
        public static Bitmap Generate(LwPolyline line, Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height);

            Graphics g = Graphics.FromImage(bmp);
            DrawPolyline(bmp.Size, line, g);
            return bmp;
        }

        private static void DrawPolyline(Size destSize, LwPolyline line, Graphics g)
        {
            var list = line.Vertexes.Select(x => x.Location).ToList();

            float ratio, xoffset, yoffset;
            CalcTransform(destSize, list, out ratio, out xoffset, out yoffset);

            var pt1 = list[0];
            var pt2 = list[list.Count - 1];
            if (Math.Abs(pt1.X - pt2.X) > double.Epsilon || Math.Abs(pt1.Y - pt2.Y) > double.Epsilon)
            {
                list.Add(new Vector2(pt1.X, pt1.Y));
            }

            Pen pen = new Pen(Color.FromArgb(255, 0, 0));
            pen.Width = 2;

            var pts =
                list.Select(
                    x => new System.Drawing.Point((int)((x.X - xoffset) * ratio), destSize.Height - (int)((x.Y - yoffset) * ratio))).ToArray();
            g.DrawLines(pen, pts);

        }

        private static void CalcTransform(Size destSize, IList<Vector2> vertexes, out float ratio, out float xoffset, out float yoffset)
        {
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            foreach (var v in vertexes)
            {
                if (v.X > maxx)
                    maxx = (float)v.X;
                if (v.X < minx)
                    minx = (float)v.X;
                if (v.Y > maxy)
                    maxy = (float)v.Y;
                if (v.Y < miny)
                    miny = (float)v.Y;
            }

            float ratiox = destSize.Width / (maxx - minx);
            float ratioy = destSize.Height / (maxy - miny);

            ratio = ratiox < ratioy ? ratiox : ratioy;
            ratio = (float)(ratio / 1.1);
            //xoffset = minx;
            //yoffset = miny;
            xoffset = minx - (destSize.Width / ratio - (maxx - minx)) / 2;
            yoffset = miny - (destSize.Height / ratio - (maxy - miny)) / 2;
        }
    }
}