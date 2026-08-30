using System;
using UnityEngine;

// 콤보 업그레이드(UpgradeManager.ComboUnlock)를 산 뒤부터 동작하는 자동 콤보 타이머.
// 쿨타임이 지나면 일정 시간 동안 콤보가 활성화되고, 그동안은 Click.cs에서 파편 획득량을 2배로 처리함
public class ComboManager : MonoBehaviour
{
    // 씬 어디서든 ComboManager.Instance로 접근하기 위한 싱글톤
    public static ComboManager Instance { get; private set; }

    private const float ComboShardMultiplier = 2f; // 콤보 활성 중 파편 획득 배율

    private float _cooldownRemaining; // 다음 콤보 발동까지 남은 대기시간(초)
    private float _activeRemaining; // 지금 활성화된 콤보가 끝날 때까지 남은 시간(초)

    public bool IsComboActive => _activeRemaining > 0f;
    public float ShardMultiplier => IsComboActive ? ComboShardMultiplier : 1f;

    // 콤보가 켜지거나 꺼질 때(true/false) 전달 - 연출 등에 나중에 쓸 수 있게 미리 만들어둠
    public event Action<bool> OnComboStateChanged;

    void Awake()
    {
        // 씬에 ComboManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (UpgradeManager.Instance == null || !UpgradeManager.Instance.ComboIsUnlocked)
            return;

        if (_activeRemaining > 0f)
        {
            _activeRemaining -= Time.deltaTime;

            if (_activeRemaining <= 0f)
            {
                _activeRemaining = 0f;
                _cooldownRemaining = UpgradeManager.Instance.ComboCooldownSeconds;
                OnComboStateChanged?.Invoke(false);
            }

            return;
        }

        _cooldownRemaining -= Time.deltaTime;

        if (_cooldownRemaining <= 0f)
        {
            _activeRemaining = UpgradeManager.Instance.ComboDurationSeconds;
            OnComboStateChanged?.Invoke(true);
        }
    }
}
