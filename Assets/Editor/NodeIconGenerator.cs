using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 오브젝트 대상 업그레이드 노드(드랍량/자동클릭/추가 배치/해금) 아이콘을 손으로 그리지 않고
// "틀 + 오브젝트 그림(ObjectManager 첫 체력 단계) + 효과 배지(기존 노드 그림 오른쪽 위에서 잘라옴)"로 만들어 노드에 넣는다.
// 오브젝트 그림을 바꾸거나 추가하면 생성 메뉴만 다시 누르면 됨. 되돌리기 메뉴로 원래 그림으로 복구 가능
public static class NodeIconGenerator
{
    const string OutDir = "Assets/3_Image/NodeImage/Auto"; // 생성 그림 폴더
    const string BackupPath = OutDir + "/original_sprites.txt"; // 원래 스프라이트 기록 (되돌리기용)
    const int Size = 32; // 노드 그림 크기(px)
    static readonly RectInt BadgeArea = new RectInt(18, 2, 13, 14); // 기존 그림에서 배지가 있는 영역 (위에서부터 센 좌표)
    static readonly RectInt ObjectBox = new RectInt(2, 10, 20, 20); // 오브젝트를 넣을 영역 (위에서부터 센 좌표)
    static readonly RectInt WideBox = new RectInt(2, 16, 28, 14); // 띠 오브젝트(도로/벨트)를 넣을 영역 - 배지 아래 가로 전체
    static readonly RectInt RiderBox = new RectInt(4, 6, 10, 10); // 벨트 위에 올릴 왁뿌볼 영역 (벨트 바로 위)
    const float StripAspect = 2f; // 띠 오브젝트는 왼쪽에서 높이의 이 배수만큼만 잘라 크게 보여줌
    static readonly string[] KeepOriginal = { }; // 자동 그림이 잘리거나 안 어울려서 원래 손그림 유지할 오브젝트 (간판/트럭/회사는 정면 그림으로 바뀌어 다시 자동 그림)
    // 이 배지 노드만 자동 그림, 나머지 노드는 원래 손그림 유지 (오브젝트 이름 -> 자동으로 만들 배지)
    static readonly Dictionary<string, string[]> OnlyBadges = new Dictionary<string, string[]> { { "자동차", new[] { "AutoClick", "AutoSpeed", "AutoCount" } } };
    // 오브젝트 그림 대신 기존 노드 손그림 속 오브젝트를 그대로 씀 (오브젝트 이름 -> 손그림 경로)
    static readonly Dictionary<string, string> ArtFromNode = new Dictionary<string, string>
    {
        { "가로등", "Assets/3_Image/NodeImage/Object/Object6_DropRate_X.png" },
        { "자동차", "Assets/3_Image/NodeImage/Object/Object9_DropRate_X.png" },
    };
    static readonly string[] CrispObjects = { "자전거" }; // 가는 선이 많아 줄이면 뭉개지는 오브젝트 - 가는 선/밝은 부분을 살려서 줄임
    static readonly RectInt CrispBox = new RectInt(2, 15, 26, 15); // 가는 선 오브젝트를 넣을 영역 (배지 아래 가로로 넓게, 32px 기준)
    const int CrispScale = 2; // 가는 선 오브젝트는 그림을 이 배수 해상도로 만듦 (테두리/배지는 그대로 키움)
    static readonly string[] StripObjects = { "도로", "컨베이어 벨트" }; // 가로로 긴 띠라 일부만 잘라 크게 넣는 오브젝트
    const string BeltName = "컨베이어 벨트"; // 도로와 구분하려고 위에 왁뿌볼을 올리는 오브젝트
    const string BeltRider = "왁뿌볼"; // 벨트 위에 올릴 오브젝트
    static readonly Color32 BorderX = new Color32(255, 255, 255, 255); // 미구매 테두리 색 (기존 오브젝트 노드 그림과 같음)
    static readonly Color32 BorderO = new Color32(0, 255, 0, 255); // 구매 후 테두리 색
    static readonly Color32 Inside = new Color32(0, 0, 0, 255); // 안쪽 바탕 색

    // 배지 종류 -> 잘라올 기존 그림 (옛 손그림 노드 그림을 Editor 폴더에 따로 보관 - 빌드엔 안 들어감)
    const string BadgeDir = "Assets/Editor/NodeIconBadges"; // 배지 원본 폴더
    static readonly Dictionary<string, string> BadgeSources = new Dictionary<string, string>
    {
        { "DropRate", BadgeDir + "/DropRate.png" },
        { "DoubleDrop", BadgeDir + "/DoubleDrop.png" },
        { "AutoClick", BadgeDir + "/AutoClick.png" },
        { "UnLock", BadgeDir + "/UnLock.png" },
    };
    const float BasePixelsPerUnit = 100f; // 32px 노드 그림의 PPU (기존 노드 그림과 같음)

    [MenuItem("Tools/Upgrade Tree/Node Icons - Generate and Apply")]
    static void Apply()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[NodeIcon] 플레이 중엔 안 됨"); return; }
        var objectManager = Object.FindFirstObjectByType<ObjectManager>(); // 오브젝트 그림 출처
        if (objectManager == null) { Debug.LogError("[NodeIcon] ObjectManager 없음"); return; }
        var visuals = new SerializedObject(objectManager).FindProperty("objectVisuals"); // 오브젝트별 체력 단계 그림
        Directory.CreateDirectory(OutDir);

        var badges = new Dictionary<string, Color32[]>(); // 배지 종류 -> 잘라낸 픽셀 (BadgeArea 크기, 위에서부터)
        foreach (var pair in BadgeSources) badges[pair.Key] = CutBadge(pair.Value);
        badges["PlusOne"] = PlusOneBadge();
        badges["AutoSpeed"] = BoltBadge();
        badges["AutoCount"] = DoubleCursorBadge();

        var targets = new List<(Component node, int objectIndex, string badge)>(); // 아이콘을 바꿀 노드
        foreach (var node in Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string badge = BadgeFor(node.Effect, out bool handled); // 효과에 맞는 배지 (null이면 배지 없음)
            int index = ObjectManager.StaticIndexOfName(node.TargetObjectName); // 대상 오브젝트
            if (handled && index >= 0) targets.Add((node, index, badge));
        }
        foreach (var node in Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(node); // 해금/획득 노드 값
            targets.Add((node, so.FindProperty("objectIndex").intValue, so.FindProperty("isGain").boolValue ? "DropRate" : "UnLock"));
        }

        // 처음 적용할 때만 원래 그림을 기록 (재생성 때 덮어쓰면 복구가 안 됨)
        if (!File.Exists(BackupPath))
        {
            var lines = new List<string>(); // 노드 이름 / 기본 그림 / 구매 후 그림
            foreach (var t in targets)
                lines.Add($"{t.node.name}\t{AssetDatabase.GetAssetPath(t.node.GetComponent<Image>().sprite)}\t{AssetDatabase.GetAssetPath(new SerializedObject(t.node).FindProperty("upgradedSprite").objectReferenceValue)}");
            File.WriteAllLines(BackupPath, lines);
        }
        var original = ReadBackup(); // 노드 이름 -> 원래 그림 경로

        var made = new HashSet<string>(); // 이미 만든 그림 이름 (같은 그림 재사용)
        var paths = new HashSet<string>(); // 이번에 쓴 png 경로 (임포트 설정 + 안 쓰는 옛 그림 정리용)
        foreach (var t in targets)
        {
            string name = ObjectManager.StaticObjectNameAt(t.objectIndex); // 대상 오브젝트 이름
            string key = $"{t.objectIndex + 1}_{t.badge ?? "Plain"}"; // 같은 오브젝트+배지면 같은 그림
            if (KeepsOriginal(name, t.badge) || !made.Add(key)) continue;
            var layers = new List<(Color32[] px, RectInt box)>(); // 그릴 그림들 (아래부터, box는 최종 해상도 좌표)
            int scale = 1; // 그림 해상도 배수 (가는 선 오브젝트만 2배로 더 촘촘하게)
            if (ArtFromNode.TryGetValue(name, out string artPath))
                layers.Add((CutNodeArt(artPath), new RectInt(0, 0, Size, Size)));
            else
            {
                Sprite source = SourceSprite(visuals, t.objectIndex); // 오브젝트 첫 체력 단계 그림
                if (source == null) { Debug.LogWarning($"[NodeIcon] {name} 그림 없음"); continue; }
                bool strip = System.Array.IndexOf(StripObjects, name) >= 0; // 가로로 긴 띠 오브젝트인지
                bool crisp = System.Array.IndexOf(CrispObjects, name) >= 0; // 가는 선 살려서 줄일 오브젝트인지
                RectInt box = strip ? WideBox : crisp ? CrispBox : ObjectBox; // 오브젝트를 넣을 영역
                if (crisp) { scale = CrispScale; box = new RectInt(box.x * scale, box.y * scale, box.width * scale, box.height * scale); }
                layers.Add((FitObject(source, box, strip ? StripAspect : 0f, crisp), box));
            }
            if (name == BeltName) // 도로와 구분되게 벨트 위에 왁뿌볼을 올림
            {
                Sprite rider = SourceSprite(visuals, ObjectManager.StaticIndexOfName(BeltRider)); // 벨트 위에 올릴 그림
                if (rider != null) layers.Add((FitObject(rider, RiderBox, 0f, false), RiderBox));
            }
            Color32[] badgePixels = t.badge != null ? badges[t.badge] : null; // 붙일 배지
            paths.Add(WritePng($"{OutDir}/{key}_X.png", Compose(BorderX, layers, badgePixels, scale), Size * scale));
            paths.Add(WritePng($"{OutDir}/{key}_O.png", Compose(BorderO, layers, badgePixels, scale), Size * scale));
        }

        foreach (string file in Directory.GetFiles(OutDir, "*.png")) // 이번에 안 만든 옛 그림은 지움 (원래 그림 유지로 바뀐 오브젝트 등)
        {
            string path = file.Replace('\\', '/'); // 에셋 경로 형식
            if (!paths.Contains(path)) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.Refresh();
        foreach (string path in paths) SetupImporter(path);

        int kept = 0; // 원래 그림으로 둔 노드 수
        foreach (var t in targets)
        {
            if (KeepsOriginal(ObjectManager.StaticObjectNameAt(t.objectIndex), t.badge))
            {
                if (original.TryGetValue(t.node.name, out var p))
                {
                    SetSprites(t.node, AssetDatabase.LoadAssetAtPath<Sprite>(p.baseSprite), AssetDatabase.LoadAssetAtPath<Sprite>(p.upgraded));
                    kept++;
                }
                continue;
            }
            string key = $"{t.objectIndex + 1}_{t.badge ?? "Plain"}"; // 이 노드가 쓸 그림
            var x = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutDir}/{key}_X.png"); // 미구매 그림
            var o = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutDir}/{key}_O.png"); // 구매 후 그림
            if (x != null && o != null) SetSprites(t.node, x, o);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[NodeIcon] 노드 {targets.Count - kept}개 자동 그림, {kept}개 원래 그림, 그림 {made.Count}종 (씬 저장 필요)");
    }

    [MenuItem("Tools/Upgrade Tree/Node Icons - Revert")]
    static void Revert()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[NodeIcon] 플레이 중엔 안 됨"); return; }
        if (!File.Exists(BackupPath)) { Debug.LogWarning("[NodeIcon] 되돌릴 기록 없음"); return; }
        var original = ReadBackup(); // 노드 이름 -> 원래 그림 경로
        var nodes = new List<Component>(); // 씬의 오브젝트 대상 노드 전부
        nodes.AddRange(Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        nodes.AddRange(Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        int restored = 0; // 복구한 노드 수
        foreach (var node in nodes)
        {
            if (!original.TryGetValue(node.name, out var p)) continue;
            SetSprites(node, AssetDatabase.LoadAssetAtPath<Sprite>(p.baseSprite), AssetDatabase.LoadAssetAtPath<Sprite>(p.upgraded));
            restored++;
        }
        AssetDatabase.DeleteAsset(OutDir); // 생성 그림 폴더도 지움
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[NodeIcon] 노드 {restored}개 원래 그림으로 복구 (씬 저장 필요)");
    }

    // 이 오브젝트의 이 배지 노드가 원래 손그림을 유지하는지
    static bool KeepsOriginal(string name, string badge) =>
        System.Array.IndexOf(KeepOriginal, name) >= 0
        || (OnlyBadges.TryGetValue(name, out string[] only) && System.Array.IndexOf(only, badge) < 0);

    // 기존 노드 손그림에서 오브젝트만 남김 (테두리/배지/검은 바탕은 투명). 32x32, 위에서부터
    static Color32[] CutNodeArt(string path)
    {
        Color32[] src = LoadTopDown(path, out int w, out int h); // 손그림 픽셀
        var result = new Color32[Size * Size]; // 오브젝트만 남긴 결과
        for (int y = 1; y < Mathf.Min(h, Size) - 1; y++)
            for (int x = 1; x < Mathf.Min(w, Size) - 1; x++)
            {
                Color32 c = src[y * w + x]; // 손그림 픽셀
                bool background = c.a == 0 || (c.r < 8 && c.g < 8 && c.b < 8); // 검은 바탕인지
                if (background || BadgeArea.Contains(new Vector2Int(x, y))) continue;
                result[y * Size + x] = c;
            }
        return result;
    }

    // 백업 파일 -> 노드 이름별 원래 그림 경로
    static Dictionary<string, (string baseSprite, string upgraded)> ReadBackup()
    {
        var result = new Dictionary<string, (string baseSprite, string upgraded)>(); // 노드 이름 -> (기본 그림, 구매 후 그림)
        if (!File.Exists(BackupPath)) return result;
        foreach (string line in File.ReadAllLines(BackupPath))
        {
            string[] cols = line.Split('\t'); // 노드 이름 / 기본 그림 / 구매 후 그림
            if (cols.Length >= 3) result[cols[0]] = (cols[1], cols[2]);
        }
        return result;
    }

    // 노드 기본 아이콘과 구매 후 아이콘을 바꿈
    static void SetSprites(Component node, Sprite baseSprite, Sprite upgraded)
    {
        var image = new SerializedObject(node.GetComponent<Image>()); // 노드 기본 아이콘
        image.FindProperty("m_Sprite").objectReferenceValue = baseSprite;
        image.ApplyModifiedProperties();
        var so = new SerializedObject(node); // upgradedSprite 필드
        so.FindProperty("upgradedSprite").objectReferenceValue = upgraded;
        so.ApplyModifiedProperties();
    }

    // index번째 오브젝트의 첫 체력 단계 그림 (없으면 null)
    static Sprite SourceSprite(SerializedProperty visuals, int index)
    {
        if (index < 0 || index >= visuals.arraySize) return null;
        var stages = visuals.GetArrayElementAtIndex(index).FindPropertyRelative("healthStages"); // 체력 단계 그림들
        return stages.arraySize > 0 ? stages.GetArrayElementAtIndex(0).objectReferenceValue as Sprite : null;
    }

    // 효과 -> 배지 종류. handled=false면 이 도구가 안 건드리는 노드
    static string BadgeFor(UpgradeManager.UpgradeEffect effect, out bool handled)
    {
        handled = true;
        switch (effect)
        {
            case UpgradeManager.UpgradeEffect.PieceGainObject: return "DropRate";
            case UpgradeManager.UpgradeEffect.ObjectDoubleDrop: return "DoubleDrop";
            case UpgradeManager.UpgradeEffect.AutoClickUnlock: return "AutoClick";
            case UpgradeManager.UpgradeEffect.AutoClickSpeed: return "AutoSpeed"; // 노란 번개
            case UpgradeManager.UpgradeEffect.AutoClickCount: return "AutoCount"; // 겹친 커서 2개
            case UpgradeManager.UpgradeEffect.ObjectExtraCopies: return "PlusOne";
            default: handled = false; return null;
        }
    }

    // 기존 노드 그림 오른쪽 위 배지만 잘라냄 (검은 바탕은 투명으로)
    static Color32[] CutBadge(string path)
    {
        Color32[] src = LoadTopDown(path, out int w, out _); // 원본 픽셀 (위에서부터)
        var result = new Color32[BadgeArea.width * BadgeArea.height]; // 잘라낸 배지
        for (int y = 0; y < BadgeArea.height; y++)
            for (int x = 0; x < BadgeArea.width; x++)
            {
                Color32 c = src[(BadgeArea.y + y) * w + BadgeArea.x + x]; // 원본 픽셀
                bool background = c.a == 0 || (c.r < 8 && c.g < 8 && c.b < 8); // 바탕(검정)인지
                result[y * BadgeArea.width + x] = background ? new Color32(0, 0, 0, 0) : c;
            }
        return result;
    }

    // 오브젝트 그림을 그려진 부분만 잘라 box에 비율 유지로 줄여 넣음 (칸마다 평균, 반 이상 차면 불투명).
    // cropAspect > 0이면 왼쪽에서 높이의 그 배수만큼만 잘라 씀 (가로로 긴 띠를 크게 보여주려고).
    // crisp면 30%만 차도 그리고, 칸의 가장 밝은 색을 조금 섞어서 가는 선(바퀴/살)이 안 사라지게 함
    static Color32[] FitObject(Sprite sprite, RectInt box, float cropAspect, bool crisp)
    {
        const int CrispHighlightPercent = 25; // crisp일 때 칸 평균 색에 가장 밝은 색을 섞는 비율(%) - 높을수록 하얘짐
        Color32[] tex = LoadTopDown(AssetDatabase.GetAssetPath(sprite.texture), out int tw, out int th); // 텍스처 전체 픽셀 (위에서부터)
        float fx = (float)tw / sprite.texture.width, fy = (float)th / sprite.texture.height; // 원본 파일 / 임포트 크기 비 (Max Size로 줄어든 큰 그림 보정)
        Rect r = sprite.rect; // 텍스처 안의 스프라이트 영역 (아래에서부터, 임포트 크기 기준)
        int sx0 = Mathf.RoundToInt(r.x * fx), sw = Mathf.Min(tw - sx0, Mathf.RoundToInt(r.width * fx)); // 원본 파일 기준 시작/너비
        int sh = Mathf.Min(th, Mathf.RoundToInt(r.height * fy)); // 원본 파일 기준 높이
        int sy0 = Mathf.Max(0, th - Mathf.RoundToInt(r.y * fy) - sh); // 위에서부터 센 시작 줄
        int minX = sw, minY = sh, maxX = -1, maxY = -1; // 그려진 부분 범위
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (tex[(sy0 + y) * tw + sx0 + x].a > 100)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        var result = new Color32[box.width * box.height]; // 줄인 결과 (위에서부터)
        if (maxX < 0) return result;
        int cw = maxX - minX + 1, ch = maxY - minY + 1; // 그려진 부분 크기
        if (cropAspect > 0f) cw = Mathf.Min(cw, Mathf.RoundToInt(ch * cropAspect)); // 띠는 왼쪽 일부만
        float scale = Mathf.Min((float)box.width / cw, (float)box.height / ch); // 줄이는 배율
        int dw = Mathf.Max(1, Mathf.RoundToInt(cw * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(ch * scale)); // 줄인 크기
        int ox = (box.width - dw) / 2, oy = box.height - dh; // 가로 가운데, 바닥 맞춤
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int ax = minX + x * cw / dw, bx = Mathf.Max(ax + 1, minX + (x + 1) * cw / dw); // 이 칸이 덮는 원본 가로 범위
                int ay = minY + y * ch / dh, by = Mathf.Max(ay + 1, minY + (y + 1) * ch / dh); // 이 칸이 덮는 원본 세로 범위
                long rs = 0, gs = 0, bs = 0; int solid = 0, total = 0; // 불투명 픽셀 색 합 / 개수 / 전체 개수
                Color32 brightest = default; int brightestSum = -1; // 칸에서 가장 밝은 불투명 픽셀 (crisp용)
                for (int yy = ay; yy < by; yy++)
                    for (int xx = ax; xx < bx; xx++)
                    {
                        Color32 c = tex[(sy0 + yy) * tw + sx0 + xx]; // 원본 픽셀
                        total++;
                        if (c.a <= 100) continue;
                        rs += c.r; gs += c.g; bs += c.b; solid++;
                        if (c.r + c.g + c.b > brightestSum) { brightestSum = c.r + c.g + c.b; brightest = c; }
                    }
                if (solid == 0 || (crisp ? solid * 10 < total * 3 : solid * 2 < total)) continue; // 덜 차면 투명
                int cr = (int)(rs / solid), cg = (int)(gs / solid), cb = (int)(bs / solid); // 칸 평균 색
                if (crisp)
                {
                    cr += (brightest.r - cr) * CrispHighlightPercent / 100;
                    cg += (brightest.g - cg) * CrispHighlightPercent / 100;
                    cb += (brightest.b - cb) * CrispHighlightPercent / 100;
                }
                result[(oy + y) * box.width + ox + x] = new Color32((byte)cr, (byte)cg, (byte)cb, 255);
            }
        return result;
    }

    // 틀 + 오브젝트 그림들 + 배지를 합친 (32*scale)x(32*scale) 픽셀 (위에서부터).
    // layers의 box는 최종 해상도 좌표, 테두리/배지는 32px 기준이라 scale배로 키워서 그림
    static Color32[] Compose(Color32 border, List<(Color32[] px, RectInt box)> layers, Color32[] badgePixels, int scale)
    {
        int size = Size * scale; // 결과 한 변 픽셀 수
        var px = new Color32[size * size]; // 결과 픽셀
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = x < scale || y < scale || x >= size - scale || y >= size - scale ? border : Inside;
        foreach (var layer in layers) Paste(px, size, layer.px, layer.box, 1);
        if (badgePixels != null) Paste(px, size, badgePixels, BadgeArea, scale);
        return px;
    }

    // 투명 아닌 픽셀만 area 위치에 덮어씀 (scale배로 키워서: 원본 1픽셀 -> scale x scale)
    static void Paste(Color32[] dst, int dstSize, Color32[] src, RectInt area, int scale)
    {
        for (int y = 0; y < area.height; y++)
            for (int x = 0; x < area.width; x++)
            {
                Color32 c = src[y * area.width + x]; // 붙일 픽셀
                if (c.a == 0) continue;
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        dst[((area.y + y) * scale + sy) * dstSize + (area.x + x) * scale + sx] = c;
            }
    }

    // 자동클릭 속도 강화용 노란 번개 배지
    static Color32[] BoltBadge() => DrawBadge((new[]
    {
        "....yyyy",
        "...yyyy.",
        "...yyy..",
        "..yyy...",
        ".yyyyyyy",
        "yyyyyyy.",
        "...yyy..",
        "..yyy...",
        "..yy....",
        ".yy.....",
        ".y......",
    }, 3, 1));

    // 자동클릭 횟수 강화용 겹친 커서 2개 배지 (뒤 파랑, 앞 흰색)
    static Color32[] DoubleCursorBadge()
    {
        string[] cursor = // 'w' = 칠할 칸
        {
            "w......",
            "ww.....",
            "www....",
            "wwww...",
            "wwwww..",
            "wwwwww.",
            "wwwwwww",
            "wwwww..",
            "ww.ww..",
            "w...ww.",
            ".....w.",
        };
        string[] back = System.Array.ConvertAll(cursor, row => row.Replace('w', 'b')); // 뒤 커서는 파랑
        return DrawBadge((back, 1, 0), (cursor, 5, 3));
    }

    // 글자 그림 여러 장을 BadgeArea 크기 배지로 (나중 것이 위). w=흰색, b=파랑, y=노랑, '.'=투명
    static Color32[] DrawBadge(params (string[] rows, int x, int y)[] layers)
    {
        var palette = new Dictionary<char, Color32> // 글자 -> 색 (기존 배지 색과 맞춤)
        {
            { 'w', new Color32(255, 255, 255, 255) },
            { 'b', new Color32(0, 137, 251, 255) },
            { 'y', new Color32(255, 214, 64, 255) },
        };
        var result = new Color32[BadgeArea.width * BadgeArea.height]; // 배지 픽셀 (위에서부터)
        foreach (var layer in layers)
            for (int y = 0; y < layer.rows.Length; y++)
                for (int x = 0; x < layer.rows[y].Length; x++)
                    if (palette.TryGetValue(layer.rows[y][x], out Color32 c))
                        result[(layer.y + y) * BadgeArea.width + layer.x + x] = c;
        return result;
    }

    // 오브젝트 추가 배치용 노란 "+1" 배지 (BadgeArea 크기, 위에서부터) - 기존 배지 그림이 없어서 코드로 그림
    static Color32[] PlusOneBadge()
    {
        string[] glyph = // '#' = 칠할 칸 ("+" 5칸 + 한 칸 띄고 "1" 3칸)
        {
            "..#....#.",
            "..#...##.",
            "#####..#.",
            "..#....#.",
            "..#...###",
        };
        var yellow = new Color32(255, 214, 64, 255); // 배지 색
        var result = new Color32[BadgeArea.width * BadgeArea.height]; // 배지 픽셀
        const int left = 2, top = 2; // BadgeArea 안에서 글자 시작 위치 (다른 배지와 비슷한 자리)
        for (int y = 0; y < glyph.Length; y++)
            for (int x = 0; x < glyph[y].Length; x++)
                if (glyph[y][x] == '#') result[(top + y) * BadgeArea.width + left + x] = yellow;
        return result;
    }

    // png 파일을 읽어 위에서부터 순서 픽셀로 (임포트 설정의 Read/Write와 무관하게 파일을 직접 읽음)
    static Color32[] LoadTopDown(string path, out int w, out int h)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false); // 읽기용 임시 텍스처
        tex.LoadImage(File.ReadAllBytes(path));
        w = tex.width; h = tex.height;
        Color32[] bottomUp = tex.GetPixels32(); // 유니티는 아래 줄부터
        Object.DestroyImmediate(tex);
        var result = new Color32[w * h]; // 위 줄부터로 뒤집은 결과
        for (int y = 0; y < h; y++) System.Array.Copy(bottomUp, (h - 1 - y) * w, result, y * w, w);
        return result;
    }

    // 위에서부터 픽셀을 png로 저장
    static string WritePng(string path, Color32[] topDown, int size)
    {
        var bottomUp = new Color32[size * size]; // 유니티 순서 (아래 줄부터)
        for (int y = 0; y < size; y++) System.Array.Copy(topDown, y * size, bottomUp, (size - 1 - y) * size, size);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); // 저장용 임시 텍스처
        tex.SetPixels32(bottomUp);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return path;
    }

    // 프로젝트 규칙: Single / Point / 압축 없음, PPU는 기존 노드 그림과 같게
    static void SetupImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); // 새 그림 임포터
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.GetSourceTextureWidthAndHeight(out int width, out _); // 2배 해상도 그림이면 PPU도 2배 (실제 크기는 같게)
        importer.spritePixelsPerUnit = BasePixelsPerUnit * width / Size;
        importer.SaveAndReimport();
    }
}
