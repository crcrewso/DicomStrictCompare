<#
.SYNOPSIS
    Relative performance of DicomStrictCompare's comparison engines on this machine, in one command, with a
    results file that can be sent back and compared across machines.

.DESCRIPTION
    Builds the DSCgpu benchmark (prototypes/DSCgpu), records the machine (CPU, cores, RAM, GPUs, OS), times a cold
    read of every dose file (and hashes it), then runs the benchmark's three modes:
      quick   comparison work only, loading excluded: the engine on one thread against the CPU port, the threshold
              box and the GPU comparers, in double and float;
      cores   one pair, the multi-thread comparer pinned to each kind of core and unpinned (core types, SMT);
      batch   whole runs through the library as a user runs them, reading included, cold: the reference engine,
              the single- and multi-thread comparers, the read queue with target-box reads, and the GPU.
    quick and batch run under up to three CPU shapes:
      all       every logical processor (as a user's run would);
      physical  one logical processor per physical core (SMT/Hyper-Threading siblings left idle);
      four      four physical cores, one thread each, standing in for a 4-core machine (P-cores on a hybrid).
    Every row's counts are checked against the reference engine (the HandWrittenComparer). A double-precision row
    that disagrees is a bug, and is listed under "problems" in results.json and at the top of summary.md.

    Output, in one folder and zipped: results.json (everything, for a machine to read), summary.md (for a person),
    machine.json, read.csv, the benchmark's own CSVs and console output, and run-log.txt.

    Default data: nine reference pairs from the repository's own resources/ folder, the same pairs that
    DoseComparerEquivalenceTests uses, so no other data is needed. -QAtpsPTWFolder or -PairList use larger sets.

    Works in Windows PowerShell 5.1 and PowerShell 7 (pwsh) on Windows and Linux. Needs the .NET 10 SDK.
    On Linux the CPU shapes need `taskset` (util-linux), and cold reads use `dd iflag=nocache` (GNU coreutils);
    neither needs root.

    PRIVACY. Nothing is sent anywhere. Without -Anonymous the files hold this machine's name, your user name (in
    run-log.txt) and the full paths of the data. With -Anonymous the machine name becomes a short hash, the user
    name and home folder are removed, and data paths become relative to the data folder. Both ways the files hold
    the CPU, GPU and driver versions, RAM, OS version and power plan, and the SHA-256 of each dose file used.

.PARAMETER PairList
    A tab-separated file: name, source path, target path. Relative paths resolve against -DataRoot, or else the
    list's own folder.

.PARAMETER Anonymous
    Replace the machine name, user name and data paths, as described under PRIVACY.

.EXAMPLE
    ./Measure-DscPerformance.ps1
    Reference pairs from the repository, all modes and shapes, results in ./dsc-perf-<machine>-<time>.zip

.EXAMPLE
    ./Measure-DscPerformance.ps1 -PairList pairs-E11V11-E15V11-AiXflatXaaa.tsv -DataRoot 'D:\DSCReferences' -Anonymous
    An Eclipse version-matrix list whose paths are relative to D:\DSCReferences, anonymised.
#>
[CmdletBinding()]
param(
    [string]$QAtpsPTWFolder,
    [string]$PairList,
    [string]$DataRoot,
    [int]$Pairs = 0,
    [string]$Criteria = '3/2,0.1/0.5',
    [double]$Threshold = 10,
    [int]$Repeats = 3,
    [string[]]$Modes = @('quick', 'cores', 'batch'),     # any of quick, cores, batch
    [string[]]$Shapes = @('all', 'physical', 'four'),    # any of all, physical, four
    [switch]$Anonymous,
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'
# `powershell -File script.ps1 -Modes cores,batch` passes one string "cores,batch": split, then check.
$Modes = @($Modes | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Shapes = @($Shapes | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($m in $Modes) { if ($m -notin 'quick', 'cores', 'batch') { throw "-Modes takes quick, cores and batch, not '$m'" } }
foreach ($s in $Shapes) { if ($s -notin 'all', 'physical', 'four') { throw "-Shapes takes all, physical and four, not '$s'" } }
$RunnerVersion = '2'                                  # bump when results.json changes meaning
$onWindows = $env:OS -eq 'Windows_NT'
$inv = [Globalization.CultureInfo]::InvariantCulture
function Stamp { [DateTime]::UtcNow.ToString('HH:mm:ss') + 'Z' }
function Say([string]$s) { Write-Host "[$(Stamp)] $s" }
function Num([string]$s) { [long]($s -replace '[^\d]', '') }   # "30,040,420" -> 30040420
function Dbl([string]$s) { [double]::Parse($s, $inv) }

# ---- locations ----
$dsc = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path          # .../DicomStrictCompare
$proj = Join-Path $dsc 'prototypes/DSCgpu/DSCgpu.csproj'
$resources = Join-Path $dsc 'resources'
$realMachine = [Environment]::MachineName
$sha = [Security.Cryptography.SHA256]::Create()
function Hex([byte[]]$b) { -join ($b | ForEach-Object { $_.ToString('x2') }) }
$machine = if ($Anonymous) { 'm-' + (Hex ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($realMachine.ToUpperInvariant())))).Substring(0, 8) } else { $realMachine }
if (-not $OutDir) { $OutDir = Join-Path (Get-Location) ("dsc-perf-{0}-{1}" -f $machine, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir 'run-log.txt'
Start-Transcript -Path $log -Append | Out-Null
# The harness prints counts and seconds in the current culture; invariant mode makes them parseable everywhere.
$env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$results = [ordered]@{ schema = 1; runner = $RunnerVersion; anonymous = [bool]$Anonymous }
$problems = New-Object Collections.ArrayList
$runs = New-Object Collections.ArrayList

try {
    # ---- .NET SDK, commit, build (from inside the repository, so global.json picks the SDK) ----
    $sdks = @(& dotnet --list-sdks 2>$null)
    if (-not ($sdks | Where-Object { $_ -match '^10\.' })) { throw "The .NET 10 SDK is needed (found: $($sdks -join '; ')). Install it from https://dotnet.microsoft.com/download/dotnet/10.0" }
    $commit = $null; $dirty = $null
    try { $commit = (& git -C $dsc rev-parse HEAD 2>$null); $dirty = [bool](& git -C $dsc status --porcelain --untracked-files=no 2>$null) } catch { }
    $results.dsc_commit = $commit; $results.dsc_tree_modified = $dirty
    $results.started_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $results.powershell = "$($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)"
    Say "building DSCgpu (Release) at $commit"
    Push-Location $dsc
    try { & dotnet build $proj -c Release -nologo -v:quiet | Out-File (Join-Path $OutDir 'build.txt'); if ($LASTEXITCODE -ne 0) { throw "build failed; see build.txt" } }
    finally { Pop-Location }
    $exe = Get-ChildItem (Join-Path $dsc 'prototypes/DSCgpu/bin/Release') -Recurse -File |
        Where-Object { $_.Name -eq 'DSCgpu.exe' -or ($_.Name -eq 'DSCgpu' -and -not $onWindows) } | Select-Object -First 1
    if (-not $exe) { $exe = Get-ChildItem (Join-Path $dsc 'prototypes/DSCgpu/bin/Release') -Recurse -Filter 'DSCgpu.dll' | Select-Object -First 1 }
    # @( ) keeps a one-element array an array (an if-expression would unroll it to a string).
    $runner = @(if ($exe.Extension -eq '.dll') { 'dotnet'; $exe.FullName } else { $exe.FullName })

    # ---- pairs ----
    $pairFile = Join-Path $OutDir 'pairs.tsv'
    $setName = ''; $dataRoots = @()
    if ($PairList) {
        $base = if ($DataRoot) { (Resolve-Path $DataRoot).Path } else { Split-Path (Resolve-Path $PairList).Path }
        $lines = Get-Content $PairList | Where-Object { $_.Trim() } | ForEach-Object {
            $c = $_ -split "`t"
            $abs = @($c[1], $c[2]) | ForEach-Object { if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $base $_ } }
            "{0}`t{1}`t{2}" -f $c[0], $abs[0], $abs[1] }
        $lines | Out-File -Encoding ascii $pairFile; $setName = [IO.Path]::GetFileNameWithoutExtension($PairList); $dataRoots = @($base)
    } elseif ($QAtpsPTWFolder) {
        $names = [string[]](Get-ChildItem $QAtpsPTWFolder -Filter 'RD.QAtpsPTW.SquareFieldsS.*.dcm' | ForEach-Object { $_.Name.Substring(26) } |
            Where-Object { Test-Path (Join-Path $QAtpsPTWFolder "RD.QAtpsPTW.SquFldsSAXB.$_") })
        if ($names.Count -eq 0) { throw "no RD.QAtpsPTW.SquareFieldsS.*.dcm with a matching SquFldsSAXB file in $QAtpsPTWFolder" }
        [Array]::Sort($names, [StringComparer]::Ordinal)
        $lines = foreach ($n in $names) { "{0}`t{1}`t{2}" -f ($n -replace '\.dcm$', ''), (Join-Path $QAtpsPTWFolder "RD.QAtpsPTW.SquareFieldsS.$n"), (Join-Path $QAtpsPTWFolder "RD.QAtpsPTW.SquFldsSAXB.$n") }
        $lines | Out-File -Encoding ascii $pairFile; $setName = 'QAtpsPTW-native'; $dataRoots = @((Resolve-Path $QAtpsPTWFolder).Path)
    } else {
        # The pairs DoseComparerEquivalenceTests uses (DSClibraryTests/DoseComparerEquivalenceTests.cs).
        $ref = @(
            @('P1Ref.X-100A-10.0-1', 'P1 Acuros.X-100X-10.0-1'), @('P1Ref.F-100A-10.0-1', 'P1 Acuros.F-100X-10.0-1'),
            @('P1Ref.X-100A-10.0-1', 'P1 2 mm.X-100A-10.0-0.2'), @('P1Ref.X-100A-10.0-1', 'P1 5 mm.X-100A-10.0-5'),
            @('P1 5 mm.X-100A-10.0-5', 'P1Ref.X-100A-10.0-1'), @('P2 10.1.X-100X-10.1-1', 'P3 11.0.X-100X-10.1-1'),
            @('P5 Varied MU.X-095A-10.0-1', 'P5 Varied MU.X-101A-10.0-1'), @('P1Ref.e-100A-10.0-1-13', 'P4 E 2D.e-100A-10.0-1-12'),
            @('P1Ref.X-100A-10.0-1', 'P1Ref.X-100A-10.0-1'))
        $lines = foreach ($p in $ref) {
            $s = Join-Path $resources "RD.UnitTest.$($p[0]).dcm"; $t = Join-Path $resources "RD.UnitTest.$($p[1]).dcm"
            if (-not (Test-Path $s) -or -not (Test-Path $t)) { throw "reference file missing: $s or $t" }
            "{0} vs {1}`t{2}`t{3}" -f $p[0], $p[1], $s, $t
        }
        $lines | Out-File -Encoding ascii $pairFile; $setName = 'repository-reference-9'; $dataRoots = @($resources)
    }
    $pairRows = @(Get-Content $pairFile | Where-Object { $_.Trim() } | ForEach-Object { , ($_ -split "`t") })
    foreach ($r in $pairRows) { foreach ($p in $r[1..2]) { if (-not (Test-Path -LiteralPath $p)) { throw "pair file missing: $p" } } }
    $dataRoots = @($dataRoots | ForEach-Object { $_.TrimEnd('\', '/') })
    function Rel([string]$p) { foreach ($root in $dataRoots) { if ($p.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return $p.Substring($root.Length).TrimStart('\', '/').Replace('\', '/') } }; return [IO.Path]::GetFileName($p) }

    # ---- machine ----
    $logical = [Environment]::ProcessorCount
    $info = [ordered]@{ machine = $machine; recorded_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'); logical_processors = $logical }
    $topo = $null
    if ($onWindows) {
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        $os = Get-CimInstance Win32_OperatingSystem
        $info.cpu = $cpu.Name.Trim(); $info.physical_cores = ($(Get-CimInstance Win32_Processor) | Measure-Object NumberOfCores -Sum).Sum
        $info.ram_total_gb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1); $info.ram_available_gb = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
        $info.os = "$($os.Caption) $($os.Version)"
        $info.gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object { "$($_.Name) (driver $($_.DriverVersion))" })
        try { $info.power_plan = ((powercfg /getactivescheme) -join ' ') -replace '^.*\(|\).*$', '' } catch { }
        # Core topology: which logical processors share a physical core, and each core's efficiency class.
        Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class DscTopology {
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetLogicalProcessorInformationEx(int relation, IntPtr buffer, ref uint length);
    public static List<long[]> Cores() {   // each: { mask of logical processors (group 0), efficiency class, smt 0/1 }
        uint len = 0; GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref len);
        IntPtr buf = Marshal.AllocHGlobal((int)len); var list = new List<long[]>();
        try {
            if (!GetLogicalProcessorInformationEx(0, buf, ref len)) throw new Exception("GetLogicalProcessorInformationEx " + Marshal.GetLastWin32Error());
            int off = 0;
            while (off < len) {
                int size = Marshal.ReadInt32(buf, off + 4);
                byte flags = Marshal.ReadByte(buf, off + 8), eff = Marshal.ReadByte(buf, off + 9);
                long mask = Marshal.ReadInt64(buf, off + 8 + 24); short group = Marshal.ReadInt16(buf, off + 8 + 32);
                if (group == 0) list.Add(new long[] { mask, eff, flags });
                off += size;
            }
        } finally { Marshal.FreeHGlobal(buf); }
        return list;
    }
}
'@
        $topo = @([DscTopology]::Cores() | ForEach-Object { [pscustomobject]@{ Mask = $_[0]; Eff = $_[1]; Smt = $_[2] } })
    } else {
        $info.cpu = ((Get-Content /proc/cpuinfo | Where-Object { $_ -match '^model name' } | Select-Object -First 1) -replace '^.*:\s*', '')
        $mem = Get-Content /proc/meminfo
        $info.ram_total_gb = [math]::Round([double](($mem | Where-Object { $_ -match '^MemTotal' }) -replace '\D', '') / 1MB, 1)
        $info.ram_available_gb = [math]::Round([double](($mem | Where-Object { $_ -match '^MemAvailable' }) -replace '\D', '') / 1MB, 1)
        $info.os = (Get-Content /etc/os-release | Where-Object { $_ -match '^PRETTY_NAME' }) -replace '^PRETTY_NAME="?|"$', ''
        try { $info.gpus = @(& lspci 2>$null | Where-Object { $_ -match 'VGA|3D|Display' }) } catch { $info.gpus = @() }
        function Ids([string]$list) { $ids = @(); foreach ($part in $list.Trim() -split ',') { if (-not $part) { continue }; if ($part -match '-') { $a, $b = $part -split '-'; $ids += [int]$a..[int]$b } else { $ids += [int]$part } }; $ids }
        # Intel hybrids list their P-cores' logical processors here; elsewhere every core is class 0.
        $pList = @(); if (Test-Path /sys/devices/cpu_core/cpus) { $pList = @(Ids (Get-Content /sys/devices/cpu_core/cpus)) }
        $groups = @{}
        Get-ChildItem /sys/devices/system/cpu -Directory | Where-Object Name -match '^cpu\d+$' | ForEach-Object {
            $sib = Join-Path $_.FullName 'topology/thread_siblings_list'
            if (Test-Path $sib) { $k = (Get-Content $sib).Trim(); if (-not $groups.ContainsKey($k)) { $groups[$k] = $k } }
        }
        $topo = @($groups.Keys | ForEach-Object {
            $ids = @(Ids $_)
            $m = [long]0; foreach ($i in $ids) { if ($i -lt 63) { $m = $m -bor ([long]1 -shl $i) } }
            [pscustomobject]@{ Mask = $m; Eff = [int](@($ids | Where-Object { $pList -contains $_ }).Count -gt 0); Smt = [int]($ids.Count -gt 1) } })
        $info.physical_cores = $topo.Count
        $info.p_cores_from = if ($pList.Count) { '/sys/devices/cpu_core/cpus' } else { 'none (no hybrid listing)' }
    }
    if ($topo) { $info.efficiency_classes = (@($topo | Group-Object Eff | ForEach-Object { "class $($_.Name): $($_.Count) cores" }) -join ', ') }
    # One logical per core (lowest sibling); the four-core shape prefers the highest efficiency class (P-cores).
    function LowBit([long]$m) { for ($i = 0; $i -lt 63; $i++) { if (($m -shr $i) -band 1) { return [long]1 -shl $i } }; return [long]0 }
    function CpuList([long]$m) { (@(for ($i = 0; $i -lt 63; $i++) { if (($m -shr $i) -band 1) { $i } }) -join ',') }
    $maskPhysical = [long]0; foreach ($c in $topo) { $maskPhysical = $maskPhysical -bor (LowBit $c.Mask) }
    $maskFour = [long]0; foreach ($c in @($topo | Sort-Object @{ e = { -$_.Eff } }, @{ e = { $_.Mask } } | Select-Object -First 4)) { $maskFour = $maskFour -bor (LowBit $c.Mask) }
    $info | ConvertTo-Json -Depth 3 | Out-File -Encoding utf8 (Join-Path $OutDir 'machine.json')
    Say ("{0}: {1}, {2} physical / {3} logical, {4} GB RAM ({5} available)" -f $machine, $info.cpu, $info.physical_cores, $logical, $info.ram_total_gb, $info.ram_available_gb)
    $results.machine = $info

    # ---- how many pairs fit (quick holds them all, in double plus float copies) ----
    $fileBytes = @($pairRows | ForEach-Object { (Get-Item -LiteralPath $_[1]).Length + (Get-Item -LiteralPath $_[2]).Length })
    $perPair = [double](($fileBytes | Measure-Object -Maximum).Maximum) * 3.5   # 32-bit voxels -> 8-byte doubles + 4-byte floats, plus slack
    $fit = [math]::Max(1, [math]::Floor($info.ram_available_gb * 1GB * 0.6 / $perPair))
    $n = $pairRows.Count; if ($Pairs -gt 0) { $n = [math]::Min($Pairs, $n) }; $n = [math]::Min($n, $fit)
    Say ("pairs: {0} of {1} in the set (about {2:N2} GB each; {3} fit in 60 % of available RAM)" -f $n, $pairRows.Count, ($perPair / 1GB), $fit)
    if ($n -lt $pairRows.Count) { $pairRows[0..($n - 1)] | ForEach-Object { $_ -join "`t" } | Out-File -Encoding ascii $pairFile }
    $pairRows = @($pairRows[0..($n - 1)])
    $files = @($pairRows | ForEach-Object { $_[1]; $_[2] } | Select-Object -Unique)

    # ---- cold reads (cache emptied first), and each file's SHA-256 from the same read ----
    $ddOk = $false
    function Purge([string[]]$paths) {
        foreach ($f in $paths) {
            if ($onWindows) { $h = New-Object IO.FileStream($f, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read, 1, [IO.FileOptions]0x20000000); $h.Dispose() }   # unbuffered open purges the file's cached pages
            else { & dd "if=$f" iflag=nocache count=0 status=none 2>$null; $script:ddOk = ($LASTEXITCODE -eq 0) }                                                                 # posix_fadvise(DONTNEED), no root
        }
    }
    Purge $files
    $cold = if ($onWindows) { 'cold (cache purged by an unbuffered open)' } elseif ($ddOk) { 'cold (cache dropped by dd iflag=nocache)' } else { 'possibly cached (dd iflag=nocache failed)' }
    $bytes = 0L; $readS = 0.0; $fileInfo = New-Object Collections.ArrayList
    foreach ($f in $files) {
        $sw = [Diagnostics.Stopwatch]::StartNew(); $b = [IO.File]::ReadAllBytes($f); $sw.Stop()
        $bytes += $b.Length; $readS += $sw.Elapsed.TotalSeconds
        [void]$fileInfo.Add([ordered]@{ path = $(if ($Anonymous) { Rel $f } else { $f }); bytes = $b.Length; sha256 = (Hex ($sha.ComputeHash($b))) })
        $b = $null
    }
    $readMBs = $bytes / [math]::Max($readS, 1e-9) / 1MB
    # A real run reads both files of every pair; the reference set reuses some files, so scale by pair bytes.
    $pairBytes = [double](($pairRows | ForEach-Object { (Get-Item -LiteralPath $_[1]).Length + (Get-Item -LiteralPath $_[2]).Length } | Measure-Object -Sum).Sum)
    $readPerPair = ($pairBytes / $pairRows.Count) / ($bytes / [math]::Max($readS, 1e-9))
    "files,bytes,seconds,MB_per_s,kind`n{0},{1},{2:0.000},{3:0},{4}" -f $files.Count, $bytes, $readS, $readMBs, $cold | Out-File -Encoding utf8 (Join-Path $OutDir 'read.csv')
    Say ("read {0} files, {1:N2} GB in {2:N2} s = {3:N0} MB/s, {4}" -f $files.Count, ($bytes / 1GB), $readS, $readMBs, $cold)
    $results.dataset = [ordered]@{ name = $setName; pairs = @($pairRows | ForEach-Object { [ordered]@{ name = $_[0]; source = $(if ($Anonymous) { Rel $_[1] } else { $_[1] }); target = $(if ($Anonymous) { Rel $_[2] } else { $_[2] }) } }); files = $fileInfo }
    $results.reads = [ordered]@{ files = $files.Count; bytes = $bytes; seconds = [math]::Round($readS, 3); mb_per_s = [math]::Round($readMBs, 0); kind = $cold; seconds_per_pair = [math]::Round($readPerPair, 4) }

    # ---- running the harness under a CPU shape ----
    $self = [Diagnostics.Process]::GetCurrentProcess(); $fullMask = $self.ProcessorAffinity
    $haveTaskset = (-not $onWindows) -and [bool](Get-Command taskset -ErrorAction SilentlyContinue)
    $shapeMask = @{ all = [long]0; physical = $maskPhysical; four = $maskFour }
    $useShapes = foreach ($shape in $Shapes) {
        $m = $shapeMask[$shape]
        if ($shape -eq 'physical' -and ($m -eq 0 -or $m -eq $fullMask.ToInt64())) { Say "skip physical: no SMT siblings on this machine"; continue }
        if ($shape -eq 'four' -and @($topo).Count -lt 5) { Say "skip four: this machine has 4 or fewer cores, so 'all' already is that shape"; continue }
        if ($m -ne 0 -and -not $onWindows -and -not $haveTaskset) { Say "skip $shape`: taskset is not installed (util-linux)"; continue }
        $shape
    }
    function Harness([string]$shape, [string[]]$harnessArgs, [string]$out) {
        $m = $shapeMask[$shape]
        # Windows: affinity on this process before the start; the child inherits it. Linux: pwsh starts children
        # from another thread, which doesn't inherit the main thread's affinity, so taskset launches the child.
        if ($m -ne 0 -and $onWindows) { $self.ProcessorAffinity = [IntPtr]$m }
        try {
            $cmd = @($runner) + $harnessArgs
            if ($m -ne 0 -and -not $onWindows) { $cmd = @('taskset', '-c', (CpuList $m)) + $cmd }
            $text = & $cmd[0] $cmd[1..($cmd.Count - 1)] 2>&1 | Out-String
        } finally { if ($onWindows) { $self.ProcessorAffinity = $fullMask } }
        $text | Out-File -Encoding utf8 $out
        if ($LASTEXITCODE -ne 0) { Say "  benchmark exited $LASTEXITCODE; see $(Split-Path $out -Leaf)" }
        return $text
    }
    $topologySummary = $null
    function Problem([string]$what) { [void]$problems.Add($what); Say "  PROBLEM: $what" }

    # ---- quick: algorithm only ----
    if ($Modes -contains 'quick') {
        foreach ($shape in $useShapes) {
            $csv = Join-Path $OutDir "quick-$shape.csv"; if (Test-Path $csv) { Remove-Item $csv }
            Say ("quick, shape '{0}', affinity {1}" -f $shape, $(if ($shapeMask[$shape]) { CpuList $shapeMask[$shape] } else { 'all' }))
            $text = Harness $shape @('0', "$n", '-', "$Repeats", '--pairlist', $pairFile, '--set', $setName, '--criteria', $Criteria, '--threshold', ($Threshold.ToString($inv)), '--quick', '--csv', $csv) (Join-Path $OutDir "quick-$shape.txt")
            if (-not (Test-Path $csv)) { Problem "quick, shape $shape`: no CSV written"; continue }
            # Counts: the reference totals per criterion from the per-pair lines; a method's totals are the reference
            # plus the difference it prints ("COUNTS DIFFER in k pair(s): compared +a, failed +b").
            $blocks = @(($text -split "`n") | ForEach-Object { $_.TrimEnd("`r") })
            $crit = -1; $ref = @{}; $diff = @{}; $inRef = $false
            foreach ($l in $blocks) {
                if ($l -match '=== ') { $crit++; $ref[$crit] = @(0L, 0L); $inRef = $false; continue }
                if ($crit -lt 0) { continue }
                if ($l -match '^    engine, 1 thread, pairs in turn') { $inRef = $true; continue }
                if ($inRef -and $l -match '^      .+: ([\d,]+) compared, ([\d,]+) failed$') { $ref[$crit][0] += (Num $matches[1]); $ref[$crit][1] += (Num $matches[2]); continue }
                if ($l -match '^      above threshold') { $inRef = $false; continue }
                if ($l -match '^    (.+?)\s+[\d.]+ s\s+[\d.]+ s/pair.*COUNTS DIFFER in (\d+) pair\(s\): compared ([+-]?\d+), failed ([+-]?\d+)') { $diff["$crit|$($matches[1].Trim())"] = @([int]$matches[2], [long]$matches[3], [long]$matches[4]) }
            }
            $critNames = @($Criteria -split ',')
            foreach ($r in (Import-Csv $csv)) {
                $ci = [array]::IndexOf($critNames, $r.criterion); $rc = $ref[$ci]; $d = $diff["$ci|$($r.method)"]
                $isFloat = $r.method -match 'float'; $isZeroed = $r.method -match 'zeroed'
                $entry = [ordered]@{ mode = 'quick'; shape = $shape; method = $r.method; criterion = "$($r.criterion) mm, $Threshold % global"
                    seconds = (Dbl $r.seconds); seconds_per_pair = (Dbl $r.s_per_pair); speedup = (Dbl $r.speedup)
                    reference_compared = $rc[0]; reference_failed = $rc[1]
                    compared = $(if ($d) { $rc[0] + $d[1] } else { $rc[0] }); failed = $(if ($d) { $rc[1] + $d[2] } else { $rc[1] })
                    agrees = $(switch ($r.agrees) { 'reference' { 'reference' } 'yes' { 'yes' } 'n/a' { 'n/a' } default { if ($isFloat) { 'no (float: expected)' } elseif ($isZeroed) { 'no (zeroed: different rules)' } else { 'NO' } } })
                    upload_s = $(if ($r.upload_s) { Dbl $r.upload_s } else { $null }); kernel_s = $(if ($r.kernel_s) { Dbl $r.kernel_s } else { $null }) }
                if ($r.method -match '^engine, (\d+) pairs|(\d+) pairs at once') { $entry.workers = [int]($r.method -replace '^\D*(\d+).*$', '$1') }
                if ($r.method -match '(\d+) thread') { $entry.threads = [int]$matches[1] }
                $entry.pinning = 'unpinned (the harness''s own threads, within the shape)'
                [void]$runs.Add($entry)
                if ($entry.agrees -eq 'NO') { Problem "quick, shape $shape, $($r.criterion): '$($r.method)' (double) did not reproduce the reference engine: compared $($entry.compared) vs $($rc[0]), failed $($entry.failed) vs $($rc[1])" }
            }
        }
    }

    # ---- cores: one pair, core kinds pinned and unpinned (the heaviest source in the set) ----
    if ($Modes -contains 'cores') {
        $heavy = $pairRows | Sort-Object { (Get-Item -LiteralPath $_[1]).Length } -Descending | Select-Object -First 1
        Say "cores: '$($heavy[0])', no threshold box, best of 5"
        $text = Harness 'all' @('cores', $heavy[1], $heavy[2], '5', '--criteria', $Criteria, '--threshold', ($Threshold.ToString($inv)), '--nobox') (Join-Path $OutDir 'cores.txt')
        $firstCounts = $null
        foreach ($l in (($text -split "`n") | ForEach-Object { $_.TrimEnd("`r") })) {
            if (-not $topologySummary -and $l -match '^\S+: (\d+ cores .*)$') { $topologySummary = $matches[1] }
            if ($l -match '^(?<name>\S.*?)\s+(?<s>[\d.]+) s\s+(?<x>[\d.]+)x\s+(?<c>[\d/ ]+?)(?<d>\s+COUNTS DIFFER)?$') {
                $rowName = $matches.name.Trim(); $rowS = Dbl $matches.s; $rowX = Dbl $matches.x; $rowDiffer = [bool]$matches.d   # before any other -match
                $pairs2 = @($matches.c.Trim() -split '\s+' | ForEach-Object { $a = $_ -split '/'; , @([long]$a[0], [long]$a[1]) })
                $firstCounts = if ($firstCounts) { $firstCounts } else { $pairs2 }
                $cs = @($Criteria -split ',')
                for ($k = 0; $k -lt $pairs2.Count; $k++) {
                    $e = [ordered]@{ mode = 'cores'; shape = 'all'; method = $rowName; pair = $heavy[0]; criterion = "$($cs[$k]) mm, $Threshold % global, no box"
                        seconds = $rowS; speedup = $rowX; compared = $pairs2[$k][0]; failed = $pairs2[$k][1]
                        reference_compared = $firstCounts[$k][0]; reference_failed = $firstCounts[$k][1]
                        agrees = $(if ($rowDiffer) { 'NO' } elseif ($rowName -match '^single thread') { 'reference' } else { 'yes' }) }
                    if ($e.method -match '(\d+)\)?$|: (\d+) pinned') { $e.threads = [int](($e.method -replace '^.*?(\d+)( pinned|\))?$', '$1')) }
                    $e.pinning = if ($rowName -match 'unpinned') { 'unpinned' } elseif ($rowName -match 'pinned') { 'pinned' } else { 'one thread' }
                    [void]$runs.Add($e)
                    if ($e.agrees -eq 'NO') { Problem "cores: '$($e.method)' counts differ from the single-thread row at $($cs[$k])" }
                }
            }
        }
    }

    # ---- batch: whole runs, reading included, cold, against the reference engine ----
    if ($Modes -contains 'batch') {
        $gpuOk = -not ($runs | Where-Object { $_.mode -eq 'quick' }) -or [bool]($runs | Where-Object { $_.mode -eq 'quick' -and $_.method -match 'Silk.NET' })
        $rows = @(
            @('reference engine (HandWritten)', @('--engine', 'HandWritten')),
            @('single thread, pairs side by side', @('--engine', 'SingleThread')),
            @('multi thread', @('--engine', 'MultiThread')),
            @('multi thread, 4 readers, target box only', @('--engine', 'MultiThread', '--readers', '4', '--held', '4', '--partial')))
        if ($gpuOk) { $rows += , @('OpenCL (Silk.NET), double', @('--engine', 'OpenCl')) }
        foreach ($shape in $useShapes) {
            $bcsv = Join-Path $OutDir "batch-$shape.csv"
            $refTotals = $null
            foreach ($row in $rows) {
                Purge $files
                Say ("batch, shape '{0}': {1}" -f $shape, $row[0])
                $text = Harness $shape (@('batch', $pairFile) + $row[1] + @('--criteria', $Criteria, '--threshold', ($Threshold.ToString($inv)), '--csv', $bcsv, '--label', ($row[0] -replace ',', ';'))) (Join-Path $OutDir ("batch-$shape-{0}.txt" -f ($row[0] -replace '[^\w]+', '-')))
                $lines = @(($text -split "`n") | ForEach-Object { $_.TrimEnd("`r") })
                if (-not $topologySummary) { $t = $lines | Where-Object { $_ -match '^\S+: (\d+ cores .*)$' } | Select-Object -First 1; if ($t -match '^\S+: (.*)$') { $topologySummary = $matches[1] } }
                $wall = $lines | Where-Object { $_ -match '^([\d.]+) s wall clock at (\d+) worker' } | Select-Object -First 1
                if (-not $wall) { Problem "batch, shape $shape, '$($row[0])': no result (see its .txt)"; continue }
                $null = $wall -match '^([\d.]+) s wall clock at (\d+) worker\(s\); peak working set ([\d.]+) GB(; targets ([\d.]+) ?% read)?'
                $sec = Dbl $matches[1]; $workers = [int]$matches[2]; $peak = Dbl $matches[3]; $frac = if ($matches[5]) { (Dbl $matches[5]) / 100 } else { $null }
                $desc = ($lines | Where-Object { $_ -match ' pairs from ' } | Select-Object -First 1) -replace '^.*?; ', ''
                $tot = @($lines | Where-Object { $_ -match '^  (.+?): compared (.+?), failed (.+?), (\d+) failed pair' } | ForEach-Object { $null = $_ -match '^  (.+?): compared (.+?), failed (.+?), (\d+) failed pair'; , @($matches[1], (Num $matches[2]), (Num $matches[3]), [int]$matches[4]) })
                if (-not $refTotals) { $refTotals = $tot }
                for ($k = 0; $k -lt $tot.Count; $k++) {
                    $e = [ordered]@{ mode = 'batch'; shape = $shape; method = $row[0]; engine = $desc; criterion = "$($tot[$k][0]), $Threshold % threshold"
                        seconds = $sec; seconds_per_pair = [math]::Round($sec / $pairRows.Count, 5); workers = $workers; peak_working_set_gb = $peak; target_fraction_read = $frac
                        compared = $tot[$k][1]; failed = $tot[$k][2]; failed_pairs = $tot[$k][3]
                        reference_compared = $refTotals[$k][1]; reference_failed = $refTotals[$k][2]
                        agrees = $(if ($row[0] -match '^reference') { 'reference' } elseif ($tot[$k][1] -eq $refTotals[$k][1] -and $tot[$k][2] -eq $refTotals[$k][2]) { 'yes' } else { 'NO' }) }
                    if ($desc -match '(\d+) threads') { $e.threads = [int]$matches[1] }
                    # The library's own setting (CpuTopology.PinByDefault), when the harness says it.
                    $e.pinning = if ($desc -match '\bunpinned\b') { 'unpinned' } elseif ($desc -match '\bpinned\b') { 'pinned' } else { 'library default (not printed by this harness)' }
                    [void]$runs.Add($e)
                    if ($e.agrees -eq 'NO') { Problem "batch, shape $shape, '$($row[0])' at $($tot[$k][0]): compared $($e.compared) vs $($e.reference_compared), failed $($e.failed) vs $($e.reference_failed)" }
                    if ($tot[$k][3] -gt 0) { Problem "batch, shape $shape, '$($row[0])' at $($tot[$k][0]): $($tot[$k][3]) pair(s) failed to compare" }
                }
            }
        }
    }
    $results.cpu_topology_summary = $topologySummary
    $results.criteria = @($Criteria -split ',' | ForEach-Object { "$_ mm, $Threshold % global" })
    $results.shapes = @($useShapes | ForEach-Object { [ordered]@{ shape = $_; cpus = $(if ($shapeMask[$_]) { CpuList $shapeMask[$_] } else { 'all' }) } })
    $results.problems = @($problems)
    $results.runs = @($runs)
    $results.finished_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $results | ConvertTo-Json -Depth 6 | Out-File -Encoding utf8 (Join-Path $OutDir 'results.json')

    # ---- summary ----
    $sum = New-Object Text.StringBuilder
    [void]$sum.AppendLine("# DicomStrictCompare relative performance: $machine")
    [void]$sum.AppendLine("")
    if ($problems.Count) { [void]$sum.AppendLine("**$($problems.Count) PROBLEM(S): a double-precision row that disagrees with the reference engine is a bug.**"); foreach ($p in $problems) { [void]$sum.AppendLine("- $p") }; [void]$sum.AppendLine("") }
    else { [void]$sum.AppendLine("Every double-precision row reproduced the reference engine's counts."); [void]$sum.AppendLine("") }
    [void]$sum.AppendLine("$($info.cpu); $($info.physical_cores) physical / $logical logical; $($info.ram_total_gb) GB RAM; $($info.os)")
    [void]$sum.AppendLine("GPU: $($info.gpus -join '; ')")
    [void]$sum.AppendLine("Topology as the library sees it: $topologySummary")
    [void]$sum.AppendLine("DSC commit $commit$(if ($dirty) { ' (modified)' }); runner $RunnerVersion; $($results.powershell).")
    [void]$sum.AppendLine("Set: $setName, $n pairs, native resolution, criteria $Criteria at $Threshold % global, repeats $Repeats.")
    [void]$sum.AppendLine(("Disk read: {0:N0} MB/s ({1}); about {2:N3} s per pair to read." -f $readMBs, $cold, $readPerPair))
    $keep = 'engine, 1 thread|CPU port, \d+ thread|CPU port boxed|OpenCL, (double|float), boxed$|OpenCL, (double|float)$'
    foreach ($shape in $useShapes) {
        $q = @($runs | Where-Object { $_.mode -eq 'quick' -and $_.shape -eq $shape -and $_.method -match $keep })
        if ($q.Count) {
            [void]$sum.AppendLine(""); [void]$sum.AppendLine("## quick, shape $shape (algorithm only; est. whole run = read + algorithm, decoding not included)")
            [void]$sum.AppendLine(""); [void]$sum.AppendLine("| criterion | method | s/pair | x | est. whole run s/pair | counts |"); [void]$sum.AppendLine("|---|---|---|---|---|---|")
            foreach ($r in $q) { [void]$sum.AppendLine(("| {0} | {1} | {2:0.0000} | {3} | {4:0.000} | {5} |" -f $r.criterion, $r.method, $r.seconds_per_pair, $r.speedup, ($r.seconds_per_pair + $readPerPair), $r.agrees)) }
        }
        $b = @($runs | Where-Object { $_.mode -eq 'batch' -and $_.shape -eq $shape })
        if ($b.Count) {
            [void]$sum.AppendLine(""); [void]$sum.AppendLine("## batch, shape $shape (whole runs, cold reads, every criterion from one read)")
            [void]$sum.AppendLine(""); [void]$sum.AppendLine("| method | workers | seconds | s/pair | peak GB | criterion | compared | failed | counts |"); [void]$sum.AppendLine("|---|---|---|---|---|---|---|---|---|")
            foreach ($r in $b) { [void]$sum.AppendLine(("| {0} | {1} | {2:0.00} | {3:0.000} | {4:0.0} | {5} | {6:N0} | {7:N0} | {8} |" -f $r.method, $r.workers, $r.seconds, $r.seconds_per_pair, $r.peak_working_set_gb, $r.criterion, $r.compared, $r.failed, $r.agrees)) }
        }
    }
    $c = @($runs | Where-Object { $_.mode -eq 'cores' })
    if ($c.Count) {
        [void]$sum.AppendLine(""); [void]$sum.AppendLine("## cores ('$($c[0].pair)', no box, best of 5)")
        [void]$sum.AppendLine(""); [void]$sum.AppendLine("| method | seconds | x | counts |"); [void]$sum.AppendLine("|---|---|---|---|")
        # Script-block properties: Windows PowerShell 5.1 can't group or project an ordered dictionary by key name.
        foreach ($g in ($c | Group-Object { $_.method })) {
            $counts = (@($g.Group | ForEach-Object { '{0:N0}/{1:N0}' -f $_.compared, $_.failed }) -join ', ')
            $agree = (@($g.Group | ForEach-Object { $_.agrees }) | Select-Object -Unique) -join ', '
            [void]$sum.AppendLine(("| {0} | {1:0.000} | {2:0.00} | {3} ({4}) |" -f $g.Name, $g.Group[0].seconds, $g.Group[0].speedup, $agree, $counts))
        }
    }
    $sum.ToString() | Out-File -Encoding utf8 (Join-Path $OutDir 'summary.md')
    Say "summary: $(Join-Path $OutDir 'summary.md')"
    if ($problems.Count) { Write-Warning "$($problems.Count) problem(s); see summary.md" }
}
finally {
    Stop-Transcript | Out-Null
}
# ---- anonymise every text file written, then zip ----
if ($Anonymous) {
    $user = [Environment]::UserName; $home2 = [Environment]::GetFolderPath('UserProfile')
    $swap = New-Object Collections.ArrayList; [void]$swap.Add(@($realMachine, $machine))
    # Longest first (the data can sit inside the repository, the repository inside the home folder), and each path
    # also as JSON writes it (backslashes doubled) and with forward slashes.
    $paths = New-Object Collections.ArrayList   # (path, stand-in); an ArrayList, because one-element arrays unroll
    foreach ($root in $dataRoots) { [void]$paths.Add(@([string]$root, '<data>')) }
    [void]$paths.Add(@([string]$dsc, '<repo>')); [void]$paths.Add(@((Resolve-Path $OutDir).Path, '<out>')); [void]$paths.Add(@([string]$home2, '<home>'))
    foreach ($p in $paths) {
        [void]$swap.Add(@($p[0].Replace('\', '\\'), $p[1])); [void]$swap.Add(@($p[0], $p[1])); [void]$swap.Add(@($p[0].Replace('\', '/'), $p[1]))
    }
    if ($user.Length -ge 3) { [void]$swap.Add(@($user, '<user>')) }
    foreach ($f in Get-ChildItem $OutDir -File -Recurse) {
        $t = [IO.File]::ReadAllText($f.FullName)
        foreach ($s in $swap) { if ($s[0]) { $t = [regex]::Replace($t, [regex]::Escape($s[0]), $s[1].Replace('$', '$$'), 'IgnoreCase') } }
        [IO.File]::WriteAllText($f.FullName, $t, (New-Object Text.UTF8Encoding($false)))
    }
}
$zip = "$OutDir.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $OutDir '*') -DestinationPath $zip
Write-Host "[$(Stamp)] done: $zip"
