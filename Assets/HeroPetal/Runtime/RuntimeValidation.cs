using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace MindState
{
    // Opt-in player verification. The normal exhibition scene never adds this component.
    public sealed class RuntimeValidation : MonoBehaviour
    {
        HeroPetal petal;
        GalleryController gallery;
        string output;
        readonly List<string> checks=new List<string>();
        double cpuSum,frameSum,maxCpu;
        long allocationMax;
        int samples;
        FrameTiming[] timings=new FrameTiming[1];
        double gpuSum;
        double renderCpuSum,lastRenderCpu;
        int gpuSamples;
        Camera renderCamera;
        RenderTexture renderTarget;
        void LateUpdate()
        {
            if(renderCamera!=null)
            {
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                renderCamera.Render();
                lastRenderCpu=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyPetal")>=0)
                Camera.main.gameObject.AddComponent<RuntimeValidation>();
        }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            output=Value(args,"-evidencePath",Path.Combine(Application.persistentDataPath,"Evidence"));
            Directory.CreateDirectory(output);
            Application.targetFrameRate=60;
            petal=FindObjectOfType<HeroPetal>(); gallery=FindObjectOfType<GalleryController>();
            // Explicit offscreen rendering is essential when the verification window is hidden.
            // It also ensures the soak test keeps exercising the GPU rather than measuring a minimized player.
            renderCamera=gallery.GetComponent<Camera>(); renderCamera.enabled=false;
            renderTarget=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 };
            renderTarget.Create(); renderCamera.targetTexture=renderTarget;
            yield return new WaitForSeconds(2);
            Check(Application.unityVersion=="2022.3.62f3c1","Exact Unity editor/player version");
            Check(GraphicsSettings.currentRenderPipeline==null,"Built-in pipeline");
            Check(Screen.width>0&&renderTarget.IsCreated(),"1080p offscreen render target available");
            Check(petal.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported,"Pearl shader supported");
            petal.SetShapeImmediate(125,0.2f,0.7f); petal.frozen=true; petal.grayPreview=true;
            yield return Capture("00_GrayFront",0,8,6.2f);
            yield return Capture("00_GraySide",72,8,6.2f);
            yield return Capture("00_GrayBack",180,8,6.2f);
            if(Array.IndexOf(args,"-grayOnly")>=0) { Application.Quit(); yield break; }
            petal.grayPreview=false;
            yield return Capture("01_Gallery",-12,12,6.2f);
            yield return Capture("02_Front",0,0,6.2f);
            yield return Capture("03_Side",78,8,6.2f);
            yield return Capture("04_Back",180,8,6.2f);
            yield return Capture("05_PleatCloseup",-18,22,3.2f);
            gallery.post.bloom=false;
            yield return Capture("06_NoBloom",-12,12,6.2f);
            for(int a=0;a<3;a++) for(int c=0;c<3;c++) for(int soft=0;soft<3;soft++) for(int phase=0;phase<3;phase++)
            {
                petal.SetShapeImmediate(90+30*a,c*0.5f,soft*0.5f,phase*0.5f); petal.frozen=true;
                ValidateMesh("Shape "+a+","+c+","+soft+" / unfold "+phase);
                if(phase==2&&a!=1&&c!=1&&soft!=1)
                    yield return Capture("07_Corner_"+a+"_"+c+"_"+soft,-12,12,6.2f);
            }
            for(int phase=0;phase<5;phase++)
            {
                petal.SetShapeImmediate(125,0.2f,0.7f,phase*0.25f); petal.frozen=true;
                yield return Capture("08_Unfold_"+(phase*25).ToString("000"),-12,12,6.2f);
            }
            petal.SetShapeImmediate(125,0.2f,0.7f);
            petal.SetShapeParameters(float.NaN,float.PositiveInfinity,float.NaN);
            Check(petal.targetOpeningAngle==125&&petal.targetCurlAmount==0.2f&&petal.targetPleatSoftness==0.7f,"Non-finite shape inputs ignored");
            petal.SetShapeParameters(999,-5,9);
            Check(petal.targetOpeningAngle==150&&petal.targetCurlAmount==0&&petal.targetPleatSoftness==1,"Shape inputs clamped independently");
            Check(petal.inputMode==PetalInputMode.DirectShape,"Shape API selects direct input without simulated overwrite");
            petal.SetShapeParameters(130,0.2f,0.7f);
            yield return new WaitForSeconds(0.15f);
            Check(petal.targetOpeningAngle==130&&petal.targetCurlAmount==0.2f&&petal.targetPleatSoftness==0.7f,"Direct shape targets held independently");
            petal.SetImmediate(0.4f,0.7f); float before=petal.targetAttention;
            petal.SetBrainMetrics(float.NaN,float.PositiveInfinity);
            Check(petal.targetAttention==before&&petal.targetRelaxation==0.7f,"Non-finite input ignored");
            petal.SetBrainMetrics(-5,9);
            Check(petal.targetAttention==0&&petal.targetRelaxation==1,"Input clamped to normalized interval");
            petal.SetImmediate(0,0); petal.SetBrainMetrics(1,1);
            yield return null;
            Check(petal.Attention>0&&petal.Attention<0.2f&&petal.Relaxation>0&&petal.Relaxation<0.2f,"Target step smoothed continuously");
            petal.frozen=true;
            Vector3 frozenVertex=petal.RuntimeMesh.vertices[3000];
            petal.SetBrainMetrics(0.9f,0.9f); yield return new WaitForSeconds(0.3f);
            Check(petal.RuntimeMesh.vertices[3000]==frozenVertex,"Freeze preserves geometry");
            petal.RestartGrowth(); Check(petal.Growth==0&&!petal.frozen,"Compatibility restart resets unfolding and unfreezes");
            gallery.SetView(-12,12,6.2f); gallery.showInterface=false;
            float seconds=float.Parse(Value(args,"-soakSeconds","25"),System.Globalization.CultureInfo.InvariantCulture);
            petal.SetShapeImmediate(125,0.2f,0.7f);
            yield return new WaitForSeconds(3);
            GC.Collect();
            long startMemory=Profiler.GetTotalAllocatedMemoryLong();
            File.WriteAllText(Path.Combine(output,"SoakProgress.txt"),"");
            int startMaterials=Resources.FindObjectsOfTypeAll<Material>().Length;
            float end=Time.realtimeSinceStartup+seconds,nextReplay=Time.realtimeSinceStartup+30,nextReport=Time.realtimeSinceStartup+60;
            float orbitStart=Time.realtimeSinceStartup,nextMetric=orbitStart;
            while(Time.realtimeSinceStartup<end)
            {
                float run=Time.realtimeSinceStartup-orbitStart;
                gallery.SetView(-12+run*6,12,6.2f+0.5f*Mathf.Sin(run*0.1f));
                if(Time.realtimeSinceStartup>=nextMetric)
                {
                    if(((int)(run/60))%2==0)
                        petal.SetShapeParameters(120+29*Mathf.Sin(run*0.13f),0.5f+0.48f*Mathf.Sin(run*0.21f),0.5f+0.48f*Mathf.Sin(run*0.17f+1));
                    else petal.SetBrainMetrics(0.5f+0.48f*Mathf.Sin(run*0.19f),0.5f+0.48f*Mathf.Sin(run*0.11f));
                    nextMetric=Time.realtimeSinceStartup+1;
                }
                if(Time.realtimeSinceStartup>=nextReplay) { petal.RestartGrowth(); nextReplay+=30; }
                if(Time.realtimeSinceStartup>=nextReport)
                {
                    File.AppendAllText(Path.Combine(output,"SoakProgress.txt"),$"{DateTime.UtcNow:O} frames={samples} memory={Profiler.GetTotalAllocatedMemoryLong()} materials={Resources.FindObjectsOfTypeAll<Material>().Length}\n");
                    nextReport+=60;
                }
                cpuSum+=petal.LastUpdateMilliseconds; maxCpu=Math.Max(maxCpu,petal.LastUpdateMilliseconds);
                allocationMax=Math.Max(allocationMax,petal.LastUpdateAllocation); frameSum+=Time.unscaledDeltaTime; samples++;
                renderCpuSum+=lastRenderCpu;
                FrameTimingManager.CaptureFrameTimings();
                if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0) { gpuSum+=timings[0].gpuFrameTime; gpuSamples++; }
                yield return null;
            }
            GC.Collect();
            Check(Resources.FindObjectsOfTypeAll<Material>().Length==startMaterials,"Stable material count through unfolding replay");
            Check(allocationMax==0,"Fan surface update: zero managed bytes per frame");
            Check(Profiler.GetTotalAllocatedMemoryLong()-startMemory<8*1024*1024,"Unity allocated memory growth below 8 MiB tolerance");
            var report=new Report {
                unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                graphicsApi=SystemInfo.graphicsDeviceType.ToString(),resolution=Screen.width+"x"+Screen.height,
                durationSeconds=seconds,frames=samples,meanFps=samples/frameSum,
                petalCpuMeanMs=cpuSum/samples,petalCpuMaxMs=maxCpu,petalGcMaxBytes=allocationMax,
                gpuMeanMs=gpuSamples>0?gpuSum/gpuSamples:-1,
                cameraSubmitCpuMeanMs=renderCpuSum/samples,
                memoryStart=startMemory,memoryEnd=Profiler.GetTotalAllocatedMemoryLong(),
                materialsStart=startMaterials,materialsEnd=Resources.FindObjectsOfTypeAll<Material>().Length,
                checks=checks.ToArray()
            };
            File.WriteAllText(Path.Combine(output,"Validation.json"),JsonUtility.ToJson(report,true));
            Debug.Log("MINDSTATE_VALIDATION_COMPLETE "+JsonUtility.ToJson(report));
            Application.Quit(checks.Exists(x=>x.StartsWith("FAIL"))?2:0);
        }
        IEnumerator Capture(string name,float yaw,float pitch,float distance)
        {
            gallery.showInterface=false; gallery.SetView(yaw,pitch,distance);
            yield return null;
            renderCamera.Render();
            var previous=RenderTexture.active; RenderTexture.active=renderTarget;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            Destroy(image); RenderTexture.active=previous;
            yield return null; yield return null;
        }
        void ValidateMesh(string name)
        {
            Mesh mesh=petal.RuntimeMesh; var v=mesh.vertices; var n=mesh.normals; var tr=mesh.triangles;
            bool finite=true,valid=true;
            int badNormal=0,badFace=0,firstBad=-1,badWinding=0,firstWinding=-1;
            bool bounds=true;
            for(int i=0;i<v.Length;i++)
            {
                bool good=!float.IsNaN(v[i].sqrMagnitude)&&!float.IsInfinity(v[i].sqrMagnitude)&&n[i].sqrMagnitude>0.9f;
                bounds&=mesh.bounds.Contains(v[i]);
                finite&=good; if(!good) { badNormal++; if(firstBad<0) firstBad=i; }
            }
            for(int i=0;i<tr.Length;i+=3)
            {
                bool good=Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]).sqrMagnitude>1e-18f;
                valid&=good; if(!good) badFace++;
                Vector3 face=Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]);
                if(Vector3.Dot(face,n[tr[i]]+n[tr[i+1]]+n[tr[i+2]])<=0) { badWinding++; if(firstWinding<0) firstWinding=i; }
            }
            if(!finite||!valid||badWinding>0||!bounds) Debug.LogWarning($"Mesh diagnostics: badNormals={badNormal}, first={firstBad}, badFaces={badFace}, wrongWinding={badWinding}, bounds={bounds}");
            if(firstWinding>=0) Debug.LogWarning($"First winding triangle {firstWinding/3}: vertices {tr[firstWinding]},{tr[firstWinding+1]},{tr[firstWinding+2]} positions {v[tr[firstWinding]]:F5} {v[tr[firstWinding+1]]:F5} {v[tr[firstWinding+2]]:F5}");
            Check(finite&&valid&&bounds&&badWinding==0,name+": finite, normals, faces, winding, bounds");
        }
        void Check(bool passed,string name) { checks.Add((passed?"PASS ":"FAIL ")+name); Debug.Log(checks[checks.Count-1]); }
        void OnDestroy()
        {
            if(renderCamera!=null) { renderCamera.targetTexture=null; renderCamera.enabled=true; }
            if(renderTarget!=null) { renderTarget.Release(); Destroy(renderTarget); }
        }
        static string Value(string[] args,string key,string fallback) { int i=Array.IndexOf(args,key); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
        [Serializable] sealed class Report
        {
            public string unity,gpu,cpu,graphicsApi,resolution;
            public float durationSeconds;
            public int frames,materialsStart,materialsEnd;
            public double meanFps,petalCpuMeanMs,petalCpuMaxMs,gpuMeanMs,cameraSubmitCpuMeanMs;
            public long petalGcMaxBytes,memoryStart,memoryEnd;
            public string[] checks;
        }
    }
}
