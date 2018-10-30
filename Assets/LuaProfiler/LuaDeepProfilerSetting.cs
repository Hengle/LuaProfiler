/*
               #########                       
              ############                     
              #############                    
             ##  ###########                   
            ###  ###### #####                  
            ### #######   ####                 
           ###  ########## ####                
          ####  ########### ####               
         ####   ###########  #####             
        #####   ### ########   #####           
       #####   ###   ########   ######         
      ######   ###  ###########   ######       
     ######   #### ##############  ######      
    #######  #####################  ######     
    #######  ######################  ######    
   #######  ###### #################  ######   
   #######  ###### ###### #########   ######   
   #######    ##  ######   ######     ######   
   #######        ######    #####     #####    
    ######        #####     #####     ####     
     #####        ####      #####     ###      
      #####       ###        ###      #        
        ###       ###        ###               
         ##       ###        ###               
__________#_______####_______####______________

               我们的未来没有BUG                  
* ==============================================================================
* Filename: LuaDeepProfilerSetting
* Created:  2018/7/13 14:29:22
* Author:   エル・プサイ・コングルゥ
* Purpose:  
* ==============================================================================
*/

namespace MikuLuaProfiler
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class LuaDeepProfilerSetting : ScriptableObject
    {
        public List<string> excludeFolder = new List<string>();
        public List<string> excludeFile = new List<string>();

        public string luaExtern = ".lua";
        public const string SettingsAssetName = "LuaDeepProfilerSettings";
        [HideInInspector]
        [SerializeField]
        public string luaProjectPath = "Lua";


        public string profilerLuaProjectPath
        {
            get
            {
                return luaProjectPath + "Profiler";
            }
        }
        private static LuaDeepProfilerSetting instance;
        public static LuaDeepProfilerSetting Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = AssetDatabase.LoadAssetAtPath<LuaDeepProfilerSetting>("Assets/" + SettingsAssetName + ".asset");
                    if (instance == null)
                    {
                        Debug.Log("Lua Profiler: cannot find integration settings, creating default settings");
                        instance = CreateInstance<LuaDeepProfilerSetting>();
                        instance.name = "Lua Profiler Integration Settings";
#if UNITY_EDITOR
                        AssetDatabase.CreateAsset(instance, "Assets/" + SettingsAssetName + ".asset");
#endif
                    }
                }
                return instance;
            }
        }
        [SerializeField]
        private bool m_isDeepProfiler = false;
        public bool isDeepProfiler
        {
            get
            {
                return m_isDeepProfiler;
            }
            set
            {
                m_isDeepProfiler = value;
            }
        }

#if UNITY_EDITOR

        [MenuItem("Tools/LuaProfiler/Setting", priority = 10)]
        public static void EditSettings()
        {
            Selection.activeObject = Instance;
#if UNITY_2018_1_OR_NEWER
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
#else
            EditorApplication.ExecuteMenuItem("Window/Inspector");
#endif
        }
#endif

        [SerializeField]
        public List<string> keyList = new List<string>();
        [SerializeField]
        public List<string> md5List = new List<string>();
        public void ReMakeDict()
        {
            md5Dict.Clear();
            for (int i = 0, imax = keyList.Count; i < imax; i++)
            {
                md5Dict[keyList[i]] = md5List[i];
            }
            md5List.Clear();
            keyList.Clear();
        }
        private Dictionary<string, string> md5Dict = new Dictionary<string, string>();
        public void AddValue(string key, string value)
        {
            keyList.Add(key);
            md5List.Add(value);
        }
        public void ClearMD5Dict()
        {
            keyList.Clear();
            md5List.Clear();
            md5Dict.Clear();
        }
        public void SlotMd5(string key, string value)
        {
            md5Dict[key] = value;
        }
        public bool TryGetMd5(string key, out string value)
        {
            bool result = md5Dict.TryGetValue(key, out value);
            return result;
        }
    }
#endif
}