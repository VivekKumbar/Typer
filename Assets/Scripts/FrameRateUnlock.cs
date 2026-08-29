using UnityEngine;

// Unlocks the mobile frame rate (Unity defaults phones to 30).
// Put on any GameObject that loads early — or better, mark it DontDestroyOnLoad.
public class FrameRateUnlock : MonoBehaviour
{
    [Tooltip("Target FPS. 60 for smooth, -1 for platform max.")]
    public int targetFrameRate = 60;

    void Awake()
    {
        // VSync must be OFF or it overrides targetFrameRate
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
    }
}