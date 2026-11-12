using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

// Dialogue box: writes letter by letter whatever is in the Text field of _text (set it in
// the Inspector) and closes by itself after _holdTime.
// Any button (keyboard, mouse or gamepad) skips: the first press shows the whole text,
// the next one closes the box.
public class DialogueBox : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _lettersPerSecond = 30f;
    [SerializeField] private float _holdTime = 2.5f; // seconds the full text stays on screen

    private bool _pressed;

    // Run it from another coroutine: yield return box.Show();
    // (the box starts inactive, and an inactive object can't run its own coroutines)
    public IEnumerator Show()
    {
        gameObject.SetActive(true);
        _text.maxVisibleCharacters = 0; // only hides letters: the text stays as you wrote it
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;

        _pressed = false;
        IDisposable listener = InputSystem.onAnyButtonPress.Call(_ => _pressed = true);

        // Letter by letter, until it finishes or a button is pressed
        float shown = 0f;
        while (shown < total && !_pressed)
        {
            shown += _lettersPerSecond * Time.unscaledDeltaTime;
            _text.maxVisibleCharacters = Mathf.Min((int)shown, total);
            yield return null;
        }
        _text.maxVisibleCharacters = total;

        // The press that completed the text doesn't close it too
        _pressed = false;

        float timer = 0f;
        while (timer < _holdTime && !_pressed)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        listener.Dispose();
        gameObject.SetActive(false);
    }
}
