#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 // Unlike the short bake ray, this spans the complete small-angle motion range.
 // A separated sheet on the wrong side must not be mistaken for clearance.
 sealed class FlexRayTree {
  struct Node {internal Bounds bounds;internal int x0,x1,y0,y1,left,right;}
  readonly Vector3[] vertices;readonly List<Node> nodes=new List<Node>();
  internal FlexRayTree(Vector3[] positions,int firstRow=0){vertices=(Vector3[])positions.Clone();Build(0,128,firstRow,80);}
  int Build(int x0,int x1,int y0,int y1){int id=nodes.Count;var n=new Node{x0=x0,x1=x1,y0=y0,y1=y1,left=-1,right=-1};nodes.Add(n);if(x1-x0>4||y1-y0>4){if(x1-x0>=y1-y0){int mid=(x0+x1)/2;n.left=Build(x0,mid,y0,y1);n.right=Build(mid,x1,y0,y1);}else{int mid=(y0+y1)/2;n.left=Build(x0,x1,y0,mid);n.right=Build(x0,x1,mid,y1);}n.bounds=nodes[n.left].bounds;n.bounds.Encapsulate(nodes[n.right].bounds);}else{n.bounds=new Bounds(vertices[y0*129+x0],Vector3.zero);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)n.bounds.Encapsulate(vertices[y*129+x]);}n.bounds.Expand(.00001f);nodes[id]=n;return id;}
  internal void Update(Vector3[] positions){System.Array.Copy(positions,vertices,vertices.Length);for(int i=nodes.Count-1;i>=0;i--){var n=nodes[i];if(n.left>=0){n.bounds=nodes[n.left].bounds;n.bounds.Encapsulate(nodes[n.right].bounds);}else{n.bounds=new Bounds(vertices[n.y0*129+n.x0],Vector3.zero);for(int y=n.y0;y<=n.y1;y++)for(int x=n.x0;x<=n.x1;x++)n.bounds.Encapsulate(vertices[y*129+x]);}n.bounds.Expand(.00001f);nodes[i]=n;}}
  internal float OutsideDistance(Vector3 point,Vector3 direction)=>OutsideDistance(point,direction,out _);
  internal float OutsideDistance(Vector3 point,Vector3 direction,out float alignment){var ray=new Ray(point-direction*2,direction);float furthest=float.NegativeInfinity;alignment=1;Hit(0,ray,ref furthest,ref alignment);return furthest-2;}
  void Hit(int id,Ray ray,ref float furthest,ref float alignment){var n=nodes[id];if(!n.bounds.IntersectRay(ray,out float near)||near>4)return;if(n.left>=0){Hit(n.left,ray,ref furthest,ref alignment);Hit(n.right,ray,ref furthest,ref alignment);return;}for(int y=n.y0;y<n.y1;y++)for(int x=n.x0;x<n.x1;x++){int a=y*129+x,b=a+129,c=a+1;Triangle(ray,vertices[a],vertices[b],vertices[c],ref furthest,ref alignment);Triangle(ray,vertices[c],vertices[b],vertices[b+1],ref furthest,ref alignment);}}
  static void Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,ref float furthest,ref float alignment){var e=b-a;var f=c-a;var h=Vector3.Cross(ray.direction,f);float det=Vector3.Dot(e,h);if(Mathf.Abs(det)<1e-10f)return;float inv=1/det;var s=ray.origin-a;float u=Vector3.Dot(s,h)*inv;if(u<0||u>1)return;var q=Vector3.Cross(s,e);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)return;float t=Vector3.Dot(f,q)*inv;if(t>=0&&t<=4&&t>=furthest){furthest=t;var normal=Vector3.Cross(e,f);float length=normal.magnitude;alignment=length>1e-12f?Mathf.Abs(Vector3.Dot(ray.direction,normal)/length):1;}}
  internal float MinimumDistance(FlexRayTree other){float best=float.PositiveInfinity;Distance(0,other,0,ref best);return Mathf.Sqrt(best);}
  static float BoxDistance(Bounds a,Bounds b){float x=Mathf.Max(0,Mathf.Max(a.min.x-b.max.x,b.min.x-a.max.x)),y=Mathf.Max(0,Mathf.Max(a.min.y-b.max.y,b.min.y-a.max.y)),z=Mathf.Max(0,Mathf.Max(a.min.z-b.max.z,b.min.z-a.max.z));return x*x+y*y+z*z;}
  void Distance(int index,FlexRayTree other,int second,ref float best){
   var a=nodes[index];var b=other.nodes[second];if(BoxDistance(a.bounds,b.bounds)>=best)return;
   if(a.left>=0&&(b.left<0||a.bounds.size.sqrMagnitude>=b.bounds.size.sqrMagnitude)){int first=a.left,last=a.right;if(BoxDistance(nodes[last].bounds,b.bounds)<BoxDistance(nodes[first].bounds,b.bounds)){first=a.right;last=a.left;}Distance(first,other,second,ref best);Distance(last,other,second,ref best);return;}
   if(b.left>=0){int first=b.left,last=b.right;if(BoxDistance(a.bounds,other.nodes[last].bounds)<BoxDistance(a.bounds,other.nodes[first].bounds)){first=b.right;last=b.left;}Distance(index,other,first,ref best);Distance(index,other,last,ref best);return;}
   for(int y=a.y0;y<a.y1;y++)for(int x=a.x0;x<a.x1;x++)for(int side=0;side<2;side++){int i=y*129+x,j=i+129,k=i+1;if(side==1){i=k;k=j+1;}for(int yy=b.y0;yy<b.y1;yy++)for(int xx=b.x0;xx<b.x1;xx++)for(int ss=0;ss<2;ss++){int p=yy*129+xx,q=p+129,r=p+1;if(ss==1){p=r;r=q+1;}best=Mathf.Min(best,TriangleDistance(vertices[i],vertices[j],vertices[k],other.vertices[p],other.vertices[q],other.vertices[r]));}}
  }
  static float TriangleDistance(Vector3 a,Vector3 b,Vector3 c,Vector3 p,Vector3 q,Vector3 r){
   if(FlowerSurfaceCollision.Triangle(a,b,c,p,q,r,out _))return 0;float best=Mathf.Min(PointTriangle(a,p,q,r),Mathf.Min(PointTriangle(b,p,q,r),PointTriangle(c,p,q,r)));best=Mathf.Min(best,Mathf.Min(PointTriangle(p,a,b,c),Mathf.Min(PointTriangle(q,a,b,c),PointTriangle(r,a,b,c))));
   best=Mathf.Min(best,Mathf.Min(Segments(a,b,p,q),Mathf.Min(Segments(a,b,q,r),Segments(a,b,r,p))));best=Mathf.Min(best,Mathf.Min(Segments(b,c,p,q),Mathf.Min(Segments(b,c,q,r),Segments(b,c,r,p))));return Mathf.Min(best,Mathf.Min(Segments(c,a,p,q),Mathf.Min(Segments(c,a,q,r),Segments(c,a,r,p))));
  }
  static float PointTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
   var ab=b-a;var ac=c-a;if(Vector3.Cross(ab,ac).sqrMagnitude<1e-20f)return Mathf.Min(Segments(p,p,a,b),Mathf.Min(Segments(p,p,b,c),Segments(p,p,c,a)));var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return ap.sqrMagnitude;var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.sqrMagnitude;float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return (p-(a+ab*(d1/(d1-d3)))).sqrMagnitude;var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.sqrMagnitude;float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return (p-(a+ac*(d2/(d2-d6)))).sqrMagnitude;float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return (p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).sqrMagnitude;float den=va+vb+vc;if(Mathf.Abs(den)<1e-20f)return Mathf.Min(Segments(p,p,a,b),Mathf.Min(Segments(p,p,b,c),Segments(p,p,c,a)));return (p-(a+ab*(vb/den)+ac*(vc/den))).sqrMagnitude;
  }
  static float Segments(Vector3 a,Vector3 b,Vector3 p,Vector3 q){
   var d=b-a;var e=q-p;var r=a-p;float aa=Vector3.Dot(d,d),ee=Vector3.Dot(e,e),f=Vector3.Dot(e,r),s,t;if(aa<1e-20f&&ee<1e-20f)return r.sqrMagnitude;if(aa<1e-20f){s=0;t=Mathf.Clamp01(f/ee);}else{float c=Vector3.Dot(d,r);if(ee<1e-20f){t=0;s=Mathf.Clamp01(-c/aa);}else{float bb=Vector3.Dot(d,e),den=aa*ee-bb*bb;s=den>1e-20f?Mathf.Clamp01((bb*f-c*ee)/den):0;t=(bb*s+f)/ee;if(t<0){t=0;s=Mathf.Clamp01(-c/aa);}else if(t>1){t=1;s=Mathf.Clamp01((bb-c)/aa);}}}return (a+d*s-p-e*t).sqrMagnitude;
  }

 }
}
}
#endif
