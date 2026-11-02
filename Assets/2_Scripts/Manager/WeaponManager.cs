using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    // 같은 티어 오브젝트를 4~8클릭에 부수도록 잡은 데미지 (다음 무기 = 이전 x2 + 4)
    // 맵마다 2개: 책상 위 / 길거리 / 공사장 / 바다 / 우주
    private static readonly List<WeaponData> weapons = new List<WeaponData>
    {
        new WeaponData(1, "맨손", 1),
        new WeaponData(2, "프라이팬", 6),
        new WeaponData(3, "야구 방망이", 16),
        new WeaponData(4, "교통 표지판", 36),
        new WeaponData(5, "곡괭이", 76),
        new WeaponData(6, "다이너마이트", 156),
        new WeaponData(7, "작살", 316),
        new WeaponData(8, "닻", 636),
        new WeaponData(9, "미사일", 1276),
        new WeaponData(10, "블랙홀", 2556),
    };

    [SerializeField] private Sprite[] weaponIcons; // weapons와 같은 순서
    [SerializeField] private float[] weaponSizeMultipliers; // weapons와 같은 순서, 0 이하는 1
    [SerializeField] private float[] weaponRotationOffsets; // 도, weapons와 같은 순서
    [SerializeField] private int cursorSize = 64; // 픽셀, 무기 커서 한 변 크기
    [SerializeField] private float cursorTilt = 25f; // 도, 마우스 화살표처럼 세로에서 왼쪽으로 기운 각도
    private const int CursorSampleSize = 256; // 돌리기 전에 원본을 읽어오는 크기 (클수록 덜 깨짐)

    private int equippedIndex;
    private bool[] _unlocked;
    private Texture2D _cursorTexture; // 지금 커서로 쓰는 무기 그림 (바꿀 때 이전 것 삭제)
    private Vector2 _cursorHotspot; // 커서 클릭 지점 (왼쪽 위 기준 픽셀)
    private bool _cursorShown; // 지금 무기 커서가 보이는지

    public event Action<int> OnUnlockChanged;

    public int WeaponCount => weapons.Count;
    public int VisibleWeaponCount => GameSettings.IsDemo ? Mathf.Min(GameSettings.DemoWeaponCount, weapons.Count) : weapons.Count; // 데모에선 풀버전 무기 숨김
    public int EquippedIndex => equippedIndex;
    public WeaponData CurrentWeapon => weapons[equippedIndex];
    public int CurrentClickDamage => CurrentWeapon.clickDamage;

    public static int StaticWeaponCount => weapons.Count;

    public static string StaticWeaponNameAt(int index) =>
        index >= 0 && index < weapons.Count ? weapons[index].weaponName : "";

    public static int StaticIndexOfWeaponName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName)) return -1;

        string wanted = weaponName.Trim();
        for (int i = 0; i < weapons.Count; i++)
        {
            if (string.Equals(weapons[i].weaponName.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public event Action<WeaponData> OnWeaponChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < weapons.Count && weaponIcons != null && i < weaponIcons.Length; i++)
            weapons[i].icon = weaponIcons[i];

        _unlocked = new bool[weapons.Count];
        _unlocked[0] = true;
    }

    void Start() => ApplyCursor();

    void OnDestroy()
    {
        if (Instance != this) return;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // 메뉴로 나가면 기본 커서로
        if (_cursorTexture != null) Destroy(_cursorTexture);
    }

    // 게임 화면 위에서만 무기 커서, UI 위/창 밖/일시정지면 원래 커서
    void Update()
    {
        bool show = _cursorTexture != null && Application.isFocused && !UIPointerGuard.IsPointerOverUI && IsMouseInWindow(); // 무기 커서를 보일지
        if (show == _cursorShown) return;
        _cursorShown = show;
        Cursor.SetCursor(show ? _cursorTexture : null, show ? _cursorHotspot : Vector2.zero, CursorMode.Auto);
    }

    private static bool IsMouseInWindow()
    {
        if (UnityEngine.InputSystem.Mouse.current == null) return false;
        Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue(); // 화면 픽셀 좌표
        return pos.x >= 0f && pos.y >= 0f && pos.x < Screen.width && pos.y < Screen.height;
    }

    // 장착 무기 그림을 마우스 화살표처럼 기울여 커서 크기로 만듦
    private void ApplyCursor()
    {
        Sprite icon = CurrentWeapon.icon; // 장착 무기 그림
        if (icon == null) return;

        int size = Mathf.Max(16, cursorSize); // 커서 한 변
        Rect rect = icon.textureRect; // 아틀라스 안 그림 영역
        Texture source = icon.texture; // 원본 텍스처 (읽기 불가라 GPU로 복사)
        float fit = CursorSampleSize / Mathf.Max(rect.width, rect.height); // 긴 변을 샘플 크기에 맞춤
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width * fit)); // 샘플 너비
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height * fit)); // 샘플 높이

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32); // 복사용 임시 버퍼
        Graphics.Blit(source, rt, new Vector2(rect.width / source.width, rect.height / source.height), new Vector2(rect.x / source.width, rect.y / source.height));
        var sample = new Texture2D(width, height, TextureFormat.RGBA32, false); // CPU로 읽은 원본
        RenderTexture previous = RenderTexture.active; // 원래 렌더 대상
        RenderTexture.active = rt;
        sample.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] src = sample.GetPixels32(); // 아래 줄부터 저장됨
        Destroy(sample);

        // 맨손만 세워 그렸고 나머지는 45도 대각선 - 모두 세로에서 cursorTilt만큼 왼쪽으로 기울게 돌림
        float drawnTilt = equippedIndex == 0 ? 0f : 45f; // 그림에 이미 들어간 기울기 (도)
        float rad = (cursorTilt - drawnTilt) * Mathf.Deg2Rad; // 더 돌릴 각도, +면 반시계
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad); // 회전값

        // 돌린 뒤 그림이 차지하는 범위
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue; // 회전 좌표 범위
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (src[y * width + x].a < 128) continue;
                float cx = x + 0.5f - width * 0.5f, cy = y + 0.5f - height * 0.5f; // 중심 기준 좌표
                float rx = cx * cos - cy * sin, ry = cx * sin + cy * cos; // 돌린 좌표
                minX = Mathf.Min(minX, rx); maxX = Mathf.Max(maxX, rx);
                minY = Mathf.Min(minY, ry); maxY = Mathf.Max(maxY, ry);
            }
        if (minX > maxX) return;

        float scale = size / Mathf.Max(maxX - minX + 1f, maxY - minY + 1f); // 돌린 그림을 커서에 맞추는 배율
        float midX = (minX + maxX) * 0.5f, midY = (minY + maxY) * 0.5f; // 돌린 그림 중심
        var pixels = new Color32[size * size]; // 새 커서 그림, 빈 곳은 투명
        Vector2 hotspot = Vector2.zero; // 클릭 지점 = 가장 왼쪽 위 픽셀 (무기 머리 끝)
        int best = int.MaxValue; // x + 위에서부터 y 가 가장 작은 값
        for (int oy = 0; oy < size; oy++)
            for (int ox = 0; ox < size; ox++)
            {
                float rx = midX + (ox + 0.5f - size * 0.5f) / scale, ry = midY + (oy + 0.5f - size * 0.5f) / scale; // 돌린 좌표
                int sx = Mathf.FloorToInt(rx * cos + ry * sin + width * 0.5f); // 되돌린 원본 x
                int sy = Mathf.FloorToInt(-rx * sin + ry * cos + height * 0.5f); // 되돌린 원본 y
                if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
                Color32 c = src[sy * width + sx]; // 가장 가까운 원본 픽셀
                if (c.a < 128) continue;
                c.a = 255;
                pixels[oy * size + ox] = c;
                int fromTop = size - 1 - oy; // 위에서부터 줄 번호
                if (ox + fromTop < best) { best = ox + fromTop; hotspot = new Vector2(ox, fromTop); }
            }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; // 새 커서
        texture.SetPixels32(pixels);
        texture.Apply();

        if (_cursorTexture != null) Destroy(_cursorTexture);
        _cursorTexture = texture;
        _cursorHotspot = hotspot;
        _cursorShown = false; // 다음 Update에서 다시 적용
    }

    public bool IsUnlocked(int index) => index >= 0 && index < _unlocked.Length && _unlocked[index];

    public bool IsUnlockOrderMet(int index) => index > 0 && IsUnlocked(index - 1);

    // 새 티어 첫 오브젝트의 해금 비용과 같음
    public long GetUnlockCost(int index) =>
        ObjectManager.Instance != null ? ObjectManager.Instance.GetUnlockCost(ObjectManager.FirstIndexOfTier(weapons[index].tier)) : 0;

    public bool AnyUnlockAffordable()
    {
        if (CurrencyManager.Instance == null) return false;

        long have = CurrencyManager.Instance.GetPieces();
        for (int i = 1; i < VisibleWeaponCount; i++)
            if (!_unlocked[i] && IsUnlockOrderMet(i) && have >= GetUnlockCost(i)) return true;
        return false;
    }

    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= VisibleWeaponCount || _unlocked[index] || !IsUnlockOrderMet(index)) return false;

        var cost = new List<PieceCost> { new PieceCost(GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost)) return false;

        SetUnlocked(index, true);
        return true;
    }

    public void SetUnlocked(int index, bool unlocked)
    {
        if (index <= 0 || index >= _unlocked.Length) return;
        _unlocked[index] = unlocked;
        OnUnlockChanged?.Invoke(index);
    }

    public void UnlockAllDebug()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, true);
    }

    public void ResetUnlocks()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, false);
        Equip(0);
    }

    public WeaponData GetWeaponAt(int index) => weapons[index];

    // 플레이 중 인스펙터 수정이 반영되게 매번 읽음
    public float GetSizeMultiplier(int index)
    {
        if (weaponSizeMultipliers != null && index >= 0 && index < weaponSizeMultipliers.Length && weaponSizeMultipliers[index] > 0f)
            return weaponSizeMultipliers[index];

        return 1f;
    }

    public float GetRotationOffset(int index)
    {
        if (weaponRotationOffsets != null && index >= 0 && index < weaponRotationOffsets.Length)
            return weaponRotationOffsets[index];

        return 0f;
    }

    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, VisibleWeaponCount - 1);
        if (!IsUnlocked(index)) return;
        equippedIndex = index;
        ApplyCursor();
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }
}
