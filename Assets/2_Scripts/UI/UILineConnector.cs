using UnityEngine;
using UnityEngine.UI;

// 부모/자식 노드 RectTransform 두 개를 잇는 선. 인스펙터에 from/to만 꽂아두면
// 씬 뷰에서 두 노드 위치를 바꿔도 선이 알아서 따라옴. 둘 중 하나라도 안 보이면(비활성 상태) 선도 같이 숨김.
// (from/to는 이 선과 같은 부모(Content) 아래에 있어야 정확히 계산됨)
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class UILineConnector : MonoBehaviour
{
    [SerializeField] private RectTransform from; // 부모 노드
    [SerializeField] private RectTransform to; // 자식 노드
    [SerializeField] private float thickness = 4f; // 선 두께(px)

    private RectTransform _rect;
    private Image _image;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _image = GetComponent<Image>();
        _image.raycastTarget = false; // 선이 클릭을 가로채면 안 됨

        // 왼쪽 끝을 기준점으로 삼아야 늘리고 회전시키기 편함 (from 위치에서 to 방향으로 뻗어나가는 형태)
        _rect.pivot = new Vector2(0f, 0.5f);
        _rect.anchorMin = new Vector2(0f, 0.5f);
        _rect.anchorMax = new Vector2(0f, 0.5f);
    }

    void LateUpdate()
    {
        bool visible = from != null && to != null && from.gameObject.activeInHierarchy && to.gameObject.activeInHierarchy;

        if (_image.enabled != visible)
            _image.enabled = visible;

        if (!visible) return;

        // 같은 부모(Content) 기준 로컬 좌표로 변환해서 비교해야 정확함
        Transform parent = _rect.parent;
        Vector2 fromPos = parent.InverseTransformPoint(from.position);
        Vector2 toPos = parent.InverseTransformPoint(to.position);

        Vector2 delta = toPos - fromPos;
        float distance = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        _rect.anchoredPosition = fromPos;
        _rect.sizeDelta = new Vector2(distance, thickness);
        _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
