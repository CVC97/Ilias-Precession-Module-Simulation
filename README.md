# Ilias Precession Simulation

This repository contains the 3D gyroscope simulation for the ilias-precession-module. It is build with the Unity games engine / C#.

### How to push a build to pages

1. In Unity: raise the version
2. Clean build directory
```
rm -rf docs/* && touch docs/.nojekyll
```
3. In Unity: Build Profiles → Build
4. Test locally
```
python3 -m http.server -d docs 8000
```
5. Commit the code first, then the build separately
```
git add -A -- . ':!docs'
git commit -m "Bridge: SimulationTime, WebBridge"
git add docs
git commit -m "Web build 0.1.0"
```
# 6. Tag and push
git push
git tag sim-v0.1.0
git push origin sim-v0.1.0