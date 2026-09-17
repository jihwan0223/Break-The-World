using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

// Feel(MMFeedbacks)의 MMF_Player를 런타임에 만들어 붙여서, 버튼 클릭시 살짝 커졌다 돌아오는 펀치 연출을 재생함
[RequireComponent(typeof(Button))]
public class ButtonClickFeedback : MonoBehaviour
{
    [SerializeField] private float punchAmount = 0.15f; // 최대로 커지는 비율 (1 + 이 값 배로 커짐)
    [SerializeField] private float duration = 0.15f; // 펀치 애니메이션 길이(초)

    private Button _button; // onClick을 구독할 대상 버튼
    private MMF_Player _player; // 런타임에 만들어 붙이는 Feel 피드백 플레이어

    void Awake()
    {
        _button = GetComponent<Button>();

        _player = gameObject.AddComponent<MMF_Player>();
        var scale = new MMF_Scale
        {
            AnimateScaleTarget = transform,
            AnimateScaleDuration = duration,
            RemapCurveZero = 1f,
            RemapCurveOne = 1f + punchAmount
        };
        _player.FeedbacksList.Add(scale);
        _player.Initialization();
    }

    void OnEnable() => _button.onClick.AddListener(Play);
    void OnDisable() => _button.onClick.RemoveListener(Play);

    private void Play() => _player.PlayFeedbacks();
}
