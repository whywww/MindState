using UnityEditor;
using UnityEngine;
namespace MindState.Editor {
public static class PerformanceBuild {
 public static void Validation()=>Build("Validation",true);
 public static void Delivery()=>Build("Windows",false);
 static void Build(string folder,bool validation) {
 if(!FlowerCacheProvenance.Matches())throw new System.Exception("Geometry cache is stale. Run Tools/Bake.ps1 before building.");
 if(!FlowerCacheProvenance.PlaybackMatches())throw new System.Exception("Playback cache is stale. Run Tools/Bake.ps1 before building.");
 var options=new BuildPlayerOptions { scenes=new[]{"Assets/Flower/Scenes/FlowerGallery.unity"},locationPathName="Builds/"+folder+"/MindState.exe",target=BuildTarget.StandaloneWindows64,options=validation?BuildOptions.Development:BuildOptions.None,extraScriptingDefines=validation?new[]{"FLOWER_VALIDATION"}:new string[0] };
 var report=BuildPipeline.BuildPlayer(options); if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(report.summary.result.ToString());
 Debug.Log("PERF_BUILD_SUCCESS "+folder);
 }
}}
