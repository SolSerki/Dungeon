using UnityEngine;
using UnityEngine.SceneManagement;

// Acciones de escena para los botones de los menús (se cablean desde el OnClick del Button).
// Vive dentro de cada pantalla, así los prefabs no necesitan referencias a objetos de la escena.
public class SceneLoader : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f; // la pantalla de victoria pausa el juego
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
