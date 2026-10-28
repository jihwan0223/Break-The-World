using System.Collections.Generic;

public static class GateReveals
{
    private static readonly HashSet<string> _seen = new HashSet<string>();

    // 노드마다 처음 한 번만 true
    public static bool TryMarkSeen(string nodeName) => _seen.Add(nodeName);

    public static string[] ToArray() => new List<string>(_seen).ToArray();

    public static void Load(string[] names)
    {
        _seen.Clear();
        if (names != null) _seen.UnionWith(names);
    }

    public static void Reset() => _seen.Clear();
}
