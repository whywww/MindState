#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace MindState
{
    // Opt-in development diagnostics. Never added to the exhibition scene or a release player.
    public sealed class SilkComparison : MonoBehaviour
    {
        HeroPetal petal;
        GalleryController gallery;
        MeshRenderer rendererComponent;
        Material baseline,optimized;
        Camera cameraComponent;
        RenderTexture target;
        MaterialPropertyBlock properties;
        string output;
        bool isA,dynamicRun;
        float sequenceStart,nextInput,nextReplay;
        double lastSubmit;
        readonly Sample[] samples=new Sample[131072];
        readonly List<Segment> segments=new List<Segment>();
        Probe main,render,gpu,vertices,batches,gc,petalProbe,submitProbe;
        readonly FrameTiming[] timing=new FrameTiming[1];
        Recorder cameraRecorder;
        static readonly ProfilerMarker SubmitMarker=new ProfilerMarker("MindState.Camera.Submit");
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-silkAB")>=0)
                Camera.main.gameObject.AddComponent<SilkComparison>();
        }
        void LateUpdate()
        {
            if(cameraComponent==null) return;
            rendererComponent.GetPropertyBlock(properties); properties.SetFloat("_Breath",0);
            rendererComponent.SetPropertyBlock(properties);
            long t=System.Diagnostics.Stopwatch.GetTimestamp();
            using(SubmitMarker.Auto()) cameraComponent.Render();
            lastSubmit=(System.Diagnostics.Stopwatch.GetTimestamp()-t)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void Update()
        {
            if(!dynamicRun||petal==null) return;
            float t=Time.realtimeSinceStartup-sequenceStart;
            if(t>=nextInput)
            {
                // Identical, wall-time 1Hz input sequence in each A/B segment.
                petal.SetShapeParameters(120+25*Mathf.Sin(t*0.13f),0.35f+0.30f*Mathf.Sin(t*0.21f),0.6f+0.3f*Mathf.Sin(t*0.17f+0.3f));
                nextInput+=1;
            }
            if(t>=nextReplay) { petal.RestartOpening(); nextReplay+=20; }
        }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            output=Arg(args,"-evidencePath",Path.Combine(Application.persistentDataPath,"SilkMicroDetail"));
            Directory.CreateDirectory(output);
            petal=FindObjectOfType<HeroPetal>(); gallery=FindObjectOfType<GalleryController>();
            rendererComponent=petal.GetComponent<MeshRenderer>(); optimized=petal.pearlMaterial;
            baseline=Resources.Load<Material>("SilkBaseline");
            if(baseline==null) throw new InvalidOperationException("Build SilkBaseline diagnostic assets first.");
            gallery.showInterface=false; gallery.post.bloom=false;
            cameraComponent=gallery.GetComponent<Camera>(); cameraComponent.enabled=false;
            properties=new MaterialPropertyBlock();
            target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 };
            target.Create(); cameraComponent.targetTexture=target;
            yield return new WaitForSeconds(2);
            foreach(bool a in new[]{true,false})
            {
                Select(a); petal.SetShapeImmediate(125,0.2f,0.7f); petal.frozen=true;
                yield return Capture((a?"A":"B")+"_Front",0,0,6.2f);
                yield return Capture((a?"A":"B")+"_Side",78,8,6.2f);
                yield return Capture((a?"A":"B")+"_Closeup",-18,22,3.2f);
                yield return Capture((a?"A":"B")+"_Gallery",-12,12,6.2f);
                yield return Capture((a?"A":"B")+"_Back",180,8,6.2f);
            }
            Select(false);
            foreach(int fine in new[]{20,32,40})
            {
                petal.finePleatCount=fine;
                yield return Capture("B_FineCount_"+fine,-18,22,4.0f);
            }
            petal.finePleatCount=32;
            for(int i=0;i<5;i++)
            {
                petal.SetShapeImmediate(125,0.2f,0.7f,i*0.25f); petal.frozen=true;
                yield return Capture("B_Unfold_"+(i*25).ToString("000"),-12,12,6.2f);
            }
            if(Array.IndexOf(args,"-abScreensOnly")>=0) { Finish(); yield break; }

            Application.targetFrameRate=-1; QualitySettings.vSyncCount=0;
            Profiler.enabled=true;
            Profiler.SetAreaEnabled(ProfilerArea.GPU,true);
            yield return new WaitForSeconds(2);
            Discover();
            cameraRecorder=Recorder.Get("Camera.Render"); cameraRecorder.enabled=true;
            int rounds=int.Parse(Arg(args,"-abRounds","3"),Invariant);
            float seconds=float.Parse(Arg(args,"-abSeconds","60"),Invariant);
            // Both frozen and animated cases, three alternated A/B pairs per case.
            for(int mode=0;mode<2;mode++) for(int round=0;round<rounds;round++) for(int order=0;order<2;order++)
            {
                bool a=(round%2==0)?order==0:order==1;
                Select(a); ResetSequence(mode==1);
                gallery.SetView(-12,12,6.2f);
                string name=(mode==0?"Frozen":"Animated")+"_"+(round+1)+"_"+(a?"A":"B");
                if(round==0)
                {
                    Profiler.logFile=Path.Combine(output,name+".raw"); Profiler.enableBinaryLog=true;
                }
                yield return new WaitForSeconds(1);
                Profiler.enableBinaryLog=false;
                yield return new WaitForSeconds(9);
                ResetSequence(mode==1);
                int count=0; float started=Time.realtimeSinceStartup,end=started+seconds;
                while(Time.realtimeSinceStartup<end&&count<samples.Length)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    double gpuMs=Read(gpu,true);
                    if(gpuMs<=0&&FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].gpuFrameTime>0) gpuMs=timing[0].gpuFrameTime;
                    samples[count++]=new Sample {
                        mainMs=Read(main,true),renderMs=Read(render,true)>0?Read(render,true):-1,gpuMs=gpuMs>0?gpuMs:-1,
                        gpuCameraMs=cameraRecorder.gpuSampleBlockCount>0?cameraRecorder.gpuElapsedNanoseconds/1e6:-1,
                        petalMs=petal.LastUpdateMilliseconds,submitMs=lastSubmit,
                        vertices=Read(vertices),batches=Read(batches),gcFrame=Read(gc),petalGc=petal.LastUpdateAllocation,
                        frameMs=Time.unscaledDeltaTime*1000.0
                    };
                }
                dynamicRun=false;
                var result=new Segment { name=name,variant=a?"A":"B",animated=mode==1,
                    samples=count,seconds=Time.realtimeSinceStartup-started,
                    mainMs=Summarize(count,0),renderMs=Summarize(count,1),gpuMs=Summarize(count,2),
                    gpuCameraMs=Summarize(count,3),petalMs=Summarize(count,4),submitMs=Summarize(count,5),
                    vertices=Summarize(count,6),batches=Summarize(count,7),gcFrame=Summarize(count,8),
                    petalGc=Summarize(count,9),frameMs=Summarize(count,10) };
                segments.Add(result);
                WriteCsv(name,count);
                File.AppendAllText(Path.Combine(output,"BenchmarkProgress.txt"),DateTime.UtcNow.ToString("O")+" "+name+" frames="+count+" petalMs="+result.petalMs.mean.ToString("F4",Invariant)+"\n");
                WriteReport();
            }
            Finish();
        }
        void Select(bool a)
        {
            isA=a; petal.diagnosticPleatScale=a?1:0.8f;
            rendererComponent.sharedMaterial=a?baseline:optimized;
        }
        void ResetSequence(bool animate)
        {
            petal.SetShapeImmediate(125,0.2f,0.7f,animate?0:1);
            petal.frozen=!animate; dynamicRun=animate;
            sequenceStart=Time.realtimeSinceStartup; nextInput=0; nextReplay=20;
        }
        IEnumerator Capture(string name,float yaw,float pitch,float radius)
        {
            gallery.SetView(yaw,pitch,radius);
            yield return null; yield return null;
            LateUpdate();
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            Destroy(image); RenderTexture.active=previous;
        }
        void Discover()
        {
            var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var text=new StringBuilder();
            foreach(var h in handles)
            {
                var d=ProfilerRecorderHandle.GetDescription(h);
                text.Append(d.Category.Name).Append(" | ").Append(d.Name).Append(" | ").Append(d.UnitType).AppendLine();
            }
            File.WriteAllText(Path.Combine(output,"AvailableProfilerCounters.txt"),text.ToString());
            main=Open(handles,"CPU Main Thread Frame Time","Main Thread");
            render=Open(handles,"CPU Render Thread Frame Time","Render Thread");
            gpu=Open(handles,"GPU Frame Time","FrameTime.GPU"); vertices=Open(handles,"Vertices Count","Vertices");
            batches=Open(handles,"Batches Count","Batches"); gc=Open(handles,"GC Allocated In Frame");
            petalProbe=Open(handles,"MindState.Petal.Update"); submitProbe=Open(handles,"MindState.Camera.Submit");
        }
        static Probe Open(List<ProfilerRecorderHandle> handles,params string[] names)
        {
            foreach(string name in names) foreach(var handle in handles)
            {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name==name) return new Probe { name=name,recorder=new ProfilerRecorder(handle,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame) };
            }
            return new Probe { name=names[0] };
        }
        static double Read(Probe p,bool milliseconds=false)
        {
            if(p==null||!p.recorder.Valid||p.recorder.Count==0) return -1;
            return p.recorder.LastValue/(milliseconds?1e6:1);
        }
        static double Field(Sample s,int field)
        {
            switch(field) {
                case 0:return s.mainMs; case 1:return s.renderMs; case 2:return s.gpuMs;
                case 3:return s.gpuCameraMs; case 4:return s.petalMs; case 5:return s.submitMs;
                case 6:return s.vertices; case 7:return s.batches; case 8:return s.gcFrame;
                case 9:return s.petalGc; default:return s.frameMs;
            }
        }
        Stats Summarize(int count,int field)
        {
            var values=new double[count]; int n=0; double sum=0;
            for(int i=0;i<count;i++) { double v=Field(samples[i],field); if(v<0) continue; values[n++]=v; sum+=v; }
            if(n==0) return new Stats { mean=-1,p95=-1,min=-1,max=-1 };
            Array.Sort(values,0,n);
            return new Stats { mean=sum/n,p95=values[Math.Min(n-1,(int)Math.Ceiling(n*0.95)-1)],min=values[0],max=values[n-1],availableSamples=n };
        }
        void WriteCsv(string name,int count)
        {
            using(var w=new StreamWriter(Path.Combine(output,name+".csv"),false,Encoding.UTF8))
            {
                w.WriteLine("sample,main_ms,render_ms,gpu_frame_ms,gpu_camera_ms,petal_ms,submit_cpu_ms,render_vertices,batches,gc_frame_bytes,petal_gc_bytes,frame_ms");
                for(int i=0;i<count;i++)
                {
                    w.Write(i.ToString(Invariant));
                    for(int f=0;f<11;f++) { w.Write(','); w.Write(Field(samples[i],f).ToString("F6",Invariant)); }
                    w.WriteLine();
                }
            }
        }
        void WriteReport()
        {
            var report=new Report { unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                graphicsApi=SystemInfo.graphicsDeviceType.ToString(),resolution="1920x1080 offscreen 4xMSAA",frameLimit=-1,
                mainCounter=main!=null?main.name:"",renderCounter=render!=null?render.name:"",gpuCounter=gpu!=null?gpu.name:"",
                meshVertices=petal.RuntimeMesh.vertexCount,meshTriangles=petal.RuntimeMesh.triangles.Length/3,
                baselinePasses=baseline.passCount,optimizedPasses=optimized.passCount,segments=segments.ToArray() };
            File.WriteAllText(Path.Combine(output,"ProfilerComparison.json"),JsonUtility.ToJson(report,true));
        }
        void Finish()
        {
            WriteReport(); Profiler.enableBinaryLog=false; Profiler.enabled=false;
            Debug.Log("MINDSTATE_SILK_AB_COMPLETE segments="+segments.Count);
            if(!Application.isEditor) Application.Quit();
#if UNITY_EDITOR
            else UnityEditor.EditorApplication.Exit(0);
#endif
        }
        static string Arg(string[] args,string key,string fallback) { int i=Array.IndexOf(args,key); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
        void OnDestroy()
        {
            foreach(var p in new[]{main,render,gpu,vertices,batches,gc,petalProbe,submitProbe}) if(p!=null) p.recorder.Dispose();
            if(cameraRecorder!=null) cameraRecorder.enabled=false;
            if(cameraComponent!=null) { cameraComponent.targetTexture=null; cameraComponent.enabled=true; }
            if(target!=null) { target.Release(); Destroy(target); }
        }
        sealed class Probe { public string name; public ProfilerRecorder recorder; }
        struct Sample { public double mainMs,renderMs,gpuMs,gpuCameraMs,petalMs,submitMs,vertices,batches,gcFrame,petalGc,frameMs; }
        [Serializable] public struct Stats { public double mean,p95,min,max; public int availableSamples; }
        [Serializable] public sealed class Segment {
            public string name,variant; public bool animated; public int samples; public float seconds;
            public Stats mainMs,renderMs,gpuMs,gpuCameraMs,petalMs,submitMs,vertices,batches,gcFrame,petalGc,frameMs;
        }
        [Serializable] sealed class Report {
            public string unity,gpu,cpu,graphicsApi,resolution,mainCounter,renderCounter,gpuCounter; public int frameLimit,meshVertices,meshTriangles,baselinePasses,optimizedPasses;
            public Segment[] segments;
        }
    }
}
#endif
