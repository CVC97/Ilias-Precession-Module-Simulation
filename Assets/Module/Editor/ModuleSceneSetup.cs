using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;


// Module version, step 1: the scene copy and its look. The original scene and its materials are not touched.
//   Module → Create module scene   copies the original scene once, removes what the module doesn't need, applies the look
//   Module → Apply studio look     (re)applies materials, lights, fog, reflections and post-processing to the open module scene;
//                                  overwrites manual changes to these, so tune the values here while we iterate
// The flat look of the first version is commit a435b47.
public static class ModuleSceneSetup {
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string ModuleFolder = "Assets/Module";
    private const string MaterialFolder = "Assets/Module/Materials";
    private const string ModuleScenePath = "Assets/Module/ModuleScene.unity";
    private const string VolumeProfilePath = "Assets/Module/ModuleVolumeProfile.asset";
    private const string StudioHdr = "Assets/Textures/studio_small_09_2k.hdr";         // the original's visible skybox
    private const string TextureFolder = "Assets/Module/Textures";
    private const string ReflectionCubemapPath = "Assets/Module/Textures/studio_reflection.hdr";
    private const int ReflectionFaceSize = 128;                                         // px per cube face; reflections only, never visible

    // module colours (sRGB), from ../ilias-precession-module/frontend/src/styles.css
    private static readonly Color32 UniBlau1 = new Color32(21, 50, 104, 255);
    private static readonly Color32 UniBlau3 = new Color32(0, 145, 200, 255);
    private static readonly Color32 YellowOrange = new Color32(253, 166, 66, 255);

    // stage: background = fog colour, so the floor fades into it from every camera angle
    private static readonly Color32 StageColor = new Color32(222, 227, 233, 255);


    [MenuItem("Module/Create module scene")]
    private static void CreateModuleScene() {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ModuleScenePath) != null) {
            EditorUtility.DisplayDialog("Module scene", $"{ModuleScenePath} already exists. Delete it first to recreate it.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            return;                                                                 // user pressed Cancel
        }

        if (!AssetDatabase.IsValidFolder(ModuleFolder)) AssetDatabase.CreateFolder("Assets", "Module");
        AssetDatabase.CopyAsset(SourceScene, ModuleScenePath);                      // the copy gets a new GUID
        Scene scene = EditorSceneManager.OpenScene(ModuleScenePath, OpenSceneMode.Single);

        // remove what the module doesn't need (the old post-processing volume belongs to the original scene)
        foreach (string path in new[] { "Snow", "Snow (fast)", "Global Volume", "ConeTest", "Canvas/ControlPanel/Schnee" }) {
            Transform t = FindInScene(scene, path);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Lightmapping.lightingDataAsset = null;                                      // don't share the original's baked lighting

        ApplyLook(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Module scene created: {ModuleScenePath}");
    }


    [MenuItem("Module/Apply studio look")]
    private static void ApplyStudioLook() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ModuleScenePath) {
            EditorUtility.DisplayDialog("Module scene", $"Open {ModuleScenePath} first.", "OK");
            return;
        }
        ApplyLook(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Studio look applied.");
    }


    private static void ApplyLook(Scene scene) {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(ModuleFolder, "Materials");

        // materials: metal for the apparatus, lacquer in uni-blau-1 for the base, vectors in module colours
        // Lit(source, name, colour, metallic, smoothness, emission = fraction of the colour that glows)
        var remap = new Dictionary<Material, Material> {
            [Original("Disk")] = Lit("Disk", "ModuleWheel", new Color32(170, 175, 182, 255), 0.9f, 0.4f),         // brushed aluminium (rough: the flat face mirrors the spot otherwise)
            [Original("Marker")] = Lit("Marker", "ModuleMarker", new Color32(35, 38, 45, 255), 0f, 0.5f),          // dark stripe
            [Original("Axle")] = Lit("Axle", "ModuleAxle", new Color32(215, 217, 220, 255), 1f, 0.7f),            // polished steel (not too glossy: glare at the pivot)
            [Original("Tower")] = Lit("Tower", "ModuleTower", new Color32(185, 190, 196, 255), 0.9f, 0.5f),        // satin aluminium
            [Original("Base")] = Lit("Base", "ModuleBase", UniBlau1, 0.2f, 0.75f),                                 // blue lacquer
            [Original("Floor")] = Lit("Floor", "ModuleFloor", new Color32(232, 235, 240, 255), 0f, 0.3f),
            [Original("ArrowOmega")] = Lit("ArrowOmega", "ModuleArrowOmega", UniBlau1, 0f, 0.6f, 0.25f),
            [Original("ArrowL")] = Lit("ArrowL", "ModuleArrowL", UniBlau3, 0f, 0.6f, 0.25f),
            [Original("ArrowD")] = Lit("ArrowD", "ModuleArrowD", YellowOrange, 0f, 0.6f, 0.25f),
            [Original("TrailWhite")] = Tinted("TrailWhite", "ModuleTrail", new Color32(90, 98, 110, 255)),
        };
        AssetDatabase.SaveAssets();
        SwapMaterials(scene, remap);

        // stage: large floor that fades into the background through fog of the same colour
        FindInScene(scene, "Floor").localScale = new Vector3(30f, 0.001f, 30f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = StageColor;
        RenderSettings.fogStartDistance = 1.6f;                                     // beyond the camera's max distance (1.35 m)
        RenderSettings.fogEndDistance = 5f;

        Camera cam = FindInScene(scene, "Main Camera").GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = StageColor;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // light: soft spot from above (gentler than the original), rim light from behind for edge highlights
        Vector3 pivot = FindInScene(scene, "Gyroscope").position;
        Light key = FindInScene(scene, "KeyLight").GetComponent<Light>();
        key.type = LightType.Spot;
        key.transform.position = pivot + new Vector3(0.35f, 1.0f, -0.35f);
        key.transform.LookAt(pivot);
        key.color = new Color32(255, 246, 234, 255);                                // slightly warm
        key.intensity = 5f;
        key.range = 4f;
        key.spotAngle = 70f;                                                        // wide cone, soft edge
        key.innerSpotAngle = 30f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.65f;

        Transform rim = FindInScene(scene, "Main Camera/RimLight");
        rim.gameObject.SetActive(true);
        Light rimLight = rim.GetComponent<Light>();
        rimLight.color = new Color32(230, 238, 255, 255);                           // slightly cool
        rimLight.intensity = 0.8f;

        // ambient from three directions; reflections from the studio HDR (gives the metal its softbox highlights)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(190, 197, 208, 255);
        RenderSettings.ambientEquatorColor = new Color32(165, 172, 182, 255);
        RenderSettings.ambientGroundColor = new Color32(110, 114, 120, 255);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = ReflectionCubemap();
        RenderSettings.reflectionIntensity = 0.9f;

        ApplyPostProcessing(scene);
    }


    // gentle post-processing: neutral tonemapping (keeps the module colours), more contrast, faint bloom on highlights,
    // soft blue vignette
    private static void ApplyPostProcessing(Scene scene) {
        AssetDatabase.DeleteAsset(VolumeProfilePath);                               // rebuilt from scratch on every run
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        Tonemapping tonemapping = AddOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments colorAdjustments = AddOverride<ColorAdjustments>(profile);
        colorAdjustments.contrast.Override(20f);                                    // -100 … 100
        colorAdjustments.postExposure.Override(-0.2f);                              // in stops; keeps the brighter whites from clipping

        Bloom bloom = AddOverride<Bloom>(profile);
        bloom.threshold.Override(1.2f);
        bloom.intensity.Override(0.2f);
        bloom.scatter.Override(0.6f);

        Vignette vignette = AddOverride<Vignette>(profile);
        vignette.color.Override((Color)UniBlau1);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.6f);
        AssetDatabase.SaveAssets();

        Transform volumeObject = FindInScene(scene, "Module Volume");
        if (volumeObject == null) {
            var go = new GameObject("Module Volume", typeof(Volume));
            SceneManager.MoveGameObjectToScene(go, scene);
            volumeObject = go.transform;
        }
        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }


    // small copy of the studio HDR for the reflections: the original's skybox import (512 px faces, 2 MB in the
    // web build) stays untouched, the copy only costs ~0.1 MB
    private static Cubemap ReflectionCubemap() {
        if (AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionCubemapPath) == null) {
            if (!AssetDatabase.IsValidFolder(TextureFolder)) AssetDatabase.CreateFolder(ModuleFolder, "Textures");
            AssetDatabase.CopyAsset(StudioHdr, ReflectionCubemapPath);              // copies the import settings too
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(ReflectionCubemapPath);
        TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
        if (importer.maxTextureSize != ReflectionFaceSize || !web.overridden || web.maxTextureSize != ReflectionFaceSize) {
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.maxTextureSize = ReflectionFaceSize;                           // default (editor and other platforms)
            web.overridden = true;                                                  // the web build, explicitly
            web.maxTextureSize = ReflectionFaceSize;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionCubemapPath);
    }


    // volume components are sub-assets of the profile file
    private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent {
        T component = profile.Add<T>();
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }


    // swap the materials on every renderer (incl. inactive ones and those inside prefab instances)
    private static void SwapMaterials(Scene scene, Dictionary<Material, Material> remap) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) {
                    if (mats[i] != null && remap.TryGetValue(mats[i], out Material moduleMat)) mats[i] = moduleMat;
                }
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);         // keep the change as a scene override
            }
        }
    }


    private static Material Original(string name) {
        return AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
    }


    // module copy of an original material (keeps shader and keywords); created once, then reused
    private static Material CopyOf(string source, string name) {
        string path = $"{MaterialFolder}/{name}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) {
            AssetDatabase.CopyAsset($"Assets/Materials/{source}.mat", path);
        }
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }


    // URP/Lit
    private static Material Lit(string source, string name, Color color, float metallic, float smoothness, float emission = 0f) {
        Material mat = CopyOf(source, name);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission > 0f) {
            mat.SetColor("_EmissionColor", color * emission);                       // keeps the vector colour readable on the shadow side
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        } else {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }


    // unlit/particle materials (trail): only the colour
    private static Material Tinted(string source, string name, Color color) {
        Material mat = CopyOf(source, name);
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }


    // "Root/Child/Grandchild"; unlike GameObject.Find it also finds inactive objects
    private static Transform FindInScene(Scene scene, string path) {
        string[] parts = path.Split('/', 2);
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == parts[0]) {
                return parts.Length == 1 ? root.transform : root.transform.Find(parts[1]);
            }
        }
        return null;
    }
}
