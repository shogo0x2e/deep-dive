using UnityEngine;

namespace HighwaySurfing
{
    public sealed partial class NightHighwayWorld
    {
        private struct RoadSection
        {
            public int Branch, Kind; // 0: original motorway, 1: ramp, 2: crossing motorway
            public float From, To;
        }
        private readonly System.Collections.Generic.List<RoadSection> visibleRoads = new System.Collections.Generic.List<RoadSection>(12);
        private readonly System.Collections.Generic.List<Vector3> reservedSegments = new System.Collections.Generic.List<Vector3>(128);
        private HighwayRouteState.Frame SectionFrame(RoadSection road, float at)
            => road.Kind == 2 ? route.Crossing(road.Branch, at) : route.Branch(road.Branch, at, road.Kind == 1);
        private Vector3 RoadVertex(HighwayRouteState.Frame frame, float x, float y) => WorldPoint(frame.Point(x, y));
        private void RoadStrip(HighwayRouteState.Frame a, HighwayRouteState.Frame b, float left, float right, float height, Color color)
            => surfaces.Quad(RoadVertex(a,left,height),RoadVertex(a,right,height),RoadVertex(b,right,height),RoadVertex(b,left,height),color);

        private void BuildRoad(float s)
        {
            visibleRoads.Clear();
            AddVisibleRoad(new RoadSection { Branch=0, Kind=0, From=-2000, To=0 });
            // Retain roads by physical distance, including older decks seen after a full turn.
            for (int i = 0; i <= route.Decisions; i++)
            {
                AddVisibleRoad(new RoadSection { Branch=i, Kind=0, From=0, To=HighwayRouteState.BranchInterval });
                AddVisibleRoad(new RoadSection { Branch=i, Kind=1, From=0, To=HighwayRouteState.ConnectorLength });
                AddVisibleRoad(new RoadSection { Branch=i, Kind=2, From=-2000,
                    To=i<route.Decisions && route.TookExit(i) ? HighwayRouteState.BranchInterval-HighwayRouteState.ConnectorLength : 1200 });
            }
            // Cache simple road segments once per render, not once per city building.
            reservedSegments.Clear();
            foreach (RoadSection road in visibleRoads)
            {
                float stride=road.Kind==1 ? 25 : road.To-road.From;
                Vector3 a=SectionFrame(road,road.From).Position;
                for(float at=road.From;at<road.To;at+=stride)
                {
                    Vector3 b=SectionFrame(road,Mathf.Min(at+stride,road.To)).Position;
                    reservedSegments.Add(a); reservedSegments.Add(b); a=b;
                }
            }
            // The ground stays fixed below the elevated decks, including while the road turns.
            Vector3 ground = new Vector3(Mathf.Floor(origin.x/100)*100, -8, Mathf.Floor(origin.z/100)*100);
            surfaces.Quad(WorldPoint(ground+new Vector3(-2000,0,-2000)),WorldPoint(ground+new Vector3(2000,0,-2000)),
                WorldPoint(ground+new Vector3(2000,0,2000)),WorldPoint(ground+new Vector3(-2000,0,2000)), new Color(.0012f,.0025f,.005f));
            foreach (RoadSection road in visibleRoads) DrawRoadSection(road);
        }
        private void AddVisibleRoad(RoadSection road)
        {
            Vector3 a=SectionFrame(road,road.From).Position, b=SectionFrame(road,road.To).Position;
            var bounds=new Bounds((a+b)*.5f, new Vector3(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y),Mathf.Abs(a.z-b.z)));
            bounds.Expand(road.Kind==1 ? 150 : 30);
            if(bounds.SqrDistance(origin)<(DrawDistance+150)*(DrawDistance+150)) visibleRoads.Add(road);
        }

        private void DrawRoadSection(RoadSection road)
        {
            bool ramp = road.Kind == 1;
            const float step = 5;
            float left = -11, right = ramp ? -.8f : 11;
            HighwayRouteState.Frame mergeFrame=route.Crossing(road.Branch,0);
            HighwayRouteState.Frame a = SectionFrame(road, road.From);
            for (float at = road.From; at < road.To-.001f; at += step)
            {
                float end = Mathf.Min(at+step, road.To);
                HighwayRouteState.Frame b = SectionFrame(road, end);
                Vector3 mid = WorldPoint((a.Position+b.Position)*.5f);
                if (mid.z > -100 && mid.z < DrawDistance+80 && Mathf.Abs(mid.x) < Mathf.Max(0,mid.z)*1.8f+90)
                {
                    float light = Mathf.Exp(-Mathf.Pow((mid.z-22)/32,2));
                    float lampPool = Mathf.Pow(.5f+.5f*Mathf.Cos(at/40*Mathf.PI*2),12);
                    Color asphalt = new Color(.002f,.003f,.005f)+new Color(.002f,.003f,.0045f)*light;
                    asphalt += new Color(.003f,.0011f,.0002f)*lampPool;
                    bool merging = ramp && at>=HighwayRouteState.MergeStart;
                    // On the flat merge, draw only the ramp pavement outside the motorway.
                    // The existing deck fills the rest, avoiding coplanar flicker and crossing lane paint.
                    float cutA=merging ? MergePavementEdge(a,mergeFrame) : right;
                    float cutB=merging ? MergePavementEdge(b,mergeFrame) : right;
                    surfaces.Quad(RoadVertex(a,left,-.02f),RoadVertex(a,cutA,-.02f),RoadVertex(b,cutB,-.02f),RoadVertex(b,left,-.02f),asphalt);
                    Color concrete = new Color(.008f,.012f,.019f);
                    surfaces.Quad(RoadVertex(a,left,-1),RoadVertex(a,left,-.02f),RoadVertex(b,left,-.02f),RoadVertex(b,left,-1),concrete);
                    surfaces.Quad(RoadVertex(a,right,-.02f),RoadVertex(a,right,-1),RoadVertex(b,right,-1),RoadVertex(b,right,-.02f),concrete*.7f);
                    RoadStrip(a,b,left,right,-1,concrete*.45f);
                    float fade = .3f+.7f*Mathf.Exp(-Mathf.Max(mid.z,0)/180);
                    Color edge = (ramp ? new Color(.07f,.25f,.22f) : new Color(.11f,.14f,.18f))*fade;
                    bool forkOpening = road.Kind==0 && road.From>=0 && at<100;
                    bool crossOpening = road.Kind==2 && at>=-HighwayRouteState.MergeRun && at<25;
                    if (!forkOpening && !crossOpening) RoadStrip(a,b,-10,-9.86f,.015f,edge);
                    if (!ramp)
                    {
                        RoadStrip(a,b,9.86f,10,.015f,edge*.4f);
                        RoadStrip(a,b,-.9f,-.8f,.015f,new Color(.35f,.19f,.04f));
                        RoadStrip(a,b,.8f,.9f,.015f,new Color(.12f,.08f,.03f));
                        RoadStrip(a,b,-.62f,.62f,.35f,concrete);
                    }
                    else if (at>80 && !merging) RoadStrip(a,b,-1.04f,-.90f,.015f,edge);
                    if (Mathf.FloorToInt(at/5)%3==0)
                    {
                        if(!merging || Mathf.Min(cutA,cutB)>-5.30f)
                            RoadStrip(a,b,-5.46f,-5.30f,.025f,new Color(.45f,.52f,.59f)*fade);
                        if (!ramp) RoadStrip(a,b,5.30f,5.46f,.025f,new Color(.45f,.52f,.59f)*fade*.35f);
                    }
                    if (!forkOpening && !crossOpening) RoadGuard(a,b,-10.7f,ramp);
                    if (!ramp) RoadGuard(a,b,10.7f,false);
                    else if (at>80 && !merging) RoadGuard(a,b,-.98f,true);
                    if (Mathf.FloorToInt(at/5)%2==0)
                    {
                        if (!forkOpening && !crossOpening) Glow(RoadVertex(a,-10.58f,.76f),.17f,ramp?RampTeal:CoolWhite*.65f);
                        if (!ramp) Glow(RoadVertex(a,-.68f,.58f),.16f,Amber*.55f);
                        if (ramp && at>80 && !merging) Glow(RoadVertex(a,-1.10f,.76f),.13f,RampTeal*.65f);
                    }
                    if (Mathf.FloorToInt(at/5)%8==0)
                    {
                        float poleX = ramp ? -10.65f : 0, lampX = ramp ? -7 : -2.4f;
                        Color pole = new Color(.007f,.010f,.016f);
                        LineBox(RoadVertex(a,poleX,.4f),RoadVertex(a,poleX,8.5f),.055f,pole);
                        LineBox(RoadVertex(a,poleX,8.5f),RoadVertex(a,lampX,8.7f),.05f,pole);
                        Glow(RoadVertex(a,lampX,8.64f),.8f,ramp?Color.Lerp(Amber,RampTeal,.35f):Amber);
                        if (!ramp)
                        {
                            LineBox(RoadVertex(a,0,8.5f),RoadVertex(a,2.4f,8.7f),.05f,pole);
                            Glow(RoadVertex(a,2.4f,8.64f),.65f,Amber*.75f);
                        }
                    }
                    if (merging && Mathf.FloorToInt(at/5)%16==0)
                    {
                        // Rightward arrows in the acceleration lane explain the parallel merge.
                        HighwayRouteState.Frame tip=SectionFrame(road,Mathf.Min(at+7,road.To));
                        surfaces.Quad(RoadVertex(a,-8.1f,.035f),RoadVertex(a,-7.7f,.035f),RoadVertex(tip,-6.7f,.035f),RoadVertex(tip,-7.1f,.035f),new Color(.24f,.40f,.38f));
                        surfaces.Quad(RoadVertex(tip,-7.6f,.035f),RoadVertex(tip,-5.7f,.035f),RoadVertex(SectionFrame(road,Mathf.Min(at+10,road.To)),-6.4f,.035f),RoadVertex(SectionFrame(road,Mathf.Min(at+10,road.To)),-6.4f,.035f),new Color(.24f,.40f,.38f));
                    }
                    if (Mathf.FloorToInt(at/5)%16==0 && (road.Kind!=0 || a.Position.y>1) && (!ramp || at>100))
                    {
                        // Pier columns stop below the deck and never project into a carriageway.
                        // Omit the central crossing area so the original road stays clear below.
                        Vector3 entry = route.Branch(road.Branch,0,false).Position;
                        Vector3 entryRight = route.Branch(road.Branch,0,false).Right;
                        HighwayRouteState.Frame crossing=route.Crossing(road.Branch,0);
                        bool aboveCrossing=ramp && Mathf.Abs(Vector3.Dot(a.Position-crossing.Position,crossing.Right))<24;
                        if (Mathf.Abs(Vector3.Dot(a.Position-entry,entryRight))>22 && !aboveCrossing)
                            DrawPier(a.Point(ramp?-5.5f:0,-1));
                    }
                }
                a=b;
            }
        }
        private static float MergePavementEdge(HighwayRouteState.Frame ramp, HighwayRouteState.Frame crossing)
        {
            float offset=Vector3.Dot(ramp.Position-crossing.Position,crossing.Right);
            return Mathf.Clamp((-11-offset)/Vector3.Dot(ramp.Right,crossing.Right),-11,-.8f);
        }
        private void RoadGuard(HighwayRouteState.Frame a, HighwayRouteState.Frame b, float x, bool ramp)
        {
            Color color = ramp ? new Color(.016f,.037f,.040f) : new Color(.010f,.016f,.025f);
            surfaces.Quad(RoadVertex(a,x,.65f),RoadVertex(a,x,.8f),RoadVertex(b,x,.8f),RoadVertex(b,x,.65f),color);
            surfaces.Quad(RoadVertex(a,x,.30f),RoadVertex(a,x,.39f),RoadVertex(b,x,.39f),RoadVertex(b,x,.30f),color*.6f);
        }
        private void DrawPier(Vector3 top)
        {
            Vector3 basePoint = new Vector3(top.x,-8,top.z);
            Vector3 x = Vector3.right*1.2f, z=Vector3.forward*1.2f;
            Color c=new Color(.006f,.009f,.014f);
            surfaces.Quad(WorldPoint(basePoint-x-z),WorldPoint(basePoint+x-z),WorldPoint(top+x-z),WorldPoint(top-x-z),c);
            surfaces.Quad(WorldPoint(basePoint+x-z),WorldPoint(basePoint+x+z),WorldPoint(top+x+z),WorldPoint(top+x-z),c*.7f);
            surfaces.Quad(WorldPoint(basePoint+x+z),WorldPoint(basePoint-x+z),WorldPoint(top-x+z),WorldPoint(top+x+z),c);
            surfaces.Quad(WorldPoint(basePoint-x+z),WorldPoint(basePoint-x-z),WorldPoint(top-x-z),WorldPoint(top-x+z),c*.7f);
        }
        private bool NearVisibleRoad(Vector3 world, float clearance)
        {
            // A conservative XZ reserve keeps city blocks out of every branch, chosen or not.
            for(int i=0;i<reservedSegments.Count;i+=2)
            {
                Vector3 delta=reservedSegments[i+1]-reservedSegments[i]; delta.y=0;
                Vector3 offset=world-reservedSegments[i]; offset.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                if ((offset-delta*t).sqrMagnitude<clearance*clearance) return true;
            }
            return false;
        }

        private void BuildDirectionSigns(float s)
        {
            float fork=StartingDistance+HighwayRouteState.BranchAt(route.Decisions);
            for (int i=0;i<2;i++)
            {
                float at=fork-85+i*40;
                if (at<s-10 || at>s+DrawDistance) continue;
                RoadArrow(at,-7.5f,true,new Color(.10f,.43f,.36f));
                RoadArrow(at,-3.1f,false,new Color(.43f,.34f,.16f));
            }
            float signAt=fork-130;
            bool show=signAt-s>-30 && signAt-s<DrawDistance;
            exitSignText.gameObject.SetActive(show); mainSignText.gameObject.SetActive(show);
            if (!show) return;
            Quaternion signRotation=inverseHeading*Quaternion.LookRotation(Tangent(signAt),Vector3.up);
            exitSignText.transform.localPosition=Point(signAt-.08f,-7.25f,8.35f);
            mainSignText.transform.localPosition=Point(signAt-.08f,-2.25f,8.35f);
            exitSignText.transform.localRotation=mainSignText.transform.localRotation=signRotation;
            exitSignText.text="EXIT "+UpcomingBranchName; mainSignText.text=RouteName;
            Color metal=new Color(.026f,.037f,.048f), ink=new Color(.8f,1f,.9f);
            LineBox(Point(signAt,-10.9f,0),Point(signAt,-10.9f,9),.10f,metal);
            LineBox(Point(signAt,10.9f,0),Point(signAt,10.9f,9),.10f,metal);
            surfaces.Quad(Point(signAt,-10,6),Point(signAt,1,6),Point(signAt,1,9),Point(signAt,-10,9),new Color(.009f,.12f,.075f));
            LineBox(Point(signAt-.05f,-7,6.3f),Point(signAt-.05f,-8.3f,7.5f),.09f,ink);
            LineBox(Point(signAt-.05f,-8.3f,7.5f),Point(signAt-.05f,-7.6f,7.5f),.09f,ink);
            LineBox(Point(signAt-.05f,-8.3f,7.5f),Point(signAt-.05f,-8.3f,6.9f),.09f,ink);
            LineBox(Point(signAt-.05f,-3,6.3f),Point(signAt-.05f,-3,7.5f),.09f,ink);
            LineBox(Point(signAt-.05f,-3,7.5f),Point(signAt-.05f,-3.4f,7.1f),.09f,ink);
            LineBox(Point(signAt-.05f,-3,7.5f),Point(signAt-.05f,-2.6f,7.1f),.09f,ink);
        }
        private void RoadArrow(float at,float x,bool exit,Color color)
        {
            Strip(at-2.8f,at+.5f,x-.15f,x+.15f,.035f,color,color);
            float tipX=x+(exit?-1.1f:0);
            surfaces.Quad(Point(at+.2f,x-.8f,.035f),Point(at+.2f,x+.8f,.035f),
                Point(at+3.2f,tipX,.035f),Point(at+3.2f,tipX,.035f),color);
        }
    }
}
