using UnityEngine;

namespace Hikaria.NetworkQualityTracker.Utility
{
    internal class Utils
    {
        public static void RGBToHSL(Color color, out float h, out float s, out float l)
        {
            float r = color.r;
            float g = color.g;
            float b = color.b;

            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float delta = max - min;

            // 计算亮度
            l = (max + min) / 2f;

            // 计算色相
            if (delta == 0)
            {
                h = 0f; // 无色
            }
            else if (max == r)
            {
                h = (g - b) / delta;
                if (g < b)
                {
                    h += 6f;
                }
            }
            else if (max == g)
            {
                h = 2f + (b - r) / delta;
            }
            else // max == b
            {
                h = 4f + (r - g) / delta;
            }

            h /= 6f;

            // 计算饱和度
            if (delta == 0)
            {
                s = 0f;
            }
            else
            {
                s = delta / (1f - Math.Abs(2 * l - 1));
            }
        }

        public static Color HSLToRGB(float h, float s, float l)
        {
            if (s == 0f)
            {
                return new Color(l, l, l);
            }

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            float r = HueToRGB(p, q, h + 1f / 3f);
            float g = HueToRGB(p, q, h);
            float b = HueToRGB(p, q, h - 1f / 3f);

            return new Color(r, g, b);
        }

        public static float HueToRGB(float p, float q, float t)
        {
            if (t < 0f)
            {
                t += 1f;
            }
            else if (t > 1f)
            {
                t -= 1f;
            }

            if (t < 1f / 6f)
            {
                return p + (q - p) * 6f * t;
            }
            else if (t < 1f / 2f)
            {
                return q;
            }
            else if (t < 2f / 3f)
            {
                return p + (q - p) * (2f / 3f - t) * 6f;
            }
            else
            {
                return p;
            }
        }
    }
}
