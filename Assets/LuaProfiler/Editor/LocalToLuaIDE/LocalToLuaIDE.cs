using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using UnityEditor.Callbacks;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;


public class LocalToLuaIDE : Editor {
    /// <summary>
    /// 获取窗体的句柄函数
    /// </summary>
    /// <param name="lpClassName">窗口类名</param>
    /// <param name="lpWindowName">窗口标题名</param>
    /// <returns>返回句柄</returns>
    [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hwnd"></param>
    /// <returns></returns>
    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    public static extern IntPtr SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "BringWindowToTop")]
    public static extern IntPtr BringWindowToTop(IntPtr hwnd);
    /// <summary>
    /// 最大化窗口，最小化窗口，正常大小窗口；
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "ShowWindow", CharSet = CharSet.Auto)]
    public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible", CharSet = CharSet.Auto)]
    public static extern bool IsWindowVisible(IntPtr hwnd);


    private const string EXTERNAL_EDITOR_PATH_KEY = "mTv8";
    private const string LUA_PROJECT_ROOT_FOLDER_PATH_KEY = "obUd";

    /// <summary>
    /// 双击console的回调
    /// </summary>
    /// <param name="instanceID"></param>
    /// <param name="line"></param>
    /// <returns></returns>
    public static bool OnOpen(int instanceID, int line) {
        if (!GetConsoleWindowListView() || (object)EditorWindow.focusedWindow != consoleWindow) {
            return false;
        }
        string fileName = GetListViewRowCount(ref line);

        if (fileName == null) {
            return false;
        }
        OnOpenAsset(fileName, line);

        return true;
    }

    public static bool OnOpenAsset(string file, int line) {
        string filePath = file;

        string luaFolderRoot = "Lua";//Startup.luaPath;
        filePath = luaFolderRoot.Trim() + filePath.Trim();//+ ".lua";

        return OpenFileAtLineExternal(filePath, line);
    }

    static bool OpenFileAtLineExternal(string fileName, int line) {
        string editorPath = EditorUserSettings.GetConfigValue(EXTERNAL_EDITOR_PATH_KEY);
        if (string.IsNullOrEmpty(editorPath) || !File.Exists(editorPath)) {   // 没有path就弹出面板设置
            SetExternalEditorPath();
        }
        OpenFileWith(fileName, line);
        return true;
    }

    static void OpenFileWith(string fileName, int line) {
        string editorPath = EditorUserSettings.GetConfigValue(EXTERNAL_EDITOR_PATH_KEY);
        string projectRootPath = EditorUserSettings.GetConfigValue(LUA_PROJECT_ROOT_FOLDER_PATH_KEY);
        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = editorPath;
        string procArgument = "";
        if (editorPath.IndexOf("idea") != -1) {
            procArgument = string.Format("{0} --line {1} {2}", projectRootPath, line, fileName);
        }
        else {
            procArgument = string.Format("{0}:{1}:0", fileName, line);
        }
        proc.StartInfo.UseShellExecute = true;
        proc.StartInfo.Arguments = procArgument;
        proc.Start();
        IntPtr hwd = FindWindow("SunAwtFrame", null);
        if (hwd != IntPtr.Zero && IsWindowVisible(hwd)) {
            //ShowWindow(hwd, 2);
            //SetForegroundWindow(hwd);
            ShowWindow(hwd, 3);
        }
    }

    [MenuItem("Tools/Lua IDE Setting", false, 15)]
    static void SetExternalEditorPath() {
        string path = EditorUserSettings.GetConfigValue(EXTERNAL_EDITOR_PATH_KEY);
        path = EditorUtility.OpenFilePanel("Select Lua IDE", path, "exe");

        if (path != "") {
            EditorUserSettings.SetConfigValue(EXTERNAL_EDITOR_PATH_KEY, path);
            Debug.Log("Set Lua IDE Path: " + path);
        }
    }

    private static object consoleWindow;
    private static object logListView;
    private static FieldInfo logListViewCurrentRow;
    private static MethodInfo LogEntriesGetEntry;
    private static object logEntry;
    private static FieldInfo logEntryCondition;
    private static bool GetConsoleWindowListView() {
        if (logListView == null) {
            Assembly unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
            Type consoleWindowType = unityEditorAssembly.GetType("UnityEditor.ConsoleWindow");
            FieldInfo fieldInfo = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
            consoleWindow = fieldInfo.GetValue(null);

            if (consoleWindow == null) {
                logListView = null;
                return false;
            }

            FieldInfo listViewFieldInfo = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);
            logListView = listViewFieldInfo.GetValue(consoleWindow);
            logListViewCurrentRow = listViewFieldInfo.FieldType.GetField("row", BindingFlags.Instance | BindingFlags.Public);

            Type logEntriesType = unityEditorAssembly.GetType("UnityEditor.LogEntries");
            LogEntriesGetEntry = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
            Type logEntryType = unityEditorAssembly.GetType("UnityEditor.LogEntry");
            logEntry = Activator.CreateInstance(logEntryType);
            logEntryCondition = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);
        }

        return true;
    }
    private static string GetListViewRowCount(ref int line) {
        int row = (int)logListViewCurrentRow.GetValue(logListView);
        LogEntriesGetEntry.Invoke(null, new object[] { row, logEntry });
        string condition = logEntryCondition.GetValue(logEntry) as string;

        condition = GetLuaLine(condition);
        string[] strs = condition.Split(new char[] { ':' });

        if (strs.Length < 2) {
            return null;
        }
        line = 0;

        if (!int.TryParse(strs[1], out line)) {
            return null;
        }
        return "/" + strs[0].Replace(".", "/").Trim() + ".lua";
    }

    private static string GetLuaLine(string line)
    {
        string result = Regex.Match(line, @"(?<=(\<i\>)).*?(?=(\</i\>))").ToString();

        if (!string.IsNullOrEmpty(result)) {
            return result;
        }
        if (Regex.IsMatch(line, @"(?<=(Exception: )).*:\d*(?=(:))"))
        {
            return Regex.Match(line, @"(?<=(Exception: )).*:\d*(?=(:))").ToString().Replace(".lua", "");
        }
        else if (Regex.IsMatch(line, @"(?<=(LUA: )).*:\d*(?=(:))"))
        {
            return Regex.Match(line, @"(?<=(LUA: )).*:\d*(?=(:))").ToString().Replace(".lua", "");
        }
        else if (Regex.IsMatch(line, @"(?<=(Warning: )).*:\d*(?=(:))"))
        {
            return Regex.Match(line, @"(?<=(Warning: )).*:\d*(?=(:))").ToString().Replace(".lua", "");
        }
        else if (Regex.IsMatch(line, @"(?<=(LUA ERROR :)).*:\d*(?=(:))"))
        {
            return Regex.Match(line, @"(?<=(LUA ERROR :)).*:\d*(?=(:))").ToString().Replace(".lua", "");
        }
        else if (Regex.IsMatch(line, @"(?<=(Lua\\)).*.lua:.*?(?=(:))"))
        {
            return Regex.Match(line, @"(?<=(Lua\\)).*.lua:.*?(?=(:))").ToString().Replace(".lua", "");
        }
        else if (Regex.IsMatch(line, "LuaException: \n	no such file \'.*\' in CustomLoaders!\nstack traceback:"))
        {
            string[] strList = line.Split('\n');
            string str = strList[5].Trim();

            return Regex.Match(str, @".*:\d+").ToString().Replace(".lua", "");
        }
        return "";
    }
}

