using UnityEngine;
namespace MindState {
 [CreateAssetMenu(menuName="MindState/Flower Appearance")]
 public sealed class FlowerAppearancePreset : ScriptableObject {
  public Color rose=new Color(.91f,.51f,.62f), peach=new Color(1,.57f,.43f), lavender=new Color(.65f,.47f,.79f), pearl=new Color(1,.91f,.87f), cyan=new Color(.49f,.94f,.96f);
  public Vector3 layerPink=new Vector3(.35f,.65f,.85f);
  [Range(0,1)] public float variation=.22f,cyanStrength=.65f,polish=.38f,transmission=.12f;
  public int seed=271;
  [Range(0,5)] public float coloredLightIntensity=2.8f;
  [Range(25,80)] public float coloredLightAngle=48;
  [Range(.003f,.05f)] public float lightDriftSpeed=.012f;
  // Stored as rose / peach / lavender / cyan; pearl is the remaining fraction.
  public Vector4[] stateColorWeights={new Vector4(.1f,.1f,.3f,.3f),new Vector4(.2f,.2f,.2f,.2f),new Vector4(.15f,.15f,.25f,.25f),new Vector4(.1f,.15f,.3f,.25f)};
  public Vector4 flowPeriods=new Vector4(12,10,9,18);
  [Range(.1f,1)] public float bandWidth=.65f;
  [Range(0,1)] public float phaseSpread=1;
  [Range(0,.1f)] public float periodVariation=.1f;
 }
 public sealed partial class FlowerAssembler {
  public FlowerAppearancePreset appearance;
  public bool appearanceEnabled=true;
  Texture2D flowPalette;
  public float ArtFlowPhase { get; private set; }
  void ResetFlowPhase(){ ArtFlowPhase=0; if(layers!=null) foreach(var l in layers) foreach(var p in l.petals) p.flowPhase=0; }
  void ReleaseFlowPalette(){if(naturalLightRoot!=null) Destroy(naturalLightRoot.gameObject);if(flowPalette!=null) Destroy(flowPalette);flowPalette=null;}
  [ContextMenu("Refresh appearance without rebuilding geometry")]
  public void RefreshAppearance() {
   if(layers==null) return;
   if(appearance!=null) UpdateFlowPalette();
   EnsureNaturalLights();
   for(int k=0;k<layers.Length;k++) for(int j=0;j<layers[k].petals.Length;j++) {
    var p=layers[k].petals[j]; var b=p.block;
    bool enabled=appearanceEnabled&&appearance!=null;
    b.SetFloat("_TargetAppearance",enabled?1:0);
    if(enabled) {
     // Vector uniforms explicitly contain linear RGB; never implicitly convert twice.
     b.SetVector("_RoseLinear",appearance.rose.linear); b.SetVector("_PeachLinear",appearance.peach.linear);
     b.SetVector("_LavenderLinear",appearance.lavender.linear); b.SetVector("_PearlLinear",appearance.pearl.linear); b.SetVector("_CyanLinear",appearance.cyan.linear);
     float v=Mathf.Repeat((j+1)*.618034f+k*.37f+appearance.seed*.013f,1);
     float pink=appearance.layerPink[Mathf.Min(k,2)]+(v-.5f)*appearance.variation;
     b.SetVector("_AppearancePlacement",new Vector4(pink,v,k,1));
     b.SetTexture("_FlowPalette",flowPalette);
     b.SetVector("_AppearanceSurface",new Vector4(appearance.polish,appearance.transmission,appearance.cyanStrength,0));
     b.SetVector("_StateTint",new Vector4(1,1,0,0));b.SetVector("_StateAccent",(Vector4)Color.white.linear);b.SetVector("_StateSurface",Vector4.zero);
    }
    p.renderer.SetPropertyBlock(b);
   }
   foreach(var r in generated.GetComponentsInChildren<MeshRenderer>()) {
    if(r.sharedMaterial==coreMaterial||r.sharedMaterial==stamenMaterial) {
     var b=new MaterialPropertyBlock(); r.GetPropertyBlock(b);
     b.SetColor("_Color",appearanceEnabled&&appearance!=null?(r.sharedMaterial==coreMaterial?new Color(.97f,.58f,.50f):new Color(1f,.86f,.52f)):r.sharedMaterial.color); r.SetPropertyBlock(b);
    }
   }
  }
  static Vector3 Lab(Color c) {
   float l=Mathf.Pow(.4122214708f*c.r+.5363325363f*c.g+.0514459929f*c.b,1f/3),m=Mathf.Pow(.2119034982f*c.r+.6806995451f*c.g+.1073969566f*c.b,1f/3),s=Mathf.Pow(.0883024619f*c.r+.2817188376f*c.g+.6299787005f*c.b,1f/3);
   return new Vector3(.2104542553f*l+.793617785f*m-.0040720468f*s,1.9779984951f*l-2.428592205f*m+.4505937099f*s,.0259040371f*l+.7827717662f*m-.808675766f*s);
  }
  static Color FromLab(Vector3 v) {
   float l=v.x+.3963377774f*v.y+.2158037573f*v.z,m=v.x-.1055613458f*v.y-.0638541728f*v.z,s=v.x-.0894841775f*v.y-1.291485548f*v.z;l=l*l*l;m=m*m*m;s=s*s*s;
   return new Color(Mathf.Clamp01(4.0767416621f*l-3.3077115913f*m+.2309699292f*s),Mathf.Clamp01(-1.2684380046f*l+2.6097574011f*m-.3413193965f*s),Mathf.Clamp01(-.0041960863f*l-.7034186147f*m+1.707614701f*s),1);
  }
  void UpdateFlowPalette() {
   if(flowPalette==null) flowPalette=new Texture2D(256,4,TextureFormat.RGBAHalf,false,true){name="Shared OKLab flow palette",wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   var colors=new[]{Lab(Color.Lerp(appearance.cyan.linear,appearance.pearl.linear,.22f)),Lab(Color.Lerp(appearance.lavender.linear,appearance.pearl.linear,.22f)),Lab(Color.Lerp(appearance.rose.linear,appearance.pearl.linear,.22f)),Lab(Color.Lerp(Color.Lerp(appearance.peach.linear,appearance.rose.linear,.28f),appearance.pearl.linear,.22f)),Lab(appearance.pearl.linear)};
   var pixels=new Color[1024];
   for(int row=0;row<4;row++) {
    var w=appearance.stateColorWeights[row];var weights=new[]{Mathf.Max(.001f,w.w),Mathf.Max(.001f,w.z),Mathf.Max(.001f,w.x),Mathf.Max(.001f,w.y),Mathf.Max(.001f,1-w.x-w.y-w.z-w.w)};float sum=0;foreach(float x in weights)sum+=x;
    for(int x=0;x<256;x++) {float t=(x+.5f)/256f*sum;int i=0;while(i<4&&t>=weights[i])t-=weights[i++];float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,1,t/weights[i]));pixels[row*256+x]=FromLab(Vector3.Lerp(colors[i],colors[(i+1)%5],blend));}
   }
   flowPalette.SetPixels(pixels);flowPalette.Apply(false,false);
  }

 }
}
