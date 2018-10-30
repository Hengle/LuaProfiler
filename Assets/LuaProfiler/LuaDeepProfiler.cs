using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using System.Text.RegularExpressions;
using UnityEngine;

namespace MikuLuaProfiler
{
#if UNITY_EDITOR
    using UnityEditor;
    public class StringLoadInfo
    {
        public StringLoadInfo(string s)
        {
            Str = s;
            Pos = 0;
        }

        public int ReadByte()
        {
            if (Pos >= Str.Length)
                return -1;
            else
                return Str[Pos++];
        }
        public void ReadBack()
        {
            Pos = Pos - 1;
        }


        public string Replace(int start, int len, string value)
        {
            string result = Str.Substring(start, len);
            Str = Str.Remove(start, len);
            Str = Str.Insert(start, value);
            if ((start + len) <= Pos)
            {
                Pos = Pos - (len - value.Length);
            }
            return result;
        }

        public int PeekByte()
        {
            if (Pos >= Str.Length)
                return -1;
            else
                return Str[Pos];
        }
        private string Str;
        private int Pos;
        public int pos
        {
            get
            {
                return Pos;
            }
        }
    }

    [CustomEditor(typeof(LuaDeepProfilerSetting))]
    public class LuaDeepProfiler : Editor
    {
        enum keyword
        {
            knull,
            kfunction,
            kfor,
            kif,
            kwhile,
            kleftTable,
            krightTable,
        }
        private static string returnVarName = "";
        private static string returnFunName = "";
        private static string returnTbName = "";
        private static string rootDirPath = "";
        private static string rootProfilerDirPath = "";
        private static bool ignoreBigFile = false;
        private static int bigFileSize = 1024 * 150;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            LuaDeepProfilerSetting settings = (LuaDeepProfilerSetting)target;

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Resources Path", GUI.skin.textField, style);
            settings.luaProjectPath = EditorGUILayout.TextField(GUIContent.none, settings.luaProjectPath);
            if (GUILayout.Button("Browse", GUILayout.ExpandWidth(false)))
            {
                GUI.FocusControl(null);
                var path = EditorUtility.OpenFolderPanel("Locate Build Folder", settings.luaProjectPath, null);
                if (!String.IsNullOrEmpty(path))
                {
                    settings.luaProjectPath = MakePathRelativeToProject(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("start", GUILayout.ExpandWidth(true)))
            {
                Start();
            }
            if (GUILayout.Button("restore", GUILayout.ExpandWidth(true)))
            {
                Restore();
            }
            EditorGUILayout.EndVertical();
        }

        public static void Start(bool isHook = false)
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("editor is comipiling");
                return;
            }
            try
            {
                string path = LuaDeepProfilerSetting.Instance.luaProjectPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = EditorUtility.OpenFolderPanel("请选择Lua脚本存放文件夹", "", "*");
                    path = MakePathRelativeToProject(path);
                    LuaDeepProfilerSetting.Instance.luaProjectPath = path;
                }
                rootDirPath = Application.dataPath.Replace("Assets", path).Replace("/", "\\");
                rootProfilerDirPath = Application.dataPath.Replace("Assets", LuaDeepProfilerSetting.Instance.profilerLuaProjectPath).Replace("/", "\\");

                LuaDeepProfilerSetting.Instance.ReMakeDict();
                DirectoryInfo info = new DirectoryInfo(path);
                if (!isHook)
                {
                    LuaDeepProfilerSetting.Instance.isDeepProfiler = true;
                }

                FindFunctionChunk(info);
                if (!isHook)
                {
                    UnityEditor.EditorApplication.isPlaying = false;
                }
                Debug.Log("complete");
            }
            catch (Exception e)
            {
                throw e;
                //Debug.LogError(e.Message);
            }
            EditorUtility.SetDirty(LuaDeepProfilerSetting.Instance);
            EditorUtility.ClearProgressBar();
            if (!isHook)
            {
                AssetDatabase.SaveAssets();
            }
            AssetDatabase.Refresh();
        }
        public static void Restore()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("editor is comipiling");
                return;
            }
            try
            {
                string path = LuaDeepProfilerSetting.Instance.luaProjectPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = EditorUtility.OpenFolderPanel("", "", "*");
                    LuaDeepProfilerSetting.Instance.luaProjectPath = MakePathRelativeToProject(path);
                }
                rootDirPath = Application.dataPath.Replace("Assets", path).Replace("/", "\\");
                rootProfilerDirPath = Application.dataPath.Replace("Assets", LuaDeepProfilerSetting.Instance.profilerLuaProjectPath).Replace("/", "\\");

                LuaDeepProfilerSetting.Instance.ClearMD5Dict();
                Directory.Delete(rootProfilerDirPath, true);
                LuaDeepProfilerSetting.Instance.isDeepProfiler = false;
                Debug.Log("complete");
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            EditorUtility.SetDirty(LuaDeepProfilerSetting.Instance);
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        public static string MakePathRelativeToProject(string path)
        {
            string result = "";
            string fullPath = Path.GetFullPath(path);
            string fullProjectPath = Path.GetFullPath(Environment.CurrentDirectory + Path.DirectorySeparatorChar);

            int i = 0;
            string dirName = Path.GetFileNameWithoutExtension(Environment.CurrentDirectory);
            string upProjectPath = Path.GetFullPath(Environment.CurrentDirectory.TrimEnd(dirName.ToCharArray()));
            string sep = ".." + Path.DirectorySeparatorChar;
            string fullSep = "";

            while (i < 4)
            {
                fullSep = sep + fullSep;
                if (fullPath.Contains(upProjectPath) && !fullPath.Contains(fullProjectPath))
                {
                    result = fullSep + fullPath.Replace(upProjectPath, "");
                    break;
                }
                else if (i == 3)
                {
                    result = fullPath.Replace(fullProjectPath, "");
                    break;
                }

                dirName = Path.GetFileNameWithoutExtension(upProjectPath.TrimEnd(Path.DirectorySeparatorChar));
                upProjectPath = upProjectPath.TrimEnd((dirName + Path.DirectorySeparatorChar).ToCharArray()) + Path.DirectorySeparatorChar;
                i++;

            }
            return result;
        }

        private static void Clear()
        {
            returnVarName = "";
            returnFunName = "";
            returnTbName = "";
        }
        #region diff
        /// <summary>
        /// 获取文件MD5值
        /// </summary>
        /// <param name="fileName">文件绝对路径</param>
        /// <returns>MD5值</returns>
        public static string GetMD5HashFromFile(string fileName)
        {
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }
        #endregion

        private static void FindFunctionChunk(DirectoryInfo dir)
        {
            do
            {
                FileInfo[] files = dir.GetFiles("*" + LuaDeepProfilerSetting.Instance.luaExtern);
                int count = files.Length;
                int process = 0;
                foreach (FileInfo item in files)
                {
                    process++;
                    if (item.Extension != LuaDeepProfilerSetting.Instance.luaExtern)
                    {
                        continue;
                    }

                    string cacheMD5;
                    string nowMD5 = GetMD5HashFromFile(item.FullName);
                    LuaDeepProfilerSetting.Instance.AddValue(item.FullName, nowMD5);
                    if (LuaDeepProfilerSetting.Instance.TryGetMd5(item.FullName, out cacheMD5)
                        && cacheMD5 == nowMD5)
                    {
                        continue;
                    }
                    EditorUtility.DisplayProgressBar("profiler lua", item.FullName, (float)process / count);
                    string allCode = File.ReadAllText(item.FullName);
                    var excludeFile = LuaDeepProfilerSetting.Instance.excludeFile;
                    var excludeFolder = LuaDeepProfilerSetting.Instance.excludeFolder;

                    if (!((ignoreBigFile && allCode.Length > bigFileSize)
                        || excludeFile.Contains(item.Name) || excludeFolder.Contains(dir.Name)))
                    {
                        allCode = ParseLua(item.FullName.Replace(rootDirPath + "\\", "").Replace("\\", "."), allCode);
                    }

                    string profilerPath = item.FullName.Replace(rootDirPath, rootProfilerDirPath);
                    string profilerDirPath = profilerPath.Replace(item.Name, "");
                    if (!Directory.Exists(profilerDirPath))
                    {
                        Directory.CreateDirectory(profilerDirPath);
                    }
                    File.WriteAllText(profilerPath, allCode);
                }
            } while (false);

            DirectoryInfo[] dis = dir.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                FindFunctionChunk(di);
            }
        }
        private static string ParseLua(string fileName, string allCode)
        {
            #region file profiler
            StringBuilder sb = new StringBuilder();
            string format = "";
            #endregion

            strCount = 0;
            m_dict.Clear();
            allCode = TrimComment(allCode);
            allCode = NewLineFunction(allCode);
            allCode = PrettyReturn(allCode);
            allCode = PrettyTable(allCode);
            allCode = PrettyOrAnd(allCode);
            allCode = PrettyCalSig(allCode);
            allCode = PrettyThen(allCode);
            allCode = PrettyEnd(allCode);
            allCode = LocalReturnFun(allCode);

            StringReader sr = new StringReader(allCode);
            Stack<keyword> keywordStack = new Stack<keyword>();
            bool beginSample = false;
            bool needEndSample = true;

            format = "CS.MikuLuaProfiler.LuaProfiler.BeginSample(\"{0}\")";
            ApeendCrLine(sb, string.Format(format, "require " + fileName));
            bool needEndFileSample = true;
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine();

                #region key word
                bool containEnd = CheckIsEndLine(line);
                bool containFunction = CheckIsFunctionLine(line);
                bool containFor = CheckIsForLine(line);
                bool containIf = CheckIsIfLine(line);
                bool containWhile = CheckIsWhileLine(line);
                bool containReturn = CheckIsReturnLine(line);

                if (containFunction) keywordStack.Push(keyword.kfunction);
                if (containFor) keywordStack.Push(keyword.kfor);
                if (containIf) keywordStack.Push(keyword.kif);
                if (containWhile) keywordStack.Push(keyword.kwhile);

                if (containReturn)
                {
                    var keyArray = keywordStack.ToArray();
                    for (int i = 0, imax = keyArray.Length; i < imax; i++)
                    {
                        if (keyArray[i] == keyword.kfunction)
                        {
                            needEndSample = false;
                            break;
                        }
                        else if (keyArray[i] == keyword.kif)
                        {
                            needEndSample = true;
                            break;
                        }
                    }
                }
                keyword lastPop = keyword.kif;
                if (containEnd && keywordStack.Count > 0)
                {
                    lastPop = keywordStack.Pop();
                }

                #endregion

                #region add profiler
                if (containFunction)
                {
                    needEndSample = true;
                    beginSample = true;
                    format = "CS.MikuLuaProfiler.LuaProfiler.BeginSample(\"{0}\")";
                    string funName = GetFunName(line, fileName);
                    ApeendCrLine(sb, line);
                    ApeendCrLine(sb, string.Format(format, funName));
                }
                else if (containEnd && lastPop == keyword.kfunction && beginSample && needEndSample)
                {
                    ApeendCrLine(sb, "CS.MikuLuaProfiler.LuaProfiler.EndSample()");
                    ApeendCrLine(sb, line);
                    beginSample = keywordStack.Count > 0;
                }
                else if (containReturn)
                {
                    line = line.Replace("return",
                        "CS.MikuLuaProfiler.LuaProfiler.EndSample()\r\nreturn");
                    ApeendCrLine(sb, line);
                    if (!keywordStack.Contains(keyword.kfunction))
                    {
                        needEndFileSample = false;
                    }
                }
                else
                {
                    ApeendCrLine(sb, line);
                }
                if (containEnd)
                {
                    ApeendCrLine(sb, "");
                }
                #endregion
            }

            if (needEndFileSample)
            {
                ApeendCrLine(sb, "\r\nCS.MikuLuaProfiler.LuaProfiler.EndSample()");
            }
            string code = sb.ToString().Replace("{\r\n", "{");
            code = code.Replace("\r\n}", "}");
            code = RollBackString(code);

            return PrettySpace(code);
        }

        #region check lua keyword
        private static void ApeendCrLine(StringBuilder sb, string line)
        {
            sb.Append(line + "\r\n");
        }
        private static string GetFunName(string line, string fileName)
        {
            string funName = "";
            if (!Regex.IsMatch(line, @"function\s*\("))
            {
                funName = Regex.Match(line, @"(?<=function).*?(?=\()").Value.Trim();
            }
            if (string.IsNullOrEmpty(funName))
            {
                funName = "anonymous";
            }
            return string.Format("{0},line:%d funName:{1}", fileName, funName);
        }
        private static bool CheckIsLeftTable(string line)
        {
            return line.Contains("{");
        }
        private static bool CheckIsRightTable(string line)
        {
            return line.Contains("}");
        }
        private static bool CheckIsEndLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))end(?=(,|$|\s|\)))");
        }
        private static bool CheckIsReturnLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))return(?=(\s|\(|$))");
        }
        private static bool CheckIsLocalLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))local(?=(\s|\(|$))");
        }
        private static bool CheckIsFunctionLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(\(|,|^|\s|\=))function(?=(\s|\())");
        }
        private static bool CheckIsForLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))for(?=(\s|\(|$))");
        }
        private static bool CheckIsIfLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))if(?=(\s|\(|$))");
        }
        private static bool CheckIsWhileLine(string line)
        {
            return Regex.IsMatch(line, @"(?<=(^|\s))while(?=(\s|\(|$))");
        }
        #endregion

        #region pretty code
        private static string PrettySpace(string allCode)
        {
            StringBuilder sb = new StringBuilder();
            StringReader sr = new StringReader(allCode);
            Stack<keyword> keywordStack = new Stack<keyword>();
            int lineIndex = 0;
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine().Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                lineIndex++;

                //bool containLocal = CheckIsLocalLine(line);
                bool containFunction = CheckIsFunctionLine(line);
                bool containFor = CheckIsForLine(line);
                bool containIf = CheckIsIfLine(line);
                bool containWhile = CheckIsWhileLine(line);
                bool containEnd = CheckIsEndLine(line);
                bool containLeft = CheckIsLeftTable(line);
                bool containRight = CheckIsRightTable(line);

                keyword popValue = keyword.knull;
                if ((containEnd || (containRight && !containLeft)) && !containIf && keywordStack.Count > 0)
                {
                    popValue = keywordStack.Pop();
                }
                int count = keywordStack.Count;
                if (Regex.IsMatch(line, @"(?<=(^|\s))else(?=(\s|$))"))
                {
                    count--;
                }
                else if (Regex.IsMatch(line, @"(?<=(^|\s))elseif(?=(\s|$))"))
                {
                    count--;
                }

                line = new string(' ', 4 * Math.Max(0, count)) + line;

                ApeendCrLine(sb, line.Replace("line:%d", string.Format("line:{0}", lineIndex)));
                if (containEnd && !containIf && popValue != keyword.kif)
                {
                    ApeendCrLine(sb, "");
                    lineIndex++;
                }


                #region key word
                if (containFunction) keywordStack.Push(keyword.kfunction);//if (containFunction && !containLocal) keywordStack.Push(keyword.kfunction);
                if (containFor) keywordStack.Push(keyword.kfor);
                if (containIf && !containEnd) keywordStack.Push(keyword.kif);
                if (containWhile) keywordStack.Push(keyword.kwhile);
                if (containLeft && !containRight) keywordStack.Push(keyword.kleftTable);
                #endregion
            }
            return sb.ToString();
        }
        private static string LocalReturnFun(string allCode)
        {
            StringBuilder result = new StringBuilder();
            Stack<keyword> keywordStack = new Stack<keyword>();
            StringReader sr = new StringReader(allCode);
            int localFunCount = 0;
            int localVarCount = 0;
            //int localTBCount = 0;

            bool canStackPush = false;
            while (sr.Peek() != -1)
            {

                string line = sr.ReadLine();
                #region key word
                bool containEnd = CheckIsEndLine(line);
                bool containFunction = CheckIsFunctionLine(line);
                bool containFor = CheckIsForLine(line);
                bool containIf = CheckIsIfLine(line);
                bool containWhile = CheckIsWhileLine(line);
                bool containReturn = CheckIsReturnLine(line);
                bool containLeft = CheckIsLeftTable(line);
                #endregion
                keyword lastPop = keyword.kif;
                if (canStackPush)
                {
                    if (containFunction) keywordStack.Push(keyword.kfunction);
                    if (containFor) keywordStack.Push(keyword.kfor);
                    if (containIf) keywordStack.Push(keyword.kif);
                    if (containWhile) keywordStack.Push(keyword.kwhile);

                    if (containEnd && keywordStack.Count > 0)
                    {
                        lastPop = keywordStack.Pop();
                    }
                }

                if (containReturn)
                {
                    if (containFunction)
                    {
                        returnFunName = "Profiler_Return_FunVar" + localFunCount++;
                        canStackPush = true;
                        keywordStack.Push(keyword.kfunction);
                        line = Regex.Replace(line, @"(?<=(^|\s))return(?=(\s|\(|$))", ReplaceReturnFun);
                    }
                    else
                    {
                        if (line.Trim() != "return")
                        {
                            returnVarName = "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++ + ","
                                + "Profiler_Return_Var" + localVarCount++; //没办法保证一个函数到底返回几个数，所以就搞了5个返回值
                            line = Regex.Replace(line, @"(?<=(^|\s))return(?=(\s|\(|$))", ReplaceReturnVar).TrimEnd();
                            if (containLeft)
                            {
                                line += "\r\n" + GetReturnTable(sr);
                            }
                            line += "\r\nreturn " + returnVarName;
                            returnVarName = string.Empty;
                        }
                    }
                }
                else if (containEnd && lastPop == keyword.kfunction && keywordStack.Count <= 0)
                {
                    line = line + "\r\nreturn " + returnFunName;
                    returnFunName = string.Empty;
                    canStackPush = false;
                }

                ApeendCrLine(result, line);
            }

            return result.ToString();
        }
        static string GetReturnTable(StringReader sr)
        {
            StringBuilder sb = new StringBuilder();
            Stack<keyword> keywordStack = new Stack<keyword>();
            keywordStack.Push(keyword.kleftTable);
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine();
                bool containLeft = CheckIsLeftTable(line);
                bool containRight = CheckIsRightTable(line);

                if (containLeft) keywordStack.Push(keyword.kleftTable);
                ApeendCrLine(sb, line);
                if (containRight) keywordStack.Pop();
                if (keywordStack.Count <= 0) break;
            }

            return sb.ToString();
        }
        private static string NewLineFunction(string allCode)
        {
            StringBuilder result = new StringBuilder();

            StringReader sr = new StringReader(allCode);
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine().Trim();

                #region key word
                bool containEnd = CheckIsEndLine(line);
                bool containFunction = CheckIsFunctionLine(line);
                #endregion

                if (containFunction && containEnd)
                {
                    line = Regex.Replace(line, @"(,|^|\s)function.*?\)", ReplaceBackNewLine);
                    line = Regex.Replace(line, @"(?<=(^|\s))end(?=(,|$|\s|\)))", ReplaceForwardNewLine);
                }
                ApeendCrLine(result, line);
            }

            return result.ToString();
        }
        private static string PrettyReturn(string allCode)
        {
            Regex reg = new Regex(@"(?<=(^|\s))return(?=(\s|\(|$))");
            return reg.Replace(allCode, ReplaceForwardNewLine);
        }
        private static string PrettyEnd(string allCode)
        {
            Regex reg = new Regex(@"(?<=(^|\s))end(?=(,|$|\s|\)))");
            return reg.Replace(allCode, ReplaceForwardNewLine);
        }
        private static string PrettyThen(string allCode)
        {
            Regex reg = new Regex(@"(?<=(^|\s))then(?=(\s)\w)");
            return reg.Replace(allCode, ReplaceBackNewLine);
        }
        private static string ReplaceReturnFun(Match m)
        {
            return m.Value.Replace("return", "local " + returnFunName + " = ");
        }
        private static string ReplaceReturnVar(Match m)
        {
            return m.Value.Replace("return", "local " + returnVarName + " = ");
        }
        private static string ReplaceReturnTb(Match m)
        {
            return m.Value.Replace("return", "local " + returnTbName + " = ");
        }
        private static string ReplaceBackNewLine(Match m)
        {
            return m.Value + "\r\n";
        }
        private static string ReplaceForwardNewLine(Match m)
        {
            return "\r\n" + m.Value;
        }

        private static bool SkipComment(StringLoadInfo sl, int c)
        {
            if (c == (int)'-') return false;
            do
            {
                c = sl.ReadByte();
                if (c == (int)'-')
                {
                    int c1 = sl.ReadByte();
                    if (c1 == (int)'\n') break;
                    int c2 = sl.ReadByte();
                    if (c2 == (int)'\n') break;

                    if (c1 == (int)'[' && c2 == (int)'[')
                    {
                        while (c != -1)
                        {
                            c = sl.ReadByte();
                            if (c == (int)']')
                            {
                                c = sl.ReadByte();
                                if (c == (int)']') break;
                            }
                        }

                    }
                    else
                    {
                        while (c != -1)
                        {
                            if (c == (int)'\n') break;
                            c = sl.ReadByte();
                        }
                    }
                }
            } while (false);
            return true;
        }

        private static int strCount = 0;
        private static Dictionary<int, string> m_dict = new Dictionary<int, string>();
        private static string ReplaceString(StringLoadInfo sl, int c)
        {
            int startPos = sl.pos - 1;
            int len = 1;
            if (c != (int)'"' && c != (int)'\'') return "";
            bool isDouble = c == '"';
            while (c != -1)
            {
                c = sl.ReadByte();
                len++;

                switch (c)
                {
                    case (int)'\n':
                    case (int)'\r':
                        {
                            throw new Exception("string is invalide");
                        }
                    case (int)'\\':
                        {
                            //转义字符下一个字符都不管了
                            c = sl.ReadByte();
                            len++;
                        }
                        break;
                    case (int)'\'':
                        {
                            if (!isDouble)
                            {
                                string replaceStr = "$" + strCount + "$";
                                string strCode = sl.Replace(startPos, len, replaceStr);
                                m_dict.Add(strCount, strCode);
                                strCount++;
                                return replaceStr;
                            }
                        }
                        break;
                    case (int)'"':
                        {
                            if (isDouble)
                            {
                                string replaceStr = "$" + strCount + "$";
                                string strCode = sl.Replace(startPos, len, replaceStr);
                                m_dict.Add(strCount, strCode);
                                strCount++;
                                return replaceStr;
                            }
                        }
                        break;
                }
            }
            return "";
        }
        private static string ReplaceLongString(StringLoadInfo sl, int c)
        {
            int startPos = sl.pos - 2;
            int len = 2;
            while (c != -1)
            {
                c = sl.ReadByte();
                len++;

                switch (c)
                {
                    case (int)']':
                        {
                            int c2 = sl.ReadByte();
                            len++;
                            if (c2 == (int)']')
                            {
                                string replaceStr = "$" + strCount + "$";
                                string strCode = sl.Replace(startPos, len, replaceStr);
                                m_dict.Add(strCount, strCode);
                                strCount++;
                                return replaceStr;
                            }
                        }
                        break;
                }
            }
            return "";
        }

        private static string TrimComment(string allCode)
        {
            StringLoadInfo sl = new StringLoadInfo(allCode);
            int c = sl.ReadByte();
            StringBuilder sb = new StringBuilder();
            while (c != -1)
            {
                begin:
                switch (c)
                {
                    case (int)'-':
                        while (SkipComment(sl, c))
                        {
                            c = sl.ReadByte();
                            goto begin;
                        }
                        break;
                    case '"':
                    case '\'':
                        sb.Append(ReplaceString(sl, c));
                        c = sl.ReadByte();
                        break;
                    case (int)'[':
                        int c2 = sl.ReadByte();
                        if (c2 == (int)'[')
                        {
                            sb.Append(ReplaceLongString(sl, c2));
                            c = sl.ReadByte();
                            goto begin;
                        }
                        else
                        {
                            sl.ReadBack();
                        }
                        break;
                }

                if (c != -1)
                {
                    sb.Append((char)c);
                    c = sl.ReadByte();
                }
            }
            allCode = sb.ToString();


            allCode = Regex.Replace(allCode, "\".*?\"", MatchStrQuote);
            allCode = Regex.Replace(allCode, "'.*?'", MatchStrDouble);
            allCode = allCode.Replace("'", "\\\"");

            allCode = Regex.Replace(allCode, "\".*?\"", MatchStrComment);
            allCode = Regex.Replace(allCode, @"---.*(?=(\n|\r\n|$))", "");
            allCode = Regex.Replace(allCode, @"--\[\[([\s\S]*?)\]\]", "");
            allCode = Regex.Replace(allCode, @"--.*(?=(\n|\r\n|$))", "");
            allCode = Regex.Replace(allCode, "\".*?\"", MatchStrRec);

            allCode = allCode.Replace("\\\"", "'");
            //strCount = 0;
            //m_dict.Clear();
            //allCode = Regex.Replace(allCode, "\".*?\"", MatchStrToNumber);
            //allCode = Regex.Replace(allCode, @"\[\[.*?\]\]", MatchStrToNumber);
            strCount = 0;
            return allCode;
        }

        private static string RollBackString(string allCode)
        {
            allCode = Regex.Replace(allCode, @"\$.*?\$", MatchNumberToStr);

            return allCode;
        }

        private static string MatchNumberToStr(Match mt)
        {
            int value = int.Parse(mt.Value.Replace("$", "").Trim());
            string str = "\"\"";
            m_dict.TryGetValue(value, out str);
            return str;
        }
        private static string MatchStrToNumber(Match mt)
        {
            string result = "$" + strCount + "$";
            m_dict.Add(strCount, mt.Value);
            strCount++;
            return result;
        }

        private static string MatchStrDouble(Match mt)
        {
            return mt.Value.Replace("\"", "\\\"").Replace("'", "\"");
        }

        private static string MatchStrRec(Match mt)
        {
            return mt.Value.Replace("\'", "-");
        }
        private static string MatchStrQuote(Match mt)
        {
            return mt.Value.Replace("'", "\\\"");
        }
        private static string MatchStrComment(Match mt)
        {
            return mt.Value.Replace("-", "\'");
        }
        private static string PrettyOrAnd(string allCode)
        {
            Regex reg = new Regex(@"(\n|\r\n)\s*?(or|and|:|\+|-|\*|/|\%|\^)");
            return reg.Replace(allCode, TrimBeginline);
        }
        private static string PrettyCalSig(string allCode)
        {
            Regex reg = new Regex(@".*(\+|-|\*|/|\%|\^|:|\.)(\n|\r\n)");
            MatchEvaluator evaluator = new MatchEvaluator(TrimEndline);
            return reg.Replace(allCode, evaluator);
        }
        private static string PrettyTable(string allCode)
        {
            allCode = allCode.Replace("{", "{\r\n");
            allCode = allCode.Replace("}", "\r\n}");

            return allCode;
        }
        private static string TrimEndline(Match m)
        {
            string mt = m.Value;
            return mt.Replace("\r", "").Replace("\n", "");
        }

        private static string TrimBeginline(Match m)
        {
            string mt = m.Value;
            if (mt.Contains("\r\n"))
            {
                return " " + mt.Substring(2);
            }
            else
            {
                return " " + mt.Substring(1);
            }
        }
        #endregion


    }
#endif
}

