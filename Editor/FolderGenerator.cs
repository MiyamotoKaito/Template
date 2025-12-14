using System.IO;
using System.Linq;
using UnityEditor;

public static class FolderGenerator
{
    /// <summary>
    /// 規定のディレクトリ構成を生成する
    /// </summary>
    [MenuItem("Tools/FolderGenerator", priority = 100)]
    public static void GenerateFolder()
    {
        string assetsPath = "Assets";
        string artPath = "Arts";
        string animationPath = "Animation";

        string[] assetsFolders =
            // アセット直下のフォルダ
            new string[] { artPath, "AssetStoreTools", "Editor", "Resources", "Prefabs", "Scenes", "Scripts", "Settings" }
            //Artフォルダ内のフォルダ
            .Concat(new string[] { animationPath, "Audio", "Materials", "Meshes", "Textures", "Shaders", "Sprites" }
                .Select(s => $"{artPath}/{s}"))
            //Animationのフォルダ
            .Concat(new string[] { "Clips", "Controllers" }
                .Select(s => $"{artPath}/{animationPath}/{s}"))
            .ToArray();

        //全てのフォルダを生成する
        foreach (string folder in assetsFolders)
        {
            string path = $"{assetsPath}/{folder}";

            if (!AssetDatabase.IsValidFolder(path))
            {
                FolderCreate(path);
            }
        }
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("フォルダを生成", "フォルダを生成しました", "OK");
    }

    /// <summary>
    ///     フォルダを生成する
    /// </summary>
    /// <param name="path"></param>
    private static void FolderCreate(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path);
        string folderName = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            FolderCreate(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}