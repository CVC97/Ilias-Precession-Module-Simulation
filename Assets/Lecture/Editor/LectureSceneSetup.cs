using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;


// The lecture gyroscope (Vorlesungskreisel) in the noir look of the original, built by editor script like the module
// scene (ModuleSceneSetup); the original scene and its assets are not changed, only referenced (arrow, marker, trail
// and label materials, the post-processing profile).
//   Lecture → 1 Create lecture scene      (once) copies the original scene, removes the toy top's parts, its stand and the
//                                          uGUI panel; keeps camera, lights, floor, volume, snow, arrows, labels, trail
//   Lecture → 2 Build lecture gyroscope   (once) disk, rod, tripod and ring as one spinning body on a pin, with pedestal,
//                                          controller (ModuleGyroscopeController with only a lecture rig)
//   Lecture → 3 Apply lecture look        (rerunnable) materials, disk picture, lights, snow, camera, arrow and label sizes;
//                                          overwrites manual changes to these, so tune the values here
//   Lecture → 4 Add lecture helpers       (rerunnable) switchboard, precession cone and the three nutation cones as light cones
//   Lecture → 5 Use lecture web bridge    (rerunnable) ModuleWebBridge on the WebBridge object, without the assembly game
//   Lecture → Set up everything           all five in order (also LectureSceneSetup.SetUpFromCommandLine in batch mode)
// Local up of the Gyroscope object is the figure axis (rod, disk side); it sits exactly on the tip of the pin.
public static class LectureSceneSetup {
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string LectureFolder = "Assets/Lecture";
    private const string ScenePath = "Assets/Lecture/LectureScene.unity";
    private const string AssetPath = "Assets/Lecture/LectureGyroscope.asset";
    private const string MaterialFolder = "Assets/Lecture/Materials";
    private const string TextureFolder = "Assets/Lecture/Textures";
    private const string MeshFolder = "Assets/Lecture/Meshes";
    // Carlo's own picture for the disk (square, the disk is the inscribed circle); without it a generated placeholder
    private const string DiskArtPath = "Assets/Lecture/Textures/DiskArt.png";
    private const string DiskArtPlaceholderPath = "Assets/Lecture/Textures/DiskArtPlaceholder.png";

    // stage: the tip of the pin, m above the floor (a tall pedestal: the lecture top stands well above the table)
    private const float PivotHeight = 0.80f;
    // the reflections for the metal (the module's small studio cubemap, dimmed): without any, steel and chrome mirror
    // the black stage and look black; the copied scene loses the original's baked reflection probe
    private const string ReflectionCubemapPath = "Assets/Module/Textures/studio_reflection.hdr";
    // a short spike on top of the hub: the trail, the arrows and the precession cone's rim are at its tip
    private const float SpikeLength = 0.06f;
    // the scale between the original toy top (camera 0.5 m) and this apparatus (camera 2 m): lights, snow, labels
    private const float SceneScale = 4f;

    // noir colours: only the original's (L yellow, ω white, D red) and greys; precession light grey (Carlo)
    private static readonly Color32 Yellow = new Color32(255, 235, 4, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 LightGrey = new Color32(220, 223, 230, 255);
    private static readonly Color32 Grey = new Color32(170, 175, 185, 255);


    [MenuItem("Lecture/1 Create lecture scene")]
    private static void CreateMenu() => CreateLectureScene();

    [MenuItem("Lecture/2 Build lecture gyroscope")]
    private static void BuildMenu() => Run(BuildLectureGyroscope);

    [MenuItem("Lecture/3 Apply lecture look")]
    private static void LookMenu() => Run(ApplyLectureLook);

    [MenuItem("Lecture/4 Add lecture helpers")]
    private static void HelpersMenu() => Run(AddLectureHelpers);

    [MenuItem("Lecture/5 Use lecture web bridge")]
    private static void BridgeMenu() => Run(UseLectureWebBridge);

    [MenuItem("Lecture/Set up everything")]
    private static void SetUpMenu() => SetUpEverything();


    // Unity -batchmode -nographics -projectPath . -executeMethod LectureSceneSetup.SetUpFromCommandLine -logFile setup.log
    public static void SetUpFromCommandLine() {
        EditorApplication.Exit(SetUpEverything() ? 0 : 1);
    }


    private static bool SetUpEverything() {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) {
            if (!CreateLectureScene()) return false;
        } else {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Scene scene = SceneManager.GetActiveScene();
        if (FindInScene(scene, "LecturePedestal") == null && !Run(BuildLectureGyroscope)) return false;
        return Run(ApplyLectureLook) && Run(AddLectureHelpers) && Run(UseLectureWebBridge);
    }


    // runs a step on the open lecture scene and saves it
    private static bool Run(System.Func<Scene, bool> step) {
        if (EditorApplication.isPlaying) {                                      // scene changes would be lost on exit
            Fail("Leave Play mode first: in Play mode the scene can't be saved.");
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) {
            Fail($"Open {ScenePath} first (Lecture → 1 Create lecture scene makes it).");
            return false;
        }
        if (!step(scene)) return false;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }


    private static void Fail(string message) {
        if (Application.isBatchMode) Debug.LogError("LectureSceneSetup: " + message);
        else EditorUtility.DisplayDialog("Lecture scene", message, "OK");
    }


    // ---- 1 ----

    private static bool CreateLectureScene() {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) {
            Fail($"{ScenePath} already exists. Delete it first to recreate it.");
            return false;
        }
        if (EditorApplication.isPlaying) {
            Fail("Leave Play mode first: in Play mode the scene can't be saved.");
            return false;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        EnsureFolders();
        AssetDatabase.CopyAsset(SourceScene, ScenePath);                       // the copy gets a new GUID
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // the toy top's stand, the uGUI dev panel, the console tests
        foreach (string path in new[] { "Stand", "Canvas", "EventSystem", "ModelTest", "ConeTest" }) {
            Transform t = FindInScene(scene, path);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Lightmapping.lightingDataAsset = null;                                  // don't share the original's baked lighting

        // the gyroscope keeps its arrows (noir materials) and its trail; the toy top's parts and scripts go
        GameObject gyroscope = FindInScene(scene, "Gyroscope").gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(gyroscope)) {
            PrefabUtility.UnpackPrefabInstance(gyroscope, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        TrailRenderer trail = gyroscope.GetComponentInChildren<TrailRenderer>(true);
        trail.transform.SetParent(gyroscope.transform, false);                 // rescued; Build puts it on the new body
        trail.name = "Tip";
        for (int i = gyroscope.transform.childCount - 1; i >= 0; i--) {
            Transform child = gyroscope.transform.GetChild(i);
            if (child.name != "Vectors" && child != trail.transform) Object.DestroyImmediate(child.gameObject);
        }
        // dependents first (RequireComponent): ConeMesh needs MeshFilter and MeshRenderer
        foreach (System.Type type in new[] { typeof(PrecessionCone), typeof(RotorSpin), typeof(ConeMesh), typeof(MeshRenderer),
                                             typeof(MeshFilter), typeof(GyroscopeController) }) {
            Component c = gyroscope.GetComponent(type);
            if (c != null) Object.DestroyImmediate(c);
        }
        gyroscope.AddComponent<ModuleGyroscopeController>();

        // the scene's WebBridge (original WebBridge) pointed to the old controller: off until step 5
        Transform bridge = FindInScene(scene, "WebBridge");
        if (bridge != null) {
            var oldBridge = bridge.GetComponent<WebBridge>();
            if (oldBridge != null) Object.DestroyImmediate(oldBridge);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Lecture scene created: {ScenePath}");
        return true;
    }


    // ---- 2 ----

    private static bool BuildLectureGyroscope(Scene scene) {
        if (FindInScene(scene, "LecturePedestal") != null) {
            Fail("The lecture gyroscope is already built.");
            return false;
        }
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        var controller = gyroscope.GetComponent<ModuleGyroscopeController>();
        Transform tip = gyroscope.Find("Tip");
        if (controller == null || tip == null) {
            Fail("Run Lecture → 1 Create lecture scene first.");
            return false;
        }
        LectureGyroscope p = LectureAsset();
        float r = p.DiskRadius, h = p.DiskHeight, ringY = p.PlateHeight;
        float ringMid = (p.PlateOuterRadius + p.PlateInnerRadius) / 2;

        gyroscope.SetPositionAndRotation(new Vector3(0f, PivotHeight, 0f), Quaternion.identity);
        gyroscope.localScale = Vector3.one;

        // everything that spins: disk, rod, grip, hub, tripod, ring (the whole body turns about the rod)
        Transform rig = Empty("LectureRig", gyroscope, Vector3.zero);
        Transform rotor = Empty("Rotor", rig, Vector3.zero);
        Cylinder("Hub", rotor, 0f, 0.045f, -0.025f, 0.025f, "LectureDarkSteel");
        Cylinder("Grip", rotor, 0f, 0.030f, 0.025f, 0.22f, "LectureRubber");             // the dark sleeve one holds it by
        Cylinder("Rod", rotor, 0f, 0.016f, 0.025f, h, "LectureSteel");
        Cylinder("Disk", rotor, 0f, 2 * r, h - 0.0015f, h, "LectureDiskEdge");            // thin and light
        Transform face = new GameObject("DiskFace", typeof(MeshFilter), typeof(MeshRenderer)).transform;   // the picture
        face.SetParent(rotor, false);
        face.localPosition = new Vector3(0f, h + 0.0002f, 0f);
        face.localScale = new Vector3(r, 1f, r);
        face.GetComponent<MeshFilter>().sharedMesh = DiskFaceMesh();
        Cylinder("HubNut", rotor, 0f, 0.035f, h, h + 0.012f, "LectureSteel");
        Transform marker = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;      // the original's red marker
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.name = "Marker";
        marker.SetParent(rotor, false);
        marker.localPosition = new Vector3(r - 0.012f, h + 0.001f, 0f);
        marker.localScale = new Vector3(0.018f, 0.004f, 0.008f);
        for (int i = 0; i < 3; i++) {
            float a = 2 * Mathf.PI * i / 3;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Rod("Leg" + (i + 1), rotor, dir * 0.02f + Vector3.up * -0.01f, dir * ringMid + Vector3.up * ringY, 0.018f, "LectureCastIron");
        }
        Transform ring = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer)).transform;   // the heavy part
        ring.SetParent(rotor, false);
        ring.localPosition = new Vector3(0f, ringY, 0f);
        ring.GetComponent<MeshFilter>().sharedMesh = RingMesh(p.PlateInnerRadius, p.PlateOuterRadius, 0.025f);

        // not spinning: the spike with the trail at its tip, the add-on weight around it
        Transform body = Empty("Body", rig, Vector3.zero);
        Cylinder("Spike", body, 0f, 0.006f, h + 0.012f, h + SpikeLength, "LectureSteel");
        tip.SetParent(body, false);
        tip.localPosition = new Vector3(0f, h + SpikeLength, 0f);
        tip.localRotation = Quaternion.identity;
        Transform weight = Empty("AddOnWeight", body, new Vector3(0f, h + 0.012f, 0f));
        Transform slug = Cylinder("Slug", weight, 0f, 0.06f, 0f, 0.01f, "LectureSteel");
        var weightScript = new SerializedObject(weight.gameObject.AddComponent<LectureAddOnWeight>());
        weightScript.FindProperty("controller").objectReferenceValue = controller;
        weightScript.FindProperty("slug").objectReferenceValue = slug;
        weightScript.ApplyModifiedPropertiesWithoutUndo();

        var pedestalObject = new GameObject("LecturePedestal");
        BuildPedestal(pedestalObject.transform);

        // rig: θ₀ 5–25°, stop at 32° (a leg, 38° from the rod, touches the pin); arrows from the spike's tip
        var rigSettings = new SerializedObject(rig.gameObject.AddComponent<ApparatusRig>());
        rigSettings.FindProperty("parameters").objectReferenceValue = p;
        rigSettings.FindProperty("rotor").objectReferenceValue = rotor;
        rigSettings.FindProperty("trail").objectReferenceValue = tip.GetComponent<TrailRenderer>();
        rigSettings.FindProperty("stand").objectReferenceValue = pedestalObject;
        rigSettings.FindProperty("minTheta0Deg").floatValue = 5f;
        rigSettings.FindProperty("maxTheta0Deg").floatValue = 25f;
        rigSettings.FindProperty("stopThetaDeg").floatValue = 32f;
        rigSettings.ApplyModifiedPropertiesWithoutUndo();

        // start state for Play mode in the editor (the page always sends a complete preset)
        var settings = new SerializedObject(controller);
        settings.FindProperty("lectureRig").objectReferenceValue = rig.GetComponent<ApparatusRig>();
        settings.FindProperty("apparatus").enumValueIndex = (int)ModuleGyroscopeController.Apparatus.Lecture;
        settings.FindProperty("rpm").floatValue = 300f;
        settings.FindProperty("addOnMassGrams").floatValue = 0f;
        settings.FindProperty("theta0Deg").floatValue = 15f;
        settings.FindProperty("startMode").enumValueIndex = (int)ModuleGyroscopeController.StartMode.Guided;
        settings.FindProperty("trailSamplesPerStep").intValue = 8;
        settings.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = gyroscope.gameObject;
        Debug.Log("Lecture gyroscope built.");
        return true;
    }


    // the fixed part: a heavy lacquered pedestal with chrome edges, a tall column, the thin pin with the tip on top;
    // rebuilt by step 3 as well, so its size can be tuned (the pivot height moves the gyroscope with it)
    private static void BuildPedestal(Transform pedestal) {
        for (int i = pedestal.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pedestal.GetChild(i).gameObject);
        float columnTop = PivotHeight - 0.25f;                                  // the pin stands 25 cm above the column
        Cylinder("Foot", pedestal, 0f, 0.56f, 0f, 0.03f, "LectureLacquer");
        Cylinder("FootEdge", pedestal, 0f, 0.566f, 0.026f, 0.031f, "LectureChrome");
        Cylinder("Step", pedestal, 0f, 0.40f, 0.03f, 0.055f, "LectureLacquer");
        Cylinder("StepEdge", pedestal, 0f, 0.405f, 0.052f, 0.056f, "LectureChrome");
        Cylinder("Column", pedestal, 0f, 0.07f, 0.055f, columnTop, "LectureLacquer");
        Cylinder("ColumnRing", pedestal, 0f, 0.078f, 0.12f, 0.128f, "LectureChrome");
        Cylinder("Collar", pedestal, 0f, 0.085f, columnTop - 0.006f, columnTop + 0.006f, "LectureChrome");
        Cylinder("Pin", pedestal, 0f, 0.014f, columnTop + 0.006f, PivotHeight - 0.004f, "LectureChrome");
        Transform pinTip = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        Object.DestroyImmediate(pinTip.GetComponent<Collider>());
        pinTip.name = "PinTip";
        pinTip.SetParent(pedestal, false);
        pinTip.localPosition = new Vector3(0f, PivotHeight - 0.004f, 0f);
        pinTip.localScale = Vector3.one * 0.014f;
    }


    // ---- 3 ----

    private static bool ApplyLectureLook(Scene scene) {
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        Transform rig = gyroscope != null ? gyroscope.Find("LectureRig") : null;
        if (rig == null) {
            Fail("Run Lecture → 2 Build lecture gyroscope first.");
            return false;
        }
        LectureGyroscope p = LectureAsset();
        gyroscope.position = new Vector3(0f, PivotHeight, 0f);
        BuildPedestal(FindInScene(scene, "LecturePedestal"));
        Vector3 pivot = gyroscope.position;
        float h = p.DiskHeight;

        // apparatus: black-and-white metal, lacquer, rubber; the disk's picture; colour only for the red marker
        var materials = new Dictionary<string, Material> {
            // metals partly diffuse, so they show their shape in the spot and not only in the (dim) reflections
            ["LectureSteel"] = Lit("LectureSteel", new Color32(215, 217, 222, 255), 0.8f, 0.72f),
            // glossy again since the rim light no longer reaches upward faces (see below); the white balls of the
            // first versions were the rim light mirrored in horizontal tops, not the gloss itself
            ["LectureChrome"] = Lit("LectureChrome", new Color32(235, 236, 240, 255), 0.95f, 0.85f),
            ["LectureDarkSteel"] = Lit("LectureDarkSteel", new Color32(125, 128, 135, 255), 0.75f, 0.62f),
            ["LectureCastIron"] = Lit("LectureCastIron", new Color32(105, 107, 113, 255), 0.55f, 0.45f),
            ["LectureLacquer"] = Lit("LectureLacquer", new Color32(34, 34, 38, 255), 0f, 0.75f),        // glossy black lacquer
            ["LectureRubber"] = Lit("LectureRubber", new Color32(50, 50, 52, 255), 0f, 0.25f),
            ["LectureDiskEdge"] = Lit("LectureDiskEdge", new Color32(190, 192, 198, 255), 0.8f, 0.65f),
        };
        Material art = Lit("LectureDiskArt", Color.white, 0.15f, 0.55f);
        art.SetTexture("_BaseMap", DiskArt());
        EditorUtility.SetDirty(art);
        Mesh cylinder = ModuleSceneSetup.SmoothCylinder();
        foreach (Transform root in new[] { rig, FindInScene(scene, "LecturePedestal") }) {
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true)) {
                string part = r.name;
                if (part == "DiskFace") r.sharedMaterial = art;
                else if (part == "Marker") r.sharedMaterial = Original("Marker");
                else if (part == "Ring" || part.StartsWith("Leg")) r.sharedMaterial = materials["LectureCastIron"];
                else if (part == "PinTip") r.sharedMaterial = materials["LectureChrome"];
                else if (r.sharedMaterial == null || !materials.ContainsValue(r.sharedMaterial)) r.sharedMaterial = materials["LectureSteel"];
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                var filter = r.GetComponent<MeshFilter>();
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Cylinder") filter.sharedMesh = cylinder;
            }
        }
        // the parts got their materials by name when built (Cylinder): reapply the current versions
        foreach (MeshRenderer r in rig.GetComponentsInChildren<MeshRenderer>(true)) {
            if (r.sharedMaterial != null && materials.TryGetValue(r.sharedMaterial.name, out Material m)) r.sharedMaterial = m;
        }

        // trail: the original's grey, wider for the bigger scene, long enough for one slow precession
        TrailRenderer trail = rig.GetComponentInChildren<TrailRenderer>(true);
        trail.widthMultiplier = 0.002f * SceneScale;
        trail.minVertexDistance = 0.004f;
        trail.time = 12f;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 2;
        trail.emitting = true;

        // arrows (the original's emissive materials): thicker for the bigger scene; drawn from the spike's tip
        foreach (VectorArrow3D arrow in gyroscope.GetComponentsInChildren<VectorArrow3D>(true)) {
            var a = new SerializedObject(arrow);
            a.FindProperty("shaftDiameter").floatValue = 0.002f * SceneScale;
            a.FindProperty("headDiameter").floatValue = 0.006f * SceneScale;
            a.FindProperty("headLength").floatValue = 0.012f * SceneScale;
            a.ApplyModifiedPropertiesWithoutUndo();
        }
        // L ≈ 1.3 kg·m²/s at 300 rpm → 0.36 m; ω ≈ 31 rad/s → 0.25 m; D ≈ 0.23 N·m (hanging, 15°) → 0.18 m
        var scales = new Vector3(0.28f, 0.008f, 0.8f);
        var vectors = new SerializedObject(gyroscope.GetComponent<VectorDisplay>());
        vectors.FindProperty("scaleL").floatValue = scales.x;
        vectors.FindProperty("scaleOmega").floatValue = scales.y;
        vectors.FindProperty("scaleD").floatValue = scales.z;
        vectors.FindProperty("anchorHeight").floatValue = h + SpikeLength;
        vectors.ApplyModifiedPropertiesWithoutUndo();
        var rigSettings = new SerializedObject(rig.GetComponent<ApparatusRig>());
        rigSettings.FindProperty("vectorScales").vector3Value = scales;
        rigSettings.FindProperty("coneLength").floatValue = h + SpikeLength;              // the precession cone's rim at the tip
        rigSettings.ApplyModifiedPropertiesWithoutUndo();

        // labels: the original's (black halo), at the same size on screen with the camera 4 × farther away
        Transform labels = FindInScene(scene, "VectorLabels");
        if (labels != null) {
            foreach (VectorLabel label in labels.GetComponentsInChildren<VectorLabel>(true)) {
                label.GetComponent<TextMeshPro>().fontSize = 0.23f * SceneScale;
                label.GetComponent<TextMeshPro>().UpdateMeshPadding();
                var l = new SerializedObject(label);
                bool beside = label.name == "LabelOmega";                       // ω⃗ beside its tip, as in the original
                l.FindProperty("offset").floatValue = beside ? 0f : 0.016f * SceneScale;
                l.FindProperty("sideOffset").floatValue = beside ? 0.02f * SceneScale : 0f;
                l.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // the spot pool: the original's key light, moved and scaled with the scene (same angle, 1/r² intensity)
        Light key = FindInScene(scene, "KeyLight").GetComponent<Light>();
        Vector3 aim = pivot + Vector3.up * 0.05f;                              // the whole apparatus, pedestal included
        key.transform.position = pivot + new Vector3(0.3f, 0.45f, -0.1f) * SceneScale * 1.1f;
        key.transform.LookAt(aim);
        key.range = 2f * SceneScale * 1.2f;
        key.intensity = 2f * SceneScale * SceneScale * 1.2f;
        key.spotAngle = 50f;
        key.innerSpotAngle = 20f;

        // rim light (a directional light on the camera, the original's): it came from behind the apparatus 20° from
        // above, so at low camera angles every horizontal top (foot, collar, floor) mirrored it straight into the
        // bloom. From just below the horizon it still rims the rod, legs and column, but no upward face can catch it.
        Transform rim = FindInScene(scene, "Main Camera/RimLight");
        if (rim != null) {
            rim.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.06f, -1f));   // towards the camera, slightly up
            rim.GetComponent<Light>().intensity = 2.5f;
        }

        // the noir stays dark, but not pitch black: a little more ambient than the original (0.04) and dim studio
        // reflections, so the structure keeps its edges outside the spot's centre
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.095f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionCubemapPath);
        RenderSettings.reflectionIntensity = 0.55f;

        // floor large enough for the bigger pool; snow scaled as a whole (it simulates in local space: emitter, fall,
        // flake size and noise scale together; the fall time and the number of flakes stay the original's)
        Transform floor = FindInScene(scene, "Floor");
        floor.localScale = new Vector3(2f * SceneScale, 0.001f, 2f * SceneScale);
        // own floor material, rougher than the original's (0.3): seen at a grazing angle against the spot, the Fresnel
        // highlight of the original floor bloomed into a white ball around the pedestal's foot
        floor.GetComponent<MeshRenderer>().sharedMaterial = Lit("LectureFloor", new Color(0.2f, 0.2f, 0.2f), 0f, 0.08f);
        float snowScale = (pivot.y + h + 1.4f) / 0.8f;                         // the original snows from 0.8 m
        foreach (string name in new[] { "Snow", "Snow (fast)" }) {
            Transform snow = FindInScene(scene, name);
            if (snow == null) continue;
            snow.position = new Vector3(0f, 0.8f * snowScale, 0f);
            snow.localScale = (name == "Snow" ? new Vector3(0.75f, 1f, 0.75f) : Vector3.one) * snowScale;
        }

        // camera: the original's orbit around the pivot, looking at the middle of the apparatus
        var orbit = new SerializedObject(FindInScene(scene, "Main Camera").GetComponent<OrbitCamera>());
        orbit.FindProperty("targetOffset").vector3Value = new Vector3(0f, 0.02f, 0f);   // apparatus spans 0 … 1.6 m
        orbit.FindProperty("pitch").floatValue = 24f;
        orbit.FindProperty("yaw").floatValue = 0f;
        orbit.FindProperty("distance").floatValue = 2.9f;
        orbit.FindProperty("minDistance").floatValue = 0.9f;
        orbit.FindProperty("maxDistance").floatValue = 5f;
        orbit.FindProperty("minPitch").floatValue = 4f;                        // not at floor level: grazing views only show glare
        orbit.ApplyModifiedPropertiesWithoutUndo();
        Camera cam = FindInScene(scene, "Main Camera").GetComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;

        Debug.Log("Lecture look applied.");
        return true;
    }


    // ---- 4 ----

    private static bool AddLectureHelpers(Scene scene) {
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        Transform rig = gyroscope != null ? gyroscope.Find("LectureRig") : null;
        Transform labels = FindInScene(scene, "VectorLabels");
        if (rig == null || labels == null) {
            Fail("Run Lecture → 2 Build lecture gyroscope first (and keep the original's VectorLabels).");
            return false;
        }
        Transform old = FindInScene(scene, "Helpers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldLabel = labels.Find("LabelPrecession");
        if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);

        var controller = gyroscope.GetComponent<ModuleGyroscopeController>();
        float tipLength = LectureAsset().DiskHeight + SpikeLength;
        var helpersObject = new GameObject("Helpers");
        SceneManager.MoveGameObjectToScene(helpersObject, scene);
        Transform helpers = helpersObject.transform;

        // precession: the cone the rod sweeps around the vertical, rim at the spike's tip, ω_P label beside it
        Transform precession = Empty("Precession", helpers, Vector3.zero);
        HelperCone precessionCone = LightCone(precession, "Cone", "Precession", LightGrey, 0.1f);
        TextMeshPro label = ModuleSceneSetup.Label(labels, "LabelPrecession", "<i>ω</i><sub><i>P</i></sub>", LightGrey,
                                                   AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VectorLabel.mat"));
        label.fontSize = 0.23f * SceneScale;
        label.fontStyle = FontStyles.Bold;
        label.UpdateMeshPadding();
        var display = new SerializedObject(precession.gameObject.AddComponent<PrecessionDisplay>());
        display.FindProperty("controller").objectReferenceValue = controller;
        display.FindProperty("cone").objectReferenceValue = precessionCone;
        display.FindProperty("label").objectReferenceValue = label;
        display.FindProperty("smoothingSeconds").floatValue = 3f;              // nutation ≈ 1 s, precession ≈ 10 s
        display.FindProperty("labelOffset").floatValue = 0.12f;
        display.ApplyModifiedPropertiesWithoutUndo();

        // the three cones of the balanced top (colours: what they contain or turn around; provisional, Carlo decides):
        // Nutationskegel grey (the rod), Rastpolkegel yellow (around L⃗), Gangpolkegel white (ω⃗, around the rod)
        Transform nutation = Empty("Nutation", helpers, Vector3.zero);
        HelperCone nutationCone = LightCone(nutation, "NutationCone", "Nutation", Grey, 0.14f);
        HelperCone spaceCone = LightCone(nutation, "SpaceCone", "Space", Yellow, 0.14f);
        HelperCone bodyCone = LightCone(nutation, "BodyCone", "Body", White, 0.1f);
        var cones = new SerializedObject(nutation.gameObject.AddComponent<NutationCones>());
        cones.FindProperty("controller").objectReferenceValue = controller;
        cones.FindProperty("nutationCone").objectReferenceValue = nutationCone;
        cones.FindProperty("spaceCone").objectReferenceValue = spaceCone;
        cones.FindProperty("bodyCone").objectReferenceValue = bodyCone;
        cones.FindProperty("nutationLength").floatValue = tipLength;           // the spike's tip runs along its rim
        cones.FindProperty("spaceLength").floatValue = tipLength + 0.2f;
        cones.FindProperty("bodyLength").floatValue = tipLength + 0.4f;
        cones.ApplyModifiedPropertiesWithoutUndo();

        var board = new SerializedObject(helpers.gameObject.AddComponent<ModuleHelpers>());
        board.FindProperty("controller").objectReferenceValue = controller;
        board.FindProperty("vectors").objectReferenceValue = gyroscope.GetComponent<VectorDisplay>();
        board.FindProperty("labels").objectReferenceValue = labels.gameObject;
        board.FindProperty("precessionCone").objectReferenceValue = precessionCone.gameObject;
        board.FindProperty("nutationCone").objectReferenceValue = nutationCone.gameObject;
        board.FindProperty("spaceCone").objectReferenceValue = spaceCone.gameObject;
        board.FindProperty("bodyCone").objectReferenceValue = bodyCone.gameObject;
        board.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = helpersObject;
        Debug.Log("Lecture helpers added.");
        return true;
    }


    // ---- 5 ----

    private static bool UseLectureWebBridge(Scene scene) {
        Transform helpers = FindInScene(scene, "Helpers");
        if (helpers == null) {
            Fail("Run Lecture → 4 Add lecture helpers first.");
            return false;
        }
        Transform bridgeObject = FindInScene(scene, "WebBridge");
        if (bridgeObject == null) {
            bridgeObject = new GameObject("WebBridge").transform;
            SceneManager.MoveGameObjectToScene(bridgeObject.gameObject, scene);
        }
        GameObject go = bridgeObject.gameObject;
        go.SetActive(true);                                                     // SendMessage only finds active objects
        var oldBridge = go.GetComponent<WebBridge>();
        if (oldBridge != null) Object.DestroyImmediate(oldBridge);
        var simTime = go.GetComponent<SimulationTime>();
        if (simTime == null) simTime = go.AddComponent<SimulationTime>();
        var bridge = go.GetComponent<ModuleWebBridge>();
        if (bridge == null) bridge = go.AddComponent<ModuleWebBridge>();

        var so = new SerializedObject(bridge);
        so.FindProperty("controller").objectReferenceValue = FindInScene(scene, "Gyroscope").GetComponent<ModuleGyroscopeController>();
        so.FindProperty("helpers").objectReferenceValue = helpers.GetComponent<ModuleHelpers>();
        so.FindProperty("simTime").objectReferenceValue = simTime;
        so.FindProperty("assemblyAvailable").boolValue = false;
        so.FindProperty("startAssembly").boolValue = false;
        so.FindProperty("testPreset").stringValue =
            "{\"apparatus\":\"lecture\",\"rpm\":300,\"addOnMassGrams\":120,\"theta0Deg\":10,\"pushThetaDot\":0.6,\"show\":{\"D\":false,\"precessionCone\":false}}";
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        Debug.Log("Lecture WebBridge uses ModuleWebBridge (no assembly game).");
        return true;
    }


    // ---- parts ----

    // smooth cylinder along local y from y0 to y1 (Unity's cylinder spans −1 … 1, so scale.y is half the length)
    private static Transform Cylinder(string name, Transform parent, float x, float diameter, float y0, float y1, string material) {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(go.GetComponent<Collider>());                   // no physics engine involved
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, (y0 + y1) / 2, 0f);
        go.transform.localScale = new Vector3(diameter, (y1 - y0) / 2, diameter);
        go.GetComponent<MeshFilter>().sharedMesh = ModuleSceneSetup.SmoothCylinder();
        go.GetComponent<MeshRenderer>().sharedMaterial = LitPlaceholder(material);
        return go.transform;
    }

    // cylinder between two local points
    private static void Rod(string name, Transform parent, Vector3 from, Vector3 to, float diameter, string material) {
        Transform t = Cylinder(name, parent, 0f, diameter, 0f, 1f, material);
        Vector3 d = to - from;
        t.localPosition = (from + to) / 2;
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.localScale = new Vector3(diameter, d.magnitude / 2, diameter);
    }

    private static Transform Empty(string name, Transform parent, Vector3 localPosition) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }


    // light cone: an additive surface that brightens towards the rim (HelperCone's UV v: 0 apex, 1 rim) and glows
    // through the bloom, plus a bright rim line. On the black stage it reads as a cone of light.
    private static HelperCone LightCone(Transform parent, string name, string materialName, Color color, float surfaceIntensity) {
        var coneObject = new GameObject(name);
        coneObject.transform.SetParent(parent, false);
        HelperCone cone = coneObject.AddComponent<HelperCone>();                // also adds MeshFilter and MeshRenderer
        var surface = coneObject.GetComponent<MeshRenderer>();
        surface.sharedMaterial = ConeSurfaceMaterial("LectureCone" + materialName, color, surfaceIntensity);
        surface.shadowCastingMode = ShadowCastingMode.Off;
        surface.receiveShadows = false;
        Transform rimObject = Empty("Rim", coneObject.transform, Vector3.zero);
        var rim = rimObject.gameObject.AddComponent<LineRenderer>();
        rim.positionCount = 0;                                                  // HelperCone fills it at runtime
        rim.sharedMaterial = GlowMaterial("LectureConeRim" + materialName, color, 0.8f);    // below the bloom threshold
        rim.widthMultiplier = 0.004f;
        rim.alignment = LineAlignment.View;
        rim.shadowCastingMode = ShadowCastingMode.Off;
        rim.receiveShadows = false;
        var so = new SerializedObject(cone);
        so.FindProperty("rim").objectReferenceValue = rim;
        so.ApplyModifiedPropertiesWithoutUndo();
        return cone;
    }


    // ---- materials and textures ----

    private static Material LoadOrCreate(string name, string shader) {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find(shader);
        return mat;
    }

    // URP/Lit with colour, metallic and smoothness (no emission: the apparatus only shines where the spot hits it)
    private static Material Lit(string name, Color color, float metallic, float smoothness) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // while building, before step 3 sets the real values: the asset by that name, created plain if missing
    private static Material LitPlaceholder(string name) {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
        return mat != null ? mat : Lit(name, Color.grey, 0.5f, 0.5f);
    }

    // URP/Unlit opaque in HDR (× intensity): the bloom makes it glow, like the original's emissive arrows
    private static Material GlowMaterial(string name, Color color, float intensity) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
        Color hdr = color.linear * intensity;
        hdr.a = 1f;
        mat.SetColor("_BaseColor", hdr.gamma);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // URP/Unlit, transparent, additive (adds light, never darkens), both faces, no depth write; the alpha ramp texture
    // fades the surface in towards the rim. Alpha of the colour = brightness of the surface.
    private static Material ConeSurfaceMaterial(string name, Color color, float intensity) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);                                             // additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetTexture("_BaseMap", GlowRamp());
        Color c = color;
        c.a = intensity;
        mat.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // alpha ramp along v (apex 0 → rim 1): nothing near the apex, a soft rise towards the rim; rewritten on every run
    private static Texture2D GlowRamp() {
        string path = $"{TextureFolder}/ConeGlow.asset";
        const int n = 128;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = tex == null;
        if (isNew) tex = new Texture2D(4, n, TextureFormat.RGBA32, false) { name = "ConeGlow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) {
            float v = (float)y / (n - 1);
            float a = 0.05f + 0.75f * v * v * v + 0.2f * Mathf.Pow(v, 16f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        tex.Apply();
        if (isNew) AssetDatabase.CreateAsset(tex, path);
        else EditorUtility.SetDirty(tex);
        return tex;
    }

    private static Material Original(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");


    // Carlo's picture if it is there, else the generated placeholder: a noir night through cell bars (moon, bars,
    // a red spot), white on black, in the inscribed circle of a square texture
    private static Texture2D DiskArt() {
        if (File.Exists(DiskArtPath)) {
            ConfigureArtImport(DiskArtPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DiskArtPath);
        }
        if (!File.Exists(DiskArtPlaceholderPath)) {
            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var black = new Color(0.02f, 0.02f, 0.025f);
            for (int y = 0; y < n; y++) {
                for (int x = 0; x < n; x++) {
                    float u = 2f * x / (n - 1) - 1f, v = 2f * y / (n - 1) - 1f;         // −1 … 1
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = black;
                    // moon: a disk with a bite taken out, behind the bars
                    float moon = Mathf.Sqrt((u + 0.2f) * (u + 0.2f) + (v - 0.25f) * (v - 0.25f));
                    float bite = Mathf.Sqrt((u + 0.05f) * (u + 0.05f) + (v - 0.33f) * (v - 0.33f));
                    if (moon < 0.3f && bite > 0.27f) c = new Color(0.92f, 0.92f, 0.9f);
                    // a red spot, the noir's one colour
                    if (Mathf.Sqrt((u - 0.38f) * (u - 0.38f) + (v + 0.35f) * (v + 0.35f)) < 0.07f) c = new Color(0.85f, 0.02f, 0.02f);
                    // cell bars and a cross bar
                    float bar = Mathf.Abs(Mathf.Repeat(u + 0.1f, 0.25f) - 0.125f);
                    if (bar < 0.018f || Mathf.Abs(v + 0.05f) < 0.02f) c = new Color(0.8f, 0.8f, 0.82f);
                    if (bar < 0.006f) c = new Color(1f, 1f, 1f);                     // highlight on each bar
                    // rim: a white ring and a thin black edge
                    if (r > 0.9f && r < 0.94f) c = Color.white;
                    if (r >= 0.94f) c = black;
                    tex.SetPixel(x, y, c);
                }
            }
            File.WriteAllBytes(DiskArtPlaceholderPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DiskArtPlaceholderPath);
        }
        ConfigureArtImport(DiskArtPlaceholderPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(DiskArtPlaceholderPath);
    }

    private static void ConfigureArtImport(string path) {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize == 1024 && importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = 4;                                                // the disk is often seen at a grazing angle
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }


    // ---- meshes ----

    // flat disk of radius 1 facing up, UVs of the inscribed circle of the texture (scaled by the transform)
    private static Mesh DiskFaceMesh() {
        string path = $"{MeshFolder}/DiskFace.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        const int segments = 128;
        var vertices = new List<Vector3> { Vector3.zero };
        var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++) {
            float a = 2 * Mathf.PI * i / segments;
            vertices.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
        }
        for (int i = 0; i < segments; i++) triangles.AddRange(new[] { 0, i + 2, i + 1 });   // clockwise seen from above
        mesh = new Mesh { name = "DiskFace" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // annulus with thickness (the ring at the tripod's feet), centred on its own origin; rebuilt on every call
    private static Mesh RingMesh(float inner, float outer, float thickness) {
        string path = $"{MeshFolder}/Ring.asset";
        const int segments = 96;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        float y0 = -thickness / 2, y1 = thickness / 2;

        // four faces: outer side, inner side, top, bottom; each with its own vertices (sharp edges)
        void Strip(float ra, float ya, float rb, float yb, System.Func<Vector3, Vector3> normal, bool flip) {
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++) {
                float a = 2 * Mathf.PI * i / segments;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(radial * ra + Vector3.up * ya);
                vertices.Add(radial * rb + Vector3.up * yb);
                normals.Add(normal(radial));
                normals.Add(normal(radial));
            }
            for (int i = 0; i < segments; i++) {
                int b = start + 2 * i;
                if (flip) triangles.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                else triangles.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
        }
        Strip(outer, y0, outer, y1, n => n, false);                             // outer side, normal outwards
        Strip(inner, y0, inner, y1, n => -n, true);                             // inner side, normal inwards
        Strip(inner, y1, outer, y1, n => Vector3.up, false);                    // top
        Strip(inner, y0, outer, y0, n => Vector3.down, true);                   // bottom

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "Ring" };
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }


    // ---- assets and lookup ----

    private static void EnsureFolders() {
        if (!AssetDatabase.IsValidFolder(LectureFolder)) AssetDatabase.CreateFolder("Assets", "Lecture");
        foreach (string sub in new[] { "Materials", "Textures", "Meshes" }) {
            if (!AssetDatabase.IsValidFolder($"{LectureFolder}/{sub}")) AssetDatabase.CreateFolder(LectureFolder, sub);
        }
    }

    private static LectureGyroscope LectureAsset() {
        EnsureFolders();
        var asset = AssetDatabase.LoadAssetAtPath<LectureGyroscope>(AssetPath);
        if (asset == null) {
            asset = ScriptableObject.CreateInstance<LectureGyroscope>();        // with the default values from the class
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        return asset;
    }

    private static Transform FindInScene(Scene scene, string path) => ModuleSceneSetup.FindInScene(scene, path);
}
