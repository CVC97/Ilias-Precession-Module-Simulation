using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


public class OrbitCamera : MonoBehaviour {
    // set by components that use the pointer themselves (AssemblyMode: a part is grabbed, its editor test panel);
    // true = this press is theirs. Checked once at the press, like the UI test.
    public static Func<bool> BlockOrbit;

    [Header("Target")]
    [SerializeField] private Transform target;                                          // Gyroscope
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, -0.03f, 0f);        // look slightly below the pivot

    [Header("Start view")]
    [SerializeField] private float yaw = 0f;                                            // deg
    [SerializeField] private float pitch = 20f;                                         // deg
    [SerializeField] private float distance = 0.5f;                                     // m

    [Header("Limits")]
    [SerializeField] private float minPitch = -10f, maxPitch = 85f;
    [SerializeField] private float minDistance = 0.35f, maxDistance = 1.35f;

    [Header("Feel")]
    [SerializeField] private float rotateSpeed = 0.25f;                                 // deg per pixel of mouse movement
    [SerializeField] private float zoomStep = 1.1f;                                     // factor per scroll notch
    [SerializeField] private float smoothTime = 0.08f;                                  // s

    // smoothed values that are actually used for the camera
    private float currentYaw, currentPitch, currentDistance;
    private Vector3 currentOffset;
    private float yawVel, pitchVel, distanceVel;                                        // SmoothDamp needs these
    private Vector3 offsetVel;
    private bool dragging;
    private float startYaw, startPitch, startDistance;                                 // the scene's view, for ResetView
    private Vector3 startOffset;


    private bool PointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();


    // a prepared view (AssemblyMode: oblique, side, top); the camera glides there like after a drag
    public void SetView(float yaw, float pitch, float distance, Vector3 offset) {
        this.yaw = yaw;
        this.pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        this.distance = Mathf.Clamp(distance, minDistance, maxDistance);
        targetOffset = offset;
    }


    // back to the view the scene starts with
    public void ResetView() => SetView(startYaw, startPitch, startDistance, startOffset);



    // in Awake, before anyone's Start can call SetView
    void Awake() {
        startYaw = yaw;
        startPitch = pitch;
        startDistance = distance;
        startOffset = targetOffset;
    }


    void Start() {
        currentYaw = yaw;
        currentPitch = pitch;
        currentDistance = distance;
        currentOffset = targetOffset;
    }


    void Update() {
        Mouse mouse = Mouse.current;
        if (mouse == null) {
            return;                                                                     // e.g. a device without mouse
        }

        // start / end of a drag
        if (mouse.leftButton.wasPressedThisFrame) {
            dragging = !PointerOverUI() && !(BlockOrbit != null && BlockOrbit());       // decided ONCE at the start of the drag
        }
        if (mouse.leftButton.wasReleasedThisFrame) {
            dragging = false;
        }

        // rotate while dragging
        if (dragging) {
            Vector2 delta = mouse.delta.ReadValue();                                    // Vector2, pixels moved since last frame
            yaw += delta.x * rotateSpeed;
            pitch += delta.y * rotateSpeed;                                             // moving the mouse up should look from higher up — check the sign!
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // zoom (only when not over UI)
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0 && !PointerOverUI()) {
            distance = scroll > 0 ? distance / zoomStep : distance * zoomStep;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            
        }
    }


    // runs after all Update() calls of the frame
    void LateUpdate() {
        // current values = SmoothDamp(current, target, ...)
        currentYaw = Mathf.SmoothDampAngle(currentYaw, yaw, ref yawVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, pitch, ref pitchVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        currentDistance = Mathf.SmoothDamp(currentDistance, distance, ref distanceVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        currentOffset = Vector3.SmoothDamp(currentOffset, targetOffset, ref offsetVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 pos = target.position + currentOffset + rot * new Vector3(0f, 0f, -currentDistance);
        transform.SetPositionAndRotation(pos, rot);
    }
}