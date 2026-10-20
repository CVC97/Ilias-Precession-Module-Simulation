using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


public class OrbitCamera : MonoBehaviour {
    [Header("Target")]
    [SerializeField] private Transform target;                                          // Gyroscope
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, -0.03f, 0f);        // look slightly below the pivot

    [Header("Start view")]
    [SerializeField] private float yaw = 0f;                                            // deg
    [SerializeField] private float pitch = 20f;                                         // deg
    [SerializeField] private float distance = 0.35f;                                    // m

    [Header("Limits")]
    [SerializeField] private float minPitch = -10f, maxPitch = 85f;
    [SerializeField] private float minDistance = 0.12f, maxDistance = 1.0f;

    [Header("Feel")]
    [SerializeField] private float rotateSpeed = 0.25f;                                 // deg per pixel of mouse movement
    [SerializeField] private float zoomStep = 1.1f;                                     // factor per scroll notch
    [SerializeField] private float smoothTime = 0.08f;                                  // s

    // smoothed values that are actually used for the camera
    private float currentYaw, currentPitch, currentDistance;
    private float yawVel, pitchVel, distanceVel;                                        // SmoothDamp needs these
    private bool dragging;


    private bool PointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();



    void Start() {
        currentYaw = yaw;
        currentPitch = pitch;
        currentDistance = distance;
    }


    void Update() {
        Mouse mouse = Mouse.current;
        if (mouse == null) {
            return;                                                                     // e.g. a device without mouse
        }

        // start / end of a drag
        if (mouse.leftButton.wasPressedThisFrame) {
            dragging = !PointerOverUI();                                                // decided ONCE at the start of the drag
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

        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 pos = target.position + targetOffset + rot * new Vector3(0f, 0f, -currentDistance);
        transform.SetPositionAndRotation(pos, rot);
    }
}