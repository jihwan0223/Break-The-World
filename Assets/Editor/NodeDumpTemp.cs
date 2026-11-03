using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 임시: 업그레이드 노드 정보를 Temp/nodes_dump.txt로 저장
public static class NodeDumpTemp
{
    [MenuItem("Tools/Temp/Node Dump")]
    public static void Dump()
    {
        var sb = new StringBuilder(); // 결과 글
        foreach (var node in Object.FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(node); // 직렬화 값 읽기용
            var rect = (RectTransform)node.transform; // 트리 위치
            sb.Append(node.name).Append('\t').Append(node.Effect).Append('\t').Append(node.TargetObjectName)
              .Append('\t').Append(node.MaxLevel).Append('\t').Append(rect.anchoredPosition.x).Append(',').Append(rect.anchoredPosition.y)
              .Append('\t').Append(so.FindProperty("displayName").stringValue)
              .Append('\t').Append(so.FindProperty("fullVersionOnly").boolValue)
              .Append('\t').Append(so.FindProperty("gateObjectName").stringValue);
            var costs = so.FindProperty("costs"); // 비용 배열
            for (int i = 0; i < costs.arraySize; i++)
            {
                var c = costs.GetArrayElementAtIndex(i); // 비용 한 칸
                sb.Append("\tcost[crystal=").Append(c.FindPropertyRelative("useCrystals").boolValue)
                  .Append(" base=").Append(c.FindPropertyRelative("baseAmount").longValue)
                  .Append(" growth=").Append(c.FindPropertyRelative("levelGrowth").floatValue)
                  .Append(" first=").Append(c.FindPropertyRelative("firstLevelAmount").longValue).Append(" levels=");
                var lv = c.FindPropertyRelative("levelAmounts"); // 레벨별 값
                for (int j = 0; j < lv.arraySize; j++) sb.Append(lv.GetArrayElementAtIndex(j).longValue).Append(',');
                sb.Append(']');
            }
            sb.AppendLine();
        }
        File.WriteAllText("Temp/nodes_dump.txt", sb.ToString());
        Debug.Log("Node dump saved");
    }
}
