using System.Collections.Generic;
using UnityEngine;

namespace MindState
{
    public sealed partial class FlowerAssembler
    {
        public struct PetalLabelAnchor
        {
            public int layer,index;
            public bool visible;
            public Vector3 position;
            public string Id => "L"+layer+"-P"+index;
        }
        public void CopyPetalLabelAnchors(List<PetalLabelAnchor> destination)
        {
            destination.Clear();
            if(!Ready||layers==null) return;
            for(int i=0;i<layers.Length;i++) for(int j=0;j<layers[i].petals.Length;j++)
            {
                var renderer=layers[i].petals[j].renderer;
                destination.Add(new PetalLabelAnchor { layer=i,index=j,visible=renderer.enabled&&renderer.gameObject.activeInHierarchy,position=renderer.bounds.center });
            }
        }
    }
}
