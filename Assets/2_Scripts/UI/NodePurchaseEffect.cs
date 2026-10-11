using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 트리 노드 연출. 샀을 때: 노드가 툭 커졌다 돌아오고, 빛이 번지고, 작은 조각들이 사방으로 튐.
// 새로 열렸을 때: 작았다가 살짝 넘치게 커지며 튀어나옴.
// 노드와 같은 부모 아래에 연출용 오브젝트를 잠깐 만들었다 지움 (업그레이드 화면은 timeScale 0이라 전부 unscaled 시간).
public class NodePurchaseEffect : MonoBehaviour
{
    public static readonly Color NormalColor = new Color(1f, 0.93f, 0.55f);  // 조각으로 사는 노드 / 해금 노드의 연출 색
    public static readonly Color CrystalColor = new Color(0.45f, 0.9f, 1f);  // 결정으로 사는 노드의 연출 색

    private const float BurstDuration = 0.45f; // 구매 연출 전체 시간(초)
    private const float PopDuration = 0.18f;   // 노드가 커졌다 돌아오는 시간(초)
    private const float PopAmount = 0.22f;     // 노드가 가장 커졌을 때 더해지는 배율
    private const float FlashAlpha = 0.65f;    // 번지는 빛의 처음 불투명도
    private const float FlashScale = 1.7f;     // 번지는 빛이 끝에 도달하는 배율 (노드 크기 기준)
    private const int PieceCount = 8;          // 튀는 조각 수 (보통)
    private const int BigPieceCount = 16;      // 튀는 조각 수 (최대 레벨 달성 / 해금)
    private const float PieceSize = 0.14f;     // 조각 한 변 길이 (노드 한 변 기준 비율)
    private const float RevealDuration = 0.28f; // 등장 연출 시간(초)
    private const float RevealStartScale = 0.4f; // 등장할 때 시작 배율

    private RectTransform _node; // 연출 대상 노드 - 연출이 중간에 끊겨도 배율을 되돌리려고 기억

    // 구매 연출. big이면 조각이 더 많이, 더 멀리 튐 (최대 레벨을 찍었거나 오브젝트를 해금했을 때)
    public static void Play(RectTransform node, Color color, bool big)
    {
        NodePurchaseEffect effect = Create(node);
        effect.StartCoroutine(effect.Burst(color, big));
    }

    // 등장 연출 - 방금 공개된 노드가 튀어나옴
    public static void PlayReveal(RectTransform node)
    {
        NodePurchaseEffect effect = Create(node);
        effect.StartCoroutine(effect.Reveal());
    }

    private static NodePurchaseEffect Create(RectTransform node)
    {
        var holder = new GameObject("NodePurchaseEffect", typeof(RectTransform), typeof(NodePurchaseEffect)); // 연출 조각들을 담는 임시 오브젝트
        holder.transform.SetParent(node.parent, false);
        holder.transform.localPosition = node.localPosition;
        holder.transform.SetAsLastSibling(); // 노드들 위에 그려지게

        NodePurchaseEffect effect = holder.GetComponent<NodePurchaseEffect>(); // 코루틴을 돌릴 컴포넌트
        effect._node = node;
        return effect;
    }

    // 화면이 닫히는 등으로 연출이 끊기면 노드 배율을 원래대로 돌리고 정리
    void OnDisable()
    {
        if (_node != null) _node.localScale = Vector3.one;
        Destroy(gameObject);
    }

    private IEnumerator Burst(Color color, bool big)
    {
        float nodeSize = _node.rect.width; // 노드 한 변 길이 - 모든 크기/거리의 기준
        int count = big ? BigPieceCount : PieceCount; // 이번에 튀는 조각 수
        float reach = nodeSize * (big ? 1.5f : 1f); // 조각이 노드 가장자리에서 날아가는 거리

        RectTransform flash = CreateSquare("Flash", nodeSize, color, out Image flashImage); // 노드 위에서 번지는 빛
        var pieces = new RectTransform[count];   // 튀는 조각들
        var pieceImages = new Image[count];      // 조각 이미지 (투명도 조절용)
        var directions = new Vector2[count];     // 조각별 날아가는 방향
        var distances = new float[count];        // 조각별 날아가는 거리 (조금씩 다르게)
        var spins = new float[count];            // 조각별 회전 속도(도/초)
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f; // 고르게 퍼지되 살짝 흐트러진 각도
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            distances[i] = reach * Random.Range(0.6f, 1f);
            spins[i] = Random.Range(-360f, 360f);
            pieces[i] = CreateSquare("Piece", nodeSize * PieceSize * Random.Range(0.7f, 1.3f), color, out pieceImages[i]);
        }

        float elapsed = 0f; // 연출 시작 후 지난 시간
        while (elapsed < BurstDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / BurstDuration); // 전체 진행도 0~1
            float eased = 1f - Mathf.Pow(1f - progress, 3f);          // 처음엔 빠르고 끝으로 갈수록 느려짐

            float pop = Mathf.Clamp01(elapsed / PopDuration); // 노드 커졌다 돌아오기 진행도
            _node.localScale = Vector3.one * (1f + PopAmount * Mathf.Sin(pop * Mathf.PI));

            flash.localScale = Vector3.one * Mathf.Lerp(1f, FlashScale, eased);
            flashImage.color = new Color(color.r, color.g, color.b, FlashAlpha * (1f - progress));

            for (int i = 0; i < count; i++)
            {
                pieces[i].anchoredPosition = directions[i] * (nodeSize * 0.5f + distances[i] * eased);
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, spins[i] * elapsed);
                pieces[i].localScale = Vector3.one * (1f - progress * 0.6f);
                pieceImages[i].color = new Color(color.r, color.g, color.b, 1f - progress * progress);
            }
            yield return null;
        }

        Destroy(gameObject); // OnDisable에서 노드 배율도 원래대로
    }

    private IEnumerator Reveal()
    {
        const float overshoot = 1.70158f; // 끝에서 살짝 넘쳤다 돌아오는 정도 (표준 back 이징 값)

        float elapsed = 0f; // 연출 시작 후 지난 시간
        while (elapsed < RevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float back = Mathf.Clamp01(elapsed / RevealDuration) - 1f; // -1~0, 0에 가까울수록 끝
            float eased = 1f + (overshoot + 1f) * back * back * back + overshoot * back * back; // 넘쳤다 돌아오는 진행도
            _node.localScale = Vector3.one * Mathf.LerpUnclamped(RevealStartScale, 1f, eased);
            yield return null;
        }

        Destroy(gameObject);
    }

    // 스프라이트 없는 단색 사각형 하나를 만듦 (도트 느낌에 맞춰 조각도 빛도 전부 사각형)
    private RectTransform CreateSquare(string squareName, float size, Color color, out Image image)
    {
        var square = new GameObject(squareName, typeof(RectTransform), typeof(Image)); // 사각형 오브젝트
        square.transform.SetParent(transform, false);

        image = square.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // 노드 클릭을 가로채지 않게

        var rect = (RectTransform)square.transform; // 크기/위치를 정할 RectTransform
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }
}
