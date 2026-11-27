#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MindState.BCI.BrainLink.Tests
{
    public sealed class BrainLinkSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-verifyBrainLink")>=0)
                Camera.main.gameObject.AddComponent<BrainLinkSmoke>();
        }
        string output;
        TcpListener listener;
        TcpClient peer;
        BrainLinkFlowerInput input;
        void Check(bool condition, string name)
        {
            File.AppendAllText(Path.Combine(output,"Checks.txt"),(condition?"PASS ":"FAIL ")+name+"\n");
            if (!condition) throw new Exception(name);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../"+(Application.isEditor?"":"../../")+"Outputs/BrainLink/"+(Application.isEditor?"Editor":"Windows")));
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output,"Checks.txt"),"");
            File.WriteAllText(Path.Combine(output,"Complete.txt"),"Running");
            var run=Run(); bool passed=true;
            while (true)
            {
                object current=null; bool more=false;
                try { more=run.MoveNext(); if(more) current=run.Current; }
                catch (Exception error) { Debug.LogException(error); passed=false; }
                if (!passed || !more) break;
                yield return current;
            }
            if(input!=null) input.enabled=false;
            peer?.Close(); listener?.Stop();
            File.WriteAllText(Path.Combine(output,"Complete.txt"),passed?"PASS; hardware not tested":"FAIL");
            Debug.Log("[BrainLinkSmoke] "+(passed?"PASS":"FAIL")+" "+output);
#if UNITY_EDITOR
            if(Application.isEditor) { UnityEditor.EditorApplication.Exit(passed?0:1); yield break; }
#endif
            Application.Quit(passed?0:1);
        }
        bool Accept()
        {
            if(!listener.Pending()) return false;
            peer=listener.AcceptTcpClient(); return true;
        }
        void Send(int a,int m,int s=0)
        {
            var bytes=Encoding.ASCII.GetBytes($"{{\"attention\":{a},\"meditation\":{m},\"signal\":{s},\"mode\":\"simulate\"}}\n");
            peer.GetStream().Write(bytes,0,bytes.Length);
        }
        IEnumerator Run()
        {
            yield return null;
            var gallery=GetComponent<FlowerGallery>(); input=GetComponent<BrainLinkFlowerInput>();
            var flower=gallery.flower;
            Check(flower.Ready && input.enabled,"Scene starts ready with BrainLink always on");
            Check(!input.ManualWindowActive,"No manual window at start");
            Check(!input.ShowDiagnostics,"Diagnostics hidden by default");
            Check(!gallery.BrainLinkOwnsInput,"Without data the flower stays manual");
            input.enabled=false;
            listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            input.settings.port=((IPEndPoint)listener.LocalEndpoint).Port;
            input.enabled=true;
            double deadline=BrainLinkClient.Now+8;
            while(!Accept() && BrainLinkClient.Now<deadline) yield return null;
            Check(peer!=null,"Always-on client connects");
            yield return null;
            Check(!input.ReadSnapshot().HasSample,"Waiting does not invent values");
            Check(!gallery.BrainLinkOwnsInput,"Connected without data still manual");
            Send(100,0); yield return new WaitForSecondsRealtime(1.2f);
            Check(input.ReadSnapshot().Attention==100 && input.ReadSnapshot().Meditation==0,"Live values received");
            Check(gallery.BrainLinkOwnsInput && flower.ArtInput==FlowerArtInput.External,"Usable data drives the flower");
            Check(flower.ArtWeights.y>.05f,"Meditation drives Gathering");
            float oldZ=flower.ArtWeights.z;
            Send(0,100); yield return new WaitForSecondsRealtime(1.2f);
            Check(flower.ArtWeights.z>oldZ+.05f,"Independent attention/meditation drive Wandering");
            input.ToggleDiagnostics(); gallery.showInterface=false;
            Check(input.ShowDiagnostics,"F9 toggle path independent of H panel");
            var beforeManual=flower.ArtWeights;
            gallery.InjectArtCorner(3);
            Check(input.ManualWindowActive && !gallery.BrainLinkOwnsInput,"Manual intervention takes priority");
            Send(100,0); yield return new WaitForSecondsRealtime(1.2f);
            Check(input.ReadSnapshot().Attention==100,"Brain data still received during manual window");
            Check(flower.ArtWeights.w>beforeManual.w+.02f,"Manual pose moves to the injected corner despite brain data");
            float manualUntil=Time.unscaledTime+5.2f;
            while(Time.unscaledTime<manualUntil) { Send(100,0); yield return new WaitForSecondsRealtime(.5f); }
            var atHandback=flower.ArtWeights;
            yield return new WaitForSecondsRealtime(1.5f);
            Check(!input.ManualWindowActive && gallery.BrainLinkOwnsInput,"Idle five seconds with usable data returns to brain");
            Check(flower.ArtWeights.y>atHandback.y+.02f,"Brain resumes driving after manual window");
            var held=flower.ArtWeights;
            Send(70,50,200); yield return new WaitForSecondsRealtime(.3f);
            Check(!input.ReadSnapshot().Usable,"Poor contact rejects control");
            Check(!gallery.BrainLinkOwnsInput && flower.ArtWeights.y>=held.y-.01f,"Poor contact falls back to manual without pose jump");
            Check(flower.ArtStatus!="正在重新感知","No waiting state while manual fallback holds");
            Send(70,50); yield return new WaitForSecondsRealtime(.3f);
            Check(input.ReadSnapshot().Usable && gallery.BrainLinkOwnsInput && flower.ArtStatus=="外部脑电","Signal recovery resumes");
            yield return new WaitForSecondsRealtime(5.2f);
            Check(!input.ReadSnapshot().Fresh && !gallery.BrainLinkOwnsInput,"Stale data falls back to manual");
            Check(flower.ArtStatus!="正在重新感知","Stale data holds pose instead of waiting");
            peer.Close(); peer=null;
            deadline=BrainLinkClient.Now+8;
            while(!Accept() && BrainLinkClient.Now<deadline) yield return null;
            Check(peer!=null,"Reconnect after socket close");
            Send(50,80); yield return new WaitForSecondsRealtime(.3f);
            Check(input.ReadSnapshot().Usable && gallery.BrainLinkOwnsInput && flower.ArtStatus=="外部脑电","New callback after reconnect resumes");
            var client=input.Client; input.enabled=false;
            Check(!client.IsWorkerAlive && !gallery.BrainLinkOwnsInput,"Disable releases worker and ownership");
            Check(flower.ArtInput==FlowerArtInput.External,"Disable returns to slider-controlled external input");
            input.enabled=true; yield return new WaitForSecondsRealtime(.2f);
            client=input.Client; input.enabled=false;
            Check(!client.IsWorkerAlive,"Second enable/disable leaves no worker");
        }
    }
}
#endif
