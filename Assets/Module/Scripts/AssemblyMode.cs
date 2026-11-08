using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;


// Assembly mode: the students build the lab gyroscope themselves before they run it. The parts lie on a tray in front
// of the stand; a part is dragged onto a numbered marker, or clicked (it lifts) and then the marker clicked. The
// markers appear in the order the apparatus allows and only say where something goes, not what (no part shapes).
// Then the axle is balanced with the counterweight, the add-on weight hung into the notch, the wheel spun up by hand,
// and let go: from there the controller runs the real precession.
// Until the release the controller is frozen and this component poses the apparatus itself: without spin the axle just
// tips about the horizontal pivot axis, driven by the parts already placed, until it hits a stop.
// Created at runtime by ModuleWebBridge (Setup); the page starts it with a preset {"mode":"assembly"}, any other
// preset ends it (End puts every part back where the scene had it). In the editor it starts by itself.
[DefaultExecutionOrder(1000)]                                                   // after HangingWeight, PivotHead and the orbit camera
public class AssemblyMode : MonoBehaviour {
    public enum Phase { Build, Balance, Weight, Spin, Released }

    public const int SlotCount = 6;
    public const float MinBalance = 0.08f, MaxBalance = 0.24f;                  // m, range of the counterweight slider

    private const float AddOnGrams = 30f;                                       // the add-on weight of the game (3 discs)
    private const float ReleaseRpm = 400f;
    private const float StartDistance = 0.11f;                                  // m: the counterweight first sits too close, the wheel side tips
    private const float BalanceTolerance = 0.004f;                              // m around the balance position ...
    private const float BalanceSeconds = 0.5f;                                  // ... held this long: it snaps in
    private const float StopRad = 25f * Mathf.Deg2Rad;                          // tilt stops of the bearing
    private const float Damping = 2f;                                           // 1/s, friction of the tilt
    private const float Bounce = 0.25f;                                         // share of the tilt speed kept at a stop
    private const float AxleInertia = 0.002f;                                   // the axle's own mass is neglected (as in LabGyroscope),
                                                                                // except for this bit: a bare axle must not divide by zero
    private const float HoldSeconds = 0.6f;                                     // levelling the axle by hand
    private const float SpinUpSeconds = 1.5f;
    private const float FlightSeconds = 0.35f;
    private const float FlightHop = 0.05f;                                      // m, a moving part rises in an arc
    private const float LiftHeight = 0.04f;                                     // m, a clicked part rises from the tray
    private const float SnapRadius = 0.05f;                                     // m between the pointer ray and a marker: a drop snaps
    private const float ClickRadius = 0.035f;                                   // m: a click hits a marker
    private const float DragThreshold = 6f;                                     // px before a press on a part becomes a drag
    private const float MarkerSize = 0.03f;                                     // m
    private const float BeamOutside = 0.009f;                                   // m, light beam beyond the wheel rim (the strip reaches 0.018)
    private const float SlideAway = 0.30f;                                      // m, the light barrier makes room for the precession
    private const float TrayDistance = 0.50f;                                   // m, tray row in front of the stand (camera side)
    private const float TrayGap = 0.06f;                                        // m between the parts
    private const float TrayHeight = 0.01f;
    private const float LedFlashSeconds = 0.05f;

    // colours: the markers and the LED glow neutral white (bloom), never in a quantity's colour; text dark grey
    private static readonly Color MarkerDim = new Color(1.1f, 1.1f, 1.1f);
    private static readonly Color MarkerBright = new Color(2.2f, 2.2f, 2.2f);
    private static readonly Color LedOff = new Color(0.12f, 0.12f, 0.13f);
    private static readonly Color LedOn = new Color(2.4f, 2.4f, 2.3f);
    private static readonly Color32 TextColor = new Color32(40, 44, 52, 255);
    private static readonly Color32 TrayColor = new Color32(205, 209, 216, 255);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static Quaternion Lying => Quaternion.AngleAxis(-90f, Vector3.forward);   // on the tray: local up → +x, towards the stand

    private enum HoldReason { Balance, Spin }

    private class Part {
        public string id, name;
        public int slot;                                                        // 0: fits nowhere
        public Transform t;
        public Transform home;                                                  // scene parent; null for the parts this mode builds
        public Vector3 homePos;
        public Quaternion homeRot;
        public Vector3 trayPos;                                                 // world, resting on the tray
        public Quaternion trayRot;
        public Vector3[] corners;                                               // mesh bounds corners relative to t, rotation removed
        public bool placed;                                                     // on its slot, or on the way there
        public bool landed;                                                     // arrived: counts for the tilt
        public bool flying;
        public float flightT;
        public Vector3 fromPos;
        public Quaternion fromRot;
        public float lift;
    }

    // ---- wiring (Setup) ----
    private ModuleGyroscopeController controller;
    private ModuleHelpers helpers;
    private Action<string> post;
    private LabGyroscope lab;
    private Transform gyroscope, labRig, body, stand, rotor, strip;
    private Camera cam;
    private OrbitCamera orbit;
    private bool ready;
    private Transform root;                                                     // world-fixed: tray, extra parts, markers, labels
    private readonly List<Part> parts = new List<Part>();                       // tray order, mixed: it mustn't give the build order away
    private Part pivot, axle, wheel, counterweight, lightgate, weight, pendulum;
    private Renderer led;
    private readonly Transform[] markers = new Transform[SlotCount + 1];        // index = slot number
    private readonly Renderer[] markerGlows = new Renderer[SlotCount + 1];
    private TextMeshPro nameLabel;
    private MaterialPropertyBlock block;
    private readonly List<Material> createdMaterials = new List<Material>();
    private Mesh cylinderMesh;
    private Func<bool> blockOrbit;

    // ---- state ----
    private bool active;
    private Phase phase;
    private string hint = "";
    private float e, eDot;                                                      // rad: elevation of the counterweight side
    private float distance;                                                     // m, counterweight from the pivot
    private bool held;                                                          // levelled and held by hand: no tilting
    private HoldReason holdReason;
    private float holdFrom, holdT;
    private float balanceTimer;
    private float spinDeg, spinRate, spinTarget;
    private bool spunUp;
    private bool lightgateAway;
    private float ledTimer, lastStripAngle;
    private bool hadStripAngle;
    private Part selected, dragging, hoverPart;
    private int hoverSlot;
    private bool pressActive, pressMoved;
    private Vector2 pressPos;
    private Part pressPart;
    private int pressSlot;
    private float grabDepth;
    private Vector3 grabOffset, dragPos;
    private string savedFlags;
    private int seq;
    private readonly ModuleAssemblyMessage message = new ModuleAssemblyMessage();


    // ---- read by the editor test panel ----

    public bool Active => active;
    public Phase CurrentPhase => phase;
    public string Hint => hint;
    public int PartCount => parts.Count;
    public string PartId(int i) => parts[i].id;
    public string PartName(int i) => parts[i].name;
    public bool PartPlaced(int i) => parts[i].placed;
    public string SelectedId => selected != null ? selected.id : "";
    public float CounterweightPosition => distance;
    public bool CanBalance => active && phase == Phase.Balance && !held;
    public bool CanSpin => active && phase == Phase.Weight && weight.landed && !held;
    public bool CanRelease => active && phase == Phase.Spin && spunUp;

    public string PhaseLabel {
        get {
            switch (phase) {
                case Phase.Build: return "Aufbau";
                case Phase.Balance: return "Ausbalancieren";
                case Phase.Weight: return "Zusatzgewicht";
                case Phase.Spin: return "Andrehen";
                default: return "Losgelassen";
            }
        }
    }

    public bool SlotAvailable(int n) {
        if (!active || phase == Phase.Released || Filled(n)) return false;
        switch (n) {
            case 1: return true;                                                // Lagerbock on the rod
            case 2: return Filled(1);                                           // Achse into the bearing
            case 3: case 4: return Filled(2);                                   // Rad, Ausgleichsgewicht on the axle ends
            case 5: return Filled(3);                                           // Lichtschranke at the wheel
            case 6: return phase >= Phase.Weight;                               // Zusatzgewicht only when balanced
        }
        return false;
    }


    // ---- called by ModuleWebBridge (and the editor test panel) ----

    public void Setup(ModuleGyroscopeController controller, ModuleHelpers helpers, Action<string> post) {
        this.controller = controller;
        this.helpers = helpers;
        this.post = post;
        ApparatusRig rig = controller != null ? controller.LabRig : null;
        lab = rig != null ? rig.Parameters as LabGyroscope : null;
        cam = Camera.main;
        orbit = cam != null ? cam.GetComponent<OrbitCamera>() : null;
        TextMeshPro labelStyle = helpers != null ? helpers.GetComponentInChildren<TextMeshPro>(true) : null;
        if (lab == null || rig.StandObject == null || rig.Rotor == null || orbit == null || labelStyle == null) {
            Debug.LogWarning("AssemblyMode: lab apparatus, camera or labels not found, assembly mode off.");
            return;
        }
        gyroscope = controller.transform;
        labRig = rig.transform;
        stand = rig.StandObject.transform;
        rotor = rig.Rotor;
        strip = rotor.Find("Strip");
        body = labRig.Find("Body");
        PivotHead head = stand.GetComponentInChildren<PivotHead>(true);
        Transform axleObject = body != null ? body.Find("Axle") : null;
        Transform counterweightObject = body != null ? body.Find("Counterweight") : null;
        HangingWeight hanging = body != null ? body.GetComponentInChildren<HangingWeight>(true) : null;
        VectorArrow3D arrow = controller.GetComponentInChildren<VectorArrow3D>(true);
        if (strip == null || head == null || axleObject == null || counterweightObject == null || hanging == null || arrow == null) {
            Debug.LogWarning("AssemblyMode: a part of the lab apparatus is missing, assembly mode off.");
            return;
        }

        // materials: copies of the scene's (the apparatus stays metal and grey), unlit for what glows
        Material metal = MaterialOf(stand.Find("Rod"));
        Material dark = MaterialOf(head.transform.Find("Block"));
        Material steel = MaterialOf(axleObject);
        Material unlit = arrow.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
        GameObject floor = GameObject.Find("Floor");
        Material trayMaterial = Copy(floor != null ? MaterialOf(floor.transform) : metal, "AssemblyTray", TrayColor);
        Material markerMaterial = Copy(unlit, "AssemblyMarker", MarkerDim);
        Material ledMaterial = Copy(unlit, "AssemblyLed", LedOff);
        cylinderMesh = axleObject.GetComponent<MeshFilter>().sharedMesh;        // the apparatus' smooth cylinder
        block = new MaterialPropertyBlock();

        root = new GameObject("Assembly").transform;                            // hidden at the end, until Begin

        // parts in tray order (left to right in the start view)
        float pivotHeight = gyroscope.position.y - stand.position.y;
        wheel = AddPart("wheel", "Rad", 3, rotor, Quaternion.identity);
        weight = AddPart("weight", "Zusatzgewicht", 6, hanging.transform, Lying);
        pivot = AddPart("pivot", "Lagerbock", 1, head.transform, Quaternion.identity);
        lightgate = AddPart("lightgate", "Lichtschranke", 5, BuildLightgate(pivotHeight, metal, dark, ledMaterial), Lying);
        axle = AddPart("axle", "Achse", 2, axleObject, Lying);
        pendulum = AddPart("pendulum", "Pendelgewicht", 0, BuildPendulum(steel, dark), Lying);
        counterweight = AddPart("counterweight", "Ausgleichsgewicht", 4, counterweightObject, Quaternion.identity);
        LayOutTray(trayMaterial);

        for (int n = 1; n <= SlotCount; n++) {
            Transform marker = new GameObject($"Slot{n}").transform;
            marker.SetParent(root, false);
            markerGlows[n] = Glowing(Primitive(PrimitiveType.Sphere, "Glow", marker, Vector3.zero, Vector3.one * MarkerSize, markerMaterial));
            Text(marker, "Number", n.ToString(), 0.2f, labelStyle);
            markers[n] = marker;
        }
        nameLabel = Text(root, "PartName", "", 0.22f, labelStyle);
        root.gameObject.SetActive(false);

#if UNITY_EDITOR
        gameObject.AddComponent<AssemblyTestPanel>();
#endif
        ready = true;
    }


    // (re)start: every part on the tray, the bare stand, all helpers off, the view onto stand and tray
    public void Begin() {
        if (!ready) return;
        if (!active) {
            savedFlags = JsonUtility.ToJson(helpers.Show);
            foreach (System.Reflection.FieldInfo field in typeof(HelperFlags).GetFields()) {   // all, also flags added later
                if (field.FieldType == typeof(bool)) field.SetValue(helpers.Show, false);
            }
        }
        active = true;
        root.gameObject.SetActive(true);

        controller.SetApparatus(ModuleGyroscopeController.Apparatus.Lab);
        controller.SetStartMode(ModuleGyroscopeController.StartMode.Released);
        controller.SetRpm(0f);                                                  // the page reads ω_R = 0 while building
        controller.SetAddOnMassGrams(AddOnGrams);                               // HangingWeight shows the discs on the tray too
        controller.SetTheta0Deg(90f);
        controller.Restart();
        controller.Frozen = true;

        foreach (Part p in parts) {
            p.placed = p.landed = p.flying = false;
            p.lift = 0f;
            p.t.SetParent(root, false);
            p.t.SetPositionAndRotation(p.trayPos, p.trayRot);
        }
        phase = Phase.Build;
        e = eDot = 0f;
        distance = StartDistance;
        held = false;
        balanceTimer = 0f;
        spinDeg = spinRate = 0f;
        spinTarget = Mathf.Min(ReleaseRpm * 6f, controller.MaxVisibleSpinRps * 360f) * -lab.SpinSign;   // as the controller draws it
        spunUp = false;
        lightgateAway = false;
        selected = dragging = hoverPart = pressPart = null;
        pressActive = false;
        hoverSlot = 0;
        hint = Texts.Start;
        PoseGyroscope();
        SetView("oblique");
        Emit("begin");
    }


    // back to the normal simulation: every part where the scene had it, helpers as before
    public void End() {
        if (!ready || !active) return;
        active = false;
        foreach (Part p in parts) {
            if (p.home == null) continue;                                       // light barrier, pendulum weight: hidden with the root
            p.t.SetParent(p.home, false);
            p.t.localPosition = p.homePos;
            p.t.localRotation = p.homeRot;
        }
        root.gameObject.SetActive(false);
        controller.Frozen = false;
        JsonUtility.FromJsonOverwrite(savedFlags, helpers.Show);
        orbit.ResetView();
        selected = dragging = hoverPart = pressPart = null;
        Emit("end");
    }


    public void Select(string id) {
        if (!active) return;
        Part p = parts.Find(x => x.id == id);
        if (p == null) {
            Debug.LogWarning($"AssemblyMode: unknown part \"{id}\"");
            return;
        }
        if (p.placed) {
            hint = Texts.AlreadyPlaced;
            return;
        }
        if (p == selected) Deselect();
        else Select(p);
    }


    public void Place(int slot) {
        if (!active) return;
        if (selected == null) {
            hint = Texts.ChooseFirst;
            Emit("unavailable", null, slot);
            return;
        }
        TryPlace(selected, slot);
    }


    public void SetBalance(float metres) {
        if (CanBalance) distance = Mathf.Clamp(metres, MinBalance, MaxBalance);
    }


    public void DoAction(string action) {
        if (!active) return;
        switch (action) {
            case "spin": Spin(); break;
            case "release": Release(); break;
            default: Debug.LogWarning($"AssemblyMode: unknown action \"{action}\" (spin | release)"); break;
        }
    }


    // oblique: from above, side-on to the axle, tray in view; side: level with the axle; top: from above.
    // At φ = 0 the axle lies along z and the tray is on −x, so yaw 90° looks side-on across both.
    public void SetView(string view) {
        if (orbit == null) return;
        switch (view) {
            case "oblique": orbit.SetView(70f, 32f, 1.45f, new Vector3(-0.25f, -0.17f, 0f)); break;
            case "side": orbit.SetView(90f, 6f, 1.15f, new Vector3(0f, -0.1f, 0f)); break;
            case "top": orbit.SetView(90f, 85f, 1.5f, new Vector3(-0.25f, -0.33f, 0f)); break;
            default: Debug.LogWarning($"AssemblyMode: unknown view \"{view}\" (oblique | side | top)"); break;
        }
    }


    // ---- Unity ----

    void Awake() {
        blockOrbit = PointerIsMine;
    }


    void OnEnable() {
        OrbitCamera.BlockOrbit = blockOrbit;
    }


    void OnDisable() {
        if (OrbitCamera.BlockOrbit == blockOrbit) OrbitCamera.BlockOrbit = null;
    }


    void OnDestroy() {
        foreach (Material m in createdMaterials) Destroy(m);
    }


    void Update() {
        if (!active) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);                            // simulated time: the tilt pauses with the simulation
        float realDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);                // hands and animations: real time

        HandlePointer();
        if (phase == Phase.Released) return;                                    // the controller has the apparatus now
        UpdateHold(realDt);
        if (!held) Tilt(dt);
        UpdateBalance(realDt);
        UpdateSpin(realDt);
        PoseGyroscope();                                                        // in Update: HangingWeight hangs the weight from this pose
    }


    void LateUpdate() {
        if (!active) return;
        float realDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        foreach (Part p in parts) MovePart(p, realDt);
        if (phase != Phase.Released) {
            if (wheel.landed) rotor.localRotation = Quaternion.AngleAxis(spinDeg, Vector3.up);
            if (counterweight.landed) counterweight.t.localPosition = CounterweightLocal(distance);
        }
        UpdateLed(realDt);
        UpdateMarkers();
        UpdateNameLabel();
    }


    // ---- pointer: drag a part onto a marker, or click the part and then the marker ----

    private bool PointerIsMine() {
        Pointer pointer = Pointer.current;
        if (pointer == null) return false;
        Vector2 position = pointer.position.ReadValue();
        if (AssemblyTestPanel.Covers(position)) return true;
        if (!active || cam == null) return false;
        Ray ray = cam.ScreenPointToRay(position);
        return PartAt(ray) != null || (selected != null && SlotAt(ray, ClickRadius) != 0);
    }


    private void HandlePointer() {
        hoverPart = null;
        hoverSlot = 0;
        Pointer pointer = Pointer.current;
        if (pointer == null || cam == null) return;
        Vector2 position = pointer.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(position);
        bool overPanel = AssemblyTestPanel.Covers(position);

        if (pointer.press.wasPressedThisFrame && !overPanel) {
            pressActive = true;
            pressMoved = false;
            pressPos = position;
            pressPart = PartAt(ray);
            pressSlot = selected != null ? SlotAt(ray, ClickRadius) : 0;
        }
        if (pressActive && pointer.press.isPressed && (position - pressPos).sqrMagnitude > DragThreshold * DragThreshold) {
            pressMoved = true;
            if (pressPart != null && dragging == null) StartDrag(pressPart, ray);
        }

        if (dragging != null) {
            dragPos = PointAtDepth(ray, grabDepth) + grabOffset;
            dragPos.y = Mathf.Max(dragPos.y, stand.position.y + 0.02f);          // not through the floor
            hoverSlot = SlotAt(ray, SnapRadius);
        } else if (!overPanel) {
            hoverPart = PartAt(ray);
            if (selected != null) hoverSlot = SlotAt(ray, ClickRadius);
        }

        if (pressActive && pointer.press.wasReleasedThisFrame) {
            pressActive = false;
            if (dragging != null) Drop(ray);
            else if (!pressMoved) Click(pressPart, pressSlot);
        }
    }


    private void Click(Part part, int slot) {
        if (part != null) {
            if (part == selected) Deselect();
            else Select(part);
        } else if (slot != 0 && selected != null) {
            TryPlace(selected, slot);
        } else if (selected != null) {
            Deselect();                                                         // click into empty space
        }
    }


    private void Select(Part p) {
        if (p.placed || p == dragging) return;
        selected = p;
        Emit("select", p);
    }


    private void Deselect() {
        if (selected == null) return;
        Part p = selected;
        selected = null;
        Emit("deselect", p);
    }


    private void StartDrag(Part p, Ray ray) {
        if (selected != null && selected != p) Deselect();
        selected = null;
        p.flying = false;
        dragging = p;
        // the part moves in the plane at its depth in front of the camera, keeping where it was grabbed
        grabDepth = Vector3.Dot(p.t.position - cam.transform.position, cam.transform.forward);
        grabOffset = p.t.position - PointAtDepth(ray, grabDepth);
        dragPos = p.t.position;
        Emit("grab", p);
    }


    private void Drop(Ray ray) {
        Part p = dragging;
        dragging = null;
        int slot = SlotAt(ray, SnapRadius);                                     // only shown slots: TryPlace always moves it
        if (slot != 0) {
            TryPlace(p, slot);
        } else {
            StartFlight(p);
            hint = Texts.Back;
            Emit("back", p);
        }
    }


    // a right part flies onto its slot; a wrong one hops back onto the tray, with a hint that guides but doesn't tell
    private void TryPlace(Part p, int slot) {
        if (p.placed) return;
        if (!SlotAvailable(slot)) {
            hint = Texts.Unavailable;
            Emit("unavailable", p, slot);
            return;
        }
        if (selected == p) selected = null;
        StartFlight(p);                                                         // from where it is now: hand, tray or mid-air
        if (p.slot == slot) {
            p.placed = true;
            hint = Texts.Placed(p.id);
            Emit("place", p, slot);
        } else {
            hint = p == pendulum ? Texts.Pendulum : SlotAvailable(p.slot) ? Texts.Wrong(p.id, slot) : Texts.NotYet(p.id);
            Emit("wrong", p, slot);
        }
    }


    private Part PartAt(Ray ray) {
        Part best = null;
        float bestDistance = float.MaxValue;
        foreach (Part p in parts) {
            if (p.placed || p == dragging) continue;                            // placed parts stay where they are
            Bounds bounds = BoundsAt(p, p.t.position, p.t.rotation);
            bounds.Expand(0.02f);                                               // thin parts (axle, string) stay easy to hit
            if (bounds.IntersectRay(ray, out float d) && d < bestDistance) {
                best = p;
                bestDistance = d;
            }
        }
        return best;
    }


    // available slot closest to the ray, within radius (m)
    private int SlotAt(Ray ray, float radius) {
        int best = 0;
        float bestDistance = radius;
        for (int n = 1; n <= SlotCount; n++) {
            if (!SlotAvailable(n)) continue;
            Vector3 v = SlotPosition(n) - ray.origin;
            float along = Vector3.Dot(v, ray.direction);
            if (along < 0f) continue;
            float d = (v - ray.direction * along).magnitude;
            if (d < bestDistance) {
                best = n;
                bestDistance = d;
            }
        }
        return best;
    }


    private Vector3 PointAtDepth(Ray ray, float depth) {
        float along = Mathf.Max(Vector3.Dot(ray.direction, cam.transform.forward), 0.01f);
        return cam.transform.position + ray.direction * (depth / along);
    }


    // ---- the apparatus while building ----

    private void PoseGyroscope() {
        // as the controller's ApplyPose with φ = 0, θ_model = 90° − e (the figure axis points to the counterweight)
        gyroscope.localRotation = Quaternion.AngleAxis(-(90f - e * Mathf.Rad2Deg), Vector3.right);
    }


    // tilt about the horizontal pivot axis without spin: I ë = −g (m_c d_c + m_z r − m_w d_w) cos e − friction.
    // Only parts that have arrived count; wheel as a disk about its diameter plus Steiner, the others as point masses.
    private void Tilt(float dt) {
        if (!axle.landed) {
            e = eDot = 0f;
            return;
        }
        float moment = 0f, inertia = AxleInertia;                               // Σ m·d, positive on the counterweight side
        if (wheel.landed) {
            float m = lab.WheelMass, d = lab.WheelDistance, r = lab.WheelRadius;
            moment -= m * d;
            inertia += 0.25f * m * r * r + m * d * d;
        }
        if (counterweight.landed) {
            float m = lab.CounterweightMass;
            moment += m * distance;
            inertia += m * distance * distance;
        }
        if (weight.landed) {
            float m = AddOnGrams / 1000f, r = lab.NotchDistance;
            moment += m * r;
            inertia += m * r * r;
        }

        const int steps = 4;                                                    // the bare wheel tips fast
        float h = dt / steps;
        for (int i = 0; i < steps; i++) {
            float acceleration = -lab.Gravity * moment * Mathf.Cos(e) / inertia - Damping * eDot;
            eDot += acceleration * h;
            e += eDot * h;
            if (e > StopRad) {
                e = StopRad;
                if (eDot > 0f) eDot *= -Bounce;
            } else if (e < -StopRad) {
                e = -StopRad;
                if (eDot < 0f) eDot *= -Bounce;
            }
        }
    }


    private void StartHold(HoldReason reason) {
        held = true;
        holdReason = reason;
        holdFrom = e;
        holdT = 0f;
        eDot = 0f;
    }


    // a hand brings the axle level and holds it there
    private void UpdateHold(float dt) {
        if (!held || holdT >= 1f) return;
        holdT = Mathf.Min(1f, holdT + dt / HoldSeconds);
        e = Mathf.Lerp(holdFrom, 0f, Mathf.SmoothStep(0f, 1f, holdT));
        eDot = 0f;
        if (holdT < 1f || holdReason != HoldReason.Balance) return;
        held = false;                                                           // balanced: it stays level by itself
        phase = Phase.Weight;
        hint = Texts.Balanced;
        Emit("balanced");
    }


    // close enough for a moment: the counterweight snaps into the balance position
    private void UpdateBalance(float dt) {
        if (phase != Phase.Balance || held) return;
        float balance = lab.CounterweightDistance;
        if (Mathf.Abs(distance - balance) > BalanceTolerance) {
            balanceTimer = 0f;
            return;
        }
        balanceTimer += dt;
        if (balanceTimer < BalanceSeconds) return;
        distance = balance;
        StartHold(HoldReason.Balance);
    }


    private void Spin() {
        if (!CanSpin) {
            hint = Texts.NotReadyToSpin;
            Emit("unavailable");
            return;
        }
        phase = Phase.Spin;
        spinRate = 0f;
        spunUp = false;
        StartHold(HoldReason.Spin);
        hint = Texts.Spinning;
        Emit("spin");
    }


    // spun up by hand while the axle is held level; drawn at the controller's spin rate, so the release looks seamless
    private void UpdateSpin(float dt) {
        if (phase != Phase.Spin || holdT < 1f) return;
        spinRate = Mathf.MoveTowards(spinRate, spinTarget, Mathf.Abs(spinTarget) / SpinUpSeconds * dt);
        spinDeg = Mathf.Repeat(spinDeg + spinRate * dt, 360f);
        if (spunUp || spinRate != spinTarget) return;
        spunUp = true;
        hint = Texts.Ready;
        Emit("spun");
    }


    // from here the real simulation runs: released from rest, θ = 90°, with the add-on weight
    private void Release() {
        if (!CanRelease) return;
        phase = Phase.Released;
        held = false;
        controller.SetApparatus(ModuleGyroscopeController.Apparatus.Lab);
        controller.SetStartMode(ModuleGyroscopeController.StartMode.Released);
        controller.SetRpm(ReleaseRpm);
        controller.SetAddOnMassGrams(AddOnGrams);
        controller.SetTheta0Deg(90f);
        controller.Restart();
        controller.Frozen = false;
        lightgateAway = true;                                                   // the precessing wheel would run into it
        StartFlight(lightgate);
        hint = Texts.Released;
        Emit("release");
    }


    // ---- parts ----

    private Part AddPart(string id, string name, int slot, Transform t, Quaternion trayRot) {
        var p = new Part {
            id = id, name = name, slot = slot, t = t, trayRot = trayRot,
            home = t.parent != root ? t.parent : null,
            homePos = t.localPosition, homeRot = t.localRotation,
            corners = Corners(t),
        };
        parts.Add(p);
        return p;
    }


    // one row along the axle direction, each part resting on the tray
    private void LayOutTray(Material material) {
        Vector3 origin = stand.position;
        float rowX = origin.x - TrayDistance;
        float total = -TrayGap;
        foreach (Part p in parts) total += BoundsAt(p, Vector3.zero, p.trayRot).size.z + TrayGap;

        float z = origin.z + total / 2;
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (Part p in parts) {
            Bounds b = BoundsAt(p, Vector3.zero, p.trayRot);
            p.trayPos = new Vector3(rowX - b.center.x, origin.y + TrayHeight - b.min.y, z - b.size.z / 2 - b.center.z);
            z -= b.size.z + TrayGap;
            minX = Mathf.Min(minX, rowX - b.extents.x);
            maxX = Mathf.Max(maxX, rowX + b.extents.x);
        }
        Primitive(PrimitiveType.Cube, "Tray", root, new Vector3((minX + maxX) / 2, origin.y + TrayHeight / 2, origin.z),
                  new Vector3(maxX - minX + 0.06f, TrayHeight, total + 0.08f), material);
    }


    private void StartFlight(Part p) {
        if (p.landed && p.home != null) p.t.SetParent(root, true);              // off the apparatus
        p.landed = p.landed && p.home == null;                                  // the light barrier stays "landed" while it slides away
        p.fromPos = p.t.position;
        p.fromRot = p.t.rotation;
        p.flightT = 0f;
        p.flying = true;
    }


    private void MovePart(Part p, float dt) {
        if (p == dragging) {
            p.t.position = dragPos;
            return;
        }
        if (p.landed && p.home != null) return;                                 // on the apparatus: moves with it

        p.lift = Mathf.MoveTowards(p.lift, p == selected ? LiftHeight : 0f, dt * LiftHeight / 0.15f);
        Vector3 pos;
        Quaternion rot;
        if (p.placed) {
            InstalledPose(p, out pos, out rot);
        } else {
            pos = p.trayPos + Vector3.up * p.lift;
            rot = p.trayRot;
        }
        if (p.flying) {
            p.flightT = Mathf.Min(1f, p.flightT + dt / FlightSeconds);
            float s = Mathf.SmoothStep(0f, 1f, p.flightT);
            pos = Vector3.Lerp(p.fromPos, pos, s) + Vector3.up * (FlightHop * Mathf.Sin(Mathf.PI * p.flightT));
            rot = Quaternion.Slerp(p.fromRot, rot, s);
            if (p.flightT >= 1f) {
                p.flying = false;
                if (p.placed && !p.landed) {
                    Land(p);
                    if (p.home != null) return;
                }
            }
        }
        p.t.SetPositionAndRotation(pos, rot);
    }


    private void Land(Part p) {
        p.landed = true;
        if (p.home != null) {
            p.t.SetParent(p.home, false);
            p.t.localPosition = p == counterweight ? CounterweightLocal(distance) : p.homePos;
            p.t.localRotation = p == wheel ? Quaternion.AngleAxis(spinDeg, Vector3.up) : p.homeRot;
            if (p == weight) p.t.rotation = Quaternion.identity;                // hangs straight, as HangingWeight keeps it
        }
        if (phase == Phase.Build && pivot.landed && axle.landed && wheel.landed && counterweight.landed && lightgate.landed) {
            phase = Phase.Balance;
            hint = Texts.Balance;
            Emit("phase");
        }
        if (p == weight) hint = Texts.Tipped;
    }


    private void InstalledPose(Part p, out Vector3 pos, out Quaternion rot) {
        if (p == lightgate) {
            pos = LightgatePosition() + (lightgateAway ? LevelRotation * wheel.homePos.normalized * SlideAway : Vector3.zero);
            rot = Quaternion.LookRotation(LevelRotation * -wheel.homePos, Vector3.up);   // local x points away from the wheel
            return;
        }
        Vector3 local = p == counterweight ? CounterweightLocal(distance) : p.homePos;
        Quaternion localRot = p == wheel ? Quaternion.AngleAxis(spinDeg, Vector3.up) : p.homeRot;
        pos = p.home.TransformPoint(local);
        rot = p == weight ? Quaternion.identity : p.home.rotation * localRot;
    }


    private Vector3 CounterweightLocal(float d) => new Vector3(counterweight.homePos.x, d, counterweight.homePos.z);


    // the gyroscope as built: θ = 90°, φ = 0. LabRig sits at the gyroscope's origin, so the parts' home positions
    // under it are positions relative to the pivot.
    private Quaternion LevelRotation =>
        (gyroscope.parent != null ? gyroscope.parent.rotation : Quaternion.identity) * Quaternion.AngleAxis(-90f, Vector3.right);


    // The fork straddles the wheel rim at axle height, on the camera's side. Not below the wheel: the axle tips while
    // building, and only at axle height does the rim move straight up and down (along the fork's slot), never into it.
    private Vector3 LightgatePosition() {
        Quaternion level = LevelRotation;
        Vector3 outward = -(level * Vector3.right);
        return gyroscope.position + level * wheel.homePos + outward * (lab.WheelRadius + BeamOutside);
    }


    private Vector3 SlotPosition(int n) {
        Vector3 pivotPoint = stand.TransformPoint(pivot.homePos);
        switch (n) {
            case 1: return pivotPoint;
            case 2: return pivotPoint + Vector3.up * 0.05f;                     // above the bearing block, which hides the axle's seat
            case 3: return labRig.TransformPoint(wheel.homePos);
            case 4: return body.TransformPoint(CounterweightLocal(StartDistance));
            case 5: return LightgatePosition();
            default: return body.TransformPoint(weight.homePos);                // the notch
        }
    }


    private bool Filled(int n) {
        foreach (Part p in parts) {
            if (p.slot == n && p.placed) return true;
        }
        return false;
    }


    // ---- what the light barrier, the markers and the labels show ----

    // the LED lights while the strip passes the beam; between frames the strip moves up to 30° at the drawn spin
    // limit, so every pass is caught (sign change of the angle), not only frames with the strip inside the beam
    private void UpdateLed(float dt) {
        bool on = false;
        if (phase == Phase.Spin && holdT >= 1f && lightgate.landed && !lightgateAway) {
            Vector3 centre = rotor.position, axis = rotor.up;
            Vector3 toBeam = Vector3.ProjectOnPlane(LightgatePosition() - centre, axis);
            Vector3 toStrip = Vector3.ProjectOnPlane(strip.position - centre, axis);
            float angle = Vector3.SignedAngle(toBeam, toStrip, axis);
            if (hadStripAngle && Mathf.Sign(angle) != Mathf.Sign(lastStripAngle) && Mathf.Abs(angle - lastStripAngle) < 180f) {
                ledTimer = LedFlashSeconds;
            }
            on = Mathf.Abs(angle) < 4f;
            lastStripAngle = angle;
            hadStripAngle = true;
        } else {
            hadStripAngle = false;
        }
        ledTimer -= dt;
        block.SetColor(BaseColor, on || ledTimer > 0f ? LedOn : LedOff);
        led.SetPropertyBlock(block);
    }


    private void UpdateMarkers() {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        Quaternion facing = cam.transform.rotation;
        for (int n = 1; n <= SlotCount; n++) {
            bool show = SlotAvailable(n);
            markers[n].gameObject.SetActive(show);
            if (!show) continue;
            bool target = n == hoverSlot;
            markers[n].SetPositionAndRotation(SlotPosition(n), facing);         // the number faces the camera
            markers[n].localScale = Vector3.one * (target ? 1.5f : 1f + 0.12f * pulse);
            block.SetColor(BaseColor, target ? MarkerBright : Color.Lerp(MarkerDim, MarkerBright, 0.4f * pulse));
            markerGlows[n].SetPropertyBlock(block);
        }
    }


    private void UpdateNameLabel() {
        Part p = dragging ?? selected ?? hoverPart;
        nameLabel.gameObject.SetActive(p != null);
        if (p == null) return;
        Bounds b = BoundsAt(p, p.t.position, p.t.rotation);
        nameLabel.text = p.name;
        nameLabel.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 0.03f, b.center.z), cam.transform.rotation);
    }


    private void Emit(string evt, Part part = null, int slot = 0) {
        var placedIds = new List<string>();
        foreach (Part p in parts) {
            if (p.placed) placedIds.Add(p.id);
        }
        var slots = new List<int>();
        for (int n = 1; n <= SlotCount; n++) {
            if (SlotAvailable(n)) slots.Add(n);
        }
        message.phase = phase.ToString().ToLowerInvariant();                    // build | balance | weight | spin | released
        message.placed = placedIds.ToArray();
        message.slots = slots.ToArray();
        message.selected = SelectedId;
        message.@event = evt;
        message.part = part != null ? part.id : "";
        message.slot = slot;
        message.seq = ++seq;
        post?.Invoke(JsonUtility.ToJson(message));
    }


    // ---- building blocks ----

    // light barrier: post on a foot, U-fork at the top. Local frame: origin on the light beam, x away from the wheel,
    // z along the axle (the prongs sit either side of the rim), y up.
    private Transform BuildLightgate(float height, Material metal, Material dark, Material ledMaterial) {
        Transform t = new GameObject("Lichtschranke").transform;
        t.SetParent(root, false);
        const float bar = 0.045f;                                               // x of the bar joining the prongs, and of the post
        Primitive(PrimitiveType.Cylinder, "Foot", t, new Vector3(bar, -height + 0.004f, 0f), new Vector3(0.06f, 0.004f, 0.06f), dark);
        Primitive(PrimitiveType.Cylinder, "Post", t, new Vector3(bar, -height / 2, 0f), new Vector3(0.012f, (height - 0.016f) / 2, 0.012f), metal);
        Primitive(PrimitiveType.Cube, "Bar", t, new Vector3(bar, 0f, 0f), new Vector3(0.014f, 0.016f, 0.056f), dark);
        // prongs end 4 mm outside the rim: the wheel turns and tips without touching them, only the strip passes
        Primitive(PrimitiveType.Cube, "ProngA", t, new Vector3(0.0175f, 0f, -0.022f), new Vector3(0.045f, 0.016f, 0.008f), dark);
        Primitive(PrimitiveType.Cube, "ProngB", t, new Vector3(0.0175f, 0f, 0.022f), new Vector3(0.045f, 0.016f, 0.008f), dark);
        led = Glowing(Primitive(PrimitiveType.Sphere, "Led", t, new Vector3(bar, 0.011f, 0f), Vector3.one * 0.008f, ledMaterial));
        return t;
    }


    // distractor from the pendulum experiment: steel bob on a string
    private Transform BuildPendulum(Material steel, Material dark) {
        Transform t = new GameObject("Pendelgewicht").transform;
        t.SetParent(root, false);
        Primitive(PrimitiveType.Sphere, "Bob", t, Vector3.zero, Vector3.one * 0.05f, steel);
        Primitive(PrimitiveType.Cylinder, "Hook", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.008f, 0.006f, 0.008f), dark);
        Primitive(PrimitiveType.Cylinder, "String", t, new Vector3(0f, 0.126f, 0f), new Vector3(0.0015f, 0.09f, 0.0015f), dark);
        return t;
    }


    // primitive without collider (picking tests the bounds), cylinders with the apparatus' smooth mesh
    private Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        if (type == PrimitiveType.Cylinder && cylinderMesh != null) go.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }


    // markers and the LED are light, not objects: no shadows
    private static Renderer Glowing(Transform t) {
        var r = t.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }


    // like the helper labels: drawn on top with a white halo, so numbers and names stay readable over the apparatus
    private static TextMeshPro Text(Transform parent, string name, string text, float size, TextMeshPro style) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = style.font;
        tmp.fontSharedMaterial = style.fontSharedMaterial;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = TextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.1f);
        return tmp;
    }


    private Material Copy(Material source, string name, Color color) {
        var material = new Material(source) { name = name };
        material.SetColor(BaseColor, color);
        createdMaterials.Add(material);
        return material;
    }


    private static Material MaterialOf(Transform t) => t.GetComponent<Renderer>().sharedMaterial;


    // corners of every mesh's bounds below t, relative to t's position and rotation (scale kept): the bounds of the part
    // in any pose, without waiting for renderer bounds
    private static Vector3[] Corners(Transform t) {
        var corners = new List<Vector3>();
        Quaternion inverse = Quaternion.Inverse(t.rotation);
        foreach (MeshFilter filter in t.GetComponentsInChildren<MeshFilter>(true)) {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++) {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                corners.Add(inverse * (filter.transform.TransformPoint(c) - t.position));
            }
        }
        return corners.ToArray();
    }


    private static Bounds BoundsAt(Part p, Vector3 position, Quaternion rotation) {
        var bounds = new Bounds(position + rotation * p.corners[0], Vector3.zero);
        foreach (Vector3 c in p.corners) bounds.Encapsulate(position + rotation * c);
        return bounds;
    }


    // German texts in the module's form of address (Sie). The hints guide, they never name the answer.
    private static class Texts {
        public const string Start = "Bauen Sie den Kreisel auf: Ziehen Sie ein Teil auf eine leuchtende Markierung. "
                                    + "Oder klicken Sie erst das Teil und dann die Markierung.";
        public const string Back = "Das Teil liegt wieder auf dem Tisch. Legen Sie es auf eine leuchtende Markierung.";
        public const string ChooseFirst = "Wählen Sie zuerst ein Teil aus.";
        public const string AlreadyPlaced = "Dieses Teil ist schon eingebaut.";
        public const string Unavailable = "Diese Markierung ist gerade nicht frei.";
        public const string Pendulum = "Das Pendelgewicht gehört zum Pendelversuch, nicht hierher.";
        public const string Balance = "Alle Teile sitzen. Die Achse kippt aber zur Seite des Rades. "
                                      + "Verschieben Sie das Ausgleichsgewicht, bis die Achse in jeder Lage stehen bleibt.";
        public const string Balanced = "Ausbalanciert: Die Achse bleibt in jeder Lage stehen. Jetzt fehlt noch das Zusatzgewicht.";
        public const string Tipped = "Ohne Drehung kippt die Achse mit dem Zusatzgewicht einfach nach unten. "
                                     + "Was passiert wohl, wenn sich das Rad schnell dreht?";
        public const string NotReadyToSpin = "Zuerst muss der Kreisel fertig sein: ausbalanciert und mit Zusatzgewicht.";
        public const string Spinning = "Sie halten die Achse waagerecht und drehen das Rad an. "
                                       + "Die Lichtschranke leuchtet bei jeder Umdrehung kurz auf.";
        public const string Ready = "Das Rad dreht sich schnell. Lassen Sie los, wenn Sie bereit sind.";
        public const string Released = "Beobachten Sie die Achse.";

        public static string Placed(string id) {
            switch (id) {
                case "pivot": return "Richtig. Im Lagerbock kann die Achse kippen und sich drehen.";
                case "axle": return "Richtig. Die Achse liegt im Lager.";
                case "wheel": return "Richtig. Das Rad sitzt auf der Achse.";
                case "counterweight": return "Richtig. Das Ausgleichsgewicht sitzt auf der Achse.";
                case "lightgate": return "Richtig. Der Streifen am Radrand läuft durch die Gabel der Lichtschranke.";
                default: return "Richtig. Das Zusatzgewicht hängt in der Kerbe.";
            }
        }

        // the part's own slot is shown, but it went somewhere else
        public static string Wrong(string id, int slot) {
            switch (id) {
                case "pivot": return "Der Lagerbock ist das Lager der Achse. Er braucht selbst einen festen Halt.";
                case "axle": return "Die Achse muss in einem Lager liegen, in dem sie kippen und sich drehen kann.";
                case "wheel":
                    return slot == 4
                        ? "Auf dieser Seite hat die Achse eine Kerbe. Was muss dort später noch Platz haben?"
                        : "Das Rad dreht sich um die Achse. Wo kann es auf der Achse sitzen?";
                case "counterweight":
                    return slot == 3
                        ? "Das Ausgleichsgewicht soll sich entlang der Achse verschieben lassen. Ist auf dieser Seite genug Platz dafür?"
                        : "Das Ausgleichsgewicht gleicht das Rad aus. Wo muss es dafür sitzen?";
                case "lightgate": return "Die Lichtschranke misst die Drehzahl: Der Streifen am Radrand muss durch ihre Gabel laufen.";
                default: return "Das Zusatzgewicht hängt an einem Faden. Wo an der Achse lässt es sich einhängen?";
            }
        }

        // the part's own slot isn't there yet
        public static string NotYet(string id) {
            switch (id) {
                case "axle": return "Die Achse braucht zuerst ein Lager.";
                case "wheel":
                case "counterweight": return "Dafür muss zuerst die Achse im Lager liegen.";
                case "lightgate": return "Die Lichtschranke misst am Rad. Ist das Rad schon eingebaut?";
                default: return "Das Zusatzgewicht kommt erst dran, wenn der Kreisel ausbalanciert ist.";
            }
        }
    }
}
