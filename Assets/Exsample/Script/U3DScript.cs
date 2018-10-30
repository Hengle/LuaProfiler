using System;
using System.IO;
using System.Collections;
using UnityEngine;
using XLua;

public class U3DScript : MonoBehaviour
{
    private LuaEnv _env;

    LuaTable main;
    private Action<LuaTable> start;
    private Action<LuaTable> update;
    private Action<LuaTable> destroy;
    private void Awake()
    {
        _env = new LuaEnv();
#if UNITY_EDITOR
        MikuLuaProfiler.LuaProfiler.SetMainLuaEnv(_env);
#endif
    }
    void Start()
    {
        StartCoroutine(StartLua());
    }

    IEnumerator StartLua()
    {
        yield return null;
        yield return null;
        _env.AddLoader(CustomLoader);

        main = Require("main");

        start = main.Get<string, Action<LuaTable>>("start");
        update = main.Get<string, Action<LuaTable>>("update");
        destroy = main.Get<string, Action<LuaTable>>("ondestroy");

        main.Set<string, Transform>("transform", transform);
        GameObject go = GameObject.Find("Directional light");
        main.Set<string, GameObject>("lightObject", go);

        start(main);
    }


    void Update()
    {
        if (update == null) return;
        update(main);
    }

    private void OnDestroy()
    {
        destroy(main);
    }

    #region custom load
    private const string LUA_EXTENSION = ".lua";
    private const string LUA_PATH = "Lua/";

    public LuaTable Require(string luaFile)
    {
        if (_env == null)
        {
            return null;
        }

        LuaTable result = null;
        var args = _env.DoString(string.Format("return require '{0}'", luaFile));
        if (args != null && args.Length > 0)
        {
            result = args[0] as LuaTable;
        }
        return result;
    }

    private byte[] CustomLoader(ref string filename)
    {
        byte[] result = null;
        string path = null;
        filename = filename.ToLower();
        path = LUA_PATH + filename.Replace(".", "/") + LUA_EXTENSION;

        if (File.Exists(path))
        {
            result = File.ReadAllBytes(path);
        }
        else
        {
            XLua.LuaDLL.Lua.luaL_error(_env.L, string.Format("exception:{0} not exit", path));
        }

        return result;
    }
    #endregion
}
