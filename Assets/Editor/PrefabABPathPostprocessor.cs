using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabABPathPostprocessor : AssetPostprocessor
{
    /// <summary>
    /// 在导入任何资源之前调用，用于设置AssetBundle路径
    /// </summary>
    void OnPreprocessAsset()
    {
        // 只在Assets目录下
        if (!assetPath.StartsWith("Assets/"))
            return;

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return;

        bool isNeed = assetPath.Contains("Actors") || assetPath.Contains("Effects") || assetPath.Contains("UI") | assetPath.Contains("Other");
        if (isNeed && assetPath.EndsWith(".prefab"))
        {
            // 根据文件夹路径生成AssetBundle名称
            string bundleName = GenerateBundleNameFromFolder(assetPath);
            if (string.IsNullOrEmpty(bundleName))
                return;
            importer.assetBundleName = bundleName + "_prefab";
        }
        else if(assetPath.Contains("UI/RawImage"))
        {
            // 根据文件夹路径生成AssetBundle名称
            string bundleName = GenerateBundleNameFromFolder(assetPath);
            if (string.IsNullOrEmpty(bundleName))
                return;
            string name = bundleName + "/" + Path.GetFileNameWithoutExtension(assetPath);
            importer.assetBundleName = name + "_prefab";
        }
        else if (assetPath.EndsWith(".asset") || assetPath.EndsWith(".wav") || assetPath.EndsWith(".mp3"))
        {
            // 根据文件夹路径生成AssetBundle名称
            string bundleName = GenerateBundleNameFromFolder(assetPath);
            if (string.IsNullOrEmpty(bundleName))
                return;
            importer.assetBundleName = bundleName + "_prefab";
        }
        else
        {
            importer.assetBundleName = string.Empty;
        }
    }

    /// <summary>
    /// 根据资源路径生成基于文件夹的AssetBundle名称
    /// </summary>
    public static string GenerateBundleNameFromFolder(string assetPath)
    {
        // 获取文件所在目录（去掉文件名）
        string directory = Path.GetDirectoryName(assetPath);

        if (string.IsNullOrEmpty(directory))
            return null;

        // 移除"Assets/"前缀，得到相对路径
        string relativePath = directory;
        if (relativePath.StartsWith("Assets\\Game\\"))
        {
            relativePath = relativePath.Substring(12); // 移除"Assets/Game/"
        }

        // 如果直接在Assets根目录，使用特殊名称
        if (string.IsNullOrEmpty(relativePath))
        {
            relativePath = "root_assets";
        }

        relativePath = relativePath.ToLower().Replace('\\', '/');
        return relativePath;
    }

    /// <summary>
    /// 处理资源移动后的AB路径更新
    /// </summary>
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string movedAsset in movedAssets)
        {
            if (movedAsset.EndsWith(".prefab"))
            {
                // 重新应用AB路径设置
                AssetImporter importer = AssetImporter.GetAtPath(movedAsset);
                if (importer != null)
                {
                    string newBundleName = GenerateBundleNameFromFolder(movedAsset);
                    if (!string.IsNullOrEmpty(newBundleName))
                    {
                        importer.assetBundleName = newBundleName.ToLower().Replace('\\', '/');
                        //Debug.Log($"移动后更新AB路径: {movedAsset} -> {newBundleName}");
                    }
                }
            }
        }
    }
}
