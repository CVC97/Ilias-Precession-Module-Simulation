# Design notes — Kreiselpräzession simulation

What was built and why: the decisions behind the Unity simulation, for anyone (or any Claude session) who extends it or builds the next simulation for a learning module. **How to** run, test and release is in `README.md`; this file doesn't repeat it.

Built by Carlo (physics education group, Göttingen) with Claude, 2026-10-02 to 2026-10-30. The original simulation was Carlo's Unity/C# learning project (Carlo wrote the code, Claude tutored); the module build was written by Claude and checked step by step by Carlo in the editor. This file replaces the working log `learning_migration.md` (gitignored, local only).

Related: the learning module [`../ilias-precession-module`](https://github.com/CVC97/Ilias-Precession-Module) has its own `design_notes.md` for the page side (learning path, the six embeds, panel rules, the sim contract as the page uses it). Rough lab data are in `experimental.md` (gitignored).


## 1 Status (2026-10-30)

| | Original | Module build |
|---|---|---|
| Purpose | standalone demo: a heavy top on a tip, noir look, own controls | embedded in the learning module on six pages, controlled by the page |
| Scene | `Assets/Scenes/SampleScene.unity` | `Assets/Module/ModuleScene.unity` |
| Build profile | *Web - Desktop - Release* → `docs/` (tracked) | *Web - Module - Release* → `Builds/ModuleWeb/` (gitignored) |
| Web template | `Assets/WebGLTemplates/Precession` | `Assets/WebGLTemplates/PrecessionModule` |
| Bridge | `WebBridge` (JS → C# only) | `ModuleWebBridge` + `ModuleBridge.jslib` (both ways) |
| Release | tag `sim-v*` → GitHub Pages (workflow) | tag `module-sim-v*` + GitHub release with `ModuleWeb.zip` → the module's `sim.version` |
| Live | `sim-v1.0.3` | `module-sim-v1.1.0` |

Everything is on `master` (the former `module-sim` branch is merged). Open items: section 9.


## 2 Physics model (shared by both builds)

`Assets/Scripts/Physics/` (`GyroscopeModel`, `Vec3d`) is plain C# with **no Unity reference** (assembly `GyroscopePhysics`, `noEngineReferences`). That keeps the physics testable with `dotnet` while the editor holds the project lock, and keeps it free of Unity's `float`.

| Decision | Why |
|---|---|
| **Own integrator, not PhysX** | PhysX clamps angular velocity and approximates gyroscopic terms; precession and nutation would be wrong exactly where they matter. |
| **Symmetric heavy top in Euler angles** (z-x-z: φ precession, θ tilt, ψ spin), **reduced form**: state (θ, θ̇, φ, ψ), φ̇ and ψ̇ from the conserved momenta p_φ, p_ψ | Fewer equations, conservation holds by construction, energy is the remaining numerical check. φ̈ is not needed. Singular only at θ = 0/π (division by sin²θ), never reached. |
| **RK4 with substeps in `FixedUpdate`, `double` throughout** | Explicit Euler is unstable for the nutation (amplitude grows each step; measured). RK4 at h ≈ 2 ms keeps the energy error at ~10⁻¹⁴ of m g l. |
| **Right-handed, z up** in the physics | Textbook frame; conversion to Unity happens only in the view (section 3). |
| Vectors `AxisDirection`, `AngularVelocity`, `AngularMomentum` (L = I₁ω + (I₃ − I₁)ω₃ê₃), `Torque` | Coordinate-free forms; checked by L·ẑ = p_φ and L·ê₃ = p_ψ to rounding error. |

**Module extensions** (additive; the original constructor reproduces the old model bit for bit, checked by a regression test):
- `FromInertia(i1, i3, k)` with a **torque constant k** instead of m g l (lab: k = m_z g r of the add-on weight; k = 0 is the balanced, force-free gyroscope). A static factory, not a constructor overload: a 3-argument call to the old constructor would otherwise silently pick the new one.
- p_ψ is a state variable (needed for **friction**: bearing torque −c ψ̇, so dp_ψ/dt = −c ψ̇ while p_φ stays conserved).
- `Push(Δθ̇, Δφ̇)` recomputes the momenta from the new velocities; `SetState` takes over a running state (used when a slider changes the parameters, section 5).
- `SteadyPrecessionRate(θ, ω₃)`: the slow root of θ̈ = 0, written as 2b/(a + sign(a)√D) to avoid cancellation. NaN if the top is too slow for steady precession (guided start then falls back to released).
- Tests: `Assets/Module/Tests/EditMode/GyroscopeModelTests.cs` (energy conservation loaded and balanced, ω_P against m_z g r/(I₃ω₃), θ-independence, nutation frequency growing with ω₃, reversed spin, continuation).

Physics notes worth knowing:
- ω⃗ is **not** on the figure axis: φ̇ẑ and θ̇ê_N add a transverse part, angle ≈ ω_⊥/ω₃ (≈ 1° at 3000 rpm on the original top, ∝ 1/ω₃²). L⃗ deviates I₁/I₃ times more. This is correct and shown on purpose.
- The fast-top precession rate Ω = m g l/(I₃ω₃) is independent of θ; the exact steady rate depends weakly on θ, and steady precession exists only for I₃²ω₃² ≥ 4 k I₁ cos θ.


## 3 Display conventions

| Decision | Why |
|---|---|
| **Vectors only swap axes, angles change sign** (`PhysicsToUnity`, used by both builds). Physics (x, y, z) → Unity (x, z, y): physics x, y, z become Unity's right, forward, up, which is again a right-handed set, so geometrically the map is a rotation and every vector, L⃗ and D⃗ included, maps by swapping y and z. Unity's rotation API, however, counts angles by the left-hand rule, so angles passed to `AngleAxis` get a minus. Pose = `precession * tilt` on the gyroscope root, spin on the rotor child. | Without the minus, the picture is a consistent mirror image that looks plausible (mechanics is parity-invariant) but contradicts the arrows computed in physics coordinates. Check: seen from the free end, a positive ω₃ turns counterclockwise. |
| **The hierarchy is the Euler-angle chain**: Gyroscope root at the pivot (φ, θ), Body (not spinning), Rotor (ψ). Only leaf meshes are scaled. | Each degree of freedom is one transform; a scaled parent would skew rotating children. |
| **Display spin limiter** (`maxVisibleSpinRps`): the drawn spin follows ψ̇ × timeScale, clamped to a few turns per *real* second, direction kept; `SpinDisplayLimited` tells the UI. | 3000 rpm at 60 fps is 300° per frame: the wheel appears to turn backwards (aliasing). In slow motion the true spin shows automatically. The UI says honestly when the display is slowed. |
| **`SimulationTime`** owns pause and slow motion; it scales `Time.fixedDeltaTime` with `timeScale`. | `FixedUpdate` runs on game time: at 0.05× without scaling the physics stepped 2.5 times per real second and the motion jumped. Scaled, it runs at 50 Hz real time and gets more accurate. One owner, because uGUI and the web bridge both pause. |
| Camera and UI use **unscaled time**; cameras move in `LateUpdate`. | The camera must work while paused; `LateUpdate` avoids a one-frame lag behind the pose. |
| Trail is cleared after each reset, after the new pose is applied. | Otherwise the jump to the start pose draws a straight line. |
| **Arrows behind an abstract `ArrowBase`** (`VectorArrow` line version, `VectorArrow3D` cylinder + procedural cone from `ConeMesh`). | `VectorDisplay` doesn't care which; both stay swappable in the Inspector. |


## 4 The original build

- **Toy top on a tip** (disk 6 cm, axle 10 cm, pivot ball), chosen over the lab's horizontal gyroscope for the standalone demo.
- **Noir look** ("Sin City"): black stage, spotlight pool with hard shadows, camera-relative rim light, black-and-white metal, colour only for the red marker and the arrows (L yellow, ω white, D red, emissive with bloom), film grain, vignette, snow that only lights up in the cone (particles run on game time, so pause freezes them). Arrows are URP Lit with black base + HDR emission: the editor-only `SimpleDots` test shader had made them pink in the build.
- **Controls stay uGUI** in the standalone build. An HTML UI in the template was considered and dropped: the module needs Angular components anyway, so it would have been built twice; only the bridge is reused.
- `WebBridge` (`SetRpm`, `SetTheta0`, `SetPhiDot0`, `SetTimeScale`, `SetPaused`, `Restart`, `SetShow*`, `SetSnow`) is the JS → C# entry for console tests; `hideUguiInBrowser` stays off.
- Template `Precession`: full-window canvas at devicePixelRatio (capped at 2), noir loading screen, German error text, `window.unityInstance`. The template thumbnail follows Unity's PWA style (`Design/thumbnail.svg`, outside `Assets/` so it isn't copied into builds).
- Deploy: the build in `docs/` is committed only for a release; `.github/workflows/pages.yml` publishes `docs/` only on a `sim-v*` tag. Gzip with decompression fallback, because GitHub Pages sends no `Content-Encoding`.


## 5 The module build

Why a separate build (Carlo): the lab uses a **horizontal gyroscope balanced at its pivot** with add-on weights, not a top on a tip; the module needs labels, axes and cones; the noir look distracts from the learning target. **The original stays untouched** (scene, look, `docs/`, `sim-v*`); module code lives under `Assets/Module/`, shared code is only extended.

**How it was built:** every scene change is an editor script, menu *Module → …* in `Assets/Module/Editor/ModuleSceneSetup.cs` (create scene, apply studio look, use module controller, build lab gyroscope, add simple top, add helpers, add precession cone, add nutation cones, use module web bridge). "Once" items refuse to run twice; *Apply studio look* and the cone items are rerunnable and overwrite manual tweaks. Reason: reproducible, reviewable, and the YAML is never edited by hand.

### Apparatus

Two apparatus in one build, chosen by the page's preset (learning path lab → simple top → lab, see the module's notes):

| | Lab gyroscope (`LabGyroscope.asset`) | Simple top (`SimpleTop.asset`) |
|---|---|---|
| Parameters (lab values rounded generously, `experimental.md`) | I₃ ≈ 0.01 kg m² (wheel 1.3 kg, R 0.12 m); I₁ ≈ 0.05 kg m² + m_z r²; counterweight 0.9 kg at 0.17 m; notch r = 0.27 m; k = m_z g r; friction c = I₃/τ, τ = 120 s | disk 1 kg, R 0.10 m, lever arm 0.167 m (the original's proportions), k = m g l |
| Limits | 800 rpm; θ₀ 60–120° (beyond, the wheel hits the stand) | 3000 rpm; θ₀ 20–100°; no collision handling (below ~600 rpm it falls through its tower) |
| Spin sign | −1: L⃗ points from the pivot through the wheel, as in textbook figures | +1 |
| Typical times | 30 g, 400 rpm: T_P ≈ 31 s; balanced: T_N ≈ 0.8 s | 2400 rpm, 60°: T_P ≈ 4.8 s |

- **θ convention [Carlo]:** θ is the angle between L⃗ (wheel side) and the vertical, for both apparatus, as in the module text. The model's figure axis points to the torque side, so the controller maps θ = π − θ_model for spin sign −1 (`ThetaOfL`, its own inverse). Model and tests unchanged.
- `ApparatusParameters` (abstract ScriptableObject) → `LabGyroscope`, `SimpleTop`; `ApparatusRig` holds each apparatus's parts, θ₀ limits, arrow scales and cone lengths. `ModuleGyroscopeController` implements `IGyroscopeModelSource`, so `VectorDisplay` works with both controllers.
- **Setters only mark changes; one apply per `FixedUpdate`.** Apparatus and start mode **restart** (`Run` + 1). Drehzahl, Zusatzgewicht and Neigung **continue** the running gyroscope (`Segment` + 1, from v1.1.0, Carlo: a slider must not put the gyroscope back to its start pose): the model is rebuilt and takes over φ, ψ, θ̇; in guided mode φ̇ shifts by the change of the steady rate, so a calm precession stays calm and nutation keeps its deviation.
- **Start modes:** guided = φ̇₀ at the steady rate (no nodding); released = axle at rest (dip ∝ m_z/ω_R², cusps).

### Look ("studio", Carlo)

A flat light look was tried first and rejected as lacking the polished feel. Now: soft warm spot + cool rim light, metal wheel and axle with a small studio reflection cubemap (128 px, `Assets/Module/Textures`), blue lacquered base, light floor fading into fog of the stage colour, gentle post (neutral tonemapping, contrast +20, bloom 0.2, blue vignette). Smooth 96-sided cylinders replace the faceted built-in ones. URP shadow distance 1 → 3 m project-wide (approved; harmless for the original). One light theme; `?theme=dark` only darkens the loading screen.

### Helpers (switchboard `ModuleHelpers`, 13 flags)

- **Colours are the module's everywhere** (sim and drawn figures): ω uni-blau-1, L uni-blau-3, D physik-gelb, precession jura-rot, figure axis and forces graphite. Arrows are **helpers, not parts**: URP Unlit, no shadows, 7 mm shafts.
- Labels L⃗, ω⃗, D⃗, ω_P as camera-facing TMP billboards with a soft white halo (an outline ate the thin italic strokes).
- **Precession = the cone swept by the figure axis** (jura-rot, apex at the pivot, on L⃗'s side, drawn at the time-averaged θ so nutation shows as the axle wobbling around it; surface fades out towards θ = 90°, rim stays). Rejected after screenshots: a curved ω_P arrow and an ω⃗_P vector (code still there, off), a dot on the arc, a cone swept by L⃗ (size depended on rpm: a metre wide on the lab).
- **Nutation cones** (balanced lab gyroscope after a push): Nutationskegel (figure axis around L⃗, graphite), Rastpolkegel (ω⃗ around L⃗, uni-blau-3), Gangpolkegel (ω⃗ around the figure axis, uni-blau-1), each with its own slant length (0.25/0.35/0.45 m) so the rims don't bunch up and the wheel doesn't hide them. For the lab (I₁ > I₃) ω⃗ lies between figure axis and L⃗ and the Gangpolkegel rolls on the outside.
- **Physics gating in the sim:** precession cone only with torque (k ≠ 0); nutation cones only without, and only once they are wider than 0.5°.
- **Never spoil the Auswertung:** the slope of ω_N vs. ω_R is I₃/I₁. No readout, label or message contains a moment of inertia or a ratio; the cones show it only geometrically.

### Bridge

```
module page ── postMessage {source:'precession-module', type:'command', cmd, value} ──► template: origin + allowlist,
                                                                                         queue until Unity runs,
                                                                                         SendMessage('WebBridge', cmd, value)
module page ◄── postMessage {source:'precession-sim', type:'ready' | 'state'} ◄── ModuleBridge.jslib ◄── ModuleWebBridge
```

- iframe on the same origin (`<base>/sim/`): freeing the WebGL context by removing the iframe is reliable, origin checks are trivial, no third-party host (GDPR).
- `ApplyPreset(json)` is a **complete** state (missing keys take the class defaults, not the previous values; the default helpers are on), always one restart. `SetHelpers` is partial. State at 10 Hz real time, also while paused: `run, segment, time, apparatus, thetaDeg, phiDeg, phiDot, omegaR, omegaP, addOnMassGrams, paused, spinLimited`.
- The GameObject keeps the name `WebBridge` (SendMessage target); *Use module web bridge* swaps the original's component for `ModuleWebBridge` in the module scene only.
- Full contract as the page uses it: the module's `design_notes.md`, section 4. Keep both in sync when commands or state fields change. Test page without the module: `Tools/bridge-test.html`.


## 6 Pitfalls and lessons (Unity/C#)

- **Shadowing:** `double g = gravity;` in a constructor, `bool X = …` in a method, `unit = unit;` with a same-named parameter: all compile and leave the field unset. Use `this.` and read every type name left of `=`.
- **Struct parameters are copies** (`TopState`, `Vector3`): pass with `ref`/`in`/`out` or work on the field.
- **Prefabs:** the root's position/rotation is always per instance; Prefab Mode *in Context* shows the instance's root values; scene overrides (bold in the Inspector) silently win over prefab changes; references to scene objects can't live in a prefab. Assign scene references from the Hierarchy, never from the Project window (that's the asset).
- **Transform scale vs. module scale:** particle size is scaled by the Transform (Scaling Mode Local); set emission volumes in the Shape module. Keep Transform scales at 1 on parents.
- **New objects appear at the Scene view's pivot**, and reparenting keeps world positions: reset Transforms after creating.
- **Particles:** Prewarm simulates one *Duration*, not one lifetime: Duration ≥ Start Lifetime, else the snow starts as a falling sheet.
- **Pink in the build only:** a shader from an `Editor` or `Tests` folder. Check shader paths before a release.
- **Build profiles override Player settings** (template, version, compression): change them in the profile.
- **Unity's `.unityweb` + decompression fallback** works on GitHub Pages and the university's Apache without `Content-Encoding` rules.
- **Inspector `InvalidOperationException … Next returned false`** after a field type or shader change while selected: an editor glitch, reselect or restart.
- **VS Code red marks while Unity compiles fine:** the language server hasn't reloaded the regenerated `.csproj`; restart it. Unity's Console is the reference, with the error filter on.
- **TMP font assets** (`LiberationSans SDF*.asset`) get re-serialized by TMP and are shared by both builds; Carlo set `git update-index --skip-worktree` on both. Don't commit them.
- **`docs/` and `.gitignore`:** ignoring doesn't apply to tracked files (`git rm --cached`); `docs/` is tracked on purpose (Pages), so it stays un-ignored.
- **Compile and test without the editor:** copy the Unity-generated `*.csproj` to a scratch folder, make paths absolute, `dotnet build`; the physics and its NUnit tests run in a scratch project linking `Assets/Scripts/Physics/*.cs` and `Assets/Module/Tests/EditMode/*.cs`.
- **Headless Chrome screenshots of the canvas** need `--use-angle=swiftshader --enable-unsafe-swiftshader` (WebGL 2).


## 7 Reusing this for another simulation

- Keep the physics a plain C# assembly without engine references; test it with `dotnet`.
- Map physics → Unity in one helper (`PhysicsToUnity`); decide once whether the frame change is a rotation or a mirror.
- Bridge pattern: one MonoBehaviour per build with one public method per command; `ApplyPreset` as a complete state with explicit defaults; a `.jslib` posting JSON to `window.parent` with the own origin; a template that checks origin and source, allowlists commands and queues them until Unity runs; a run counter (and a segment counter if parameters can change without a restart); state at ~10 Hz in real time.
- Build scenes with editor menu items, not by hand, when the work is shared between people and sessions.
- Design the presets page by page before the code (the module's notes, section 8).


## 8 Repository map

```
Assets/Scripts/Physics/        GyroscopeModel, Vec3d (assembly GyroscopePhysics, no Unity)
Assets/Scripts/                GyroscopeController, SimulationTime, WebBridge (original); RotorSpin, PrecessionCone (early milestones, disabled)
Assets/Scripts/View/           PhysicsToUnity, VectorDisplay, ArrowBase, VectorArrow(3D), ConeMesh, OrbitCamera, ControlPanel, SliderRow
Assets/Scripts/Testing/        ModelTest (console checks of the original model)
Assets/Module/Scripts/         ModuleGyroscopeController, ApparatusParameters/LabGyroscope/SimpleTop, ApparatusRig, ModuleHelpers,
                               HelperCone, PrecessionDisplay, NutationCones, VectorLabel, HangingWeight, PivotHead,
                               ModuleWebBridge (+ DTOs), PrecessionArc/PrecessionVector (off)
Assets/Module/Editor/          ModuleSceneSetup (menu Module → …)
Assets/Module/Plugins/         ModuleBridge.jslib
Assets/Module/Tests/EditMode/  GyroscopeModelTests (NUnit)
Assets/WebGLTemplates/         Precession (original), PrecessionModule (module)
Design/                        thumbnail.svg (template thumbnail source)
Tools/                         package-module-build.sh, bridge-test.html
.github/workflows/pages.yml    GitHub Pages deploy of docs/ on sim-v* tags
```


## 9 Open items

- **Push sign quirk:** positive `pushThetaDot` raises the wheel on `lab` but lowers the disk on `top` (it acts on the model θ). Unify with `ThetaOfL` if a page ever pushes the top in θ; only p6 uses it (lab, positive).
- `PrecessionArc` and `PrecessionVector` are off and unused: delete them or keep them for comparison.
- Optional: a body-fixed marker on the Gangpolkegel to show the rolling; "x-ray" for arrow parts hidden behind the wheel (needs a RenderObjects feature); the soft horizon line of the module floor.
- Build size (~18 MB): the Unity logo texture (2.7 MB) is still in the module data although the splash is off (maybe the hidden *Show Unity Logo* flag); URP film grain textures and ProBuilder/TMP are project-wide.
- From the module's QA list (sim side): no touch rotation on phones/tablets; the mouse wheel over the sim zooms instead of scrolling the page; p7's light grey trail has weak contrast on the light stage; on p4 at 1000 rpm the disk can hide the short arrows in some phases.
- The pending change `preloadedAssets: []` in *Web - Module - Release* came from Unity (Input System asset no longer preloaded); check that keyboard/mouse input still works in the next module build before committing it.
