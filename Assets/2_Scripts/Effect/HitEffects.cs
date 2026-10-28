using System.Collections.Generic;
using UnityEngine;

public class HitEffects : MonoBehaviour
{
    private const int ChipsPerHit = 5;
    private const int StrongChipsPerHit = 10;
    private const int MaxChips = 120; // 넘으면 오래된 것부터 재사용
    private const float ChipLifetime = 0.45f; // 초
    private const float ChipGravity = 12f;
    private const float ChipMinSize = 0.06f; // 월드 유닛
    private const float ChipMaxSize = 0.13f; // 월드 유닛
    private const int ChipSortingOrder = 26;
    private const float NumberJitter = 0.25f; // 월드 유닛
    private const float NumberScale = 1.35f;
    private const float StrongNumberScale = 2f;
    private const float ShieldedNumberScale = 1.1f;

    private static readonly Color DamageColor = Color.white;
    private static readonly Color StrongDamageColor = new Color(1f, 0.55f, 0.55f, 0.95f);
    private static readonly Color SuperDamageColor = new Color(1f, 0.9f, 0.45f, 0.95f);
    private static readonly Color ShieldedDamageColor = new Color(0.62f, 0.78f, 0.92f);

    private class Chip
    {
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float spin; // 도/초
        public float age;
        public Color color;
    }

    private static HitEffects _instance;

    private readonly List<Chip> _active = new List<Chip>();
    private readonly Stack<Chip> _pool = new Stack<Chip>();
    private Sprite _square;
    private PickupPopup _numbers;

    public static void Play(Vector2 point, long damage, bool strong, Color chipColor, bool shielded = false, bool lucky = false)
    {
        if (_instance == null)
            _instance = new GameObject("HitEffects").AddComponent<HitEffects>();
        _instance.Emit(point, damage, strong, chipColor, shielded, lucky);
    }

    void Awake()
    {
        _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
        _numbers = gameObject.AddComponent<PickupPopup>();
    }

    private void Emit(Vector2 point, long damage, bool strong, Color chipColor, bool shielded, bool lucky)
    {
        int count = strong ? StrongChipsPerHit : ChipsPerHit;
        for (int i = 0; i < count; i++)
        {
            Chip chip;
            if (_active.Count >= MaxChips)
            {
                chip = _active[0];
                _active.RemoveAt(0);
            }
            else
            {
                chip = _pool.Count > 0 ? _pool.Pop() : CreateChip();
            }

            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float speed = Random.Range(2f, 4.5f) * (strong ? 1.3f : 1f);
            chip.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            chip.spin = Random.Range(-540f, 540f);
            chip.age = 0f;
            chip.color = chipColor;
            chip.renderer.color = chipColor;
            chip.renderer.transform.position = point;
            chip.renderer.transform.localScale = Vector3.one * Random.Range(ChipMinSize, ChipMaxSize);
            chip.renderer.gameObject.SetActive(true);
            _active.Add(chip);
        }

        Vector3 numberPosition = new Vector3(point.x + Random.Range(-NumberJitter, NumberJitter), point.y, 0f);
        if (GameSettings.DamageNumbers)
            _numbers.ShowDamage(numberPosition, damage,
                shielded ? ShieldedDamageColor : lucky ? SuperDamageColor : strong ? StrongDamageColor : DamageColor,
                shielded ? ShieldedNumberScale : strong ? StrongNumberScale : NumberScale);
    }

    void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Chip chip = _active[i];
            chip.age += Time.deltaTime;
            float progress = chip.age / ChipLifetime;
            if (progress >= 1f)
            {
                chip.renderer.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _pool.Push(chip);
                continue;
            }

            chip.velocity += Vector2.down * (ChipGravity * Time.deltaTime);
            Transform chipTransform = chip.renderer.transform;
            chipTransform.position += (Vector3)(chip.velocity * Time.deltaTime);
            chipTransform.Rotate(0f, 0f, chip.spin * Time.deltaTime);
            chip.renderer.color = new Color(chip.color.r, chip.color.g, chip.color.b, 1f - progress * progress);
        }
    }

    private Chip CreateChip()
    {
        var chipObject = new GameObject("HitChip");
        chipObject.transform.SetParent(transform, false);

        var chipRenderer = chipObject.AddComponent<SpriteRenderer>();
        chipRenderer.sprite = _square;
        chipRenderer.sortingOrder = ChipSortingOrder;
        return new Chip { renderer = chipRenderer };
    }
}
