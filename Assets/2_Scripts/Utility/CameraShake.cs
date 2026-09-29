using UnityEngine;

// 카메라 흔들기. 여러 곳에서 동시에/연달아 불러도 흔들기 전 위치를 한 번만 기억해서 카메라가 밀려나지 않고,
// 흔드는 동안 흔들림 세기만큼 살짝 확대해서 배경 밖(스카이박스)이 안 보이게 함. 처음 부를 때 메인 카메라에 알아서 붙음
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    private Camera _camera; // 흔들 카메라
    private Vector3 _home; // 흔들기 전 카메라 위치
    private float _homeSize; // 흔들기 전 카메라 크기(orthographicSize)
    private float _amount; // 이번 흔들림의 최대 세기(월드 유닛)
    private float _duration; // 이번 흔들림 총 시간(초)
    private float _remaining; // 남은 흔들림 시간(초)
    private bool _shaking; // 지금 흔드는 중인지

    // 메인 카메라를 amount 세기로 duration초 동안 흔듦 (갈수록 약해짐). 흔드는 중에 또 부르면 처음부터 다시 흔듦
    public static void Shake(float amount, float duration)
    {
        Camera cam = Camera.main; // 흔들 카메라
        if (cam == null || amount <= 0f || duration <= 0f) return;

        CameraShake shake = cam.GetComponent<CameraShake>(); // 카메라에 붙은 흔들기
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

        _amount = Mathf.Max(amount, CurrentStrength); // 더 약한 흔들림이 이어져도 지금 세기보다 갑자기 약해지지 않게
        _duration = duration;
        _remaining = duration;
    }

    private float CurrentStrength => _shaking && _duration > 0f ? _amount * (_remaining / _duration) : 0f; // 지금 흔들림 세기

    void LateUpdate()
    {
        if (!_shaking) return;

        _remaining -= Time.unscaledDeltaTime; // timeScale이 0이어도 끝까지 흔들고 원래대로 돌아오게
        if (_remaining <= 0f)
        {
            transform.position = _home;
            _camera.orthographicSize = _homeSize;
            _shaking = false;
            return;
        }

        float strength = CurrentStrength; // 이번 프레임 흔들림 세기
        transform.position = _home + (Vector3)(Random.insideUnitCircle * strength);
        _camera.orthographicSize = _homeSize - strength; // 흔들린 거리만큼 확대 - 세로는 딱 그만큼, 가로는 화면비만큼 더 여유가 생김
    }
}
