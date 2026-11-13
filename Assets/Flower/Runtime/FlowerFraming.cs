using UnityEngine;
namespace MindState
{
    // Camera framing lives beside the assembled flower but outside the baked geometry sources, so
    // refitting the page never invalidates the petal caches.
    public sealed partial class FlowerAssembler
    {
        // The camera never moves for a state change; the flowers compensate instead. Whichever bloom
        // closes steps toward the viewer and grows a little, and the whole cluster drops in the page
        // so the tighter bud stays near the middle of the frame.
        const float BudScaleGain=.14f,BudForwardGain=.25f,BudDropGain=.05f,BudCenterGain=.06f;
        // With the main bloom closed the companion settles further back and drifts toward it.
        const float EchoBackGain=.05f,EchoSideGain=.04f;
        Vector3 viewToCamera=Vector3.back,viewPageUp=Vector3.up,viewPageRight=Vector3.right;
        float viewDistance=8.5f;
        Vector3 restPosition,bloomCenterLocal;
        bool restCaptured;
        // Mathf.SmoothStep smooths the lerp parameter, not a value inside from..to, so map the
        // openness onto 0..1 first: a tight bud reads 1, an open bloom reads 0.
        static float BudFactor(float bloom)
        {
            float t=Mathf.Clamp01((bloom-.15f)/.45f);
            return 1f-t*t*(3f-2f*t);
        }
        internal void SetViewFrame(Vector3 toCamera,Vector3 pageUp,Vector3 pageRight,float distance)
        { viewToCamera=toCamera.normalized; viewPageUp=pageUp; viewPageRight=pageRight; viewDistance=distance; }
        // Called only when choosing a view, never in the animation update. Fit the actual full-bloom
        // vertices rather than the empty corners of a large whole-flower AABB, and include the echo
        // flower so the page always holds both blooms. The silk thread stays out of the fit: its
        // low ring would otherwise eat the vertical frame at the default downward view.
        public float FitCompositionDistance(Quaternion rotation,float fieldOfView,float aspect,out Vector3 pivot)
        {
            float tan=Mathf.Tan(fieldOfView*Mathf.Deg2Rad*.5f),distance=1;
            var min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
            var max=new Vector3(float.MinValue,float.MinValue,float.MinValue);
            foreach(var l in layers) for(int j=0;j<l.petals.Length;j++)
            {
                var p=l.petals[j]; var v=SurfaceFor(l,j).poses[PoseCount-1].data;
                for(int i=0;i<v.Length;i++) { var point=p.fullMatrix.MultiplyPoint3x4(v[i].p); min=Vector3.Min(min,point); max=Vector3.Max(max,point); }
            }
            var echoOffset=EchoOffsetUnit*MeasureBloomRadius();
            var echoCorners=new Vector3[8];
            for(int i=0;i<8;i++)
            {
                var corner=new Vector3((i&1)==0?min.x:max.x,(i&2)==0?min.y:max.y,(i&4)==0?min.z:max.z);
                echoCorners[i]=transform.TransformPoint(echoOffset+transform.InverseTransformPoint(corner)*EchoScale);
                min=Vector3.Min(min,echoCorners[i]); max=Vector3.Max(max,echoCorners[i]);
            }
            // Lean the frame toward the main bloom: the echo still fits on the left, but the main
            // flower sits near the centre of the page instead of being pushed right by the companion.
            pivot=Vector3.Lerp((min+max)*.5f,FullBloomBounds.center,.65f);
            Matrix4x4 view=Matrix4x4.TRS(pivot,rotation,Vector3.one).inverse;
            void Fit(Vector3 world)
            {
                Vector3 point=view.MultiplyPoint3x4(world);
                distance=Mathf.Max(distance,Mathf.Max(Mathf.Abs(point.y)/tan-point.z,Mathf.Abs(point.x)/(tan*aspect)-point.z));
            }
            foreach(var l in layers) for(int j=0;j<l.petals.Length;j++)
            {
                var p=l.petals[j]; var v=SurfaceFor(l,j).poses[PoseCount-1].data;
                for(int i=0;i<v.Length;i++) Fit(p.fullMatrix.MultiplyPoint3x4(v[i].p));
            }
            foreach(var corner in echoCorners) Fit(corner);
            // Sitting the composition a little high reads better than dead centre: the lower petal
            // skirt gets the room, the empty sky above stays slim.
            pivot+=rotation*Vector3.down*(distance*.05f);
            // The depth-aware fit is dominated by the far, high petals and sits well outside the
            // visible silhouette, so the page can come in closer and still hold both blooms.
            return distance*.83f;
        }
    }
}
