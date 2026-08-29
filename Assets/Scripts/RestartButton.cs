using UnityEngine;
using UnityEngine.SceneManagement;

// Hook UI buttons to these:
//   Restart()  -> reloads the current game scene for a fresh run
//   GoToMenu() -> returns to the main menu scene
// Both un-freeze time (Game Over set timeScale to 0).
public class RestartButton : MonoBehaviour
{
    [Tooltip("Exact name of your main menu scene (must be in Build Settings).")]
    public string menuSceneName = "MainMenu";

    public void Restart()
    {
        if (GameManager.Instance != null) GameManager.Instance.BankEarnings();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BankEarnings();
            GameManager.Instance.SaveProgressIfActive(); // autosave so Continue picks this run back up
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}