using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MindState.Editor
{
    public static class CreateFlowerGallery
    {
        const string Root="Assets/Flower";
        [MenuItem("MindState/Create Flower Gallery")]
        public static void Create()
        {
            var preset=AssetDatabase.LoadAssetAtPath<FlowerPreset>(Root+"/Art/ExhibitionPeony.asset");
            if(preset==null) throw new System.InvalidOperationException("Current ExhibitionPeony preset is missing.");
            var blush=Material("BlushHeart");
            var champagne=Material("ChampagneAnthers");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.32f,.32f,.36f); RenderSettings.ambientEquatorColor=new Color(.23f,.23f,.27f); RenderSettings.ambientGroundColor=new Color(.14f,.14f,.18f);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/HeroPetal/Art/StudioReflection.asset"); RenderSettings.reflectionIntensity=.65f;
            var go=new GameObject("Pearl Peony · 18 petals / 3 shared layers");
            var flower=go.AddComponent<FlowerAssembler>(); flower.preset=preset; flower.petalPrefab=AssetDatabase.LoadAssetAtPath<HeroPetal>("Assets/HeroPetal/Prefabs/HeroPetal.prefab"); flower.appearance=AssetDatabase.LoadAssetAtPath<FlowerAppearancePreset>(Root+"/Art/TargetAppearance.asset"); if(flower.appearance==null) throw new System.InvalidOperationException("Current TargetAppearance is missing."); flower.coreMaterial=blush; flower.stamenMaterial=champagne;
            PrefabUtility.SaveAsPrefabAssetAndConnect(go,Root+"/Prefabs/PearlPeony.prefab",InteractionMode.AutomatedAction);
            Light("01 · broad neutral key",new Vector3(38,-28,0),new Color(1,.98f,.95f),.40f,true);
            Light("02 · lavender fill",new Vector3(60,130,0),new Color(.86f,.92f,1),.12f,false);
            Light("03 · cool rim",new Vector3(120,20,0),new Color(.78f,.86f,1),.22f,false);
            var camera=new GameObject("Flower Gallery Camera"); camera.tag="MainCamera";
            var c=camera.AddComponent<Camera>(); c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=Color.black; c.fieldOfView=38; c.nearClipPlane=.05f; c.farClipPlane=40; c.allowHDR=true; c.allowMSAA=true; c.renderingPath=RenderingPath.Forward;
            var post=camera.AddComponent<GalleryPost>(); post.shader=Shader.Find("Hidden/MindState/GalleryPost"); post.bloom=false; post.exposure=.92f;
            var gallery=camera.AddComponent<FlowerGallery>(); gallery.flower=flower;
            var brainLink=camera.AddComponent<MindState.BCI.BrainLink.BrainLinkFlowerInput>(); brainLink.flower=flower; brainLink.enabled=false;
            camera.transform.position=new Vector3(1.96f,7.61f,-4.85f); camera.transform.LookAt(new Vector3(0,.65f,0));
            string scene=Root+"/Scenes/FlowerGallery.unity"; EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene(scene,true) };
            AssetDatabase.SaveAssets(); Debug.Log("MINDSTATE_FLOWER_ASSETS_READY");
        }
        static Material Material(string name)
        {
            string path=Root+"/Art/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) throw new System.InvalidOperationException("Current material is missing: "+path);
            return material;
        }
        static void Light(string name,Vector3 rotation,Color color,float intensity,bool shadow)
        {
            var go=new GameObject(name); go.transform.eulerAngles=rotation;
            var light=go.AddComponent<Light>(); light.type=LightType.Directional; light.color=color; light.intensity=intensity;
            light.shadows=shadow?LightShadows.Soft:LightShadows.None; light.shadowStrength=.30f; light.shadowBias=.008f; light.shadowNormalBias=.012f;
        }
        public static void Build()=>PerformanceBuild.Delivery();
        public static void CreateAndBuild() { Create(); Build(); }
    }
    [CustomEditor(typeof(FlowerAssembler))]
    public sealed class FlowerAssemblerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var f=(FlowerAssembler)target;
            EditorGUILayout.LabelField("Total petals",f.PetalCount.ToString());
            EditorGUILayout.HelpBox("Preset 数量、形态或种子修改后，在 Play Mode 显式重建。进度拖动只更新共享 Mesh。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if(GUILayout.Button("Rebuild Flower / 重建")) f.RebuildFlower();
                if(GUILayout.Button("Play Bloom / 重播")) f.PlayBloom();
                FlowerAvoidanceInspector.Draw(f);
            }
        }
        void OnSceneGUI()=>FlowerAvoidanceInspector.DrawScene((FlowerAssembler)target);
    }
}


