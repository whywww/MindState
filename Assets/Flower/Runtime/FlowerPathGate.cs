#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal bool VerifyStandardArtPath(FlowerArtState state,float time) {
  suppressArtMotion=true;
  SetArtPreviewImmediate(state);MotionStep(time);
  var petals=GetDiagnosticPetals();var vertices=new Vector3[petals.Length][];var matrices=new Matrix4x4[petals.Length];
  for(int i=0;i<petals.Length;i++) {vertices[i]=PetalMesh(petals[i].layer,petals[i].index).vertices;matrices[i]=generated.worldToLocalMatrix*petals[i].body.localToWorldMatrix;}
  float progress=bloomProgress;SetBloomProgress(progress);bool equal=true;
  for(int i=0;i<petals.Length;i++) {
   var current=PetalMesh(petals[i].layer,petals[i].index).vertices;
   for(int j=0;j<current.Length;j++)equal&=(current[j]-vertices[i][j]).sqrMagnitude<1e-12f;
   var matrix=generated.worldToLocalMatrix*petals[i].body.localToWorldMatrix;
   for(int j=0;j<16;j++)equal&=Mathf.Abs(matrix[j]-matrices[i][j])<1e-5f;
  }
  suppressArtMotion=false;return equal;
 }
}
// Release gate: compare dynamic contacts against the standard pose at exactly the same progress.
public sealed class FlowerPathGate : MonoBehaviour {
 public static void Run() {
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
  UnityEditor.EditorApplication.EnterPlaymode();
 }
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-pathGate")>=0) Camera.main.gameObject.AddComponent<FlowerPathGate>();
 }
 static Dictionary<string,int> Counts(FlowerSurfaceCollision scan,List<FlowerSurfaceCollision.Contact> hits) {
  scan.SampleRendered();scan.FindContacts(hits,parallel:true);
  var counts=new Dictionary<string,int>();
  foreach(var h in hits) if(!h.rootRegion) {string key=h.petalA+":"+h.petalB;counts.TryGetValue(key,out int n);counts[key]=n+1;}
  return counts;
 }
 IEnumerator Start() {
  yield return null;
  const string output=".validation/CurrentPathGate";Directory.CreateDirectory(output);
  File.WriteAllText(Path.Combine(output,"Result.txt"),"RUNNING: no acceptance result yet");
  var f=FindObjectOfType<FlowerAssembler>();f.enabled=false;
  var scan=new FlowerSurfaceCollision(f);var hits=new List<FlowerSurfaceCollision.Contact>();
  var checks=new List<string>();
  for(int state=0;state<4;state++)for(int sample=0;sample<3;sample++) {
   bool equal=f.VerifyStandardArtPath((FlowerArtState)state,sample*1.25f);
   checks.Add((equal?"PASS ":"FAIL ")+"standard path state="+state+" time="+sample*1.25f);
   File.WriteAllLines(Path.Combine(output,"PathChecks.txt"),checks);
   if(!equal){File.WriteAllText(Path.Combine(output,"Result.txt"),"FAIL: standard path mismatch");UnityEditor.EditorApplication.Exit(3);yield break;}
  }
  var lines=new List<string>{"state,time,progress,pair,standardContacts,dynamicContacts"};
  for(int state=0;state<4;state++) for(int sample=0;sample<=12;sample++) {
   float time=sample*1.25f;
   f.SetArtPreviewImmediate((FlowerArtState)state);f.MotionStep(time);
   float progress=f.bloomProgress;
   f.SetBloomProgress(progress);
   var baseline=Counts(scan,hits);
   f.SetArtPreviewImmediate((FlowerArtState)state);f.MotionStep(time);
   var dynamic=Counts(scan,hits);
   bool failed=false;
   foreach(var pair in dynamic) {baseline.TryGetValue(pair.Key,out int n);if(pair.Value<=n)continue;
    lines.Add(state+","+time+","+progress+","+pair.Key+","+n+","+pair.Value);failed=true;
   }
   File.WriteAllLines(Path.Combine(output,"Contacts.csv"),lines);
   Debug.Log("PATH_GATE state="+state+" time="+time+" failed="+failed);
   if(failed) {File.WriteAllText(Path.Combine(output,"Result.txt"),"FAIL: original amplitude adds non-root contacts. Cleanup suspended.");UnityEditor.EditorApplication.Exit(2);yield break;}
   yield return null;
  }
  File.WriteAllText(Path.Combine(output,"Result.txt"),"PASS: sampled steady states");UnityEditor.EditorApplication.Exit(0);
 }
}
}
#endif
