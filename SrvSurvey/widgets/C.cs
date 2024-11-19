﻿using System;
using System.Drawing.Drawing2D;
using DPen = System.Drawing.Pen;

namespace SrvSurvey.widgets
{
    /// <summary>
    /// Repository of all named colors, pens and brushes
    /// </summary>
    internal static class C
    {
        // seed with default values
        private static readonly Theme theme = Theme.loadTheme();

        /// <summary> Get a named colour </summary>
        public static Color c(string name)
        {
            if (!theme.ContainsKey(name)) throw new Exception($"Unexpected color name: {name}");
            return theme[name];
        }

        #region common colors

        public static Color orange = c("orange");
        public static Color orangeDark = c("orangeDark");

        public static Color cyan = c("cyan");
        public static Color cyanDark = c("cyanDark");

        public static Color red = c("red");

        public static Color yellow = c("yellow");

        #endregion

        public static class Pens
        {
            public static Pen orange1 = orange.toPen(1);
            public static Pen orange2 = orange.toPen(2);

            public static Pen orangeDark1 = orangeDark.toPen(1);

            public static Pen cyan1 = cyan.toPen(1);

            public static Pen cyanDark1 = cyanDark.toPen(1);
        }

        public static class Brushes
        {
            public static Brush orange = C.orange.toBrush();
            public static Brush orangeDark = C.orangeDark.toBrush();
            public static Brush cyan = C.cyan.toBrush();
            public static Brush cyanDark = C.cyanDark.toBrush();

            public static Brush black = Color.Black.toBrush();
        }

        internal class Bio
        {
            public static Color gold = c("bio.gold");
            public static Brush brushGold = gold.toBrush();

            public static Brush brushUnknown = c("bio.unknown").toBrush();
            public static Brush brushHatch = new HatchBrush(HatchStyle.DarkUpwardDiagonal, c("bio.hatch"), Color.Transparent);
        }
    }

    static class DrawingExtensions
    {
        public static SolidBrush toBrush(this Color color)
        {
            return new SolidBrush(color);
        }

        public static Pen toPen(this Color color, float width)
        {
            return new Pen(color, width * GameColors.scaleFactor);
        }

        public static Pen toPen(this Color color, float width, LineCap lineCap)
        {
            return new Pen(color, width * GameColors.scaleFactor)
            {
                StartCap = lineCap,
                EndCap = lineCap,
            };
        }

        public static Pen toPen(this Color color, float width, DashStyle dashStyle)
        {
            return new Pen(color, width * GameColors.scaleFactor)
            {
                DashStyle = dashStyle,
            };
        }

        /// <summary>
        /// Draw a line relative to the first point.
        /// </summary>
        public static void DrawLineR(this Graphics g, Pen pen, float x, float y, float dx, float dy)
        {
            g.DrawLine(pen, x, y, x + dx, y + dy);
        }


        /// <summary>
        /// Adjust graphics transform, calls the lambda then reverses the adjustments.
        /// </summary>
        public static void Adjust(this Graphics g, float rot, Action func)
        {
            DrawingExtensions.Adjust(g, 0, 0, 0, rot, func);
        }

        /// <summary>
        /// Adjust graphics transform, calls the lambda then reverses the adjustments.
        /// </summary>
        public static void Adjust(this Graphics g, PointF pf, float rot, Action func)
        {
            DrawingExtensions.Adjust(g, 0, pf.X, pf.Y, rot, func);
        }
        /// <summary>
        /// Adjust graphics transform, calls the lambda then reverses the adjustments.
        /// </summary>
        public static void Adjust(this Graphics g, float x, float y, float rot, Action func)
        {
            DrawingExtensions.Adjust(g, 0, x, y, rot, func);
        }

        /// <summary>
        /// Adjust graphics transform, calls the lambda then reverses the adjustments.
        /// </summary>
        public static void Adjust(this Graphics g, float rot1, float x, float y, float rot2, Action func)
        {
            g.RotateTransform(+rot1);
            // Y value only is inverted
            g.TranslateTransform(+x, -y);
            g.RotateTransform(+rot2);

            func();

            g.RotateTransform(-rot2);
            g.TranslateTransform(-x, +y);
            g.RotateTransform(-rot1);
        }
    }
}