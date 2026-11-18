# The difference viewer (prototype, 2026-11-18)

crcrewso, 2026-11-18: NIfTI and NiiVue, *"since [they] are common components for other medical projects"*. This builds
step 1 of SS8 (`study-store.md`), and much of step 2, with NiiVue in place of ScottPlot heatmaps. Both windows have it: a Viewer tab,
"View difference" on a result row, and `--viewer SOURCE TARGET`.

## The frame: the axis relative to the beam

crcrewso: *"The axis relative to the beam matters, the rest does not. So for static gantry x = Coll 0 x jaws, y = coll 0
y jaws, z = beam axis. Center of rotation is iso for dynamic gantry, center of phantom for static gantry."*

- `BeamFrame` (DSClibrary/Maps): IEC 61217's gantry system. x along the collimator-0 X jaws, y along the Y jaws
  (toward the gantry), z along the beam axis, **positive toward the source**, so depth runs down the screen. The
  collimator angle is ignored on purpose.
- **Origin:** the plan's isocentre for an arc, with the axes at gantry 0; the phantom's centre for a static gantry, at
  the beam's gantry angle. The phantom's centre is the middle of the box of voxels with any dose: Eclipse leaves dose
  outside the body at zero. With no plan, head first supine at gantry 0 is assumed and the note says so.
- **Refused, not guessed:** anything but head first supine, and couch angles other than 0, as `ScanGeometry` does.

## The files

- `DifferenceMap` computes, on the source (reference) grid:
  - the gamma index, run to its cap of 3 without stopping once a point passes, so each value is the point's gamma;
  - the signed difference, target minus source, as a percent of the source maximum.

  NaN marks points not evaluated: those below the cutoff, and those outside the target grid.
- `NiftiWriter` writes each as a single-file NIfTI-1 (float32). The voxel order is the grid's, and the sform and qform
  map voxels into the beam frame in mm, not into RAS. So NiiVue's "axial" view is across the beam, and its coronal
  view is the X jaws against depth.
- `DifferenceViewer` (Shared/Viewer) writes `dose.nii`, `gamma.nii`, `percent.nii` and `info.json` to a temporary
  folder.
- `ViewerServer` serves them, the page and NiiVue on 127.0.0.1, at a random port and path. Nothing leaves the machine.

## The page

- **NiiVue 0.69.0** (BSD-2), vendored as `Viewer/assets/niivue.umd.js`, with its licence and sha512 check noted in
  `NIIVUE-LICENSE.txt`. It works offline, and needs WebGL2.
- **Threshold** (crcrewso): values whose size is below it are transparent. At 1, gamma under 1 is hidden, and so is
  every difference between −1 % and +1 %. Positive differences are drawn warm and negative ones cool, gamma yellow to
  red, all opaque from the threshold up (NiiVue's own warm and winter stop at half opacity, which left 3D mostly grey).
- **Views:** slices and 3D, 3D alone, or slices alone. In 3D, "Dose only" draws the phantom as a lit surface, with
  low dose half-clear so a ray looks past the under-dosed edge voxel. "Cut" (crcrewso: "it should follow the viewer crosshair in that axis and completely remove one side") removes everything on one
  side of a plane through the crosshair, across x, y or z, dose and overlay alike (`isClipAllVolumes`), and moves as the
  crosshair moves.
- **Clicking** reads out x, y and z in the frame, the distance off axis, the dose, gamma and the difference.
- **Settings from the address**, for links and captures: `?show=percent&threshold=1&top=10&view=render&cut=x&side=low&at=20,0,0&azimuth=120&elevation=25` (`at` places the crosshair, in mm in the frame).

## Checks (2026-11-18)

- **Orientation, static:** E15V15 Prod `AllMFields 15x`, gantry 0. "Dose only" in 3D shows the 10 × 10 field as a
  bright square on the phantom's top face. The slices along the beam put the source at the top.
- **Orientation, arc:** Equinox `CalcJT 06 MV`, field JT C, a clockwise arc from gantry 181. It shows a bright band
  around the phantom's curved surface. Surface dose is 18–33 % of the maximum over frame y −86 to +2 mm, against
  1–5 % outside that band.
- **A poorly matching pair:** E15V15 Prod vs Equinox (PRD-After), `AllMFields 15x`.
  - Gamma 3 %/2 mm (10 % cutoff) passes 30.6 %; the study's 3 %/2 mm DTA gave 28.5 %.
  - On the central axis the difference runs from −17 % at the surface, through 0 near dmax, to +11 % at 15 cm. The
    target penetrates further: a depth-dose difference, not a scale factor.
  - The study's Energy column reads 6 MV for this "15x" field; the plan itself isn't checked.
- **Hosts:**
  - Avalonia's `NativeWebView` (Avalonia.Controls.WebView 12.1.1, MIT) runs it, also on WebKitGTK. GTK must use its X11
    backend to embed, so the app sets `GDK_BACKEND=x11` on Linux at start; Avalonia itself runs on X11.
  - Eto's GTK WebView (WebKitGTK 2.52) runs it, including on Xvfb's software renderer.
  - Headless Chrome runs it with SwiftShader.
  - The Claude desktop app's browser pane has no WebGL2.

## Not done

- Gamma for one pair at 2.5 mm against a finer target took about 3 minutes, and nothing is cached between opens.
- The point context of SS4 (depth from the surface, distance from the 50 % edge) is not shown: those wait on the surface
  and field-edge conventions being unified.
