using UnityEngine;


// The add-on weight on its string in the notch: moves with the axle, but always hangs straight down.
// Shows one slotted disc per 10 g; no string at all without add-on weight.
public class HangingWeight : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private GameObject stringAndHook;
    [SerializeField] private GameObject[] discs;                                // 10 g each, top to bottom


    // LateUpdate: after the controller has posed the gyroscope in Update
    void LateUpdate() {
        transform.rotation = Quaternion.identity;                               // world orientation: string vertical

        int count = Mathf.Clamp(Mathf.RoundToInt(controller.AddOnMassGrams / 10f), 0, discs.Length);
        for (int i = 0; i < discs.Length; i++) {
            discs[i].SetActive(i < count);
        }
        stringAndHook.SetActive(count > 0);
    }
}
