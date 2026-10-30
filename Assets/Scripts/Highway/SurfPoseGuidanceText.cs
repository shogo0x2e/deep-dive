using HighwaySurfing.Kinect;
using static HighwaySurfing.Kinect.SurfPoseDetector;

namespace HighwaySurfing
{
    public static class SurfPoseGuidanceText
    {
        public const string FontCharacters = "腕を上げてくださいもっと高くしてくださいもうちょっと高くしてください腕を下げてくださいもうちょっと低くしてくださいひじをのばしてくださいからだをまっすぐにしてください両腕が見えるようにひろげてくださいそのまま少しだけキープ肩の高さで、そのまま静止してください手とひじが見えるように、両腕をひろげてください左右にかたむけず、からだをまっすぐにしてくださいまっすぐのばしてください腕を体の前と後ろへ、大きくひろげてください肩の高さに合わせてください左腕右腕画面側の腕後ろ側の腕";
        public static string Title(Guidance guide)
        {
            switch (guide.Action)
            {
                case Adjustment.Ready: return "そのまま、少しだけキープ";
                case Adjustment.RaiseArms: return "腕を\n上げてください";
                case Adjustment.RaiseMore: return "もっと高く\nしてください";
                case Adjustment.RaiseSlightly: return "もうちょっと\n高くしてください";
                case Adjustment.LowerArms: return "腕を\n下げてください";
                case Adjustment.LowerSlightly: return "もうちょっと\n低くしてください";
                case Adjustment.ExtendArms: return "ひじを\nのばしてください";
                case Adjustment.StraightenBody: return "からだを\nまっすぐにしてください";
                case Adjustment.ShowArms: return "両腕が見えるように\nひろげてください";
                default: return "手を前と後ろに\nひろげてください";
            }
        }
        public static string Instruction(Guidance guide, KinectDebugRig rig)
        {
            switch (guide.Action)
            {
                case Adjustment.Ready: return "肩の高さで、\nそのまま静止してください";
                case Adjustment.ShowArms: return "手とひじが見えるように、\n両腕をひろげてください";
                case Adjustment.StraightenBody: return "左右にかたむけず、\nからだをまっすぐにしてください";
                case Adjustment.ExtendArms: return Target(guide, rig) + "のひじを、\nまっすぐのばしてください";
                case Adjustment.OpenArms: return "腕を体の前と後ろへ、\n大きくひろげてください";
                default: return Target(guide, rig) + "を、\n肩の高さに合わせてください";
            }
        }
        static string Target(Guidance guide, KinectDebugRig rig)
        {
            if (guide.Wrist == 0) return "両腕";
            if (rig != null && rig.TryGetJointPosition(7, out var l) && rig.TryGetJointPosition(14, out var r) &&
                SurfArmSpeedSolver.TrySelectFrontWrist(l, r, rig.ScreenReference, out int front))
                return guide.Wrist == front ? "画面側の腕" : "後ろ側の腕";
            return guide.Wrist == 7 ? "左腕" : "右腕";
        }
    }
}
