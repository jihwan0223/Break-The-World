using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(AudioSource))]
public class ObjectSwapController : MonoBehaviour
{
    public static ObjectSwapController Instance { get; private set; }

    [SerializeField] private float flyOutDuration = 0.3f; // 초
    [SerializeField] private float flyInDuration = 0.3f; // 초
    [SerializeField] private float flyOutDistance = 8f; // 월드 유닛

    private Vector3 _restPosition;
    private Health _health;
    private Collider2D _collider;
    private AudioSource _audioSource;
    private Coroutine _swapRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _restPosition = transform.position;
        _health = GetComponent<Health>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
    }

    public void TriggerSwap(ObjectData oldObject, ObjectData newObject)
    {
        if (_swapRoutine != null)
            StopCoroutine(_swapRoutine);

        _swapRoutine = StartCoroutine(SwapRoutine(oldObject, newObject));
    }

    private IEnumerator SwapRoutine(ObjectData oldObject, ObjectData newObject)
    {
        if (_collider != null) _collider.enabled = false;

        Vector3 flyOutTarget = _restPosition + Vector3.left * flyOutDistance;
        yield return MoveOverTime(transform.position, flyOutTarget, flyOutDuration);

        if (oldObject != null && oldObject.breakSound != null)
            SfxPlayer.Play(_audioSource, oldObject.breakSound, priority: true);

        transform.position = GetScreenCenterWorldPosition();

        _health.ApplyObjectTier(newObject.tier, newObject.indexInTier, newObject.hpMultiplier);

        yield return MoveOverTime(transform.position, _restPosition, flyInDuration);

        transform.position = _restPosition;

        if (_collider != null) _collider.enabled = true;

        _swapRoutine = null;
    }

    private IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(from, to, progress);
            yield return null;
        }

        transform.position = to;
    }

    private Vector3 GetScreenCenterWorldPosition()
    {
        Camera cam = Camera.main;
        float depth = Mathf.Abs(_restPosition.z - cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }
}
