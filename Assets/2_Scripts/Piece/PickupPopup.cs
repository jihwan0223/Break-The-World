using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PickupPopup : MonoBehaviour
{
    [SerializeField] private float fontSize = 4f;
    [SerializeField] private float riseDistance = 0.6f; // 월드 유닛
    [SerializeField] private float lifetime = 0.8f; // 초 - 뒤 절반 동안 흐려짐
    [SerializeField] private float heightOffset = 0.3f;
    [SerializeField] private float mergeRadius = 1.5f; // 월드 유닛
    [SerializeField] private float mergeWindow = 0.3f; // 초
    [SerializeField] private int maxActive = 40; // 넘으면 오래된 것부터 재사용
    [SerializeField] private int sortingOrder = 35; // 파편/오브젝트(~32)보다 위, 커서 범위(40)보다 아래
    [SerializeField] private Color pieceColor = Color.white;
    [SerializeField] private Color crystalColor = new Color(0.55f, 0.9f, 1f);
    [SerializeField] private float popDuration = 0.16f; // 초
    [SerializeField] private float popStartScale = 0.3f;
    [SerializeField] private float popDrift = 0.45f; // 월드 유닛
    [SerializeField] private float popTilt = 12f; // 도
    [SerializeField] private float flashDuration = 0.08f; // 초 - 데미지 숫자가 처음 하얗게 번쩍이는 시간
    [SerializeField] private float shakeDuration = 0.15f; // 초 - 치명타 숫자가 떨리는 시간
    [SerializeField] private float shakeAmount = 0.08f; // 월드 유닛 - 치명타 숫자 떨림 폭
    [SerializeField] private float critOvershoot = 3f; // 치명타 팝이 튀는 정도 (일반 1.70158)

    private class Popup
    {
        public TextMeshPro text;
        public Vector3 start;
        public float age;
        public long amount;
        public bool isCrystal;
        public bool mergeable;
        public string sign;
        public bool pops;
        public float drift;
        public float baseAlpha = 1f;
        public Color baseColor = Color.white; // 번쩍임이 끝나고 돌아갈 색
        public bool shakes; // 치명타라 떨림
    }

    private readonly List<Popup> _active = new List<Popup>();
    private readonly Stack<Popup> _pool = new Stack<Popup>();

    public void Show(Vector3 position, long amount, bool isCrystal, bool merge)
    {
        if (merge)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Popup existing = _active[i];
                if (existing.mergeable && existing.isCrystal == isCrystal && existing.age < mergeWindow
                    && Mathf.Abs(existing.start.x - position.x) < mergeRadius)
                {
                    existing.amount += amount;
                    SetText(existing);
                    return;
                }
            }
        }

        Popup popup = Begin(position, amount, isCrystal ? crystalColor : pieceColor, "+", 1f);
        popup.isCrystal = isCrystal;
        popup.mergeable = merge;
    }

    public void ShowDamage(Vector3 position, long amount, Color color, float sizeScale, bool crit = false)
    {
        Popup popup = Begin(position, amount, color, "-", sizeScale);
        popup.pops = true;
        popup.shakes = crit;
        popup.text.color = new Color(1f, 1f, 1f, color.a); // 첫 프레임부터 하얗게
        popup.drift = Random.Range(-popDrift, popDrift);
        popup.text.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-popTilt, popTilt));
        popup.text.transform.localScale = Vector3.one * popStartScale;
    }

    private Popup Begin(Vector3 position, long amount, Color color, string sign, float sizeScale)
    {
        Popup popup;
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
        popup.isCrystal = false;
        popup.mergeable = false;
        popup.sign = sign;
        popup.pops = false;
        popup.drift = 0f;
        popup.shakes = false;
        popup.baseColor = color;
        popup.text.transform.rotation = Quaternion.identity;
        popup.text.transform.localScale = Vector3.one;
        popup.text.color = color;
        popup.baseAlpha = color.a;
        popup.text.fontSize = fontSize * sizeScale;
        popup.text.transform.position = popup.start;
        popup.text.gameObject.SetActive(true);
        SetText(popup);
        _active.Add(popup);
        return popup;
    }

    void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Popup popup = _active[i];
            popup.age += Time.deltaTime;
            float progress = popup.age / lifetime;
            if (progress >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _pool.Push(popup);
                continue;
            }

            float eased = 1f - (1f - progress) * (1f - progress);
            Vector3 shake = Vector3.zero; // 치명타 떨림 (점점 약해짐)
            if (popup.shakes && popup.age < shakeDuration)
                shake = (Vector3)(Random.insideUnitCircle * shakeAmount * (1f - popup.age / shakeDuration));
            popup.text.transform.position = popup.start + new Vector3(popup.drift * eased, riseDistance * eased, 0f) + shake;
            if (popup.pops)
            {
                float overshoot = popup.shakes ? critOvershoot : 1.70158f; // back 이징 값 (1.70158 = 표준)
                float back = Mathf.Clamp01(popup.age / popDuration) - 1f;
                float pop = 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
                popup.text.transform.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, pop);
            }
            Color color = popup.pops && popup.age < flashDuration
                ? Color.Lerp(Color.white, popup.baseColor, popup.age / flashDuration) // 처음엔 하얗게 번쩍
                : popup.baseColor;
            color.a = popup.baseAlpha * (progress < 0.5f ? 1f : 1f - (progress - 0.5f) / 0.5f);
            popup.text.color = color;
        }
    }

    private void SetText(Popup popup) =>
        popup.text.text = $"{popup.sign}{NumberFormatUtil.Format(popup.amount)}";

    private Popup CreatePopup()
    {
        var textObject = new GameObject("PickupPopup");
        textObject.transform.SetParent(transform, false);

        var text = textObject.AddComponent<TextMeshPro>();
        text.font = GameFonts.Tmp;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.sortingOrder = sortingOrder;
        // 테두리 두께는 8_Font/Resources/MonaS12 SDF Outline.mat에서 조절
        if (GameFonts.TmpOutline != null) text.fontSharedMaterial = GameFonts.TmpOutline;
        text.outlineColor = Color.black;

        return new Popup { text = text };
    }
}
