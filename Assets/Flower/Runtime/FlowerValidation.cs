#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace MindState
{
    // Explicit opt-in diagnostics; never present during normal exhibition playback.
    public sealed class FlowerValidation : MonoBehaviour
    {
        FlowerAssembler flower;
        FlowerGallery gallery;
        Camera cameraComponent;
        RenderTexture target;
        string output;
        FlowerPreset layoutOverride;
        readonly List<string> checks=new List<string>();
        struct FrameSample { public double cpu,update,gpu,interpolation,upload,transform,material,wall; public long gc,updateGc,vertices,batches; }
        [Serializable] public sealed class Metrics
        { public string state; public int samples; public double interpolationMs,uploadMs,transformMs,materialMs,wallMeanMs; public double cpuMeanMs,cpuP95Ms,updateMeanMs,updateP95Ms,gpuMeanMs=-1; public long maxGc,updateGc,vertices,batches; public int meshUpdates; }
        [Serializable] public sealed class Report
        { public string unity,device,cpu,gpu,gpuTiming="Unavailable unless positive GPU counter samples are recorded",pipeline="Built-in",resolution="1920x1080 MSAA4 / HDR / Bloom off",notes; public int petals,sharedMeshes,meshVerticesPerPetal=21730,trianglesPerPetal=41792,renderers; public long poseCacheBytes,geometryBytes,totalAllocatedMemory,managedMemory; public double initializationMs; public float interpolationMaxError,normalMaxAngle,rootAnchorMaxError; public string[] checks; public Metrics[] performance; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        { if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyFlower")>=0) Camera.main.gameObject.AddComponent<FlowerValidation>(); }
        void LateUpdate() { if(cameraComponent!=null) cameraComponent.Render(); }
        int WorkingMeshCount()
        {
            var meshes=new System.Collections.Generic.HashSet<Mesh>();
            foreach(var petal in flower.GetDiagnosticPetals()) meshes.Add(flower.PetalMesh(petal.layer,petal.index));
            for(int i=0;i<flower.SharedMeshCount;i++) meshes.Remove(flower.SharedMesh(i));
            return meshes.Count;
        }
        void Check(bool ok,string text)
        { checks.Add((ok?"PASS ":"FAIL ")+text); if(!ok) Debug.LogError("FLOWER_CHECK "+text); }
        IEnumerator Start()
        {
            output=Arg("-evidencePath",Path.Combine(Application.persistentDataPath,"FlowerAssembly")); Directory.CreateDirectory(output);
            flower=FindObjectOfType<FlowerAssembler>(); gallery=FindObjectOfType<FlowerGallery>();
            yield return null;
            string layoutPath=Arg("-flowerLayoutJson","");
            if(layoutPath.Length>0)
            {
                layoutOverride=Instantiate(flower.preset); layoutOverride.ApplyLayoutJson(File.ReadAllText(layoutPath));
                flower.preset=layoutOverride; flower.RebuildFlower(); yield return null; yield return null;
            }
            gallery.showInterface=false; flower.materialBreathing=false;
            cameraComponent=GetComponent<Camera>(); cameraComponent.enabled=false;
            target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 }; target.Create(); cameraComponent.targetTexture=target;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=int.Parse(Arg("-flowerFps","-1"));
            yield return new WaitForSeconds(1);
            Check(Application.unityVersion=="2022.3.62f3c1","Exact Unity version");
            Check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null,"Built-in pipeline");
            int expectedMeshes=0; foreach(var layer in flower.preset.layers) expectedMeshes+=layer.surfaceProfile.softFolds&&layer.count>1?2:1;
            Check(flower.PetalCount==18&&flower.LayerCount==3&&flower.SharedMeshCount==expectedMeshes,"18 petals, three layers, expected shared surface variants");
            Check(flower.CacheBytes-flower.BottomPatchCacheBytes<172*1024*1024,"Base pose cache under 172 MiB");
            Check(flower.GeometryBytes<320*1024*1024,"Cache plus per-petal working geometry under 320 MiB");
            Check(flower.MaxCachePositionError<=.00255f,"Midpoint cache position error <= 0.1% petal length");
            File.WriteAllText(Path.Combine(output,"PackedInterpolation.txt"),flower.VerifyPackedInterpolation());
            ValidateClosure();
            flower.SetBloomProgress(1);
            bool distinct=false; var firstVariant=flower.PetalMesh(0,0).vertices; var secondVariant=flower.PetalMesh(0,1).vertices;
            for(int k=0;k<firstVariant.Length;k++) distinct|=(firstVariant[k]-secondVariant[k]).sqrMagnitude>1e-8f;
            Check(flower.preset.layers[0].surfaceProfile.softFolds?distinct&&flower.SharedMeshCount==expectedMeshes:!distinct,"Surface variants match the selected fold mode");
            if(Arg("-flowerIdColors","")=="1") { flower.diagnosticColors=true; flower.ApplyDiagnosticColors(); }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flowerContacts")>=0)
            {
                var scan=new FlowerSurfaceCollision(flower,1);
                var hits=new List<FlowerSurfaceCollision.Contact>();
                var csv=new StringBuilder("progress,petalA,petalB,layerA,layerB,kind,lengthA,lengthB,sA,sB,x,y,z"+Environment.NewLine);
                var summary=new StringBuilder();
                var coverage=new StringBuilder("progress,layer,petalA,petalB,above,below,touching,status"+Environment.NewLine);
                foreach(float phase in new[]{0f,.25f,.5f,.75f,1f})
                {
                    flower.SetBloomProgress(phase); scan.Sample(phase); scan.FindContacts(hits,parallel:true);
                    int same=0,cross=0,self=0,roots=0;
                    foreach(var h in hits)
                    {
                        var a=scan.Petals[h.petalA]; var b=scan.Petals[h.petalB];
                        string kind=h.rootRegion?"root":h.petalA==h.petalB?"self":a.layer==b.layer?"same":"cross";
                        if(kind=="root") roots++; else if(kind=="self") self++; else if(kind=="same") same++; else cross++;
                        csv.AppendFormat(CultureInfo.InvariantCulture,"{0:F3},{1},{2},{3},{4},{5},{6:F5},{7:F5},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4}"+Environment.NewLine,phase,a.id,b.id,a.layer,b.layer,kind,h.lengthA,h.lengthB,h.widthA,h.widthB,h.point.x,h.point.y,h.point.z);
                    }
                    summary.AppendFormat(CultureInfo.InvariantCulture,"progress={0} contacts={1} sameLayer={2} crossLayer={3} self={4} roots={5}"+Environment.NewLine,phase,hits.Count,same,cross,self,roots);
                    for(int a=0;a<scan.Petals.Length;a++)
                    {
                        int layer=scan.Petals[a].layer;
                        int next=(scan.Petals[a].index+1)%flower.preset.layers[layer].count,b=-1;
                        for(int k=0;k<scan.Petals.Length;k++) if(scan.Petals[k].layer==layer&&scan.Petals[k].index==next) b=k;
                        scan.NeighborCoverage(a,b,40,out int above,out int below,out int touch);
                        coverage.AppendFormat(CultureInfo.InvariantCulture,"{0:F3},{1},{2},{3},{4},{5},{6},{7}"+Environment.NewLine,phase,layer,scan.Petals[a].id,scan.Petals[b].id,above,below,touch,above>0&&below==0?touch==0?"PASS":"TOUCH":"FAIL");
                    }
                    File.WriteAllText(Path.Combine(output,"ContactSummary.txt"),summary.ToString());
                    File.WriteAllText(Path.Combine(output,"Contacts.csv"),csv.ToString());
                    File.WriteAllText(Path.Combine(output,"NeighborCoverage.csv"),coverage.ToString());
                    File.WriteAllText(Path.Combine(output,"Preset.json"),JsonUtility.ToJson(flower.preset,true));
                    yield return null;
                }
                Debug.Log("MINDSTATE_FLOWER_CONTACTS_DONE "+output);
                Application.Quit(0); yield break;
            }
            yield return Capture("01_ThreeQuarter",22,55);
            yield return Capture("02_Front",0,90);
            yield return Capture("03_Side",22,8);
            yield return Capture("04_Closeup",40,48,5.6f);
            for(int i=0;i<5;i++) { flower.SetBloomProgress(i*.25f); yield return Capture("05_Bloom_"+(i*25).ToString("000"),22,55); if(i<3) yield return Capture("05_Bloom_"+(i*25).ToString("000")+"_Closeup",40,48,4.8f); Check(flower.RootAnchorError()<.00001f,"Root attachment at progress "+i*.25f); ValidateMesh(); }
            for(int i=0;i<8;i++) { flower.SetBloomProgress(1); yield return Capture("06_Orbit_"+(i*45).ToString("000"),i*45,40); }
            if(Arg("-flowerGrayScreens","")=="1")
            {
                foreach(var r in flower.GetComponentsInChildren<MeshRenderer>()) if(r.sharedMaterial==flower.petalPrefab.pearlMaterial)
                { var block=new MaterialPropertyBlock(); r.GetPropertyBlock(block); block.SetFloat("_GrayMode",1); r.SetPropertyBlock(block); }
                flower.SetBloomProgress(1);
                yield return Capture("07_Gray_ThreeQuarter",22,55); yield return Capture("07_Gray_Side",22,8); yield return Capture("07_Gray_Closeup",40,48,5.6f);
                foreach(var r in flower.GetComponentsInChildren<MeshRenderer>()) if(r.sharedMaterial==flower.petalPrefab.pearlMaterial)
                { var block=new MaterialPropertyBlock(); r.GetPropertyBlock(block); block.SetFloat("_GrayMode",0); r.SetPropertyBlock(block); }
            }
            var diagnostic=flower.GetDiagnosticPetals(); var anchors=new Vector3[diagnostic.Length]; var rotations=new Quaternion[diagnostic.Length];
            flower.SetBloomProgress(0);
            for(int k=0;k<diagnostic.Length;k++) { anchors[k]=diagnostic[k].anchor.position; rotations[k]=diagnostic[k].anchor.rotation; }
            var renderers=flower.GetComponentsInChildren<MeshRenderer>(); foreach(var renderer in renderers) renderer.enabled=false;
            var closureMetrics=new StringBuilder("progress,middleMeanTipRadius\n");
            foreach(float phase in new[]{0,.5f,1})
            {
                flower.SetBloomProgress(phase); float radius=0; int count=0;
                foreach(var petal in diagnostic) if(petal.layer==1)
                {
                    petal.renderer.enabled=true;
                    var vertices=flower.PetalMesh(petal.layer,petal.index).vertices; int index=HeroPetal.LengthSegments*(HeroPetal.WidthSegments+1)+HeroPetal.WidthSegments/2,offset=(HeroPetal.LengthSegments+1)*(HeroPetal.WidthSegments+1);
                    var tip=petal.body.TransformPoint((vertices[index]+vertices[index+offset])*.5f)-flower.transform.position;
                    radius+=new Vector2(tip.x,tip.z).magnitude; count++;
                }
                closureMetrics.AppendFormat(CultureInfo.InvariantCulture,"{0},{1}\n",phase,radius/count);
                for(int k=0;k<diagnostic.Length;k++) diagnostic[k].anchor.SetPositionAndRotation(anchors[k],rotations[k]);
                yield return Capture("08_MiddleFixed_"+(phase*100).ToString("000"),22,55,6.2f);
                yield return Capture("08_MiddleFixedSide_"+(phase*100).ToString("000"),22,8,6.2f);
                yield return Capture("09_MiddleFixedTop_"+(phase*100).ToString("000"),0,90,6.2f);
            }
            foreach(var renderer in renderers) renderer.enabled=true;
            for(int i=1;i<4;i++)
            {
                flower.SetBloomProgress(i*.25f);
                yield return Capture("10_AllTop_"+(i*25).ToString("000"),0,90,6.2f);
                yield return Capture("10_AllView_"+(i*25).ToString("000"),22,55,6.2f);
            }
            flower.SetBloomProgress(1);
            yield return Capture("10_AllTop_100",0,90,6.2f);
            yield return Capture("10_AllView_100",22,55,6.2f);
            flower.SetBloomProgress(.99f); flower.SetBloomProgress(1);
            File.WriteAllText(Path.Combine(output,"ClosureMetrics.csv"),closureMetrics.ToString());
            var report=new Report { unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,petals=flower.PetalCount,sharedMeshes=flower.SharedMeshCount,poseCacheBytes=flower.CacheBytes,geometryBytes=flower.GeometryBytes,initializationMs=flower.InitializationMilliseconds,interpolationMaxError=flower.MaxCachePositionError,normalMaxAngle=flower.MaxCacheNormalAngle,rootAnchorMaxError=flower.RootAnchorError(),renderers=flower.GetComponentsInChildren<MeshRenderer>().Length };
            Material shared=flower.petalPrefab.pearlMaterial; int petals=0;
            foreach(var r in flower.GetComponentsInChildren<MeshRenderer>()) if(r.sharedMaterial==shared) petals++;
            Check(petals==18,"All 18 petals use existing shared Pearl material");
            float original=flower.bloomProgress; flower.SetBloomProgress(float.NaN); Check(original==flower.bloomProgress,"Nonfinite progress ignored");
            flower.SetBloomProgress(2); Check(flower.bloomProgress==1,"Progress clamp");
            int updates=flower.MeshUpdates; flower.frozen=true; yield return new WaitForSeconds(.15f); Check(flower.MeshUpdates==updates,"Frozen hold performs zero mesh updates");
            flower.PlayBloom(); Check(flower.autoPlay&&!flower.frozen&&flower.bloomProgress==0,"Replay resets and unfreezes");
            yield return new WaitForSeconds(.2f); Check(flower.bloomProgress>0,"Playback advances continuously");
            flower.frozen=true; float frozen=flower.bloomProgress; yield return new WaitForSeconds(.15f); Check(flower.bloomProgress==frozen,"Freeze holds progress");
            flower.frozen=false; flower.SetBloomProgress(.5f); yield return null; Check(!flower.autoPlay&&flower.bloomProgress==.5f,"Manual scrub stops playback");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flowerScreensOnly")<0)
            {
                // Clone the preset so count tests never modify production assets.
                var originalPreset=flower.preset; var testPreset=Instantiate(originalPreset); flower.preset=testPreset;
                flower.SetBloomProgress(1); var reference=flower.PetalMesh(0,0).vertices; var second=flower.PetalMesh(0,1).vertices; int materialCount=Resources.FindObjectsOfTypeAll<Material>().Length; int meshCount=Resources.FindObjectsOfTypeAll<Mesh>().Length; int workingCount=WorkingMeshCount();
                var originalRenderers=flower.GetComponentsInChildren<MeshRenderer>(); var matrices=new Matrix4x4[18];
                for(int k=0;k<18;k++) matrices[k]=originalRenderers[k].localToWorldMatrix;
                for(int n=17;n<=19;n++)
                {
                    testPreset.layers[1].count=n==17?5:6; testPreset.layers[2].count=n==19?6:5;
                    flower.RebuildFlower(); yield return null; yield return null;
                    Check(flower.PetalCount==n&&flower.GetComponentsInChildren<MeshRenderer>().Length==n+2,"Count and resource rebuild "+n);
                    Vector3[] rebuilt=flower.PetalMesh(0,0).vertices,rebuiltSecond=flower.PetalMesh(0,1).vertices; bool equal=true; for(int k=0;k<rebuilt.Length;k++) if(rebuilt[k]!=reference[k]||rebuiltSecond[k]!=second[k]) { equal=false; break; }
                    Check(equal,"Same seed and source trajectory remain deterministic "+n);
                    var currentRenderers=flower.GetComponentsInChildren<MeshRenderer>(); bool sameAssembly=true;
                    for(int k=0;k<(n==18?18:7);k++) sameAssembly&=currentRenderers[k].localToWorldMatrix==matrices[k];
                    Check(sameAssembly,"Seeded assembly transforms reproduce "+n);
                    Check(Resources.FindObjectsOfTypeAll<Material>().Length==materialCount,"Rebuild does not clone materials "+n);
                    Check(Resources.FindObjectsOfTypeAll<Mesh>().Length==meshCount+WorkingMeshCount()-workingCount,"Rebuild releases replaced runtime meshes "+n);
                }
                flower.preset=originalPreset; flower.RebuildFlower(); Destroy(testPreset); yield return null; yield return null;
                var results=new List<Metrics>();
                foreach(string state in Arg("-flowerSoak","")=="1"?new[]{"Animated bloom"}:new[]{"Bud hold","Animated bloom","Full bloom hold"})
                { var metrics=new Metrics { state=state }; yield return Measure(metrics); results.Add(metrics); }
                report.performance=results.ToArray();
            }
            report.totalAllocatedMemory=Profiler.GetTotalAllocatedMemoryLong(); report.managedMemory=GC.GetTotalMemory(false); report.checks=checks.ToArray();
            report.notes="Rendered vertices include light/shadow passes; each shared surface variant has 21730 vertices. Runtime caches and working meshes are shared across 18 renderers. CPU frame includes explicit offscreen camera rendering. Zero/unsupported GPU values are reported unavailable.";
            File.WriteAllText(Path.Combine(output,"Validation.json"),JsonUtility.ToJson(report,true));
            File.WriteAllLines(Path.Combine(output,"Checks.txt"),checks);
            Debug.Log("MINDSTATE_FLOWER_VALIDATION_DONE "+output);
            bool failed=checks.Exists(s=>s.StartsWith("FAIL")); Application.Quit(failed?2:0);
        }
        void ValidateClosure()
        {
            var source=Instantiate(flower.petalPrefab); source.enabled=false; source.GetComponent<MeshRenderer>().enabled=false;
            var config=flower.preset.layers[1]; var profile=config.surfaceProfile;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,1,config.maximumCurl,config.edgeFoldScale,profile,731);
            var full=source.RuntimeMesh.vertices; profile.closureBendDegrees=0;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,1,config.maximumCurl,config.edgeFoldScale,profile,731);
            var original=source.RuntimeMesh.vertices; bool equal=true; for(int k=0;k<full.Length;k++) equal&=full[k]==original[k];
            Check(equal,"Closure vanishes exactly at full bloom (all vertices identical)");
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,0,config.maximumCurl,config.edgeFoldScale,profile,731); var unbent=source.RuntimeMesh.vertices;
            profile=config.surfaceProfile;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,0,config.maximumCurl,config.edgeFoldScale,profile,731); var bent=source.RuntimeMesh.vertices;
            int cols=HeroPetal.WidthSegments+1,surface=cols*(HeroPetal.LengthSegments+1); float max=0,root=0;
            for(int k=0;k<surface;k++) { float delta=Vector3.Distance((bent[k]+bent[k+surface])*.5f,(unbent[k]+unbent[k+surface])*.5f); max=Mathf.Max(max,delta); if(k<cols*8) root=Mathf.Max(root,delta); }
            Check(max>.1f&&root<.00001f,"Fixed transform: real longitudinal bending, stable first 10 percent of root");
            profile=config.surfaceProfile; profile.closureSideLeftDegrees=profile.closureSideRightDegrees=0;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,0,config.maximumCurl,config.edgeFoldScale,profile,731); var flat=source.RuntimeMesh.vertices;
            profile=config.surfaceProfile;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,0,config.maximumCurl,config.edgeFoldScale,profile,731); var wrapped=source.RuntimeMesh.vertices;
            int row=HeroPetal.LengthSegments*3/4;
            Vector3 leftFlat=(flat[row*cols]+flat[row*cols+surface])*.5f,rightFlat=(flat[row*cols+cols-1]+flat[row*cols+cols-1+surface])*.5f;
            Vector3 leftWrap=(wrapped[row*cols]+wrapped[row*cols+surface])*.5f,rightWrap=(wrapped[row*cols+cols-1]+wrapped[row*cols+cols-1+surface])*.5f;
            File.WriteAllText(Path.Combine(output,"SideSectionMetrics.csv"),"metric,value\npreviousSpan,"+Vector3.Distance(leftFlat,rightFlat).ToString(CultureInfo.InvariantCulture)+"\nwrappedSpan,"+Vector3.Distance(leftWrap,rightWrap).ToString(CultureInfo.InvariantCulture)+"\nleftDisplacement,"+Vector3.Distance(leftFlat,leftWrap).ToString(CultureInfo.InvariantCulture)+"\nrightDisplacement,"+Vector3.Distance(rightFlat,rightWrap).ToString(CultureInfo.InvariantCulture));
            Check(Vector3.Distance(leftWrap,rightWrap)<Vector3.Distance(leftFlat,rightFlat)*.95f,"Fixed transform: transverse section wraps and narrows its projected span");
            float rootDelta=0; for(int k=0;k<cols*8;k++) rootDelta=Mathf.Max(rootDelta,Vector3.Distance((flat[k]+flat[k+surface])*.5f,(wrapped[k]+wrapped[k+surface])*.5f));
            Check(rootDelta<1e-5f,"Side wrap leaves root first 10 percent fixed");
            profile.closureSideLeftDegrees=profile.closureSideRightDegrees=0; profile.closureBendDegrees=0;
            source.SetShapeImmediateLimited(config.openingAngle,config.curl,config.softness,1,config.maximumCurl,config.edgeFoldScale,profile,731); var noSides=source.RuntimeMesh.vertices;
            bool same=true; for(int k=0;k<full.Length;k++) same&=full[k]==noSides[k];
            Check(same,"Full bloom exactly preserves the previous mesh when both closure scales vanish");
            Destroy(source.gameObject);
        }
        void ValidateMesh()
        {
            bool valid=true;
            for(int l=0;l<flower.SharedMeshCount;l++)
            {
                Mesh m=flower.SharedMesh(l); var v=m.vertices; var n=m.normals; var t=m.tangents; var triangles=m.triangles;
                valid&=v.Length==21730&&triangles.Length==41792*3;
                Bounds b=m.bounds; b.Expand(.0001f);
                for(int k=0;k<v.Length;k++) valid&=!float.IsNaN(v[k].x)&&b.Contains(v[k])&&Mathf.Abs(n[k].magnitude-1)<.002f&&Mathf.Abs(Vector3.Dot(n[k],new Vector3(t[k].x,t[k].y,t[k].z)))<.002f;
                for(int k=0;k<triangles.Length;k+=3) valid&=Vector3.Cross(v[triangles[k+1]]-v[triangles[k]],v[triangles[k+2]]-v[triangles[k]]).sqrMagnitude>1e-22f;
            }
            Check(valid,"Finite mesh, topology, bounds, unit normals, orthogonal tangents, nondegenerate faces at "+flower.bloomProgress);
        }
        IEnumerator Capture(string name,float az,float elevation,float distance=-1)
        {
            gallery.SetView(az,elevation,distance);
            if(Arg("-flowerFixedFrame","")=="1") { gallery.focus=new Vector3(0,.65f,0); gallery.SetView(az,elevation,distance>0?distance:8.5f); gallery.focus=new Vector3(0,.65f,0); }
            yield return null; yield return null;
            cameraComponent.Render(); var before=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG()); Destroy(image); RenderTexture.active=before;
        }
        bool ArgCachedSoak;
        IEnumerator Measure(Metrics metrics)
        {
            ArgCachedSoak=Arg("-flowerSoak","")=="1";
            gallery.SetView(22,55); bool animated=metrics.state=="Animated bloom";
            flower.SetBloomProgress(metrics.state=="Bud hold"?0:1); if(animated) flower.PlayBloom();
            yield return new WaitForSeconds(3); GC.Collect(); yield return null;
            using var cpu=Counter(ProfilerCategory.Internal,"CPU Main Thread Frame Time");
            using var gpu=Counter(ProfilerCategory.Render,"GPU Frame Time");
            using var vertices=Counter(ProfilerCategory.Render,"Vertices Count");
            using var batches=Counter(ProfilerCategory.Render,"Batches Count");
            using var gc=Counter(ProfilerCategory.Memory,"GC Allocated In Frame");
            float seconds=float.Parse(Arg("-flowerSeconds","20"),CultureInfo.InvariantCulture),end=Time.realtimeSinceStartup+seconds;
            var cpuSamples=new double[65536]; var updateSamples=new double[65536];
            var raw=new FrameSample[65536];
            // Exclude recorder setup and raw-buffer allocations from the measured frames.
            for(int warm=0;warm<4;warm++) yield return null;
            end=Time.realtimeSinceStartup+seconds;
            long initialMemory=GC.GetTotalMemory(false); int initialMeshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,initialMaterials=Resources.FindObjectsOfTypeAll<Material>().Length;
            var memoryTrace=new StringBuilder("elapsedSeconds,managedBytes\n"); float memoryStart=Time.realtimeSinceStartup,nextMemory=memoryStart;
            int startUpdates=flower.MeshUpdates,gpuSamples=0; double gpuTotal=0;
            while(Time.realtimeSinceStartup<end&&metrics.samples<cpuSamples.Length)
            {
                if(ArgCachedSoak&&Time.realtimeSinceStartup>=nextMemory) { memoryTrace.AppendFormat(CultureInfo.InvariantCulture,"{0:F1},{1}\n",Time.realtimeSinceStartup-memoryStart,GC.GetTotalMemory(false)); nextMemory+=30; }
                if(animated&&flower.bloomProgress>=1) flower.PlayBloom();
                yield return null;
                int i=metrics.samples++; double c=cpu.Valid?cpu.LastValue/1e6:-1,g=gpu.Valid&&gpu.LastValue>0?gpu.LastValue/1e6:-1;
                cpuSamples[i]=c; updateSamples[i]=flower.LastUpdateMilliseconds; metrics.cpuMeanMs+=c; metrics.updateMeanMs+=flower.LastUpdateMilliseconds;
                if(g>0) { gpuTotal+=g; gpuSamples++; }
                metrics.maxGc=Math.Max(metrics.maxGc,gc.Valid?gc.LastValue:-1); metrics.updateGc=Math.Max(metrics.updateGc,flower.LastUpdateAllocation);
                metrics.vertices=Math.Max(metrics.vertices,vertices.Valid?vertices.LastValue:-1); metrics.batches=Math.Max(metrics.batches,batches.Valid?batches.LastValue:-1);
                metrics.interpolationMs+=flower.InterpolateMs; metrics.uploadMs+=flower.UploadMs; metrics.transformMs+=flower.TransformMs; metrics.materialMs+=flower.MaterialMs; metrics.wallMeanMs+=Time.unscaledDeltaTime*1000;
                raw[i]=new FrameSample { interpolation=flower.InterpolateMs,upload=flower.UploadMs,transform=flower.TransformMs,material=flower.MaterialMs,wall=Time.unscaledDeltaTime*1000, cpu=c,update=flower.LastUpdateMilliseconds,gpu=g,gc=gc.Valid?gc.LastValue:-1,updateGc=flower.LastUpdateAllocation,vertices=vertices.Valid?vertices.LastValue:-1,batches=batches.Valid?batches.LastValue:-1 };
            }
            if(ArgCachedSoak) File.WriteAllText(Path.Combine(output,"MemoryTrace.csv"),memoryTrace.ToString());
            metrics.interpolationMs/=metrics.samples; metrics.uploadMs/=metrics.samples; metrics.transformMs/=metrics.samples; metrics.materialMs/=metrics.samples; metrics.wallMeanMs/=metrics.samples;
            if(Arg("-flowerSoak","")=="1")
            { Check(Resources.FindObjectsOfTypeAll<Mesh>().Length==initialMeshes&&Resources.FindObjectsOfTypeAll<Material>().Length==initialMaterials,"Soak stable mesh/material counts"); GC.Collect(); Check(GC.GetTotalMemory(false)-initialMemory<8*1024*1024,"Soak managed memory growth below 8 MiB"); }
            metrics.meshUpdates=flower.MeshUpdates-startUpdates;
            metrics.cpuMeanMs/=metrics.samples; metrics.updateMeanMs/=metrics.samples;
            Array.Sort(cpuSamples,0,metrics.samples); Array.Sort(updateSamples,0,metrics.samples); int p95=Math.Min(metrics.samples-1,(int)(metrics.samples*.95));
            metrics.cpuP95Ms=cpuSamples[p95]; metrics.updateP95Ms=updateSamples[p95]; metrics.gpuMeanMs=gpuSamples>0?gpuTotal/gpuSamples:-1;
            var csv=new StringBuilder("frame,cpuMs,flowerUpdateMs,gpuMs,frameGc,flowerGc,vertices,batches,interpolateMs,uploadMs,transformMs,materialMs,wallMs\n");
            for(int i=0;i<metrics.samples;i++) { var s=raw[i]; csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:F5},{2:F5},{3:F5},{4},{5},{6},{7},{8:F5},{9:F5},{10:F5},{11:F5},{12:F5}\n",i,s.cpu,s.update,s.gpu,s.gc,s.updateGc,s.vertices,s.batches,s.interpolation,s.upload,s.transform,s.material,s.wall); }
            File.WriteAllText(Path.Combine(output,metrics.state.Replace(' ','_')+".csv"),csv.ToString());
            Check(metrics.updateGc==0,"Zero per-frame flower managed allocation / "+metrics.state);
            if(!animated) Check(metrics.meshUpdates==0,"No mesh upload in "+metrics.state);
        }
        static ProfilerRecorder Counter(ProfilerCategory category,string name)=>new ProfilerRecorder(category,name,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);
        static string Arg(string key,string fallback) { var a=Environment.GetCommandLineArgs(); int i=Array.IndexOf(a,key); return i>=0&&i+1<a.Length?a[i+1]:fallback; }
        void OnDestroy() { if(target!=null) { target.Release(); Destroy(target); } if(layoutOverride!=null) Destroy(layoutOverride); }
    }
}
#endif




