using System.Globalization;
using DSClibrary.Profiles;
using DSCplots;

// DSCprofiles: water-tank scans against calculated doses, from the command line.
//
//   DSCprofiles <scansDir> <dosesDir> <outputDir> [options]
//
//   Every PTW mcc scan under scansDir is paired with every RT Dose under dosesDir whose plan beam is the same field
//   (DSClibrary.Profiles.ScanDoseMatching), the dose is sampled along the scan's line, and the two are compared.
//
//   --dose 2               dose criterion, %                             --distance 2      distance criterion, mm
//   --test gamma|strict    gamma, or the strict DTA rule (distance 0: dose difference only)
//   --reference measured|calculated                                      --local           local dose difference
//   --cutoff 10            profile cutoff, % of the reference maximum    --pdd-cutoff 10   depth-dose cutoff, %
//   --max-gamma inf        gamma search cap (pymedphys max_gamma)        --interp 10       gamma interp_fraction
//   --depth-shift 3[,0.1]  search a depth-dose shift of +-3 mm in 0.1 mm steps
//   --plans <regex>        only doses whose plan label matches           --exclude <regex> skip scan folders matching
//   --threads N            doses loaded at once                          --curves          also write every curve
//   --stem name            output file stem (default "profiles")
//   --plots                an image of every compared pair, in <stem>-plots/ (the Graphs tab's figure)
//   --plots-failing        the same, only for pairs with a point that failed
//   --plot-format png|svg  (default png)                                 --plot-size 1200x900   pixels
//   --header-fixes FILE    people's corrections to scan headers (default: the scans folder's dsc-header-fixes.json
//                          when there is one)                            --no-header-fixes  use the files' headers as they are

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: DSCprofiles <scansDir> <dosesDir> <outputDir> [--dose 2] [--distance 2] [--test gamma|strict]");
    Console.Error.WriteLine("       [--reference measured|calculated] [--local] [--cutoff 10] [--pdd-cutoff 10] [--max-gamma inf]");
    Console.Error.WriteLine("       [--interp 10] [--depth-shift 3[,0.1]] [--plans regex] [--exclude regex] [--threads N] [--curves] [--stem name]");
    Console.Error.WriteLine("       [--plots | --plots-failing] [--plot-format png|svg] [--plot-size 1200x900] [--header-fixes FILE | --no-header-fixes]");
    return 2;
}

var ci = CultureInfo.InvariantCulture;
double dose = 2, distance = 2, cutoff = 10, pddCutoff = 10, maxGamma = double.PositiveInfinity, interp = 10;
var test = ProfileTest.Gamma;
var reference = ProfileReference.Measured;
bool local = false, curves = false;
DepthShiftSearch? shift = null;
string? plans = null, exclude = null, stem = "profiles", headerFixes = null;
bool noHeaderFixes = false;
int? threads = null;
var plots = PlotSelection.None;
string plotFormat = "png";
int plotWidth = ProfilePlot.DefaultWidth, plotHeight = ProfilePlot.DefaultHeight;
for (int i = 3; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--dose": dose = double.Parse(args[++i], ci); break;
        case "--distance": distance = double.Parse(args[++i], ci); break;
        case "--test": test = Enum.Parse<ProfileTest>(args[++i], ignoreCase: true); break;
        case "--reference": reference = Enum.Parse<ProfileReference>(args[++i], ignoreCase: true); break;
        case "--local": local = true; break;
        case "--cutoff": cutoff = double.Parse(args[++i], ci); break;
        case "--pdd-cutoff": pddCutoff = double.Parse(args[++i], ci); break;
        case "--max-gamma": maxGamma = args[++i] is "inf" ? double.PositiveInfinity : double.Parse(args[i], ci); break;
        case "--interp": interp = double.Parse(args[++i], ci); break;
        case "--depth-shift":
        {
            var parts = args[++i].Split(',');
            shift = new DepthShiftSearch(double.Parse(parts[0], ci), parts.Length > 1 ? double.Parse(parts[1], ci) : 0.1);
            break;
        }
        case "--plans": plans = args[++i]; break;
        case "--exclude": exclude = args[++i]; break;
        case "--threads": threads = int.Parse(args[++i], ci); break;
        case "--curves": curves = true; break;
        case "--stem": stem = args[++i]; break;
        case "--plots": plots = PlotSelection.All; break;
        case "--plots-failing": plots = PlotSelection.Failing; break;
        case "--header-fixes": headerFixes = args[++i]; break;
        case "--no-header-fixes": noHeaderFixes = true; break;
        case "--plot-format":
            plotFormat = args[++i].ToLowerInvariant().TrimStart('.');
            if (plotFormat is not ("png" or "svg")) { Console.Error.WriteLine($"--plot-format is png or svg, not '{args[i]}'"); return 2; }
            break;
        case "--plot-size":
        {
            var parts = args[++i].ToLowerInvariant().Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, ci, out plotWidth) || !int.TryParse(parts[1], NumberStyles.None, ci, out plotHeight)
                || plotWidth < 100 || plotHeight < 100)
            { Console.Error.WriteLine($"--plot-size is WIDTHxHEIGHT in pixels, at least 100x100, not '{args[i]}'"); return 2; }
            break;
        }
        default: Console.Error.WriteLine($"unknown option '{args[i]}'"); return 2;
    }
}

string scans = Path.GetFullPath(args[0]), doses = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
if (!Directory.Exists(scans)) { Console.Error.WriteLine($"scans folder not found: {scans}"); return 2; }
if (!Directory.Exists(doses)) { Console.Error.WriteLine($"doses folder not found: {doses}"); return 2; }
Directory.CreateDirectory(output);

var comparison = new ProfileComparisonOptions(dose, distance, test, reference, cutoff, pddCutoff, local, maxGamma, interp, shift);
try { comparison.Validate(); }
catch (ArgumentOutOfRangeException ex) { Console.Error.WriteLine($"invalid criterion: {ex.Message}"); return 2; }

void Say(string message) => Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}Z] {message}");
Say($"criterion: {comparison}");

ScanHeaderFixes? fixes = null;
if (!noHeaderFixes)
{
    string fixesPath = headerFixes is null ? ScanHeaderFixes.DefaultPath(scans) : Path.GetFullPath(headerFixes);
    if (headerFixes is not null && !File.Exists(fixesPath)) { Console.Error.WriteLine($"header fixes file not found: {fixesPath}"); return 2; }
    try { fixes = ScanHeaderFixes.Load(fixesPath); }
    catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException)
    { Console.Error.WriteLine($"header fixes can't be read: {ex.Message}"); return 2; }
    if (fixes.Fixes.Count > 0) Say($"{fixes.Fixes.Count} header fix(es) from {fixesPath}");
}
var result = ProfileBatch.Run(scans, doses, new ProfileBatchOptions(comparison, plans, exclude, threads, fixes), Say);
var plotFiles = ProfilePlotExport.Write(output, stem!, scans, result, plots, plotFormat, plotWidth, plotHeight, threads, Say);
ProfileBatchReport.Write(output, stem!, scans, doses, comparison, plans, exclude, result, curves, plotFiles);

int compared = result.Rows.Count(r => r.Result is not null);
Say($"{compared} of {result.Rows.Count} pairs compared, {result.Unmatched.Count} scans unpaired, "
    + $"{result.Threads} thread(s), {result.Elapsed.TotalSeconds:0.0} s; written to {output}");
return 0;
