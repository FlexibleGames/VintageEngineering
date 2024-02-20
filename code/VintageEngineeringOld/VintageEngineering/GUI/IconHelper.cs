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

		public static void HorizontalBar(Context cr, double[] rgba, double lineWidth = 1.0, bool strokeOrFill = true, bool defaultPattern = true, float width = 40, float height = 10)
		{
			Pattern pattern = null;
			Matrix matrix = cr.Matrix;

			cr.Save();
			float w = 40;
			float h = 10;
			float scale = Math.Min(width / w, height / h);
			//matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
			matrix.Scale(scale, scale);
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
			cr.MoveTo(10, 1);
			cr.LineTo(1, 1);
			cr.LineTo(1, 9);
			cr.LineTo(10, 9);
			cr.LineTo(10, 8);
			cr.LineTo(11, 8);
			cr.LineTo(11, 9);
			cr.LineTo(20, 9);
			cr.LineTo(20, 6);
			cr.LineTo(21, 6);
			cr.LineTo(21, 9);
			cr.LineTo(30, 9);
			cr.LineTo(30, 8);
			cr.LineTo(31, 8);
			cr.LineTo(31, 9);
			cr.LineTo(39, 9);
			cr.LineTo(39, 1);
			cr.LineTo(31, 0);
			cr.LineTo(31, 2);
			cr.LineTo(30, 2);
			cr.LineTo(30, 1);
			cr.LineTo(20, 1);
			cr.LineTo(20, 4);
			cr.LineTo(20, 4);
			cr.LineTo(20, 1);
			cr.LineTo(11, 1);
			cr.LineTo(11, 2);
			cr.LineTo(10, 2);
			cr.ClosePath();
			cr.MoveTo(10, 1);
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
