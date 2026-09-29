using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 파편을 주운 자리 위에 "+N"이 떠올랐다 사라지는 연출.
// 클릭으로 주운 건 하나씩 띄우고, 빗자루가 주운 건 근처(mergeRadius) 것끼리 한 숫자로 합쳐서 띄움 (한꺼번에 많이 떠서 지저분해지지 않게)
public class PickupPopup : MonoBehaviour
{
    [SerializeField] private float fontSize = 4f; // 글자 크기 (TextMeshPro 월드 기준)
    [SerializeField] private float riseDistance = 0.6f; // 사라질 때까지 위로 떠오르는 거리(월드 유닛)
    [SerializeField] private float lifetime = 0.8f; // 떠 있는 시간(초) - 뒤 절반 동안 흐려짐
    [SerializeField] private float heightOffset = 0.3f; // 파편 중심보다 이만큼 위에서 시작
    [SerializeField] private float mergeRadius = 1.5f; // 빗자루가 주운 파편끼리 이 거리 안이면 한 숫자로 합침
    [SerializeField] private float mergeWindow = 0.3f; // 뜬 지 이 시간(초) 안의 숫자에만 합침 - 지나면 새 숫자를 띄움
    [SerializeField] private int maxActive = 40; // 동시에 떠 있을 수 있는 최대 개수 - 넘으면 가장 오래된 것부터 재사용
    [SerializeField] private int sortingOrder = 30; // 파편/결정보다 위에 그려지도록
    [SerializeField] private Color pieceColor = Color.white; // 조각 숫자 색
    [SerializeField] private Color crystalColor = new Color(0.55f, 0.9f, 1f); // 결정 숫자 색 (좌상단 결정 라벨과 같은 하늘색)

    // 떠 있는 숫자 하나
    private class Popup
    {
        public TextMeshPro text; // 숫자 글자
        public Vector3 start; // 떠오르기 시작한 위치
        public float age; // 뜬 뒤 흐른 시간(초)
        public long amount; // 지금 표시 중인 합계
        public bool isCrystal; // 결정 숫자인지
        public bool mergeable; // 빗자루 숫자인지 (빗자루 숫자끼리만 합침)
    }

    private readonly List<Popup> _active = new List<Popup>(); // 지금 떠 있는 숫자들 (오래된 순)
    private readonly Stack<Popup> _pool = new Stack<Popup>(); // 다 떠서 재사용 대기 중인 숫자들

    // 주운 자리에 +amount를 띄움. merge면 근처의 방금 뜬 빗자루 숫자에 합침
    public void Show(Vector3 position, long amount, bool isCrystal, bool merge)
    {
        if (merge)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Popup existing = _active[i]; // 합칠 수 있는지 볼 숫자
                if (existing.mergeable && existing.isCrystal == isCrystal && existing.age < mergeWindow
                    && Mathf.Abs(existing.start.x - position.x) < mergeRadius)
                {
                    existing.amount += amount;
                    SetText(existing);
                    return;
                }
            }
        }

        Popup popup; // 새로 띄울 숫자
        if (_active.Count >= maxActive)
        {
            popup = _active[0];
            _active.RemoveAt(0);
        }
        else
        {
            popup = _pool.Count > 0 ? _pool.Pop() : CreatePopup();
        }

        popup.start = position + Vector3.up * heightOffset;
        popup.age = 0f;
        popup.amount = amount;
        popup.isCrystal = isCrystal;
        popup.mergeable = merge;
        popup.text.color = isCrystal ? crystalColor : pieceColor;
        popup.text.transform.position = popup.start;
        popup.text.gameObject.SetActive(true);
        SetText(popup);
        _active.Add(popup);
    }

    void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Popup popup = _active[i]; // 이번에 움직일 숫자
            popup.age += Time.deltaTime;
            float progress = popup.age / lifetime; // 0~1 진행률
            if (progress >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _pool.Push(popup);
                continue;
            }

            popup.text.transform.position = popup.start + Vector3.up * (riseDistance * (1f - (1f - progress) * (1f - progress))); // 처음엔 빠르게, 끝에 느리게 (ease-out)
            Color color = popup.text.color; // 투명도만 바꿔 다시 넣을 색
            color.a = progress < 0.5f ? 1f : 1f - (progress - 0.5f) / 0.5f;
            popup.text.color = color;
        }
    }

    private void SetText(Popup popup) =>
        popup.text.text = $"+{NumberFormatUtil.Format(popup.amount)}";

    private Popup CreatePopup()
    {
        var textObject = new GameObject("PickupPopup"); // 숫자 하나를 담을 오브젝트
        textObject.transform.SetParent(transform, false);

        var text = textObject.AddComponent<TextMeshPro>(); // 월드 공간 글자
        text.font = GameFonts.Tmp;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.sortingOrder = sortingOrder;
        text.outlineWidth = 0.25f; // 파편 더미 위에서도 잘 보이게 검은 테두리
        text.outlineColor = Color.black;

        return new Popup { text = text };
    }
}
