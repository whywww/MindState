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
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../"+(Application.isEditor?"":"../../")+"Evidence/BrainLink/"+(Application.isEditor?"Editor":"Windows")));
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
            Check(flower.Ready && !input.enabled,"Scene starts ready with BrainLink disabled");
            Check(!input.ShowDiagnostics,"Diagnostics hidden by default");
            listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            input.settings.port=((IPEndPoint)listener.LocalEndpoint).Port;
            input.enabled=true;
            double deadline=BrainLinkClient.Now+8;
            while(!Accept() && BrainLinkClient.Now<deadline) yield return null;
            Check(peer!=null,"Inspector component enable connects");
            yield return null;
            Check(gallery.BrainLinkOwnsInput && flower.ArtInput==FlowerArtInput.External,"Exclusive external ownership");
            Check(!input.ReadSnapshot().HasSample,"Waiting does not invent values");
            Send(100,0); yield return new WaitForSecondsRealtime(1.2f);
            Check(input.ReadSnapshot().Attention==100 && input.ReadSnapshot().Meditation==0,"Live values received");
            Check(flower.ArtWeights.y>.05f,"Meditation drives Gathering");
            float oldZ=flower.ArtWeights.z;
            Send(0,100); yield return new WaitForSecondsRealtime(1.2f);
            Check(flower.ArtWeights.z>oldZ+.05f,"Independent attention/meditation drive Wandering");
            input.ToggleDiagnostics(); gallery.showInterface=false;
            Check(input.ShowDiagnostics,"F9 toggle path independent of H panel");
            Send(70,50,200); yield return new WaitForSecondsRealtime(.2f);
            Check(!input.ReadSnapshot().Usable,"Poor contact rejects control");
            yield return new WaitForSecondsRealtime(5.1f);
            Check(flower.ArtStatus=="正在重新感知","Bad data cannot refresh five second flower timeout");
            Send(70,50); yield return new WaitForSecondsRealtime(.2f);
            Check(flower.ArtStatus=="外部脑电","Signal recovery resumes");
            yield return new WaitForSecondsRealtime(5.2f);
            Check(!input.ReadSnapshot().Fresh && flower.ArtStatus=="正在重新感知","Polling does not replay cached callback");
            peer.Close(); peer=null;
            deadline=BrainLinkClient.Now+8;
            while(!Accept() && BrainLinkClient.Now<deadline) yield return null;
            Check(peer!=null,"Reconnect after socket close");
            Send(50,80); yield return new WaitForSecondsRealtime(.3f);
            Check(input.ReadSnapshot().Usable && flower.ArtStatus=="外部脑电","New callback after reconnect resumes");
            var client=input.Client; input.enabled=false;
            Check(!client.IsWorkerAlive && !gallery.BrainLinkOwnsInput,"Disable releases worker and ownership");
            Check(flower.ArtInput==FlowerArtInput.Preview,"Disable restores Clarity preview");
            input.enabled=true; yield return new WaitForSecondsRealtime(.2f);
            client=input.Client; input.enabled=false;
            Check(!client.IsWorkerAlive,"Second enable/disable leaves no worker");
        }
    }
}
#endif
