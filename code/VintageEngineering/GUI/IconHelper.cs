using System;
using Cairo;

namespace VintageEngineering.GUI
{
    public class IconHelper
    {
        public IconHelper()
        {

        }

        public static void VerticalBar(Context cr, float width, float height, double lineWidth = 3.0, bool strokeOrFill = true, bool defaultPattern = true)
		{
			//			Pattern pattern = null;
			//			Matrix matrix = cr.Matrix;

			//			cr.Save();
			//			float w = 30;
			//			float h = 100;
			//			float scale = Math.Min(width / w, height / h);
			//			matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
			//			matrix.Scale(scale, scale);
			cr.Operator = Operator.Over;
			cr.LineWidth = lineWidth;
			cr.MiterLimit = 4;
			cr.LineCap = 0;
			cr.LineJoin = 0;			
			if (defaultPattern)
			{
				Pattern pattern = new SolidPattern(0,0,0,1);
				cr.SetSource(pattern);
				pattern.Dispose();
			}
//			cr.Paint();			
			cr.NewPath();
			cr.MoveTo(2, 2);
			cr.LineTo(2, 25);
			cr.LineTo(6, 25);
			cr.LineTo(6, 27);
			cr.LineTo(2, 27);
			cr.LineTo(2, 50);
			cr.LineTo(6, 50);
			cr.LineTo(6, 52);
			cr.LineTo(2, 52);
			cr.LineTo(2, 75);
			cr.LineTo(6, 75);
			cr.LineTo(6, 77);
			cr.LineTo(2, 77);
			cr.LineTo(2, 98);
			cr.LineTo(28, 98);
			cr.LineTo(28, 77);
			cr.LineTo(24, 77);
			cr.LineTo(24, 75);
			cr.LineTo(28, 75);
			cr.LineTo(28, 52);
			cr.LineTo(24, 52);
			cr.LineTo(24, 50);
			cr.LineTo(28, 50);
			cr.LineTo(28, 27);
			cr.LineTo(24, 27);
			cr.LineTo(24, 25);
			cr.LineTo(28, 25);
			cr.LineTo(28, 2);
			cr.ClosePath();
			cr.MoveTo(2, 2);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.None;
//			cr.FillRule = FillRule.Winding;
//			cr.FillPreserve();
			if (strokeOrFill)
            {
				cr.Stroke();				
            }
			else
            {
				cr.Fill();
            }			

//			cr.Restore();
		}

        public static void NewHorizontalBar(Context cr, int x, int y, double[] rgba, double lineWidth = 3.0, bool strokeOrFill = true, bool defaultPattern = true, float width = 100, float height = 25)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            //cr.Save();
            float w = 100;
            float h = 25;
            float wscale = width / w;
			float hscale = height / h;
//            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(wscale, hscale);
            cr.Matrix = matrix;

            if (defaultPattern)
            {
                pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
                cr.SetSource(pattern);
                pattern.Dispose();
            }

            cr.Operator = Operator.Over;
            cr.LineWidth = lineWidth;
            cr.MiterLimit = 4;
            cr.LineCap = 0;
            cr.LineJoin = 0;

            cr.NewPath();            
//            cr.LineTo(100, 0);
//            cr.LineTo(100, 25);
//            cr.LineTo(0, 25);
//            cr.LineTo(0, 0);
//            cr.ClosePath();
            cr.MoveTo(2, 2);
            cr.LineTo(2, 23);
            cr.LineTo(23, 23);
            cr.LineTo(23, 20);
            cr.LineTo(25, 20);
            cr.LineTo(25, 23);
            cr.LineTo(49, 23);
            cr.LineTo(49, 17);
            cr.LineTo(51, 17);
            cr.LineTo(51, 23);
            cr.LineTo(74, 23);
            cr.LineTo(74, 19);
            cr.LineTo(76, 19);
            cr.LineTo(76, 23);
            cr.LineTo(98, 23);
            cr.LineTo(98, 2);
            cr.LineTo(76, 2);
            cr.LineTo(76, 6);
            cr.LineTo(74, 6);
            cr.LineTo(74, 2);
            cr.LineTo(51, 2);
            cr.LineTo(51, 8);
            cr.LineTo(49, 8);
            cr.LineTo(49, 2);
            cr.LineTo(25, 2);
            cr.LineTo(25, 5);
            cr.LineTo(23, 5);
            cr.LineTo(23, 2);
            cr.LineTo(2, 2);
            cr.ClosePath();
            cr.MoveTo(0, 0);

            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.None;
            if (strokeOrFill)
            {
                cr.Stroke();
            }
            else
            {
                cr.Fill();
            }
        }

    }
}
