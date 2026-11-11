using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MindState.BCI.BrainLink.Tests
{
    public static class BrainLinkAcceptance
    {
        static int checks;
        static void Check(bool ok, string message)
        { checks++; if (!ok) throw new Exception("BrainLink: " + message); }
        static string Packet(int a=70, int m=50, int s=0)
            => $"{{\"attention\":{a},\"meditation\":{m},\"signal\":{s},\"mode\":\"simulate\"}}\n";
        static void Feed(BrainLinkStream stream, string json, double now)
        { var bytes=Encoding.ASCII.GetBytes(json); stream.Feed(bytes, bytes.Length, now); }
        static void Until(Func<bool> condition, string name)
        {
            double deadline=BrainLinkClient.Now+8;
            while (!condition() && BrainLinkClient.Now<deadline) Thread.Sleep(10);
            Check(condition(), name);
        }

        [MenuItem("MindState/BrainLink/Run Acceptance")]
        public static void Run()
        {
            try
            {
                VerifyStream(); VerifySocket();
                EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
                var input=Camera.main.GetComponent<BrainLinkFlowerInput>();
                Check(input!=null && !input.enabled && input.flower!=null, "Scene reference and disabled default");
                Check(!input.ShowDiagnostics, "Diagnostics hidden initially");
                input.ToggleDiagnostics(); Check(input.ShowDiagnostics, "Diagnostics toggle while disabled");
                input.ToggleDiagnostics(); Check(!input.ShowDiagnostics, "Diagnostics toggle back");
                Debug.Log("[BrainLinkAcceptance] PASS "+checks+" checks; hardware not tested.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        static void VerifyStream()
        {
            var stream=new BrainLinkStream();
            Check(!stream.Snapshot(true,"",0).HasSample,"No implicit zero initial sample");
            string packet=Packet();
            for (int split=1; split<packet.Length; split++)
            {
                stream.Reset(); Feed(stream,packet.Substring(0,split),1);
                Check(!stream.Snapshot(true,"",1).Usable,"Incomplete fragment");
                Feed(stream,packet.Substring(split),1);
                var sample=stream.Snapshot(true,"",1);
                Check(sample.Usable && sample.Attention==70 && sample.Meditation==50,"Split "+split);
            }
            Feed(stream,Packet(100,0)+Packet(0,100),2);
            Check(stream.Attention==0 && stream.Meditation==100,"Coalesced callbacks");
            Check(stream.Snapshot(true,"",6.999).Fresh && !stream.Snapshot(true,"",7).Fresh,"Five second expiry");
            foreach (string bad in new[]{"{}\n","[]\n","{broken}\n","not json\n",
                "{\"attention\":70,\"signal\":0,\"mode\":\"simulate\"}\n",
                "{\"meditation\":50,\"signal\":0,\"mode\":\"simulate\"}\n",
                "{\"attention\":70,\"meditation\":50,\"mode\":\"simulate\"}\n",
                packet.Replace("70","70.5"),packet.Replace("simulate","unknown"),Packet(-1),Packet(101),Packet(70,-1),Packet(70,101),Packet(70,50,201)})
            {
                Feed(stream,packet,10); long revision=stream.Revision; Feed(stream,bad,11);
                Check(!stream.Snapshot(true,"",11).Usable && revision==stream.Revision,"Reject incomplete/invalid packet");
            }
            Feed(stream,Packet(70,50,200),12);
            Check(!stream.Snapshot(true,"",12).Usable,"Poor contact");
            Feed(stream,packet,13); Check(stream.Snapshot(true,"",13).Usable,"Signal recovery");
            Feed(stream,new string('x',16385)+"\n"+packet,14);
            Check(stream.Snapshot(true,"",14).Usable,"Recover after oversized line");
            long before=stream.Revision; stream.Reset(); Feed(stream,packet,15);
            Check(stream.Revision>before,"Revision survives reconnect reset");
            Check(!stream.Snapshot(false,"Disconnected",15).Usable,"Disconnected cannot drive");
            Check(FlowerAssembler.BrainArtWeights(1,1)==new Vector4(1,0,0,0),"Clarity corner");
            Check(FlowerAssembler.BrainArtWeights(1,0)==new Vector4(0,1,0,0),"Meditation independent");
            Check(FlowerAssembler.BrainArtWeights(0,1)==new Vector4(0,0,1,0),"Attention independent");
        }

        static void VerifySocket()
        {
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            var settings=new BrainLinkSettings { port=((IPEndPoint)listener.LocalEndpoint).Port };
            var client=new BrainLinkClient(settings);
            try
            {
                client.Start(); Until(()=>listener.Pending(),"Connect");
                using (var peer=listener.AcceptTcpClient())
                {
                    var bytes=Encoding.ASCII.GetBytes(Packet(85,25)); peer.GetStream().Write(bytes,0,bytes.Length);
                    Until(()=>client.Snapshot().Usable,"Receive");
                    Check(client.Snapshot().Meditation==25,"Meditation through TCP");
                    Thread.Sleep(5100); Check(!client.Snapshot().Usable,"Paused sender expires");
                }
                Until(()=>!client.Snapshot().Connected,"Detect disconnect");
                Until(()=>listener.Pending(),"Automatic reconnect");
                using (var peer=listener.AcceptTcpClient())
                {
                    Check(!client.Snapshot().Usable,"No replay on reconnect");
                    var bytes=Encoding.ASCII.GetBytes(Packet()); peer.GetStream().Write(bytes,0,bytes.Length);
                    Until(()=>client.Snapshot().Usable,"Fresh callback restores");
                    client.Dispose(); Check(!client.IsWorkerAlive,"Dispose while network read blocked");
                }
            }
            finally { client.Dispose(); listener.Stop(); }
            var retry=new BrainLinkClient(settings); retry.Start(); retry.Dispose();
            Check(!retry.IsWorkerAlive,"Dispose during connect/retry");
        }

        public static void PlaySmoke()
        {
            EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
            Camera.main.gameObject.AddComponent<BrainLinkSmoke>();
            EditorApplication.isPlaying=true;
        }

    }
}
