#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace MindState {
 // Shared by cache generation and both build configurations.
 public static class FlowerCacheProvenance {
  static readonly string[] Sources={
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
   return File.Exists(path)&&File.ReadAllText(path).Trim()==Signature();
  }
 }
}
#endif
