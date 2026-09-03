using UnityEngine;
using UnityEngine.UI;

// 부모/자식 노드를 잇는 선 하나. 위치/회전/길이는 자동 계산하지 않고 씬 뷰에서 손으로 직접 맞춰두면 됨 -
// 이 스크립트는 from/to 두 노드가 "둘 다 지금 보이는 상태인지"만 확인해서 이 선을 켜고 끄는 역할만 함.
// (from 노드가 아직 안 열려있거나, to 노드가 아직 안 열려있으면 선도 같이 숨김)
[RequireComponent(typeof(Image))]
public class UILineConnector : MonoBehaviour
{
    [SerializeField] private GameObject from; // 부모 노드
    [SerializeField] private GameObject to; // 자식 노드

    private Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false; // 선이 클릭을 가로채면 안 됨
    }

    void LateUpdate()
    {
        bool visible = from != null && to != null && from.activeInHierarchy && to.activeInHierarchy;

        if (_image.enabled != visible)
            _image.enabled = visible;
    }
}
