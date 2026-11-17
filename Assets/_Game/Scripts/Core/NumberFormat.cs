using System;
using System.Globalization;

namespace Roglike.Core
{
    /// <summary>큰 수를 한국식 단위로 표기한다. 예: 12,345 → 1.2만, 123,456,789 → 1.2억.</summary>
    public static class NumberFormat
    {
        private static readonly (double Unit, string Suffix)[] Units =
        {
            (1e16, "경"),
            (1e12, "조"),
            (1e8, "억"),
            (1e4, "만"),
        };

        public static string ToKorean(double value)
        {
            double abs = Math.Abs(value);
            if (abs < 1e4)
            {
                return Math.Floor(value).ToString("N0", CultureInfo.InvariantCulture);
            }

            foreach (var (unit, suffix) in Units)
            {
                if (abs < unit)
                {
                    continue;
                }

                double scaled = value / unit;
                string format = Math.Abs(scaled) >= 100 ? "0" : "0.#";
                return scaled.ToString(format, CultureInfo.InvariantCulture) + suffix;
            }

            return value.ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
