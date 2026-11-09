// Ported from pymedphys (lib/pymedphys/_utilities/createshells.py), Copyright (C) 2015-2018 Simon Biggs,
// Apache License 2.0 - see THIRD-PARTY-NOTICES.md at the repository root. Ported to C# for
// DicomStrictCompare by crane-dsc on 2026-11-09 at Cody Crewson's request.
using System;
using System.Collections.Generic;

namespace DSClibrary.Gamma
{
    /// <summary>
    /// The coordinate offsets of a search shell: every point at one distance from a reference point,
    /// spaced no further apart than the step size, in one, two or three dimensions.
    /// <para>
    /// A port of pymedphys' <c>calculate_coordinates_shell</c>. The shells are what gamma searches:
    /// for each reference point, the evaluation dose is interpolated at every point of each shell at
    /// increasing distances, and the smallest dose difference on the shell bounds the gamma at that
    /// distance.
    /// </para>
    /// </summary>
    public static class ShellCoordinates
    {
        /// <summary>
        /// The offsets of the shell at <paramref name="distance"/>, as one array per dimension,
        /// all of the same length. A distance of zero is the single point at the origin.
        /// </summary>
        public static double[][] Calculate(double distance, int dimensions, double stepSize)
        {
            if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            if (stepSize <= 0) throw new ArgumentOutOfRangeException(nameof(stepSize));
            return dimensions switch
            {
                1 => OneDimension(distance),
                2 => TwoDimensions(distance, stepSize),
                3 => ThreeDimensions(distance, stepSize),
                _ => throw new ArgumentOutOfRangeException(nameof(dimensions), "Shells exist in 1, 2 or 3 dimensions")
            };
        }

        /// <summary>The two points at the distance, or the origin alone at zero.</summary>
        private static double[][] OneDimension(double distance) =>
            distance == 0 ? new[] { new[] { 0.0 } } : new[] { new[] { distance, -distance } };

        /// <summary>Points round a circle, no further apart than the step size.</summary>
        private static double[][] TwoDimensions(double distance, double stepSize)
        {
            int amount = (int)Math.Ceiling(2 * Math.PI * distance / stepSize) + 1;
            double[] theta = LinspaceOpen(0, 2 * Math.PI, amount);
            var x = new double[theta.Length];
            var y = new double[theta.Length];
            for (int i = 0; i < theta.Length; i++)
            {
                x[i] = distance * Math.Cos(theta[i]);
                y[i] = distance * Math.Sin(theta[i]);
            }
            return new[] { x, y };
        }

        /// <summary>Points over a sphere: rows of latitude, each a circle, with no gap wider than the step.</summary>
        private static double[][] ThreeDimensions(double distance, double stepSize)
        {
            int rows = (int)Math.Ceiling(Math.PI * distance / stepSize) + 1;
            double[] elevation = Linspace(0, Math.PI, rows);
            var xs = new List<double>();
            var ys = new List<double>();
            var zs = new List<double>();
            foreach (double phi in elevation)
            {
                double rowRadius = distance * Math.Sin(phi);
                int amountInRow = (int)Math.Ceiling(2 * Math.PI * rowRadius / stepSize) + 1;
                double[] azimuth = LinspaceOpen(0, 2 * Math.PI, amountInRow);
                foreach (double a in azimuth)
                {
                    xs.Add(distance * Math.Sin(phi) * Math.Cos(a));
                    ys.Add(distance * Math.Sin(phi) * Math.Sin(a));
                    zs.Add(distance * Math.Cos(phi));
                }
            }
            return new[] { xs.ToArray(), ys.ToArray(), zs.ToArray() };
        }

        /// <summary>numpy.linspace(start, stop, count): count points including both ends; one point is the start.</summary>
        internal static double[] Linspace(double start, double stop, int count)
        {
            if (count <= 0) return Array.Empty<double>();
            var values = new double[count];
            if (count == 1) { values[0] = start; return values; }
            double step = (stop - start) / (count - 1);
            for (int i = 0; i < count; i++) values[i] = start + i * step;
            values[count - 1] = stop;
            return values;
        }

        /// <summary>numpy.linspace(start, stop, count + 1)[:-1]: count points from start, stopping short of stop.</summary>
        internal static double[] LinspaceOpen(double start, double stop, int count)
        {
            if (count <= 0) return Array.Empty<double>();
            var values = new double[count];
            double step = (stop - start) / count;
            for (int i = 0; i < count; i++) values[i] = start + i * step;
            return values;
        }
    }
}
