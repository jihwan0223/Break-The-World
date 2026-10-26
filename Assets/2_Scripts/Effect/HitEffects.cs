using System.Collections.Generic;
using UnityEngine;

// 오브젝트를 때렸을 때 맞은 자리에서 나오는 연출: 작은 부스러기가 튀고, 데미지 숫자("-12")가 떠오름.
// 체력이 많은 오브젝트도 스프라이트가 바뀌기 전부터 "맞고 있다"는 게 보이게 하려는 것.
// 씬에 따로 놓을 필요 없이 처음 쓸 때 스스로 만들어짐
public class HitEffects : MonoBehaviour
{
    private const int ChipsPerHit = 5;          // 한 번 맞을 때 튀는 부스러기 수
    private const int StrongChipsPerHit = 10;   // 치명타/럭키클릭일 때 튀는 부스러기 수
    private const int MaxChips = 120;           // 동시에 날아다닐 수 있는 부스러기 수 - 넘으면 가장 오래된 것부터 재사용
    private const float ChipLifetime = 0.45f;   // 부스러기가 사라질 때까지 시간(초)
    private const float ChipGravity = 12f;      // 부스러기가 아래로 떨어지는 가속도
    private const float ChipMinSize = 0.06f;    // 부스러기 한 변 최소 길이(월드 유닛)
    private const float ChipMaxSize = 0.13f;    // 부스러기 한 변 최대 길이(월드 유닛)
    private const int ChipSortingOrder = 26;    // 오브젝트/파편보다 앞, 숫자보다는 뒤
    private const float NumberJitter = 0.25f;   // 데미지 숫자가 겹치지 않게 좌우로 흩는 폭(월드 유닛)
    private const float NumberScale = 1.35f;      // 보통 데미지 숫자 크기 배율 (줍기 숫자 대비)
    private const float StrongNumberScale = 2f;   // 치명타/럭키클릭 숫자 크기 배율
    private const float ShieldedNumberScale = 1.1f; // 보스 보호막에 막혀 줄어든 숫자 크기 배율 (보통보다 작게)

    private static readonly Color DamageColor = Color.white;                        // 보통 데미지 숫자 색 (치명타/럭키클릭이 아닐 때)
    private static readonly Color StrongDamageColor = new Color(1f, 0.85f, 0.2f); // 치명타/럭키클릭 숫자 색
    private const string LuckyLabel = "LUCKY!"; // 럭키클릭 숫자 위에 붙는 글자
    private static readonly Color ShieldedDamageColor = new Color(0.62f, 0.78f, 0.92f); // 보스 보호막에 막혀 줄어든 숫자 색 (흐린 하늘색)

    // 날아가는 부스러기 하나
    private class Chip
    {
        public SpriteRenderer renderer; // 부스러기 그림
        public Vector2 velocity;        // 지금 속도
        public float spin;              // 회전 속도(도/초)
        public float age;               // 튄 뒤 흐른 시간(초)
        public Color color;             // 원래 색 (투명도만 바꿔 다시 넣음)
    }

    private static HitEffects _instance; // 처음 쓸 때 만들어지는 단 하나의 인스턴스 (씬이 바뀌면 같이 사라지고 다시 만들어짐)

    private readonly List<Chip> _active = new List<Chip>(); // 지금 날아가는 부스러기들 (오래된 순)
    private readonly Stack<Chip> _pool = new Stack<Chip>(); // 재사용 대기 중인 부스러기들
    private Sprite _square;        // 부스러기용 단색 사각형
    private PickupPopup _numbers;  // 데미지 숫자 표시 (줍기 숫자와 섞이지 않게 따로 둔 인스턴스)

    // point에서 부스러기가 튀고 데미지 숫자가 뜸. strong이면 치명타/럭키클릭 - 더 많이 튀고 숫자가 크고 노랗게 나옴.
    // shielded면 보스 보호막에 막혀 줄어든 데미지 - 숫자가 작고 흐린 하늘색 (치명타여도 이쪽이 우선)
    // lucky면 럭키클릭 - 숫자 위에 "LUCKY!"가 붙음 (숫자는 한 방에 깎은 남은 체력)
    public static void Play(Vector2 point, long damage, bool strong, Color chipColor, bool shielded = false, bool lucky = false)
    {
        if (_instance == null)
            _instance = new GameObject("HitEffects").AddComponent<HitEffects>();
        _instance.Emit(point, damage, strong, chipColor, shielded, lucky);
    }

    void Awake()
    {
        _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width); // 크기 1일 때 한 변이 1 유닛인 흰 사각형
        _numbers = gameObject.AddComponent<PickupPopup>();
    }

    private void Emit(Vector2 point, long damage, bool strong, Color chipColor, bool shielded, bool lucky)
    {
        int count = strong ? StrongChipsPerHit : ChipsPerHit; // 이번에 튀는 부스러기 수
        for (int i = 0; i < count; i++)
        {
            Chip chip; // 이번에 날릴 부스러기
            if (_active.Count >= MaxChips)
            {
                chip = _active[0];
                _active.RemoveAt(0);
            }
            else
            {
                chip = _pool.Count > 0 ? _pool.Pop() : CreateChip();
            }

            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad; // 위쪽 반원으로 튐
            float speed = Random.Range(2f, 4.5f) * (strong ? 1.3f : 1f); // 튀는 속도
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

        Vector3 numberPosition = new Vector3(point.x + Random.Range(-NumberJitter, NumberJitter), point.y, 0f); // 숫자가 뜰 자리
        if (GameSettings.DamageNumbers) // 설정에서 데미지 숫자를 끄면 부스러기만
            _numbers.ShowDamage(numberPosition, damage,
                shielded ? ShieldedDamageColor : strong ? StrongDamageColor : DamageColor,
                shielded ? ShieldedNumberScale : strong ? StrongNumberScale : NumberScale,
                lucky ? LuckyLabel : null);
    }

    void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Chip chip = _active[i]; // 이번에 움직일 부스러기
            chip.age += Time.deltaTime;
            float progress = chip.age / ChipLifetime; // 0~1 진행률
            if (progress >= 1f)
            {
                chip.renderer.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _pool.Push(chip);
                continue;
            }

            chip.velocity += Vector2.down * (ChipGravity * Time.deltaTime);
            Transform chipTransform = chip.renderer.transform; // 부스러기 위치/회전
            chipTransform.position += (Vector3)(chip.velocity * Time.deltaTime);
            chipTransform.Rotate(0f, 0f, chip.spin * Time.deltaTime);
            chip.renderer.color = new Color(chip.color.r, chip.color.g, chip.color.b, 1f - progress * progress); // 끝으로 갈수록 빠르게 흐려짐
        }
    }

    private Chip CreateChip()
    {
        var chipObject = new GameObject("HitChip"); // 부스러기 하나를 담을 오브젝트
        chipObject.transform.SetParent(transform, false);

        var chipRenderer = chipObject.AddComponent<SpriteRenderer>(); // 부스러기 그림
        chipRenderer.sprite = _square;
        chipRenderer.sortingOrder = ChipSortingOrder;
        return new Chip { renderer = chipRenderer };
    }
}
