using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 扫描并尽量修复 C# 中文备注乱码。
/// 可自动处理：整文件被当成 GBK、UTF-8 被误当成 Latin1/GBK 的 mojibake。
/// <c>???</c> 属于字符已丢失，只能对照 git 历史把同一行备注对回去。
/// </summary>
public sealed class CommentEncodingRepairWindow : EditorWindow
{
    const int GitHistoryDepth = 25;
    const int MaxFileBytes = 2 * 1024 * 1024;

    static readonly string[] ScanRoots =
    {
        "Assets/Scripts",
        "Assets/Editor",
    };

    static readonly string[] SkipPathParts =
    {
        "/Plugins/",
        "\\Plugins\\",
        "/Config/Luban/",
        "\\Config\\Luban\\",
        "/PackageCache/",
    };

    Vector2 _scroll;
    readonly List<RepairHit> _hits = new List<RepairHit>(64);
    bool _restoreFromGit = true;
    bool _writeUtf8Bom = true;
    string _status = "尚未扫描。";
    bool _scanning;

    [MenuItem("Tools/编码/扫描并修复中文备注")]
    public static void Open()
    {
        var window = GetWindow<CommentEncodingRepairWindow>("中文备注修复");
        window.minSize = new Vector2(640, 360);
    }

    [MenuItem("Tools/编码/扫描中文备注（仅报告）")]
    public static void ScanOnlyMenu()
    {
        var window = GetWindow<CommentEncodingRepairWindow>("中文备注修复");
        window._restoreFromGit = true;
        window.Scan(apply: false);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "乱码分两类：\n" +
            "1) 错误解码（æˆ˜ / 锟斤拷）——可以按编码规则转回中文。\n" +
            "2) 连续问号（?? / ???）——原文已丢，只能从 git 历史同一行对回。\n" +
            "单个 ?（疑问句、T?、?: 三元）不会当作乱码。建议先扫描预览，确认后再写入。",
            MessageType.Info);

        _restoreFromGit = EditorGUILayout.ToggleLeft("从 git 历史恢复 ??? 备注", _restoreFromGit);
        _writeUtf8Bom = EditorGUILayout.ToggleLeft("写回 UTF-8 BOM（降低 VS 再次当 GBK 打开的概率）", _writeUtf8Bom);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(_scanning))
        {
            if (GUILayout.Button("扫描（不写盘）", GUILayout.Height(28)))
                Scan(apply: false);
            if (GUILayout.Button("扫描并写回", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("写回确认", "将按扫描结果改写脚本文件。建议先提交或备份当前改动。", "写回", "取消"))
                    Scan(apply: true);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(_status);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _hits.Count; i++)
        {
            RepairHit hit = _hits[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(hit.RelativePath, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(hit.KindLabel);
            if (!string.IsNullOrEmpty(hit.Detail))
                EditorGUILayout.LabelField(hit.Detail, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    void Scan(bool apply)
    {
        _scanning = true;
        _hits.Clear();
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Encoding gbk = TryGetGbk();
            var gitCache = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);

            int scanned = 0;
            int writable = 0;
            int unfixable = 0;
            for (int r = 0; r < ScanRoots.Length; r++)
            {
                string absRoot = Path.Combine(projectRoot, ScanRoots[r]);
                if (!Directory.Exists(absRoot))
                    continue;

                string[] files = Directory.GetFiles(absRoot, "*.cs", SearchOption.AllDirectories);
                for (int f = 0; f < files.Length; f++)
                {
                    string abs = files[f];
                    if (ShouldSkip(abs))
                        continue;

                    scanned++;
                    if (!TryRepairFile(projectRoot, abs, gbk, gitCache, apply, out RepairHit hit))
                        continue;

                    _hits.Add(hit);
                    if (hit.WouldWrite)
                        writable++;
                    else
                        unfixable++;
                }
            }

            if (apply && writable > 0)
                AssetDatabase.Refresh();

            _status = apply
                ? $"扫描 {scanned} 个文件，写回 {writable} 个；仍无法自动修复 {unfixable} 个。"
                : $"扫描 {scanned} 个文件，可修复 {writable} 个，无法自动修复 {unfixable} 个。";
        }
        catch (Exception ex)
        {
            _status = "扫描失败：" + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _scanning = false;
        }
    }

    bool TryRepairFile(
        string projectRoot,
        string absPath,
        Encoding gbk,
        Dictionary<string, List<string[]>> gitCache,
        bool apply,
        out RepairHit hit)
    {
        hit = default;
        byte[] raw = File.ReadAllBytes(absPath);
        if (raw.Length == 0 || raw.Length > MaxFileBytes)
            return false;

        string relative = ToGitRelative(projectRoot, absPath);
        DecodeResult decoded = DecodeBytes(raw, gbk);
        string text = decoded.Text;
        bool changed = decoded.UsedFallbackEncoding;

        if (LooksLikeMojibake(text))
        {
            string mojibakeFixed = TryFixMojibakeText(text);
            if (!ReferenceEquals(mojibakeFixed, text) && CountCjk(mojibakeFixed) > CountCjk(text))
            {
                text = mojibakeFixed;
                changed = true;
            }
        }

        string[] lines = SplitKeepNewline(text, out _);
        bool gitRestored = false;
        if (_restoreFromGit && HasSuspiciousQuestionMarks(text))
        {
            if (TryRestoreQuestionMarksFromGit(projectRoot, relative, lines, gitCache))
            {
                gitRestored = true;
                changed = true;
            }
        }

        string newText = string.Join("", lines);
        if (changed)
        {
            hit = new RepairHit
            {
                RelativePath = relative,
                WouldWrite = true,
                KindLabel = BuildKindLabel(decoded.UsedFallbackEncoding, gitRestored, decoded.EncodingName),
                Detail = BuildDetail(lines, decoded.Text),
            };

            if (apply)
                WriteUtf8(absPath, newText, _writeUtf8Bom);

            return true;
        }

        if (HasSuspiciousQuestionMarks(decoded.Text))
        {
            hit = new RepairHit
            {
                RelativePath = relative,
                WouldWrite = false,
                KindLabel = "无法自动修复：备注已变成 ???，git 历史对不上（原文已丢失）",
                Detail = CollectGarbledPreview(decoded.Text),
            };
            return true;
        }

        return false;
    }

    static string CollectGarbledPreview(string text)
    {
        var sb = new StringBuilder(256);
        string[] lines = SplitKeepNewline(text, out _);
        int shown = 0;
        for (int i = 0; i < lines.Length && shown < 6; i++)
        {
            if (!LineLooksGarbled(lines[i]))
                continue;
            sb.Append(i + 1).Append(": ");
            sb.Append(TrimPreview(lines[i]));
            sb.Append('\n');
            shown++;
        }
        return sb.ToString();
    }

    static string BuildKindLabel(bool encoding, bool git, string encodingName)
    {
        if (encoding && git)
            return $"整文件按 {encodingName} 转 UTF-8，并从 git 对回 ???";
        if (encoding)
            return $"整文件按 {encodingName} 转 UTF-8";
        if (git)
            return "从 git 历史对回 ??? 备注";
        return "已按 mojibake 规则转回中文";
    }

    static string BuildDetail(string[] lines, string original)
    {
        string[] oldLines = SplitKeepNewline(original, out _);
        int shown = 0;
        var sb = new StringBuilder(256);
        int n = Math.Min(oldLines.Length, lines.Length);
        for (int i = 0; i < n && shown < 6; i++)
        {
            if (oldLines[i] == lines[i])
                continue;
            sb.Append(i + 1).Append(": ");
            sb.Append(TrimPreview(oldLines[i]));
            sb.Append("  =>  ");
            sb.Append(TrimPreview(lines[i]));
            sb.Append('\n');
            shown++;
        }
        return sb.ToString();
    }

    static string TrimPreview(string line)
    {
        string t = line.TrimEnd('\r', '\n');
        return t.Length <= 120 ? t : t.Substring(0, 117) + "...";
    }

    static bool ShouldSkip(string absPath)
    {
        for (int i = 0; i < SkipPathParts.Length; i++)
        {
            if (absPath.IndexOf(SkipPathParts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    static string ToGitRelative(string projectRoot, string absPath)
    {
        string rel = absPath.Substring(projectRoot.Length).TrimStart('\\', '/');
        return rel.Replace('\\', '/');
    }

    static DecodeResult DecodeBytes(byte[] raw, Encoding gbk)
    {
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
        {
            return new DecodeResult
            {
                Text = Encoding.UTF8.GetString(raw, 3, raw.Length - 3),
                EncodingName = "UTF-8 BOM",
                UsedFallbackEncoding = false,
            };
        }

        var utf8Strict = new UTF8Encoding(false, true);
        try
        {
            return new DecodeResult
            {
                Text = utf8Strict.GetString(raw),
                EncodingName = "UTF-8",
                UsedFallbackEncoding = false,
            };
        }
        catch (DecoderFallbackException)
        {
        }

        if (gbk != null)
        {
            string gbkText = gbk.GetString(raw);
            if (CountCjk(gbkText) > 0)
            {
                return new DecodeResult
                {
                    Text = gbkText,
                    EncodingName = "GBK",
                    UsedFallbackEncoding = true,
                };
            }
        }

        return new DecodeResult
        {
            Text = Encoding.UTF8.GetString(raw),
            EncodingName = "UTF-8(lossy)",
            UsedFallbackEncoding = false,
        };
    }

    static Encoding TryGetGbk()
    {
        try
        {
            return Encoding.GetEncoding(936);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>仅在文本已经像乱码时才做 round-trip，避免把正常中文编进 Latin1 变成问号。</summary>
    static bool LooksLikeMojibake(string text)
    {
        if (text.IndexOf("锟斤拷", StringComparison.Ordinal) >= 0)
            return true;

        int latin1 = 0;
        int cjk = 0;
        int n = Math.Min(text.Length, 20000);
        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            if (c >= 0x00C0 && c <= 0x00FF)
                latin1++;
            else if (IsCjkOrFullwidth(c))
                cjk++;
        }

        return latin1 >= 8 && latin1 > cjk;
    }

    /// <summary>把 UTF-8 被误读成 Latin1/GBK 后的字符转回中文。</summary>
    static string TryFixMojibakeText(string text)
    {
        string best = text;
        int bestCjk = CountCjk(text);

        TryRoundTrip(text, Encoding.GetEncoding("ISO-8859-1"), Encoding.UTF8, ref best, ref bestCjk);
        try
        {
            TryRoundTrip(text, Encoding.GetEncoding(1252), Encoding.UTF8, ref best, ref bestCjk);
        }
        catch (Exception)
        {
        }

        Encoding gbk = TryGetGbk();
        if (gbk != null)
            TryRoundTrip(text, gbk, Encoding.UTF8, ref best, ref bestCjk);

        return best;
    }

    static void TryRoundTrip(string text, Encoding asWrong, Encoding asRight, ref string best, ref int bestCjk)
    {
        try
        {
            byte[] bytes = asWrong.GetBytes(text);
            string candidate = asRight.GetString(bytes);
            if (candidate.IndexOf('\uFFFD') >= 0)
                return;
            int cjk = CountCjk(candidate);
            if (cjk > bestCjk + 2)
            {
                best = candidate;
                bestCjk = cjk;
            }
        }
        catch (Exception)
        {
        }
    }

    static bool HasSuspiciousQuestionMarks(string text)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (LineLooksGarbled(lines[i]))
                return true;
        }
        return false;
    }

    static bool LineLooksGarbled(string line)
    {
        string t = line.TrimStart();
        bool isComment = t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal);
        bool isDocString = Regex.IsMatch(line, @"(DisplayName|ReferenceNote|Tooltip|Header)\s*=\s*""");
        if (!isComment && !isDocString)
            return false;

        return HasReplacementQuestionRun(line);
    }

    /// <summary>
    /// 只把「连续两个及以上 ?」当成替换丢失；单个 ? 可能是疑问句、可空 T?、三元运算符。
    /// 会先去掉合法的 ?. 与标识符之间的 ??，避免把 C# 语法当乱码。
    /// </summary>
    static bool HasReplacementQuestionRun(string line)
    {
        string masked = Regex.Replace(line, @"\?\.", string.Empty);
        masked = Regex.Replace(masked, @"(?<=[\w\)\]])(\s*)\?\?(\s*)(?=[\w\(\!""])", "$1$2");
        return Regex.IsMatch(masked, @"\?{2,}");
    }

    bool TryRestoreQuestionMarksFromGit(
        string projectRoot,
        string relativePath,
        string[] lines,
        Dictionary<string, List<string[]>> gitCache)
    {
        if (!gitCache.TryGetValue(relativePath, out List<string[]> history))
        {
            history = LoadGitFileHistory(projectRoot, relativePath);
            gitCache[relativePath] = history;
        }

        if (history == null || history.Count == 0)
            return false;

        bool any = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!LineLooksGarbled(lines[i]))
                continue;

            string current = StripNewline(lines[i], out string ending);
            string restored = FindHistoricalMatch(current, i, history);
            if (string.IsNullOrEmpty(restored) || restored == current)
                continue;
            if (CountCjk(restored) <= CountCjk(current))
                continue;

            lines[i] = restored + ending;
            any = true;
        }

        return any;
    }

    static string FindHistoricalMatch(string current, int lineIndex, List<string[]> history)
    {
        string currentSk = AsciiSkeleton(current);
        if (currentSk.Length < 8)
            return null;

        string best = null;
        int bestCjk = CountCjk(current);
        for (int h = 0; h < history.Count; h++)
        {
            string[] oldLines = history[h];
            int start = Math.Max(0, lineIndex - 8);
            int end = Math.Min(oldLines.Length - 1, lineIndex + 8);
            for (int i = start; i <= end; i++)
            {
                string old = StripNewline(oldLines[i], out _);
                if (AsciiSkeleton(old) != currentSk)
                    continue;
                int cjk = CountCjk(old);
                if (cjk > bestCjk)
                {
                    best = old;
                    bestCjk = cjk;
                }
            }
        }

        return best;
    }

    static List<string[]> LoadGitFileHistory(string projectRoot, string relativePath)
    {
        var result = new List<string[]>(GitHistoryDepth);
        string hashes = RunGit(projectRoot, $"log -n {GitHistoryDepth} --pretty=format:%H -- \"{relativePath}\"");
        if (string.IsNullOrEmpty(hashes))
            return result;

        string[] commits = hashes.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < commits.Length; i++)
        {
            byte[] blob = RunGitBytes(projectRoot, $"show \"{commits[i].Trim()}:{relativePath}\"");
            if (blob == null || blob.Length == 0)
                continue;
            DecodeResult decoded = DecodeBytes(blob, TryGetGbk());
            result.Add(SplitKeepNewline(decoded.Text, out _));
        }

        return result;
    }

    static string RunGit(string projectRoot, string args)
    {
        byte[] bytes = RunGitBytes(projectRoot, args);
        if (bytes == null)
            return string.Empty;
        return Encoding.UTF8.GetString(bytes);
    }

    static byte[] RunGitBytes(string projectRoot, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = projectRoot,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                if (p == null)
                    return null;
                p.ErrorDataReceived += (_, __) => { };
                p.BeginErrorReadLine();
                using (var ms = new MemoryStream())
                {
                    p.StandardOutput.BaseStream.CopyTo(ms);
                    p.WaitForExit(20000);
                    if (p.ExitCode != 0)
                        return null;
                    return ms.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CommentEncodingRepair] git 调用失败：" + ex.Message);
            return null;
        }
    }

    static void WriteUtf8(string absPath, string text, bool withBom)
    {
        var enc = new UTF8Encoding(withBom, true);
        File.WriteAllText(absPath, text, enc);
    }

    static string[] SplitKeepNewline(string text, out string detectedNl)
    {
        detectedNl = text.Contains("\r\n") ? "\r\n" : "\n";
        var list = new List<string>(256);
        int i = 0;
        while (i < text.Length)
        {
            int nl = text.IndexOf('\n', i);
            if (nl < 0)
            {
                list.Add(text.Substring(i));
                break;
            }
            int end = nl + 1;
            list.Add(text.Substring(i, end - i));
            i = end;
        }

        if (list.Count == 0)
            list.Add(text);
        return list.ToArray();
    }

    static string StripNewline(string line, out string ending)
    {
        if (line.EndsWith("\r\n", StringComparison.Ordinal))
        {
            ending = "\r\n";
            return line.Substring(0, line.Length - 2);
        }
        if (line.EndsWith("\n", StringComparison.Ordinal))
        {
            ending = "\n";
            return line.Substring(0, line.Length - 1);
        }
        ending = string.Empty;
        return line;
    }

    static string AsciiSkeleton(string line)
    {
        var sb = new StringBuilder(line.Length);
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '?' || c == '\uFFFD' || char.IsWhiteSpace(c))
                continue;
            if (IsCjkOrFullwidth(c))
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    static int CountCjk(string text)
    {
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (IsCjkOrFullwidth(text[i]))
                n++;
        }
        return n;
    }

    static bool IsCjkOrFullwidth(char c)
    {
        return c >= 0x3400 && c <= 0x9FFF
            || c >= 0x3000 && c <= 0x303F
            || c >= 0xFF00 && c <= 0xFFEF;
    }

    struct DecodeResult
    {
        public string Text;
        public string EncodingName;
        public bool UsedFallbackEncoding;
    }

    struct RepairHit
    {
        public string RelativePath;
        public string KindLabel;
        public string Detail;
        public bool WouldWrite;
    }
}
