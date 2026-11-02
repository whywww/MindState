using UnityEditor;
using UnityEngine;
namespace MindState.Editor {
public static class PerformanceBuild {
 public static void Validation()=>Build("Validation",true);
 public static void Delivery()=>Build("Windows",false);
 static void Build(string folder,bool validation) {
 var options=new BuildPlayerOptions { scenes=System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path),locationPathName="Builds/"+folder+"/MindState.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None,extraScriptingDefines=validation?new[]{"FLOWER_VALIDATION"}:new string[0] };
 var report=BuildPipeline.BuildPlayer(options); if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(report.summary.result.ToString());
 Debug.Log("PERF_BUILD_SUCCESS "+folder);
 }
}}
