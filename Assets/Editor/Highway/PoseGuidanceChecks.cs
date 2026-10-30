using System;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEngine;
using static HighwaySurfing.Kinect.SurfPoseDetector;

public static class PoseGuidanceChecks
{
    static readonly Vector3 Pelvis = new Vector3(0, .95f, 0), Chest = new Vector3(0, 1.43f, 0);
    static readonly Vector3 LS = Chest + Vector3.back * .2f, RS = Chest + Vector3.forward * .2f;
    static Vector3 Arm(float pitch, bool left) => (left ? Vector3.back : Vector3.forward) * (.5f * Mathf.Cos(pitch * Mathf.Deg2Rad)) + Vector3.up * (.5f * Mathf.Sin(pitch * Mathf.Deg2Rad));
    static Guidance Guide(float left, float right) => GetGuidance(Pelvis, Chest, LS, RS, LS + Arm(left, true), RS + Arm(right, false), Vector3.up, Vector3.right);
    public static string Run()
    {
        int count = 0;
        foreach (var test in new[] {
            (-80f, Adjustment.RaiseArms), (-45f, Adjustment.RaiseMore), (-25f, Adjustment.RaiseSlightly),
            (0f, Adjustment.Ready), (25f, Adjustment.LowerSlightly), (60f, Adjustment.LowerArms) })
        {
            var hint = Guide(test.Item1, test.Item1);
            Check(hint.Action == test.Item2 && hint.Wrist == 0, "Both arms: " + test.Item1); count++;
        }
        Check(Guide(0, -25).Wrist == 14 && Guide(0, -25).Action == Adjustment.RaiseSlightly, "Name low right wrist"); count++;
        Check(Guide(-80, 25).Wrist == 7 && Guide(-80, 25).Action == Adjustment.RaiseArms, "Opposite errors: correct worst arm"); count++;
        var shortArm = GetGuidance(Pelvis, Chest, LS, RS, LS + Vector3.back * .1f, RS + Arm(0, false), Vector3.up, Vector3.right);
        Check(shortArm.Action == Adjustment.ExtendArms && shortArm.Wrist == 7, "Folded arm is not a height correction"); count++;
        foreach (float yaw in new[] { -45f, 0, 45f })
        foreach (float lean in new[] { -25f, 0, 25f })
        foreach (float pitch in new[] { -60f, 0, 60f })
        {
            var turn = Quaternion.Euler(0, yaw, 0); var roll = Quaternion.AngleAxis(-lean, Vector3.forward);
            var q = roll * turn;
            bool riding = IsRiding(q * LS, q * RS, q * (LS + Arm(0, true)), q * (RS + Arm(pitch, false)), Vector3.up, Vector3.forward, lean);
            Check(riding, "Accelerator/brake and steering retain riding: " + yaw + "/" + lean + "/" + pitch); count++;
        }
        Check(!IsRiding(LS, RS, LS + Vector3.down * .5f, RS + Vector3.down * .5f, Vector3.up, Vector3.forward, 0), "Arms down exits riding"); count++;
        Check(!IsRiding(LS, RS, LS + Vector3.forward * .3f, RS + Vector3.back * .3f, Vector3.up, Vector3.forward, 0), "Closed arms exits riding"); count++;
        foreach (Adjustment action in Enum.GetValues(typeof(Adjustment)))
        {
            var guide = new Guidance(action, 7);
            string text = SurfPoseGuidanceText.Title(guide) + SurfPoseGuidanceText.Instruction(guide, null);
            foreach (char c in text) Check(char.IsControl(c) || SurfTutorialView.FontCharacters.Contains(c.ToString()), "Font inventory: " + c);
        }
        count++;
        return $"PASS: {count} pose guidance and riding posture checks";
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
}
