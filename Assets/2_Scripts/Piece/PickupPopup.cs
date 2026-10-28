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

    public void ShowDamage(Vector3 position, long amount, Color color, float sizeScale)
    {
        Popup popup = Begin(position, amount, color, "-", sizeScale);
        popup.pops = true;
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
            popup.text.transform.position = popup.start + new Vector3(popup.drift * eased, riseDistance * eased, 0f);
            if (popup.pops)
            {
                const float overshoot = 1.70158f; // 표준 back 이징 값
                float back = Mathf.Clamp01(popup.age / popDuration) - 1f;
                float pop = 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
                popup.text.transform.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, pop);
            }
            Color color = popup.text.color;
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
