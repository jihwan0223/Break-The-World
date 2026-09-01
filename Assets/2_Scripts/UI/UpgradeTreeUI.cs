using UnityEngine;
using UnityEngine.EventSystems;

// 업그레이드 화면 전체를 관리하는 총괄 매니저. 이 스크립트가 붙은 오브젝트(화면 전체를 덮는 패널)를
// 통째로 SetActive(false)로 꺼둔 채 시작하고, "업그레이드" 버튼을 누르면 SidePanelUI가 이 오브젝트를 직접
// SetActive(true)로 켬 - 그래서 열 때는 Instance(싱글톤)를 거치지 않음. Awake는 비활성 오브젝트에서는
// 절대 안 돌기 때문에, "업그레이드" 버튼이 Instance를 통해 열려고 했으면 첫 실행 때 Instance가 아직
// null이라 에러가 났을 것 - 그래서 여는 동작만큼은 SidePanelUI가 GameObject 참조로 직접 처리함.
// 반대로 닫기(X 버튼)는 이 화면이 이미 열려있는(=Awake가 이미 돈) 상태에서만 눌릴 수 있으니 Instance를 써도 안전함.
// - upgradeNodes/economyNodes에 손으로 배치해둔 노드를 전부 등록해두면, 구매가 일어날 때마다 한 번에 새로고침함
// - 팬(드래그)은 이 오브젝트에 같이 붙인 ScrollRect가 처리함 (Content만 연결하면 됨, Movement Type은
//   Unrestricted로 설정해서 이동 거리 제한이 없게 하고, Scroll Sensitivity는 0으로 꺼서 휠 스크롤과 안 겹치게 함)
// - 줌(마우스 휠)은 이 스크립트가 처리함 (이 오브젝트에 raycastTarget=true인 Image가 있어야 휠 입력을 받음)
[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeUI : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static UpgradeTreeUI Instance { get; private set; }

    // 이 화면이 열리면 true, 닫히면 false로 전달 - PieceUI(우상단 조각 표시)/HealthBarUI 등이 구독해서
    // 이 화면이 열려있는 동안은 스스로 숨어서 화면 안의 닫기(X) 버튼과 겹치지 않게 함
    public static event System.Action<bool> OnTreeToggled;

    [SerializeField] private UpgradeNodeUI[] upgradeNodes; // 씬에 배치한 메인 트리 노드 전부
    [SerializeField] private ObjectEconomyNodeUI[] economyNodes; // 씬에 배치한 오브젝트 해금/획득량 노드 전부

    [SerializeField] private RectTransform content; // 팬/줌이 실제로 적용되는 콘텐츠 (모든 노드/선의 부모, ScrollRect의 Content와 같은 걸 넣으면 됨)
    [SerializeField] private float minZoom = 0.5f; // 최소 축소 배율
    [SerializeField] private float maxZoom = 2.5f; // 최대 확대 배율
    [SerializeField] private float zoomStep = 0.1f; // 휠 한 틱당 줄어드는/늘어나는 "목표" 배율 감도
    [SerializeField] private float zoomSmoothSpeed = 12f; // 목표 배율을 따라잡는 속도 (클수록 빠르게/뚝뚝 끊기게, 작을수록 부드럽게)

    private float _zoomTarget = 1f; // 스크롤 입력이 가리키는 목표 배율 - 실제 적용은 Update()에서 매 프레임 이 값을 향해 부드럽게 보간됨
    private Vector2 _zoomFocalPoint; // 스크롤할 때의 커서 위치(Viewport 로컬 좌표) - 보간되는 동안 이 지점이 화면에서 안 움직이게 고정하는 기준점

    // "카메라 초기화" 버튼을 눌렀을 때 되돌아갈 기본 팬/줌 값 - Play 모드에서 원하는 위치로 맞춘 뒤
    // 그 값을 여기에 그대로 적어넣으면 됨 (content.anchoredPosition / localScale을 Inspector에서 확인 가능)
    [SerializeField] private Vector2 defaultAnchoredPosition;
    [SerializeField] private float defaultZoom = 1f;

    [SerializeField] private bool pauseGameWhileOpen = true; // 열려있는 동안 Time.timeScale을 0으로 멈출지

    void Awake()
    {
        // 씬에 UpgradeTreeUI가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private bool _hasOpenedBefore; // 맨 처음 한 번만 기본 카메라 위치로 맞추고, 그 뒤로는 유저가 옮겨둔 팬/줌을 유지하기 위한 플래그

    // SetActive(true)로 켜질 때마다(=열릴 때마다) 호출됨. Awake는 최초 1번만 돌지만 OnEnable은 켤 때마다 매번 돎
    void OnEnable()
    {
        // 맨 처음 여는 순간에만 기본 위치/배율로 맞춤 - 안 그러면 Content가 씬에 저장된(또는 마지막에 있던)
        // 위치 그대로 보여서, Default Anchored Position/Zoom을 설정해놔도 처음 열 때는 반영이 안 됨
        if (!_hasOpenedBefore)
        {
            ResetCamera();
            _hasOpenedBefore = true;
        }

        RefreshAll(); // 열 때마다 최신 레벨/조각 상태로 다시 그림

        if (pauseGameWhileOpen)
            Time.timeScale = 0f; // 업그레이드 보는 동안 자동클릭 등이 계속 진행되는 걸 막음

        OnTreeToggled?.Invoke(true); // PieceUI 등에게 알려서 우상단 조각 표시가 스스로 숨게 함
    }

    // SetActive(false)로 꺼질 때마다(=닫힐 때마다) 호출됨
    void OnDisable()
    {
        if (pauseGameWhileOpen)
            Time.timeScale = 1f; // 멈춰뒀던 게임 시간을 다시 정상 속도로

        UIPointerGuard.IsPointerOverUI = false; // 닫히는 순간 마우스가 다른 UI 위에 있지 않다면 다시 월드 클릭이 되도록

        OnTreeToggled?.Invoke(false); // PieceUI 등에게 알려서 우상단 조각 표시가 다시 보이게 함
    }

    // 화면 안의 닫기(X) 버튼 OnClick()에 연결 - 이 화면이 이미 열려있어야만 누를 수 있는 버튼이라 Instance가 항상 준비돼있음
    public void Close() => gameObject.SetActive(false);

    // 노드를 하나라도 새로 만들거나 지울 일은 거의 없으니, 구매 성공/실패와 무관하게 항상 전체를 다시 그림 -
    // 개수가 많지 않아서(최대 16 + 25쌍 정도) 매번 전체를 훑어도 부담 없음
    public void RefreshAll()
    {
        foreach (UpgradeNodeUI node in upgradeNodes)
            if (node != null) node.Refresh();

        foreach (ObjectEconomyNodeUI node in economyNodes)
            if (node != null) node.Refresh();
    }

    // 마우스 휠로 확대/축소 (ScrollRect의 자체 휠 스크롤은 꺼두고 이걸로 대체함).
    // 여기선 "목표" 값만 갱신하고, 실제 적용은 Update()에서 매 프레임 부드럽게 보간함 (뚝뚝 끊기지 않게)
    public void OnScroll(PointerEventData eventData)
    {
        if (content == null) return;

        // 스크롤할 때마다 커서 위치(Viewport 로컬 좌표)를 갱신해둠 - Update()에서 이 지점을 고정한 채로 줌을 적용함
        RectTransform viewport = content.parent as RectTransform;
        if (viewport != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out _zoomFocalPoint);

        float delta = eventData.scrollDelta.y * zoomStep;
        _zoomTarget = Mathf.Clamp(_zoomTarget + delta, minZoom, maxZoom);
    }

    void Update()
    {
        if (content == null) return;

        float currentScale = content.localScale.x;
        if (Mathf.Approximately(currentScale, _zoomTarget)) return;

        // 업그레이드 화면이 열려있는 동안 Time.timeScale이 0이라 deltaTime 대신 unscaledDeltaTime을 써야 줌 보간이 계속 동작함
        float newScale = Mathf.Lerp(currentScale, _zoomTarget, Time.unscaledDeltaTime * zoomSmoothSpeed);

        // 그냥 localScale만 바꾸면 Content의 피벗 기준으로 확대/축소되면서 노드 전체가 화면에서 쭉 밀려 보임
        // (피벗이 화면 중앙이나 커서 위치가 아니라서). 커서 아래의 지점이 확대/축소돼도 화면에서 안 움직이도록
        // 스크롤 시점에 저장해둔 커서 위치(_zoomFocalPoint)를 기준으로 anchoredPosition도 같이 보정함
        float ratio = newScale / currentScale;
        content.anchoredPosition = _zoomFocalPoint - (_zoomFocalPoint - content.anchoredPosition) * ratio;

        content.localScale = new Vector3(newScale, newScale, 1f);
    }

    // 업그레이드 트리 전체 초기화 버튼에 연결 (레벨 0, 공개 상태도 리셋 - 조각은 환불 안 됨)
    public void ResetTree()
    {
        UpgradeManager.Instance?.ResetAll();
        ObjectManager.Instance?.ResetAll();
        RefreshAll();
    }

    // "카메라 초기화" 버튼에 연결 - 팬/줌을 defaultAnchoredPosition/defaultZoom으로 되돌림
    public void ResetCamera()
    {
        if (content == null) return;

        content.anchoredPosition = defaultAnchoredPosition;
        content.localScale = new Vector3(defaultZoom, defaultZoom, 1f);
        _zoomTarget = defaultZoom; // 목표값도 같이 맞춰야 함 - 안 그러면 다음 프레임에 Update()가 예전 목표로 다시 보간해버림
    }

    // 열려있는 동안은 뒤쪽 월드 오브젝트(Click.cs)가 클릭되지 않도록 플래그를 켜고 끔
    public void OnPointerEnter(PointerEventData eventData) => UIPointerGuard.IsPointerOverUI = true;
    public void OnPointerExit(PointerEventData eventData) => UIPointerGuard.IsPointerOverUI = false;
}
