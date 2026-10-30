using System;
using HighwaySurfing.Kinect;
public static class KinectSelectionChecks
{
    public static string Run()
    {
        int count = 0;
        var s = new KinectBodySelector();
        var a = new[] { new KinectBodySelector.Candidate(10, 3), new KinectBodySelector.Candidate(20, 1.5f) };
        Check(s.Step(a, 2, 0) == 1 && s.SelectedId == 20 && s.SelectionChanged, "nearest of multiple bodies"); count++;
        Array.Reverse(a);
        Check(s.Step(a, 2, .1f) == 0 && !s.SelectionChanged, "body order does not change selection"); count++;
        a[1] = new KinectBodySelector.Candidate(10, 1.4f);
        s.Step(a, 2, 1); s.Step(a, 2, 2);
        Check(s.SelectedId == 20, "small depth jitter does not switch"); count++;
        a[1] = new KinectBodySelector.Candidate(10, 1);
        s.Step(a, 2, 3); s.Step(a, 2, 3.2f);
        Check(s.SelectedId == 20, "closer passerby requires dwell"); count++;
        s.Step(a, 2, 3.6f);
        Check(s.SelectedId == 10 && s.SelectionChanged, "closer person takes over after dwell"); count++;
        a[0] = new KinectBodySelector.Candidate(20, 1.5f); a[1] = new KinectBodySelector.Candidate(10, 2.2f, 0);
        s.Step(a, 2, 4); s.Step(a, 2, 4.7f);
        Check(s.SelectedId == 10 && !s.SelectionChanged, "joint confidence loss does not steal identity"); count++;
        Check(s.Step(a, 1, 4.8f) == -1 && s.SelectedId == 10, "missing target gives no body during grace"); count++;
        Check(s.Step(a, 1, 5.6f) == 0 && s.SelectedId == 20 && s.SelectionChanged, "lost target reacquires nearest"); count++;
        Check(s.Step(a, 0, 6.5f) == -1 && !s.HasSelection && s.SelectionChanged, "empty frame releases expired identity"); count++;
        a[0] = new KinectBodySelector.Candidate(1, float.NaN); a[1] = new KinectBodySelector.Candidate(2, .2f, 1);
        Check(s.Step(a, 2, 7) == -1, "invalid or low-confidence nearest ignored"); count++;
        a[0] = new KinectBodySelector.Candidate(1, 2); a[1] = new KinectBodySelector.Candidate(2, 1);
        s.Step(a, 1, 8); s.Step(a, 2, 8.1f); s.Step(a, 1, 8.2f); s.Step(a, 2, 8.6f);
        Check(s.SelectedId == 1, "interrupted challenger restarts dwell"); count++;
        s.Reset();
        Check(!s.HasSelection, "explicit reset clears identity"); count++;
        s.Step(a, 1, 10);
        s.Step(a, 2, 11, lockSelection: true); s.Step(a, 2, 12, lockSelection: true);
        Check(s.SelectedId == 1, "locked identity ignores closer candidate"); count++;
        a[0] = new KinectBodySelector.Candidate(2, 1);
        Check(s.Step(a, 1, 20, lockSelection: true) == -1 && s.SelectedId == 1,
            "locked missing identity never takes another person"); count++;
        return $"PASS: {count} nearest-person selection checks";
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
