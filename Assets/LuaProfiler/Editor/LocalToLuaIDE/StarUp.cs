/*        		   
* ==============================================================================
* Filename: StarUp
* Created:  2018/7/2 11:36:16
* Author:   エル・プサイ・コングルゥ
* Purpose:  
* ==============================================================================
*/


using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using RealStatePtr = System.IntPtr;
using LuaAPI = XLua.LuaDLL.Lua;
using CSObjectWrapEditor;

[InitializeOnLoad]
public static class Startup
{
    private static MethodHooker clickhook;
    private static MethodHooker reflectionHook;

    public static readonly string luaPath;
    static Startup()
    {
#if UNITY_EDITOR
        var setting = MikuLuaProfiler.LuaDeepProfilerSetting.Instance;
        bool isDeep = setting.isDeepProfiler;
        if (isDeep)
        {
            luaPath = setting.profilerLuaProjectPath;
        }
        else
        {
            luaPath = setting.luaProjectPath;
        }
#else
        luaPath = Application.persistentDataPath + "/Lua";
#endif
        Update();
        EditorApplication.playModeStateChanged += OnEditorPlaying;
    }

    public static void OnEditorPlaying(PlayModeStateChange playModeStateChange)
    {
        if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("正在编译中");
                EditorApplication.isPlaying = false;
                return;
            }
#if UNITY_EDITOR_WIN
            if (!MikuLuaProfiler.LuaCheckSetting.Instance.Check())
            {
                EditorApplication.isPlaying = false;
                EditorUtility.DisplayDialog("lua error", "some lua error happen please fix them", "OK");
            }
#endif
        }
    }

    static void Update()
    {
        HookClick();
        HookReflection();
    }

    public static void HookClick()
    {
        if (clickhook == null)
        {
            Type typeLogReplace = typeof(Log);
            Type typeLog = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
            MethodInfo clickFun = typeLog.GetMethod("RowGotDoubleClicked", BindingFlags.Static | BindingFlags.Public);
            MethodInfo clickReplace = typeLogReplace.GetMethod("RowGotDoubleClicked");
            MethodInfo clickProxy = typeLogReplace.GetMethod("Proxy", BindingFlags.Public | BindingFlags.Static);
            clickhook = new MethodHooker(clickFun, clickReplace, clickProxy);
            clickhook.Install();
        }
    }

    public static void HookReflection()
    {
        if (reflectionHook == null)
        {
            Type typeUtilsReplace = typeof(Utils);
            Type typeUtils = typeof(XLua.Utils);

            Utils.typeDict.Clear();

            Generator.GetGenConfig(XLua.Utils.GetAllTypes());
            Utils.typeDict = new HashSet<Type>(Generator.LuaCallCSharp);

            MethodInfo clickFun = typeUtils.GetMethod("ReflectionWrap", BindingFlags.Public | BindingFlags.Static);
            MethodInfo clickReplace = typeUtilsReplace.GetMethod("ReflectionWrap");
            MethodInfo clickProxy = typeUtilsReplace.GetMethod("Proxy", BindingFlags.Public | BindingFlags.Static);
            reflectionHook = new MethodHooker(clickFun, clickReplace, clickProxy);
            reflectionHook.Install();

            CodeEmit.typeDict = new HashSet<Type>(Generator.LuaCallCSharp);
            Type emit = typeof(XLua.CodeEmit);
            Type emitReplace = typeof(CodeEmit);

            clickFun = emit.GetMethod("EmitTypeWrap", BindingFlags.Public | BindingFlags.Instance);
            clickReplace = emitReplace.GetMethod("EmitTypeWrap");
            clickProxy = emitReplace.GetMethod("Proxy", BindingFlags.Public | BindingFlags.Static);

            reflectionHook = new MethodHooker(clickFun, clickReplace, clickProxy);
            reflectionHook.Install();

        }
    }

    public static class CodeEmit
    {
        public static HashSet<Type> typeDict = new HashSet<Type>();
        public static HashSet<string> ignoreDict = new HashSet<string> {
            "System.Collections.Generic.Dictionary`2[TKey,TValue]"
        };
        public static void EmitTypeWrap(XLua.CodeEmit emit, Type type)
        {
            if (UnityEngine.Application.isPlaying)
            {
                if (!type.IsSubclassOf(typeof(Delegate))
                    && !type.IsEnum && !typeDict.Contains(type)
                    && !ignoreDict.Contains(type.ToString()))
                {
                    string info = "请把类型:" + string.Format("{0} 导出", type);
                    
                    //int oldTop = LuaAPI.lua_gettop(L);
                    //LuaAPI.load_error_func(L, -1);
                    //LuaAPI.xlua_getglobal(L, "debug");
                    //LuaAPI.lua_pushstring(L, "traceback");
                    //LuaAPI.lua_rawget(L, -2);
                    //LuaAPI.lua_pcall(L, 0, 1, oldTop + 1);
                    //string t = LuaAPI.lua_tostring(L, -1);
                    //info += "\n" + t;
                    //LuaAPI.lua_settop(L, oldTop);

                    UnityEngine.Debug.LogError(info);
                    return;
                }
            }
            Proxy(emit, type);
        }
        public static void Proxy(XLua.CodeEmit emit, Type type)
        {
        }
    }

    public static class Utils
    {
        public static HashSet<Type> typeDict = new HashSet<Type>();
        public static HashSet<string> ignoreDict = new HashSet<string> {
            "System.Collections.Generic.Dictionary`2[TKey,TValue]"
        };
        public static void ReflectionWrap(RealStatePtr L, Type type, bool privateAccessible)
        {
            if (UnityEngine.Application.isPlaying)
            {
                if (!type.IsSubclassOf(typeof(Delegate)) 
                    && !type.IsEnum && !typeDict.Contains(type)
                    && !ignoreDict.Contains(type.ToString()))
                {
                    string info = "请把类型:" + string.Format("{0} 导出", type);

                    int oldTop = LuaAPI.lua_gettop(L);

                    LuaAPI.load_error_func(L, -1);
                    LuaAPI.xlua_getglobal(L, "debug");
                    LuaAPI.lua_pushstring(L, "traceback");
                    LuaAPI.lua_rawget(L, -2);
                    LuaAPI.lua_pcall(L, 0, 1, oldTop + 1);
                    string t = LuaAPI.lua_tostring(L, -1);
                    info += "\n" + t;
                    LuaAPI.lua_settop(L, oldTop);

                    UnityEngine.Debug.LogError(info);
                    return;
                }
            }
            Proxy(L, type, privateAccessible);
        }
        public static void Proxy(RealStatePtr L, Type type, bool privateAccessible)
        {
        }
    }

    public static class Log
    {

        public static void RowGotDoubleClicked(int index)
        {
            bool isLuaOpen = LocalToLuaIDE.OnOpen(-1, index);
            if (!isLuaOpen)
            {
                Proxy(index);
            }
        }

        public static void Proxy(int index)
        {
        }
    }
}