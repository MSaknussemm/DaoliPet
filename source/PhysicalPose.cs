using System;

namespace DaoliPet
{
    // Secondary animation uses real seconds, independently of idle animation speed.
    public sealed class PhysicalPose
    {
        public double Hang, Jelly, FallStretch;
        public double ScaleX = 1, ScaleY = 1, OffsetY;
        double jellyVelocity, fit = 1, grip = 0.2;
        bool held;

        public void Grab(double normalizedY)
        {
            held = true; grip = Math.Max(0.1, Math.Min(0.9, normalizedY));
        }
        public void Release() { held = false; }
        public void Impact(double speed)
        {
            if (speed < 100) return;
            jellyVelocity = Math.Min(11, jellyVelocity + 9 * Math.Min(1, speed / 1500));
            FallStretch *= 0.25;
        }
        public void Step(double seconds, bool airborne, double verticalSpeed)
        {
            double remaining = Math.Min(Math.Max(0, seconds), 0.1);
            while (remaining > 0.000001) {
                double dt = Math.Min(remaining, 1.0 / 240); remaining -= dt;
                double targetHang = held ? 0.35 * (1 - grip) : 0;
                Hang += (targetHang - Hang) * (1 - Math.Exp(-12 * dt));
                jellyVelocity += (-145 * Jelly - 11 * jellyVelocity) * dt;
                Jelly += jellyVelocity * dt;
                if (Jelly > 0.95) { Jelly = 0.95; jellyVelocity = Math.Min(0, jellyVelocity); }
                if (Jelly < -0.55) { Jelly = -0.55; jellyVelocity = Math.Max(0, jellyVelocity); }
                double targetFall = airborne && !held ? Math.Min(0.30, Math.Max(0, verticalSpeed) / 4200) : 0;
                FallStretch += (targetFall - FallStretch) * (1 - Math.Exp(-16 * dt));
                double targetFit = held ? Math.Min(1, (0.98 - grip) / (0.94 - grip) / (1 + targetHang)) : 1;
                fit += (targetFit - fit) * (1 - Math.Exp(-18 * dt));
                ScaleX = fit * (1 - 0.22 * Hang - 0.34 * FallStretch + 0.22 * Jelly);
                ScaleY = fit * (1 + Hang + FallStretch - 0.32 * Jelly);
                double targetOffset = held ? (grip - 0.5) * (1 - ScaleY) : airborne ? -0.36 * Math.Max(0, ScaleY - 1) : 0.43 * (1 - ScaleY);
                OffsetY += (targetOffset - OffsetY) * (1 - Math.Exp(-20 * dt));
            }
        }
    }
}
