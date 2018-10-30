/*
* ==============================================================================
* Filename: LuaCheckSetting
* Created:  2018/7/13 14:29:22
* Author:   エル・プサイ・コングルゥ
* Purpose:  
* ==============================================================================
*/

namespace MikuLuaProfiler
{
    using CSObjectWrapEditor;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using UnityEditor;
    using UnityEngine;
    using XLua;

    public class LuaCheckSetting : ScriptableObject
    {

        #region memeber
        public List<string> globalLuaFun = new List<string>();
        private List<string> globalCSFun = new List<string>();

        [NonSerialized]
        private string checkFile = "config.luacheckrc";
        public const string SettingsAssetName = "LuaCheckSetting";
        [HideInInspector]
        [SerializeField]
        public string luaProjectPath = "Lua";

        private static LuaCheckSetting instance;
        public static LuaCheckSetting Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = AssetDatabase.LoadAssetAtPath<LuaCheckSetting>("Assets/" + SettingsAssetName + ".asset");
                    if (instance == null)
                    {
                        UnityEngine.Debug.Log("Lua Profiler: cannot find integration settings, creating default settings");
                        instance = CreateInstance<LuaCheckSetting>();
                        instance.name = "Lua Profiler Integration Settings";
#if UNITY_EDITOR
                        AssetDatabase.CreateAsset(instance, "Assets/" + SettingsAssetName + ".asset");
#endif
                    }
                }
                return instance;
            }
        }
        #endregion

        #region check
        [MenuItem("Tools/LuaCheck", priority = 11)]
        public static void EditSettings()
        {
            Selection.activeObject = Instance;
#if UNITY_2018_1_OR_NEWER
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
#else
            EditorApplication.ExecuteMenuItem("Window/Inspector");
#endif
        }

        public static bool IsObsolete(MemberInfo mb)
        {
            object[] attrs = mb.GetCustomAttributes(true);

            for (int j = 0; j < attrs.Length; j++)
            {
                Type t = attrs[j].GetType();

                if (t == typeof(System.ObsoleteAttribute) || t == typeof(XLua.BlackListAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        public static string TypeDecl(Type t, bool isFull = true)
        {
            string result = "";
            if (t.IsGenericType)
            {
                string ret = GenericBaseName(t, isFull);

                string gs = "";
                gs += "<";
                Type[] types = t.GetGenericArguments();
                for (int n = 0; n < types.Length; n++)
                {
                    gs += TypeDecl(types[n]);
                    if (n < types.Length - 1)
                        gs += ",";
                }
                gs += ">";

                ret = Regex.Replace(ret, @"`\d", gs);

                result = ret;
            }
            else if (t.IsArray)
            {
                result = TypeDecl(t.GetElementType()) + "[]";
            }
            else
            {
                result = RemoveRef(t.ToString(), false);
            }
            result = result.Replace("<", "_")
                    .Replace(",", "_").Replace(">", "");
            return "CS." + result;
        }

        private static string GenericBaseName(Type t, bool isFull)
        {
            string n;
            if (isFull)
            {
                n = t.FullName;
            }
            else
            {
                n = t.Name;
            }
            if (n.IndexOf('[') > 0)
            {
                n = n.Substring(0, n.IndexOf('['));
            }
            return n.Replace("+", ".");
        }

        static string[] prefix = new string[] { "System.Collections.Generic" };
        private static string RemoveRef(string s, bool removearray = true)
        {
            if (s.EndsWith("&")) s = s.Substring(0, s.Length - 1);
            if (s.EndsWith("[]") && removearray) s = s.Substring(0, s.Length - 2);
            if (s.StartsWith(prefix[0])) s = s.Substring(prefix[0].Length + 1, s.Length - prefix[0].Length - 1);

            s = s.Replace("+", ".");
            if (s.Contains("`"))
            {
                string regstr = @"`\d";
                Regex r = new Regex(regstr, RegexOptions.None);
                s = r.Replace(s, "");
                s = s.Replace("[", "<");
                s = s.Replace("]", ">");
            }
            return s;
        }
        #endregion

        #region private
        private void ResetCSGlobalArgs()
        {
            globalCSFun.Clear();
            Generator.GetGenConfig(Utils.GetAllTypes());
            var typeList = Generator.LuaCallCSharp;
            BindingFlags bindType = BindingFlags.DeclaredOnly |
                                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;
            foreach (var t in typeList)
            {
                if (IsObsolete(t)) continue;

                var fields = t.GetFields(bindType);
                foreach (var field in fields)
                {
                    if (IsObsolete(field))
                    {
                        continue;
                    }
                    globalCSFun.Add(TypeDecl(t) + "." + field.Name);
                }

                var properties = t.GetProperties(bindType);
                foreach (var property in properties)
                {
                    if (IsObsolete(property))
                    {
                        continue;
                    }
                    globalCSFun.Add(TypeDecl(t) + "." + property.Name);
                }

                var methods = t.GetMethods(bindType);

                HashSet<string> methodDict = new HashSet<string>();

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    string methodName = method.Name;
                    if (IsObsolete(method))
                    {
                        continue;
                    }
                    if (method.IsGenericMethod) { continue; }
                    if (!method.IsPublic) continue;
                    if (methodName.StartsWith("get_") || methodName.StartsWith("set_")) continue;

                    if (!methodDict.Contains(methodName))
                    {
                        methodDict.Add(methodName);
                        globalCSFun.Add(TypeDecl(t) + "." + methodName);
                    }
                }

            }
        }

        public void ResetConfig()
        {
            ResetCSGlobalArgs();
            for (int key = globalLuaFun.Count - 1; key >= 0; key--)
            {
                var item = globalLuaFun[key];
                if (string.IsNullOrEmpty(item))
                {
                    globalLuaFun.RemoveAt(key);
                }
            }
            RefreshLuaGlobals();
            RefreshCSGlobals();
            //UnityEngine.Debug.Log("<color=#00ff00>Reset Check Config Sucess</color>");
            EditorUtility.SetDirty(this);
        }

        private void RefreshLuaGlobals()
        {
            if (!File.Exists(checkFile))
            {
                CreateCheckFile();
            }
            string text = File.ReadAllText(checkFile);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--begin lua globals");
            foreach (var item in globalLuaFun)
            {
                sb.AppendLine(string.Format("'{0}',", item));
            }
            sb.AppendLine("--end lua globals");
            var rep = Regex.Match(text, @"--begin lua globals[\s\S]*--end lua globals\r\n");
            if (rep.Success)
            {
                text = text.Replace(rep.Value, sb.ToString());
            }

            File.WriteAllText(checkFile, text);
        }

        private void RefreshCSGlobals()
        {
            if (!File.Exists(checkFile))
            {
                CreateCheckFile();
            }
            string text = File.ReadAllText(checkFile);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--begin cs globals");
            foreach (var item in globalCSFun)
            {
                sb.AppendLine(string.Format("'{0}',", item));
            }
            sb.AppendLine("--end cs globals");

            text = Regex.Replace(text, @"--begin cs globals[\s\S]*--end cs globals\r\n", sb.ToString());
            File.WriteAllText(checkFile, text);
        }

        private void CreateCheckFile()
        {
            if (File.Exists(checkFile))
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("globals = {");

            sb.AppendLine("--begin lua globals");
            sb.AppendLine("--end lua globals");

            sb.AppendLine("--begin cs globals");
            sb.AppendLine("--end cs globals");

            sb.AppendLine("--begin proto globals");
            sb.AppendLine("--end proto globals");

            sb.Append("}");
            File.WriteAllText(checkFile, sb.ToString());
        }
        #endregion

        public static void ClearConsole()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.ActiveEditorTracker));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }

        public bool Check()
        {
            return true;
            /*
            ResetConfig();
            ClearConsole();
            StringBuilder sb = new StringBuilder();
            sb.Append(luaProjectPath);
            sb.Append(" --std=lua53c --no-self ");
            sb.Append(" --globals");
            sb.Append(" --config " + checkFile);

            bool checkResult = ProcessCommand(sb.ToString());
            if (checkResult)
            {
                UnityEngine.Debug.Log("<color=#00ff00>Check OK</color>");
            }

            return checkResult;*/
        }

        public static bool ProcessCommand(string argument)
        {
            bool result = true;
            Process process = new Process();
            process.StartInfo.FileName = "Tools/luacheck.exe";

            process.StartInfo.Arguments = argument;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.RedirectStandardOutput = true;
            //process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string line = "";
            while (!process.StandardOutput.EndOfStream)
            {
                line = process.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(line.Trim()))
                {
                    continue;
                }
                if (!line.Contains(" OK"))
                {
                    if (line.Contains("Total:"))
                    {
                        UnityEngine.Debug.Log("<color=#00ffff>" + line.Trim() + "</color>");
                    }
                    else if (!line.Contains("Checking "))
                    {
                        UnityEngine.Debug.LogError(line.Trim());
                        result = false;
                    }
                }
                //else
                //{
                //    UnityEngine.Debug.Log("<color=#00ff00>" + line + "</color>");
                //}
            }

            process.WaitForExit();
            process.Close();

            return result;
        }
    }
}