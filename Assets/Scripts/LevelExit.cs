using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelExit : MonoBehaviour
{
    [Tooltip("Escena a cargar. Vacío = salida final: muestra la pantalla de victoria.")]
    [SerializeField] private string _nextScene = "DungeonLevel02";
    [Tooltip("Solo hace falta en la salida final.")]
    [SerializeField] private GameManager _gameManager;

    private bool _isLoading;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // El Character tiene dos colliders: sin esta guarda pediríamos la carga dos veces
        if (_isLoading) return;
        if (other.GetComponentInParent<Character>() == null) return; // ignora enemigos

        _isLoading = true;
        if (string.IsNullOrEmpty(_nextScene))
            _gameManager.ShowVictory();
        else
            SceneManager.LoadScene(_nextScene);
    }
}
