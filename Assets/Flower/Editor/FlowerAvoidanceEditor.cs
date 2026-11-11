using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MindState.Editor
{
    public static class FlowerAvoidanceEditor
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
            var flower=Object.FindObjectOfType<FlowerAssembler>();
            if(flower!=null) flower.diagnosticMaterial=DebugMaterial();
            EditorApplication.EnterPlaymode();
        }
        public static Material DebugMaterial()
        {
            const string path="Assets/Flower/Art/PetalID.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(material,path); AssetDatabase.SaveAssets(); }
            return material;
        }
        [MenuItem("MindState/Flower Avoidance/Apply layout JSON to current Preset")]
        static void ApplyLayout()
        {
            string path=EditorUtility.OpenFilePanel("Layout JSON (requires new validation after edits)","Outputs","json");
            if(string.IsNullOrEmpty(path)) return;
            ApplyLayoutFile(path,true);
        }
        static void ApplyLayoutFile(string path,bool undo)
        {
            var preset=AssetDatabase.LoadAssetAtPath<FlowerPreset>("Assets/Flower/Art/ExhibitionPeony.asset");
            if(preset==null) throw new System.InvalidOperationException("Main flower preset is missing");
            if(undo) Undo.RecordObject(preset,"Apply flower layout");
            preset.ApplyLayoutJson(System.IO.File.ReadAllText(path));
            foreach(var layer in preset.layers) layer.Constrain();
            EditorUtility.SetDirty(preset); AssetDatabase.SaveAssets();
        }
        public static void ApplyLayoutAndBuild()
        {
            var args=System.Environment.GetCommandLineArgs(); int index=System.Array.IndexOf(args,"-layoutJson");
            if(index<0||index+1>=args.Length) throw new System.ArgumentException("-layoutJson is required");
            ApplyLayoutFile(args[index+1],false); PerformanceBuild.Delivery();
        }
    }
    public static class FlowerAvoidanceInspector
    {
        static readonly List<FlowerSurfaceCollision.Contact> contacts=new List<FlowerSurfaceCollision.Contact>();
        static FlowerAssembler owner;
        static float contactProgress;
        static int contactMeshId;
        public static void Draw(FlowerAssembler f)
        {
            if(!f.Ready) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Petal diagnostics / 逐瓣诊断",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); f.diagnosticColors=EditorGUILayout.Toggle("ID colors",f.diagnosticColors);
            f.diagnosticWireframe=EditorGUILayout.Toggle("Depth-tested wireframe",f.diagnosticWireframe);
            f.diagnosticLabels=EditorGUILayout.Toggle("IDs / anchors / axes",f.diagnosticLabels);
            if(EditorGUI.EndChangeCheck()) { if(f.diagnosticColors&&f.diagnosticMaterial==null) f.diagnosticMaterial=FlowerAvoidanceEditor.DebugMaterial(); f.ApplyDiagnosticColors(); SceneView.RepaintAll(); }
            if(GUILayout.Button("Show all / 恢复全部")) f.ShowAllPetals();
            foreach(var p in f.GetDiagnosticPetals())
            {
                EditorGUILayout.BeginHorizontal(); bool visible=EditorGUILayout.ToggleLeft(p.id,p.renderer.enabled);
                if(visible!=p.renderer.enabled) f.SetPetalVisible(p.layer,p.index,visible);
                if(GUILayout.Button("Solo",GUILayout.Width(48))) f.SoloPetal(p.layer,p.index);
                EditorGUILayout.EndHorizontal();
            }
            if(GUILayout.Button("Scan current pose (exact) / 检查当前曲面"))
            {
                var scan=new FlowerSurfaceCollision(f); scan.Sample(f.bloomProgress); scan.FindContacts(contacts); owner=f; contactProgress=f.bloomProgress; contactMeshId=f.LayerMesh(0).GetInstanceID();
                Debug.Log("Flower exact scan: "+contacts.Count+" intersecting pair/region records (roots require review)"); SceneView.RepaintAll();
            }
        }
        public static void DrawScene(FlowerAssembler f)
        {
            if(!f.Ready||(!f.diagnosticWireframe&&!f.diagnosticLabels)) return;
            var previous=Handles.zTest; Color color=Handles.color; Handles.zTest=CompareFunction.LessEqual;
            try
            {
                foreach(var p in f.GetDiagnosticPetals())
                {
                    if(!p.renderer.enabled) continue;
                    Handles.color=FlowerAssembler.DiagnosticColor(p.layer,p.index);
                    var v=f.PetalMesh(p.layer,p.index).vertices; const int cols=HeroPetal.WidthSegments+1,surface=cols*(HeroPetal.LengthSegments+1);
                    if(f.diagnosticWireframe)
                    {
                        for(int y=0;y<=80;y+=4) for(int x=0;x<128;x+=4) Line(p.body,v,surface,y*cols+x,y*cols+x+4);
                        for(int x=0;x<=128;x+=4) for(int y=0;y<80;y+=4) Line(p.body,v,surface,y*cols+x,(y+4)*cols+x);
                    }
                    if(f.diagnosticLabels)
                    {
                        Vector3 label=p.body.TransformPoint((v[64*cols+64]+v[64*cols+64+surface])*.5f);
                        Handles.Label(label,p.id); Handles.SphereHandleCap(0,p.anchor.position,Quaternion.identity,.025f,EventType.Repaint);
                        Handles.color=Color.red; Handles.DrawLine(p.anchor.position,p.anchor.position+p.anchor.right*.15f);
                        Handles.color=Color.green; Handles.DrawLine(p.anchor.position,p.anchor.position+p.anchor.up*.15f);
                        Handles.color=Color.blue; Handles.DrawLine(p.anchor.position,p.anchor.position+p.anchor.forward*.15f);
                    }
                }
                if(owner==f&&contactMeshId==f.LayerMesh(0).GetInstanceID()&&Mathf.Abs(contactProgress-f.bloomProgress)<1e-6f) foreach(var hit in contacts)
                { Handles.color=hit.rootRegion?Color.yellow:Color.red; Handles.SphereHandleCap(0,hit.point,Quaternion.identity,.035f,EventType.Repaint); }
            }
            finally { Handles.zTest=previous; Handles.color=color; }
        }
        static void Line(Transform body,Vector3[] v,int surface,int a,int b)=>Handles.DrawLine(body.TransformPoint((v[a]+v[a+surface])*.5f),body.TransformPoint((v[b]+v[b+surface])*.5f));
    }
}
