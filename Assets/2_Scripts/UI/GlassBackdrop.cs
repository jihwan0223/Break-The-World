using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 창 배경을 반투명 유리처럼: 뒤의 게임 화면을 흐리게 실시간으로 보여줌
[RequireComponent(typeof(Image))]
public class GlassBackdrop : MonoBehaviour
{
    [SerializeField] private Color tint = new Color(0.32f, 0.36f, 0.48f, 1f); // 흐린 화면에 곱하는 색 (어둡게 할수록 덜 투명해 보임)
    [SerializeField, Range(2, 16)] private int downsample = 4; // 처음 렌더 해상도 = 화면 / 이 값
    [SerializeField, Range(0, 4)] private int blurSteps = 2; // 절반씩 줄이는 횟수 (많을수록 더 흐림)
    [SerializeField, Range(0f, 0.3f)] private float sheenAlpha = 0.08f; // 위쪽 유리 광택 세기

    private RawImage _view; // 흐린 화면을 띄우는 이미지
    private RenderTexture _source; // 카메라가 그리는 작은 화면
    private RenderTexture _blurred; // 줄여서 흐려진 최종 화면
    private readonly UniversalRenderPipeline.SingleCameraRequest _request = new UniversalRenderPipeline.SingleCameraRequest(); // 카메라 한 번 그리기 요청

    void Awake()
    {
        var panel = GetComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0f); // 안 보이게만, 레이캐스트는 유지 (스크롤 드래그용)

        _view = CreateChild("GlassView", 0).AddComponent<RawImage>();
        _view.raycastTarget = false;

        var sheen = CreateChild("GlassSheen", 1).AddComponent<RawImage>();
        sheen.raycastTarget = false;
        sheen.texture = SheenTexture();
        sheen.color = new Color(1f, 1f, 1f, sheenAlpha);
    }

    private GameObject CreateChild(string childName, int siblingIndex)
    {
        var go = new GameObject(childName, typeof(RectTransform)); // 패널 전체를 덮는 자식
        var rt = (RectTransform)go.transform; // 꽉 채우기용
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetSiblingIndex(siblingIndex);
        return go;
    }

    // 위는 밝고 아래로 사라지는 세로 그라데이션
    private static Texture2D SheenTexture()
    {
        const int height = 64; // 그라데이션 단계 수
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; // 1px 너비 세로 텍스처
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1); // 0 = 아래, 1 = 위
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(t, 3f)));
        }
        tex.Apply();
        return tex;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main; // 게임 화면 카메라
        if (cam == null) return;

        int w = Mathf.Max(1, Screen.width / downsample); // 작은 화면 너비
        int h = Mathf.Max(1, Screen.height / downsample); // 작은 화면 높이
        if (_source == null || _source.width != w || _source.height != h)
        {
            Release();
            _source = new RenderTexture(w, h, 16) { filterMode = FilterMode.Bilinear };
            // 알파 없는 포맷: 카메라 알파가 0이면 UI에서 투명해져 뒤가 그대로 비침
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.RGB565; // 최종 화면 포맷
            _blurred = new RenderTexture(Mathf.Max(1, w >> blurSteps), Mathf.Max(1, h >> blurSteps), 0, format) { filterMode = FilterMode.Bilinear };
        }

        _request.destination = _source;
        if (!RenderPipeline.SupportsRenderRequest(cam, _request)) return;
        RenderPipeline.SubmitRenderRequest(cam, _request);

        // 절반씩 줄여 가며 복사하면 싸게 흐려짐
        RenderTexture current = _source; // 지금 단계 화면
        for (int i = 1; i < blurSteps; i++)
        {
            var half = RenderTexture.GetTemporary(Mathf.Max(1, current.width / 2), Mathf.Max(1, current.height / 2)); // 다음 단계 화면
            half.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, half);
            if (current != _source) RenderTexture.ReleaseTemporary(current);
            current = half;
        }
        Graphics.Blit(current, _blurred);
        if (current != _source) RenderTexture.ReleaseTemporary(current);

        _view.texture = _blurred;
        _view.color = tint;
    }

    void OnDisable() => Release();

    private void Release()
    {
        if (_source != null) { _source.Release(); Destroy(_source); }
        if (_blurred != null) { _blurred.Release(); Destroy(_blurred); }
        _source = _blurred = null;
    }
}
