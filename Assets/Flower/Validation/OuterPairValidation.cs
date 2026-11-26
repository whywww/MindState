#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
namespace MindState {
public sealed class OuterPairValidation:MonoBehaviour {
 internal static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-outerPair")>=0;
 public static void RunReview(){SessionState.SetBool(FlowerOpenClearancePreview.Key,false);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,false);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 public static void Run(){SessionState.SetBool(FlowerOpenClearancePreview.Key,true);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Requested)Camera.main.gameObject.AddComponent<OuterPairValidation>();}
 IEnumerator Start(){yield return null;EditorApplication.Exit(0);}
}
}
#endif