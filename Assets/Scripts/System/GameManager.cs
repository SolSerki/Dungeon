using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private HealthSystem _playerHealth;
    [SerializeField] private GameObject _gameOverPanel;   // DefeatScreen instance
    [SerializeField] private GameObject _victoryPanel;    // VictoryScreen instance
    [SerializeField] private float _gameOverDelay = 1.5f; // lets the death animation play out

    private void OnEnable() => _playerHealth.OnDied += HandlePlayerDied;
    private void OnDisable() => _playerHealth.OnDied -= HandlePlayerDied;

    private void HandlePlayerDied() => StartCoroutine(ShowGameOver());

    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSeconds(_gameOverDelay);
        _gameOverPanel.SetActive(true);
    }

    // Called by the final exit of the last level (LevelExit without _nextScene)
    public void ShowVictory()
    {
        _victoryPanel.SetActive(true);
        Time.timeScale = 0f; // freezes enemies and player behind the screen; SceneLoader restores it
    }
}
