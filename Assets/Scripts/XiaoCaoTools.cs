using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class StringTool
{
    public static bool IsEmpty(this string str)
    {
        return string.IsNullOrEmpty(str);
    }

}

public static class FileTool
{
    public static void OpenDir(string path, bool isAssetPath = false)
    {
#if UNITY_EDITOR
        EditorUtility.RevealInFinder(CheckUpperDir(path));
#endif
    }

    public static string CheckUpperDir(string path)
    {
        if (Directory.Exists(path))
        {
            return path;
        }
        else
        {
            //路径无效查找上一层
            path = path.Substring(0, path.LastIndexOf("/") + 1);
        }
        return path;
    }

    public static void CheckDirOrCreat(string checkDir)
    {
        if (checkDir != null && !Directory.Exists(checkDir))
        {
            Debug.Log($"yns creatDir {checkDir}");
            Directory.CreateDirectory(checkDir);
        }
    }

    public static void WriteToFile(byte[] by, string filePath, string checkDir = null)
    {
        if (checkDir != null && !Directory.Exists(checkDir))
            Directory.CreateDirectory(checkDir);
        File.WriteAllBytes(filePath, by);
        Debug.LogFormat("WriteToFile {0}", filePath);
    }

    public static void WriteToFile(string str, string filePath)
    {
        using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.UTF8))
        {
            sw.Write(str);
            sw.Close();
        }
    }

    public static void WriteToFile(List<string> strList, string filePath)
    {
        string tempfile = Path.GetTempFileName();
        using (var writer = new StreamWriter(tempfile))
        {
            foreach (var str in strList)
            {
                writer.WriteLine(str);
            }
        }
        File.Copy(tempfile, filePath, true);
        //删除临时文件
        if (File.Exists(tempfile))
        {
            Debug.Log("删除临时文件: " + tempfile);
            File.Delete(tempfile);
        }

    }

    public static byte[] ReadByte(string filePath)
    {
        return File.ReadAllBytes(filePath);
    }

    public static List<string> ReadFileLines(string filePath)
    {
        List<string> strList = new List<string>();
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    strList.Add(reader.ReadLine());
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        return strList;
    }

    public static string ReadFileString(string filePath)
    {
        StreamReader sr = null;
        try
        {
            sr = File.OpenText(filePath);
        }
        catch (Exception)
        {
            Debug.LogError(filePath);
            return "";
        }
        return sr.ReadToEnd();
    }
    //读取Url内容
    public static string ReadFileWebUrl(string url)
    {
        WebClient client = new WebClient();
        byte[] buffer = client.DownloadData(new Uri(url));
        string res = Encoding.UTF8.GetString(buffer);
        return res;
    }
    //下载Url内容
    public static string DownloadUrlText(string url, string localfilePath)
    {
        string str = ReadFileWebUrl(url);
        WriteToFile(str, localfilePath);
        return str;
    }

    public static bool IsFileExist(string path)
    {
        return File.Exists(path);
    }

    // 删除文件夹
    public static void DeleteDirectory(string path)
    {
        DirectoryInfo dir = new DirectoryInfo(path);
        if (dir.Exists)
        {
            dir.Delete(true);
            Debug.Log("yns Delete " + path);
        }
    }
    //读取贴图
    public static Texture2D LoadTexture(string path, int w = 180, int h = 180)
    {
        if (!IsFileExist(path))
        {
            Debug.LogFormat("yns  no path {0}", path);
            return null;
        }
        FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        fs.Seek(0, SeekOrigin.Begin);
        byte[] bytes = new byte[fs.Length];
        fs.Read(bytes, 0, (int)fs.Length);
        fs.Close();
        fs.Dispose();

        Texture2D t = new Texture2D(w, h);
        t.LoadImage(bytes);
        return t;
    }

}

public static class MathTool
{
    public static Vector3 ChageDir(Vector3 dir, float angle)
    {
        if (angle == 0)
            return dir;
        //angle旋转角度 axis围绕旋转轴 position自身坐标 自身坐标 center旋转中心
        //Quaternion.AngleAxis(angle, axis) * (position - center) + center;
        return Quaternion.AngleAxis(angle, Vector3.up) * (dir);
    }

    public static void RotaToTarget_Com(this Transform transform, Component target, float lerp = 1)
    {
        if (target!=null)
        {
            RotaToTarget(transform, target.transform, lerp);
        }
    }

    public static void RotaToTarget(this Transform transform, Transform target, float lerp = 1)
    {
        if (target != null)
        {
            //Debug.Log($"yns  RotaToTarget {lerp} {target}");
            RotaToPos(transform, target.position, lerp);
        }
    }

    public static bool RoateY_Slow(this Transform transform, Vector3 targetPos, float rotationSpeed ,float minDetal =1)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed);

        float angle = Quaternion.Angle(transform.rotation, targetRotation);

        if (Mathf.Abs(angle) <= minDetal)
        {
            //transform.RotaToPos(targetPos);
            return true;
        }
        else
        {
            return false;
        }
    }



    public static void RotaToPos(this Transform transform, Vector3 wordlPos, float lerp = 1)
    {
        wordlPos.y = transform.position.y; //保持同一高度
        Quaternion rotation = Quaternion.LookRotation(wordlPos - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, lerp);
    }

    //二阶贝塞尔
    public static Vector3 GetBezierPoint2(Vector3 begin, Vector3 end, Vector3 handle, float t)
    {
        float pow = Mathf.Pow(1 - t, 2);
        float x = pow * begin.x + 2 * t * (1 - t) * handle.x + t * t * end.x;
        float y = pow * begin.y + 2 * t * (1 - t) * handle.y + t * t * end.y;
        float z = pow * begin.z + 2 * t * (1 - t) * handle.z + t * t * end.z;
        return new Vector3(x, y, z);
    }
    //求导
    public static Vector3 GetBezierPoint2_Speed(Vector3 begin, Vector3 end, Vector3 handle, float t)
    {
        float pow_s = 2*t - 2;
        float x = pow_s * begin.x+(2- 4 * t )* handle.x + 2 * t * end.x;
        float y = pow_s * begin.y+ (2 - 4 * t) * handle.y + 2 * t * end.y;
        float z = pow_s * begin.z+ (2 - 4 * t) * handle.z + 2 * t * end.z;
        return new Vector3(x, y, z);
    }

    //三阶段贝塞尔
    public static Vector3 GetBezierPoint3(float time, Vector3 startPosition, Vector3 endPosition, Vector3 startTangent, Vector3 endTangent)
    {
        float t = time;
        float u = 1f - t;
        float t2 = t * t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t3 = t2 * t;

        Vector3 result =
            (u3) * startPosition +
            (3f * u2 * t) * startTangent +
            (3f * u * t2) * endTangent +
            (t3) * endPosition;

        return result;
    }

    public static Vector3 LinearVec3(Vector3 start, Vector3 end, float t)
    {
        end -= start;
        return end * t + start;
    }
}

public static class EditorStringTool
{

    //移除结尾
    public static string RemoveEnd(this string str, string removeStr)
    {
        if (str.EndsWith(removeStr))
        {
            int len = str.Length;
            return str.Remove(len - removeStr.Length, removeStr.Length);
        }
        else
        {
            Debug.LogError(str + "no EndsWith " + removeStr);
            return str;
        }
    }

}