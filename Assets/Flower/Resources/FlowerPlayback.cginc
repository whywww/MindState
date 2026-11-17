#ifndef MINDSTATE_FLOWER_PLAYBACK
#define MINDSTATE_FLOWER_PLAYBACK
struct FlowerVertex { float3 p; float3 n; float4 t; };
float4 _FlowerFrame;
float _FlowerPlayback;
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
#define FLOWER_STRUCTURED_PLAYBACK
StructuredBuffer<FlowerVertex> _FlowerPoses;
FlowerVertex SampleFlower(uint id) {
 FlowerVertex a=_FlowerPoses[(uint)_FlowerFrame.x*(uint)_FlowerFrame.w+id],b=_FlowerPoses[(uint)_FlowerFrame.y*(uint)_FlowerFrame.w+id],v;
 v.p=lerp(a.p,b.p,_FlowerFrame.z);
 v.n=lerp(a.n,b.n,_FlowerFrame.z);float nl=length(v.n);v.n=nl>1e-5?v.n/nl:0;
 float3 t=lerp(a.t.xyz,b.t.xyz,_FlowerFrame.z);t-=v.n*dot(t,v.n);float tl=length(t);v.t=float4(tl>1e-5?t/tl:0,a.t.w);
 return v;
}
StructuredBuffer<float3> _FlexNormals;
StructuredBuffer<int> _FlexWall;
float _FlexWeight;
float _FlexControls[280];
int _FlexVertexCount;
float4 FlexCubic(float t){float q=1-t;return float4(q*q*q,3*t*t*t-6*t*t+4,-3*t*t*t+3*t*t+3*t+1,t*t*t)/6;}
float FlexHeight(uint id){
 uint x=id%129,y=id/129;float u=x/8.0+1,v=y/8.0+1;int ix=(int)u,iy=(int)v;float4 a=FlexCubic(frac(u)),b=FlexCubic(frac(v));float h=0;
 [unroll] for(int j=0;j<4;j++)[unroll] for(int i=0;i<4;i++)h+=_FlexControls[(iy+j-1)*20+ix+i-1]*a[i]*b[j];
 float fade=saturate((y-8.0)/12.0);h*=fade*fade*(3-2*fade)*_FlexWeight;return abs(h)<1e-7?0:h;
}
uint FlexSource(uint id){return id<20898?id%10449:(uint)_FlexWall[id-20898]%10449;}
float3 FlexPosition(uint id){uint source=FlexSource(id);return SampleFlower(id).p+_FlexNormals[source]*FlexHeight(source);}
void FlexSurface(uint source,out float3 normal,out float3 tangent){
 uint x=source%129,y=source/129;uint l=y*129+(x>0?x-1:0),r=y*129+min(x+1,128),b=(y>0?y-1:0)*129+x,t=min(y+1,80)*129+x;
 float3 across=FlexPosition(r)-FlexPosition(l),along=FlexPosition(t)-FlexPosition(b);tangent=normalize(across);normal=normalize(cross(along,tangent));
}
FlowerVertex SampleFlexibleFlower(uint id){
 FlowerVertex v=SampleFlower(id);if(_FlexWeight<=0)return v;uint source=FlexSource(id);float h=FlexHeight(source);v.p+=_FlexNormals[source]*h;
 uint x=source%129,y=source/129,l=y*129+(x>0?x-1:0),r=y*129+min(x+1,128),b=(y>0?y-1:0)*129+x,t=min(y+1,80)*129+x;
 if(h==0&&FlexHeight(l)==0&&FlexHeight(r)==0&&FlexHeight(b)==0&&FlexHeight(t)==0)return v;
 float3 normal,tangent;FlexSurface(source,normal,tangent);
 if(id>=20898){uint k=20898+((id-20898)/2)*2;uint previous=k==20898?_FlexVertexCount-2:k-2,next=k+2>=_FlexVertexCount?20898:k+2;float3 edge=normalize(FlexPosition(next)-FlexPosition(previous));v.n=normalize(cross(edge,-normal));v.t.xyz=edge;}
 else {v.n=id<10449?normal:-normal;v.t.xyz=tangent;}
 return v;
}
#endif
#endif
