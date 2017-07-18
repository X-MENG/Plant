using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Util
{
    /// <summary>
    /// 输出调试信息
    /// </summary>
    /// <param name="msg">调试信息字符串</param>
    public static void DBG(string msg)
    {
        Debug.Log("U3D - " + msg);
    }

    /// <summary>
    /// 输出错误信息
    /// </summary>
    /// <param name="msg">错误信息字符串</param>
    public static void ERR(string msg)
    {
        Debug.LogError("U3D - " + msg);
    }

    /// <summary>
    /// 读取文本
    /// </summary>
    /// <param name="fullName">文本文件名</param>
    /// <returns></returns>
    public static string ReadText(string fullName)
    {
        string txt = "";

        if (fullName.Contains("://"))
        {
            WWW www = new WWW(fullName);
            // 阻塞一下，必须加载好资源之后才能继续进行，否则画面会跳
            while (!www.isDone) { }

            txt = www.text;
        }
        else
        {
            if (System.IO.File.Exists(fullName) == false)
            {
                return txt;
            }

            txt = System.IO.File.ReadAllText(fullName);
        }

        return txt;
    }

    /// <summary>
    /// 读取StreamingAsset下的配置信息
    /// </summary>
    /// <param name="filename">配置文件名</param>
    /// <returns></returns>
    public static string ReadStreamingAsset(string filename)
    {
        string fullName = System.IO.Path.Combine(Application.streamingAssetsPath, filename);

        return ReadText(fullName);
    }

    public static int GetRouletteIndex(List<object> numList)
    {
        int n = Random.Range(1, 101);
        float r = (float)n / 100.0f;
        float sum = 0.0f;
        for (int i = 0; i < numList.Count; ++i)
        {
            float rr = float.Parse(numList[i].ToString());
            sum += rr;
            if (sum >= r)
            {
                return i;
            }
        }

        return 0;
    }

    public static int GetRouletteIndex(ArrayList numList)
    {
        int n = Random.Range(1, 101);
        float r = (float)n / 100.0f;
        float sum = 0.0f;
        foreach (int i in numList)
        {
            sum += i;
            if (sum >= r)
            {
                return i;
            }
        }

        return 0;
    }
}
