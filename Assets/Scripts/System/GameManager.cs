using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private HealthSystem _playerHealth;
    [SerializeField] private GameObject _gameOverPanel;   // instancia de DefeatScreen
    [SerializeField] private GameObject _victoryPanel;    // instancia de VictoryScreen
    [SerializeField] private float _gameOverDelay = 1.5f; // deja ver la animación de muerte

    private void OnEnable() => _playerHealth.OnDied += HandlePlayerDied;
    private void OnDisable() => _playerHealth.OnDied -= HandlePlayerDied;

    private void HandlePlayerDied() => StartCoroutine(ShowGameOver());

    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSeconds(_gameOverDelay);
        _gameOverPanel.SetActive(true);
    }

    // La llama la salida final del último nivel (LevelExit sin _nextScene)
    public void ShowVictory()
    {
        _victoryPanel.SetActive(true);
        Time.timeScale = 0f; // congela enemigos y jugador detrás de la pantalla; SceneLoader lo restaura
    }
}
