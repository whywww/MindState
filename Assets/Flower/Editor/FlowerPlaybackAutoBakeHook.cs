#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MindState.Editor
{
    // Before Play begins, decide whether the playback cache has to be baked first. A stale
    // bottom-overlap cache cannot be repaired here (it rewrites a committed artifact), so the
    // Play is cancelled and the user is pointed at Tools/Bake.ps1 instead.
    [InitializeOnLoad]
    public static class FlowerPlaybackAutoBakeHook
    {
        static FlowerPlaybackAutoBakeHook()
        {
            EditorApplication.playModeStateChanged-=OnPlayModeChanged;
            EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.ExitingEditMode||Application.isBatchMode) return;
            if(FlowerCacheProvenance.PlaybackMatches()) return;
            if(!FlowerCacheProvenance.Matches())
            {
                Debug.LogError("Geometry cache is stale; press Play was cancelled. Run Tools/Bake.ps1 before playing.");
                // Setting isPlaying here is the documented way to cancel, but it is ignored on
                // some versions, so repeat it once Unity has moved on before showing the reason.
                EditorApplication.isPlaying=false;
                EditorApplication.delayCall+=()=>
                {
                    if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;
                    EditorUtility.DisplayDialog("几何缓存已过期","FlowerAssembler 的几何来源已变化，请先运行 Tools/Bake.ps1 重新生成底部补偿缓存。","确定");
                };
                return;
            }
            SessionState.SetBool(FlowerPlaybackAutoBake.PendingKey,true);
            Debug.Log("AUTO_PLAYBACK_BAKE_PENDING play will bake the GPU playback cache before it starts.");
        }
    }
}
#endif
