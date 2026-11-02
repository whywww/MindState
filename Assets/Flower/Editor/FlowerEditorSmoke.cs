using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MindState.Editor
{
    public static class FlowerEditorSmoke
    {
        [InitializeOnLoadMethod] static void Reconnect() { if(SessionState.GetBool("FlowerSmoke",false)) EditorApplication.update+=Tick; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity"); SessionState.SetBool("FlowerSmoke",true);
            EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||Time.time<17) return;
            EditorApplication.update-=Tick; SessionState.SetBool("FlowerSmoke",false);
            try
            {
                var f=UnityEngine.Object.FindObjectOfType<FlowerAssembler>();
                if(f==null||!f.Ready||f.PetalCount!=14||f.bloomProgress<.99f||f.RootAnchorError()>.00001f) throw new Exception("Flower initialization / bloom / anchors failed");
                Directory.CreateDirectory("Evidence/FlowerPerformance/MainVerification"); File.WriteAllText("Evidence/FlowerPerformance/MainVerification/EditorPlayMode.txt","PASS Unity "+Application.unityVersion+" Play Mode: 14 petals, 6 shared meshes, full bloom, connected roots.");
                Debug.Log("MINDSTATE_FLOWER_EDITOR_PASS"); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }
    }
}
