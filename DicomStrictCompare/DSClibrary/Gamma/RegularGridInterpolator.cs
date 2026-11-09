// Ported from pymedphys (lib/pymedphys/_interp/interp.py), Copyright (C) 2024-2026 Matthew Jennings,
// Apache License 2.0 - see THIRD-PARTY-NOTICES.md at the repository root. Ported to C# for
// DicomStrictCompare by crane-dsc on 2026-11-09 at Cody Crewson's request.
using System;

namespace DSClibrary.Gamma
{
    /// <summary>
    /// Linear interpolation on an evenly spaced, ascending grid of one, two or three dimensions, with
    /// a fill value for points outside the grid.
    /// <para>
    /// A port of pymedphys' <c>_interp_validated</c> and its 1D/2D/3D kernels, kept to the same
    /// index arithmetic so that a point on a grid node takes that node's value exactly and a point
    /// on the boundary counts as inside. Values are in C order: the last axis varies fastest.
    /// </para>
    /// </summary>
    public sealed class RegularGridInterpolator
    {
        private readonly double[][] _axes;
        private readonly double[] _values;
        private readonly double[] _spacing;
        private readonly int[] _strides;
        private readonly double _fill;

        /// <param name="axes">One ascending, evenly spaced axis per dimension, each with at least two values.</param>
        /// <param name="values">The grid values in C order, length = product of the axis lengths.</param>
        /// <param name="fillValue">Returned for points outside the grid's extent.</param>
        public RegularGridInterpolator(double[][] axes, double[] values, double fillValue)
        {
            if (axes is null) throw new ArgumentNullException(nameof(axes));
            if (values is null) throw new ArgumentNullException(nameof(values));
            if (axes.Length < 1 || axes.Length > 3)
                throw new ArgumentException("The interpolator works in 1, 2 or 3 dimensions", nameof(axes));

            long expected = 1;
            _spacing = new double[axes.Length];
            for (int d = 0; d < axes.Length; d++)
            {
                double[] axis = axes[d];
                if (axis is null || axis.Length < 2)
                    throw new ArgumentException($"Axis {d} must have at least two points to interpolate", nameof(axes));
                double diff = axis[1] - axis[0];
                if (!(diff > 0)) throw new ArgumentException($"Axis {d} must be strictly ascending", nameof(axes));
                for (int i = 1; i < axis.Length; i++)
                {
                    double step = axis[i] - axis[i - 1];
                    if (!(step > 0)) throw new ArgumentException($"Axis {d} must be strictly ascending", nameof(axes));
                    if (Math.Abs(step - diff) > 1e-8 * Math.Max(1, Math.Abs(diff)) + 1e-5 * Math.Abs(diff))
                        throw new ArgumentException($"Axis {d} must be evenly spaced", nameof(axes));
                }
                _spacing[d] = diff;
                expected *= axis.Length;
            }
            if (values.Length != expected)
                throw new ArgumentException($"values has {values.Length} entries; the axes describe {expected}", nameof(values));

            _axes = axes;
            _values = values;
            _fill = fillValue;
            _strides = new int[axes.Length];
            int stride = 1;
            for (int d = axes.Length - 1; d >= 0; d--)
            {
                _strides[d] = stride;
                stride *= axes[d].Length;
            }
        }

        public int Dimensions => _axes.Length;

        /// <summary>The interpolated value at a point, or the fill value outside the grid.</summary>
        public double At(ReadOnlySpan<double> point)
        {
            if (point.Length != _axes.Length)
                throw new ArgumentException($"Expected {_axes.Length} coordinates, got {point.Length}", nameof(point));

            Span<int> lower = stackalloc int[3];
            Span<int> upper = stackalloc int[3];
            Span<double> weight = stackalloc double[3];

            for (int d = 0; d < _axes.Length; d++)
            {
                double[] axis = _axes[d];
                double p = point[d];
                if (!(axis[0] <= p && p <= axis[^1])) return _fill;

                // numpy.searchsorted(axis, p), side left: the first index whose value is >= p.
                int found = Array.BinarySearch(axis, p);
                int hi = found >= 0 ? found : ~found;
                int lo = hi - 1;
                if (lo < 0) lo = 0;
                if (hi >= axis.Length) hi = axis.Length - 1;
                lower[d] = lo;
                upper[d] = hi;
                weight[d] = (p - axis[lo]) / _spacing[d];
            }

            switch (_axes.Length)
            {
                case 1:
                    return _values[lower[0]] * (1 - weight[0]) + _values[upper[0]] * weight[0];
                case 2:
                {
                    int sx = _strides[0], sy = _strides[1];
                    double c00 = _values[lower[0] * sx + lower[1] * sy];
                    double c01 = _values[lower[0] * sx + upper[1] * sy];
                    double c10 = _values[upper[0] * sx + lower[1] * sy];
                    double c11 = _values[upper[0] * sx + upper[1] * sy];
                    double wx = weight[0], wy = weight[1];
                    double c0 = c00 * (1 - wx) + c10 * wx;
                    double c1 = c01 * (1 - wx) + c11 * wx;
                    return c0 * (1 - wy) + c1 * wy;
                }
                default:
                {
                    int sx = _strides[0], sy = _strides[1], sz = _strides[2];
                    int x0 = lower[0] * sx, x1 = upper[0] * sx;
                    int y0 = lower[1] * sy, y1 = upper[1] * sy;
                    int z0 = lower[2] * sz, z1 = upper[2] * sz;
                    double c000 = _values[x0 + y0 + z0], c001 = _values[x0 + y0 + z1];
                    double c010 = _values[x0 + y1 + z0], c011 = _values[x0 + y1 + z1];
                    double c100 = _values[x1 + y0 + z0], c101 = _values[x1 + y0 + z1];
                    double c110 = _values[x1 + y1 + z0], c111 = _values[x1 + y1 + z1];
                    double wx = weight[0], wy = weight[1], wz = weight[2];
                    double c00 = c000 * (1 - wx) + c100 * wx;
                    double c01 = c001 * (1 - wx) + c101 * wx;
                    double c10 = c010 * (1 - wx) + c110 * wx;
                    double c11 = c011 * (1 - wx) + c111 * wx;
                    double c0 = c00 * (1 - wy) + c10 * wy;
                    double c1 = c01 * (1 - wy) + c11 * wy;
                    return c0 * (1 - wz) + c1 * wz;
                }
            }
        }
    }
}
