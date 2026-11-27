using System.Collections.Generic;
using UnityEngine;

namespace MindState
{
    public sealed partial class FlowerGallery
    {
        bool showPetalLabels;
        readonly List<FlowerAssembler.PetalLabelAnchor> labelAnchors=new List<FlowerAssembler.PetalLabelAnchor>(18);
        readonly List<Rect> labelRects=new List<Rect>(18);
        GUIStyle petalLabelStyle;
        void DrawPetalLabels()
        {
            if(!showPetalLabels||viewCamera==null) return;
            if(petalLabelStyle==null) petalLabelStyle=new GUIStyle(GUI.skin.label) { font=font,fontSize=16,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white} };
            flower.CopyPetalLabelAnchors(labelAnchors); labelRects.Clear();
            foreach(var anchor in labelAnchors)
            {
                if(!anchor.visible) continue;
                var point=viewCamera.WorldToViewportPoint(anchor.position);
                if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1) continue;
                var target=new Vector2(point.x*1920,(1-point.y)*1080);
                Rect rect=new Rect(Mathf.Clamp(target.x-38,4,1840),Mathf.Clamp(target.y-32,4,1050),76,26);
                // Search a deterministic grid for the nearest unused label rectangle.
                bool placed=false;
                for(int radius=0;radius<40&&!placed;radius++)
                    for(int y=-radius;y<=radius&&!placed;y++) for(int x=-radius;x<=radius&&!placed;x++)
                    {
                        if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=radius) continue;
                        var candidate=new Rect(Mathf.Clamp(rect.x+x*80,4,1840),Mathf.Clamp(rect.y+y*30,4,1050),76,26);
                        bool overlaps=false;
                        foreach(var used in labelRects) if(candidate.Overlaps(used)) { overlaps=true; break; }
                        if(!overlaps) { rect=candidate; placed=true; }
                    }
                labelRects.Add(rect);
                var end=new Vector2(Mathf.Clamp(target.x,rect.xMin,rect.xMax),Mathf.Clamp(target.y,rect.yMin,rect.yMax));
                var delta=end-target; var matrix=GUI.matrix; var color=GUI.color;
                GUI.color=new Color(1,1,1,.7f);
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,target);
                GUI.DrawTexture(new Rect(target.x,target.y,delta.magnitude,1),Texture2D.whiteTexture);
                GUI.matrix=matrix; GUI.color=color;
                GUI.DrawTexture(rect,panel);
                GUI.Label(rect,anchor.Id,petalLabelStyle);
            }
        }
    }
}
