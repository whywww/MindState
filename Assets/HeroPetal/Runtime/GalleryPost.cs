using UnityEngine;

namespace MindState
{
    [RequireComponent(typeof(Camera))]
    public sealed class GalleryPost : MonoBehaviour
    {
        public Shader shader;
        public bool bloom = false;
        [Range(0,1)] public float bloomStrength=0.14f;
        [Range(0.2f,3)] public float exposure=1.05f;
        Material material;
        void OnEnable() { if(shader!=null) material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave }; }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(material==null) { Graphics.Blit(source,destination); return; }
            RenderTexture a=null,b=null;
            if(bloom)
            {
                int w=Mathf.Max(1,source.width/4),h=Mathf.Max(1,source.height/4);
                a=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGBHalf);
                b=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGBHalf);
                Graphics.Blit(source,a,material,0);
                for(int i=0;i<2;i++)
                {
                    material.SetVector("_Direction",new Vector4(1,0,0,0)); Graphics.Blit(a,b,material,1);
                    material.SetVector("_Direction",new Vector4(0,1,0,0)); Graphics.Blit(b,a,material,1);
                }
            }
            material.SetTexture("_BloomTex",a!=null?(Texture)a:Texture2D.blackTexture);
            material.SetFloat("_Bloom",bloom?bloomStrength:0);
            material.SetFloat("_Exposure",exposure);
            Graphics.Blit(source,destination,material,2);
            if(a!=null) RenderTexture.ReleaseTemporary(a);
            if(b!=null) RenderTexture.ReleaseTemporary(b);
        }
        void OnDisable() { if(material!=null) DestroyImmediate(material); }
    }
}
