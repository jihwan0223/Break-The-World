using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class NodeIconGenerator
{
    const string OutDir = "Assets/3_Image/NodeImage/Auto";
    const string BackupPath = OutDir + "/original_sprites.txt";
    const int Size = 32; // px
    static readonly RectInt BadgeArea = new RectInt(18, 2, 13, 14); // 위에서부터 센 좌표
    static readonly RectInt ObjectBox = new RectInt(2, 10, 20, 20); // 위에서부터 센 좌표
    static readonly RectInt WideBox = new RectInt(2, 16, 28, 14);
    static readonly RectInt RiderBox = new RectInt(4, 6, 10, 10);
    const float StripAspect = 2f;
    static readonly string[] KeepOriginal = { };
    static readonly Dictionary<string, string[]> OnlyBadges = new Dictionary<string, string[]> { { "자동차", new[] { "AutoClick", "AutoSpeed", "AutoCount", "PlusOne" } } };
    static readonly Dictionary<string, string> ArtFromNode = new Dictionary<string, string>
    {
        { "가로등", "Assets/3_Image/NodeImage/Object/Object6_DropRate_X.png" },
        { "자동차", "Assets/3_Image/NodeImage/Object/Object9_DropRate_X.png" },
    };
    static readonly string[] CrispObjects = { "자전거" };
    static readonly RectInt CrispBox = new RectInt(2, 15, 26, 15);
    const int CrispScale = 2;
    static readonly string[] StripObjects = { "도로", "컨베이어 벨트" };
    const string BeltName = "컨베이어 벨트";
    const string BeltRider = "왁뿌볼";
    static readonly Color32 BorderX = new Color32(255, 255, 255, 255);
    static readonly Color32 BorderO = new Color32(0, 255, 0, 255);
    static readonly Color32 Inside = new Color32(0, 0, 0, 255);

    const string BadgeDir = "Assets/Editor/NodeIconBadges";
    static readonly Dictionary<string, string> BadgeSources = new Dictionary<string, string>
    {
        { "DropRate", BadgeDir + "/DropRate.png" },
        { "DoubleDrop", BadgeDir + "/DoubleDrop.png" },
        { "AutoClick", BadgeDir + "/AutoClick.png" },
        { "UnLock", BadgeDir + "/UnLock.png" },
    };
    const float BasePixelsPerUnit = 100f; // 32px 노드 그림의 PPU

    [MenuItem("Tools/Upgrade Tree/Node Icons - Generate and Apply")]
    static void Apply()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[NodeIcon] 플레이 중엔 안 됨"); return; }
        var objectManager = Object.FindFirstObjectByType<ObjectManager>();
        if (objectManager == null) { Debug.LogError("[NodeIcon] ObjectManager 없음"); return; }
        var visuals = new SerializedObject(objectManager).FindProperty("objectVisuals");
        Directory.CreateDirectory(OutDir);

        var badges = new Dictionary<string, Color32[]>();
        foreach (var pair in BadgeSources) badges[pair.Key] = CutBadge(pair.Value);
        badges["PlusOne"] = PlusOneBadge();
        badges["AutoSpeed"] = BoltBadge();
        badges["AutoCount"] = DoubleCursorBadge();

        var targets = new List<(Component node, int objectIndex, string badge)>();
        foreach (var node in Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string badge = BadgeFor(node.Effect, out bool handled);
            int index = ObjectManager.StaticIndexOfName(node.TargetObjectName);
            if (handled && index >= 0) targets.Add((node, index, badge));
        }
        foreach (var node in Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(node);
            targets.Add((node, so.FindProperty("objectIndex").intValue, so.FindProperty("isGain").boolValue ? "DropRate" : "UnLock"));
        }

        // 처음 적용할 때만 기록 (덮어쓰면 복구 불가)
        if (!File.Exists(BackupPath))
        {
            var lines = new List<string>();
            foreach (var t in targets)
                lines.Add($"{t.node.name}\t{AssetDatabase.GetAssetPath(t.node.GetComponent<Image>().sprite)}\t{AssetDatabase.GetAssetPath(new SerializedObject(t.node).FindProperty("upgradedSprite").objectReferenceValue)}");
            File.WriteAllLines(BackupPath, lines);
        }
        var original = ReadBackup();

        var made = new HashSet<string>();
        var paths = new HashSet<string>();
        foreach (var t in targets)
        {
            string name = ObjectManager.StaticObjectNameAt(t.objectIndex);
            string key = $"{t.objectIndex + 1}_{t.badge ?? "Plain"}";
            if (KeepsOriginal(name, t.badge) || !made.Add(key)) continue;
            var layers = new List<(Color32[] px, RectInt box)>();
            int scale = 1;
            if (ArtFromNode.TryGetValue(name, out string artPath))
                layers.Add((CutNodeArt(artPath), new RectInt(0, 0, Size, Size)));
            else
            {
                Sprite source = SourceSprite(visuals, t.objectIndex);
                if (source == null) { Debug.LogWarning($"[NodeIcon] {name} 그림 없음"); continue; }
                bool strip = System.Array.IndexOf(StripObjects, name) >= 0;
                bool crisp = System.Array.IndexOf(CrispObjects, name) >= 0;
                RectInt box = strip ? WideBox : crisp ? CrispBox : ObjectBox;
                if (crisp) { scale = CrispScale; box = new RectInt(box.x * scale, box.y * scale, box.width * scale, box.height * scale); }
                layers.Add((FitObject(source, box, strip ? StripAspect : 0f, crisp), box));
            }
            if (name == BeltName)
            {
                Sprite rider = SourceSprite(visuals, ObjectManager.StaticIndexOfName(BeltRider));
                if (rider != null) layers.Add((FitObject(rider, RiderBox, 0f, false), RiderBox));
            }
            Color32[] badgePixels = t.badge != null ? badges[t.badge] : null;
            paths.Add(WritePng($"{OutDir}/{key}_X.png", Compose(BorderX, layers, badgePixels, scale), Size * scale));
            paths.Add(WritePng($"{OutDir}/{key}_O.png", Compose(BorderO, layers, badgePixels, scale), Size * scale));
        }

        foreach (string file in Directory.GetFiles(OutDir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            if (!paths.Contains(path)) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.Refresh();
        foreach (string path in paths) SetupImporter(path);

        int kept = 0;
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
            string key = $"{t.objectIndex + 1}_{t.badge ?? "Plain"}";
            var x = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutDir}/{key}_X.png");
            var o = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutDir}/{key}_O.png");
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
        var original = ReadBackup();
        var nodes = new List<Component>();
        nodes.AddRange(Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        nodes.AddRange(Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        int restored = 0;
        foreach (var node in nodes)
        {
            if (!original.TryGetValue(node.name, out var p)) continue;
            SetSprites(node, AssetDatabase.LoadAssetAtPath<Sprite>(p.baseSprite), AssetDatabase.LoadAssetAtPath<Sprite>(p.upgraded));
            restored++;
        }
        AssetDatabase.DeleteAsset(OutDir);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[NodeIcon] 노드 {restored}개 원래 그림으로 복구 (씬 저장 필요)");
    }

    static bool KeepsOriginal(string name, string badge) =>
        System.Array.IndexOf(KeepOriginal, name) >= 0
        || (OnlyBadges.TryGetValue(name, out string[] only) && System.Array.IndexOf(only, badge) < 0);

    static Color32[] CutNodeArt(string path)
    {
        Color32[] src = LoadTopDown(path, out int w, out int h);
        var result = new Color32[Size * Size];
        for (int y = 1; y < Mathf.Min(h, Size) - 1; y++)
            for (int x = 1; x < Mathf.Min(w, Size) - 1; x++)
            {
                Color32 c = src[y * w + x];
                bool background = c.a == 0 || (c.r < 8 && c.g < 8 && c.b < 8);
                if (background || BadgeArea.Contains(new Vector2Int(x, y))) continue;
                result[y * Size + x] = c;
            }
        return result;
    }

    static Dictionary<string, (string baseSprite, string upgraded)> ReadBackup()
    {
        var result = new Dictionary<string, (string baseSprite, string upgraded)>();
        if (!File.Exists(BackupPath)) return result;
        foreach (string line in File.ReadAllLines(BackupPath))
        {
            string[] cols = line.Split('\t');
            if (cols.Length >= 3) result[cols[0]] = (cols[1], cols[2]);
        }
        return result;
    }

    static void SetSprites(Component node, Sprite baseSprite, Sprite upgraded)
    {
        var image = new SerializedObject(node.GetComponent<Image>());
        image.FindProperty("m_Sprite").objectReferenceValue = baseSprite;
        image.ApplyModifiedProperties();
        var so = new SerializedObject(node);
        so.FindProperty("upgradedSprite").objectReferenceValue = upgraded;
        so.ApplyModifiedProperties();
    }

    static Sprite SourceSprite(SerializedProperty visuals, int index)
    {
        if (index < 0 || index >= visuals.arraySize) return null;
        var stages = visuals.GetArrayElementAtIndex(index).FindPropertyRelative("healthStages");
        return stages.arraySize > 0 ? stages.GetArrayElementAtIndex(0).objectReferenceValue as Sprite : null;
    }

    static string BadgeFor(UpgradeManager.UpgradeEffect effect, out bool handled)
    {
        handled = true;
        switch (effect)
        {
            case UpgradeManager.UpgradeEffect.PieceGainObject: return "DropRate";
            case UpgradeManager.UpgradeEffect.ObjectDoubleDrop: return "DoubleDrop";
            case UpgradeManager.UpgradeEffect.AutoClickUnlock: return "AutoClick";
            case UpgradeManager.UpgradeEffect.AutoClickSpeed: return "AutoSpeed";
            case UpgradeManager.UpgradeEffect.AutoClickCount: return "AutoCount";
            case UpgradeManager.UpgradeEffect.ObjectExtraCopies: return "PlusOne";
            default: handled = false; return null;
        }
    }

    static Color32[] CutBadge(string path)
    {
        Color32[] src = LoadTopDown(path, out int w, out _);
        var result = new Color32[BadgeArea.width * BadgeArea.height];
        for (int y = 0; y < BadgeArea.height; y++)
            for (int x = 0; x < BadgeArea.width; x++)
            {
                Color32 c = src[(BadgeArea.y + y) * w + BadgeArea.x + x];
                bool background = c.a == 0 || (c.r < 8 && c.g < 8 && c.b < 8);
                result[y * BadgeArea.width + x] = background ? new Color32(0, 0, 0, 0) : c;
            }
        return result;
    }

    static Color32[] FitObject(Sprite sprite, RectInt box, float cropAspect, bool crisp)
    {
        const int CrispHighlightPercent = 25;
        Color32[] tex = LoadTopDown(AssetDatabase.GetAssetPath(sprite.texture), out int tw, out int th);
        float fx = (float)tw / sprite.texture.width, fy = (float)th / sprite.texture.height; // 원본 파일 / 임포트 크기 비 (Max Size로 줄었을 때)
        Rect r = sprite.rect;
        int sx0 = Mathf.RoundToInt(r.x * fx), sw = Mathf.Min(tw - sx0, Mathf.RoundToInt(r.width * fx));
        int sh = Mathf.Min(th, Mathf.RoundToInt(r.height * fy));
        int sy0 = Mathf.Max(0, th - Mathf.RoundToInt(r.y * fy) - sh);
        int minX = sw, minY = sh, maxX = -1, maxY = -1;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (tex[(sy0 + y) * tw + sx0 + x].a > 100)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        var result = new Color32[box.width * box.height];
        if (maxX < 0) return result;
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        if (cropAspect > 0f) cw = Mathf.Min(cw, Mathf.RoundToInt(ch * cropAspect));
        float scale = Mathf.Min((float)box.width / cw, (float)box.height / ch);
        int dw = Mathf.Max(1, Mathf.RoundToInt(cw * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(ch * scale));
        int ox = (box.width - dw) / 2, oy = box.height - dh;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int ax = minX + x * cw / dw, bx = Mathf.Max(ax + 1, minX + (x + 1) * cw / dw);
                int ay = minY + y * ch / dh, by = Mathf.Max(ay + 1, minY + (y + 1) * ch / dh);
                long rs = 0, gs = 0, bs = 0; int solid = 0, total = 0;
                Color32 brightest = default; int brightestSum = -1;
                for (int yy = ay; yy < by; yy++)
                    for (int xx = ax; xx < bx; xx++)
                    {
                        Color32 c = tex[(sy0 + yy) * tw + sx0 + xx];
                        total++;
                        if (c.a <= 100) continue;
                        rs += c.r; gs += c.g; bs += c.b; solid++;
                        if (c.r + c.g + c.b > brightestSum) { brightestSum = c.r + c.g + c.b; brightest = c; }
                    }
                if (solid == 0 || (crisp ? solid * 10 < total * 3 : solid * 2 < total)) continue;
                int cr = (int)(rs / solid), cg = (int)(gs / solid), cb = (int)(bs / solid);
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

    static Color32[] Compose(Color32 border, List<(Color32[] px, RectInt box)> layers, Color32[] badgePixels, int scale)
    {
        int size = Size * scale;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = x < scale || y < scale || x >= size - scale || y >= size - scale ? border : Inside;
        foreach (var layer in layers) Paste(px, size, layer.px, layer.box, 1);
        if (badgePixels != null) Paste(px, size, badgePixels, BadgeArea, scale);
        return px;
    }

    static void Paste(Color32[] dst, int dstSize, Color32[] src, RectInt area, int scale)
    {
        for (int y = 0; y < area.height; y++)
            for (int x = 0; x < area.width; x++)
            {
                Color32 c = src[y * area.width + x];
                if (c.a == 0) continue;
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        dst[((area.y + y) * scale + sy) * dstSize + (area.x + x) * scale + sx] = c;
            }
    }

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

    static Color32[] DoubleCursorBadge()
    {
        string[] cursor =
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
        string[] back = System.Array.ConvertAll(cursor, row => row.Replace('w', 'b'));
        return DrawBadge((back, 1, 0), (cursor, 5, 3));
    }

    static Color32[] DrawBadge(params (string[] rows, int x, int y)[] layers)
    {
        var palette = new Dictionary<char, Color32>
        {
            { 'w', new Color32(255, 255, 255, 255) },
            { 'b', new Color32(0, 137, 251, 255) },
            { 'y', new Color32(255, 214, 64, 255) },
        };
        var result = new Color32[BadgeArea.width * BadgeArea.height];
        foreach (var layer in layers)
            for (int y = 0; y < layer.rows.Length; y++)
                for (int x = 0; x < layer.rows[y].Length; x++)
                    if (palette.TryGetValue(layer.rows[y][x], out Color32 c))
                        result[(layer.y + y) * BadgeArea.width + layer.x + x] = c;
        return result;
    }

    static Color32[] PlusOneBadge()
    {
        string[] glyph =
        {
            "..#....#.",
            "..#...##.",
            "#####..#.",
            "..#....#.",
            "..#...###",
        };
        var yellow = new Color32(255, 214, 64, 255);
        var result = new Color32[BadgeArea.width * BadgeArea.height];
        const int left = 2, top = 2;
        for (int y = 0; y < glyph.Length; y++)
            for (int x = 0; x < glyph[y].Length; x++)
                if (glyph[y][x] == '#') result[(top + y) * BadgeArea.width + left + x] = yellow;
        return result;
    }

    // Read/Write 설정과 무관하게 파일을 직접 읽음
    static Color32[] LoadTopDown(string path, out int w, out int h)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        w = tex.width; h = tex.height;
        Color32[] bottomUp = tex.GetPixels32(); // 유니티는 아래 줄부터
        Object.DestroyImmediate(tex);
        var result = new Color32[w * h];
        for (int y = 0; y < h; y++) System.Array.Copy(bottomUp, (h - 1 - y) * w, result, y * w, w);
        return result;
    }

    static string WritePng(string path, Color32[] topDown, int size)
    {
        var bottomUp = new Color32[size * size];
        for (int y = 0; y < size; y++) System.Array.Copy(topDown, y * size, bottomUp, (size - 1 - y) * size, size);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(bottomUp);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return path;
    }

    // Single / Point / 압축 없음, PPU는 기존 노드 그림과 같게
    static void SetupImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.GetSourceTextureWidthAndHeight(out int width, out _); // 2배 해상도면 PPU도 2배
        importer.spritePixelsPerUnit = BasePixelsPerUnit * width / Size;
        importer.SaveAndReimport();
    }
}
