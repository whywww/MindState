using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MindState.Editor
{
    public static class CreateGallery
    {
        const string Root="Assets/HeroPetal";
        [MenuItem("MindState/Rebuild Gallery Assets")]
        public static void Create()
        {
            Directory.CreateDirectory(Root+"/Art"); Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Prefabs");
            AssetDatabase.Refresh();
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.companyName="MindState Studio"; PlayerSettings.productName="MindState — Pearlescent Study";
            PlayerSettings.defaultScreenWidth=1920; PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;
            PlayerSettings.enableFrameTimingStats=true;
            GraphicsSettings.renderPipelineAsset=null;
            var graphicsSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            graphicsSettings.FindProperty("m_InstancingStripping").intValue=0;
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<QualitySettings.names.Length;i++) QualitySettings.SetQualityLevel(i,false);
            QualitySettings.renderPipeline=null; QualitySettings.antiAliasing=4;
            QualitySettings.pixelLightCount=4; QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh; QualitySettings.shadowDistance=20;
            QualitySettings.vSyncCount=0;
            MakeTextures();
            var pearl=LoadOrCreateMaterial("Pearl",Shader.Find("MindState/Radial Pearl Silk"));
            pearl.SetColor("_Color",new Color(0.87f,0.88f,0.94f));
            pearl.SetColor("_RootColor",new Color(0.60f,0.48f,0.70f));
            pearl.SetFloat("_Smoothness",0.38f);
            pearl.SetFloat("_BumpStrength",0.18f);
            pearl.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/RadialSilkNormal.png"));
            pearl.EnableKeyword("_NORMALMAP");
            foreach(string asset in new[]{"Champagne.mat","GoldBarb.asset","FilamentDetail.png","FilamentNormal.png"})
                AssetDatabase.DeleteAsset(Root+"/Art/"+asset);
            Cubemap environment=CreateEnvironment();
            AssetDatabase.CreateAsset(environment,UniqueReplace(Root+"/Art/StudioReflection.asset"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.36f,0.36f,0.46f);
            RenderSettings.ambientEquatorColor=new Color(0.24f,0.23f,0.32f);
            RenderSettings.ambientGroundColor=new Color(0.12f,0.10f,0.17f);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture=environment; RenderSettings.reflectionIntensity=0.8f;
            var art=new GameObject("Hero Petal — Radial Silk");
            art.AddComponent<MeshFilter>(); art.AddComponent<MeshRenderer>();
            var petal=art.AddComponent<HeroPetal>(); petal.pearlMaterial=pearl;
            PrefabUtility.SaveAsPrefabAssetAndConnect(art,Root+"/Prefabs/HeroPetal.prefab",InteractionMode.AutomatedAction);
            Light("01 · warm silk key",new Vector3(35,-35,0),new Color(1,0.96f,0.98f),1.25f,true);
            Light("02 · cool gallery fill",new Vector3(16,145,0),new Color(0.82f,0.78f,1),0.75f,false);
            Light("03 · cool silk rim",new Vector3(145,30,0),new Color(0.78f,0.86f,1),0.95f,false);
            var cameraObject=new GameObject("Gallery Camera"); cameraObject.tag="MainCamera";
            var cam=cameraObject.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(0.025f,0.04f,0.13f); cam.fieldOfView=37;
            cam.nearClipPlane=0.05f; cam.farClipPlane=50; cam.allowHDR=true; cam.allowMSAA=true; cam.renderingPath=RenderingPath.Forward;
            var post=cameraObject.AddComponent<GalleryPost>(); post.shader=Shader.Find("Hidden/MindState/GalleryPost");
            var gallery=cameraObject.AddComponent<GalleryController>(); gallery.petal=petal; gallery.post=post;
            cameraObject.transform.position=new Vector3(-1.2f,1.2f,-6.5f); cameraObject.transform.LookAt(new Vector3(-0.5f,0,-0.25f));
            string scenePath=Root+"/Scenes/HeroPetalGallery.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("MINDSTATE_ASSETS_READY "+Application.unityVersion);
        }
        static string UniqueReplace(string path) { AssetDatabase.DeleteAsset(path); return path; }
        static Material LoadOrCreateMaterial(string name,Shader shader)
        {
            if(shader==null) throw new InvalidOperationException("Missing shader "+name);
            string path=Root+"/Art/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(shader); AssetDatabase.CreateAsset(m,path); }
            else
            {
                var clean=new Material(shader) { name=name };
                EditorUtility.CopySerialized(clean,m);
                UnityEngine.Object.DestroyImmediate(clean);
            }
            m.shader=shader; return m;
        }
        static void Light(string name,Vector3 rotation,Color color,float intensity,bool shadows)
        {
            var go=new GameObject(name); go.transform.eulerAngles=rotation;
            var light=go.AddComponent<Light>(); light.type=LightType.Directional; light.color=color; light.intensity=intensity;
            light.shadows=shadows?LightShadows.Soft:LightShadows.None; light.shadowStrength=0.18f;
            light.shadowBias=0.015f; light.shadowNormalBias=0.02f;
        }
        static void MakeTextures()
        {
            const int size=1024;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=(x+0.5f)/size,v=(y+0.5f)/size;
                float phase=u*Mathf.PI*2+0.08f*Mathf.Sin(v*6.2f);
                Vector3 n=new Vector3(-0.48f*Mathf.Cos(phase),0.012f*Mathf.Cos(v*6.2f)*Mathf.Cos(phase),1).normalized;
                pixels[y*size+x]=new Color(n.x*0.5f+0.5f,n.y*0.5f+0.5f,n.z*0.5f+0.5f,1);
            }
            string path=Root+"/Art/RadialSilkNormal.png";
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.NormalMap; importer.sRGBTexture=false;
            importer.mipmapEnabled=true; importer.filterMode=FilterMode.Trilinear; importer.anisoLevel=8;
            importer.wrapModeU=TextureWrapMode.Repeat; importer.wrapModeV=TextureWrapMode.Clamp;
            importer.maxTextureSize=size; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        static Cubemap CreateEnvironment()
        {
            const int size=128;
            var cube=new Cubemap(size,TextureFormat.RGBAHalf,true); cube.name="Softbox gallery environment";
            Vector3[] keys={new Vector3(-1,1,-1).normalized,new Vector3(1,0.4f,0.2f).normalized,new Vector3(0,1,1).normalized};
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                {
                    float u=(x+0.5f)/size*2-1,v=(y+0.5f)/size*2-1;
                    Vector3 d;
                    switch(face) { case 0:d=new Vector3(1,-v,-u);break;case 1:d=new Vector3(-1,-v,u);break;case 2:d=new Vector3(u,1,v);break;case 3:d=new Vector3(u,-1,-v);break;case 4:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break; }
                    d.Normalize();
                    Color c=new Color(0.08f,0.10f,0.12f)*Mathf.Lerp(0.4f,1,Mathf.Clamp01(d.y*0.5f+0.5f));
                    c+=new Color(1.4f,1.2f,0.9f)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,keys[0])),18);
                    c+=new Color(0.4f,0.65f,0.9f)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,keys[1])),25);
                    c+=new Color(0.8f,0.65f,0.45f)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,keys[2])),35);
                    pixels[y*size+x]=c;
                }
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true,false); return cube;
        }
        [MenuItem("MindState/Build Windows Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/Windows/MindState.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Player build failed: "+report.summary.result);
            Debug.Log("MINDSTATE_BUILD_SUCCESS bytes="+report.summary.totalSize);
        }
    }
}
