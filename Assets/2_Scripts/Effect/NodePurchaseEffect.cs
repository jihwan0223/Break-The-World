using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 화면은 timeScale 0이라 전부 unscaled 시간
public class NodePurchaseEffect : MonoBehaviour
{
    public static readonly Color NormalColor = new Color(1f, 0.93f, 0.55f);
    public static readonly Color CrystalColor = new Color(0.45f, 0.9f, 1f);

    private const float BurstDuration = 0.45f; // 초
    private const float PopDuration = 0.18f; // 초
    private const float PopAmount = 0.22f;
    private const float FlashAlpha = 0.65f;
    private const float FlashScale = 1.7f; // 노드 크기 기준
    private const int PieceCount = 8;
    private const int BigPieceCount = 16;
    private const float PieceSize = 0.14f; // 노드 한 변 대비
    private const float RevealDuration = 0.28f; // 초
    private const float RevealStartScale = 0.4f;

    private RectTransform _node;

    public static void Play(RectTransform node, Color color, bool big)
    {
        NodePurchaseEffect effect = Create(node);
        effect.StartCoroutine(effect.Burst(color, big));
    }

    public static void PlayReveal(RectTransform node)
    {
        NodePurchaseEffect effect = Create(node);
        effect.StartCoroutine(effect.Reveal());
    }

    private static NodePurchaseEffect Create(RectTransform node)
    {
        var holder = new GameObject("NodePurchaseEffect", typeof(RectTransform), typeof(NodePurchaseEffect));
        holder.transform.SetParent(node.parent, false);
        holder.transform.localPosition = node.localPosition;
        holder.transform.SetAsLastSibling();

        NodePurchaseEffect effect = holder.GetComponent<NodePurchaseEffect>();
        effect._node = node;
        return effect;
    }

    void OnDisable()
    {
        if (_node != null) _node.localScale = Vector3.one;
        Destroy(gameObject);
    }

    private IEnumerator Burst(Color color, bool big)
    {
        float nodeSize = _node.rect.width;
        int count = big ? BigPieceCount : PieceCount;
        float reach = nodeSize * (big ? 1.5f : 1f);

        RectTransform flash = CreateSquare("Flash", nodeSize, color, out Image flashImage);
        var pieces = new RectTransform[count];
        var pieceImages = new Image[count];
        var directions = new Vector2[count];
        var distances = new float[count];
        var spins = new float[count]; // 도/초
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            distances[i] = reach * Random.Range(0.6f, 1f);
            spins[i] = Random.Range(-360f, 360f);
            pieces[i] = CreateSquare("Piece", nodeSize * PieceSize * Random.Range(0.7f, 1.3f), color, out pieceImages[i]);
        }

        float elapsed = 0f;
        while (elapsed < BurstDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / BurstDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            float pop = Mathf.Clamp01(elapsed / PopDuration);
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

        Destroy(gameObject);
    }

    private IEnumerator Reveal()
    {
        const float overshoot = 1.70158f; // 표준 back 이징 값

        float elapsed = 0f;
        while (elapsed < RevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float back = Mathf.Clamp01(elapsed / RevealDuration) - 1f;
            float eased = 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
            _node.localScale = Vector3.one * Mathf.LerpUnclamped(RevealStartScale, 1f, eased);
            yield return null;
        }

        Destroy(gameObject);
    }

    private RectTransform CreateSquare(string squareName, float size, Color color, out Image image)
    {
        var square = new GameObject(squareName, typeof(RectTransform), typeof(Image));
        square.transform.SetParent(transform, false);

        image = square.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // 노드 클릭을 가로채지 않게

        var rect = (RectTransform)square.transform;
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }
}
