using System;

namespace Script.TradeNodes.MathX
{
    /// <summary>
    /// Formats SciNot values into short human-readable strings like "1.23K", "45.6M", "7.89e42".
    /// Used for UI display, while SciNot.ToString() stays for debugging.
    /// </summary>
    public static class SciNotFormatter
    {
        // You can expand or localize these later (supports up to 10^33 comfortably)
        private static readonly string[] Prefixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No",
            "Dc", "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Ocd", "Nod"
        };

        /// <summary>
        /// Formats a SciNot value for UI. Automatically chooses between prefix and scientific notation.
        /// </summary>
        public static string Format(SciNot value, int decimalPlaces = 2)
        {
            if (value.IsZero)
                return "0";

            var mantissa = value.Mantissa;
            var exp = value.Exponent;

            // If it's small enough, just show it as a number.
            if (exp is < 3 and > -3)
            {
                if (value.TryToDouble(out var normal))
                    return normal.ToString($"F{decimalPlaces}");
            }

            // Figure out which prefix tier we fit in.
            var tier = exp / 3;
            if (tier >= Prefixes.Length) return $"{mantissa.ToString($"F{decimalPlaces}")}e{exp}";
            // Convert exponent into prefix scale.
            var scaled = mantissa * Math.Pow(10, exp - (tier * 3));
            return $"{scaled.ToString($"F{decimalPlaces}")}{Prefixes[tier]}";

            // If exponent too big, fallback to e-notation.
        }

        /// <summary>
        /// Compact version for tooltips or tight spaces (fewer decimals, no trailing zeros).
        /// </summary>
        public static string Compact(SciNot value)
        {
            if (value.IsZero)
                return "0";

            var mantissa = value.Mantissa;
            var exp = value.Exponent;
            var tier = exp / 3;

            if (tier >= Prefixes.Length) return $"{mantissa:0.##}e{exp}";
            var scaled = mantissa * Math.Pow(10, exp - (tier * 3));
            return $"{scaled:0.#}{Prefixes[tier]}";

        }
    }
}
