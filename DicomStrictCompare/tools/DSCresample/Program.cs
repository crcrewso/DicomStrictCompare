using System.Globalization;
using DSCresample;

// DSCresample: a command-line front for DSClibrary's resampler and DICOM writer.
//
//   DSCresample survey <file|dir>...
//       One line per RT Dose: transfer syntax, grid, voxel encoding. Measure before assuming.
//
//   DSCresample resample <sourceDir> <destinationDir> [--mm 5] [--parallel 3] [--overwrite]
//       Every RD*.dcm under the source, trilinearly resampled to the spacing, written under the
//       destination at the same relative path; RP*.dcm and *.toml copied beside them. Existing
//       outputs are skipped unless --overwrite, so an interrupted run can be resumed.
//
//   DSCresample verify <sourceDir> <resampledDir> [--sample 12] [--seed 1]
//       A random sample of resampled files against their sources: every node re-interpolated
//       (must agree within the half-unit rounding), and a fine-versus-coarse run through the
//       comparison engine at 0.1 % with no DTA, which must fail nowhere.

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: DSCresample survey <file|dir>...");
    Console.Error.WriteLine("       DSCresample resample <sourceDir> <destinationDir> [--mm 5] [--parallel 3] [--overwrite]");
    return 2;
}

switch (args[0])
{
    case "survey":
        return Survey.Run(Expand(args.Skip(1)));

    case "resample":
    {
        if (args.Length < 3) { Console.Error.WriteLine("resample needs a source and a destination folder"); return 2; }
        double mm = 5.0;
        int parallel = 3;
        bool overwrite = false;
        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mm": mm = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--parallel": parallel = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--overwrite": overwrite = true; break;
                default: Console.Error.WriteLine($"unknown option '{args[i]}'"); return 2;
            }
        }
        if (!Directory.Exists(args[1])) { Console.Error.WriteLine($"source folder not found: {args[1]}"); return 2; }
        return Resample.Run(new Resample.Options(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), mm, parallel, overwrite));
    }

    case "verify":
    {
        if (args.Length < 3) { Console.Error.WriteLine("verify needs the source folder and the resampled folder"); return 2; }
        int sample = 12, seed = 1;
        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sample": sample = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default: Console.Error.WriteLine($"unknown option '{args[i]}'"); return 2;
            }
        }
        return Verify.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), sample, seed);
    }

    default:
        Console.Error.WriteLine($"unknown command '{args[0]}'");
        return 2;
}

static IReadOnlyList<string> Expand(IEnumerable<string> paths) =>
    paths.SelectMany(p => Directory.Exists(p)
            ? Directory.EnumerateFiles(p, "RD*.dcm", SearchOption.AllDirectories)
            : new[] { p })
        .ToArray();
