using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelExit : MonoBehaviour
{
    [Tooltip("Scene to load. Empty = final exit: shows the victory screen.")]
    [SerializeField] private string _nextScene = "DungeonLevel02";
    [Tooltip("Only needed on the final exit.")]
    [SerializeField] private GameManager _gameManager;

    private bool _isLoading;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The Character has two colliders: without this guard the load would be requested twice
        if (_isLoading) return;
        if (other.GetComponentInParent<Character>() == null) return; // ignores enemies

        _isLoading = true;
        if (string.IsNullOrEmpty(_nextScene))
            _gameManager.ShowVictory();
        else
            SceneManager.LoadScene(_nextScene);
    }
}
