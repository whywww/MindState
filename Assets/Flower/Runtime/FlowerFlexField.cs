using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
#if UNITY_EDITOR
 static Vector3[] SmoothFlexNormals(Vertex[] surface){
  var current=new Vector3[BottomSurface];for(int i=0;i<BottomSurface;i++)current[i]=surface[i].n;
  for(int pass=0;pass<20;pass++){var next=new Vector3[BottomSurface];for(int y=0;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;var sum=current[id]*4+current[y*129+Mathf.Max(0,x-1)]+current[y*129+Mathf.Min(128,x+1)]+current[Mathf.Max(0,y-1)*129+x]+current[Mathf.Min(80,y+1)*129+x];next[id]=sum.sqrMagnitude>1e-12f?sum.normalized:current[id];}current=next;}return current;
 }
#endif
 sealed class FlexField {
  internal const int Cols=20,Rows=14,Count=Cols*Rows;
  internal float[] controls=new float[Count];
  internal readonly int[][] ids=new int[BottomSurface][];
  internal readonly float[][] weights=new float[BottomSurface][];
  static float[] Cubic(float t){float q=1-t;return new[]{q*q*q/6,(3*t*t*t-6*t*t+4)/6,(-3*t*t*t+3*t*t+3*t+1)/6,t*t*t/6};}
  internal FlexField(){for(int y=0;y<=80;y++)for(int x=0;x<=128;x++){int id=y*129+x;float u=x/8f+1,v=y/8f+1;int ix=(int)u,iy=(int)v;var a=Cubic(u-ix);var b=Cubic(v-iy);ids[id]=new int[16];weights[id]=new float[16];float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,20,y));for(int j=0;j<4;j++)for(int i=0;i<4;i++){int k=j*4+i;ids[id][k]=(iy+j-1)*Cols+ix+i-1;weights[id][k]=a[i]*b[j]*fade;}}}
  internal float Height(int id){float sum=0;for(int k=0;k<16;k++)sum+=controls[ids[id][k]]*weights[id][k];return sum;}
  internal void Propose(int id,float delta,float[] sum,int[] counts){float den=0;foreach(float w in weights[id])den+=w*w;if(den<1e-8f)return;for(int k=0;k<16;k++){float w=weights[id][k];if(w<1e-5f)continue;int c=ids[id][k];sum[c]+=w*delta/den;counts[c]++;}}
  internal void Step(float[] sum,int[] counts){var next=(float[])controls.Clone();for(int y=0;y<Rows;y++)for(int x=0;x<Cols;x++){int id=y*Cols+x;float value=controls[id]+(counts[id]>0?.8f*sum[id]/counts[id]:0);float smooth=0;int n=0;if(x>0){smooth+=controls[id-1];n++;}if(x+1<Cols){smooth+=controls[id+1];n++;}if(y>0){smooth+=controls[id-Cols];n++;}if(y+1<Rows){smooth+=controls[id+Cols];n++;}next[id]=value*.9978f+.002f*smooth/n;}controls=next;float max=0;for(int id=0;id<BottomSurface;id++)max=Mathf.Max(max,Mathf.Abs(Height(id)));if(max>.1f)for(int i=0;i<Count;i++)controls[i]*=.1f/max;}
 }
}
}
