using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// 책상을 처음 부술 때 나오는 "새 지역 해금" 연출: 흰 화면 번쩍임 + 카메라 흔들림 + 안내 문구.
// 번쩍임이 가라앉기 시작할 때 존을 해금해서 화살표가 연출과 함께 튀어나오게 한다.
// ZoneArrowsUI와 같은 GameObject(UIDocument 공용)에 붙이고, 오버레이는 재생할 때만 만들었다가 지움.
[RequireComponent(typeof(UIDocument))]
public class ZoneUnlockEffect : MonoBehaviour
{
    [SerializeField] private float unlockDelay = 0.2f; // 번쩍임 시작 후 존을 해금(화살표 등장)하기까지 걸리는 시간(초)
    [SerializeField] private float flashDuration = 0.6f; // 흰 화면이 사라지는 데 걸리는 시간(초)
    [SerializeField] private float shakeDuration = 0.5f; // 카메라 흔들림 시간(초)
    [SerializeField] private float shakeAmount = 0.3f; // 카메라 흔들림 최대 세기(월드 유닛, 갈수록 약해짐)
    [SerializeField] private float bannerDelay = 0.15f; // 안내 문구가 나타나기 시작하는 시점(초)
    [SerializeField] private float bannerFadeIn = 0.3f; // 안내 문구가 서서히 나타나는 시간(초)
    [SerializeField] private float bannerHold = 2f; // 안내 문구가 완전히 보인 채로 머무는 시간(초)
    [SerializeField] private float bannerFadeOut = 0.5f; // 안내 문구가 서서히 사라지는 시간(초)

    private bool _playing; // 재생 중 중복 호출 방지

    // zone 존을 해금하면서 연출 재생 (이미 재생 중이면 무시)
    public void Play(int zone)
    {
        if (!_playing)
            StartCoroutine(Run(zone));
    }

    private IEnumerator Run(int zone)
    {
        _playing = true;
        VisualElement root = GetComponent<UIDocument>().rootVisualElement; // 오버레이를 붙일 UI 루트

        VisualElement flash = new VisualElement(); // 화면 전체를 덮는 흰 막
        flash.pickingMode = PickingMode.Ignore;
        flash.style.position = Position.Absolute;
        flash.style.left = 0;
        flash.style.right = 0;
        flash.style.top = 0;
        flash.style.bottom = 0;
        flash.style.backgroundColor = Color.white;

        VisualElement banner = new VisualElement(); // 화면 위쪽에 뜨는 안내 문구 묶음
        banner.pickingMode = PickingMode.Ignore;
        banner.style.position = Position.Absolute;
        banner.style.left = 0;
        banner.style.right = 0;
        banner.style.top = Length.Percent(18);
        banner.style.alignItems = Align.Center;
        banner.style.opacity = 0f;
        banner.Add(CreateLabel("새로운 지역 해금!", 64));
        banner.Add(CreateLabel("화살표로 이동할 수 있어요", 30));

        root.Add(flash);
        root.Add(banner);

        Camera cam = Camera.main; // 흔들 카메라 (카메라는 안 움직이는 고정 시점이라 원래 위치로 그대로 되돌리면 됨)
        Vector3 camHome = cam != null ? cam.transform.position : Vector3.zero;
        float bannerEnd = bannerDelay + bannerFadeIn + bannerHold + bannerFadeOut; // 연출이 전부 끝나는 시점
        bool unlocked = false; // 존 해금을 이미 했는지

        // timeScale이 바뀌어도 연출이 안 늘어지도록 실제 시간(unscaled)으로 진행
        for (float t = 0f; t < bannerEnd; t += Time.unscaledDeltaTime)
        {
            flash.style.opacity = 1f - Mathf.Clamp01(t / flashDuration);

            if (cam != null)
                cam.transform.position = camHome + (t < shakeDuration
                    ? (Vector3)(Random.insideUnitCircle * shakeAmount * (1f - t / shakeDuration))
                    : Vector3.zero);

            if (!unlocked && t >= unlockDelay)
            {
                unlocked = true;
                if (ZoneManager.Instance != null) ZoneManager.Instance.UnlockZone(zone);
            }

            banner.style.opacity = Mathf.Min(
                Mathf.Clamp01((t - bannerDelay) / bannerFadeIn),
                Mathf.Clamp01((bannerEnd - t) / bannerFadeOut));

            yield return null;
        }

        if (cam != null) cam.transform.position = camHome;
        root.Remove(flash);
        root.Remove(banner);
        _playing = false;
    }

    // 반투명 검은 바탕 위의 흰 글씨 한 줄 (밝은 화면 위에서도 읽히게)
    private static Label CreateLabel(string text, int fontSize)
    {
        Label label = new Label(text); // 만들어서 돌려줄 라벨
        label.pickingMode = PickingMode.Ignore;
        label.style.fontSize = fontSize;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = Color.white;
        label.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        label.style.paddingLeft = 24;
        label.style.paddingRight = 24;
        label.style.paddingTop = 8;
        label.style.paddingBottom = 8;
        label.style.marginBottom = 6;
        return label;
    }
}
