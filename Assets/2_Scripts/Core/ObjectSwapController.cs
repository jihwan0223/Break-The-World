using System.Collections;
using UnityEngine;

// Object 팝업을 닫을 때(선택이 바뀌어 있었다면) 지금 오브젝트를 왼쪽으로 날려 보내고
// 화면 중앙에서 새 오브젝트가 나타나 원래 자리로 돌아오는 전환 연출을 담당함.
// (팝업이 열려있는 동안은 화면을 거의 다 가리기 때문에, 애니메이션은 팝업이 닫힐 때 재생함 - SidePanelUI가 TriggerSwap을 호출)
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(AudioSource))]
public class ObjectSwapController : MonoBehaviour
{
    // 씬 어디서든 ObjectSwapController.Instance로 접근하기 위한 싱글톤
    public static ObjectSwapController Instance { get; private set; }

    [SerializeField] private float flyOutDuration = 0.3f; // 기존 오브젝트가 왼쪽으로 날아가는 시간(초)
    [SerializeField] private float flyInDuration = 0.3f; // 새 오브젝트가 화면 중앙에서 제자리로 오는 시간(초)
    [SerializeField] private float flyOutDistance = 8f; // 왼쪽으로 날아가는 거리 (월드 유닛)

    private Vector3 _restPosition; // 오브젝트가 평소 있어야 할 원래 위치
    private Health _health;
    private Collider2D _collider;
    private AudioSource _audioSource; // 날아간 오브젝트가 사라질 때 파괴 사운드 재생용
    private Coroutine _swapRoutine;

    void Awake()
    {
        // 씬에 ObjectSwapController가 중복으로 존재하지 않도록 방지
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

    // SidePanelUI가 Object 팝업을 닫을 때, 열려있던 동안 선택이 바뀌었으면 호출함
    public void TriggerSwap(ObjectData oldObject, ObjectData newObject)
    {
        if (_swapRoutine != null)
            StopCoroutine(_swapRoutine);

        _swapRoutine = StartCoroutine(SwapRoutine(oldObject, newObject));
    }

    private IEnumerator SwapRoutine(ObjectData oldObject, ObjectData newObject)
    {
        if (_collider != null) _collider.enabled = false; // 전환 연출 중에는 클릭 막기

        Vector3 flyOutTarget = _restPosition + Vector3.left * flyOutDistance;
        yield return MoveOverTime(transform.position, flyOutTarget, flyOutDuration);

        // 날아간(기존) 오브젝트가 사라지는 시점에 그 오브젝트의 파괴 사운드 재생
        if (oldObject != null && oldObject.breakSound != null)
            _audioSource.PlayOneShot(oldObject.breakSound);

        // 화면 중앙(오브젝트가 있던 깊이 기준)으로 순간 이동
        transform.position = GetScreenCenterWorldPosition();

        // 새 오브젝트의 체력/스프라이트로 교체
        _health.ApplyObjectTier(newObject.tier, newObject.indexInTier);

        yield return MoveOverTime(transform.position, _restPosition, flyInDuration);

        transform.position = _restPosition;

        if (_collider != null) _collider.enabled = true;

        _swapRoutine = null;
    }

    // from에서 to까지 duration초 동안 등속으로 이동
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

    // 현재 오브젝트의 깊이(z)를 유지한 채, 화면 정중앙에 해당하는 월드 좌표를 계산
    private Vector3 GetScreenCenterWorldPosition()
    {
        Camera cam = Camera.main;
        float depth = Mathf.Abs(_restPosition.z - cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }
}
