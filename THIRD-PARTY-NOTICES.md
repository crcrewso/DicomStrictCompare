# Third-party notices

DicomStrictCompare is licensed under the GNU Lesser General Public License v3.0 (see `LICENSE`).
The following parts were derived from other projects and keep their own notices.

## pymedphys (Apache License 2.0)

`DicomStrictCompare/DSClibrary/Gamma/ShellCoordinates.cs`, `RegularGridInterpolator.cs` and
`GammaShell.cs` are C# ports of the gamma index implementation in
[pymedphys](https://github.com/pymedphys/pymedphys), taken from commit `ac82365` (2026-11-09):

- `lib/pymedphys/_gamma/implementation/shell.py` — Copyright (C) 2022-2026 Matthew Jennings,
  Copyright (C) 2015-2018 Simon Biggs
- `lib/pymedphys/_gamma/utilities/core.py` — Copyright (C) 2026 Matthew Jennings,
  Copyright (C) 2015 Simon Biggs
- `lib/pymedphys/_utilities/createshells.py` — Copyright (C) 2015-2018 Simon Biggs
- `lib/pymedphys/_interp/interp.py` — Copyright (C) 2024-2026 Matthew Jennings

The algorithm follows Wendling et al., *A fast algorithm for gamma evaluation in 3D*, Med. Phys. 34
(2007), <http://dx.doi.org/10.1118/1.2721657>.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use those files except
in compliance with the License. You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>. Unless required by applicable law or agreed to in
writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
language governing permissions and limitations under the License.

The ports are modifications: the vectorised NumPy search over all reference points at once became a
per-point loop over the same shells, run in parallel; one dose and distance criterion per call
instead of several; the RAM-budget chunking and the SciPy fallback for unevenly spaced grids were
not carried over (uneven evaluation axes are refused instead).
