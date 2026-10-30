# Ilias Precession Simulation

3D gyroscope simulation (Unity 6000.6.4f1, URP, C#) for the learning module [Kreiselpräzession](https://github.com/CVC97/Ilias-Precession-Module). Two Web builds from one project, all on `master`:

- **Original** (`Assets/Scenes/SampleScene`): standalone noir demo → `docs/` → [GitHub Pages](https://cvc97.github.io/Ilias-Precession-Module-Simulation/), tags `sim-v*`.
- **Module build** (`Assets/Module/ModuleScene`): lab gyroscope + simple top, controlled by the module page → GitHub release `module-sim-v*` with `ModuleWeb.zip`.

Why it is built this way, conventions, pitfalls and open items: [design_notes.md](design_notes.md).


## Release the original

```bash
rm -rf docs/* && touch docs/.nojekyll
# Unity: Build Profiles → Web - Desktop - Release → raise Version → Build into docs/ itself
python3 -m http.server -d docs 8000                      # test at localhost:8000
git add -A -- . ':!docs' && git commit -m "…"
git add docs && git commit -m "Web build 1.0.4"
git push && git tag sim-v1.0.4 && git push origin sim-v1.0.4   # the tag deploys
```


## Release the module build

Only needed when scripts, the module scene or its assets changed (not for changes to the module's panel or presets).

```bash
# Unity: Build Profiles → Web - Module - Release → raise Version → Build into Builds/ModuleWeb
python3 -m http.server 8000                              # optional: localhost:8000/Tools/bridge-test.html
Tools/package-module-build.sh                            # → Builds/ModuleWeb.zip
cd ../ilias-precession-module/frontend && npm run fetch-sim -- ../../ilias-precession-simulation/Builds/ModuleWeb.zip && npx ng serve   # try it
cd ../../ilias-precession-simulation
git add -A && git commit -m "…" && git tag module-sim-v1.2.0 && git push origin master module-sim-v1.2.0
# GitHub: Releases → new release for the tag, attach Builds/ModuleWeb.zip (keep the name)
cd ../ilias-precession-module && echo module-sim-v1.2.0 > sim.version && bash tools/fetch-sim.sh
git commit -am "Simulation module-sim-v1.2.0" && git push   # the module's deploy fetches it
```


## Rules

- Module scene changes go through the menu *Module → …* (`Assets/Module/Editor/ModuleSceneSetup.cs`), not by hand.
- Never commit the TMP `LiberationSans SDF.asset` / `LiberationSans SDF - Fallback.asset`; after a fresh clone run `git update-index --skip-worktree` on both.
- Bridge changes (commands, state fields, preset keys): also update the template's allowlist (`Assets/WebGLTemplates/PrecessionModule/index.html`), the module's `shared/sim-embed/` and section 4 of its `design_notes.md`.
