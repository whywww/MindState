using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MindState
{
    // Keep historical diagnostic command lines targeting their original single-petal scene.
    public static class PetalDiagnosticScene
    {
        public static void Start<T>() where T:Component
        {
            if(SceneManager.GetActiveScene().name=="HeroPetalGallery") Camera.main.gameObject.AddComponent<T>();
            else { SceneManager.sceneLoaded+=Loaded<T>; SceneManager.LoadScene("HeroPetalGallery"); }
        }
        static void Loaded<T>(Scene scene,LoadSceneMode mode) where T:Component
        { SceneManager.sceneLoaded-=Loaded<T>; Camera.main.gameObject.AddComponent<T>(); }
    }
}
