using UnityEngine;

// Keeps a world-space label facing the camera so the word stays readable
// from the angled top-down view. Put this on the Enemy's Label child.
public class Billboard : MonoBehaviour
{
    private Transform cam;
    void Start() { if (Camera.main != null) cam = Camera.main.transform; }
    void LateUpdate()
    {
        if (cam == null) return;
        transform.forward = cam.forward; // match camera orientation
    }
}
