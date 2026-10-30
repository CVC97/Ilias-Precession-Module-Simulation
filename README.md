# Ilias Precession Simulation

A 3D gyroscope simulation built with Unity 6 (URP) and C#, for the learning module [Kreiselpräzession](https://github.com/CVC97/Ilias-Precession-Module) of the physics education group in Göttingen. The equations of motion of the symmetric heavy top are integrated in plain C# (RK4, double precision); Unity only draws the result.

The project produces **two Web builds**:

| | Original | Module build |
|---|---|---|
| What | standalone demo: a toy top on a tip, noir look, controls in Unity | the lab gyroscope (and a simple top) for the learning module, controlled by the module page |
| Scene | `Assets/Scenes/SampleScene.unity` | `Assets/Module/ModuleScene.unity` |
| Build profile | *Web - Desktop - Release* | *Web - Module - Release* |
| Output | `docs/` (tracked) | `Builds/ModuleWeb/` (gitignored) |
| Release | tag `sim-v*` → GitHub Pages | tag `module-sim-v*` + GitHub release with `ModuleWeb.zip` |

**Why** it is built this way (physics model, coordinate conventions, the module's apparatus, helpers and bridge, pitfalls, open items) is in [design_notes.md](design_notes.md).


## Setup

- Unity **6000.6.4f1** with *Web Build Support*, opened through Unity Hub (*Add project from disk* → this folder).
- For scripts: VS Code with the C# Dev Kit and Unity extensions; Unity → *Preferences → External Tools* → Visual Studio Code, then *Regenerate project files*.
- For the release steps: `zip`, `python3`, and the module repo [`ilias-precession-module`](https://github.com/CVC97/Ilias-Precession-Module) next to this one.

The first opening imports everything into `Library/` and takes a few minutes. Open the scene you want to work on from `Assets/Scenes/` or `Assets/Module/`.


## Project Structure

```
Assets/
  Scripts/Physics/       GyroscopeModel, Vec3d: the physics, plain C# without Unity (assembly GyroscopePhysics)
  Scripts/               controller, SimulationTime, WebBridge of the original
  Scripts/View/          drawing: coordinate mapping, arrows, cone mesh, orbit camera, uGUI panel
  Module/                everything of the module build: scene, scripts, materials, apparatus assets
    Editor/              ModuleSceneSetup: menu Module → … (all scene changes of the module build)
    Plugins/             ModuleBridge.jslib (C# → page)
    Tests/EditMode/      NUnit tests of the physics
  WebGLTemplates/        Precession (original), PrecessionModule (module)
  Settings/Build Profiles/
Design/thumbnail.svg     source of the template thumbnail
Tools/
  package-module-build.sh  Builds/ModuleWeb/ → Builds/ModuleWeb.zip for a release
  bridge-test.html         test page for the module build's bridge
.github/workflows/pages.yml  GitHub Pages deploy of docs/ on sim-v* tags
docs/                    Web build of the original (GitHub Pages)
```


## Releasing the original (GitHub Pages)

Published at `https://cvc97.github.io/Ilias-Precession-Module-Simulation/`. The workflow uploads `docs/` **only** when a `sim-v*` tag is pushed (or when started by hand under *Actions*), so ordinary pushes don't change the page. One-time setup on GitHub: *Settings → Pages → Source: GitHub Actions*, and under *Settings → Environments → github-pages* a deployment rule for tags `sim-v*`.

1. In Unity, raise the version: *File → Build Profiles → Web - Desktop - Release* → Player settings override → *Version* (e.g. 1.0.4).
2. Clean the build folder:
   ```bash
   rm -rf docs/* && touch docs/.nojekyll
   ```
3. *Build* in the same profile and choose the `docs` folder itself (no subfolder).
4. Test locally at `http://localhost:8000`:
   ```bash
   python3 -m http.server -d docs 8000
   ```
5. Commit the code first, then the build separately:
   ```bash
   git add -A -- . ':!docs'
   git commit -m "…"
   git add docs
   git commit -m "Web build 1.0.4"
   ```
6. Tag and push; the tag starts the deployment:
   ```bash
   git push
   git tag sim-v1.0.4
   git push origin sim-v1.0.4
   ```


## Releasing the module build (for the learning module)

The module doesn't contain the build. Its file `sim.version` names a release of this repo, and its `tools/fetch-sim.sh` (also run by its deploy) downloads that release's `ModuleWeb.zip` into `frontend/public/sim/`. A new simulation version for the module therefore always goes through a GitHub release.

**When is this needed?** Only when something in this repo changed that the module build uses: C# scripts (including the shared physics), the module scene, its assets or its template. Changes to the module's panel or presets (`sim-presets.ts`) need no new build. Changes to the original alone need no module release either.

In the examples the new version is `module-sim-v1.2.0`; count up from the newest `module-sim-v*` tag (`git tag -l 'module-sim-v*'`). Everything happens on `master`.

1. **Build in Unity:** *File → Build Profiles → Web - Module - Release* → raise *Version* in its Player settings override (e.g. 1.2.0) → *Build*, output folder `Builds/ModuleWeb`.
2. **Test the bridge** without the module (optional, quick). The test page plays the module page's part and embeds `Builds/ModuleWeb/` in an iframe on the same origin. From the repo root:
   ```bash
   python3 -m http.server 8000
   ```
   and open `http://localhost:8000/Tools/bridge-test.html`: preset buttons, push, pause, state messages.
3. **Pack** the build into `Builds/ModuleWeb.zip` (the script checks that it is a module build with the bridge):
   ```bash
   Tools/package-module-build.sh
   ```
4. **Try it in the module** before releasing (pass the zip, otherwise fetch-sim installs the old release from `sim.version`):
   ```bash
   cd ../ilias-precession-module/frontend
   npm run fetch-sim -- ../../ilias-precession-simulation/Builds/ModuleWeb.zip
   npx ng serve
   ```
   Check the six sim pages (p1, p2, p4, p5, p6, p7): loading, presets, controls, readouts, gates.
5. **Commit, tag and push** this repo:
   ```bash
   cd ../../ilias-precession-simulation
   git add -A
   git commit -m "…"
   git tag module-sim-v1.2.0
   git push origin master module-sim-v1.2.0
   ```
6. **Create the release:** on GitHub *Releases → Draft a new release*, choose the tag `module-sim-v1.2.0`, attach `Builds/ModuleWeb.zip` (the file name must stay `ModuleWeb.zip`), *Publish release*.
7. **Pin it in the module:** write the tag into its `sim.version`, check the download, commit and push its `master` (the deploy fetches the release itself):
   ```bash
   cd ../ilias-precession-module
   echo module-sim-v1.2.0 > sim.version
   bash tools/fetch-sim.sh
   git commit -am "Simulation module-sim-v1.2.0"
   git push
   ```
   Live check: `https://interapt.uni-goettingen.de/precession/sim/sim-version.txt` shows the new tag.

**If the bridge changes** (a new command, a state field, a preset key): update the module side in the same go (`shared/sim-embed/` there) and section 4 of the module's `design_notes.md`, which is the page side's description of the contract. Commands also have to be added to the allowlist in `Assets/WebGLTemplates/PrecessionModule/index.html`.


## Working on the module build

- **Scene changes go through editor scripts:** menu *Module → …* (`Assets/Module/Editor/ModuleSceneSetup.cs`). *Apply studio look* and the cone items are rerunnable and overwrite manual tweaks of what they set; the other items run once.
- **Don't commit the TMP font assets** `LiberationSans SDF.asset` and `LiberationSans SDF - Fallback.asset`: TMP re-serializes them, and both builds share them. They are marked `git update-index --skip-worktree` locally; after a fresh clone, set it again:
  ```bash
  git update-index --skip-worktree "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset" "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
  ```
- **Tests:** Unity → *Window → General → Test Runner → EditMode* runs `Assets/Module/Tests/EditMode`. The original scene must keep working after changes to shared code (`Assets/Scripts/`): open `SampleScene`, press Play.
