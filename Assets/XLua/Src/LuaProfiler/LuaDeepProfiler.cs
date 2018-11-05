using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MikuLuaProfiler
{
    using UniLua;
#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(LuaDeepProfilerSetting))]
    public class LuaDeepProfiler : Editor
    {
        private static string rootDirPath = "";
        private static string rootProfilerDirPath = "";
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


                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();  //开始监视代码运行时间
                FindFunctionChunk(info);
                watch.Stop();  //停止监视
                TimeSpan timespan = watch.Elapsed;  //获取当前实例测量得出的总时间
                UnityEngine.Debug.LogFormat("Parse 时间：{0}(毫秒)", timespan.TotalMilliseconds);  //总毫秒数
                
                if (!isHook)
                {
                    UnityEditor.EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e)
            {
                throw e;
                //Debug.LogError(e.Message);
            }
            EditorUtility.SetDirty(LuaDeepProfilerSetting.Instance);
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

        #region parse
        public static string InsertSample(string value, string name)
        {
            LLex l = new LLex(new StringLoadInfo(value), name);
            l.InsertString(0, "BeginMikuSample(\"" + name +", line:1 require file\")\r\n");
            int lastPos = 0;
            int nextPos = l.pos;
            l.Next();
            int tokenType = l.Token.TokenType;

            lastPos = nextPos;
            nextPos = l.pos;

            InsertSample(l, ref lastPos, ref nextPos, tokenType, false);

            return l.code;
        }

        static void InsertSample(LLex l, ref int lastPos, ref int nextPos, int tokenType, bool onlyFun)
        {
            Stack<int> tokens = new Stack<int>();

            bool needLastSample = true;
            bool hasReturn = false;
            int lastStackToken = -1;
            while (tokenType != (int)TK.EOS)
            {
                switch (tokenType)
                {
                    case (int)TK.FUNCTION:
                        hasReturn = false;
                        tokens.Push(tokenType);
                        lastStackToken = tokenType;
                        string funName = "";
                        bool isLeft = false;

                        while (tokenType != (int)TK.EOS)
                        {
                            l.Next();
                            tokenType = l.Token.TokenType;

                            lastPos = nextPos;
                            nextPos = l.pos;
                            if (!isLeft)
                            {
                                if (l.Token is NameToken)
                                {
                                    funName += ((NameToken)l.Token).SemInfo;
                                }
                                else if ((l.Token.TokenType == (int)':'))
                                {
                                    funName += ':';
                                }
                                else if ((l.Token.TokenType == (int)'.'))
                                {
                                    funName += '.';
                                }
                            }


                            if (tokenType == (int)'(')
                            {
                                isLeft = true;
                            }

                            if (tokenType == (int)')')
                            {
                                l.InsertString(nextPos, "\r\nBeginMikuSample(\"" + l.Source + ",line:" + l.LineNumber + " funName:" + funName + "\")\r\n");
                                nextPos = l.pos;
                                break;
                            }
                        }
                        break;
                    case (int)TK.IF:
                    case (int)TK.FOR:
                    case (int)TK.WHILE:
                        if (tokens.Count > 0)
                        {
                            tokens.Push(tokenType);
                            lastStackToken = tokenType;
                        }
                        break;
                    case (int)TK.RETURN:
                        int insertPos = lastPos - 1;

                        if (tokens.Count == 0)
                        {
                            needLastSample = false;
                        }

                        while (tokenType != (int)TK.EOS)
                        {
                            l.Next();

                            tokenType = l.Token.TokenType;

                            lastPos = nextPos;
                            nextPos = l.pos;

                            if (tokenType == (int)TK.FUNCTION)
                            {
                                InsertSample(l, ref lastPos, ref nextPos, tokenType, true);
                                tokenType = l.Token.TokenType;
                            }

                            if (tokenType == (int)TK.END
                                || tokenType == (int)TK.ELSEIF 
                                || tokenType == (int)TK.ELSE 
                                || tokenType == (int)TK.EOS)
                            {
                                string returnStr = l.ReadString(insertPos, lastPos - 1); ;

                                returnStr = returnStr.Trim();
                                returnStr = "\r\nreturn miku_unpack_return_value(" + returnStr.Substring(6, returnStr.Length - 6).Trim() + ")\r\n";

                                l.Replace(insertPos, lastPos - 1, returnStr);
                                nextPos = l.pos;
                                if (tokenType == (int)TK.END)
                                {
                                    if (onlyFun)
                                    {
                                        l.Next();
                                        lastPos = nextPos;
                                        nextPos = l.pos;
                                        return;
                                    }
                                    else
                                    {
                                        tokens.Pop();
                                    }
            
                                }
                                break;
                            }
                        }

                        if (lastStackToken != (int)TK.IF)
                        {
                            hasReturn = true;
                        }
                        break;
                    case (int)TK.END:
                        if (tokens.Count > 0)
                        {
                            int token = tokens.Pop();
                            if (token == (int)TK.FUNCTION)
                            {
                                if (!hasReturn)
                                {
                                    l.InsertString(lastPos, "\r\nEndMikuSample()\r\n");
                                    lastPos = nextPos;
                                    nextPos = l.pos;
                                }
                                if (onlyFun && tokens.Count <= 0)
                                {
                                    l.Next();
                                    lastPos = nextPos;
                                    nextPos = l.pos;
                                    return;
                                }
                            }
                            if (tokens.Count > 0)
                            {
                                var tA = tokens.ToArray();
                                lastStackToken = tA[tA.Length - 1];
                            }
                            hasReturn = false;
                        }
                        break;
                }
                l.Next();
                tokenType = l.Token.TokenType;
                lastPos = nextPos;
                nextPos = l.pos;
            }

            if (needLastSample)
            {
                l.InsertString(nextPos, "\r\nEndMikuSample()");
            }
        }
        #endregion

        private static void FindFunctionChunk(DirectoryInfo dir)
        {
            do
            {
                FileInfo[] files = dir.GetFiles("*" + LuaDeepProfilerSetting.Instance.luaExtern);
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
                    string allCode = File.ReadAllText(item.FullName);

                    allCode = ParseLua(item.FullName.Replace(rootDirPath + "\\", "").Replace("\\", "."), allCode);

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
            string code = InsertSample(allCode, fileName);
            return code;
        }

    }
#endif
}

