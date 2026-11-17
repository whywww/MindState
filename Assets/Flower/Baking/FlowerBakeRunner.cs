#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace MindState {
 public sealed class FlowerBakeRunner : MonoBehaviour {
  public static void Run() {
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
   Application.logMessageReceived+=Fail;
   UnityEditor.EditorApplication.EnterPlaymode();
  }
  static void Fail(string message,string stack,LogType type) {
   if(type==LogType.Exception||type==LogType.Error) UnityEditor.EditorApplication.Exit(2);
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bakeFlower")>=0) {
    Application.logMessageReceived+=Fail;Camera.main.gameObject.AddComponent<FlowerBakeRunner>();
   }
  }
  internal static void ValidateReference(FlowerAssembler flower) {
   foreach(float phase in new[]{0f,.25f,.5f,.75f,1f}) {
    flower.SetBloomProgress(phase);
    if(flower.RootAnchorError()>1e-5f)throw new Exception("Bake root attachment failed at "+phase);
   }
  }
  IEnumerator Start() {
   yield return null;
   var flower=FindObjectOfType<FlowerAssembler>();
   if(flower==null||!flower.Ready) {UnityEditor.EditorApplication.Exit(2);yield break;}
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bakePlayback")>=0&&!File.Exists(Path.Combine(Application.streamingAssetsPath,"WanderingFlex.bin")))throw new InvalidOperationException("Initial flexible correction requires a complete cache rebuild; -PlaybackOnly is insufficient.");
   ValidateReference(flower);
   flower.PrepareFlexBake();
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bakePlayback")<0)flower.SaveBottomCache();
   flower.SavePlaybackCache();
   flower.SavePreparedFlex();
   Debug.Log("FLOWER_BAKE_SUCCESS");UnityEditor.EditorApplication.Exit(0);
  }
 }
}
#endif
