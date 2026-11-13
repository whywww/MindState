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
#endif
#endif
