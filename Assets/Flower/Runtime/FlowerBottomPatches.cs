using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
namespace MindState {
public sealed partial class FlowerAssembler {
 struct BottomSeed {
  public int a,i,b,j; public float p,t,s,u,v;
  public BottomSeed(int a,int i,int b,int j,float p,float t,float s,float u,float v){this.a=a;this.i=i;this.b=b;this.j=j;this.p=p;this.t=t;this.s=s;this.u=u;this.v=v;}
 }
 static readonly BottomSeed[] bottomSeeds={new BottomSeed(0,1,0,0,0.0f,0.53787765f,-0.59039945f,0.43711125f,0.40902785f),
new BottomSeed(0,0,0,6,0.0f,0.54714655f,-0.58879215f,0.39223014999999994f,0.41103045f),
new BottomSeed(0,0,1,0,0.0f,0.127709f,0.053763649999999996f,0.026434499999999993f,0.35963865f),
new BottomSeed(0,0,1,5,0.0f,0.1976591f,0.003567134999999999f,0.053044200000000014f,0.052635585f),
new BottomSeed(0,2,0,1,0.0f,0.56050485f,-0.5867013f,0.37610565f,0.40927610000000003f),
new BottomSeed(0,1,1,0,0.0f,0.18794845f,0.05325350000000001f,0.08769765f,0.22846260000000002f),
new BottomSeed(0,1,1,1,0.0f,0.14069335f,0.308553445f,0.04021255000000001f,0.38701425500000003f),
new BottomSeed(0,3,0,2,0.0f,0.53681375f,-0.58563905f,0.43664855f,0.41420225f),
new BottomSeed(0,2,1,1,0.0f,0.208957f,0.4181703f,0.066673f,0.11508059999999998f),
new BottomSeed(0,2,1,2,0.0f,0.14586215f,0.48873135f,0.04509484999999999f,0.34231645f),
new BottomSeed(0,4,0,3,0.0f,0.5647414000000001f,-0.59633545f,0.37961150000000005f,0.40166274999999996f),
new BottomSeed(0,3,1,2,0.0f,0.19944455f,0.38924806f,0.09927754999999999f,0.28962694f),
new BottomSeed(0,3,1,3,0.0f,0.1539691f,0.6871052f,0.053939900000000006f,0.31214280000000005f),
new BottomSeed(0,5,0,4,0.0f,0.53024995f,-0.58768955f,0.42912315f,0.41047835f),
new BottomSeed(0,4,1,4,0.0f,0.1733675f,0.7836333f,0.0402985f,0.21238829999999997f),
new BottomSeed(0,6,0,5,0.0f,0.5529523f,-0.5926296f,0.3921978f,0.4066861f),
new BottomSeed(0,5,1,3,0.0f,0.20840275f,-0.4233868f,0.03676165000000001f,0.022049099999999988f),
new BottomSeed(0,5,1,4,0.0f,0.2063613f,0.11604144999999999f,0.08666169999999998f,0.88310265f),
new BottomSeed(0,5,1,5,0.0f,0.19532275f,0.8849242500000001f,0.022378250000000002f,0.11307885000000001f),
new BottomSeed(0,6,1,0,0.0f,0.21675214999999998f,0.9728125000000001f,0.004542850000000001f,0.02708689999999997f),
new BottomSeed(0,6,1,4,0.0f,0.17849135f,-0.2603257f,0.07581035000000001f,0.0886402f),
new BottomSeed(1,1,1,0,0.0f,0.5685752f,-0.8136471000000001f,0.4312626f,0.18625160000000002f),
new BottomSeed(1,0,1,5,0.0f,0.6069481f,-0.62053425f,0.39276390000000005f,0.37848015f),
new BottomSeed(1,2,1,1,0.0f,0.60429725f,-0.46362585f,0.39562555f,0.53523935f),
new BottomSeed(1,3,1,2,0.0f,0.56626055f,-0.8156664499999999f,0.43319015f,0.18192395f),
new BottomSeed(1,4,1,3,0.0f,0.6059687f,-0.6031964f,0.39365669999999997f,0.3942565f),
new BottomSeed(1,5,1,4,0.0f,0.567734f,-0.81384485f,0.4318657f,0.18527164999999995f),
new BottomSeed(1,5,2,0,0.0f,0.13523475000000001f,0.74130615f,0.028950450000000003f,0.25722245f),
new BottomSeed(2,1,2,0,0.0f,0.8623514f,-0.020669749999999987f,0.13754869999999997f,0.9792600499999999f),
new BottomSeed(2,2,2,0,0.0f,0.9322836f,0.5667253f,0.06763949999999996f,0.09006670000000003f),
new BottomSeed(2,0,2,3,0.0f,0.9345565f,0.47218485f,0.06464690000000001f,0.07959214999999997f),
new BottomSeed(2,0,2,4,0.0f,0.81448835f,-0.059496899999999964f,0.18544135f,0.9127229f),
new BottomSeed(2,2,2,1,0.0f,0.8310364f,-0.0758741f,0.16888910000000001f,0.8491061f),
new BottomSeed(2,3,2,1,0.0f,0.9352741499999999f,0.551291f,0.06430154999999999f,0.0814964f),
new BottomSeed(2,1,2,4,0.0f,0.9365942f,0.6147923f,0.06294850000000002f,0.07043899999999997f),
new BottomSeed(2,3,2,2,0.0f,0.8496822500000001f,-0.0027032000000000167f,0.15028124999999998f,0.997111f),
new BottomSeed(2,4,2,2,0.0f,0.9332022f,0.6249825f,0.0666928f,0.07865279999999997f),
new BottomSeed(2,4,2,3,0.0f,0.8109478f,-0.05648705000000004f,0.18876499999999996f,0.90431095f),
new BottomSeed(0,1,0,0,0.1f,0.5497255f,-0.5674179f,0.4496873f,0.4314266f),
new BottomSeed(0,0,0,6,0.1f,0.5607825f,-0.5709668999999999f,0.3971946f,0.4256954f),
new BottomSeed(0,0,1,0,0.1f,0.11293429999999999f,-0.10762031f,0.0123493f,0.17779159f),
new BottomSeed(0,0,1,5,0.1f,0.1990983f,0.0459585f,0.0316974f,0.019654249999999998f),
new BottomSeed(0,2,0,1,0.1f,0.5765522000000001f,-0.569743f,0.37990830000000003f,0.4272043f),
new BottomSeed(0,1,1,0,0.1f,0.18481219999999998f,0.09754354499999998f,0.08394499999999999f,0.184271155f),
new BottomSeed(0,1,1,1,0.1f,0.1299951f,0.24475709499999998f,0.0294235f,0.30518740499999997f),
new BottomSeed(0,3,0,2,0.1f,0.54964385f,-0.56506f,0.44954075f,0.4334139f),
new BottomSeed(0,2,1,1,0.1f,0.21224735f,0.47064684999999995f,0.049442050000000015f,0.06458404999999998f),
new BottomSeed(0,2,1,2,0.1f,0.13733195f,0.4586679f,0.03727455f,0.29159809999999997f),
new BottomSeed(0,4,0,3,0.1f,0.58157095f,-0.58077115f,0.38260954999999996f,0.41657975f),
new BottomSeed(0,3,1,2,0.1f,0.1962329f,0.4669049f,0.096175f,0.2074299f),
new BottomSeed(0,3,1,3,0.1f,0.14719525f,0.6730584f,0.046954550000000005f,0.2779386f),
new BottomSeed(0,5,0,4,0.1f,0.53748905f,-0.5667629f,0.43707565f,0.4316284f),
new BottomSeed(0,4,1,4,0.1f,0.16028385f,0.79250325f,0.041072950000000004f,0.20557035f),
new BottomSeed(0,6,0,5,0.1f,0.5677772f,-0.57548095f,0.39633450000000003f,0.42432435f),
new BottomSeed(0,5,1,4,0.1f,0.19267774999999998f,0.12147855000000002f,0.09265425f,0.87840355f),
new BottomSeed(0,5,1,5,0.1f,0.1826441f,0.8930601f,0.023213399999999995f,0.10521979999999997f),
new BottomSeed(0,6,1,0,0.1f,0.2078932f,0.97647185f,0.003879300000000002f,0.021120150000000004f),
new BottomSeed(0,6,1,4,0.1f,0.18044944999999998f,-0.2305533f,0.060564849999999996f,0.05608359999999998f),
new BottomSeed(1,1,1,0,0.1f,0.5685752f,-0.8136471000000001f,0.4312626f,0.18625160000000002f),
new BottomSeed(1,0,1,5,0.1f,0.6069481f,-0.62053425f,0.39276390000000005f,0.37848015f),
new BottomSeed(1,2,1,1,0.1f,0.60429725f,-0.46362585f,0.39562555f,0.53523935f),
new BottomSeed(1,3,1,2,0.1f,0.56626055f,-0.8156664499999999f,0.43319015f,0.18192395f),
new BottomSeed(1,4,1,3,0.1f,0.6059687f,-0.6031964f,0.39365669999999997f,0.3942565f),
new BottomSeed(1,5,1,4,0.1f,0.567734f,-0.81384485f,0.4318657f,0.18527164999999995f),
new BottomSeed(1,5,2,0,0.1f,0.13523475000000001f,0.74130615f,0.028950450000000003f,0.25722245f),
new BottomSeed(2,1,2,0,0.1f,0.8623514f,-0.020669749999999987f,0.13754869999999997f,0.9792600499999999f),
new BottomSeed(2,2,2,0,0.1f,0.9322836f,0.5667253f,0.06763949999999996f,0.09006670000000003f),
new BottomSeed(2,0,2,3,0.1f,0.9345565f,0.47218485f,0.06464690000000001f,0.07959214999999997f),
new BottomSeed(2,0,2,4,0.1f,0.81448835f,-0.059496899999999964f,0.18544135f,0.9127229f),
new BottomSeed(2,2,2,1,0.1f,0.8310364f,-0.0758741f,0.16888910000000001f,0.8491061f),
new BottomSeed(2,3,2,1,0.1f,0.9352741499999999f,0.551291f,0.06430154999999999f,0.0814964f),
new BottomSeed(2,1,2,4,0.1f,0.9365942f,0.6147923f,0.06294850000000002f,0.07043899999999997f),
new BottomSeed(2,3,2,2,0.1f,0.8496822500000001f,-0.0027032000000000167f,0.15028124999999998f,0.997111f),
new BottomSeed(2,4,2,2,0.1f,0.9332022f,0.6249825f,0.0666928f,0.07865279999999997f),
new BottomSeed(2,4,2,3,0.1f,0.8109478f,-0.05648705000000004f,0.18876499999999996f,0.90431095f),
new BottomSeed(0,1,0,0,0.25f,0.54957075f,-0.55933105f,0.44897964999999995f,0.43929285f),
new BottomSeed(0,0,0,6,0.25f,0.9636405f,-0.9363531f,0.035652600000000034f,0.0602626f),
new BottomSeed(0,2,0,1,0.25f,0.96670955f,-0.9457264000000001f,0.030939649999999985f,0.053394f),
new BottomSeed(0,1,1,0,0.25f,0.1579378f,0.30902185f,0.05672989999999999f,0.03296494999999999f),
new BottomSeed(0,3,0,2,0.25f,0.55036355f,-0.5549131f,0.44927105f,0.4440655f),
new BottomSeed(0,4,0,3,0.25f,0.97248975f,-0.9610532f,0.026404550000000027f,0.038208399999999976f),
new BottomSeed(0,3,1,2,0.25f,0.16617825f,0.69667685f,0.06596175000000001f,0.03843704999999997f),
new BottomSeed(0,3,1,3,0.25f,0.115391f,0.63944575f,0.015281299999999998f,0.11123485f),
new BottomSeed(0,5,0,4,0.25f,0.5468694000000001f,-0.55595065f,0.4464885f,0.44312315f),
new BottomSeed(0,4,1,4,0.25f,0.1215644f,0.8357428f,0.021355200000000005f,0.11859039999999998f),
new BottomSeed(0,6,0,5,0.25f,0.972134f,-0.9582857499999999f,0.026845300000000016f,0.03933734999999999f),
new BottomSeed(0,5,1,4,0.25f,0.10499105f,0.97044525f,0.00476795f,0.029208049999999985f),
new BottomSeed(1,1,1,0,0.25f,0.5695508f,-0.77680655f,0.4288877f,0.22311435000000002f),
new BottomSeed(1,0,1,5,0.25f,0.61320125f,-0.67548235f,0.38627745f,0.32264024999999996f),
new BottomSeed(1,2,1,1,0.25f,0.605599f,-0.6535361000000001f,0.38470530000000003f,0.3463748f),
new BottomSeed(1,3,1,2,0.25f,0.56661895f,-0.77562995f,0.43211575f,0.22274145f),
new BottomSeed(1,4,1,3,0.25f,0.61188855f,-0.6641797f,0.38747275000000003f,0.3327265f),
new BottomSeed(1,5,1,4,0.25f,0.56810905f,-0.7730067f,0.43066724999999995f,0.2269618f),
new BottomSeed(1,5,2,0,0.25f,0.12298624999999999f,0.7499573500000001f,0.022758949999999993f,0.21579975f),
new BottomSeed(2,1,2,0,0.25f,0.8534413f,-0.036106850000000024f,0.1459842f,0.96223095f),
new BottomSeed(2,2,2,0,0.25f,0.9319498500000001f,0.55182425f,0.06802144999999998f,0.09643375000000001f),
new BottomSeed(2,0,2,3,0.25f,0.93448335f,0.4591071f,0.06488305f,0.08445910000000001f),
new BottomSeed(2,0,2,4,0.25f,0.80524985f,-0.057025800000000015f,0.19463864999999997f,0.9021241f),
new BottomSeed(2,2,2,1,0.25f,0.8217243000000001f,-0.07263720000000001f,0.17774099999999998f,0.8423172999999999f),
new BottomSeed(2,3,2,1,0.25f,0.9356843f,0.53739295f,0.06424439999999998f,0.08798475000000003f),
new BottomSeed(2,1,2,4,0.25f,0.9363666500000001f,0.5992322f,0.06351685000000001f,0.07793269999999997f),
new BottomSeed(2,3,2,2,0.25f,0.8364464f,-0.021273900000000012f,0.1633555f,0.9782441f),
new BottomSeed(2,4,2,2,0.25f,0.93182045f,0.6084178499999999f,0.06723584999999999f,0.08460695000000001f),
new BottomSeed(2,4,2,3,0.25f,0.80200535f,-0.05157020000000001f,0.19779114999999997f,0.8954831f),
new BottomSeed(0,1,0,0,0.5f,0.70410515f,-0.9160026f,0.11730255f,0.08334200000000003f),
new BottomSeed(0,3,0,2,0.5f,0.7173591f,-0.931875f,0.10202270000000002f,0.0674572f),
new BottomSeed(0,5,0,4,0.5f,0.7090563999999999f,-0.9523425999999999f,0.08738410000000002f,0.04738159999999997f),
new BottomSeed(1,1,1,0,0.5f,0.45398765f,-0.77941525f,0.30148265f,0.22002865000000005f),
new BottomSeed(1,0,1,5,0.5f,0.63173325f,-0.9805347f,0.07225435000000002f,0.018668099999999965f),
new BottomSeed(1,2,1,1,0.5f,0.6545704f,-0.9517697f,0.12414749999999997f,0.04760340000000002f),
new BottomSeed(1,3,1,2,0.5f,0.60386495f,-0.9604504f,0.10078024999999996f,0.03935090000000002f),
new BottomSeed(1,4,1,3,0.5f,0.6425576f,-0.9664546f,0.09943359999999996f,0.03187060000000003f),
new BottomSeed(1,5,1,4,0.5f,0.4618023f,-0.787935f,0.27693350000000005f,0.21133110000000005f),
new BottomSeed(2,1,2,0,0.5f,0.7097862f,-0.83048115f,0.2875902f,0.16825795f),
new BottomSeed(2,0,2,4,0.5f,0.72944405f,-0.80572855f,0.26873815f,0.19326584999999996f),
new BottomSeed(2,2,2,1,0.5f,0.71929775f,-0.78371305f,0.27851075000000003f,0.21373365f),
new BottomSeed(2,3,2,2,0.5f,0.7154263f,-0.8215963500000001f,0.2840547f,0.17727345f),
new BottomSeed(2,4,2,3,0.5f,0.72309775f,-0.79504095f,0.27644155000000004f,0.20296485000000003f),
new BottomSeed(0,1,1,2,0.75f,0.35108375f,0.9860662f,0.0015512499999999763f,0.01070800000000005f),
new BottomSeed(0,3,1,3,0.75f,0.20787095f,0.65072235f,0.035555050000000005f,0.34803525f),
new BottomSeed(0,4,1,4,0.75f,0.1396004f,0.8711458f,0.00043330000000001145f,0.12806669999999998f),
new BottomSeed(0,5,1,4,0.75f,0.11104695f,0.95002995f,0.010987549999999999f,0.048881549999999996f),
new BottomSeed(0,5,1,5,0.75f,0.2375447f,0.8534828999999999f,0.013048000000000004f,0.14493879999999998f),
new BottomSeed(0,6,1,0,0.75f,0.33947685f,0.8983137999999999f,0.008990949999999998f,0.09903310000000004f),
new BottomSeed(1,1,1,0,0.75f,0.33307615f,-0.52907205f,0.10296595f,0.12768015f),
new BottomSeed(0,1,1,2,1.0f,0.43578415000000004f,0.93955045f,0.036030250000000014f,0.05973475f),
new BottomSeed(0,3,1,3,1.0f,0.5853573f,0.5691044f,0.3423795f,0.4292806f),
new BottomSeed(0,4,1,4,1.0f,0.19236055f,0.7886741500000001f,0.006502750000000002f,0.21091705f),
new BottomSeed(0,5,1,4,1.0f,0.11547175f,0.9183976f,0.014996250000000003f,0.08055380000000001f),
new BottomSeed(0,5,1,5,1.0f,0.32468655f,0.7626843999999999f,0.027743749999999984f,0.232547f),
new BottomSeed(0,6,1,0,1.0f,0.56539875f,0.90849745f,0.03593004999999999f,0.09041415000000003f),
new BottomSeed(1,1,1,0,1.0f,0.36397605f,-0.4316846f,0.07159315f,0.09515309999999999f),
new BottomSeed(2,1,2,0,1.0f,0.42092745f,-0.7238798f,0.15633695f,0.1931914f),
new BottomSeed(2,3,2,2,1.0f,0.39151265f,-0.6038996f,0.06983225000000001f,0.11768259999999997f),
new BottomSeed(2,4,2,3,1.0f,0.4718481f,-0.53011085f,0.08362270000000002f,0.05961704999999998f)};
 sealed class BottomPatch { public int layer,index; public Mesh mesh; public Vertex[] work; public int[] ids,wall;public float[] radius;public bool[] dirty; public Vector3[][] delta; public int[] upperIds=new int[0];public Vector3[][] upper; public int[] extraIds=new int[0];public Vector3[][] extra; }
 readonly List<BottomPatch> bottomPatches=new List<BottomPatch>();
 const int BottomCols=129,BottomSurface=129*81;
 bool bottomPreviewEnabled=true;
 float bottomProgress=-1,bottomLow,bottomHigh;
 bool upperPreview=true,extraPreview=true;
 Vector3 BottomPoint(int l,int i,float p,float t,float s) {
  var layer=layers[l];var g=SurfaceFor(layer,i);float q=Mathf.Clamp01((p*BloomDuration-layer.config.delay)/layer.config.duration)*32;
  int a=Mathf.Min((int)q,31);float fx=Mathf.Clamp((s+1)*64,0,128),fy=Mathf.Clamp(t*80,0,80);
  int x=Mathf.Min((int)fx,127),y=Mathf.Min((int)fy,79);int k=y*129+x;
  var first=g.poses[a];var next=g.poses[a+1];
  Vector3 p0=Vector3.Lerp(Vector3.Lerp(BottomMid(first,k),BottomMid(first,k+1),fx-x),Vector3.Lerp(BottomMid(first,k+129),BottomMid(first,k+130),fx-x),fy-y);
  Vector3 p1=Vector3.Lerp(Vector3.Lerp(BottomMid(next,k),BottomMid(next,k+1),fx-x),Vector3.Lerp(BottomMid(next,k+129),BottomMid(next,k+130),fx-x),fy-y);
  return Vector3.Lerp(p0,p1,q-a);
 }
 static Vector3 BottomMid(Pose p,int k)=>(p.data[k].p+p.data[k+BottomSurface].p)*.5f;
 Matrix4x4 BottomMatrix(int l,int i,float p) {
  var layer=layers[l];var petal=layer.petals[i];float q=Mathf.Clamp01((p*BloomDuration-layer.config.delay)/layer.config.duration);
  PathPetalPose(layer,petal,i,q,out var pos,out var rot,out var scale);
  if(!rootBaseline)pos.y=layers[0].config.rootHeight+.0045f+layer.config.rootStagger*RootStaggerSign(i,layer.petals.Length);
  return Matrix4x4.TRS(pos,rot,scale)*Matrix4x4.Translate(-BottomPoint(l,i,p,0,0));
 }
 void BuildBottomPatches() {
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bottomTrace")>=0)return;
  if(!Application.isEditor&&LoadBottomCache())return;
#if UNITY_EDITOR
  bool forceRebuild=Array.IndexOf(Environment.GetCommandLineArgs(),"-rebuildBottom")>=0;
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-innerReference")>=0){
   var reference=Path.Combine(Application.dataPath,"../Evidence/Inner60/Baseline/Assets/Flower/Resources/BottomOverlap.bytes");
   if(!LoadBottomCache(File.ReadAllBytes(reference)))throw new InvalidOperationException("Inner60 reference configuration mismatch.");return;
  }
  if(!forceRebuild&&BottomCacheProvenanceMatches()&&LoadBottomCache())return;
  var innerFlowBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/InnerFlow/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(innerFlowBaseline)&&LoadBottomCache(File.ReadAllBytes(innerFlowBaseline))){SmoothInnerFlow();return;}
  var innerBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/Inner60/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(innerBaseline)&&LoadBottomCache(File.ReadAllBytes(innerBaseline))){BuildInnerResidualPatches();return;}
  var flowBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/MiddleFlow/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(flowBaseline)&&LoadBottomCache(File.ReadAllBytes(flowBaseline))){SmoothMiddleFlow();return;}
  var middleBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/Middle47/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(middleBaseline)&&LoadBottomCache(File.ReadAllBytes(middleBaseline))){BuildMiddleResidualPatches();return;}
  var residualBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/Outer58/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(residualBaseline)&&LoadBottomCache(File.ReadAllBytes(residualBaseline))){BuildOuterResidualPatches();return;}
  var outerBaseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/OuterFlow/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(outerBaseline)&&LoadBottomCache(File.ReadAllBytes(outerBaseline))){SmoothOuterPatches();return;}
  var previous=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/LayerOverlap/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(previous)&&LoadBottomCache(File.ReadAllBytes(previous),false,true)){BuildLayerPatches();return;}
  var baseline=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/UpperCorner/Baseline/Assets/Flower/Resources/BottomOverlap.bytes"));
  if(File.Exists(baseline)&&LoadBottomCache(File.ReadAllBytes(baseline),true)){BuildUpperCorners();return;}
#endif
  float low=float.PositiveInfinity,high=float.NegativeInfinity;
  for(int l=0;l<layers.Length;l++)for(int i=0;i<layers[l].petals.Length;i++){
   var m=BottomMatrix(l,i,0);var data=SurfaceFor(layers[l],i).poses[0].data;
   for(int k=0;k<BottomSurface;k++){float y=m.MultiplyPoint3x4(data[k].p).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
  }
  bottomLow=Mathf.Lerp(low,high,.6f);bottomHigh=Mathf.Lerp(low,high,.8f);

  foreach(var seed in bottomSeeds) {
   if(seed.a>=layers.Length||seed.b>=layers.Length||seed.i>=layers[seed.a].petals.Length||seed.j>=layers[seed.b].petals.Length)continue;
   if(bottomPatches.Exists(p=>p.layer==seed.a&&p.index==seed.i))continue;
   var mask=new HashSet<int>();
   foreach(var c in bottomSeeds)if(c.a==seed.a&&c.i==seed.i)for(int y=9;y<=80;y++)for(int x=0;x<129;x++)
    mask.Add(y*129+x);
   if(mask.Count==0)continue;
   var patch=new BottomPatch{layer=seed.a,index=seed.i,ids=new int[mask.Count],delta=new Vector3[33][]};mask.CopyTo(patch.ids);Array.Sort(patch.ids);
   bool changed=false;
   for(int pose=0;pose<33;pose++) {
    float phase=pose/32f;patch.delta[pose]=new Vector3[patch.ids.Length];var matrix=BottomMatrix(seed.a,seed.i,phase);var inverse=matrix.inverse;
    foreach(var c in bottomSeeds)if(c.a==seed.a&&c.i==seed.i&&c.b<layers.Length&&c.j<layers[c.b].petals.Length) {
     float gate=(1-Mathf.SmoothStep(0,1,Mathf.Abs(phase-c.p)/.18f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.65f,phase)));if(gate<=0)continue;
     var other=BottomMatrix(c.b,c.j,phase);
     var tree=new BottomTree(this,c.b,c.j,phase,other);
     for(int k=0;k<patch.ids.Length;k++) {
      int id=patch.ids[k];float t=(id/129)/80f,s=(id%129)/64f-1;
      float envelope=(1-Mathf.SmoothStep(0,1,Mathf.Max(0,Mathf.Abs(t-c.t)-c.u)/.06f))*(1-Mathf.SmoothStep(0,1,Mathf.Max(0,Mathf.Abs(s-c.s)-c.v)/.15f))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.16f,t))*gate;
      if(envelope<=0)continue;
      Vector3 point=matrix.MultiplyPoint3x4(BottomPoint(seed.a,seed.i,phase,t,s));
      envelope*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(bottomLow,bottomHigh,point.y));
      if(envelope<=0||new Vector2(point.x,point.z).magnitude<.16f)continue;
      Vector3 across=matrix.MultiplyVector(BottomPoint(seed.a,seed.i,phase,t,s+.015625f)-BottomPoint(seed.a,seed.i,phase,t,s-.015625f));
      Vector3 along=matrix.MultiplyVector(BottomPoint(seed.a,seed.i,phase,t+.0125f,s)-BottomPoint(seed.a,seed.i,phase,t-.0125f,s));
      Vector3 direction=Vector3.Cross(across,along).normalized;
      if(Vector3.Dot(direction,new Vector3(point.x,-.25f,point.z))<0)direction=-direction;
      float distance=tree.OutsideDistance(point,direction);
      if(distance<=-.012f||distance>.18f)continue;
      Vector3 correction=Vector3.ClampMagnitude(inverse.MultiplyVector(direction*((distance+.012f)*envelope)),.08f);
      if(correction.sqrMagnitude>patch.delta[pose][k].sqrMagnitude)patch.delta[pose][k]=correction;
      changed|=correction.sqrMagnitude>1e-12f;
     }
    }
   }
   if(!changed)continue;
   var lookup=new int[BottomSurface];for(int k=0;k<lookup.Length;k++)lookup[k]=-1;for(int k=0;k<patch.ids.Length;k++)lookup[patch.ids[k]]=k;
   for(int pose=0;pose<33;pose++){
    var allowed=new bool[patch.ids.Length];var m=BottomMatrix(seed.a,seed.i,pose/32f);
    for(int k=0;k<allowed.Length;k++){int id=patch.ids[k];var point=m.MultiplyPoint3x4(BottomPoint(seed.a,seed.i,pose/32f,(id/129)/80f,(id%129)/64f-1));allowed[k]=point.y<bottomHigh&&id/129>=9;}
    for(int pass=0;pass<8;pass++){
     var smooth=new Vector3[patch.ids.Length];
     for(int k=0;k<patch.ids.Length;k++){int id=patch.ids[k];Vector3 sum=patch.delta[pose][k]*2;int count=2;
      for(int direction=0;direction<4;direction++){int n=direction==0?id-129:direction==1?id+129:direction==2?(id%129>0?id-1:-1):(id%129<128?id+1:-1);if(n>=0&&n<lookup.Length){if(lookup[n]>=0)sum+=patch.delta[pose][lookup[n]];count++;}}
      smooth[k]=allowed[k]?sum/count:Vector3.zero;
     }patch.delta[pose]=smooth;
    }
   }
   var active=new List<int>();for(int k=0;k<patch.ids.Length;k++){bool used=false;for(int pose=0;pose<33;pose++)used|=patch.delta[pose][k].sqrMagnitude>1e-12f;if(used)active.Add(k);}
   var trimmed=new int[active.Count];for(int k=0;k<active.Count;k++)trimmed[k]=patch.ids[active[k]];
   for(int pose=0;pose<33;pose++){var compact=new Vector3[active.Count];for(int k=0;k<active.Count;k++)compact[k]=patch.delta[pose][active[k]];patch.delta[pose]=compact;}patch.ids=trimmed;
   AttachBottomPatch(patch);
  }
#if UNITY_EDITOR
  BuildUpperCorners();
  BuildLayerPatches();
  SmoothOuterPatches();
  BuildOuterResidualPatches();
#endif
 }
 internal long BottomPatchCacheBytes {get{long bytes=0;foreach(var p in bottomPatches)bytes+=(p.ids.Length+p.upperIds.Length+p.extraIds.Length)*33L*12;return bytes;}}
 internal long BottomWorkingBytes {get{long bytes=0;foreach(var p in bottomPatches)bytes+=p.work.Length*40L+p.wall.Length*4L+p.dirty.Length+(p.ids.Length+p.upperIds.Length+p.extraIds.Length)*4L+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(p.mesh);return bytes;}}
 void AttachBottomPatch(BottomPatch patch){
  patch.radius=new float[33];for(int pose=0;pose<33;pose++)foreach(var d in patch.delta[pose])patch.radius[pose]=Mathf.Max(patch.radius[pose],d.magnitude);
  if(patch.upper!=null)for(int pose=0;pose<33;pose++){float upperRadius=0;foreach(var d in patch.upper[pose])upperRadius=Mathf.Max(upperRadius,d.magnitude);patch.radius[pose]+=upperRadius;}
  if(patch.extra!=null)for(int pose=0;pose<33;pose++){float radius=0;foreach(var d in patch.extra[pose])radius=Mathf.Max(radius,d.magnitude);patch.radius[pose]+=radius;}
  var g=SurfaceFor(layers[patch.layer],patch.index);patch.mesh=Instantiate(g.mesh);patch.mesh.name="Local bottom overlap "+patch.layer+"-"+patch.index;patch.mesh.MarkDynamic();patch.work=new Vertex[g.data.Length];patch.dirty=new bool[BottomSurface];
  var uv=patch.mesh.uv;patch.wall=new int[uv.Length-BottomSurface*2];
  for(int k=BottomSurface*2;k<uv.Length;k++)patch.wall[k-BottomSurface*2]=Mathf.RoundToInt(uv[k].y*80)*129+Mathf.RoundToInt(uv[k].x*128)+(k%2==0?0:BottomSurface);
  layers[patch.layer].petals[patch.index].body.GetComponent<MeshFilter>().sharedMesh=patch.mesh;bottomPatches.Add(patch);
  CacheBytes+=(patch.ids.Length+patch.upperIds.Length+patch.extraIds.Length)*33L*12;
 }
 string BottomConfigHash(){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(preset))));}
#if UNITY_EDITOR
 static string BottomCacheSignature()
 {
  var files=new[]{"Flower/Runtime/FlowerBottomPatches.cs","Flower/Runtime/FlowerLayerPatches.cs","HeroPetal/Runtime/HeroPetal.cs","Flower/Runtime/FlowerAssembler.cs","Flower/Runtime/FlowerPreset.cs","Flower/Art/ExhibitionPeony.asset","HeroPetal/Prefabs/HeroPetal.prefab"};
  return string.Join("\n",Array.ConvertAll(files,path=>{using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant();}));
 }
 bool BottomCacheProvenanceMatches()
 {
  try
  {
   string path=Path.Combine(Application.dataPath,"Flower/Resources/BottomOverlapProvenance.txt");
   return File.Exists(path)&&File.ReadAllText(path).Trim()==BottomCacheSignature();
  }
  catch(Exception) { return false; }
 }
 internal void SaveBottomCache(){
  string directory=Path.Combine(Application.dataPath,"Flower/Resources");Directory.CreateDirectory(directory);
  using(var w=new BinaryWriter(File.Create(Path.Combine(directory,"BottomOverlap.bytes")))){
   w.Write("BottomOverlap/3");w.Write(BottomConfigHash());w.Write(bottomLow);w.Write(bottomHigh);w.Write(bottomPatches.Count);
   foreach(var p in bottomPatches){w.Write(p.layer);w.Write(p.index);w.Write(p.ids.Length);foreach(int id in p.ids)w.Write(id);foreach(var pose in p.delta)foreach(var d in pose){w.Write(d.x);w.Write(d.y);w.Write(d.z);}
    w.Write(p.upperIds.Length);foreach(int id in p.upperIds)w.Write(id);if(p.upper!=null)foreach(var pose in p.upper)foreach(var d in pose){w.Write(d.x);w.Write(d.y);w.Write(d.z);}
    w.Write(p.extraIds.Length);foreach(int id in p.extraIds)w.Write(id);if(p.extra!=null)foreach(var pose in p.extra)foreach(var d in pose){w.Write(d.x);w.Write(d.y);w.Write(d.z);}}
  }
  File.WriteAllText(Path.Combine(directory,"BottomOverlapProvenance.txt"),BottomCacheSignature());
 }
#endif
 bool LoadBottomCache(byte[] data=null,bool baseline=false,bool previous=false){
  if(data==null){var asset=Resources.Load<TextAsset>("BottomOverlap");if(asset==null)return false;data=asset.bytes;}
  using(var r=new BinaryReader(new MemoryStream(data))){
   if(r.ReadString()!=(baseline?"BottomOverlap/1":previous?"BottomOverlap/2":"BottomOverlap/3")||r.ReadString()!=BottomConfigHash())return false;
   bottomLow=r.ReadSingle();bottomHigh=r.ReadSingle();int count=r.ReadInt32();if(count<0||count>PetalCount)return false;
   var pending=new List<BottomPatch>();
   for(int i=0;i<count;i++){
    int layer=r.ReadInt32(),index=r.ReadInt32(),length=r.ReadInt32();
    if(layer<0||layer>=layers.Length||index<0||index>=layers[layer].petals.Length||length<0||length>BottomSurface)return false;
    var p=new BottomPatch{layer=layer,index=index,ids=new int[length],delta=new Vector3[33][]};
    for(int k=0;k<length;k++){p.ids[k]=r.ReadInt32();if(p.ids[k]<0||p.ids[k]>=BottomSurface)return false;}
    for(int pose=0;pose<33;pose++){p.delta[pose]=new Vector3[length];for(int k=0;k<length;k++)p.delta[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}int n=baseline?0:r.ReadInt32();if(n<0||n>BottomSurface)return false;p.upperIds=new int[n];p.upper=new Vector3[33][];for(int k=0;k<n;k++){p.upperIds[k]=r.ReadInt32();if(p.upperIds[k]<0||p.upperIds[k]>=BottomSurface)return false;}for(int pose=0;pose<33;pose++){p.upper[pose]=new Vector3[n];for(int k=0;k<n;k++)p.upper[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}if(!baseline&&!previous){int e=r.ReadInt32();p.extraIds=new int[e];p.extra=new Vector3[33][];for(int k=0;k<e;k++)p.extraIds[k]=r.ReadInt32();for(int pose=0;pose<33;pose++){p.extra[pose]=new Vector3[e];for(int k=0;k<e;k++)p.extra[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}}pending.Add(p);
   }
   foreach(var p in pending)AttachBottomPatch(p);return true;
  }
 }
 internal bool BottomIsolationCheck(){
  foreach(var p in bottomPatches){var g=SurfaceFor(layers[p.layer],p.index);var mask=new bool[BottomSurface];foreach(int id in p.ids)mask[id]=true;foreach(int id in p.upperIds)mask[id]=true;foreach(int id in p.extraIds)mask[id]=true;
   for(int id=0;id<BottomSurface;id++)if(id/129<8||!mask[id])if(p.work[id].p!=g.data[id].p||p.work[id+BottomSurface].p!=g.data[id+BottomSurface].p)return false;
  }return true;
 }
 sealed class BottomTree {
  struct Node { public Bounds bounds;public int x0,x1,y0,y1,left,right; }
  readonly Vector3[] v=new Vector3[BottomSurface];readonly List<Node> nodes=new List<Node>();
  public BottomTree(FlowerAssembler f,int layer,int index,float phase,Matrix4x4 matrix,bool corrected=false,bool complete=false){for(int y=0;y<=80;y++)for(int x=0;x<129;x++)v[y*129+x]=matrix.MultiplyPoint3x4(f.BottomPoint(layer,index,phase,y/80f,x/64f-1)+(corrected?(f.BottomDeltaAt(layer,index,phase,y*129+x)+f.UpperDeltaAt(layer,index,phase,y*129+x)+(complete?f.ExtraDeltaAt(layer,index,phase,y*129+x):Vector3.zero)):Vector3.zero));Build(0,128,0,80);}
  int Build(int x0,int x1,int y0,int y1){int id=nodes.Count;var n=new Node{x0=x0,x1=x1,y0=y0,y1=y1,left=-1,right=-1};nodes.Add(n);
   if(x1-x0>4||y1-y0>4){if(x1-x0>=y1-y0){int m=(x0+x1)/2;n.left=Build(x0,m,y0,y1);n.right=Build(m,x1,y0,y1);}else{int m=(y0+y1)/2;n.left=Build(x0,x1,y0,m);n.right=Build(x0,x1,m,y1);}n.bounds=nodes[n.left].bounds;n.bounds.Encapsulate(nodes[n.right].bounds);}
   else{n.bounds=new Bounds(v[y0*129+x0],Vector3.zero);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)n.bounds.Encapsulate(v[y*129+x]);}n.bounds.Expand(.00001f);nodes[id]=n;return id;
  }
  public float OutsideDistance(Vector3 point,Vector3 direction){float distance=float.NegativeInfinity;Hit(0,new Ray(point-direction*.18f,direction),ref distance);return distance-.18f;}
  void Hit(int id,Ray ray,ref float distance){var n=nodes[id];if(!n.bounds.IntersectRay(ray,out float near)||near>.36f)return;
   if(n.left>=0){Hit(n.left,ray,ref distance);Hit(n.right,ray,ref distance);return;}
   for(int y=n.y0;y<n.y1;y++)for(int x=n.x0;x<n.x1;x++){int a=y*129+x,b=a+129,c=a+1;Triangle(ray,v[a],v[b],v[c],ref distance);Triangle(ray,v[c],v[b],v[b+1],ref distance);}
  }
  static void Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,ref float distance){var e=b-a;var f=c-a;var h=Vector3.Cross(ray.direction,f);float determinant=Vector3.Dot(e,h);if(Mathf.Abs(determinant)<1e-10f)return;
   float inv=1/determinant;var s=ray.origin-a;float u=Vector3.Dot(s,h)*inv;if(u<0||u>1)return;var q=Vector3.Cross(s,e);float w=Vector3.Dot(ray.direction,q)*inv;if(w<0||u+w>1)return;float t=Vector3.Dot(f,q)*inv;if(t>=0&&t<=.36f)distance=Mathf.Max(distance,t);
  }
 }
 void ApplyBottomPatches() {
  if(!bottomPreviewEnabled||bottomProgress==bloomProgress)return;bottomProgress=bloomProgress;
  foreach(var patch in bottomPatches) {
   float frame=(artActive?ArtPatchProgress(layers[patch.layer].config):Mathf.Clamp01(bloomProgress))*32;int a=Mathf.Min((int)frame,31);float f=frame-a;
   var g=SurfaceFor(layers[patch.layer],patch.index);Array.Copy(g.data,patch.work,g.data.Length);Array.Clear(patch.dirty,0,patch.dirty.Length);
   for(int k=0;k<patch.ids.Length;k++){int id=patch.ids[k];var d=Vector3.Lerp(patch.delta[a][k],patch.delta[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   if(upperPreview&&patch.upper!=null)for(int k=0;k<patch.upperIds.Length;k++){int id=patch.upperIds[k];var d=Vector3.Lerp(patch.upper[a][k],patch.upper[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   if(extraPreview&&patch.extra!=null)for(int k=0;k<patch.extraIds.Length;k++){int id=patch.extraIds[k];var d=Vector3.Lerp(patch.extra[a][k],patch.extra[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   for(int y=8;y<=80;y++)for(int x=0;x<129;x++) {
    int id=y*129+x;if(!patch.dirty[id])continue;Vector3 across=patch.work[y*129+Mathf.Min(x+1,128)].p-patch.work[y*129+Mathf.Max(x-1,0)].p;
    Vector3 along=patch.work[Mathf.Min(y+1,80)*129+x].p-patch.work[Mathf.Max(y-1,0)*129+x].p;
    var tangent=across.normalized;var n=Vector3.Cross(along,tangent).normalized;patch.work[id].n=n;patch.work[id+BottomSurface].n=-n;
    patch.work[id].t=new Vector4(tangent.x,tangent.y,tangent.z,-1);patch.work[id+BottomSurface].t=new Vector4(tangent.x,tangent.y,tangent.z,1);
   }
   for(int k=BottomSurface*2;k<patch.work.Length;k++)patch.work[k].p=patch.work[patch.wall[k-BottomSurface*2]].p;
   for(int k=BottomSurface*2;k<patch.work.Length;k+=2){int id=patch.wall[k-BottomSurface*2];if(!patch.dirty[id])continue;
    int previous=k==BottomSurface*2?patch.work.Length-2:k-2,next=k+2>=patch.work.Length?BottomSurface*2:k+2;
    var edge=(patch.work[next].p-patch.work[previous].p).normalized;var n=Vector3.Cross(edge,-patch.work[id].n).normalized;
    patch.work[k].n=patch.work[k+1].n=n;patch.work[k].t=patch.work[k+1].t=new Vector4(edge.x,edge.y,edge.z,1);
   }
   patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length,0,MeshUpdateFlags.DontRecalculateBounds);var bounds=g.mesh.bounds;bounds.Expand(2*Mathf.Max(patch.radius[a],patch.radius[a+1]));patch.mesh.bounds=bounds;MeshUpdates++;
  }
 }
 Vector3 BottomDeltaAt(int layer,int index,float phase,int id){foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){int k=Array.BinarySearch(patch.ids,id);if(k<0)return Vector3.zero;float q=phase*32;int a=Mathf.Min((int)q,31);return Vector3.Lerp(patch.delta[a][k],patch.delta[a+1][k],q-a);}return Vector3.zero;}
 internal void UpperCornerPreview(bool enabled){upperPreview=enabled;bottomProgress=-1;ApplyBottomPatches();}
#if UNITY_EDITOR
 // Fit only the tip-connected penetration component; two UV-grid rings form the transition.
 void BuildUpperCorners(){
  string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/UpperCorner/Bake"));Directory.CreateDirectory(directory);
  var report=new StringBuilder("petal,neighbor,progress,contacts,minT,maxT,minS,maxS,coreVertices,limitedVertices,maxOffset\n");
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  var values=new Vector3[7][][];for(int i=0;i<7;i++)values[i]=new Vector3[33][];
  for(int pose=0;pose<33;pose++){
   float phase=pose/32f;scan.Sample(phase,null,0);
   for(int i=0;i<7;i++){
    values[i][pose]=new Vector3[BottomSurface];int neighbor=(i+6)%7;scan.FindPairContacts(i,neighbor,hits);
    float minT=1,maxT=0,minS=1,maxS=-1,neighborT=0,neighborS=0;int contacts=0;
    foreach(var h in hits){float t=h.lengthA,s=h.widthA;if(t<.85f||s>-.55f)continue;contacts++;neighborT+=h.lengthB;neighborS+=h.widthB;minT=Mathf.Min(minT,t);maxT=Mathf.Max(maxT,t);minS=Mathf.Min(minS,s);maxS=Mathf.Max(maxS,s);}
    if(contacts==0||pose==32){report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},0,0,0,0,0,0,0,0\n",i,neighbor,phase);continue;}
    var matrix=BottomMatrix(0,i,phase);var inverse=matrix.inverse;var other=BottomMatrix(0,neighbor,phase);var tree=new BottomTree(this,0,neighbor,phase,other,true);
    neighborT/=contacts;neighborS/=contacts;
    var neighborAcross=other.MultiplyVector(BottomPoint(0,neighbor,phase,neighborT,neighborS+.015625f)-BottomPoint(0,neighbor,phase,neighborT,neighborS-.015625f));
    var neighborAlong=other.MultiplyVector(BottomPoint(0,neighbor,phase,neighborT+.0125f,neighborS)-BottomPoint(0,neighbor,phase,neighborT-.0125f,neighborS));
    var outward=Vector3.Cross(neighborAcross,neighborAlong).normalized;var contactPoint=other.MultiplyPoint3x4(BottomPoint(0,neighbor,phase,neighborT,neighborS));if(Vector3.Dot(outward,new Vector3(contactPoint.x,0,contactPoint.z))<0)outward=-outward;
    var required=new Vector3[BottomSurface];var core=new bool[BottomSurface];int limited=0,coreCount=0;float maximum=0;
    // Region bounds come from this phase's exact crossing; include the free tip side.
    int x0=0,x1=Mathf.Min(40,Mathf.CeilToInt((maxS+1)*64)+2),y0=Mathf.Max(68,Mathf.FloorToInt(minT*80)-2);
    for(int y=y0;y<=80;y++)for(int x=x0;x<=x1;x++){
     int id=y*129+x;float t=y/80f,s=x/64f-1;var point=matrix.MultiplyPoint3x4(BottomPoint(0,i,phase,t,s)+BottomDeltaAt(0,i,phase,id));
     float distance=tree.OutsideDistance(point,outward);if(distance<0||distance>.18f)continue;
     var delta=inverse.MultiplyVector(outward*(distance+.012f));if(delta.magnitude>.08f)limited++;required[id]=Vector3.ClampMagnitude(delta,.08f);core[id]=true;
    }
    // Keep only components connected to the free tip; unrelated upper contacts stay unchanged.
    var connected=new bool[BottomSurface];var queue=new Queue<int>();for(int x=0;x<=x1;x++){int id=80*129+x;if(core[id]){connected[id]=true;queue.Enqueue(id);}}
    for(int y=y0;y<80;y++){int id=y*129;if(core[id]&&!connected[id]){connected[id]=true;queue.Enqueue(id);}}
    while(queue.Count>0){int id=queue.Dequeue();foreach(int n in new[]{id-129,id+129,id%129>0?id-1:-1,id%129<128?id+1:-1})if(n>=0&&n<BottomSurface&&core[n]&&!connected[n]){connected[n]=true;queue.Enqueue(n);}}
    var ring=new int[BottomSurface];for(int k=0;k<ring.Length;k++)ring[k]=-1;
    for(int id=0;id<BottomSurface;id++)if(connected[id]){ring[id]=0;values[i][pose][id]=required[id]*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,.925f,(id/129)/80f));queue.Enqueue(id);coreCount++;}
    while(queue.Count>0){int id=queue.Dequeue();if(ring[id]==2)continue;foreach(int n in new[]{id-129,id+129,id%129>0?id-1:-1,id%129<128?id+1:-1})if(n>=68*129&&n<BottomSurface&&ring[n]<0){ring[n]=ring[id]+1;queue.Enqueue(n);}}
    for(int pass=0;pass<3;pass++){var smooth=(Vector3[])values[i][pose].Clone();for(int id=68*129;id<BottomSurface;id++)if(ring[id]>=0){Vector3 sum=values[i][pose][id]*2;int count=2;foreach(int n in new[]{id-129,id+129,id%129>0?id-1:-1,id%129<128?id+1:-1})if(n>=0&&n<BottomSurface){sum+=values[i][pose][n];count++;}smooth[id]=ring[id]==2?Vector3.zero:sum/Mathf.Max(1,count);}values[i][pose]=smooth;}
    foreach(var d in values[i][pose])maximum=Mathf.Max(maximum,d.magnitude);
    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}\n",i,neighbor,phase,contacts,minT,maxT,minS,maxS,coreCount,limited,maximum);
   }
  }
  foreach(var patch in bottomPatches)if(patch.layer==0){var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool active=false;for(int pose=0;pose<33;pose++)active|=values[patch.index][pose][id].sqrMagnitude>1e-12f;if(active)ids.Add(id);}patch.upperIds=ids.ToArray();patch.upper=new Vector3[33][];for(int pose=0;pose<33;pose++){patch.upper[pose]=new Vector3[ids.Count];for(int k=0;k<ids.Count;k++){var d=values[patch.index][pose][ids[k]];patch.upper[pose][k]=d;}float upperRadius=0;foreach(var d in patch.upper[pose])upperRadius=Mathf.Max(upperRadius,d.magnitude);patch.radius[pose]+=upperRadius;}CacheBytes+=ids.Count*33L*12;}
  File.WriteAllText(Path.Combine(directory,"Regions.csv"),report.ToString());
 }
#endif
 internal string BottomPatchReport(){long bytes=0;float max=0;foreach(var p in bottomPatches){bytes+=p.ids.Length*33L*12+p.work.Length*40L;foreach(var pose in p.delta)foreach(var d in pose)max=Mathf.Max(max,d.magnitude);}return "bottomBand="+bottomLow+".."+bottomHigh+" patches="+bottomPatches.Count+" bytes="+bytes+" maxLocalOffset="+max;}
 internal void BottomPatchPreview(bool enabled){bottomPreviewEnabled=enabled;foreach(var p in bottomPatches)layers[p.layer].petals[p.index].body.GetComponent<MeshFilter>().sharedMesh=enabled?p.mesh:SurfaceFor(layers[p.layer],p.index).mesh;if(enabled){bottomProgress=-1;ApplyBottomPatches();}}
 void ReleaseBottomPatches(){foreach(var patch in bottomPatches)if(patch.mesh!=null)Destroy(patch.mesh);bottomPatches.Clear();bottomProgress=-1;}
}
}
