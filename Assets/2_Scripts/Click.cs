using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
public class Click : MonoBehaviour
{
    private Collider2D _collider;
    private Health _health;

    void Start()
    {
        _collider = GetComponent<Collider2D>();
        _health = GetComponent<Health>();
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        if (_collider == Physics2D.OverlapPoint(worldPos))
        {
            Debug.Log("Click");
            _health.TakeDamage(1);
        }
    }
}
