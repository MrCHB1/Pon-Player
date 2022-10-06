using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pon_Player.Misc
{
    public static class Colors
    {
        public static int HSVtoRGB(double hue, double sat, double val)
        {
            double c = sat * val;
            double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
            double m = val - c;
            double r = 0, g = 0, b = 0;
            if (hue>=0&&hue<60)
            {
                r = c;
                g = x;
            }
            else if (hue >= 60 && hue < 120)
            {
                r = x;
                g = c;
            }
            else if (hue >= 120 && hue < 180)
            {
                g = c;
                b = x;
            }
            else if (hue >= 180 && hue < 240)
            {
                g = x;
                b = c;
            }
            else if (hue >= 240 && hue < 300)
            {
                b = c;
                r = x;
            }
            else if (hue >= 300 && hue < 360)
            {
                b = x;
                r = c;
            }
            r = (int)((r + m) * 255);
            g = (int)((g + m) * 255);
            b = (int)((b + m) * 255);

            return ((int)r << 24) | ((int)g << 16) | ((int)b << 8) | 0xFF;
        }
    }
}
