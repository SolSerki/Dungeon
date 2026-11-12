using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Big boss bar at the top of the HUD ("Shadow", the Level 4 Ghost).
// The Ghost has no health: each banishment is one hit out of its 7. The bar shows up
// when the fight starts and fades out a moment after the defeat.
// The fill layers sit behind the frame (BarH), whose hole crops their ends.
public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private Ghost _boss;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _fill;            // red: hits left
    [SerializeField] private Image _drain;           // white: the hit just lost, for a moment
    [SerializeField] private Sprite[] _fillFrames;   // BossBar_Red_1..7: element i = i + 1 hits left
    [SerializeField] private Sprite[] _drainFrames;  // BossBar_Drain_1..7, same order

    [Header("Timing")]
    [SerializeField] private float _fadeTime = 0.5f;
    [SerializeField] private float _drainDelay = 0.4f;  // how long the white piece stays
    [SerializeField] private float _shakeTime = 0.25f;
    [SerializeField] private int _shakePixels = 2;      // in bar pixels
    [SerializeField] private float _hideDelay = 2f;     // after the defeat

    private RectTransform _rect;
    private Vector2 _restPosition;
    private Coroutine _fade;
    private Coroutine _hit;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _restPosition = _rect.anchoredPosition;
        _group.alpha = 0f; // hidden until the fight starts
    }

    private void OnEnable()
    {
        _boss.OnAwakened += Show;
        _boss.OnBanished += HandleBanished;
        _boss.OnDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        _boss.OnAwakened -= Show;
        _boss.OnBanished -= HandleBanished;
        _boss.OnDefeated -= HandleDefeated;
    }

    private void Show()
    {
        SetFrame(_fill, _fillFrames, _fillFrames.Length);
        SetFrame(_drain, _drainFrames, 0);
        FadeTo(1f, 0f);
    }

    private void HandleBanished(int banishes, int total)
    {
        int before = HitsToFrame(total - banishes + 1, total);
        int after = HitsToFrame(total - banishes, total);
        if (_hit != null) StopCoroutine(_hit);
        _hit = StartCoroutine(Hit(before, after));
    }

    private void HandleDefeated() => FadeTo(0f, _hideDelay);

    // If the Ghost needs other than 7 banishes, the 7 frames are spread proportionally
    private int HitsToFrame(int hitsLeft, int total) =>
        Mathf.CeilToInt(Mathf.Clamp01((float)hitsLeft / total) * _fillFrames.Length);

    private IEnumerator Hit(int before, int after)
    {
        SetFrame(_drain, _drainFrames, before); // the lost piece flashes white...
        SetFrame(_fill, _fillFrames, after);    // ...while the red drops at once

        for (float t = 0f; t < _shakeTime; t += Time.deltaTime)
        {
            // whole bar pixels, so the pixel art isn't smeared between pixels
            var offset = new Vector2(Random.Range(-_shakePixels, _shakePixels + 1), Random.Range(-_shakePixels, _shakePixels + 1));
            _rect.anchoredPosition = _restPosition + offset * _rect.localScale.x;
            yield return null;
        }
        _rect.anchoredPosition = _restPosition;

        yield return new WaitForSeconds(_drainDelay);
        SetFrame(_drain, _drainFrames, 0);
    }

    private void FadeTo(float alpha, float delay)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(alpha, delay));
    }

    private IEnumerator Fade(float target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float start = _group.alpha;
        for (float t = 0f; t < _fadeTime; t += Time.deltaTime)
        {
            _group.alpha = Mathf.Lerp(start, target, t / _fadeTime);
            yield return null;
        }
        _group.alpha = target;
    }

    // frame 0 = nothing, frame n = frames[n - 1]
    private static void SetFrame(Image image, Sprite[] frames, int frame)
    {
        image.enabled = frame > 0;
        if (frame > 0) image.sprite = frames[frame - 1];
    }
}
