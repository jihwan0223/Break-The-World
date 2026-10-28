using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickFeedback : MonoBehaviour
{
    [SerializeField] private float punchAmount = 0.15f;
    [SerializeField] private float duration = 0.15f; // 초

    private Button _button;
    private MMF_Player _player;

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
