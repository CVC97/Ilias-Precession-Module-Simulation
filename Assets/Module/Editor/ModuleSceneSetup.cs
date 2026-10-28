using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;


// Step 1 of the module version: copies the original scene and gives the copy the neutral module look.
// Menu: Module → Create module scene. The original scene and its materials are not touched.
// To redo it: delete Assets/Module/ModuleScene.unity and run it again (existing module materials are kept).
public static class ModuleSceneSetup {
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string ModuleFolder = "Assets/Module";
    private const string MaterialFolder = "Assets/Module/Materials";
    private const string ModuleScenePath = "Assets/Module/ModuleScene.unity";

    // module colours (sRGB), from ../ilias-precession-module/frontend/src/styles.css
    private static readonly Color32 PageBackground = new Color32(240, 242, 245, 255);  // #f0f2f5
    private static readonly Color32 UniBlau1 = new Color32(21, 50, 104, 255);
    private static readonly Color32 Grau20 = new Color32(217, 218, 218, 255);
    private static readonly Color32 ColorL = new Color32(199, 13, 45, 255);            // red
    private static readonly Color32 ColorOmega = new Color32(0, 145, 200, 255);        // uni-blau-3
    private static readonly Color32 ColorD = new Color32(253, 166, 66, 255);           // yellow-orange


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
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(ModuleFolder, "Materials");
        AssetDatabase.CopyAsset(SourceScene, ModuleScenePath);                      // the copy gets a new GUID

        // original material → matte module copy
        var remap = new Dictionary<Material, Material> {
            [Original("Disk")] = MatteCopy("Disk", "ModuleWheel", new Color32(200, 202, 205, 255), 0.3f),
            [Original("Marker")] = MatteCopy("Marker", "ModuleMarker", UniBlau1, 0.2f),
            [Original("Axle")] = MatteCopy("Axle", "ModuleAxle", new Color32(150, 153, 158, 255), 0.3f),
            [Original("Tower")] = MatteCopy("Tower", "ModuleTower", new Color32(120, 124, 130, 255), 0.2f),
            [Original("Base")] = MatteCopy("Base", "ModuleBase", UniBlau1, 0.2f),
            [Original("Floor")] = MatteCopy("Floor", "ModuleFloor", Grau20, 0.05f),
            [Original("ArrowL")] = MatteCopy("ArrowL", "ModuleArrowL", ColorL, 0.15f),
            [Original("ArrowOmega")] = MatteCopy("ArrowOmega", "ModuleArrowOmega", ColorOmega, 0.15f),
            [Original("ArrowD")] = MatteCopy("ArrowD", "ModuleArrowD", ColorD, 0.15f),
            [Original("TrailWhite")] = TintedCopy("TrailWhite", "ModuleTrail", UniBlau1),
        };
        AssetDatabase.SaveAssets();

        Scene scene = EditorSceneManager.OpenScene(ModuleScenePath, OpenSceneMode.Single);

        // remove what the module doesn't need
        foreach (string path in new[] { "Snow", "Snow (fast)", "Global Volume", "ConeTest", "Canvas/ControlPanel/Schnee" }) {
            Transform t = FindInScene(scene, path);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // background: solid page colour instead of the HDR skybox, no post-processing (no bloom, vignette, tonemapping)
        Camera cam = FindInScene(scene, "Main Camera").GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = PageBackground;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        // soft light: bright ambient from three directions, one directional key light with soft shadows, no rim light
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(235, 238, 243, 255);
        RenderSettings.ambientEquatorColor = new Color32(205, 209, 215, 255);
        RenderSettings.ambientGroundColor = new Color32(160, 162, 166, 255);
        Lightmapping.lightingDataAsset = null;                                      // don't share the original's baked lighting

        Light key = FindInScene(scene, "KeyLight").GetComponent<Light>();
        key.type = LightType.Directional;                                           // was a spot (light cone on the floor)
        key.color = Color.white;
        key.intensity = 1.2f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.4f;
        FindInScene(scene, "Main Camera/RimLight").gameObject.SetActive(false);

        // swap the materials on every renderer (incl. inactive ones and those inside prefab instances)
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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Module scene created: {ModuleScenePath}");
    }


    private static Material Original(string name) {
        return AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
    }


    // copy of an original material (keeps shader and keywords); an existing copy is reused, so tweaks survive a rerun
    private static Material CopyOf(string source, string name, out bool isNew) {
        string path = $"{MaterialFolder}/{name}.mat";
        isNew = AssetDatabase.LoadAssetAtPath<Material>(path) == null;
        if (isNew) AssetDatabase.CopyAsset($"Assets/Materials/{source}.mat", path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }


    // URP/Lit: colour, not metallic, low smoothness, no emission
    private static Material MatteCopy(string source, string name, Color color, float smoothness) {
        Material mat = CopyOf(source, name, out bool isNew);
        if (isNew) {
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }


    // unlit/particle materials (trail): only the colour
    private static Material TintedCopy(string source, string name, Color color) {
        Material mat = CopyOf(source, name, out bool isNew);
        if (isNew) {
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }
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
