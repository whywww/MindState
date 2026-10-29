using UnityEngine;

namespace MindState
{
    public sealed class GalleryController : MonoBehaviour
    {
        public HeroPetal petal;
        public GalleryPost post;
        public bool showInterface=true;
        public float yaw=-12,pitch=12,distance=6.2f;
        public Vector3 focus=new Vector3(0,0.05f,-0.45f);
        GUIStyle title,caption,label,button,small;
        Texture2D panel,line;
        Font font;
        bool dragging;
        float fps=60;
        public void SetView(float horizontal,float vertical,float radius)
        { yaw=horizontal; pitch=vertical; distance=radius; PositionCamera(); }
        void Start() { Application.targetFrameRate=60; PositionCamera(); }
        void Update()
        {
            fps=Mathf.Lerp(fps,1/Mathf.Max(0.0001f,Time.unscaledDeltaTime),0.025f);
            if(Input.GetKeyDown(KeyCode.H)) showInterface=!showInterface;
            if(Input.GetKeyDown(KeyCode.Space)) petal.frozen=!petal.frozen;
            if(Input.GetKeyDown(KeyCode.R)) petal.RestartOpening();
            if(Input.GetMouseButtonDown(0)&&(!showInterface||Input.mousePosition.x>385*Screen.width/1920f)) dragging=true;
            if(Input.GetMouseButtonUp(0)) dragging=false;
            if(dragging)
            {
                yaw+=Input.GetAxis("Mouse X")*3;
                pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-65,65);
            }
            if(!showInterface||Input.mousePosition.x>385*Screen.width/1920f)
                distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*0.3f,2.0f,10);
            PositionCamera();
        }
        void PositionCamera()
        {
            Quaternion q=Quaternion.Euler(pitch,-yaw,0);
            Vector3 target=focus-q*Vector3.right*(showInterface?0.65f:0);
            transform.position=target+q*new Vector3(0,0,-distance); transform.rotation=q;
        }
        Texture2D Solid(Color c)
        {
            var t=new Texture2D(1,1) { hideFlags=HideFlags.HideAndDontSave };
            t.SetPixel(0,0,c); t.Apply(); return t;
        }
        void InitStyles()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
            panel=Solid(new Color(0.035f,0.045f,0.095f,0.94f));
            line=Solid(new Color(0.58f,0.57f,0.78f,0.4f));
            title=new GUIStyle(GUI.skin.label) { font=font,fontSize=34,normal={textColor=new Color(0.92f,0.90f,0.98f)} };
            caption=new GUIStyle(title) { fontSize=11,normal={textColor=new Color(0.68f,0.66f,0.85f)} };
            label=new GUIStyle(title) { fontSize=14,normal={textColor=new Color(0.79f,0.80f,0.88f)} };
            small=new GUIStyle(label) { fontSize=11,normal={textColor=new Color(0.54f,0.58f,0.70f)} };
            button=new GUIStyle(GUI.skin.button) { font=font,fontSize=12,padding=new RectOffset(5,5,6,6) };
        }
        void Rule(float y) { GUI.DrawTexture(new Rect(40,y,302,1),line); }
        bool Button(float x,float y,float w,string text)=>GUI.Button(new Rect(x,y,w,34),text,button);
        float ShapeSlider(float y,string name,float target,float actual,float min,float max)
        {
            GUI.Label(new Rect(40,y,305,24),name,label);
            GUI.Label(new Rect(40,y+28,305,20),$"目标 {target:0.00}    当前 {actual:0.00}",small);
            return GUI.HorizontalSlider(new Rect(40,y+57,302,20),target,min,max);
        }
        void OnGUI()
        {
            if(!showInterface||petal==null) return;
            if(title==null) InitStyles();
            var saved=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1920f,Screen.height/1080f,1));
            GUI.DrawTexture(new Rect(20,24,342,1032),panel);
            GUI.Label(new Rect(40,48,310,22),"M I N D S T A T E   /   S T U D I O   0 2",caption);
            GUI.Label(new Rect(38,86,310,55),"柔褶 · 绽放",title);
            GUI.Label(new Rect(40,148,310,23),"R A D I A L   S I L K   /   S I N G L E   P E T A L",caption);
            GUI.Label(new Rect(40,189,310,50),"宽阔的弧缘，向外舒展的褶皱。\n让心绪改变一片花瓣的姿态。",label);
            Rule(254);
            GUI.Label(new Rect(40,271,310,22),"01   输入来源 / INPUT",caption);
            if(Button(40,305,94,petal.inputMode==PetalInputMode.SimulatedBrain?"● 模拟脑电":"模拟脑电")) petal.inputMode=PetalInputMode.SimulatedBrain;
            if(Button(144,305,94,petal.inputMode==PetalInputMode.ExternalBrain?"● 外部脑电":"外部脑电")) petal.inputMode=PetalInputMode.ExternalBrain;
            if(Button(248,305,94,petal.inputMode==PetalInputMode.DirectShape?"● 三参数":"三参数")) petal.inputMode=PetalInputMode.DirectShape;
            string state=petal.inputMode==PetalInputMode.SimulatedBrain?"模拟值 · 1Hz":petal.inputMode==PetalInputMode.ExternalBrain?"等待/保持外部归一化数据":"独立手动控制";
            GUI.Label(new Rect(40,352,305,22),state,small);
            GUI.Label(new Rect(40,376,310,23),$"注意力 {petal.targetAttention*100:00}   放松度 {petal.targetRelaxation*100:00}",small);
            Rule(411);
            GUI.Label(new Rect(40,428,310,22),"02   连续形态 / SHAPE",caption);
            bool enabled=GUI.enabled; GUI.enabled=petal.inputMode==PetalInputMode.DirectShape;
            float angle=ShapeSlider(466,"展开角  OpeningAngle · °",petal.targetOpeningAngle,petal.OpeningAngle,90,150);
            float curl=ShapeSlider(551,"卷曲量  CurlAmount",petal.targetCurlAmount,petal.CurlAmount,0,1);
            float softness=ShapeSlider(636,"褶皱柔软度  PleatSoftness",petal.targetPleatSoftness,petal.PleatSoftness,0,1);
            GUI.enabled=enabled;
            if(petal.inputMode==PetalInputMode.DirectShape&&(angle!=petal.targetOpeningAngle||curl!=petal.targetCurlAmount||softness!=petal.targetPleatSoftness))
                petal.SetShapeParameters(angle,curl,softness);
            if(Button(40,724,94,"卷起")) petal.SetShapeParameters(105,0.95f,0.45f);
            if(Button(144,724,94,"半开")) petal.SetShapeParameters(115,0.55f,0.6f);
            if(Button(248,724,94,"盛放")) petal.SetShapeParameters(125,0.2f,0.7f);
            GUI.Label(new Rect(40,773,310,22),$"展开 {petal.OpeningProgress*100:000}%   {(petal.frozen?"造型已冻结":"连续响应中")}",label);
            if(Button(40,809,146,"重新展开 · R")) petal.RestartOpening();
            if(Button(196,809,146,petal.frozen?"继续响应":"冻结造型")) petal.frozen=!petal.frozen;
            if(Button(40,855,94,"正面")) SetView(0,0,6.2f);
            if(Button(144,855,94,"侧面")) SetView(78,8,6.2f);
            if(Button(248,855,94,"背面")) SetView(180,8,6.2f);
            if(Button(40,901,94,petal.grayPreview?"灰模 · 开":"灰模 · 关")) petal.grayPreview=!petal.grayPreview;
            if(Button(144,901,94,post.bloom?"Bloom · 开":"Bloom · 关")) post.bloom=!post.bloom;
            if(Button(248,901,94,"重置")) { petal.ResetDemo(); SetView(-12,12,6.2f); }
            Rule(958);
            GUI.Label(new Rect(40,976,310,24),"拖动旋转 · 滚轮缩放 · H 隐藏 · 空格冻结",small);
            GUI.Label(new Rect(40,1012,310,22),$"BUILT-IN  /  {fps:000} FPS  /  7 + {petal.finePleatCount} PLEATS",caption);
            GUI.matrix=saved;
        }
        void OnDestroy()
        {
            if(panel!=null) Destroy(panel); if(line!=null) Destroy(line); if(font!=null) Destroy(font);
        }
    }
}
