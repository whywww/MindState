using UnityEngine;
namespace MindState {
 public sealed partial class FlowerAssembler {
  Transform naturalLightRoot;
  Light[] naturalLights;
  void EnsureNaturalLights() {
   if(naturalLightRoot!=null) {UpdateNaturalLights();return;}
   naturalLightRoot=new GameObject("Soft overlapping colored light").transform;
   naturalLightRoot.SetParent(transform,false);
   naturalLights=new Light[3];
   for(int i=0;i<3;i++) {
    var go=new GameObject("Soft color "+i);go.transform.SetParent(naturalLightRoot,false);
    var l=go.AddComponent<Light>();l.type=LightType.Spot;l.range=12;
    l.shadows=LightShadows.None;l.renderMode=LightRenderMode.ForcePixel;
    naturalLights[i]=l;
   }
   UpdateNaturalLights();
  }
  static float Wander(float seed,float time)=>Mathf.PerlinNoise(seed,time)*2-1;
  void UpdateNaturalLights() {
   if(naturalLights==null) return;
   bool enabled=appearanceEnabled&&appearance!=null;
   naturalLightRoot.gameObject.SetActive(enabled);
   if(!enabled)return;
   // A shared, unwrapped clock preserves continuous trajectories through state changes.
   float t=artTime*appearance.lightDriftSpeed*ArtLightDrift;
   Vector3 center=transform.TransformPoint(new Vector3(0,.9f,0));
   for(int i=0;i<naturalLights.Length;i++) {
    float seed=appearance.seed*.071f+i*19.37f;
    float angle=((i==0?230:i==1?125:20)+Wander(seed+1,t*.37f)*35)*Mathf.Deg2Rad;
    Vector3 position=center+new Vector3(Mathf.Sin(angle)*3.8f,2+Wander(seed+2,t*.43f)*.5f,Mathf.Cos(angle)*3.8f);
    Vector3 aim=center+new Vector3(Wander(seed+3,t)*1.45f,Wander(seed+7,t*.71f)*.7f,Wander(seed+11,t*.87f)*1.45f);
    var l=naturalLights[i];l.transform.SetPositionAndRotation(position,Quaternion.LookRotation(aim-position));
    Color cool=i==0?appearance.cyan:appearance.lavender;
    Color warm=Color.Lerp(appearance.peach,appearance.rose,.6f);
    Color c=i==2?warm:cool;
    // Slow, bounded changes within neighboring reference hues, never a rainbow loop.
    float colorMix=.12f+.12f*(.5f+.5f*Wander(seed+17,t*.29f));
    c=Color.Lerp(c,i==2?appearance.lavender:appearance.pearl,colorMix);
    c=Color.Lerp(c,appearance.pearl,.10f);
    c=Color.Lerp(c,ArtAccentColor,.28f);
    l.color=c;
    l.intensity=appearance.coloredLightIntensity*ArtLightGain*(.9f+.1f*Wander(seed+23,t*.53f));
    l.spotAngle=appearance.coloredLightAngle+Wander(seed+29,t*.31f)*7;
   }
  }
 }
}
