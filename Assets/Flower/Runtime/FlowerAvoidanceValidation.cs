#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MindState
{
    // Explicit command-line offline job; never attached during exhibition playback.
    public sealed class FlowerAvoidanceValidation : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string configuration,fingerprint,status,definition="Sampled mid-surface intersections. Root <=8% on BOTH petals is reported, NOT automatically exempted. Touching is not crossing. Finite samples are not a continuous-time proof.";
            public int seed,samples,sameLayer,crossLayer,self,roots;
            public float proxyError;
        }
        FlowerAssembler flower; FlowerGallery gallery; string output;
        readonly List<FlowerSurfaceCollision.Contact> hits=new List<FlowerSurfaceCollision.Contact>();
        readonly List<Result> results=new List<Result>();
        RenderTexture target; Camera cam;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        { if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flowerAvoidance")>=0) Camera.main.gameObject.AddComponent<FlowerAvoidanceValidation>(); }
        IEnumerator Start()
        {
            yield return null;
            output=Arg("-evidencePath",Path.Combine(Application.persistentDataPath,"FlowerAvoidance")); Directory.CreateDirectory(output);
            flower=FindObjectOfType<FlowerAssembler>(); gallery=FindObjectOfType<FlowerGallery>(); gallery.showInterface=false;
            var original=flower.preset; var preset=Instantiate(original); flower.preset=preset; flower.autoPlay=false; flower.materialBreathing=false;
            cam=GetComponent<Camera>(); cam.enabled=false;
            target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 }; target.Create(); cam.targetTexture=target;
            PredicateTests();
            if(Has("-avoidanceBaseline")) preset.layers=FlowerPreset.Defaults();
            string json=Arg("-layoutJson",""); if(json.Length>0) preset.ApplyLayoutJson(File.ReadAllText(json));
            foreach(var l in preset.layers) { if(Has("-avoidanceStress")) l.widthVariation=.005f; l.Constrain(); }
            flower.diagnosticExtremeVariation=Has("-avoidanceStress");
            if(Has("-avoidanceNatural")) { yield return TuneNatural(preset); }
            else if(Has("-avoidanceFold")) { yield return TuneFold(preset); }
            else if(Has("-avoidanceStagger")) { yield return TuneStagger(preset); }
            else if(Has("-avoidanceGeometry")) { yield return TuneGeometry(preset); }
            else if(Has("-avoidanceTune")||Has("-avoidanceRefine")) { yield return Tune(preset); }
            else
            {
                string[] seeds=Arg("-avoidanceSeeds","731,17,42,137,2026").Split(',');
                foreach(string seed in seeds)
                {
                    preset.randomSeed=int.Parse(seed,Inv); flower.RebuildFlower(); yield return null; yield return null;
                    IntegrationTests();
                    if(Has("-shingleTune")) { if(Has("-shingleAnimated")) TuneAnimatedCoverage(); else TuneCoverage(); flower.RebuildFlower(); }
                    WriteCoverage();
                    if(Has("-shingleTune")) { File.WriteAllText(Path.Combine(output,"TunedPreset.json"),JsonUtility.ToJson(preset,true)); Application.Quit(); yield break; }
                    yield return Scan(preset,Has("-avoidanceBaseline")?"Baseline":Has("-avoidanceStress")?"StressCandidate":"Candidate");
                }
            }
            flower.preset=original; Destroy(preset);
            bool failed=results.Exists(r=>r.status!="PASS"); Debug.Log("FLOWER_AVOIDANCE_DONE "+output);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(failed?2:0);
#else
            Application.Quit(failed?2:0);
#endif
        }
        IEnumerator Scan(FlowerPreset preset,string label)
        {
            int steps=int.Parse(Arg("-avoidanceSteps","201"),Inv); bool exact=!Has("-avoidanceCoarse");
            var scan=new FlowerSurfaceCollision(flower,exact?1:8);
            var proxy=exact?new FlowerSurfaceCollision(flower,8):scan;
            var result=new Result { configuration=label,fingerprint=Fingerprint(preset),seed=preset.randomSeed };
            var times=new SortedSet<float>(); for(int i=0;i<steps;i++) times.Add(i/(float)(steps-1));
            foreach(var l in preset.layers) for(int k=0;k<FlowerAssembler.PoseCount;k++) times.Add(Mathf.Clamp01((l.delay+l.duration*k/(FlowerAssembler.PoseCount-1))/preset.Duration));
            for(int k=0;k<5;k++) for(int d=-1;d<=1;d++) times.Add(Mathf.Clamp01(k*.25f+d*.01f));
            var csv=new StringBuilder("progress,petalA,petalB,kind,lengthA,lengthB,x,y,z\n");
            var refinement=new List<float>(); float previous=-1; bool previousHit=false; int index=0;
            foreach(float p in times)
            {
                scan.Sample(p); scan.FindContacts(hits,parallel:exact); Accumulate(result,scan,p,csv);
                if(exact) proxy.Sample(p);
                if(previous>=0&&(hits.Count>0||previousHit||scan.NearCandidates)) refinement.Add((previous+p)*.5f);
                previous=p; previousHit=hits.Count>0||scan.NearCandidates;
                if(++index%5==0) { File.WriteAllText(Path.Combine(output,"Progress.txt"),label+" seed="+preset.randomSeed+" "+index+"/"+times.Count); yield return null; }
            }
            foreach(float p in refinement) { scan.Sample(p); scan.FindContacts(hits,parallel:exact); Accumulate(result,scan,p,csv); if(++index%5==0) { File.WriteAllText(Path.Combine(output,"Progress.txt"),label+" seed="+preset.randomSeed+" refined="+(index-times.Count)+"/"+refinement.Count); yield return null; } }
            result.proxyError=proxy.MaxProxyError; result.status=exact&&result.sameLayer+result.crossLayer+result.self+result.roots==0?"PASS":"FAIL / unvalidated";
            results.Add(result);
            string prefix=label+"_"+preset.randomSeed;
            File.WriteAllText(Path.Combine(output,prefix+".json"),JsonUtility.ToJson(result,true));
            File.WriteAllText(Path.Combine(output,prefix+".csv"),csv.ToString());
            File.WriteAllText(Path.Combine(output,prefix+"_Preset.json"),JsonUtility.ToJson(preset,true));
            if(preset.randomSeed==731&&!Has("-avoidanceNoScreens"))
            {
                for(int k=0;k<5;k++) for(int d=-1;d<=1;d++)
                {
                    float p=Mathf.Clamp01(k*.25f+d*.01f); flower.SetBloomProgress(p);
                    foreach(bool debug in new[]{false,true})
                    {
                        flower.diagnosticColors=debug; flower.ApplyDiagnosticColors();
                        Capture(prefix+"_"+(p*1000).ToString("0000")+(debug?"_IDs":"")+"_Front",0,90);
                        Capture(prefix+"_"+(p*1000).ToString("0000")+(debug?"_IDs":"")+"_Side",22,8);
                        Capture(prefix+"_"+(p*1000).ToString("0000")+(debug?"_IDs":"")+"_ThreeQuarter",22,55);
                        if(!debug&&d==0) Capture(prefix+"_"+(p*1000).ToString("0000")+"_Closeup",22,55,gallery.distance*.6f);
                    }
                    yield return null;
                }
                flower.diagnosticColors=false; flower.ApplyDiagnosticColors();
                for(int k=0;k<8;k++) { flower.SetBloomProgress(.5f); Capture(prefix+"_Orbit_"+k,k*45,40); yield return null; }
            }
        }
        void Accumulate(Result r,FlowerSurfaceCollision scan,float progress,StringBuilder csv)
        {
            r.samples++;
            foreach(var h in hits)
            {
                var a=scan.Petals[h.petalA]; var b=scan.Petals[h.petalB];
                string kind=h.rootRegion?"root":h.petalA==h.petalB?"self":a.layer==b.layer?"same":"cross";
                if(kind=="root") r.roots++; else if(kind=="self") r.self++; else if(kind=="same") r.sameLayer++; else r.crossLayer++;
                csv.AppendFormat(Inv,"{0:F7},{1},{2},{3},{4:F5},{5:F5},{6:F6},{7:F6},{8:F6}\n",progress,a.id,b.id,kind,h.lengthA,h.lengthB,h.point.x,h.point.y,h.point.z);
            }
        }
        IEnumerator TuneNatural(FlowerPreset preset)
        {
            // Layout-only search: cached geometry, timing, widths and facing remain fixed.
            flower.RebuildFlower(); yield return null;
            var scan=new FlowerSurfaceCollision(flower,8);
            var basis=CloneLayers(preset.layers);
            var previous=Instantiate(preset);
            string baselineJson=Arg("-naturalBaselineJson","");
            if(string.IsNullOrWhiteSpace(baselineJson)||!File.Exists(baselineJson)) throw new ArgumentException("Natural search requires an explicit existing -naturalBaselineJson file.");
            previous.ApplyLayoutJson(File.ReadAllText(baselineJson));
            File.WriteAllText(Path.Combine(output,"BaselineUsed.json"),JsonUtility.ToJson(previous,true));
            var times=new[]{0,.01f,.125f,.24f,.25f,.26f,.375f,.49f,.5f,.51f,.625f,.74f,.75f,.76f,.875f,.99f,1};
            var known=new HashSet<int>[times.Length];
            for(int i=0;i<times.Length;i++)
            {
                known[i]=new HashSet<int>(); scan.Sample(times[i],previous.layers); scan.FindContacts(hits);
                foreach(var h in hits) known[i].Add(ContactKey(h));
            }
            Destroy(previous);
            var log=new StringBuilder("candidate,score,newSame,newCross,same,cross,roots,middleSize,amplitude\n");
            var best=CloneLayers(basis); double bestScore=double.MaxValue;
            var random=new System.Random(1907); int budget=int.Parse(Arg("-avoidanceBudget","64"),Inv);
            for(int candidate=0;candidate<budget;candidate++)
            {
                var layout=CloneLayers(basis); float amplitude=candidate==0?1:new[]{.75f,1,1.25f}[random.Next(3)];
                if(candidate>0)
                {
                    layout[1].size=basis[1].size+Range(random,-.02f,.02f);
                    for(int i=0;i<layout.Length;i++)
                    {
                        layout[i].openTilt=basis[i].openTilt+Range(random,-3,3);
                        layout[i].rootHeight=basis[i].rootHeight+Range(random,-.02f,.02f);
                        foreach(var offset in layout[i].petalOffsets)
                        { offset.height*=amplitude; offset.tilt*=amplitude; offset.lengthScale=1+(offset.lengthScale-1)*amplitude; }
                        layout[i].Constrain();
                    }
                }
                int same=0,cross=0,roots=0,newSame=0,newCross=0;
                for(int i=0;i<times.Length;i++)
                {
                    scan.Sample(times[i],layout); scan.FindContacts(hits);
                    foreach(var h in hits)
                    {
                        if(h.rootRegion) { roots++; continue; }
                        bool isCross=scan.Petals[h.petalA].layer!=scan.Petals[h.petalB].layer;
                        if(isCross) cross++; else same++;
                        if(!known[i].Contains(ContactKey(h))) { if(isCross) newCross++; else newSame++; }
                    }
                }
                double deviation=Math.Abs(layout[1].size-basis[1].size)*20+Math.Abs(amplitude-1);
                for(int i=0;i<layout.Length;i++) deviation+=Math.Abs(layout[i].openTilt-basis[i].openTilt)/3+Math.Abs(layout[i].rootHeight-basis[i].rootHeight)*50;
                double score=roots*1e8+newCross*1e6+newSame*1e4+cross*100+same+deviation*.001;
                if(score<bestScore) { bestScore=score; best=layout; }
                log.AppendFormat(Inv,"{0},{1:F6},{2},{3},{4},{5},{6},{7:F6},{8:F2}\n",candidate,score,newSame,newCross,same,cross,roots,layout[1].size,amplitude);
                File.WriteAllText(Path.Combine(output,"Progress.txt"),"Natural layout search "+(candidate+1)+"/"+budget);
                yield return null;
            }
            preset.layers=best;
            File.WriteAllText(Path.Combine(output,"Candidate.json"),JsonUtility.ToJson(preset,true));
            File.WriteAllText(Path.Combine(output,"NaturalSearch.csv"),log.ToString());
            flower.RebuildFlower(); yield return null; yield return Scan(preset,"NaturalCandidate");
        }
        static int ContactKey(FlowerSurfaceCollision.Contact h)=>h.petalA*1024+h.petalB*2+(h.rootRegion?1:0);
        IEnumerator Tune(FlowerPreset preset)
        {
            // Bounded deterministic coordinate search. Coarse scores are never certification.
            var random=new System.Random(1907); var log=new StringBuilder("candidate,layer,score,closedTilt,openTilt,roll,radius,height\n");
            int budget=int.Parse(Arg("-avoidanceBudget","120"),Inv);
            for(int layer=0;layer<preset.layers.Length;layer++) ConstrainSearch(preset.layers[layer],layer);
            flower.RebuildFlower(); yield return null;
            var scan=new FlowerSurfaceCollision(flower,8);
            if(!Has("-avoidanceRefine")) for(int layer=0;layer<preset.layers.Length;layer++)
            {
                var best=Clone(preset.layers[layer]); float bestScore=Score(scan,preset.layers,layer);
                for(int candidate=0;candidate<budget;candidate++)
                {
                    var l=Clone(best);
                    float range=candidate<budget/2?1:.35f;
                    l.closedTilt=best.closedTilt+Range(random,-6,6)*range;
                    l.openTilt=best.openTilt+Range(random,-6,6)*range;
                    ConstrainSearch(l,layer);
                    preset.layers[layer]=l; float score=Score(scan,preset.layers,layer);
                    if(score<bestScore) { bestScore=score; best=Clone(l); }
                    log.AppendFormat(Inv,"{0},{1},{2:F5},{3:F3},{4:F3},{5:F3},{6:F4},{7:F4}\n",candidate,layer,score,l.closedTilt,l.openTilt,l.roll,l.rootRadius,l.rootHeight);
                    if(candidate%5==0) { File.WriteAllText(Path.Combine(output,"TuneProgress.txt"),"layer="+layer+" candidate="+candidate+" best="+bestScore); yield return null; }
                }
                preset.layers[layer]=best;
            }
            // Cross-layer nesting: fixed radial facing; never spin petals into a pinwheel.
            float globalBest=Score(scan,preset.layers,-1); var bestLayout=CloneLayers(preset.layers);
            for(int candidate=0;candidate<budget;candidate++)
            {
                var layout=CloneLayers(bestLayout); int layer=candidate%layout.Length; var l=layout[layer];
                l.rootHeight+=Range(random,-.04f,.04f);
                l.closedTilt+=Range(random,-4,4); l.openTilt+=Range(random,-4,4);
                ConstrainSearch(l,layer);
                float score=Score(scan,layout,-1); if(score<globalBest) { globalBest=score; bestLayout=layout; }
                if(candidate%5==0) { File.WriteAllText(Path.Combine(output,"TuneProgress.txt"),"cross candidate="+candidate+" best="+globalBest); yield return null; }
            }
            preset.layers=bestLayout;
            File.WriteAllText(Path.Combine(output,"Candidate.json"),JsonUtility.ToJson(preset,true)); File.WriteAllText(Path.Combine(output,"Tune.csv"),log.ToString());
            flower.RebuildFlower(); yield return null; yield return Scan(preset,"TunedCandidate");
        }
        float Score(FlowerSurfaceCollision scan,FlowerLayer[] layout,int layer)
        {
            float score=0;
            for(int k=0;k<9;k++)
            {
                float p=k/8f; scan.Sample(p,layout,layer); scan.FindContacts(hits,false,layer);
                foreach(var h in hits) if(!h.rootRegion) score+=10+Mathf.Max(h.lengthA,h.lengthB); else score+=.1f;
            }
            // Small regularizer only breaks equal-contact solutions.
            for(int i=0;i<layout.Length;i++)
            {
                var l=layout[i]; float open=i==0?68:i==1?50:40,closed=i==0?54:i==1?52:50;
                score+=.0001f*(l.roll*l.roll*.25f+(l.openTilt-open)*(l.openTilt-open)+(l.closedTilt-closed)*(l.closedTilt-closed));
                if(i>0) score+=Mathf.Pow(Mathf.Max(0,l.openTilt+6-layout[i-1].openTilt),2)*.1f;
            }
            return score;
        }
        IEnumerator TuneGeometry(FlowerPreset preset)
        {
            // Second, bounded bank: only existing geometry parameters and allowed X scale.
            // Rebuilds are initialization-only, and every score samples the new cache.
            flower.RebuildFlower(); yield return null;
            var scan=new FlowerSurfaceCollision(flower,8); float bestScore=Score(scan,preset.layers,-1);
            var best=CloneLayers(preset.layers); var log=new StringBuilder("layer,maximumCurl,curl,width,score\n");
            for(int layer=0;layer<preset.layers.Length;layer++)
            {
                float originalCurl=best[layer].curl;
                foreach(float cap in new[]{1f,.95f,.9f,.85f}) foreach(float endCurl in new[]{originalCurl,.10f})
                {
                    preset.layers=CloneLayers(best); var l=preset.layers[layer]; l.maximumCurl=cap; l.curl=Mathf.Min(cap,endCurl);
                    flower.RebuildFlower(); yield return null; yield return null;
                    float score=Score(scan,preset.layers,-1);
                    log.AppendFormat(Inv,"{0},{1},{2},{3},{4}\n",layer,cap,endCurl,l.width,score);
                    if(score<bestScore) { bestScore=score; best=CloneLayers(preset.layers); }
                    File.WriteAllText(Path.Combine(output,"TuneProgress.txt"),"geometry layer="+layer+" cap="+cap+" curl="+endCurl+" best="+bestScore);
                }
            }
            preset.layers=best; flower.RebuildFlower(); yield return null;
            File.WriteAllText(Path.Combine(output,"Candidate.json"),JsonUtility.ToJson(preset,true)); File.WriteAllText(Path.Combine(output,"GeometrySearch.csv"),log.ToString());
            yield return Scan(preset,"GeometryCandidate");
        }
        IEnumerator TuneStagger(FlowerPreset preset)
        {
            flower.RebuildFlower(); yield return null;
            var scan=new FlowerSurfaceCollision(flower,8); float bestScore=float.MaxValue;
            var best=CloneLayers(preset.layers); int candidate=0;
            foreach(float outer in new[]{0,.03f,.06f,.09f,.12f})
            foreach(float middle in new[]{0,.03f,.06f,.09f,.12f})
            foreach(float inner in new[]{0,.03f,.06f,.09f,.12f})
            {
                preset.layers[0].rootStagger=outer; preset.layers[1].rootStagger=middle; preset.layers[2].rootStagger=inner;
                float score=Score(scan,preset.layers,-1);
                if(score<bestScore) { bestScore=score; best=CloneLayers(preset.layers); }
                if(++candidate%5==0) { File.WriteAllText(Path.Combine(output,"TuneProgress.txt"),"root stagger candidate="+candidate+"/125 best="+bestScore); yield return null; }
            }
            preset.layers=best; flower.RebuildFlower(); yield return null;
            File.WriteAllText(Path.Combine(output,"Candidate.json"),JsonUtility.ToJson(preset,true));
            yield return Scan(preset,"CenteredStaggerCandidate");
        }
        IEnumerator TuneFold(FlowerPreset preset)
        {
            flower.RebuildFlower(); yield return null;
            var scan=new FlowerSurfaceCollision(flower,8); float bestScore=float.MaxValue;
            var best=CloneLayers(preset.layers); int candidate=0;
            var basis=CloneLayers(preset.layers);
            foreach(float outer in new[]{1,.85f,.7f}) foreach(float middle in new[]{1,.85f,.7f}) foreach(float inner in new[]{1,.85f,.7f})
            {
                var factors=new[]{outer,middle,inner};
                for(int i=0;i<3;i++)
                {
                    preset.layers[i].edgeFoldScale=basis[i].edgeFoldScale*factors[i];
                    var profile=basis[i].surfaceProfile; profile.surfaceDetail*=factors[i]; preset.layers[i].surfaceProfile=profile;
                }
                flower.RebuildFlower(); yield return null; yield return null;
                float score=Score(scan,preset.layers,-1);
                if(score<bestScore) { bestScore=score; best=CloneLayers(preset.layers); }
                File.WriteAllText(Path.Combine(output,"TuneProgress.txt"),"edge fold candidate="+(++candidate)+"/27 best="+bestScore);
            }
            preset.layers=best; flower.RebuildFlower(); yield return null;
            File.WriteAllText(Path.Combine(output,"Candidate.json"),JsonUtility.ToJson(preset,true)); yield return Scan(preset,"CenteredFoldCandidate");
        }
        static void ConstrainSearch(FlowerLayer l,int layer)
        {
            // Do not buy a low collision score by flattening the bud or making a pinwheel.
            float closedMin=layer==0?50:layer==1?48:46,closedMax=layer==0?62:layer==1?60:58;
            float openMin=layer==0?64:layer==1?46:36,openMax=layer==0?76:layer==1?58:48;
            l.closedTilt=Mathf.Clamp(l.closedTilt,closedMin,closedMax); l.openTilt=Mathf.Clamp(l.openTilt,openMin,openMax);
            l.roll=0; l.azimuth=layer==0?0:layer==1?30:12;
            l.width=layer==0?.6f:layer==1?.5f:.4f;
            l.rootHeight=Mathf.Clamp(l.rootHeight,layer==0?0:layer==1?.16f:.24f,layer==0?.05f:layer==1?.32f:.4f);
            l.tangentOffset=0; l.depthOffset=0;
            l.Constrain();
        }
        static FlowerLayer Clone(FlowerLayer l)=>JsonUtility.FromJson<FlowerLayer>(JsonUtility.ToJson(l));
        static FlowerLayer[] CloneLayers(FlowerLayer[] source) { var result=new FlowerLayer[source.Length]; for(int i=0;i<result.Length;i++) result[i]=Clone(source[i]); return result; }
        static float Range(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
        TextMesh[] labels;
        void Capture(string name,float az,float elevation,float distance=-1)
        {
            gallery.SetView(az,elevation,distance);
            var petals=flower.GetDiagnosticPetals();
            if(labels==null)
            {
                labels=new TextMesh[petals.Length];
                for(int i=0;i<labels.Length;i++)
                {
                    var go=new GameObject("Diagnostic label "+petals[i].id); labels[i]=go.AddComponent<TextMesh>();
                    labels[i].text=petals[i].id; labels[i].fontSize=64; labels[i].characterSize=.018f;
                    labels[i].anchor=TextAnchor.MiddleCenter; labels[i].color=new Color(.13f,.04f,.19f);
                }
            }
            for(int i=0;i<labels.Length;i++)
            {
                labels[i].gameObject.SetActive(name.Contains("_IDs"));
                if(!name.Contains("_IDs")) continue;
                var vertices=flower.PetalMesh(petals[i].layer,petals[i].index).vertices;
                int index=64*(HeroPetal.WidthSegments+1)+HeroPetal.WidthSegments/2,surface=(HeroPetal.WidthSegments+1)*(HeroPetal.LengthSegments+1);
                labels[i].transform.SetPositionAndRotation(petals[i].body.TransformPoint((vertices[index]+vertices[index+surface])*.5f)-cam.transform.forward*.04f,cam.transform.rotation);
            }
            cam.Render(); var old=RenderTexture.active; RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG()); Destroy(texture); RenderTexture.active=old;
        }
        static string Fingerprint(FlowerPreset preset)
        {
            var input=new StringBuilder(JsonUtility.ToJson(preset)).Append('|').Append(Application.unityVersion);
            input.Append("|extreme:").Append(Has("-avoidanceStress"));
            foreach(string path in new[]{"HeroPetal/Runtime/HeroPetal.cs","HeroPetal/Prefabs/HeroPetal.prefab","Flower/Runtime/FlowerAssembler.cs","Flower/Runtime/FlowerPreset.cs","Flower/Runtime/FlowerDiagnostics.cs","Flower/Runtime/FlowerSurfaceCollision.cs"})
            {
                string file=Path.Combine(Application.dataPath,path);
                input.Append('|').Append(path).Append('|').Append(File.Exists(file)?File.ReadAllText(file):typeof(HeroPetal).Module.ModuleVersionId.ToString());
            }
            using var sha=SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()))).Replace("-","");
        }
        void TuneAnimatedCoverage()
        {
            var scan=new FlowerSurfaceCollision(flower); var petals=scan.Petals;
            var layout=CloneLayers(flower.preset.layers);
            var csv=new StringBuilder("layer,node,progress,roll,wrong,missing\n");
            for(int layer=0;layer<2;layer++)
            {
                var c=layout[layer]; c.bloomRoll=Array.Empty<float>(); var curve=new float[33]; float previous=0;
                for(int node=0;node<33;node++)
                {
                    float progress=Mathf.Clamp01((c.delay+c.duration*node/32f)/flower.BloomDuration);
                    double bestScore=double.MaxValue; float bestAngle=previous; int bestWrong=0,bestMissing=0;
                    for(int angle=-12;angle<=12;angle+=2)
                    {
                        c.roll=angle; scan.Sample(progress,layout,layer); int wrong=0,missing=0;
                        for(int a=0;a<petals.Length;a++) if(petals[a].layer==layer)
                        {
                            int next=(petals[a].index+1)%c.count,b=-1;
                            for(int k=0;k<petals.Length;k++) if(petals[k].layer==layer&&petals[k].index==next) b=k;
                            scan.NeighborCoverage(a,b,40,out int above,out int below,out int touch);
                            wrong+=below+touch; if(above+below+touch==0) missing++;
                        }
                        double score=wrong*1e4+(node>=24?missing*1e6:0)+Math.Abs(angle-previous);
                        if(score<bestScore) { bestScore=score;bestAngle=angle;bestWrong=wrong;bestMissing=missing; }
                    }
                    curve[node]=bestAngle; previous=bestAngle;
                    csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5}\n",layer,node,progress,bestAngle,bestWrong,bestMissing);
                }
                c.bloomRoll=curve;
            }
            flower.preset.layers=layout; File.WriteAllText(Path.Combine(output,"AnimatedCoverageSearch.csv"),csv.ToString());
        }
        void TuneCoverage()
        {
            var scan=new FlowerSurfaceCollision(flower); var petals=scan.Petals;
            var layout=CloneLayers(flower.preset.layers);
            var csv=new StringBuilder("layer,roll,amplitude,tilt,wrong,covered,missing\n");
            for(int layer=0;layer<2;layer++)
            {
                var basis=Clone(layout[layer]); var best=Clone(basis); double bestScore=double.MaxValue;
                foreach(float roll in new[]{4f,8f,12f}) foreach(float amplitude in new[]{1f,1.25f,1.5f})
                foreach(float tilt in layer==0?new[]{70f}:new[]{50f,55f,60f})
                {
                    var c=Clone(basis); c.roll=roll; c.openTilt=tilt;
                    foreach(var offset in c.petalOffsets) { offset.height*=amplitude/1.35f; offset.tilt*=amplitude/1.35f; offset.lengthScale=1+(offset.lengthScale-1)*amplitude/1.35f; }
                    c.Constrain(); layout[layer]=c;
                    int wrong=0,covered=0,missing=0;
                    foreach(float progress in new[]{.75f,1f})
                    {
                        scan.Sample(progress,layout,layer);
                        for(int a=0;a<petals.Length;a++) if(petals[a].layer==layer)
                        {
                            int next=(petals[a].index+1)%c.count,b=-1;
                            for(int k=0;k<petals.Length;k++) if(petals[k].layer==layer&&petals[k].index==next) b=k;
                            scan.NeighborCoverage(a,b,24,out int above,out int below,out int touch);
                            wrong+=below+touch; covered+=above; if(above+below+touch==0) missing++;
                        }
                    }
                    double score=missing*1e6+wrong*1000+Math.Abs(tilt-basis.openTilt)+Math.Abs(roll-basis.roll);
                    csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5},{6}\n",layer,roll,amplitude,tilt,wrong,covered,missing);
                    if(score<bestScore) { bestScore=score; best=Clone(c); }
                }
                layout[layer]=best;
            }
            flower.preset.layers=layout;
            File.WriteAllText(Path.Combine(output,"CoverageSearch.csv"),csv.ToString());
        }
        void WriteCoverage()
        {
            var scan=new FlowerSurfaceCollision(flower); var petals=scan.Petals;
            var csv=new StringBuilder("progress,petalA,petalB,above,below,touching,status\n");
            var times=new SortedSet<float>(); for(int k=0;k<=64;k++) times.Add(k/64f);
            foreach(var l in flower.preset.layers) for(int k=0;k<=64;k++) times.Add(Mathf.Clamp01((l.delay+l.duration*k/64f)/flower.BloomDuration));
            foreach(float progress in times)
            {
                scan.Sample(progress);
                for(int a=0;a<petals.Length;a++)
                {
                    if(petals[a].layer>1) continue;
                    int next=(petals[a].index+1)%flower.preset.layers[petals[a].layer].count,b=-1;
                    for(int k=0;k<petals.Length;k++) if(petals[k].layer==petals[a].layer&&petals[k].index==next) b=k;
                    scan.NeighborCoverage(a,b,40,out int above,out int below,out int touch);
                    csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5},{6}\n",progress,petals[a].id,petals[b].id,above,below,touch,above>0&&below==0&&touch==0?"PASS":above+below+touch==0?"NO_OVERLAP":"FAIL");
                }
            }
            File.WriteAllText(Path.Combine(output,"NeighborCoverage.csv"),csv.ToString());
        }
        void IntegrationTests()
        {
            var petals=flower.GetDiagnosticPetals();
            var legacyLayer=JsonUtility.FromJson<FlowerLayer>("{\"count\":6,\"size\":1}");
            Require(legacyLayer.surfaceProfile.widePleatCount==7&&legacyLayer.surfaceProfile.surfaceDetail==1&&legacyLayer.surfaceProfile.rimVariation==1&&legacyLayer.microNormalScale==1&&!legacyLayer.surfaceProfile.softFolds,"legacy serialized profile defaults");
            Require(legacyLayer.OffsetAt(0)==null&&legacyLayer.OffsetAt(-1)==null,"legacy/missing offsets preserve layout");
            var partial=JsonUtility.FromJson<FlowerLayer>("{\"petalOffsets\":[{\"height\":0.02}]}");
            Require(partial.OffsetAt(0).lengthScale==1&&partial.OffsetAt(1)==null,"partial serialized offsets use neutral defaults");
            var oldJson=ScriptableObject.CreateInstance<FlowerPreset>(); oldJson.layers[0].petalOffsets=new[]{new FlowerPetalOffset { height=.1f }};
            oldJson.ApplyLayoutJson("{\"layers\":[{\"count\":6,\"size\":1}]}");
            Require(oldJson.layers[0].OffsetAt(0)==null,"loading legacy layout resets optional authored offsets");
            Destroy(oldJson);
            var invalid=new FlowerPetalOffset { height=float.NaN,tilt=99,lengthScale=float.PositiveInfinity }; invalid.Constrain();
            Require(invalid.height==0&&invalid.tilt==8&&invalid.lengthScale==1,"nonfinite authored offset constraints");
            invalid=new FlowerPetalOffset { height=-1,tilt=-99,lengthScale=.1f }; invalid.Constrain();
            Require(invalid.height==-.1f&&invalid.tilt==-8&&invalid.lengthScale==.88f,"authored offset bounds");
            var anchors=new Vector3[petals.Length];
            for(int i=0;i<petals.Length;i++) anchors[i]=petals[i].anchor.position;
            foreach(var p in petals)
            {
                var l=flower.preset.layers[p.layer];
                var offset=l.OffsetAt(p.index);
                float weight=l.OffsetWeight(Mathf.Clamp01((flower.bloomProgress*flower.BloomDuration-l.delay)/l.duration));
                float expectedHeight=l.rootHeight+l.rootStagger*RootStaggerSignForTest(p.index,l.count)+(offset?.height??0)*weight;
                Require(Mathf.Abs(p.anchor.localPosition.y-expectedHeight)<1e-6f,"authored root height applied once");
                float length=p.body.localScale.y/l.size/Mathf.Lerp(1,offset?.lengthScale??1,weight);
                Require(Mathf.Abs(length-1)<=2*l.lengthVariation+1e-6f,"authored longitudinal scale applied");
                if(l.widthVariation==0) Require(Mathf.Abs(p.body.localScale.x/l.size-l.width)<1e-6f,"actual nominal width (no legacy 80% clamp)");
                else Require(p.body.localScale.x/l.size>=.399999f,"random width remains bounded");
                var block=new MaterialPropertyBlock(); p.renderer.GetPropertyBlock(block);
                Require(Mathf.Abs(block.GetFloat("_BumpStrength")-flower.petalPrefab.pearlMaterial.GetFloat("_BumpStrength")*l.microNormalScale)<1e-6f,"layer micro normal strength");
            }
            foreach(float progress in new[]{0,.25f,.5f,.75f,1})
            {
                flower.SetBloomProgress(progress);
                for(int petalIndex=0;petalIndex<petals.Length;petalIndex++)
                {
                    var p=petals[petalIndex];
                    var expected=p.body.localToWorldMatrix; var sampled=flower.DiagnosticMatrix(p.layer,p.index,progress);
                    for(int i=0;i<16;i++) Require(Mathf.Abs(expected[i]-sampled[i])<.00001f,"offline/render matrix equality");
                    if(flower.preset.layers[p.layer].depthOffset==0&&!flower.preset.layers[p.layer].stagedOffsets) Require(Vector3.Distance(anchors[petalIndex],p.anchor.position)<1e-6f,"authored anchor stays fixed during bloom");
                }
                Require(flower.RootAnchorError()<.00001f,"root fixed");
            }
            flower.diagnosticColors=true; flower.ApplyDiagnosticColors(); flower.SetPetalVisible(0,0,false);
            Require(!petals[0].renderer.enabled,"hide petal"); flower.SoloPetal(0,0);
            for(int i=0;i<petals.Length;i++) Require(petals[i].renderer.enabled==(i==0),"solo petal");
            flower.ShowAllPetals(); flower.diagnosticColors=false; flower.ApplyDiagnosticColors();
            foreach(var p in petals) Require(p.renderer.enabled&&p.renderer.sharedMaterial==flower.petalPrefab.pearlMaterial,"restore shared material / visibility");
            var collision=new FlowerSurfaceCollision(flower); collision.Sample(.5f);
            var serial=new List<FlowerSurfaceCollision.Contact>(); var parallel=new List<FlowerSurfaceCollision.Contact>();
            collision.FindContacts(serial); collision.FindContacts(parallel,parallel:true);
            Require(serial.Count==parallel.Count,"serial/parallel contact count");
            for(int i=0;i<serial.Count;i++) Require(serial[i].petalA==parallel[i].petalA&&serial[i].petalB==parallel[i].petalB&&serial[i].rootRegion==parallel[i].rootRegion&&(serial[i].point-parallel[i].point).sqrMagnitude<1e-12f,"deterministic serial/parallel contacts");
            var source=Instantiate(flower.petalPrefab); source.enabled=false; source.GetComponent<MeshRenderer>().enabled=false;
            foreach(float progress in new[]{0,.25f,.5f,.75f,1})
            {
                source.SetShapeImmediate(125,.2f,.7f,progress); var original=source.RuntimeMesh.vertices;
                source.SetShapeImmediateLimited(125,.2f,.7f,progress,1); var limited=source.RuntimeMesh.vertices;
                for(int i=0;i<original.Length;i++) Require(original[i]==limited[i],"default curl cap preserves single-petal geometry");
                source.SetShapeImmediateLimited(125,.2f,.7f,progress,1,1,PetalSurfaceProfile.Legacy); var legacyProfile=source.RuntimeMesh.vertices;
                for(int i=0;i<original.Length;i++) Require(original[i]==legacyProfile[i],"explicit legacy profile preserves geometry");
                source.SetShapeImmediateLimited(125,.2f,.7f,progress,.8f,.1f,new PetalSurfaceProfile { widePleatCount=3,surfaceDetail=.25f,rimVariation=.15f });
                source.SetShapeImmediate(125,.2f,.7f,progress); var restored=source.RuntimeMesh.vertices;
                for(int i=0;i<original.Length;i++) Require(original[i]==restored[i],"single-petal API resets sampling cap");
            }
            ValidateSoftFolds(source);
            Destroy(source.gameObject);
            // An override must scale both longitudinal and depth axes when the layer size changes.
            var layout=Clone(flower.preset.layers[0]); var referenceMatrix=flower.DiagnosticMatrix(0,0,.5f);
            layout.size*=1.02f; var larger=flower.DiagnosticMatrix(0,0,.5f,layout);
            for(int axis=0;axis<3;axis++) Require(Mathf.Abs(larger.GetColumn(axis).magnitude/referenceMatrix.GetColumn(axis).magnitude-1.02f)<1e-5f,"offline size override scales every axis");
            File.WriteAllText(Path.Combine(output,"IntegrationTests_"+flower.preset.randomSeed+".txt"),"PASS: authored height/length, fixed anchors, legacy/sparse offsets, offset constraints; actual layer widths; legacy JSON profile defaults; layer micro normals; cached/render matrices at 5 poses; size overrides on all axes; roots; hide/solo/restore; shared materials; legacy/explicit/reset geometry; serial/parallel exact contacts agree");
        }
        void ValidateSoftFolds(HeroPetal source)
        {
            var profile=new PetalSurfaceProfile { softFolds=true,widePleatCount=3,foldStrength=.45f,edgeStrength=.65f };
            const int cols=HeroPetal.WidthSegments+1,surface=cols*(HeroPetal.LengthSegments+1);
            source.SetShapeImmediateLimited(125,.1f,.82f,1,.9f,1,profile,731); var folded=source.RuntimeMesh.vertices;
            source.SetShapeImmediateLimited(125,.1f,.82f,1,.9f,1,profile,731); var repeated=source.RuntimeMesh.vertices;
            for(int i=0;i<folded.Length;i++) Require(folded[i]==repeated[i],"soft folds reproduce seed");
            profile.foldStrength=0;
            source.SetShapeImmediateLimited(125,.1f,.82f,1,.9f,1,profile,731); var smooth=source.RuntimeMesh.vertices;
            bool medium=false;
            for(int y=0;y<=HeroPetal.LengthSegments;y++) for(int x=0;x<cols;x++)
            {
                int i=y*cols+x;
                float delta=Vector3.Distance((folded[i]+folded[i+surface])*.5f,(smooth[i]+smooth[i+surface])*.5f);
                if(y==0||y==HeroPetal.LengthSegments||x==0||x==cols-1) Require(delta<1e-6f,"folds fade independently of the edge silhouette");
                if(y==HeroPetal.LengthSegments/2) medium|=delta>.005f;
                Require(delta<.04f,"bounded soft fold amplitude");
            }
            Require(medium,"soft folds remain visible in the middle");
            profile.foldStrength=.45f;
            source.SetShapeImmediateLimited(125,.1f,.82f,1,.9f,1,profile,17); var alternate=source.RuntimeMesh.vertices;
            bool different=false; for(int i=0;i<folded.Length;i++) different|=(folded[i]-alternate[i]).sqrMagnitude>1e-8f;
            Require(different,"different fold seeds produce distinct surfaces");
            var invalid=profile; invalid.foldStrength=float.NaN; invalid.edgeStrength=float.PositiveInfinity;
            invalid=invalid.Constrained(); Require(invalid.foldStrength==0&&invalid.edgeStrength==0,"finite soft fold constraints");
            File.WriteAllText(Path.Combine(output,"SoftFoldTests.txt"),"PASS: fixed seed, distinct seed, bounded amplitude, medium folds, root/side/tip fade, nonfinite controls; per-petal diagnostic surfaces match rendered variants at five poses.");
        }
        static float RootStaggerSignForTest(int index,int count)=>count%2==1&&index==count-1?0:index%2==0?-1:1;
        void PredicateTests()
        {
            Vector3 a=new Vector3(-1,0,0),b=new Vector3(1,0,0),c=new Vector3(0,1,0),d=new Vector3(0,.2f,-1),e=new Vector3(0,.2f,1),f=new Vector3(.5f,.7f,0);
            Require(FlowerSurfaceCollision.Triangle(a,b,c,d,e,f,out _)&&FlowerSurfaceCollision.Triangle(d,e,f,a,b,c,out _),"bidirectional crossing");
            Require(!FlowerSurfaceCollision.Triangle(a,b,c,a+Vector3.forward*.01f,b+Vector3.forward*.01f,c+Vector3.forward*.01f,out _),"parallel separation");
            Require(FlowerSurfaceCollision.Triangle(a,b,c,a,b,c,out _),"identical coplanar overlap");
            Require(FlowerSurfaceCollision.Triangle(a,b,c,a*.5f+Vector3.up*.1f,b*.5f+Vector3.up*.1f,c*.5f+Vector3.up*.1f,out _),"contained coplanar overlap");
            Require(!FlowerSurfaceCollision.Triangle(a,b,c,a+Vector3.down,b+Vector3.down,new Vector3(0,-2,0),out _),"edge touching");
            Require(!FlowerSurfaceCollision.Triangle(a,b,c,c,c+Vector3.up+Vector3.forward,c+Vector3.up-Vector3.forward,out _),"vertex touching");
            Require(!FlowerSurfaceCollision.Triangle(a,b,b,d,e,f,out _),"degenerate triangle");
            Require(FlowerSurfaceCollision.Adjacent(0,1,2,2,3,4)&&!FlowerSurfaceCollision.Adjacent(0,1,2,3,4,5),"topological adjacency");
            Require(FlowerSurfaceCollision.IsRoot(.08f,.08f)&&!FlowerSurfaceCollision.IsRoot(.0801f,.01f),"root boundary");
            File.WriteAllText(Path.Combine(output,"PredicateTests.txt"),"PASS: 9 predicate, adjacency and root-boundary tests");
        }
        static void Require(bool ok,string description) { if(!ok) throw new InvalidOperationException("Flower predicate failed: "+description); }
        static bool Has(string arg)=>Array.IndexOf(Environment.GetCommandLineArgs(),arg)>=0;
        static string Arg(string key,string fallback) { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
        void OnDestroy() { if(target!=null) { target.Release(); Destroy(target); } }
    }
}
#endif
