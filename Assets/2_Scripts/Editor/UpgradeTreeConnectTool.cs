using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 씬 뷰에서 노드끼리 드래그해서 UpgradeTreeLink를 바로 만드는 툴.
// Tools > Upgrade Tree > Toggle Connect Mode (Alt+C)로 켜면, 모든 업그레이드/해금 노드 위에 작은 원 핸들이 뜸
// (연결된 노드=초록, 아직 아무 데도 안 이어진 노드=빨강 - 실제 UpgradeTreeLink 선은 에디터에서 안 뜰 때가 있어서
// 여기서 연결 상태를 항상 확실하게 보여줌). 기존 연결선도 흰 선으로 같이 그려줌.
// 원을 마우스로 누른 채 다른 노드까지 끌고 가서 놓으면 그 두 노드를 잇는 UpgradeTreeLink가 자동 생성됨
// (routing은 좌표가 일치하면 직선, 아니면 ㄱ자로 자동 선택). Undo(Ctrl+Z)로 되돌릴 수 있음.
[InitializeOnLoad]
public static class UpgradeTreeConnectTool
{
    private const string MenuPath = "Tools/Upgrade Tree/Toggle Connect Mode &c"; // &c = Alt+C 단축키
    private const string PrefsKey = "UpgradeTreeConnectTool.Enabled";
    private const float HandleScreenRadius = 8f; // 노드 핸들을 클릭으로 인식하는 반경(스크린 픽셀)

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefsKey, false);
        set => EditorPrefs.SetBool(PrefsKey, value);
    }

    private static RectTransform _dragSource; // 지금 드래그 시작한 노드 (없으면 null)

    static UpgradeTreeConnectTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    [MenuItem(MenuPath)]
    private static void ToggleEnabled() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    // 씬의 모든 연결 가능한 노드(RectTransform) - 업그레이드 노드 + 오브젝트 해금/획득 노드
    private static List<RectTransform> AllNodes()
    {
        var list = new List<RectTransform>();
        foreach (UpgradeNodeUI n in Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(n.Rect);
        foreach (ObjectEconomyNodeUI n in Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(n.Rect);
        return list;
    }

    private static readonly Color ConnectedColor = new Color(0.3f, 1f, 0.5f, 0.9f); // 연결(선행/자식) 있는 노드 핸들 색 - 초록
    private static readonly Color UnconnectedColor = new Color(1f, 0.25f, 0.25f, 0.9f); // 연결 하나도 없는 노드 핸들 색 - 빨강
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.6f); // 기존 연결선 미리보기 색

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!Enabled) return;

        Event e = Event.current;
        List<RectTransform> nodes = AllNodes();

        // UpgradeTreeLink는 [ExecuteAlways]라도 실제 UGUI 렌더(OnPopulateMesh)가 에디터에선 안정적으로 안 보일 때가 있어서
        // (플레이 모드에선 잘 뜨는데 에디터에선 타이밍 이슈로 안 뜨는 경우 있음) - 여기서 Handles로 직접 다시 그려서
        // "지금 뭐가 연결돼있는지"를 항상 확실하게 보여줌. 이건 UpgradeTreeLink의 실제 시각 표현과 별개(디버그 오버레이).
        UpgradeTreeLink[] links = Object.FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var connectedNodes = new HashSet<RectTransform>(); // 선행이든 자식이든 링크에 한 번이라도 등장하는 노드

        Handles.color = LineColor;
        foreach (UpgradeTreeLink link in links)
        {
            if (link.ToNode == null) continue;
            connectedNodes.Add(link.ToNode);
            foreach (RectTransform from in link.FromNodes)
            {
                if (from == null) continue;
                connectedNodes.Add(from);
                Handles.DrawLine(from.position, link.ToNode.position, 2f);
            }
        }

        // 핸들(작은 원) 그리기 - 연결 여부에 따라 초록/빨강
        foreach (RectTransform node in nodes)
        {
            if (node == null) continue;
            Vector3 worldPos = node.position;
            float size = HandleUtility.GetHandleSize(worldPos) * 0.15f;
            Handles.color = node == _dragSource ? Color.yellow : (connectedNodes.Contains(node) ? ConnectedColor : UnconnectedColor);
            Handles.SphereHandleCap(0, worldPos, Quaternion.identity, size, EventType.Repaint);
        }

        RectTransform hovered = FindNearestNode(nodes, e.mousePosition);

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0 && hovered != null:
                _dragSource = hovered;
                e.Use();
                break;

            case EventType.MouseDrag when _dragSource != null:
                sceneView.Repaint();
                break;

            case EventType.MouseUp when _dragSource != null:
                if (hovered != null && hovered != _dragSource)
                    CreateLink(_dragSource, hovered);
                _dragSource = null;
                e.Use();
                sceneView.Repaint(); // 새로 만든 링크의 흰 선/초록 핸들이 바로 반영되게
                break;

            case EventType.MouseDown when e.button == 1 && _dragSource != null: // 우클릭 = 드래그 취소
                _dragSource = null;
                e.Use();
                break;
        }

        if (_dragSource != null)
        {
            Vector3 endPoint = hovered != null ? hovered.position : GUIPointOnNodePlane(e.mousePosition, _dragSource);
            Handles.color = Color.white;
            Handles.DrawLine(_dragSource.position, endPoint, 3f);
            sceneView.Repaint();
        }
    }

    // 화면상 마우스 위치에서 가장 가까운 노드 (일정 반경 안에 있을 때만)
    private static RectTransform FindNearestNode(List<RectTransform> nodes, Vector2 mouseGuiPos)
    {
        RectTransform nearest = null;
        float bestDist = HandleScreenRadius * 4f; // 핸들 표시보다 좀 넉넉하게 잡아서 클릭하기 편하게
        foreach (RectTransform node in nodes)
        {
            if (node == null) continue;
            Vector2 guiPos = HandleUtility.WorldToGUIPoint(node.position);
            float dist = Vector2.Distance(guiPos, mouseGuiPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = node;
            }
        }
        return nearest;
    }

    // 드래그 중 미리보기 선의 끝점 - source가 놓인 평면(캔버스 평면) 위로 마우스 레이를 투영
    private static Vector3 GUIPointOnNodePlane(Vector2 guiPos, RectTransform source)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPos);
        Plane plane = new Plane(source.forward, source.position);
        return plane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : source.position;
    }

    // from -> to를 잇는 UpgradeTreeLink를 새로 만듦 (이미 있으면 만들지 않음)
    private static void CreateLink(RectTransform from, RectTransform to)
    {
        UpgradeTreeLink[] existingLinks = Object.FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (UpgradeTreeLink link in existingLinks)
        {
            if (link.ToNode != to) continue;
            foreach (RectTransform existingFrom in link.FromNodes)
                if (existingFrom == from) return; // 이미 같은 연결이 있으면 중복 생성 안 함
        }

        Transform container = existingLinks.Length > 0 ? existingLinks[0].transform.parent : null;
        if (container == null)
        {
            Debug.LogError("UpgradeTreeConnectTool: 링크를 넣을 부모(Content/Link)를 못 찾음 - 씬에 UpgradeTreeLink가 최소 1개는 있어야 함");
            return;
        }

        var go = new GameObject($"Link_{from.name}_to_{to.name}", typeof(RectTransform), typeof(UpgradeTreeLink));
        Undo.RegisterCreatedObjectUndo(go, "Create Upgrade Tree Link");

        var rt = (RectTransform)go.transform;
        Undo.SetTransformParent(rt, container, "Create Upgrade Tree Link");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        // Straight 라우팅은 같은 행/열이면 가로/세로 직선, 그렇지 않으면(대각선 위치) 그대로 대각선 직선으로 그려짐 - 항상 이거로 씀
        var newLink = go.GetComponent<UpgradeTreeLink>();
        var so = new SerializedObject(newLink);
        so.FindProperty("fromNode").objectReferenceValue = from;
        so.FindProperty("toNode").objectReferenceValue = to;
        so.FindProperty("routing").enumValueIndex = (int)UpgradeManager.LinkRouting.Straight;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
    }
}
