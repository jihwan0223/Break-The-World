using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ZoneUnlockEffect : MonoBehaviour
{
    [SerializeField] private float unlockDelay = 0.2f; // 초
    [SerializeField] private float flashDuration = 0.6f; // 초
    [SerializeField] private float shakeDuration = 0.5f; // 초
    [SerializeField] private float shakeAmount = 0.3f; // 월드 유닛
    [SerializeField] private float bannerDelay = 0.15f; // 초
    [SerializeField] private float bannerFadeIn = 0.3f; // 초
    [SerializeField] private float bannerHold = 2f; // 초
    [SerializeField] private float bannerFadeOut = 0.5f; // 초

    private bool _playing;

    public void Play(int zone)
    {
        if (!_playing)
            StartCoroutine(Run(zone));
    }

    private IEnumerator Run(int zone)
    {
        _playing = true;
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        VisualElement flash = new VisualElement();
        flash.pickingMode = PickingMode.Ignore;
        flash.style.position = Position.Absolute;
        flash.style.left = 0;
        flash.style.right = 0;
        flash.style.top = 0;
        flash.style.bottom = 0;
        flash.style.backgroundColor = Color.white;

        VisualElement banner = new VisualElement();
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

        CameraShake.Shake(shakeAmount, shakeDuration);
        float bannerEnd = bannerDelay + bannerFadeIn + bannerHold + bannerFadeOut;
        bool unlocked = false;

        for (float t = 0f; t < bannerEnd; t += Time.unscaledDeltaTime)
        {
            flash.style.opacity = 1f - Mathf.Clamp01(t / flashDuration);

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

        root.Remove(flash);
        root.Remove(banner);
        _playing = false;
    }

    private static Label CreateLabel(string text, int fontSize)
    {
        Label label = new Label(text);
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
