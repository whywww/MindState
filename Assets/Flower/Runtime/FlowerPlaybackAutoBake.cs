#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEditor;

namespace MindState
{
    // Fills in the GPU pose cache the first time Play runs after the playback sources change,
    // so a fresh clone (or a shader edit) does not leave the scene with no flower. The editor
    // hook marks the intent in SessionState; this side takes the CPU reference path, bakes,
    // then rebuilds the same session on the GPU path.
    public sealed class FlowerPlaybackAutoBake : MonoBehaviour
    {
        public const string PendingKey="MindState.FlowerPlaybackBakePending";
        static bool pending,baking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadRequest()
        {
            pending=SessionState.GetBool(PendingKey,false);
            SessionState.SetBool(PendingKey,false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!pending||Application.isBatchMode) return;
            var flowers=FindObjectsOfType<FlowerAssembler>(true);
            foreach(var flower in flowers) flower.forceCpuReference=true;
            var host=Camera.main!=null?Camera.main.gameObject:(flowers.Length>0?flowers[0].gameObject:null);
            if(host!=null) host.AddComponent<FlowerPlaybackAutoBake>();
        }

        IEnumerator Start()
        {
            if(baking) yield break;
            baking=true;
            yield return null;
            var flowers=FindObjectsOfType<FlowerAssembler>(true);
            try
            {
                if(flowers.Length==0) throw new InvalidOperationException("No flower assembler in the scene.");
                var flower=flowers[0];
                if(!flower.Ready) throw new InvalidOperationException("Flower assembler is not ready for the playback bake.");
                FlowerBakeRunner.ValidateReference(flower);
                if(flower.UsesGpuPlayback) throw new InvalidOperationException("Playback bake needs the CPU reference geometry.");
                flower.SavePlaybackCache();
                if(!FlowerCacheProvenance.PlaybackMatches()) throw new InvalidOperationException("Playback cache was written but its provenance does not match.");
                foreach(var target in flowers) { target.SwitchToGpuPlayback(); target.SetArtState(FlowerArtState.Clarity); }
                Debug.Log("AUTO_PLAYBACK_BAKE_SUCCESS");
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("播放缓存生成失败",e.Message+"\n\n查看到 Console，或手动运行 Tools/Bake.ps1。","确定");
            }
            finally
            {
                baking=false;
                EditorUtility.ClearProgressBar();
                Destroy(this);
            }
        }
    }

    public sealed partial class FlowerAssembler
    {
        // Rebuilds this running session on the baked GPU path after the auto-bake wrote the cache.
        internal void SwitchToGpuPlayback()
        {
            forceCpuReference=false;
            enabled=true;
            RebuildFlower();
        }
    }
}
#endif
