using UnityEngine;
using MindState.BCI.BrainLink;

namespace MindState
{
    [DefaultExecutionOrder(20),RequireComponent(typeof(Camera))]
    public sealed class FlowerGallery : MonoBehaviour
    {
        public FlowerAssembler flower;
        public bool showInterface=true;
        public float azimuth=22,elevation=55,distance=8.5f;
        public Vector3 focus=new Vector3(0,.65f,0);
        GUIStyle title,text,small,button;
        Texture2D panel;
        Font font;
        bool dragging;
        BrainLinkFlowerInput brainLink;
        public bool BrainLinkOwnsInput => brainLink != null && brainLink.isActiveAndEnabled;
        void Awake() { brainLink=GetComponent<BrainLinkFlowerInput>(); }
        void Start() { Application.targetFrameRate=60; SetView(azimuth,elevation); }
        public void SetView(float az,float el,float d=-1)
        {
            azimuth=az; elevation=el;
            if(flower!=null&&flower.Ready) { focus=flower.FullBloomBounds.center; var cameraComponent=GetComponent<Camera>(); distance=d>0?d:flower.FitCameraDistance(ViewRotation(),cameraComponent.fieldOfView,cameraComponent.aspect); }
            else if(d>0) distance=d;
            PositionCamera();
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.H)) showInterface=!showInterface;
            if(Input.GetKeyDown(KeyCode.Space)) flower.frozen=!flower.frozen;
            if(Input.GetKeyDown(KeyCode.F9)&&brainLink!=null) brainLink.ToggleDiagnostics();
            if(Input.GetKeyDown(KeyCode.R)&&!BrainLinkOwnsInput) flower.PlayBloom();
            bool over=showInterface&&Input.mousePosition.x<320*Screen.width/1920f;
            over|=brainLink!=null&&brainLink.ShowDiagnostics&&Input.mousePosition.x>=1320*Screen.width/1920f&&Input.mousePosition.y>Screen.height-270*Screen.height/1080f;
            if(Input.GetMouseButtonDown(0)&&!over) dragging=true;
            if(Input.GetMouseButtonUp(0)) dragging=false;
            if(dragging) { azimuth+=Input.GetAxis("Mouse X")*3; elevation=Mathf.Clamp(elevation-Input.GetAxis("Mouse Y")*2,-35,90); }
            if(!over) distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.3f,4,13);
            PositionCamera();
        }
        Quaternion ViewRotation()
        {
            float a=azimuth*Mathf.Deg2Rad,e=elevation*Mathf.Deg2Rad;
            Vector3 dir=new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),-Mathf.Cos(a)*Mathf.Cos(e));
            return Quaternion.LookRotation(-dir,Mathf.Abs(elevation)>89?Vector3.forward:Vector3.up);
        }
        void PositionCamera()
        {
            float a=azimuth*Mathf.Deg2Rad,e=elevation*Mathf.Deg2Rad;
            Vector3 dir=new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),-Mathf.Cos(a)*Mathf.Cos(e));
            Vector3 up=Mathf.Abs(elevation)>89?Vector3.forward:Vector3.up;
            Quaternion rotation=Quaternion.LookRotation(-dir,up);
            Vector3 target=focus-rotation*Vector3.right*(showInterface?.55f:0);
            transform.SetPositionAndRotation(target+dir*distance,rotation);
        }
        void Styles()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
            panel=new Texture2D(1,1); panel.SetPixel(0,0,new Color(.025f,.035f,.072f,.94f)); panel.Apply();
            title=new GUIStyle(GUI.skin.label) { font=font,fontSize=31,normal={textColor=new Color(.94f,.89f,.96f)} };
            text=new GUIStyle(title) { fontSize=16,wordWrap=true };
            small=new GUIStyle(text) { fontSize=12,normal={textColor=new Color(.64f,.65f,.77f)} };
            button=new GUIStyle(GUI.skin.button) { font=font,fontSize=14 };
        }
        bool Button(float x,float y,float w,string s)=>GUI.Button(new Rect(x,y,w,38),s,button);
        void OnGUI()
        {
            if(flower==null||!flower.Ready) return;
            if(title==null) Styles(); var saved=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1920f,Screen.height/1080f,1));
            if(brainLink!=null) brainLink.DrawDiagnostics(text);
            if(!showInterface) { GUI.matrix=saved; return; }
            GUI.DrawTexture(new Rect(24,30,280,1020),panel);
            GUI.Label(new Rect(46,65,245,30),"M I N D S T A T E  /  0 3",small);
            GUI.Label(new Rect(44,112,250,60),"浮光 · 花境",title);
            GUI.Label(new Rect(46,183,230,30),"P E A R L   P E O N Y",small);
            GUI.Label(new Rect(46,249,230,100),"一重珍珠，一重浅紫。\n让柔软的弧缘，\n在时间中缓缓舒展。",text);
            GUI.Label(new Rect(46,380,230,32),flower.ArtStatus,small);
            bool controlsEnabled=GUI.enabled; GUI.enabled=controlsEnabled&&!BrainLinkOwnsInput;
            if(Button(46,425,108,"澄明")) flower.SetArtState(FlowerArtState.Clarity);
            if(Button(168,425,108,"凝聚")) flower.SetArtState(FlowerArtState.Gathering);
            if(Button(46,475,108,"漫游")) flower.SetArtState(FlowerArtState.Wandering);
            if(Button(168,475,108,"幽微")) flower.SetArtState(FlowerArtState.Quiet);
            if(Button(46,535,108,"模拟脑电")) flower.SimulateArtBrain();
            if(Button(168,535,108,"外部输入")) flower.WaitForArtBrain();
            if(Button(46,595,108,"开合重播")) flower.PlayBloom();
            GUI.enabled=controlsEnabled;
            if(Button(168,595,108,flower.frozen?"继续":"冻结")) flower.SetFrozen(!flower.frozen);
            float shown=flower.bloomProgress;
            GUI.Label(new Rect(46,658,54,24),"开合",text);
            GUI.enabled=controlsEnabled&&!BrainLinkOwnsInput;
            float manual=GUI.HorizontalSlider(new Rect(108,663,140,20),shown,0,1);
            if(!BrainLinkOwnsInput&&GUI.changed&&Mathf.Abs(manual-flower.bloomProgress)>.0005f) flower.SetBloomProgress(manual);
            GUI.enabled=controlsEnabled;
            shown=flower.bloomProgress;
            GUI.Label(new Rect(254,658,46,24),Mathf.RoundToInt(shown*100)+"%",small);
            GUI.Label(new Rect(46,710,230,30),"02   观察 / VIEW",small);
            if(Button(46,759,108,"正面")) SetView(0,90);
            if(Button(168,759,108,"3/4")) SetView(22,55);
            if(Button(46,812,108,"侧面")) SetView(22,8);
            if(Button(168,812,108,"重置")) { SetView(22,55); if(!BrainLinkOwnsInput) flower.ResetArt(); }
            GUI.Label(new Rect(46,939,230,65),"拖动环绕 · 滚轮缩放\nR 重播 · 空格冻结 · H 隐藏\nF9 BrainLink 数值",small);
            GUI.matrix=saved;
        }
        void OnDestroy() { if(panel!=null) Destroy(panel); if(font!=null) Destroy(font); }
    }
}

