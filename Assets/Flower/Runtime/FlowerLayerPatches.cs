using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 Vector3 UpperDeltaAt(int layer,int index,float phase,int id){foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){int k=Array.BinarySearch(patch.upperIds,id);if(k<0||patch.upper==null)return Vector3.zero;float q=phase*32;int a=Mathf.Min((int)q,31);return Vector3.Lerp(patch.upper[a][k],patch.upper[a+1][k],q-a);}return Vector3.zero;}
 internal void LayerPatchPreview(bool enabled){extraPreview=enabled;bottomProgress=-1;ApplyBottomPatches();}
 Vector3 ExtraDeltaAt(int layer,int index,float phase,int id){foreach(var p in bottomPatches)if(p.layer==layer&&p.index==index){int k=Array.BinarySearch(p.extraIds,id);if(k<0||p.extra==null)return Vector3.zero;float q=phase*32;int a=Mathf.Min((int)q,31);return Vector3.Lerp(p.extra[a][k],p.extra[a+1][k],q-a);}return Vector3.zero;}

}}
