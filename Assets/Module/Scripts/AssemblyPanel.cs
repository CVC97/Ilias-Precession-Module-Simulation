using UnityEngine;


// On-screen panel for the assembly mode (AssemblyMode), in the editor and in the standalone web build, so the game can
// be played without the learning module. The buttons call the bridge's assembly commands, the same path a page will
// use. A click on the panel never reaches the scene: AssemblyMode and the orbit camera ask Covers.
public class AssemblyPanel : MonoBehaviour {
    private static Rect area;                                                   // screen pixels, top left

    // position as the Input System gives it (origin bottom left)
    public static bool Covers(Vector2 screenPosition) =>
        area.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

    private const float Width = 320f;
    private const int WindowId = 0x4A55;                                        // any number unique among the scene's IMGUI windows
    private AssemblyMode assembly;
    private ModuleWebBridge bridge;
    private Rect window = new Rect(10f, 10f, Width, 0f);
    private GUIStyle wrapped;


    void Awake() {
        assembly = GetComponent<AssemblyMode>();
        bridge = GetComponent<ModuleWebBridge>();
    }


    void OnDisable() {
        area = Rect.zero;
    }


    void OnGUI() {
        float scale = Mathf.Max(1f, Screen.height / 1000f);                     // readable on large and HiDPI screens
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        window = GUILayout.Window(WindowId, window, Draw, "Kreisel aufbauen", GUILayout.Width(Width));
        area = new Rect(window.x * scale, window.y * scale, window.width * scale, window.height * scale);
    }


    private void Draw(int windowId) {
        wrapped ??= new GUIStyle(GUI.skin.label) { wordWrap = true };          // GUI.skin only exists inside OnGUI

        if (!assembly.Active) {
            if (GUILayout.Button("Aufbau-Spiel starten")) bridge.ApplyPreset("{\"mode\":\"assembly\"}");
            GUI.DragWindow();
            return;
        }

        GUILayout.Label($"Phase: {assembly.PhaseLabel}");
        GUILayout.Label(assembly.Hint, wrapped);

        GUILayout.Space(6f);
        GUILayout.Label("Teile (Klick = auswählen):");
        for (int i = 0; i < assembly.PartCount; i++) {
            string id = assembly.PartId(i), label = assembly.PartName(i);
            bool placed = assembly.PartPlaced(i);
            if (placed) label += "  (eingebaut)";
            else if (assembly.SelectedId == id) label = "> " + label + " <";
            GUI.enabled = !placed;
            if (GUILayout.Button(label)) bridge.AssemblySelect(id);
        }
        GUI.enabled = true;

        GUILayout.Label("Markierungen (setzt das ausgewählte Teil):");
        GUILayout.BeginHorizontal();
        for (int n = 1; n <= AssemblyMode.SlotCount; n++) {
            GUI.enabled = assembly.SlotAvailable(n);
            if (GUILayout.Button(n.ToString())) bridge.AssemblyPlace(n);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUI.enabled = assembly.CanBalance;
        float position = assembly.CounterweightPosition;
        GUILayout.Label($"Ausgleichsgewicht: {position * 100f:0.0} cm vom Lager");
        float moved = GUILayout.HorizontalSlider(position, AssemblyMode.MinBalance, AssemblyMode.MaxBalance);
        if (moved != position) bridge.AssemblyBalance(moved);
        GUI.enabled = true;

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUI.enabled = assembly.CanSpin;
        if (GUILayout.Button("Rad andrehen")) bridge.AssemblyAction("spin");
        GUI.enabled = assembly.CanRelease;
        if (GUILayout.Button("Loslassen")) bridge.AssemblyAction("release");
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Ansicht:", GUILayout.Width(55f));
        if (GUILayout.Button("Schräg")) bridge.SetCameraView("oblique");
        if (GUILayout.Button("Seite")) bridge.SetCameraView("side");
        if (GUILayout.Button("Oben")) bridge.SetCameraView("top");
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Neu beginnen")) bridge.AssemblyReset();
        if (GUILayout.Button("Normale Simulation")) bridge.ApplyPreset("{}");   // ends the game like a page's preset
        GUILayout.EndHorizontal();

        GUI.DragWindow();                                                       // the panel can be moved by its title bar
    }
}
