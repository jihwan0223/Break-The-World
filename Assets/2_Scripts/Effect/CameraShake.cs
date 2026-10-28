using UnityEngine;

// 흔들기 전 위치를 한 번만 기억 - 겹쳐 불러도 카메라가 밀리지 않음
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    private Camera _camera;
    private Vector3 _home;
    private float _homeSize;
    private float _amount; // 월드 유닛
    private float _duration; // 초
    private float _remaining; // 초
    private bool _shaking;

    public static void Shake(float amount, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null || amount <= 0f || duration <= 0f || !GameSettings.ScreenShake) return;

        CameraShake shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.Begin(amount, duration);
    }

    private void Begin(float amount, float duration)
    {
        _camera = GetComponent<Camera>();
        if (!_shaking)
        {
            _home = transform.position;
            _homeSize = _camera.orthographicSize;
            _shaking = true;
        }

        _amount = Mathf.Max(amount, CurrentStrength);
        _duration = duration;
        _remaining = duration;
    }

    private float CurrentStrength => _shaking && _duration > 0f ? _amount * (_remaining / _duration) : 0f;

    void LateUpdate()
    {
        if (!_shaking) return;

        _remaining -= Time.unscaledDeltaTime; // timeScale 0에서도 끝까지
        if (_remaining <= 0f)
        {
            transform.position = _home;
            _camera.orthographicSize = _homeSize;
            _shaking = false;
            return;
        }

        float strength = CurrentStrength;
        transform.position = _home + (Vector3)(Random.insideUnitCircle * strength);
        _camera.orthographicSize = _homeSize - strength; // 흔들린 만큼 확대해 화면 밖이 안 보이게
    }
}
