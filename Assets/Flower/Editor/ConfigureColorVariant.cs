using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace MindState.Editor {
 public static class ConfigureColorVariant {
  public static void Build() {
   EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
   const string path="Assets/Flower/Art/TargetAppearance.asset";
   var a=AssetDatabase.LoadAssetAtPath<FlowerAppearancePreset>(path);
   if(a==null) { a=ScriptableObject.CreateInstance<FlowerAppearancePreset>(); AssetDatabase.CreateAsset(a,path); }
   var f=Object.FindObjectOfType<FlowerAssembler>(); f.appearance=a; PrefabUtility.RecordPrefabInstancePropertyModifications(f); EditorUtility.SetDirty(f);
   Camera.main.backgroundColor=Color.black; Camera.main.clearFlags=CameraClearFlags.SolidColor;
   var post=Camera.main.GetComponent<GalleryPost>(); post.bloom=false; post.exposure=.92f;
   RenderSettings.ambientSkyColor=new Color(.32f,.32f,.36f);
   RenderSettings.ambientEquatorColor=new Color(.23f,.23f,.27f);
   RenderSettings.ambientGroundColor=new Color(.14f,.14f,.18f);
   foreach(var l in Object.FindObjectsOfType<Light>()) {
    if(l.name.StartsWith("01")) { l.color=new Color(1,.98f,.95f); l.intensity=.40f; }
    if(l.name.StartsWith("02")) { l.color=new Color(.86f,.92f,1); l.intensity=.12f; }
    if(l.name.StartsWith("03")) { l.color=new Color(.78f,.86f,1); l.intensity=.22f; }
   }
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); EditorUtility.SetDirty(a); AssetDatabase.SaveAssets();
   PerformanceBuild.Validation();
  }
 }
}
