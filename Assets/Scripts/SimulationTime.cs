using UnityEngine;



public class SimulationTime : MonoBehaviour {
    public bool Paused { get; private set; }
    public float TimeScale { get; private set; } = 1f;              // the slow-motion factor, also while paused


    public void SetTimeScale(float s) {
        TimeScale = s;
        if (!Paused) {
           Apply(s);
        }
    }


    public void SetPaused(bool paused) {
        Paused = paused;
        if (paused) {
            Time.timeScale = 0f;            
        } else {
            Apply(TimeScale);    
        }       
    }


    public void TogglePause() { 
        SetPaused(!Paused); 
    }


    private void Apply(float s) {
        Time.timeScale = s;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;  
    }
}
