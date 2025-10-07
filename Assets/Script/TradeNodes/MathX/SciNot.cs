using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Script.TradeNodes.MathX
{
    /// <summary>
    /// Scientific-notation-ish large number: mantissa * 10^exponent.
    /// Accurate for small magnitudes, approximate for huge ones.
    /// Designer-friendly: serializes in the Unity inspector.
    /// </summary>
    [Serializable]
    public struct SciNot : IComparable<SciNot>, IEquatable<SciNot>
    {
        /// <summary>
        /// For |exponent| <= this, math falls back to double for exact-ish small-number ops.
        /// </summary>
        public const int ExactExponentRange = 12;

        /// <summary>
        /// If exponent difference exceeds this, smaller addend is ignored in +/-.
        /// </summary>
        public const int IgnoreExpDiff = 15;

        /// <summary>
        /// Mantissa is normalized to [1,10) (or zero). Tolerance around 1.0/10.0 to avoid flapping.
        /// </summary>
        private const double NormalizeEps = 1e-12;

        // ----------------------- Serialized Core Fields --------------------------
        [SerializeField] private double _mantissa;  // normalized so 1 <= |mantissa| < 10 (unless zero)
        [SerializeField] private int _exponent;

        // ----------------------------- Constructors -----------------------------
        public SciNot(double mantissa, int exponent)
        {
            this._mantissa = mantissa;
            this._exponent = exponent;
            Normalize(ref this._mantissa, ref this._exponent);
        }

        public static SciNot Zero => new SciNot(0.0, 0);
        public static SciNot One  => new SciNot(1.0, 0);

        // ------------------------------ Properties ------------------------------
        public double Mantissa => _mantissa;
        public int Exponent => _exponent;
        public bool IsZero => _mantissa == 0.0;

        // ------------------------------ Factories -------------------------------
        public static SciNot FromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("NaN/Infinity not supported by SciNot.");

            if (value == 0.0) return Zero;

            var exp = (int)Math.Floor(System.Math.Log10(System.Math.Abs(value)));
            var man = value / Pow10(exp);
            var sn = new SciNot(man, exp); // ctor normalizes
            return sn;
        }

        public bool TryToDouble(out double value)
        {
            switch (_exponent)
            {
                // Reasonable safety to avoid overflow
                case > 308:
                    value = double.PositiveInfinity; return false;
                case < -324:
                    value = 0.0; return false;
                default:
                    value = _mantissa * Pow10(_exponent);
                    return true;
            }
        }

        public double ToDoubleApprox()
        {
            TryToDouble(out var v);
            return v;
        }

        // ------------------------------ Arithmetic ------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SciNot operator +(SciNot a, SciNot b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;

            // Exact-ish small magnitude path
            if (WithinExactRange(a) && WithinExactRange(b))
                return FromDouble(a.ToDoubleApprox() + b.ToDoubleApprox());

            // Compare exponents
            if (a._exponent == b._exponent)
                return new SciNot(a._mantissa + b._mantissa, a._exponent);

            // Keep the larger, possibly ignore the smaller
            SciNot big = a._exponent > b._exponent ? a : b;
            SciNot small = a._exponent > b._exponent ? b : a;

            int diff = big._exponent - small._exponent;
            if (diff > IgnoreExpDiff)
                return big;

            // Bring small mantissa into big's scale
            var scaledSmall = small._mantissa * Pow10(-diff);
            return new SciNot(big._mantissa + scaledSmall, big._exponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SciNot operator -(SciNot a, SciNot b)
        {
            if (b.IsZero) return a;
            if (a.IsZero) return new SciNot(-b._mantissa, b._exponent);

            if (WithinExactRange(a) && WithinExactRange(b))
                return FromDouble(a.ToDoubleApprox() - b.ToDoubleApprox());

            if (a._exponent == b._exponent)
                return new SciNot(a._mantissa - b._mantissa, a._exponent);

            var big = a._exponent > b._exponent ? a : new SciNot(-b._mantissa, b._exponent);
            var small = a._exponent > b._exponent ? b : a;

            var diff = Math.Abs(a._exponent - b._exponent);
            if (diff > IgnoreExpDiff)
                return big;

            // Align onto the bigger's exponent
            if (a._exponent > b._exponent)
            {
                var scaledSmall = b._mantissa * Pow10(-(a._exponent - b._exponent));
                return new SciNot(a._mantissa - scaledSmall, a._exponent);
            }
            else
            {
                var scaledSmall = a._mantissa * Pow10(-(b._exponent - a._exponent));
                return new SciNot(-b._mantissa + scaledSmall, b._exponent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SciNot operator *(SciNot a, SciNot b)
        {
            if (a.IsZero || b.IsZero) return Zero;

            if (WithinExactRange(a) && WithinExactRange(b))
                return FromDouble(a.ToDoubleApprox() * b.ToDoubleApprox());

            return new SciNot(a._mantissa * b._mantissa, a._exponent + b._exponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SciNot operator /(SciNot a, SciNot b)
        {
            if (b.IsZero) throw new DivideByZeroException("SciNot divide by zero.");
            if (a.IsZero) return Zero;

            if (WithinExactRange(a) && WithinExactRange(b))
                return FromDouble(a.ToDoubleApprox() / b.ToDoubleApprox());

            return new SciNot(a._mantissa / b._mantissa, a._exponent - b._exponent);
        }

        public static SciNot Abs(SciNot v) => new SciNot(System.Math.Abs(v._mantissa), v._exponent);

        // ----------------------------- Comparison -------------------------------
        public int CompareTo(SciNot other)
        {
            switch (IsZero)
            {
                case true when other.IsZero:
                    return 0;
                case true:
                    return System.Math.Sign(other._mantissa); // if other is zero handled above
            }

            if (other.IsZero) return System.Math.Sign(_mantissa);

            switch (_mantissa)
            {
                // Compare signs first
                case < 0 when other._mantissa >= 0:
                    return -1;
                case >= 0 when other._mantissa < 0:
                    return 1;
            }

            // Same sign
            var sign = _mantissa >= 0 ? 1 : -1;

            if (_exponent != other._exponent)
                return sign * System.Math.Sign(_exponent - other._exponent);

            // Same exponent, compare mantissa
            return sign * _mantissa.CompareTo(other._mantissa);
        }

        public bool Equals(SciNot other)
        {
            if (IsZero && other.IsZero) return true;
            return _exponent == other._exponent && System.Math.Abs(_mantissa - other._mantissa) <= 1e-15;
        }

        public override bool Equals(object obj) => obj is SciNot s && Equals(s);

        public override int GetHashCode() => HashCode.Combine(_exponent, _mantissa);

        public static bool operator ==(SciNot a, SciNot b) => a.Equals(b);
        public static bool operator !=(SciNot a, SciNot b) => !a.Equals(b);
        public static bool operator < (SciNot a, SciNot b) => a.CompareTo(b) < 0;
        public static bool operator > (SciNot a, SciNot b) => a.CompareTo(b) > 0;
        public static bool operator <=(SciNot a, SciNot b) => a.CompareTo(b) <= 0;
        public static bool operator >=(SciNot a, SciNot b) => a.CompareTo(b) >= 0;

        // --------------------------- Unity Lifecycle ----------------------------
        // Normalizes in editor when you tweak values manually.
        public void OnValidate()
        {
            Normalize(ref _mantissa, ref _exponent);
        }

        // ----------------------- Implicit/Explicit Casts ------------------------
        public static implicit operator SciNot(int v)    => FromDouble(v);
        public static implicit operator SciNot(long v)   => FromDouble(v);
        public static implicit operator SciNot(float v)  => FromDouble(v);
        public static implicit operator SciNot(double v) => FromDouble(v);

        public static explicit operator double(SciNot v) => v.ToDoubleApprox();

        // ------------------------------- Helpers --------------------------------
        private static bool WithinExactRange(SciNot v)
        {
            return v._exponent >= -ExactExponentRange && v._exponent <= ExactExponentRange;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Pow10(int e)
        {
            // Using Math.Pow here is okay; it's hot but rarely dominates vs gameplay.
            return System.Math.Pow(10.0, e);
        }

        private static void Normalize(ref double man, ref int exp)
        {
            if (man == 0.0) { exp = 0; return; }

            var abs = System.Math.Abs(man);

            switch (abs)
            {
                case >= 10.0 - NormalizeEps:
                {
                    while (System.Math.Abs(man) >= 10.0 - NormalizeEps)
                    {
                        man /= 10.0;
                        exp += 1;
                    }

                    break;
                }
                case < 1.0 - NormalizeEps:
                {
                    while (System.Math.Abs(man) > 0 && System.Math.Abs(man) < 1.0 - NormalizeEps)
                    {
                        man *= 10.0;
                        exp -= 1;
                    }

                    break;
                }
            }

            // Snap tiny mantissas to zero to avoid -0.000… ghosts
            if (!(System.Math.Abs(man) < 1e-18)) return;
            man = 0.0; exp = 0;
        }

        // ------------------------------ Utilities --------------------------------
        public static SciNot Max(SciNot a, SciNot b) => a >= b ? a : b;
        public static SciNot Min(SciNot a, SciNot b) => a <= b ? a : b;

        /// <summary> Returns true if a >= b * 10^k (cheap affordability test). </summary>
        public static bool ExponentGapAtLeast(SciNot a, SciNot b, int k)
        {
            // If a’s exponent is b.exponent + k or more, it dwarfs b regardless of mantissa.
            return a._exponent - b._exponent >= k;
        }

        public override string ToString()
        {
            // Placeholder; real formatter to come (engineering vs designer format).
            return IsZero ? "0" : $"{_mantissa:F3}e{_exponent}";
        }
    }
}
