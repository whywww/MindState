#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MindState
{
    // Offline only. Bounds always enclose ALL render-surface samples, including when the
    // leaf triangulation is coarse. Exact mode descends to original single-cell triangles.
    public sealed class FlowerSurfaceCollision
    {
        const int Cols=HeroPetal.WidthSegments+1,Rows=HeroPetal.LengthSegments+1,Count=Cols*Rows;
        struct Node { public int x0,x1,y0,y1,left,right; }
        sealed class Surface
        {
            public Vector3[] v=new Vector3[Count];
            public Bounds[] bounds;
            public Surface(int nodes) { bounds=new Bounds[nodes]; }
        }
        public struct Contact
        {
            public int petalA,petalB;
            public Vector3 point;
            public float lengthA,lengthB;
            public bool rootRegion;
            public float widthA,widthB;
        }
        readonly Node[] nodes;
        readonly Vector3[][] local;
        readonly Surface[] instances;
        readonly bool[] repeatedSurface;
        readonly FlowerAssembler flower;
        readonly FlowerAssembler.DiagnosticPetal[] petals;
        public FlowerAssembler.DiagnosticPetal[] Petals=>petals;
        public float MaxProxyError { get; private set; }
        public bool NearCandidates { get; private set; }
        public FlowerSurfaceCollision(FlowerAssembler flower,int cellStep=1)
        {
            this.flower=flower; petals=flower.GetDiagnosticPetals();
            repeatedSurface=new bool[petals.Length];
            for(int i=0;i<petals.Length;i++) for(int j=0;j<i;j++)
                if(flower.PetalMesh(petals[i].layer,petals[i].index)==flower.PetalMesh(petals[j].layer,petals[j].index)) repeatedSurface[i]=true;
            var list=new List<Node>(); Build(list,0,Cols-1,0,Rows-1,Mathf.Max(1,cellStep)); nodes=list.ToArray();
            local=new Vector3[petals.Length][]; for(int i=0;i<local.Length;i++) local[i]=new Vector3[Count];
            instances=new Surface[petals.Length]; for(int i=0;i<instances.Length;i++) instances[i]=new Surface(nodes.Length);
        }
        // Vertical ray against original projected triangles; returns the visible upper sheet.
        bool Height(int petal,int node,float x,float z,ref float height)
        {
            var bounds=instances[petal].bounds[node];
            if(x<bounds.min.x||x>bounds.max.x||z<bounds.min.z||z>bounds.max.z) return false;
            var n=nodes[node]; bool found=false;
            if(n.left>=0) { found=Height(petal,n.left,x,z,ref height); return Height(petal,n.right,x,z,ref height)||found; }
            var v=instances[petal].v;
            for(int y=n.y0;y<n.y1;y++) for(int u=n.x0;u<n.x1;u++)
            {
                int a=y*Cols+u,c=a+Cols;
                found=HeightTriangle(v[a],v[c],v[a+1],x,z,ref height)||found;
                found=HeightTriangle(v[a+1],v[c],v[c+1],x,z,ref height)||found;
            }
            return found;
        }
        static bool HeightTriangle(Vector3 a,Vector3 b,Vector3 c,float x,float z,ref float height)
        {
            float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Mathf.Abs(det)<1e-12f) return false;
            float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;
            float w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;
            if(u<0||w<0||u+w>1) return false;
            height=Mathf.Max(height,u*a.y+w*b.y+(1-u-w)*c.y); return true;
        }
        public void NeighborCoverage(int a,int b,int resolution,out int above,out int below,out int touching)
        {
            above=below=touching=0;
            var aa=instances[a].bounds[0]; var bb=instances[b].bounds[0];
            float x0=Mathf.Max(aa.min.x,bb.min.x),x1=Mathf.Min(aa.max.x,bb.max.x);
            float z0=Mathf.Max(aa.min.z,bb.min.z),z1=Mathf.Min(aa.max.z,bb.max.z);
            if(x1<=x0||z1<=z0) return;
            for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
            {
                float px=Mathf.Lerp(x0,x1,(x+.5f)/resolution),pz=Mathf.Lerp(z0,z1,(y+.5f)/resolution);
                // Root attachment is reported separately by the intersection scanner.
                if((new Vector2(px-flower.transform.position.x,pz-flower.transform.position.z)).magnitude<.25f) continue;
                float ha=float.NegativeInfinity,hb=ha;
                if(!Height(a,0,px,pz,ref ha)||!Height(b,0,px,pz,ref hb)) continue;
                if(ha-hb>.001f) above++; else if(hb-ha>.001f) below++; else touching++;
            }
        }
        static int Build(List<Node> list,int x0,int x1,int y0,int y1,int step)
        {
            int index=list.Count; var n=new Node { x0=x0,x1=x1,y0=y0,y1=y1,left=-1,right=-1 }; list.Add(n);
            if(x1-x0>step||y1-y0>step)
            {
                if(x1-x0>=y1-y0) { int m=(x0+x1)/2; n.left=Build(list,x0,m,y0,y1,step); n.right=Build(list,m,x1,y0,y1,step); }
                else { int m=(y0+y1)/2; n.left=Build(list,x0,x1,y0,m,step); n.right=Build(list,x0,x1,m,y1,step); }
                list[index]=n;
            }
            return index;
        }
        public void Sample(float progress,FlowerLayer[] layout=null,int focusLayer=-1)
        {
            for(int p=0;p<petals.Length;p++) if(focusLayer<0||petals[p].layer==focusLayer)
                flower.SampleDiagnosticSurface(petals[p].layer,petals[p].index,progress,local[p]);
            for(int p=0;p<instances.Length;p++)
            {
                var s=instances[p]; int layer=petals[p].layer;
                if(focusLayer>=0&&layer!=focusLayer) continue;
                var matrix=flower.DiagnosticMatrix(layer,petals[p].index,progress,layout==null?null:layout[layer]);
                for(int k=0;k<Count;k++) s.v[k]=matrix.MultiplyPoint3x4(local[p][k]);
                for(int k=nodes.Length-1;k>=0;k--)
                {
                    var n=nodes[k]; Bounds b;
                    if(n.left>=0) { b=s.bounds[n.left]; b.Encapsulate(s.bounds[n.right]); }
                    else
                    {
                        b=new Bounds(s.v[n.y0*Cols+n.x0],Vector3.zero);
                        for(int y=n.y0;y<=n.y1;y++) for(int x=n.x0;x<=n.x1;x++) b.Encapsulate(s.v[y*Cols+x]);
                        // Error against the coarse two-triangle patch in world space.
                        if(n.x1-n.x0>1||n.y1-n.y0>1) for(int y=n.y0;y<=n.y1;y++) for(int x=n.x0;x<=n.x1;x++)
                        {
                            float u=(x-n.x0)/(float)(n.x1-n.x0),v=(y-n.y0)/(float)(n.y1-n.y0);
                            Vector3 a=s.v[n.y0*Cols+n.x0],bb=s.v[n.y0*Cols+n.x1],c=s.v[n.y1*Cols+n.x0],d=s.v[n.y1*Cols+n.x1];
                            Vector3 approximate=u+v<=1?a+(bb-a)*u+(c-a)*v:d+(c-d)*(1-u)+(bb-d)*(1-v);
                            MaxProxyError=Mathf.Max(MaxProxyError,Vector3.Distance(s.v[y*Cols+x],approximate));
                        }
                    }
                    b.Expand(.000004f); s.bounds[k]=b;
                }
            }
        }
        // One representative non-root hit AND one root hit per pair. Never lets a root hit
        // hide a second intersection at the rim. Root hits remain failures until reviewed.
        public void FindContacts(List<Contact> result,bool self=true,int focusLayer=-1,bool parallel=false)
        {
            result.Clear(); NearCandidates=false;
            Action<int> inspect=a=>
            {
            for(int b=self?a:a+1;b<instances.Length;b++)
            {
                // An invertible instance transform cannot change self-intersection. All
                // petals of a layer share a surface, so inspect it once, not 14 times.
                if(a==b&&repeatedSurface[a]) continue;
                if(focusLayer>=0&&(petals[a].layer!=focusLayer||petals[b].layer!=focusLayer)) continue;
                Bounds nearby=instances[a].bounds[0]; nearby.Expand(.02f);
                if(a!=b&&nearby.Intersects(instances[b].bounds[0])) NearCandidates=true;
                if(!instances[a].bounds[0].Intersects(instances[b].bounds[0])) continue;
                if(Cross(a,0,b,0,false,out Contact hit)) { hit.petalA=a; hit.petalB=b; lock(result) result.Add(hit); }
                if(Cross(a,0,b,0,true,out hit)) { hit.petalA=a; hit.petalB=b; lock(result) result.Add(hit); }
            }
            };
            // Workers read only the already-sampled value-type geometry. No Transform,
            // Mesh, Renderer or other Unity object is accessed from a worker thread.
            if(parallel) Parallel.For(0,instances.Length,new ParallelOptions { MaxDegreeOfParallelism=4 },inspect);
            else for(int a=0;a<instances.Length;a++) inspect(a);
            result.Sort((a,b)=>a.petalA!=b.petalA?a.petalA.CompareTo(b.petalA):a.petalB!=b.petalB?a.petalB.CompareTo(b.petalB):a.rootRegion.CompareTo(b.rootRegion));
        }
        bool Cross(int a,int ia,int b,int ib,bool roots,out Contact contact)
        {
            contact=default; var sa=instances[a]; var sb=instances[b];
            if(!sa.bounds[ia].Intersects(sb.bounds[ib])) return false;
            var na=nodes[ia]; var nb=nodes[ib];
            if(!roots&&na.y1<=6&&nb.y1<=6) return false;
            if(roots&&(na.y0>7||nb.y0>7)) return false;
            if(a==b&&ia==ib&&na.left>=0)
                return Cross(a,na.left,b,na.left,roots,out contact)||Cross(a,na.left,b,na.right,roots,out contact)||Cross(a,na.right,b,na.right,roots,out contact);
            if(na.left>=0&&(nb.left<0||sa.bounds[ia].size.sqrMagnitude>=sb.bounds[ib].size.sqrMagnitude))
                return Cross(a,na.right,b,ib,roots,out contact)||Cross(a,na.left,b,ib,roots,out contact);
            if(nb.left>=0) return Cross(a,ia,b,nb.right,roots,out contact)||Cross(a,ia,b,nb.left,roots,out contact);
            for(int ta=0;ta<2;ta++) for(int tb=0;tb<2;tb++)
            {
                int a0=na.y0*Cols+na.x0,a1=na.y1*Cols+na.x0,a2=na.y0*Cols+na.x1;
                if(ta==1) { a0=a2; a2=na.y1*Cols+na.x1; }
                int b0=nb.y0*Cols+nb.x0,b1=nb.y1*Cols+nb.x0,b2=nb.y0*Cols+nb.x1;
                if(tb==1) { b0=b2; b2=nb.y1*Cols+nb.x1; }
                if(a==b&&(ia==ib||Adjacent(a0,a1,a2,b0,b1,b2))) continue;
                if(!Triangle(sa.v[a0],sa.v[a1],sa.v[a2],sb.v[b0],sb.v[b1],sb.v[b2],out Vector3 point)) continue;
                float la=LengthAt(point,sa.v[a0],sa.v[a1],sa.v[a2],a0,a1,a2),lb=LengthAt(point,sb.v[b0],sb.v[b1],sb.v[b2],b0,b1,b2);
                float wa=WidthAt(point,sa.v[a0],sa.v[a1],sa.v[a2],a0,a1,a2),wb=WidthAt(point,sb.v[b0],sb.v[b1],sb.v[b2],b0,b1,b2);
                bool root=IsRoot(la,lb); if(root!=roots) continue;
                contact=new Contact { point=point,lengthA=la,lengthB=lb,widthA=wa,widthB=wb,rootRegion=root }; return true;
            }
            return false;
        }
        public static bool IsRoot(float a,float b)=>a<=.08f&&b<=.08f;
        public static bool Adjacent(int a,int b,int c,int d,int e,int f)=>a==d||a==e||a==f||b==d||b==e||b==f||c==d||c==e||c==f;
        static float LengthAt(Vector3 p,Vector3 a,Vector3 b,Vector3 c,int ia,int ib,int ic)
        {
            Vector3 u=b-a,v=c-a,w=p-a; float uu=Vector3.Dot(u,u),uv=Vector3.Dot(u,v),vv=Vector3.Dot(v,v),wu=Vector3.Dot(w,u),wv=Vector3.Dot(w,v),den=uu*vv-uv*uv;
            if(Mathf.Abs(den)<1e-20f) return ia/Cols/(float)(Rows-1);
            float s=(vv*wu-uv*wv)/den,t=(uu*wv-uv*wu)/den;
            return ((ia/Cols)*(1-s-t)+(ib/Cols)*s+(ic/Cols)*t)/(Rows-1);
        }
        static float WidthAt(Vector3 p,Vector3 a,Vector3 b,Vector3 c,int ia,int ib,int ic)
        {
            Vector3 u=b-a,v=c-a,w=p-a; float uu=Vector3.Dot(u,u),uv=Vector3.Dot(u,v),vv=Vector3.Dot(v,v),wu=Vector3.Dot(w,u),wv=Vector3.Dot(w,v),den=uu*vv-uv*uv;
            if(Mathf.Abs(den)<1e-20f) return (ia%Cols)/(float)(Cols-1)*2-1;
            float s=(vv*wu-uv*wv)/den,t=(uu*wv-uv*wu)/den;
            return (((ia%Cols)*(1-s-t)+(ib%Cols)*s+(ic%Cols)*t)/(Cols-1))*2-1;
        }
        public static bool Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 e,Vector3 f,out Vector3 point)
        {
            point=default;
            Vector3 minA=Vector3.Min(a,Vector3.Min(b,c)),maxA=Vector3.Max(a,Vector3.Max(b,c)),minB=Vector3.Min(d,Vector3.Min(e,f)),maxB=Vector3.Max(d,Vector3.Max(e,f));
            if(minA.x>maxB.x+1e-6f||minB.x>maxA.x+1e-6f||minA.y>maxB.y+1e-6f||minB.y>maxA.y+1e-6f||minA.z>maxB.z+1e-6f||minB.z>maxA.z+1e-6f) return false;
            Vector3 na=Vector3.Cross(b-a,c-a),nb=Vector3.Cross(e-d,f-d); if(na.sqrMagnitude<1e-22f||nb.sqrMagnitude<1e-22f) return false;
            na.Normalize(); nb.Normalize();
            float da=Vector3.Dot(nb,a-d),db=Vector3.Dot(nb,b-d),dc=Vector3.Dot(nb,c-d),dd=Vector3.Dot(na,d-a),de=Vector3.Dot(na,e-a),df=Vector3.Dot(na,f-a);
            if(Mathf.Min(da,Mathf.Min(db,dc))>1e-6f||Mathf.Max(da,Mathf.Max(db,dc))<-1e-6f||Mathf.Min(dd,Mathf.Min(de,df))>1e-6f||Mathf.Max(dd,Mathf.Max(de,df))<-1e-6f) return false;
            if(Mathf.Max(Mathf.Abs(da),Mathf.Max(Mathf.Abs(db),Mathf.Abs(dc)))<1e-6f&&Mathf.Max(Mathf.Abs(dd),Mathf.Max(Mathf.Abs(de),Mathf.Abs(df)))<1e-6f)
                return Coplanar(a,b,c,d,e,f,na,out point);
            bool found=false; Vector3 first=default;
            for(int pass=0;pass<2;pass++) for(int edge=0;edge<3;edge++)
            {
                Vector3 p0=pass==0?a:d,p1=pass==0?b:e,p2=pass==0?c:f,q0=pass==0?d:a,q1=pass==0?e:b,q2=pass==0?f:c;
                Vector3 p=edge==0?p0:edge==1?p1:p2,q=edge==0?p1:edge==1?p2:p0;
                if(!Segment(p,q,q0,q1,q2,out Vector3 hit)) continue;
                if(!found) { found=true; first=hit; }
                else if((first-hit).sqrMagnitude>1e-10f) { point=(first+hit)*.5f; return true; }
            }
            return false;
        }
        static bool Segment(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c,out Vector3 hit)
        {
            hit=default; Vector3 dir=q-p,u=b-a,v=c-a,h=Vector3.Cross(dir,v); float det=Vector3.Dot(u,h);
            if(Mathf.Abs(det)<1e-14f) return false;
            Vector3 s=p-a; float x=Vector3.Dot(s,h)/det; if(x<-1e-6f||x>1+1e-6f) return false;
            Vector3 k=Vector3.Cross(s,u); float y=Vector3.Dot(dir,k)/det; if(y<-1e-6f||x+y>1+1e-6f) return false;
            float t=Vector3.Dot(v,k)/det; if(t<-1e-6f||t>1+1e-6f) return false;
            hit=p+dir*t; return true;
        }
        static Vector2 Project(Vector3 p,int axis)=>axis==0?new Vector2(p.y,p.z):axis==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);
        static float Cross2(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Coplanar(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 e,Vector3 f,Vector3 normal,out Vector3 point)
        {
            int axis=Mathf.Abs(normal.x)>Mathf.Abs(normal.y)?0:1; if(Mathf.Abs(normal.z)>Mathf.Abs(normal[axis])) axis=2;
            var polygon=new List<Vector3> { a,b,c }; Vector3[] clip={d,e,f};
            float orientation=Mathf.Sign(Cross2(Project(e-d,axis),Project(f-d,axis)));
            for(int edge=0;edge<3&&polygon.Count>0;edge++)
            {
                var output=new List<Vector3>(); Vector2 p=Project(clip[edge],axis),q=Project(clip[(edge+1)%3],axis);
                Vector3 previous=polygon[polygon.Count-1]; float dp=Cross2(q-p,Project(previous,axis)-p)*orientation;
                foreach(var current in polygon)
                {
                    float dc=Cross2(q-p,Project(current,axis)-p)*orientation;
                    if((dc>=0)!=(dp>=0)) output.Add(Vector3.LerpUnclamped(previous,current,dp/(dp-dc)));
                    if(dc>=0) output.Add(current); previous=current; dp=dc;
                }
                polygon=output;
            }
            point=default; if(polygon.Count<3) return false;
            float area=0; for(int i=1;i<polygon.Count-1;i++) area+=Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).magnitude;
            if(area<1e-10f) return false;
            foreach(var p in polygon) point+=p; point/=polygon.Count; return true;
        }
    }
}
#endif
