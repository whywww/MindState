#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
using UnityEngine;

namespace MindState
{
    public sealed partial class FlowerAssembler
    {
        [HideInInspector]
        public bool diagnosticColors,diagnosticWireframe,diagnosticLabels;
        [HideInInspector] public bool diagnosticExtremeVariation;
        public Material diagnosticMaterial;
        public struct DiagnosticPetal
        {
            public int layer,index;
            public string id;
            public Transform anchor,body;
            public MeshRenderer renderer;
        }
        public DiagnosticPetal[] GetDiagnosticPetals()
        {
            var result=new DiagnosticPetal[PetalCount]; int k=0;
            for(int i=0;i<layers.Length;i++) for(int j=0;j<layers[i].petals.Length;j++)
            {
                var p=layers[i].petals[j];
                result[k++]=new DiagnosticPetal { layer=i,index=j,id="L"+i+"-P"+j,anchor=p.anchor,body=p.body,renderer=p.renderer };
            }
            return result;
        }
        public static Color DiagnosticColor(int layer,int index)=>Color.HSVToRGB(Mathf.Repeat((layer*7+index)*.618034f,1),.72f,1);
        public void ApplyDiagnosticColors()
        {
            if(layers==null) return;
            for(int i=0;i<layers.Length;i++) for(int j=0;j<layers[i].petals.Length;j++)
            {
                var l=layers[i]; var p=l.petals[j];
                p.renderer.sharedMaterial=diagnosticColors&&diagnosticMaterial!=null?diagnosticMaterial:petalPrefab.pearlMaterial;
                // Restore exactly the cached, seeded production tint without allocating a material.
                Color c=DiagnosticColor(i,j);
                p.block.SetColor("_Color",diagnosticColors?c:p.originalColor);
                p.block.SetColor("_RootColor",diagnosticColors?c:l.config.root);
                p.block.SetColor("_PinkColor",diagnosticColors?c:l.config.pink);
                p.renderer.SetPropertyBlock(p.block);
            }
        }
        public void SetPetalVisible(int layer,int index,bool visible)=>layers[layer].petals[index].renderer.enabled=visible;
        public void SoloPetal(int layer,int index)
        { for(int i=0;i<layers.Length;i++) for(int j=0;j<layers[i].petals.Length;j++) SetPetalVisible(i,j,i==layer&&j==index); }
        public void ShowAllPetals()
        { foreach(var l in layers) foreach(var p in l.petals) p.renderer.enabled=true; }

        // Read cached interpolation directly: validation/search performs no render-mesh upload.
        public void SampleDiagnosticSurface(int layer,float globalProgress,Vector3[] destination)
        { SampleDiagnosticSurface(layer,0,globalProgress,destination); }
        public void SampleDiagnosticSurface(int layer,int index,float globalProgress,Vector3[] destination)
        {
            var l=layers[layer]; var g=SurfaceFor(l,index); float p=Mathf.Clamp01((globalProgress*BloomDuration-l.config.delay)/l.config.duration);
            float frame=p*(PoseCount-1); int a=Mathf.Min((int)frame,PoseCount-2); float f=frame-a;
            int surface=(HeroPetal.WidthSegments+1)*(HeroPetal.LengthSegments+1);
            for(int k=0;k<surface;k++) destination[k]=(Vector3.LerpUnclamped(g.poses[a].data[k].p,g.poses[a+1].data[k].p,f)+Vector3.LerpUnclamped(g.poses[a].data[k+surface].p,g.poses[a+1].data[k+surface].p,f))*.5f;
            foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){float q=Mathf.Clamp01(globalProgress)*32;int a0=Mathf.Min((int)q,31);for(int k=0;k<patch.ids.Length;k++)destination[patch.ids[k]]+=Vector3.Lerp(patch.delta[a0][k],patch.delta[a0+1][k],q-a0);if(upperPreview&&patch.upper!=null)for(int k=0;k<patch.upperIds.Length;k++)destination[patch.upperIds[k]]+=Vector3.Lerp(patch.upper[a0][k],patch.upper[a0+1][k],q-a0);if(extraPreview&&patch.extra!=null)for(int k=0;k<patch.extraIds.Length;k++)destination[patch.extraIds[k]]+=Vector3.Lerp(patch.extra[a0][k],patch.extra[a0+1][k],q-a0);}

        }
        public Matrix4x4 DiagnosticMatrix(int layer,int index,float globalProgress,FlowerLayer overrideLayout=null)
        {
            var l=layers[layer]; var g=SurfaceFor(l,index); var c=overrideLayout??l.config; var p=l.petals[index];
            float progress=Mathf.Clamp01((globalProgress*BloomDuration-l.config.delay)/l.config.duration);
            float frame=progress*(PoseCount-1); int a=Mathf.Min((int)frame,PoseCount-2); float f=frame-a;
            Vector3 root=Vector3.LerpUnclamped(g.poses[a].root,g.poses[a+1].root,f);
            float tilt=Mathf.Lerp(c.closedTilt,c.openTilt,Mathf.SmoothStep(0,1,progress));
            var offset=c.OffsetAt(index);
            PetalFrame(c,p.azimuth+c.azimuth-l.config.azimuth,tilt+p.tilt+(offset?.tilt??0)*c.OffsetWeight(progress),p.roll,(offset?.height??0)*c.OffsetWeight(progress),index,l.petals.Length,progress,out var position,out var rotation);
            if(!rootBaseline) position.y=layers[0].config.rootHeight+.0045f+c.rootStagger*RootStaggerSign(index,l.petals.Length);
            var scale=PetalScale(c,p,index,progress);
            return transform.localToWorldMatrix*Matrix4x4.TRS(position,rotation,Vector3.one)*Matrix4x4.TRS(-Vector3.Scale(root,scale),Quaternion.identity,scale);
        }
    }
}
#endif
