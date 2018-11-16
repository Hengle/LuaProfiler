# Profiler使用

在Window页签下点击 Lua Profiler Window打开Profiler窗口

点击窗口上的Deep Profiler功能即可使用性能分析器


**注意事项**


请在 Awake 或者Start的流程中 new LuaEnv,在变量声明处直接 new会导致性能分析器失效

<br/>
![](Assets/XLua/Doc/profiler.png)