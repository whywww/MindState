using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MindState.Editor
{
    public static class EditorSmoke
    {
        static double started;
        static bool entered;
        [InitializeOnLoadMethod]
        static void Reconnect()
        {
            if(SessionState.GetBool("MindStateSmoke",false)) EditorApplication.update+=Tick;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/HeroPetal/Scenes/HeroPetalGallery.unity");
            SessionState.SetBool("MindStateSmoke",true);
            EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying) return;
            if(!entered) { entered=true; started=EditorApplication.timeSinceStartup; }
            if(EditorApplication.timeSinceStartup-started<14) return;
            EditorApplication.update-=Tick;
            SessionState.SetBool("MindStateSmoke",false);
            try
            {
                var petal=UnityEngine.Object.FindObjectOfType<HeroPetal>();
                if(petal==null||petal.RuntimeMesh==null||petal.Growth<0.8f) throw new Exception("Editor runtime initialization/growth failed");
                Directory.CreateDirectory("Evidence");
                Directory.CreateDirectory("Evidence/FanPetal");
                File.WriteAllText("Evidence/FanPetal/EditorPlayMode.txt","PASS: Unity "+Application.unityVersion+" entered Play Mode, generated "+petal.RuntimeMesh.vertexCount+" vertices and completed unfolding.\n");
                Debug.Log("MINDSTATE_EDITOR_PLAYMODE_PASS");
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }
    }
}
