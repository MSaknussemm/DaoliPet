using System;
using System.Collections.Generic;
using System.Windows;

namespace DaoliPet
{
    public struct DragSample
    {
        public double Time; public Point Position;
        public DragSample(double time, Point position) { Time = time; Position = position; }
    }

    // Screen-space DIP units, real seconds; independent of animation playback speed.
    public sealed class ThrowBody
    {
        public const double Gravity = 1400.0, Restitution = 0.78;
        public double X, Y, VX, VY;
        public bool Active;
        public int BounceCount;
        public double FloorImpactSpeed;

        public static Vector ReleaseVelocity(IList<DragSample> samples, double now)
        {
            if (samples.Count < 2 || now - samples[samples.Count - 1].Time > 0.15) return new Vector();
            int first = 0;
            while (first < samples.Count - 1 && samples[first].Time < now - 0.12) first++;
            DragSample a = samples[first], b = samples[samples.Count - 1];
            double dt = b.Time - a.Time;
            if (dt < 0.02) return new Vector();
            Vector mean = (b.Position - a.Position) / dt;
            double magnitude = mean.Length;
            if (magnitude < 500) return new Vector();
            // Soft cap retains differences between fast and very fast throws.
            return mean * (4800 * (1 - Math.Exp(-magnitude / 4800)) / magnitude);
        }

        public void Launch(double x, double y, Vector velocity)
        {
            X = x; Y = y; VX = velocity.X; VY = velocity.Y;
            BounceCount = 0; FloorImpactSpeed = 0; Active = velocity.Length > 0;
        }
        public void Stop() { Active = false; VX = VY = 0; }

        public void Step(double elapsed, Rect bounds)
        {
            FloorImpactSpeed = 0;
            if (!Active || elapsed <= 0) return;
            // Fixed upper step size avoids tunnelling and unstable landing at low frame rates.
            double remaining = Math.Min(elapsed, 0.10);
            while (remaining > 0.0000001 && Active) {
                double dt = Math.Min(remaining, 1.0 / 240.0); remaining -= dt;
                bool grounded = Y >= bounds.Bottom - 0.001 && VY == 0;
                X += VX * dt;
                if (!grounded) { Y += VY * dt + 0.5 * Gravity * dt * dt; VY += Gravity * dt; }
                if (X < bounds.Left) { X = bounds.Left; if (VX < 0) { VX = -VX * Restitution; BounceCount++; } }
                if (X > bounds.Right) { X = bounds.Right; if (VX > 0) { VX = -VX * Restitution; BounceCount++; } }
                if (Y < bounds.Top) { Y = bounds.Top; if (VY < 0) { VY = -VY * Restitution; BounceCount++; } }
                if (Y >= bounds.Bottom) {
                    Y = bounds.Bottom;
                    if (VY > 0) { FloorImpactSpeed = Math.Max(FloorImpactSpeed, VY); VY = VY < 100 ? 0 : -VY * Restitution; VX *= 0.88; BounceCount++; }
                    if (VY == 0) {
                        VX *= Math.Exp(-2.5 * dt);
                        if (Math.Abs(VX) < 15) Stop();
                    }
                }
            }
        }
    }
}
