#pragma warning disable CS0219//故意在c#这里产生于lua那边的等量GC
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif
using XLua;
using LuaLib = XLua.LuaDLL.Lua;
using LuaCSFunction = XLua.LuaDLL.lua_CSFunction;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using System.IO;
using UnityEditorInternal;
using UnityEditor;

namespace MikuLuaProfiler
{

    [InitializeOnLoad]
    static class HookSetup
    {
        static HookSetup()
        {
            if (LuaDeepProfilerSetting.Instance.isDeepProfiler)
            {
                LuaDeepProfiler.Start(true);
            }
            //HookLuaFuns();
            EditorApplication.playModeStateChanged += OnEditorPlaying;
        }

        public static void OnEditorPlaying(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                LuaProfiler.SetMainLuaEnv(null);
                Uninstall();
            }
        }

        #region hook

        #region hook tostring
        public class WeakDictionary<K, V>
        {
            readonly Dictionary<K, WeakReference> _dict;
            public WeakDictionary()
            {
                _dict = new Dictionary<K, WeakReference>();
            }

            public WeakDictionary(int capacity)
            {
                _dict = new Dictionary<K, WeakReference>(capacity);
            }

            public V this[K key]
            {
                get
                {
                    WeakReference w = _dict[key];
                    // IsAlive is not reliable in IL2CPP
                    V value = (V)w.Target;
                    if (value != null)
                        return value;
                    return default(V);
                }

                set
                {
                    Add(key, value);
                }
            }

            public int Count
            {
                get
                {
                    int result = 0;
                    lock (this)
                    {
                        result = _dict.Count;
                    }
                    return result;
                }
            }

            public int AliveCount
            {
                get
                {
                    int cnt = 0;
                    lock (this)
                    {
                        foreach (var pair in _dict)
                        {
                            if (pair.Value.IsAlive && ((V)pair.Value.Target) != null)
                            {
                                cnt++;
                            }
                        }
                    }
                    return cnt;
                }
            }
            public void Clear()
            {
                lock (this)
                {
                    _dict.Clear();
                }
            }
            public void Add(K key, V value)
            {
                lock (this)
                {
                    if (_dict.ContainsKey(key))
                    {
                        if (_dict[key].Target != null)
                            throw new ArgumentException("key exists");

                        _dict[key].Target = value;
                    }
                    else
                    {
                        WeakReference w = new WeakReference(value);
                        _dict.Add(key, w);
                    }
                }
            }

            public bool ContainsKey(K key)
            {
                return _dict.ContainsKey(key);
            }
            public bool Remove(K key)
            {
                bool result = false;
                lock (this)
                {
                    result = _dict.Remove(key);
                }
                return result;
            }
            public bool TryGetValue(K key, out V value)
            {
                WeakReference w;
                if (_dict.TryGetValue(key, out w))
                {
                    value = (V)w.Target;
                    return value != null;
                }
                value = default(V);
                return false;

            }
        }

        public class LuaDll
        {
            #region luastring
            const int MAX_WEAK_STRING = 1000;
            public static readonly WeakDictionary<long, LuaString> weakDictionary = new WeakDictionary<long, LuaString>();
            public static bool TryGetLuaString(IntPtr p, out string result)
            {
                LuaString ls;

                bool ret = weakDictionary.TryGetValue((long)p, out ls);
                if (ret)
                {
                    result = ls.value;
                }
                else
                {
                    result = string.Empty;
                }
                return ret;
            }
            public static void RefString(IntPtr strPoint, int index, string s, IntPtr L)
            {
                //超过缓存上限就等着 释放吧
                if (weakDictionary.Count >= MAX_WEAK_STRING)
                {
                    return;
                }

                int oldTop = LuaLib.lua_gettop(L);
                LuaLib.lua_pushvalue(L, index);
                int refIndex = LuaLib.luaL_ref(L, LuaIndexes.LUA_REGISTRYINDEX);
                LuaLib.lua_settop(L, oldTop);
                //TODO 这里要不要改下，暂时 XLUA没有 用L查询 LuaEnv的接口
                LuaString ls = new LuaString(refIndex, LuaProfiler.mainEnv, s, strPoint);
                weakDictionary[(long)strPoint] = ls;
            }
            public class LuaString : LuaBase
            {
                public string value { get; set; }
                public IntPtr strPoint { get; set; }

                public LuaString(int reference, LuaEnv luaenv, string value, IntPtr strPoint) : base(reference, luaenv)
                {
                    this.value = value;
                    this.strPoint = strPoint;
                }

                ~LuaString()
                {
                    Dispose(false);
                }

                public override void Dispose(bool disposeManagedResources)
                {
                    weakDictionary.Remove((long)strPoint);
                    base.Dispose(disposeManagedResources);
                }
            }
            #endregion

            public static string lua_tostring(IntPtr L, int index)
            {
                IntPtr strlen;

                IntPtr str = LuaLib.lua_tolstring(L, index, out strlen);
                if (str != IntPtr.Zero)
                {
#if XLUA_GENERAL || (UNITY_WSA && !UNITY_EDITOR)
                int len = strlen.ToInt32();
                byte[] buffer = new byte[len];
                Marshal.Copy(str, buffer, 0, len);
                return Encoding.UTF8.GetString(buffer);
#else
                    string ret;
                    if (TryGetLuaString(str, out ret))
                    {
                        return ret;
                    }

                    ret = Marshal.PtrToStringAnsi(str, strlen.ToInt32());
                    if (ret == null)
                    {
                        int len = strlen.ToInt32();
                        byte[] buffer = new byte[len];
                        Marshal.Copy(str, buffer, 0, len);
                        ret = Encoding.UTF8.GetString(buffer);
                    }
                    if (ret != null)
                    {
                        RefString(str, index, ret, L);
                    }
                    return ret;
#endif
                }
                else
                {
                    return null;
                }
            }

            public static string PoxyToString(IntPtr L, int index)
            {
                return null;
            }
        }
        #endregion

        #region hook loader
        public class LuaLoader
        {
            private static bool m_isInited = false;
            private static readonly Dictionary<string, string> m_pathDict = new Dictionary<string, string>();
            private static void InitPathDict()
            {
                if (m_isInited) return;
                string[] paths = null;
                m_pathDict.Clear();

                LuaDeepProfilerSetting ls = LuaDeepProfilerSetting.Instance;
                string luaPath = !ls.isDeepProfiler ? ls.luaProjectPath : ls.profilerLuaProjectPath;
                luaPath = luaPath.Replace('\\', '/');

                if (Directory.Exists(luaPath))
                {
                    paths = Directory.GetFiles(luaPath,"*" + LuaDeepProfilerSetting.Instance.luaExtern, SearchOption.AllDirectories);
                }
                if (paths == null) throw new System.Exception("lua director not exit");

                for (int i = 0, imax = paths.Length; i < imax; i++)
                {
                    string thePath = paths[i].Replace('\\', '/');
                    string name = Path.GetFileName(thePath);
                    string ext = name.Split(new char[] { '.' }, 2)[1];

                    string fileName = thePath.Replace(luaPath + "/", "").Replace(LuaDeepProfilerSetting.Instance.luaExtern, "").Replace("/", ".").ToLower();

                    m_pathDict.Add(fileName, thePath);
                }
                m_isInited = true;
            }
            private static byte[] CustomLoader(ref string filename)
            {
                if (!LuaDeepProfilerSetting.Instance.isDeepProfiler)
                {
                    return null;
                }
                byte[] result = null;
                string path = null;
                filename = filename.ToLower();
                path = m_pathDict[filename];
                if (File.Exists(path))
                {
                    result = File.ReadAllBytes(path);
                }
                else
                {
                    throw new Exception(string.Format("{0} not exit", filename));
                }

                return result;
            }

            public void AddLoader(XLua.LuaEnv.CustomLoader loader)
            {
                bool isDeep = LuaDeepProfilerSetting.Instance.isDeepProfiler;
                if (isDeep)
                {
                    InitPathDict();
                    Proxy(CustomLoader);

                }
                Proxy(loader);
            }
            public void Proxy(XLua.LuaEnv.CustomLoader loader)
            {
            }

            public static void AddSearcher(LuaEnv env, LuaCSFunction searcher, int index)
            {
                bool isDeep = LuaDeepProfilerSetting.Instance.isDeepProfiler;
                if (isDeep)
                {
                    env.AddLoader(CustomLoader);

                }
                ProxySearcher(env, searcher, index);
            }

            public static void ProxySearcher(LuaEnv env, LuaCSFunction searcher, int index)
            {

            }
        }
        #endregion

        #region hook profiler
        public class Profiler
        {
            private static Stack<string> m_Stack = new Stack<string>();
            private static int m_currentFrame = 0;
            public static void BeginSampleOnly(string name)
            {
                if (ProfilerDriver.deepProfiling) return;
                if (Time.frameCount != m_currentFrame)
                {
                    m_Stack.Clear();
                    m_currentFrame = Time.frameCount;
                }
                m_Stack.Push(name);
                ProxyBeginSample(name);
            }
            public static void BeginSample(string name, UnityEngine.Object targetObject)
            {
                if (ProfilerDriver.deepProfiling) return;
                m_Stack.Push(name);
                ProxyBeginSample(name, targetObject);
            }

            public static void EndSample()
            {
                if (ProfilerDriver.deepProfiling) return;
                if (m_Stack.Count <= 0)
                {
                    return;
                }
                m_Stack.Pop();
                ProxyEndSample();
            }

            public static void ProxyBeginSample(string name)
            {
            }
            public static void ProxyBeginSample(string name, UnityEngine.Object targetObject)
            {
            }

            public static void ProxyEndSample()
            {
            }
        }
        #endregion

        #region do hook
        private static MethodHooker beginSampeOnly;
        private static MethodHooker beginObjetSample;
        private static MethodHooker endSample;
        private static MethodHooker tostringHook;
        private static MethodHooker loaderHook;

        private static bool m_hooked = false;
        public static void HookLuaFuns()
        {
            if (m_hooked) return;
            if (tostringHook == null)
            {
                Type typeLogReplace = typeof(LuaDll);
                Type typeLog = typeof(LuaLib);
                MethodInfo tostringFun = typeLog.GetMethod("lua_tostring");
                MethodInfo tostringReplace = typeLogReplace.GetMethod("lua_tostring");
                MethodInfo tostringProxy = typeLogReplace.GetMethod("ProxyToString");

                tostringHook = new MethodHooker(tostringFun, tostringReplace, tostringProxy);
                tostringHook.Install();
            }

            if (loaderHook == null)
            {
                Type typeLoadReplace = typeof(LuaLoader);
                Type typeEnv = typeof(XLua.LuaEnv);
                MethodInfo loaderFun = typeEnv.GetMethod("AddLoader");
                MethodInfo loaderReplace = typeLoadReplace.GetMethod("AddLoader");
                MethodInfo loaderProxy = typeLoadReplace.GetMethod("Proxy");

                loaderHook = new MethodHooker(loaderFun, loaderReplace, loaderProxy);
                loaderHook.Install();

                MethodInfo searchFun = typeEnv.GetMethod("AddSearcher", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo searchReplace = typeLoadReplace.GetMethod("AddSearcher", BindingFlags.Public | BindingFlags.Static);
                MethodInfo searchProxy = typeLoadReplace.GetMethod("ProxySearcher", BindingFlags.Public | BindingFlags.Static);

                loaderHook = new MethodHooker(searchFun, searchReplace, searchProxy);
                loaderHook.Install();
            }

            if (beginSampeOnly == null)
            {
                Type typeTarget = typeof(UnityEngine.Profiling.Profiler);
                Type typeReplace = typeof(Profiler);

                MethodInfo hookTarget = typeTarget.GetMethod("BeginSampleOnly", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                MethodInfo hookReplace = typeReplace.GetMethod("BeginSampleOnly");
                MethodInfo hookProxy = typeReplace.GetMethod("ProxyBeginSample", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                beginSampeOnly = new MethodHooker(hookTarget, hookReplace, hookProxy);
                beginSampeOnly.Install();

                hookTarget = typeTarget.GetMethod("BeginSample", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(UnityEngine.Object) }, null);
                hookReplace = typeReplace.GetMethod("BeginSample");
                hookProxy = typeReplace.GetMethod("ProxyBeginSample", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(UnityEngine.Object) }, null);
                beginObjetSample = new MethodHooker(hookTarget, hookReplace, hookProxy);
                beginObjetSample.Install();

                hookTarget = typeTarget.GetMethod("EndSample", BindingFlags.Public | BindingFlags.Static, null, new Type[] { }, null);
                hookReplace = typeReplace.GetMethod("EndSample");
                hookProxy = typeReplace.GetMethod("ProxyEndSample", BindingFlags.Public | BindingFlags.Static, null, new Type[] { }, null);
                endSample = new MethodHooker(hookTarget, hookReplace, hookProxy);
                endSample.Install();
            }

            m_hooked = true;
        }

        public static void Uninstall()
        {
            if (beginSampeOnly != null)
            {
                beginSampeOnly.Uninstall();
                beginSampeOnly = null;
            }
            if (beginObjetSample != null)
            {
                beginObjetSample.Uninstall();
                beginObjetSample = null;
            }
            if (endSample != null)
            {
                endSample.Uninstall();
                endSample = null;
            }
            if (tostringHook != null)
            {
                tostringHook.Uninstall();
                tostringHook = null;
            }
            if (loaderHook != null)
            {
                loaderHook.Uninstall();
                loaderHook = null;
            }

            m_hooked = false;
        }
        #endregion

        #endregion
    }

    public class LuaProfiler
    {
        public static LuaEnv mainEnv
        {
            get
            {
                return _mainEnv;
            }
        }
        private static LuaEnv _mainEnv;
        public static void SetMainLuaEnv(LuaEnv env)
        {
            _mainEnv = env;
            HookSetup.HookLuaFuns();
        }

        public static string GetLuaMemory()
        {
            long result = 0;
            if (_mainEnv != null)
            {
                result = GetLuaMemory(_mainEnv.L);
            }

            return GetMemoryString(result);
        }

        public static long GetLuaMemory(IntPtr luaState)
        {
            long result = 0;

            result = LuaLib.lua_gc(luaState, LuaGCOptions.LUA_GCCOUNT, 0);
            result = result * 1024 + LuaLib.lua_gc(luaState, LuaGCOptions.LUA_GCCOUNTB, 0);

            return result;
        }

        public class Sample
        {
            private static ObjectPool<Sample> samplePool = new ObjectPool<Sample>(250);
            public static Sample Create(float time, long memory, string name)
            {
                Sample s = samplePool.GetObject();
                s.currentTime = time;
                s.currentLuaMemory = memory;
                s.realCurrentLuaMemory = memory;
                s.costGC = 0;
                s.name = name;
                s.costTime = 0;
                s.childs.Clear();
                s.mirrorList.Clear();
                s._father = null;

                return s;
            }

            public void Restore(bool isMirror = false)
            {
                for (int i = 0, imax = mirrorList.Count; i < imax; i++)
                {
                    mirrorList[i].Restore(true);
                }
                if (!isMirror)
                {
                    for (int i = 0, imax = childs.Count; i < imax; i++)
                    {
                        childs[i].Restore();
                    }
                }
                childs.Clear();
                mirrorList.Clear();
                samplePool.Store(this);
            }

            public bool Compare(Sample sample)
            {
                if (sample == null) return false;
                if (this == sample) return true;
                if (_father.name != sample._father.name) return false;
                if (this.name != sample.name) return false;
                if (this.childs.Count != sample.childs.Count) return false;
                for (int i = 0, imax = sample.childs.Count; i < imax; i++)
                {
                    var item = sample.childs[i];
                    if (!this.childs[i].Compare(item))
                    {
                        return false;
                    }
                }
                return true;
            }
            public int oneFrameCall
            {
                get
                {
                    return 1 + mirrorList.Count;
                }
            }
            public float currentTime { private set; get; }
            public long realCurrentLuaMemory { private set; get; }
            public string name { private set; get; }
            //这玩意在统计的window里面没啥卵用
            public long currentLuaMemory { set; get; }

            private float _costTime;
            public float costTime
            {
                set
                {
                    _costTime = value;
                }
                get
                {
                    float result = _costTime;
                    for (int i = 0, imax = mirrorList.Count; i < imax; i++)
                    {
                        result += mirrorList[i]._costTime;
                    }
                    return result;
                }
            }

            private long _costGC;
            public long costGC
            {
                set
                {
                    _costGC = value;
                }
                get
                {
                    long result = _costGC;
                    for (int i = 0, imax = mirrorList.Count; i < imax; i++)
                    {
                        result += mirrorList[i]._costGC;
                    }
                    return result;
                }
            }
            private Sample _father;
            public Sample fahter
            {
                set
                {
                    _father = value;
                    if (_father != null)
                    {
                        bool needAdd = true;
                        for (int i = 0, imax = _father.childs.Count; i < imax; i++)
                        {
                            var item = _father.childs[i];
                            if (item.Compare(this) && item != this)
                            {
                                needAdd = false;
                                item.AddMirror(this);
                                return;
                            }
                        }
                        if (needAdd)
                        {
                            _father.childs.Add(this);
                        }

                        //SetSliberMirror(_father.fahter, this.fahter);
                    }
                }
                get
                {
                    return _father;
                }
            }

            private static void SetSliberMirror(Sample father, Sample node)
            {
                if (father == null) return;
                bool needAdd = true;
                for (int i = 0, imax = father.childs.Count; i < imax; i++)
                {
                    var item = father.childs[i];
                    if (item.Compare(node) && item != node)
                    {
                        needAdd = false;
                        item.AddMirror(node);
                        if (father.childs.Contains(node))
                        {
                            father.childs.Remove(node);
                        }
                        return;
                    }
                }
                SetSliberMirror(father.fahter, node.fahter);
            }

            private void AddMirror(Sample s)
            {
                mirrorList.Add(s);
                for (int i = 0, imax = childs.Count; i < imax; i++)
                {
                    childs[i].AddMirror(s.childs[i]);
                }
            }
            //如果不为空，这个Sample在统计上会被合并到_mirror中
            public readonly List<Sample> mirrorList = new List<Sample>();
            public readonly List<Sample> childs = new List<Sample>();
        }
        //开始采样时候的lua内存情况，因为中间有可能会有二次采样，所以要丢到一个盏中
        public static readonly List<Sample> beginSampleMemoryStack = new List<Sample>();

        private static Action<Sample> m_SampleEndAction;

        private static bool _stableGC = false;
        public static bool stableGC
        {
            get { return _stableGC; }
        }
        public static void ToggleStableGC()
        {
            _stableGC = !_stableGC;
        }
        private static bool isDeep
        {
            get
            {
#if UNITY_EDITOR
                return ProfilerDriver.deepProfiling;
#else
            return false;
#endif
            }
        }
        public static void SetSampleEnd(Action<Sample> action)
        {
            m_SampleEndAction = action;
        }
        public static void BeginSample(string name)
        {
#if DEBUG
            if (_mainEnv != null)
            {
                BeginSample(_mainEnv.L, name);
            }
#endif
        }
        public static void BeginSample(IntPtr luaState)
        {
#if DEBUG
            BeginSample(luaState, "lua gc");
#endif
        }
        private static int m_currentFrame = 0;
        public static void BeginSample(IntPtr luaState, string name)
        {
            if (m_currentFrame != Time.frameCount)
            {
                PopAllSampleWhenLateUpdate();
                m_currentFrame = Time.frameCount;
            }
#if UNITY_EDITOR
            HookSetup.HookLuaFuns();
#endif

#if DEBUG
            if (beginSampleMemoryStack.Count == 0 && _stableGC)
                LuaLib.lua_gc(luaState, LuaGCOptions.LUA_GCSTOP, 0);

            long memoryCount = GetLuaMemory(luaState);
            Sample sample = Sample.Create(Time.realtimeSinceStartup, memoryCount, name);

            beginSampleMemoryStack.Add(sample);
            if (!isDeep)
            {
                Profiler.BeginSample(name);
            }
#endif
        }
        public static void PopAllSampleWhenLateUpdate()
        {
            for (int i = 0, imax = beginSampleMemoryStack.Count; i < imax; i++)
            {
                var item = beginSampleMemoryStack[i];
                if (item.fahter == null)
                {
                    item.Restore();
                }
            }
            beginSampleMemoryStack.Clear();
        }
        public static void EndSample()
        {
#if DEBUG
            if (_mainEnv != null)
            {
                EndSample(_mainEnv.L);
            }
#endif
        }
        public static void EndSample(IntPtr luaState)
        {
#if DEBUG
            if (beginSampleMemoryStack.Count <= 0)
            {
                return;
            }
            int count = beginSampleMemoryStack.Count;
            Sample sample = beginSampleMemoryStack[beginSampleMemoryStack.Count - 1];
            long oldMemoryCount = sample.currentLuaMemory;
            beginSampleMemoryStack.RemoveAt(count - 1);
            long nowMemoryCount = GetLuaMemory(luaState);
            sample.fahter = count > 1 ? beginSampleMemoryStack[count - 2] : null;

            if (!isDeep)
            {
                long delta = nowMemoryCount - oldMemoryCount;

                long tmpDelta = delta;
                if (delta > 0)
                {
                    delta = Math.Max(delta - 40, 0);//byte[0] 的字节占用是40
                    byte[] luagc = new byte[delta];
                }
                for (int i = 0, imax = beginSampleMemoryStack.Count; i < imax; i++)
                {
                    Sample s = beginSampleMemoryStack[i];
                    s.currentLuaMemory += tmpDelta;
                    beginSampleMemoryStack[i] = s;
                }
                Profiler.EndSample();
            }

            sample.costTime = Time.realtimeSinceStartup - sample.currentTime;
            var gc = nowMemoryCount - sample.realCurrentLuaMemory;
            sample.costGC = gc > 0 ? gc : 0;
            if (beginSampleMemoryStack.Count == 0 && _stableGC)
            {
                LuaLib.lua_gc(luaState, LuaGCOptions.LUA_GCRESTART, 0);
                LuaLib.lua_gc(luaState, LuaGCOptions.LUA_GCCOLLECT, 0);
            }


            if (m_SampleEndAction != null && beginSampleMemoryStack.Count == 0)
            {
                m_SampleEndAction(sample);
            }

            if (sample.fahter == null)
            {
                sample.Restore();
            }
#endif
        }

        const long MaxB = 1024;
        const long MaxK = MaxB * 1024;
        const long MaxM = MaxK * 1024;
        const long MaxG = MaxM * 1024;

        public static string GetMemoryString(long value, string unit = "B")
        {
            string result = null;
            if (value < MaxB)
            {
                result = string.Format("{0}{1}", value, unit);
            }
            else if (value < MaxK)
            {
                result = string.Format("{0:N2}K{1}", (float)value / MaxB, unit);
            }
            else if (value < MaxM)
            {
                result = string.Format("{0:N2}M{1}", (float)value / MaxK, unit);
            }
            else if (value < MaxG)
            {
                result = string.Format("{0:N2}G{1}", (float)value / MaxM, unit);
            }
            return result;
        }
    }
}
#endif

