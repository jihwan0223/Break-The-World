using System.Collections.Generic;

// 자물쇠(관문) 노드 중 "열리는 연출"을 이미 본 노드 이름들 - 저장됨.
// 관문 오브젝트가 해금된 뒤 업그레이드 창을 처음 열 때 한 번만 카메라 이동 + 자물쇠 깨짐 연출을 보여주려고 씀
public static class GateReveals
{
    private static readonly HashSet<string> _seen = new HashSet<string>(); // 연출을 본 노드 이름

    // 아직 연출을 안 봤으면 본 것으로 표시하고 true (한 노드당 딱 한 번만 true)
    public static bool TryMarkSeen(string nodeName) => _seen.Add(nodeName);

    public static string[] ToArray() => new List<string>(_seen).ToArray();

    public static void Load(string[] names)
    {
        _seen.Clear();
        if (names != null) _seen.UnionWith(names);
    }

    public static void Reset() => _seen.Clear();
}
