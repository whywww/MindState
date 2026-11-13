#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace MindState {
 // Shared by cache generation and both build configurations.
 public static class FlowerCacheProvenance {
  static readonly string[] Sources={
   "Flower/Runtime/FlowerPlayback.cs", "Flower/Baking/FlowerPlaybackBaking.cs",
   "Flower/Resources/FlowerPlayback.cginc", "HeroPetal/Shaders/Pearl.shader",
   "Flower/Runtime/FlowerBottomPatches.cs", "Flower/Runtime/FlowerLayerPatches.cs",
   "Flower/Baking/FlowerGeometryBaking.cs", "Flower/Baking/FlowerLayerBaking.cs",
   "Flower/Baking/FlowerCacheProvenance.cs", "HeroPetal/Runtime/HeroPetal.cs",
   "Flower/Validation/FlowerDiagnostics.cs", "Flower/Validation/FlowerSurfaceCollision.cs",
   "Flower/Runtime/FlowerAssembler.cs", "Flower/Runtime/FlowerPreset.cs",
   "Flower/Art/ExhibitionPeony.asset", "HeroPetal/Prefabs/HeroPetal.prefab"
  };
  public static string Signature()=>string.Join("\n",Array.ConvertAll(Sources,path=>{
   using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant();
  }));
  public static bool Matches() {
   string path=Path.Combine(Application.dataPath,"Flower/Resources/BottomOverlapProvenance.txt");
   if(!File.Exists(path))return false;
   var stored=File.ReadAllText(path).Trim().Split('\n');var current=Signature().Split('\n');
   if(stored.Length!=Sources.Length)return false;
   // Playback/shader changes require a new playback bake, but do not change the
   // underlying overlap solver. Keep its recorded hashes, without re-signing it.
   for(int i=0;i<Sources.Length;i++){
    string source=Sources[i];
    if(source=="Flower/Runtime/FlowerPlayback.cs"||source=="Flower/Baking/FlowerPlaybackBaking.cs"||source=="Flower/Resources/FlowerPlayback.cginc"||source=="HeroPetal/Shaders/Pearl.shader"||source=="Flower/Baking/FlowerCacheProvenance.cs")continue;
    if(stored[i].Trim()!=current[i])return false;
   }
   return true;
  }
  public static bool PlaybackMatches() {
   string path=Path.Combine(Application.dataPath,"Flower/Resources/FlowerPlaybackProvenance.txt");
   return File.Exists(Path.Combine(Application.streamingAssetsPath,"FlowerPlayback.bin"))&&File.Exists(path)&&File.ReadAllText(path).Trim()==Signature();
  }
 }
}
#endif
