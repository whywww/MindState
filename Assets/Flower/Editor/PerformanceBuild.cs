using UnityEditor;
using UnityEngine;
namespace MindState.Editor {
public static class PerformanceBuild {
 public static void Validation()=>Build("Validation",true);
 public static void Delivery()=>Build("Windows",false);
 static void Build(string folder,bool validation) {
 var files=new[]{"Flower/Runtime/FlowerBottomPatches.cs","Flower/Runtime/FlowerLayerPatches.cs","HeroPetal/Runtime/HeroPetal.cs","Flower/Runtime/FlowerAssembler.cs","Flower/Runtime/FlowerPreset.cs","Flower/Art/ExhibitionPeony.asset","HeroPetal/Prefabs/HeroPetal.prefab"};
 var signature=string.Join("\n",System.Array.ConvertAll(files,path=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant();}));
 string stamp=System.IO.Path.Combine(Application.dataPath,"Flower/Resources/BottomOverlapProvenance.txt");
 if(!System.IO.File.Exists(stamp)||System.IO.File.ReadAllText(stamp)!=signature)throw new System.Exception("Bottom overlap cache provenance differs from current geometry sources. Regenerate and verify the sparse cache before building.");
 var options=new BuildPlayerOptions { scenes=System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path),locationPathName="Builds/"+folder+"/MindState.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None,extraScriptingDefines=validation?new[]{"FLOWER_VALIDATION"}:new string[0] };
 var report=BuildPipeline.BuildPlayer(options); if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(report.summary.result.ToString());
 Debug.Log("PERF_BUILD_SUCCESS "+folder);
 }
}}
