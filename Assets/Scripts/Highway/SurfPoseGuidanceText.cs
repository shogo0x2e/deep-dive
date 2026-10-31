using HighwaySurfing.Kinect;
using static HighwaySurfing.Kinect.SurfPoseDetector;

namespace HighwaySurfing
{
    public static class SurfPoseGuidanceText
    {
        public const string FontCharacters = "腕を上げてくださいもっと高くしてくださいもうちょっと高くしてください腕を下げてくださいもうちょっと低くしてくださいひじをのばしてくださいからだをまっすぐにしてください両腕が見えるようにひろげてくださいそのまま少しだけキープ肩の高さで、そのまま静止してください手とひじが見えるように、両腕をひろげてください左右にかたむけず、からだをまっすぐにしてくださいまっすぐのばしてください腕を体の前と後ろへ、大きくひろげてください肩の高さに合わせてください左腕右腕画面側の腕後ろ側の腕そのまま維持してください1秒";
        public const string HoldTitle = "そのまま\n維持してください";
        public static Guidance Simplify(Guidance guide)
        {
            switch (guide.Action)
            {
                case Adjustment.RaiseMore:
                case Adjustment.RaiseSlightly: return new Guidance(Adjustment.RaiseArms);
                case Adjustment.LowerSlightly: return new Guidance(Adjustment.LowerArms);
                case Adjustment.ExtendArms:
                case Adjustment.ShowArms: return new Guidance(Adjustment.OpenArms);
                default: return new Guidance(guide.Action);
            }
        }
        public static string Title(Guidance guide)
        {
            switch (Simplify(guide).Action)
            {
                case Adjustment.Ready: return HoldTitle;
                case Adjustment.RaiseArms: return "腕を\n上げてください";
                case Adjustment.LowerArms: return "腕を\n下げてください";
                case Adjustment.StraightenBody: return "からだを\nまっすぐにしてください";
                default: return "手を前と後ろに\nひろげてください";
            }
        }
        public static string Instruction(Guidance guide, KinectDebugRig rig)
        {
            switch (Simplify(guide).Action)
            {
                case Adjustment.Ready: return rig != null && rig.RelaxedStartPose ?
                    "そのまま1秒、\n維持してください" : "肩の高さで、\nそのまま静止してください";
                case Adjustment.StraightenBody: return "左右にかたむけず、\nからだをまっすぐにしてください";
                case Adjustment.OpenArms: return "腕を体の前と後ろへ、\n大きくひろげてください";
                default: return "両腕を、\n肩の高さに合わせてください";
            }
        }
    }
}
