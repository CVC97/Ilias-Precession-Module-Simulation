using System;
using System.Runtime.InteropServices;
using UnityEngine;


// A complete page state for the simulation. Fields missing in the page's JSON keep the defaults below, so a preset
// never depends on what ran before. The field names are the JSON keys.
[Serializable]
public class ModulePreset {
    public string apparatus = "lab";                                            // "lab" | "top"
    public float rpm = 400f;                                                    // clamped per apparatus (lab 800, top 3000)
    public float addOnMassGrams = 30f;                                          // lab only
    public float theta0Deg = 90f;                                               // θ₀ of L⃗, clamped per apparatus
    public string startMode = "guided";                                         // "guided" | "released"
    public bool friction = false;
    public float pushThetaDot = 0.5f;                                           // rad/s, strength of the Stoß
    public float pushPhiDot = 0f;                                               // rad/s
    public float timeScale = 1f;
    public bool paused = false;
    public HelperFlags show = new HelperFlags();
}


// Messages to the page. Same structure as the page expects: {source, type, ...}.
[Serializable]
public class ModuleReadyMessage {
    public string source = "precession-sim";
    public string type = "ready";
}


// ~10 Hz. Angles of the axle in the module's convention (θ of L⃗); rates as magnitudes except phiDot.
// No moments of inertia, not even as a ratio: the students find I₃/I₁ in the Auswertung.
[Serializable]
public class ModuleStateMessage {
    public string source = "precession-sim";
    public string type = "state";
    public int run;                                                             // +1 on every restart
    public float time;                                                          // s of simulated time since the restart
    public string apparatus;
    public float thetaDeg;
    public float phiDeg;                                                        // precession angle, continuous (not wrapped)
    public float phiDot;                                                        // rad/s, signed
    public float omegaR;                                                        // rad/s
    public float omegaP;                                                        // rad/s
    public float addOnMassGrams;
    public bool paused;
    public bool spinLimited;                                                    // drawn spin slower than the real one
}


// The module scene's end of the page ↔ simulation contract. The page's commands arrive through the template script
// (origin check, allowlist) as unityInstance.SendMessage("WebBridge", method, value); the value is a number or a
// string, so switches come as 0/1 and structured data as a JSON string. Back to the page goes JSON through
// ModuleBridge.jslib → window.parent.postMessage.
// The original's WebBridge (Assets/Scripts/WebBridge.cs) stays for the original scene.
public class ModuleWebBridge : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private ModuleHelpers helpers;
    [SerializeField] private SimulationTime simTime;
    [SerializeField] private float stateInterval = 0.1f;                        // s of real time

    [Header("Editor test")]
    [SerializeField, TextArea(3, 8)] private string testPreset =
        "{\"apparatus\":\"lab\",\"rpm\":200,\"addOnMassGrams\":0,\"pushThetaDot\":2,\"show\":{\"D\":false}}";
    [SerializeField, TextArea(3, 8)] private string lastMessage;                // what was sent last (written in Play mode)

    private readonly ModuleStateMessage state = new ModuleStateMessage();
    private float sinceState;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ModulePostToParent(string json);                 // Assets/Module/Plugins/ModuleBridge.jslib
#endif


    // ---- called from JavaScript via SendMessage ----

    public void ApplyPreset(string json) {
        var preset = new ModulePreset();
        if (!TryOverwrite(json, preset)) return;

        if (TryParseApparatus(preset.apparatus, out var apparatus)) controller.SetApparatus(apparatus);
        if (TryParseStartMode(preset.startMode, out var startMode)) controller.SetStartMode(startMode);
        controller.SetRpm(preset.rpm);
        controller.SetAddOnMassGrams(preset.addOnMassGrams);
        controller.SetTheta0Deg(preset.theta0Deg);
        controller.SetFriction(preset.friction);
        controller.SetPush(preset.pushThetaDot, preset.pushPhiDot);
        controller.Restart();                                                   // also if no value changed: a preset is a fresh start

        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset.show), helpers.Show);    // copy into the switchboard's object
        simTime.SetTimeScale(preset.timeScale);
        simTime.SetPaused(preset.paused);
    }

    // partial: {"trail":false} changes only the trail
    public void SetHelpers(string json) {
        TryOverwrite(json, helpers.Show);
    }

    public void SetApparatus(string value) {
        if (TryParseApparatus(value, out var apparatus)) controller.SetApparatus(apparatus);
    }

    public void SetStartMode(string value) {
        if (TryParseStartMode(value, out var startMode)) controller.SetStartMode(startMode);
    }

    public void SetRpm(float value) => controller.SetRpm(value);
    public void SetAddOnMassGrams(float value) => controller.SetAddOnMassGrams(value);
    public void SetTheta0Deg(float value) => controller.SetTheta0Deg(value);
    public void SetFriction(float on) => controller.SetFriction(on > 0.5f);
    public void SetTimeScale(float value) => simTime.SetTimeScale(value);
    public void SetPaused(float on) => simTime.SetPaused(on > 0.5f);
    public void Restart() => controller.Restart();
    public void Push() => controller.Push();


    [ContextMenu("Apply test preset")]
    private void ApplyTestPreset() => ApplyPreset(testPreset);


    void Start() {
        Post(JsonUtility.ToJson(new ModuleReadyMessage()));
    }


    // after the controller's Update, so the state is this frame's; real time, so the page also hears while paused
    void LateUpdate() {
        sinceState += Time.unscaledDeltaTime;
        if (sinceState < stateInterval || controller.Model == null) return;
        sinceState = 0f;

        state.run = controller.Run;
        state.time = (float)controller.SimTime;
        state.apparatus = controller.SelectedApparatus == ModuleGyroscopeController.Apparatus.Lab ? "lab" : "top";
        state.thetaDeg = (float)(controller.Theta * Mathf.Rad2Deg);
        state.phiDeg = (float)(controller.Model.Phi * Mathf.Rad2Deg);
        state.phiDot = (float)controller.Model.PhiDot;
        state.omegaR = (float)controller.OmegaR;
        state.omegaP = (float)controller.OmegaP;
        state.addOnMassGrams = controller.AddOnMassGrams;
        state.paused = simTime.Paused;
        state.spinLimited = controller.SpinDisplayLimited;
        Post(JsonUtility.ToJson(state));
    }


    private void Post(string json) {
        lastMessage = json;
#if UNITY_WEBGL && !UNITY_EDITOR
        ModulePostToParent(json);
#endif
    }


    // JsonUtility throws on malformed JSON; a bad message from the page must not stop the simulation
    private static bool TryOverwrite(string json, object target) {
        try {
            JsonUtility.FromJsonOverwrite(json, target);
            return true;
        } catch (ArgumentException e) {
            Debug.LogWarning($"ModuleWebBridge: ignored malformed JSON ({e.Message}): {json}");
            return false;
        }
    }

    private static bool TryParseApparatus(string value, out ModuleGyroscopeController.Apparatus apparatus) {
        switch (value) {
            case "lab": apparatus = ModuleGyroscopeController.Apparatus.Lab; return true;
            case "top": apparatus = ModuleGyroscopeController.Apparatus.SimpleTop; return true;
        }
        Debug.LogWarning($"ModuleWebBridge: unknown apparatus \"{value}\" (lab | top)");
        apparatus = default;
        return false;
    }

    private static bool TryParseStartMode(string value, out ModuleGyroscopeController.StartMode startMode) {
        switch (value) {
            case "guided": startMode = ModuleGyroscopeController.StartMode.Guided; return true;
            case "released": startMode = ModuleGyroscopeController.StartMode.Released; return true;
        }
        Debug.LogWarning($"ModuleWebBridge: unknown start mode \"{value}\" (guided | released)");
        startMode = default;
        return false;
    }
}
