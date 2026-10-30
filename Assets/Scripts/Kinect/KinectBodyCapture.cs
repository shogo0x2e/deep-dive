using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace HighwaySurfing.Kinect
{
    // Depth + body only. No RGB, hand inference, or recording.
    internal sealed class KinectBodyCapture
    {
        internal sealed class Body { internal uint Id; internal AzureKinectNative.Joint[] Joints; }
        internal sealed class Snapshot
        {
            internal Body[] Bodies;
            internal ulong DeviceMicroseconds;
            internal long ReceivedTicks, FrameNumber;
        }
        readonly string directory;
        readonly int fps, backend;
        readonly object gate = new object();
        Thread thread;
        volatile bool stopping;
        IntPtr tracker;
        bool shutdown;
        Snapshot latest;
        string status = "Stopped";
        long captures, results, dropped;
        internal bool IsAlive => thread != null && thread.IsAlive;
        internal string Status { get { lock (gate) return status; } }
        internal Snapshot Latest { get { lock (gate) return latest; } }
        internal long Captures => Interlocked.Read(ref captures);
        internal long Results => Interlocked.Read(ref results);
        internal long Dropped => Interlocked.Read(ref dropped);
        internal KinectBodyCapture(string directory, int fps, int backend)
        { this.directory = directory; this.fps = fps; this.backend = backend; }
        void SetStatus(string message) { lock (gate) status = message; }
        internal void Start()
        {
            if (IsAlive) return;
            stopping = false;
            thread = new Thread(Run) { IsBackground = true, Name = "Highway Kinect Body" };
            thread.Start();
        }
        internal bool Stop(int waitMs = 2000)
        {
            stopping = true;
            lock (gate)
            {
                latest = null;
                if (tracker != IntPtr.Zero && !shutdown)
                { AzureKinectNative.k4abt_tracker_shutdown(tracker); shutdown = true; }
            }
            bool ended = thread == null || thread.Join(waitMs);
            if (!ended) SetStatus("Stopping: waiting for native initialization");
            return ended;
        }
        void Run()
        {
            IntPtr device = IntPtr.Zero;
            try
            {
                SetStatus("Loading SDK");
                AzureKinectNative.Load(directory);
                if (stopping) return;
                if (AzureKinectNative.k4a_device_get_installed_count() == 0)
                    throw new InvalidOperationException("No Kinect. Check power and USB 3.");
                if (AzureKinectNative.k4a_device_open(0, out device) != 0)
                    throw new InvalidOperationException("Kinect busy. Close other Kinect viewers.");
                var config = new AzureKinectNative.DeviceConfiguration { DepthMode = 2, CameraFps = fps == 30 ? 2 : 1 };
                if (AzureKinectNative.k4a_device_start_cameras(device, ref config) != 0)
                    throw new InvalidOperationException("Depth camera could not start.");
                if (AzureKinectNative.k4a_device_get_calibration(device, config.DepthMode, 0, out var calibration) != 0)
                    throw new InvalidOperationException("Depth calibration unavailable.");
                SetStatus("Creating body tracker...");
                IntPtr model = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(Path.Combine(directory, "dnn_model_2_0_lite_op11.onnx"));
                IntPtr created;
                try
                {
                    var settings = new AzureKinectNative.TrackerConfiguration { ProcessingMode = backend, ModelPath = model };
                    if (AzureKinectNative.k4abt_tracker_create(ref calibration, settings, out created) != 0)
                        throw new InvalidOperationException("Body tracker failed. Check SDK/GPU runtime.");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(model); }
                lock (gate) { tracker = created; shutdown = false; }
                int pending = 0;
                while (!stopping)
                {
                    for (int i = 0; i < 2 && !stopping; i++)
                    {
                        int result = AzureKinectNative.k4abt_tracker_pop_result(created, out var frame, 0);
                        if (result == 2) break;
                        if (result != 0) { if (stopping) break; throw new InvalidOperationException("Body tracker stopped."); }
                        try
                        {
                            pending = Math.Max(0, pending - 1);
                            uint count = AzureKinectNative.k4abt_frame_get_num_bodies(frame);
                            var bodies = new Body[Math.Min(count, 6u)];
                            for (uint b = 0; b < bodies.Length; b++)
                            {
                                if (AzureKinectNative.k4abt_frame_get_body_skeleton(frame, b, out var skeleton) != 0)
                                    throw new InvalidOperationException("Could not read skeleton.");
                                bodies[b] = new Body { Id = AzureKinectNative.k4abt_frame_get_body_id(frame, b), Joints = skeleton.Joints };
                            }
                            var snapshot = new Snapshot
                            {
                                Bodies = bodies, DeviceMicroseconds = AzureKinectNative.k4abt_frame_get_device_timestamp_usec(frame),
                                ReceivedTicks = Stopwatch.GetTimestamp(), FrameNumber = Interlocked.Increment(ref results)
                            };
                            lock (gate) latest = snapshot;
                            SetStatus(count > 0 ? "Depth + skeleton live" : "No body in view");
                        }
                        finally { AzureKinectNative.k4abt_frame_release(frame); }
                    }
                    int captureResult = AzureKinectNative.k4a_device_get_capture(device, out var capture, 50);
                    if (captureResult == 2) continue;
                    if (captureResult != 0) { if (stopping) break; throw new InvalidOperationException("Depth disconnected."); }
                    try
                    {
                        Interlocked.Increment(ref captures);
                        if (pending >= 2) { Interlocked.Increment(ref dropped); continue; }
                        int queued = AzureKinectNative.k4abt_tracker_enqueue_capture(created, capture, 0);
                        if (queued == 0) pending++;
                        else if (queued == 2) Interlocked.Increment(ref dropped);
                        else if (!stopping) throw new InvalidOperationException("Body input rejected.");
                    }
                    finally { AzureKinectNative.k4a_capture_release(capture); }
                }
                SetStatus("Stopped");
            }
            catch (Exception exception) { SetStatus(exception.Message); }
            finally
            {
                lock (gate)
                {
                    latest = null;
                    if (tracker != IntPtr.Zero)
                    {
                        if (!shutdown) AzureKinectNative.k4abt_tracker_shutdown(tracker);
                        AzureKinectNative.k4abt_tracker_destroy(tracker);
                        tracker = IntPtr.Zero;
                    }
                }
                if (device != IntPtr.Zero)
                { AzureKinectNative.k4a_device_stop_cameras(device); AzureKinectNative.k4a_device_close(device); }
            }
        }
    }
}
