using UnityEngine;
using UnityEngine.SceneManagement;

// Scene actions for the menu buttons (wired up from the Button's OnClick).
// It lives inside each screen, so the prefabs don't need references to scene objects.
public class SceneLoader : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f; // the victory screen pauses the game
        SceneManager.LoadScene(sceneName);
    }

    public void ReloadCurrent() => LoadScene(SceneManager.GetActiveScene().name);

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
