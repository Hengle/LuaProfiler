using System.Collections.Generic;
using System;
using UnityEngine;
using XLua;

namespace MikuLuaProfiler
{
    public static class LuaExport
    {
        [LuaCallCSharp]
        public static List<Type> LuaCallCSharpTypes = new List<Type>() {
            typeof(System.Reflection.Missing),
            typeof(System.Type),
            typeof(LuaProfiler),
            typeof(LuaProfiler.Sample),
            typeof(List<LuaProfiler.Sample>),
            typeof(LuaProfiler),
            typeof(UnityEngine.Debug),
            typeof(UnityEngine.Light)
        };
    }


}