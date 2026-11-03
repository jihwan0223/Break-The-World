using System.IO;
using UnityEngine;

// 게임 이름을 Break The World에서 Break The Wax World로 바꾸면서 저장 폴더도 바뀜.
// 새 폴더에 세이브가 없고 옛 폴더에 있으면 옛 파일을 복사해옴 (옛 폴더는 그대로 둠).
public static class SaveFolderMigration
{
    private const string OldFolderName = "Break The World"; // 이름 바꾸기 전 Product Name
    private const string SaveFileName = "savedata.json"; // SaveManager의 저장 파일명과 같아야 함

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Migrate()
    {
        string newDir = Application.persistentDataPath; // .../_jihwan/Break The Wax World
        string parent = Path.GetDirectoryName(newDir); // .../_jihwan
        if (string.IsNullOrEmpty(parent)) return;
        string oldDir = Path.Combine(parent, OldFolderName); // .../_jihwan/Break The World
        if (oldDir == newDir || File.Exists(Path.Combine(newDir, SaveFileName)) || !File.Exists(Path.Combine(oldDir, SaveFileName))) return;

        try
        {
            foreach (string file in Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(newDir, Path.GetRelativePath(oldDir, file)); // 새 폴더 안 같은 위치
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target)) File.Copy(file, target);
            }
            Debug.Log($"[SaveFolderMigration] 옛 저장 폴더에서 복사함: {oldDir}");
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[SaveFolderMigration] 복사 실패: {e.Message}");
        }
    }
}
