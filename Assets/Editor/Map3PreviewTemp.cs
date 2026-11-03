using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 임시: 맵3 새 배치(오브젝트별 4개 + 박스) 미리보기를 PNG로 저장, 씬은 원래대로
public static class Map3PreviewTemp
{
    // 이름, 스프라이트 원본 오브젝트(없으면 박스 그림), 위치, 크기
    static readonly (string kind, float x, float y, float scale)[] Layout =
    {
        ("트럭", 5.35f, 0.47f, -0.17f), ("트럭", -4.78f, -3.36f, 0.17f), ("트럭", 4.45f, -2.57f, -0.17f), ("트럭", -4.28f, 2.22f, 0.17f), ("간판", -7.26f, -0.28f, 0.19f), ("간판", -2.97f, 0.6f, 0.19f), ("간판", 7.23f, -0.56f, 0.19f), ("간판", 7.09f, 2.72f, 0.19f),
        ("박스", -7.77f, 1.03f, 0.3f), ("박스", 7.64f, -3.21f, 0.3f), ("박스", 2.99f, 1.43f, 0.3f), ("박스", -7.46f, -3.19f, 0.3f), ("왁뿌볼", 2.99f, -0.92f, 0.34f), ("왁뿌볼", -2.4f, -2.49f, 0.34f), ("왁뿌볼", -5.1f, -0.51f, 0.34f), ("왁뿌볼", 4.8f, 1.87f, 0.34f),
    };

    [MenuItem("Tools/Temp/Map3 Preview")]
    public static void Shot()
    {
        Scene scene = SceneManager.GetActiveScene(); // 메인 씬
        var rootStates = new Dictionary<GameObject, bool>(); // 맵 루트 원래 켜짐 상태
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith("Map")) continue;
            rootStates[root] = root.activeSelf;
            root.SetActive(root.name == "Map3");
        }

        Transform map3 = GameObject.Find("Map3").transform; // 맵3 루트
        var sprites = new Dictionary<string, SpriteRenderer>(); // 종류별 원본 렌더러
        var hidden = new List<GameObject>(); // 잠깐 끈 기존 오브젝트
        foreach (Transform child in map3)
        {
            if (child.name.StartsWith("Map3_컨베이어") || child.name.StartsWith("Map3_왁뿌볼") || child.name.StartsWith("Map3_간판") || child.name.StartsWith("Map3_트럭"))
            {
                string kind = child.name.Replace("Map3_", "").Split('_')[0]; // 종류 이름
                if (!sprites.ContainsKey(kind)) sprites[kind] = child.GetComponent<SpriteRenderer>();
                if (child.gameObject.activeSelf) { child.gameObject.SetActive(false); hidden.Add(child.gameObject); }
            }
        }

        Sprite box = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3_Image/ObjectImage/Box.png"); // 박스 그림
        SpriteRenderer template = sprites["간판"]; // 정렬 순서 복사용
        var temps = new List<GameObject>(); // 미리보기용 임시 오브젝트
        foreach (var item in Layout)
        {
            var go = new GameObject("PreviewTemp"); // 임시 오브젝트
            go.transform.SetParent(map3, false);
            go.transform.localPosition = new Vector3(item.x, item.y, 0f);
            go.transform.localScale = new Vector3(item.scale, Mathf.Abs(item.scale), 1f);
            var sr = go.AddComponent<SpriteRenderer>(); // 그림
            sr.sprite = item.kind == "박스" ? box : sprites[item.kind].sprite;
            sr.sortingLayerID = template.sortingLayerID;
            sr.sortingOrder = template.sortingOrder;
            temps.Add(go);
        }

        Camera cam = Camera.main; // 게임 카메라
        int w = 1920, h = 1080; // 저장 크기
        var rt = new RenderTexture(w, h, 24); // 임시 렌더 대상
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); // 읽어올 그림
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Directory.CreateDirectory("Temp/Shots");
        File.WriteAllBytes("Temp/Shots/map3_preview.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        foreach (var go in temps) Object.DestroyImmediate(go);
        foreach (var go in hidden) go.SetActive(true);
        foreach (var pair in rootStates) pair.Key.SetActive(pair.Value);
        Debug.Log("Map3 preview saved");
    }
}
