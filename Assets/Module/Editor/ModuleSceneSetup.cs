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
//   Module → Use module controller  (step 2, once) swaps GyroscopeController for ModuleGyroscopeController
//   Module → Build lab gyroscope    (step 3, once) replaces the toy top by the lab apparatus (dimensions from LabGyroscope)
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
    private const string LabAssetPath = "Assets/Module/LabGyroscope.asset";
    private const string MeshFolder = "Assets/Module/Meshes";
    private const string CylinderMeshPath = "Assets/Module/Meshes/SmoothCylinder.asset";

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


    [MenuItem("Module/Use module controller")]
    private static void UseModuleController() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ModuleScenePath) {
            EditorUtility.DisplayDialog("Module scene", $"Open {ModuleScenePath} first.", "OK");
            return;
        }
        GameObject gyroscope = FindInScene(scene, "Gyroscope").gameObject;
        var oldController = gyroscope.GetComponent<GyroscopeController>();
        if (oldController == null) {
            EditorUtility.DisplayDialog("Module scene", "The gyroscope already uses ModuleGyroscopeController.", "OK");
            return;
        }

        // the module scene gets its own copy of the gyroscope: from now on changes on either side don't reach the other
        if (PrefabUtility.IsPartOfPrefabInstance(gyroscope)) {
            PrefabUtility.UnpackPrefabInstance(gyroscope, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        // new controller with the old one's scene references; private [SerializeField] fields are set via SerializedObject
        var oldSettings = new SerializedObject(oldController);
        var controller = gyroscope.AddComponent<ModuleGyroscopeController>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("lab").objectReferenceValue = LabAsset();
        settings.FindProperty("rotor").objectReferenceValue = oldSettings.FindProperty("rotor").objectReferenceValue;
        settings.FindProperty("trail").objectReferenceValue = oldSettings.FindProperty("trail").objectReferenceValue;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Object.DestroyImmediate(oldController);

        // arrow scales for the lab values (L ≈ 0.4 kg·m²/s, ω ≈ 40 rad/s, D ≈ 0.08 N·m); provisional until step 3
        var vectors = new SerializedObject(gyroscope.GetComponent<VectorDisplay>());
        vectors.FindProperty("scaleL").floatValue = 0.6f;
        vectors.FindProperty("scaleOmega").floatValue = 0.005f;
        vectors.FindProperty("scaleD").floatValue = 2f;
        vectors.ApplyModifiedPropertiesWithoutUndo();

        // the uGUI dev panel and the logging test of the original are wired to the old controller: off in the module
        FindInScene(scene, "Canvas").gameObject.SetActive(false);
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == "ModelTest") root.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = gyroscope;
        Debug.Log("Module scene uses ModuleGyroscopeController now.");
    }


    // lab apparatus (Bild 4.1 of the script), built from primitives; local up of the Gyroscope object = figure axis,
    // pointing to the counterweight side
    private const float PivotHeight = 0.33f;                                    // m above the floor
    private const float StringLength = 0.12f;                                   // m, notch to the first disc

    [MenuItem("Module/Build lab gyroscope")]
    private static void BuildLabGyroscope() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ModuleScenePath) {
            EditorUtility.DisplayDialog("Module scene", $"Open {ModuleScenePath} first.", "OK");
            return;
        }
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        var controller = gyroscope.GetComponent<ModuleGyroscopeController>();
        if (controller == null) {
            EditorUtility.DisplayDialog("Module scene", "Run Module → Use module controller first.", "OK");
            return;
        }
        if (gyroscope.Find("Body/Counterweight") != null) {
            EditorUtility.DisplayDialog("Module scene", "The lab gyroscope is already built.", "OK");
            return;
        }
        LabGyroscope lab = LabAsset();
        float wheelRadius = lab.WheelRadius, wheelDistance = lab.WheelDistance, notch = lab.NotchDistance;
        float axleStart = -(wheelDistance + 0.06f), axleEnd = notch + 0.02f;

        // stand: base plate, rod, bearing block that turns with φ
        Transform stand = FindInScene(scene, "Stand");
        if (PrefabUtility.IsPartOfPrefabInstance(stand.gameObject)) {
            PrefabUtility.UnpackPrefabInstance(stand.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        DeleteChildren(stand);
        stand.SetPositionAndRotation(new Vector3(gyroscope.position.x, 0f, gyroscope.position.z), Quaternion.identity);
        Part("Base", PrimitiveType.Cube, stand, new Vector3(0f, 0.015f, 0f), new Vector3(0.36f, 0.03f, 0.16f), "ModuleBase");
        float rodTop = PivotHeight - 0.025f;
        Part("Rod", PrimitiveType.Cylinder, stand, new Vector3(0f, (0.03f + rodTop) / 2, 0f),
             new Vector3(0.012f, (rodTop - 0.03f) / 2, 0.012f), "ModuleTower");
        Transform head = Empty("PivotHead", stand, new Vector3(0f, PivotHeight, 0f));
        Part("Block", PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.04f, 0.05f, 0.04f), "ModuleMarker");
        SetReference(head.gameObject.AddComponent<PivotHead>(), "controller", controller);

        // the gyroscope object sits in the pivot
        gyroscope.position = new Vector3(gyroscope.position.x, PivotHeight, gyroscope.position.z);

        // rotor: wheel with hub and the strip for the light barrier, on the far side of the pivot
        Transform rotor = gyroscope.Find("Rotor");
        DeleteChildren(rotor);
        rotor.localPosition = new Vector3(0f, -wheelDistance, 0f);
        Part("Wheel", PrimitiveType.Cylinder, rotor, Vector3.zero, new Vector3(2*wheelRadius, 0.015f, 2*wheelRadius), "ModuleWheel");
        Part("Hub", PrimitiveType.Cylinder, rotor, Vector3.zero, new Vector3(0.05f, 0.025f, 0.05f), "ModuleTower");
        Part("Strip", PrimitiveType.Cube, rotor, new Vector3(wheelRadius + 0.008f, 0f, 0f), new Vector3(0.02f, 0.012f, 0.003f), "ModuleMarker");

        // body: axle, counterweight, axle end with the trail, add-on weight in the notch
        Transform body = gyroscope.Find("Body");
        foreach (string oldPart in new[] { "PivotBall", "AxleTip", "Axle" }) {
            Transform t = body.Find(oldPart);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Part("Axle", PrimitiveType.Cylinder, body, new Vector3(0f, (axleStart + axleEnd) / 2, 0f),
             new Vector3(0.006f, (axleEnd - axleStart) / 2, 0.006f), "ModuleAxle");
        Part("Counterweight", PrimitiveType.Cylinder, body, new Vector3(0f, lab.CounterweightDistance, 0f),
             new Vector3(0.058f, 0.0215f, 0.058f), "ModuleAxle");
        Transform tip = body.Find("Tip");                                       // carries the TrailRenderer
        tip.localPosition = new Vector3(0f, axleEnd, 0f);
        tip.GetComponent<TrailRenderer>().widthMultiplier = 0.003f;

        Transform weight = Empty("AddOnWeight", body, new Vector3(0f, notch, 0f));
        Transform hanger = Empty("StringAndHook", weight, Vector3.zero);
        Part("String", PrimitiveType.Cylinder, hanger, new Vector3(0f, -StringLength / 2, 0f),
             new Vector3(0.0015f, StringLength / 2, 0.0015f), "ModuleMarker");
        Part("Hook", PrimitiveType.Cylinder, hanger, new Vector3(0f, -StringLength - 0.004f, 0f),
             new Vector3(0.006f, 0.006f, 0.006f), "ModuleMarker");
        var discs = new GameObject[6];
        for (int i = 0; i < discs.Length; i++) {
            discs[i] = Part($"Disc{(i + 1) * 10}g", PrimitiveType.Cylinder, weight,
                            new Vector3(0f, -StringLength - 0.013f - 0.006f*i, 0f), new Vector3(0.04f, 0.0025f, 0.04f), "ModuleMarker").gameObject;
        }
        var hanging = new SerializedObject(weight.gameObject.AddComponent<HangingWeight>());
        hanging.FindProperty("controller").objectReferenceValue = controller;
        hanging.FindProperty("stringAndHook").objectReferenceValue = hanger.gameObject;
        SerializedProperty discList = hanging.FindProperty("discs");
        discList.arraySize = discs.Length;
        for (int i = 0; i < discs.Length; i++) discList.GetArrayElementAtIndex(i).objectReferenceValue = discs[i];
        hanging.ApplyModifiedPropertiesWithoutUndo();

        // arrow lengths with the defaults: L ≈ 0.35 m, ω ≈ 0.22 m, D ≈ 0.2 m (thickness: ApplyLook)
        var vectors = new SerializedObject(gyroscope.GetComponent<VectorDisplay>());
        vectors.FindProperty("scaleL").floatValue = 0.9f;
        vectors.FindProperty("scaleOmega").floatValue = 0.0053f;
        vectors.FindProperty("scaleD").floatValue = 2.5f;
        vectors.ApplyModifiedPropertiesWithoutUndo();

        // camera for the larger apparatus
        var orbit = new SerializedObject(FindInScene(scene, "Main Camera").GetComponent<OrbitCamera>());
        orbit.FindProperty("targetOffset").vector3Value = new Vector3(0f, -0.08f, 0f);
        orbit.FindProperty("distance").floatValue = 1.1f;
        orbit.FindProperty("minDistance").floatValue = 0.6f;
        orbit.FindProperty("maxDistance").floatValue = 2.0f;
        orbit.ApplyModifiedPropertiesWithoutUndo();

        ApplyLook(scene);                                                       // fog distances and light follow the new size
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Lab gyroscope built.");
    }


    // primitive without collider (no physics engine involved: we integrate ourselves)
    private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, string material) {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}.mat");
        return go.transform;
    }


    private static Transform Empty(string name, Transform parent, Vector3 localPosition) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }


    private static void DeleteChildren(Transform t) {
        for (int i = t.childCount - 1; i >= 0; i--) {
            Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }


    private static void SetReference(Object component, string field, Object value) {
        var so = new SerializedObject(component);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }


    private static LabGyroscope LabAsset() {
        var lab = AssetDatabase.LoadAssetAtPath<LabGyroscope>(LabAssetPath);
        if (lab == null) {
            lab = ScriptableObject.CreateInstance<LabGyroscope>();                  // with the default values from the class
            AssetDatabase.CreateAsset(lab, LabAssetPath);
        }
        return lab;
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

        // apparatus: smooth cylinders instead of the 20-sided primitive (the wheel rim showed facets);
        // arrows slightly thicker than the axle (6 mm), on which L and ω lie, so the surfaces don't z-fight
        Mesh cylinder = SmoothCylinder();
        foreach (string root in new[] { "Gyroscope", "Stand" }) {
            foreach (MeshFilter filter in FindInScene(scene, root).GetComponentsInChildren<MeshFilter>(true)) {
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Cylinder") {
                    filter.sharedMesh = cylinder;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                }
            }
        }
        foreach (VectorArrow3D arrow in FindInScene(scene, "Gyroscope").GetComponentsInChildren<VectorArrow3D>(true)) {
            var a = new SerializedObject(arrow);
            a.FindProperty("shaftDiameter").floatValue = 0.007f;
            a.FindProperty("headDiameter").floatValue = 0.020f;
            a.FindProperty("headLength").floatValue = 0.038f;
            a.ApplyModifiedPropertiesWithoutUndo();
        }

        // stage: large floor that fades into the background through fog of the same colour
        FindInScene(scene, "Floor").localScale = new Vector3(30f, 0.001f, 30f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = StageColor;
        RenderSettings.fogStartDistance = 2.6f;                                     // beyond the camera's max distance (2 m)
        RenderSettings.fogEndDistance = 7f;

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


    // like Unity's cylinder primitive (radius 0.5, y from −1 to 1, so all scales stay valid), but with 96 sides;
    // smooth normals on the side, flat caps with their own vertices (sharp rim edge). Saved once as a mesh asset.
    private static Mesh SmoothCylinder() {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CylinderMeshPath);
        if (mesh != null) {
            return mesh;
        }
        const int segments = 96;
        const float r = 0.5f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        // side: bottom/top vertex pairs with radial normals (front faces wind so that cross(B−A, C−A) points outwards)
        for (int i = 0; i <= segments; i++) {
            float a = 2*Mathf.PI/segments * i;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices.Add(new Vector3(r*radial.x, -1f, r*radial.z));
            vertices.Add(new Vector3(r*radial.x, 1f, r*radial.z));
            normals.Add(radial);
            normals.Add(radial);
        }
        for (int i = 0; i < segments; i++) {
            int b = 2*i;                                                            // bottom_i, top_i, bottom_i+1, top_i+1
            triangles.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
        }

        // caps: centre + ring, normal ±y
        foreach (float y in new[] { 1f, -1f }) {
            int centre = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));
            normals.Add(new Vector3(0f, y, 0f));
            for (int i = 0; i <= segments; i++) {
                float a = 2*Mathf.PI/segments * i;
                vertices.Add(new Vector3(r*Mathf.Cos(a), y, r*Mathf.Sin(a)));
                normals.Add(new Vector3(0f, y, 0f));
            }
            for (int i = 0; i < segments; i++) {
                int ring = centre + 1 + i;
                if (y > 0) triangles.AddRange(new[] { centre, ring + 1, ring });
                else triangles.AddRange(new[] { centre, ring, ring + 1 });
            }
        }

        mesh = new Mesh { name = "SmoothCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(ModuleFolder, "Meshes");
        AssetDatabase.CreateAsset(mesh, CylinderMeshPath);
        return mesh;
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
