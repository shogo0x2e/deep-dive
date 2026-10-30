using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>A small vector illustration: opening the arms into a surfing stance.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SurfPoseGraphic : UnityEngine.UI.MaskableGraphic
    {
        readonly Color ink = new Color(.12f,.23f,.29f);
        readonly Color mint = new Color(.12f,.66f,.59f);
        void Update() { if (Application.isPlaying) SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            float scale = Mathf.Min(rectTransform.rect.width/420, rectTransform.rect.height/420);
            float t = Application.isPlaying ? Mathf.SmoothStep(0,1,(Mathf.Sin(Time.unscaledTime*1.5f)+1)*.5f) : 1;
            Disk(vh,new Vector2(0,15),new Vector2(174,174),new Color(.87f,.95f,.93f),scale);
            Disk(vh,new Vector2(0,-145),new Vector2(160,16),new Color(.74f,.86f,.84f),scale);
            Disk(vh,new Vector2(0,-128),new Vector2(145,15),ink,scale);
            Capsule(vh,new Vector2(-112,-126),new Vector2(112,-126),4,mint,scale);
            Capsule(vh,new Vector2(-8,-8),new Vector2(-39,-70),12,ink,scale);
            Capsule(vh,new Vector2(-39,-70),new Vector2(-65,-115),10,ink,scale);
            Capsule(vh,new Vector2(8,-8),new Vector2(48,-65),12,ink,scale);
            Capsule(vh,new Vector2(48,-65),new Vector2(68,-115),10,ink,scale);
            Capsule(vh,new Vector2(-67,-116),new Vector2(-40,-116),8,ink,scale);
            Capsule(vh,new Vector2(62,-116),new Vector2(89,-116),8,ink,scale);
            Capsule(vh,new Vector2(0,2),new Vector2(0,82),24,mint,scale);
            Capsule(vh,new Vector2(0,92),new Vector2(0,111),9,ink,scale);
            Disk(vh,new Vector2(0,137),new Vector2(25,29),ink,scale);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var shoulder=new Vector2(sign*17,80);
                var elbow=new Vector2(sign*Mathf.Lerp(68,86,t),Mathf.Lerp(24,80,t));
                var wrist=new Vector2(sign*Mathf.Lerp(97,151,t),Mathf.Lerp(-20,80,t));
                Capsule(vh,shoulder,elbow,10,mint,scale);
                Capsule(vh,elbow,wrist,9,ink,scale);
                Disk(vh,wrist,new Vector2(10,10),ink,scale);
                Vector2 a=new Vector2(sign*180,80),b=new Vector2(sign*166,92),c=new Vector2(sign*166,68);
                Triangle(vh,a,b,c,mint,scale);
            }
        }
        static void Capsule(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,float radius,Color tint,float scale)
        {
            var delta=(b-a).normalized; var normal=new Vector2(-delta.y,delta.x)*radius;
            Triangle(vh,a+normal,b+normal,b-normal,tint,scale); Triangle(vh,a+normal,b-normal,a-normal,tint,scale);
            Disk(vh,a,Vector2.one*radius,tint,scale); Disk(vh,b,Vector2.one*radius,tint,scale);
        }
        static void Disk(UnityEngine.UI.VertexHelper vh,Vector2 center,Vector2 size,Color tint,float scale)
        {
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                Triangle(vh,center,center+new Vector2(Mathf.Cos(a)*size.x,Mathf.Sin(a)*size.y),center+new Vector2(Mathf.Cos(b)*size.x,Mathf.Sin(b)*size.y),tint,scale);
            }
        }
        static void Triangle(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint,float scale)
        {
            int start=vh.currentVertCount; vh.AddVert(a*scale,tint,Vector2.zero); vh.AddVert(b*scale,tint,Vector2.zero); vh.AddVert(c*scale,tint,Vector2.zero); vh.AddTriangle(start,start+1,start+2);
        }
    }
}
