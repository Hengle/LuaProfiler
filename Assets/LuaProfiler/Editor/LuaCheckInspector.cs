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
* Filename: LuaCheckSetting
* Created:  2018/7/13 14:29:22
* Author:   エル・プサイ・コングルゥ
* Purpose:  
* ==============================================================================
*/

namespace MikuLuaProfiler
{
    using System;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(LuaCheckSetting))]
    public class LuaCheckInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            LuaCheckSetting settings = (LuaCheckSetting)target;

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Resources Path", GUI.skin.textField, style);
            settings.luaProjectPath = EditorGUILayout.TextField(GUIContent.none, settings.luaProjectPath);
            if (GUILayout.Button("Browse", GUILayout.ExpandWidth(false)))
            {
                GUI.FocusControl(null);
                var path = EditorUtility.OpenFolderPanel("Locate Build Folder", settings.luaProjectPath, null);
                if (!string.IsNullOrEmpty(path))
                {
                    settings.luaProjectPath = LuaDeepProfiler.MakePathRelativeToProject(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("save global", GUILayout.ExpandWidth(true)))
            {
                LuaCheckSetting.Instance.ResetConfig();
            }

            if (GUILayout.Button("check", GUILayout.ExpandWidth(true)))
            {
                LuaCheckSetting.Instance.Check();
            }
            EditorGUILayout.EndVertical();
        }
    }
}

