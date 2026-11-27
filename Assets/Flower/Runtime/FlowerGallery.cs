using UnityEngine;
using MindState.BCI.BrainLink;

namespace MindState
{
    [DefaultExecutionOrder(20),RequireComponent(typeof(Camera))]
    public sealed partial class FlowerGallery : MonoBehaviour
    {
        public FlowerAssembler flower;
        public bool showInterface;
        public float azimuth=22,elevation=55,distance=8.5f;
        public Vector3 focus=new Vector3(0,.65f,0);
        GUIStyle title,text,small,button,fpsLabel;
        Texture2D panel;
        Font font;
        bool dragging,showFps;
        const float SliderSyncDuration=6;
        static readonly Vector2[] ArtTargets={new Vector2(.8f,.8f),new Vector2(.8f,.2f),new Vector2(.2f,.8f),new Vector2(.2f,.2f)};
        static readonly Vector2[] StateCorners={new Vector2(1,1),new Vector2(1,0),new Vector2(0,1),new Vector2(0,0)};
        float attention=.5f,relaxation=.5f,fps, fpsTime;
        float sliderFromA=.5f,sliderFromR=.5f,sliderTargetA=.5f,sliderTargetR=.5f,sliderAnim=SliderSyncDuration;
        int fpsFrames;
        bool sliderOwnsBrain,lastBrainOwned,animatingSliders;
        BrainLinkFlowerInput brainLink;
        Camera viewCamera;
        GUIStyle barText,barValue;
        public bool BrainLinkOwnsInput => brainLink != null && brainLink.isActiveAndEnabled
            && !brainLink.ManualWindowActive && brainLink.ReadSnapshot().Usable;
        void Awake()
        {
            brainLink=GetComponent<BrainLinkFlowerInput>(); viewCamera=GetComponent<Camera>();
            // The connection stays on for the whole run; the scene switch stays off so the geometry cache hash is untouched.
            if(brainLink!=null) brainLink.enabled=true;
        }
        void Start() { Application.targetFrameRate=240; SetView(azimuth,elevation); }
        public void SetView(float az,float el,float d=-1)
        {
            azimuth=az; elevation=el;
            if(flower!=null&&flower.Ready)
            {
                var rotation=ViewRotation();
                float fit=flower.FitCompositionDistance(rotation,viewCamera.fieldOfView,viewCamera.aspect,out var pivot);
                distance=d>0?d:fit;
                focus=d>0?flower.FullBloomBounds.center:pivot;
            }
            else if(d>0) distance=d;
            PositionCamera();
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.L)) showFps=!showFps;
            fpsFrames++; fpsTime+=Time.unscaledDeltaTime;
            if(fpsTime>=.5f) { fps=fpsFrames/fpsTime; fpsFrames=0; fpsTime=0; }
            if(Input.GetKeyDown(KeyCode.N)) showPetalLabels=!showPetalLabels;
            if(Input.GetKeyDown(KeyCode.H)) showInterface=!showInterface;
            if(Input.GetKeyDown(KeyCode.Space)) flower.frozen=!flower.frozen;
            if(Input.GetKeyDown(KeyCode.F9)&&brainLink!=null) brainLink.ToggleDiagnostics();
            if(Input.GetKeyDown(KeyCode.R)) { animatingSliders=false; flower.PlayBloom(); }
            if(flower!=null&&flower.Ready)
            {
                bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
                for(int i=0;i<4;i++) if(Input.GetKeyDown(KeyCode.Alpha1+i))
                {
                    if(shift) InjectArtCorner(i);
                    else SelectArtState(i);
                }
            }
            bool over=showInterface&&Input.mousePosition.x<320*Screen.width/1920f;
            over|=brainLink!=null&&brainLink.ShowDiagnostics&&Input.mousePosition.x>=1320*Screen.width/1920f&&Input.mousePosition.y>Screen.height-270*Screen.height/1080f;
            if(Input.GetMouseButtonDown(0)&&!over) dragging=true;
            if(Input.GetMouseButtonUp(0)) dragging=false;
            if(dragging) { azimuth+=Input.GetAxis("Mouse X")*3; elevation=Mathf.Clamp(elevation-Input.GetAxis("Mouse Y")*2,-35,90); }
            if(!over) distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.3f,4,16);
            PositionCamera();
            if(animatingSliders)
            {
                if(brainLink!=null) brainLink.NoteManualInteraction();
                sliderAnim=Mathf.Min(SliderSyncDuration,sliderAnim+Time.unscaledDeltaTime);
                float t=Mathf.SmoothStep(0,1,sliderAnim/SliderSyncDuration);
                attention=Mathf.Lerp(sliderFromA,sliderTargetA,t);
                relaxation=Mathf.Lerp(sliderFromR,sliderTargetR,t);
                if(sliderAnim>=SliderSyncDuration) animatingSliders=false;
            }
            bool owns=BrainLinkOwnsInput;
            if(flower!=null&&flower.Ready)
            {
                if(owns)
                {
                    animatingSliders=false; sliderOwnsBrain=false;
                    var sample=brainLink.ReadSnapshot();
                    attention=sample.Attention/100f; relaxation=sample.Meditation/100f;
                    flower.BrainBaselineFrozen=false;
                }
                else
                {
                    if(lastBrainOwned) sliderOwnsBrain=true;
                    flower.BrainBaselineFrozen=sliderOwnsBrain;
                    if(sliderOwnsBrain&&flower.ArtInput==FlowerArtInput.External)
                        flower.SetBrainMetrics(attention,relaxation);
                }
            }
            lastBrainOwned=owns;
        }
        void AnimateSliders(Vector2 target)
        {
            sliderFromA=attention; sliderFromR=relaxation;
            sliderTargetA=target.x; sliderTargetR=target.y;
            sliderAnim=0; animatingSliders=true;
        }
        void SelectArtState(int state)
        {
            if(flower==null) return;
            if(brainLink!=null) brainLink.NoteManualInteraction();
            if(!flower.ArtActive||flower.ArtInput!=FlowerArtInput.External) flower.WaitForArtBrain();
            AnimateSliders(ArtTargets[state]);
            sliderOwnsBrain=true;
            flower.BrainBaselineFrozen=true; flower.ResetBrainBaseline();
        }
        public void InjectArtCorner(int state)
        {
            if(flower==null) return;
            if(brainLink!=null) brainLink.NoteManualInteraction();
            animatingSliders=false;
            var corner=StateCorners[state];
            attention=corner.x; relaxation=corner.y;
            if(!flower.ArtActive||flower.ArtInput!=FlowerArtInput.External) flower.WaitForArtBrain();
            sliderOwnsBrain=true; flower.BrainBaselineFrozen=true; flower.ResetBrainBaseline(); flower.SetBrainMetrics(attention,relaxation);
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
            // The camera stays fixed across art states; the buds step toward it instead.
            if(flower!=null) flower.SetViewFrame(dir,rotation*Vector3.up,rotation*Vector3.right,distance);
            transform.SetPositionAndRotation(focus+dir*distance,rotation);
        }
        void Styles()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
            panel=new Texture2D(1,1); panel.SetPixel(0,0,new Color(.025f,.035f,.072f,.94f)); panel.Apply();
            title=new GUIStyle(GUI.skin.label) { font=font,fontSize=31,normal={textColor=new Color(.94f,.89f,.96f)} };
            text=new GUIStyle(title) { fontSize=16,wordWrap=true };
            small=new GUIStyle(text) { fontSize=12,normal={textColor=new Color(.64f,.65f,.77f)} };
            button=new GUIStyle(GUI.skin.button) { font=font,fontSize=14 };
            fpsLabel=new GUIStyle(text) { alignment=TextAnchor.LowerRight,normal={textColor=new Color(.78f,.8f,.9f)} };
            barText=new GUIStyle(small) { fontSize=24 };
            barValue=new GUIStyle(barText) { alignment=TextAnchor.MiddleRight };
        }
        bool Button(float x,float y,float w,string s)=>GUI.Button(new Rect(x,y,w,38),s,button);
        void OnGUI()
        {
            if(title==null) Styles(); var saved=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1920f,Screen.height/1080f,1));
            if(showFps) GUI.Label(new Rect(1676,1026,220,30),Mathf.RoundToInt(fps)+" FPS",fpsLabel);
            DrawBrainBar();
            if(flower==null||!flower.Ready) { GUI.matrix=saved; return; }
            DrawPetalLabels();
            if(brainLink!=null) brainLink.DrawDiagnostics(text);
            if(!showInterface) { GUI.matrix=saved; return; }
            GUI.DrawTexture(new Rect(24,30,280,960),panel);
            GUI.Label(new Rect(46,65,245,30),"M I N D S T A T E  /  0 3",small);
            GUI.Label(new Rect(44,112,250,60),"浮光 · 花境",title);
            GUI.Label(new Rect(46,183,230,30),"P E A R L   P E O N Y",small);
            GUI.Label(new Rect(46,249,230,100),"一重珍珠，一重浅紫。\n让柔软的弧缘，\n在时间中缓缓舒展。",text);
            GUI.Label(new Rect(46,380,230,32),flower.ArtStatus,small);
            if(Button(46,425,108,"澄明")) SelectArtState(0);
            if(Button(168,425,108,"凝聚")) SelectArtState(1);
            if(Button(46,475,108,"漫游")) SelectArtState(2);
            if(Button(168,475,108,"幽微")) SelectArtState(3);
            if(Button(46,535,108,"开合重播")) { animatingSliders=false; flower.PlayBloom(); }
            if(Button(168,535,108,flower.frozen?"继续":"冻结")) flower.SetFrozen(!flower.frozen);
            float shown=flower.bloomProgress;
            GUI.Label(new Rect(46,598,54,24),"开合",text);
            float manual=GUI.HorizontalSlider(new Rect(108,603,140,20),shown,0,1);
            if(GUI.changed&&Mathf.Abs(manual-flower.bloomProgress)>.0005f) { animatingSliders=false; flower.SetBloomProgress(manual); }
            shown=flower.bloomProgress;
            GUI.Label(new Rect(254,598,46,24),Mathf.RoundToInt(shown*100)+"%",small);
            GUI.Label(new Rect(46,650,230,30),"02   观察 / VIEW",small);
            if(Button(46,699,108,"正面")) SetView(0,90);
            if(Button(168,699,108,"3/4")) SetView(22,55);
            if(Button(46,752,108,"侧面")) SetView(22,8);
            if(Button(168,752,108,"重置")) { SetView(22,55); flower.ResetArt(); SelectArtState(0); }
            string hint;
            if(BrainLinkOwnsInput) hint="脑电跟随中";
            else if(brainLink!=null&&brainLink.isActiveAndEnabled&&brainLink.ManualWindowActive)
                hint="手动优先 · "+Mathf.CeilToInt(brainLink.ManualWindowRemaining)+"s";
            else if(sliderOwnsBrain) hint="手动 · 等待脑电";
            else hint="拖动校准";
            GUI.Label(new Rect(46,794,130,24),"03   脑电 / SIGNAL",small);
            GUI.Label(new Rect(176,794,120,24),hint,small);
            GUI.Label(new Rect(46,820,54,24),"专注",text);
            float attentionNow=GUI.HorizontalSlider(new Rect(108,825,140,20),attention,0,1);
            if(!Mathf.Approximately(attentionNow,attention)) { animatingSliders=false; attention=attentionNow; sliderOwnsBrain=true; if(brainLink!=null) brainLink.NoteManualInteraction(); flower.BrainBaselineFrozen=true; flower.ResetBrainBaseline(); flower.SetBrainMetrics(attention,relaxation); }
            GUI.Label(new Rect(254,820,46,24),Mathf.RoundToInt(attention*100)+"%",small);
            GUI.Label(new Rect(46,850,54,24),"放松",text);
            float relaxationNow=GUI.HorizontalSlider(new Rect(108,855,140,20),relaxation,0,1);
            if(!Mathf.Approximately(relaxationNow,relaxation)) { animatingSliders=false; relaxation=relaxationNow; sliderOwnsBrain=true; if(brainLink!=null) brainLink.NoteManualInteraction(); flower.BrainBaselineFrozen=true; flower.ResetBrainBaseline(); flower.SetBrainMetrics(attention,relaxation); }
            GUI.Label(new Rect(254,850,46,24),Mathf.RoundToInt(relaxation*100)+"%",small);
            GUI.Label(new Rect(46,879,230,80),"拖动环绕 · 滚轮缩放\nR 重播 · 空格冻结 · H 隐藏\nN 花瓣编号 · F9 BrainLink 数值\n手动后 5 秒自动回脑电",small);
            GUI.matrix=saved;
        }
        void DrawBrainBar()
        {
            float x=showInterface?328:24;
            GUI.DrawTexture(new Rect(x,928,744,128),panel);
            var s=brainLink!=null?brainLink.ReadSnapshot():default;
            bool live=brainLink!=null&&brainLink.isActiveAndEnabled&&s.Usable;
            Color gray=new Color(.45f,.47f,.55f);
            DrawMetricBar(x,956,"专注度",s,live?new Color(.94f,.89f,.96f):gray);
            DrawMetricBar(x,1012,"放松度",s,live?new Color(1f,.72f,.82f):gray);
        }
        void DrawMetricBar(float x,float y,string label,BrainLinkSnapshot s,Color fill)
        {
            GUI.Label(new Rect(x+36,y-12,152,40),label,barText);
            GUI.color=new Color(.16f,.18f,.26f);
            GUI.DrawTexture(new Rect(x+196,y,400,20),Texture2D.whiteTexture);
            if(s.HasSample)
            {
                int value=label=="专注度"?s.Attention:s.Meditation;
                if(value>0) { GUI.color=fill; GUI.DrawTexture(new Rect(x+196,y,400*Mathf.Clamp01(value/100f),20),Texture2D.whiteTexture); }
                GUI.color=Color.white;
                GUI.Label(new Rect(x+612,y-12,96,40),value.ToString(),barValue);
            }
            else
            {
                GUI.color=Color.white;
                GUI.Label(new Rect(x+612,y-12,96,40),"--",barValue);
            }
        }
        void OnDestroy() { if(panel!=null) Destroy(panel); if(font!=null) Destroy(font); }
    }
}

