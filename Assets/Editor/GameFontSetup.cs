using UnityEditor;
using UnityEngine;
using TMPro;

// Tools ▸ Break The World ▸ 폰트 세팅 : Assets/8_Font/Resources/MonaS12.ttf 로 TMP 폰트 애셋을 만들고
// TMP 기본 폰트로 지정한다. UI 툴킷 쪽은 코드에서 GameFonts.Ui를 쓰므로 이 메뉴만 한 번 돌리면 됨.
public static class GameFontSetup
{
    private const string SdfPath = "Assets/8_Font/Resources/MonaS12 SDF.asset";

    [MenuItem("Tools/Break The World/폰트 세팅 (MonaS12)")]
    public static void Setup()
    {
        var font = Resources.Load<Font>("MonaS12");
        if (font == null) { Debug.LogError("Resources/MonaS12 (Assets/8_Font/Resources/MonaS12.ttf) 를 못 찾음"); return; }

        var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
        if (sdf == null)
        {
            sdf = TMP_FontAsset.CreateFontAsset(font); // 동적(atlas 자동 확장) SDF 폰트 애셋
            sdf.name = "MonaS12 SDF";
            AssetDatabase.CreateAsset(sdf, SdfPath);
            if (sdf.material != null) AssetDatabase.AddObjectToAsset(sdf.material, sdf);
            if (sdf.atlasTextures != null && sdf.atlasTextures.Length > 0 && sdf.atlasTextures[0] != null)
                AssetDatabase.AddObjectToAsset(sdf.atlasTextures[0], sdf);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SdfPath);
            Debug.Log("TMP 폰트 애셋 생성: " + SdfPath);
        }

        var settings = TMP_Settings.instance;
        var so = new SerializedObject(settings);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = sdf;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);

        // 이미 씬/프리팹에 박혀 있는 TMP 텍스트들도 이 폰트로 (material까지 알아서 따라감)
        int changed = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == sdf) continue;
            t.font = sdf;
            EditorUtility.SetDirty(t);
            changed++;
        }
        if (changed > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(p);
            bool touched = false;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font == sdf) continue;
                t.font = sdf;
                touched = true;
            }
            if (touched) PrefabUtility.SaveAsPrefabAsset(root, p);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"TMP 기본 폰트 = MonaS12 SDF 완료 (씬 TMP {changed}개 교체)");
    }
}
