using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace MindState {
// Explicit opt-in standalone benchmark; file IO and screenshots occur after measurement.
public sealed class FlowerPerformanceCapture : MonoBehaviour {
 readonly List<double> frames=new List<double>(40000),updates=new List<double>(40000),gpu=new List<double>(40000),cpu=new List<double>(40000);
 readonly FrameTiming[] timing=new FrameTiming[1];
 readonly List<int> stages=new List<int>(40000);
 Camera view;
 RenderTexture target;
 Texture2D syncPixel;
 CustomSampler renderSampler;
 Recorder renderRecorder;
 FlowerAssembler flower;
 string output;
 int stage=-1,uploads;
 long allocations;
 double start,previous,commandMs;
 bool measuring;
 double duration=120;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flowerBenchmark")>=0) new GameObject("Flower performance capture").AddComponent<FlowerPerformanceCapture>();
 }
 static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
 IEnumerator Start(){
  output=Arg("-benchmarkOutput","Outputs/Performance");Directory.CreateDirectory(output);
  duration=double.Parse(Arg("-benchmarkSeconds","120"),CultureInfo.InvariantCulture);
  Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=240;
  Screen.SetResolution(1920,1080,false);
  yield return null;flower=FindObjectOfType<FlowerAssembler>();
  if(flower==null||!flower.Ready){File.WriteAllText(Path.Combine(output,"Result.txt"),"Flower not ready");Application.Quit(2);yield break;}
  // Hidden/minimized Windows players skip normal presentation. Render explicitly
  // and sync one pixel so every measured frame includes completed 1080p GPU work.
  view=Camera.main;view.enabled=false;target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf){antiAliasing=Mathf.Max(1,QualitySettings.antiAliasing)};target.Create();view.targetTexture=target;
  syncPixel=new Texture2D(1,1,TextureFormat.RGB24,false);
  Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.GPU,true);
  renderSampler=CustomSampler.Create("FlowerBenchmark.Render",true);renderRecorder=renderSampler.GetRecorder();renderRecorder.enabled=true;
  yield return new WaitForSecondsRealtime(10);
  uploads=flower.MeshUpdates;start=previous=Time.realtimeSinceStartupAsDouble;measuring=true;
 }
 void LateUpdate(){
  if(view!=null){renderSampler.Begin();view.Render();renderSampler.End();var old=RenderTexture.active;RenderTexture.active=target;syncPixel.ReadPixels(new Rect(0,0,1,1),0,0);RenderTexture.active=old;}
  if(!measuring)return;
  double now=Time.realtimeSinceStartupAsDouble,elapsed=now-start;
  frames.Add((now-previous)*1000);stages.Add(Mathf.Clamp(stage,0,5));previous=now;updates.Add(flower.LastUpdateMilliseconds+commandMs);allocations+=flower.LastUpdateAllocation;
  FrameTimingManager.CaptureFrameTimings();
  bool recorded=renderRecorder.isValid&&renderRecorder.gpuSampleBlockCount>0;
  if(recorded)gpu.Add(renderRecorder.gpuElapsedNanoseconds/1000000.0);
  if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(!recorded&&timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);if(timing[0].cpuFrameTime>0)cpu.Add(timing[0].cpuFrameTime);}
  long commandStart=System.Diagnostics.Stopwatch.GetTimestamp(),commandBytes=GC.GetAllocatedBytesForCurrentThread();
  int next=(int)(elapsed/20);
  if(stage!=next){stage=next;if(stage==0)flower.SetBloomProgress(0);else if(stage<=4)flower.SetArtState((FlowerArtState)(stage-1));else flower.WaitForArtBrain();}
  if(stage==0)flower.SetBloomProgress(Mathf.PingPong((float)elapsed/6,1));
  if(stage==5){float t=(float)(elapsed-100);flower.SetBrainMetrics(((int)(t/3)&1)==0?.8f:.2f,((int)(t/5)&1)==0?.8f:.2f);flower.SetFrozen(t>=8&&t<10);}
  commandMs=(System.Diagnostics.Stopwatch.GetTimestamp()-commandStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;allocations+=GC.GetAllocatedBytesForCurrentThread()-commandBytes;
  if(elapsed<duration)return;
  measuring=false;flower.SetFrozen(false);
  var rows=new List<string>{"frame,stage,wall_ms,flower_ms"};for(int i=0;i<frames.Count;i++)rows.Add(i+","+stages[i]+","+F(frames[i])+","+F(updates[i]));File.WriteAllLines(Path.Combine(output,"Frames.csv"),rows);
  var summary=new List<string>{SystemInfo.graphicsDeviceName,"1920x1080 MSAA="+target.antiAliasing+" explicit offscreen render + one-pixel GPU completion sync (conservative; excludes normal CPU/GPU overlap)","gpu_recorder_supported="+SystemInfo.supportsGpuRecorder+" valid="+renderRecorder.isValid,"frames="+frames.Count};
  for(int s=0;s<6;s++){var segment=new List<double>();for(int i=0;i<frames.Count;i++)if(stages[i]==s)segment.Add(frames[i]);summary.Add(Stats("stage"+s,segment));}
  summary.AddRange(new[]{Stats("wall",frames),Stats("flower",updates),Stats("cpu",cpu),Stats("gpu",gpu),"flower_allocations="+allocations,"mesh_uploads="+(flower.MeshUpdates-uploads),"geometry_bytes="+flower.GeometryBytes,"initialization_ms="+F(flower.InitializationMilliseconds)});File.WriteAllLines(Path.Combine(output,"Summary.txt"),summary);
  StartCoroutine(Finish());
 }
 IEnumerator Finish(){
  if(duration<120){File.WriteAllText(Path.Combine(output,"Result.txt"),"SMOKE COMPLETE");Application.Quit(0);yield break;}
  var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
  using var readback=new FlowerCaptureReadback(1920,1080);
  for(int i=0;i<4;i++){flower.SetArtState((FlowerArtState)i);yield return new WaitForSecondsRealtime(6.5f);readback.Read(target,image);File.WriteAllBytes(Path.Combine(output,"State"+i+".png"),image.EncodeToPNG());yield return null;}
  Destroy(image);
  File.WriteAllText(Path.Combine(output,"Result.txt"),"COMPLETE");Application.Quit(0);
 }
 void OnDestroy(){if(renderRecorder!=null)renderRecorder.enabled=false;if(view!=null)view.targetTexture=null;if(target!=null){target.Release();Destroy(target);}if(syncPixel!=null)Destroy(syncPixel);}
 static string F(double n)=>n.ToString("F4",CultureInfo.InvariantCulture);
 static string Stats(string name,List<double> values){if(values.Count==0)return name+"=unavailable";values.Sort();return name+"_ms p50="+F(values[values.Count/2])+" p95="+F(values[Math.Min(values.Count-1,(int)(values.Count*.95))])+" p99="+F(values[Math.Min(values.Count-1,(int)(values.Count*.99))]);}
}
}
