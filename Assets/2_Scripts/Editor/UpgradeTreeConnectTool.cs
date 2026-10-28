using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Upgrade Tree > Toggle Connect Mode (Alt+C)
[InitializeOnLoad]
public static class UpgradeTreeConnectTool
{
    private const string MenuPath = "Tools/Upgrade Tree/Toggle Connect Mode &c"; // &c = Alt+C
    private const string PrefsKey = "UpgradeTreeConnectTool.Enabled";
    private const float HandleScreenRadius = 8f; // 스크린 px

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefsKey, false);
        set => EditorPrefs.SetBool(PrefsKey, value);
    }

    private static RectTransform _dragSource;

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

    private static List<RectTransform> AllNodes()
    {
        var list = new List<RectTransform>();
        foreach (UpgradeNodeUI n in Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(n.Rect);
        foreach (ObjectEconomyNodeUI n in Object.FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(n.Rect);
        return list;
    }

    private static readonly Color ConnectedColor = new Color(0.3f, 1f, 0.5f, 0.9f);
    private static readonly Color UnconnectedColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.6f);

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!Enabled) return;

        Event e = Event.current;
        List<RectTransform> nodes = AllNodes();

        // 에디터에서 UpgradeTreeLink가 안 보일 때가 있어 Handles로 다시 그림
        UpgradeTreeLink[] links = Object.FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var connectedNodes = new HashSet<RectTransform>();

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
                sceneView.Repaint();
                break;

            case EventType.MouseDown when e.button == 1 && _dragSource != null:
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

    private static RectTransform FindNearestNode(List<RectTransform> nodes, Vector2 mouseGuiPos)
    {
        RectTransform nearest = null;
        float bestDist = HandleScreenRadius * 4f;
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

    private static Vector3 GUIPointOnNodePlane(Vector2 guiPos, RectTransform source)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPos);
        Plane plane = new Plane(source.forward, source.position);
        return plane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : source.position;
    }

    private static void CreateLink(RectTransform from, RectTransform to)
    {
        UpgradeTreeLink[] existingLinks = Object.FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (UpgradeTreeLink link in existingLinks)
        {
            if (link.ToNode != to) continue;
            foreach (RectTransform existingFrom in link.FromNodes)
                if (existingFrom == from) return;
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

        var newLink = go.GetComponent<UpgradeTreeLink>();
        var so = new SerializedObject(newLink);
        so.FindProperty("fromNode").objectReferenceValue = from;
        so.FindProperty("toNode").objectReferenceValue = to;
        so.FindProperty("routing").enumValueIndex = (int)UpgradeManager.LinkRouting.Straight;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
    }
}
