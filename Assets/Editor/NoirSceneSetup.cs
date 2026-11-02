using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


// The original (noir) scene, changed by an editor script like the module scene (ModuleSceneSetup).
//   Noir → Add vector labels   (rerunnable) L⃗, ω⃗, D⃗ at the arrow tips; overwrites manual changes to the labels
//   Noir → Apply trail look    (rerunnable) soft grey trail with rounded corners; overwrites manual changes to these
public static class NoirSceneSetup {
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string LabelMaterialPath = "Assets/Materials/VectorLabel.mat";
    private const string LabelRootName = "VectorLabels";

    // the arrows' colours, without their HDR intensity (Assets/Materials/Arrow*.mat)
    private static readonly Color32 Yellow = new Color32(255, 235, 4, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Red = new Color32(255, 0, 0, 255);

    // same size on screen as in the module: the camera is at 0.5 m instead of 1.1 m
    private const float FontSize = 0.23f;
    private const float TipOffset = 0.016f;                                     // m beyond the tip
    private const float SideOffset = 0.02f;                                     // m beside the tip

    // trail: pure white on black shows every stair step of the thin line; grey like the module's, but light for the dark stage
    private static readonly Color32 TrailColor = new Color32(190, 195, 202, 255);
    private const float TrailWidth = 0.002f;                                    // m, times the width curve 0.5 → 0.2 of the scene


    [MenuItem("Noir/Add vector labels")]
    private static void AddVectorLabels() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) {
            EditorUtility.DisplayDialog("Noir scene", $"Open {ScenePath} first.", "OK");
            return;
        }
        Transform old = ModuleSceneSetup.FindInScene(scene, LabelRootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // black halo: on the black stage a white one would swallow the white ω⃗; it only shows over the bright metal
        Material material = ModuleSceneSetup.LabelOverlayMaterial(LabelMaterialPath, new Color(0f, 0f, 0f, 0.9f));
        Transform arrows = ModuleSceneSetup.FindInScene(scene, "Gyroscope").Find("Vectors");
        Transform root = new GameObject(LabelRootName).transform;

        // L⃗ and ω⃗ lie almost on one line with a fixed length ratio, so their tips close up at low rpm: L⃗ beyond its tip, ω⃗ beside it
        foreach (var (arrow, symbol, arrowHeightEm, color, tip, side) in new[] {
                     ("Arrow3DL", "L", 0.78f, (Color)Yellow, TipOffset, 0f),
                     ("Arrow3DOmega", "ω", 0.6f, (Color)White, 0f, SideOffset),
                     ("Arrow3DD", "D", 0.78f, (Color)Red, TipOffset, 0f) }) {
            TextMeshPro label = ModuleSceneSetup.Label(root, "Label" + arrow.Substring(7), ModuleSceneSetup.VectorSymbol(symbol, arrowHeightEm), color, material);
            label.fontSize = FontSize;
            label.fontStyle = FontStyles.Bold;
            label.UpdateMeshPadding();                                          // the halo needs room around the glyphs

            var settings = new SerializedObject(label.gameObject.AddComponent<VectorLabel>());
            settings.FindProperty("arrow").objectReferenceValue = arrows.Find(arrow).GetComponent<VectorArrow3D>();
            settings.FindProperty("offset").floatValue = tip;
            settings.FindProperty("sideOffset").floatValue = side;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Noir scene: vector labels added.");
    }


    [MenuItem("Noir/Apply trail look")]
    private static void ApplyTrailLook() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) {
            EditorUtility.DisplayDialog("Noir scene", $"Open {ScenePath} first.", "OK");
            return;
        }
        TrailRenderer trail = ModuleSceneSetup.FindInScene(scene, "Gyroscope").GetComponentInChildren<TrailRenderer>(true);
        trail.widthMultiplier = TrailWidth;
        trail.numCornerVertices = 3;                                            // the ribbon would fold flat at the nutation's cusps
        trail.numCapVertices = 2;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(TrailColor, 0f), new GradientColorKey(TrailColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        PrefabUtility.RecordPrefabInstancePropertyModifications(trail);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Noir scene: trail look applied.");
    }
}
