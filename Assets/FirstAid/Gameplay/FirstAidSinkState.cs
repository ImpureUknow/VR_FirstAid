using System;

namespace FirstAid
{
    internal sealed class FirstAidSinkState
    {
        public const float RequiredWashSeconds = 2f;

        private readonly float[] washSeconds = new float[2];
        private readonly bool[] washed = new bool[2];
        private readonly bool[] gloves = new bool[2];

        public bool TapOpen { get; private set; }
        public bool BothProtected => gloves[0] && gloves[1];

        public void ToggleTap() => TapOpen = !TapOpen;

        // Hand 0 is left; hand 1 is right. Progress is normalized from 0 to 1.
        public bool IsWashed(int hand)
        {
            ValidateHand(hand);
            return washed[hand];
        }

        public bool HasGlove(int hand)
        {
            ValidateHand(hand);
            return gloves[hand];
        }

        public float WashProgress(int hand)
        {
            ValidateHand(hand);
            return washSeconds[hand] / RequiredWashSeconds;
        }

        // Call while this hand is in the wash area; closing the tap pauses progress.
        public bool AdvanceWash(int hand, float seconds)
        {
            ValidateHand(hand);
            if (!TapOpen || washed[hand] || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return false;

            float remaining = RequiredWashSeconds - washSeconds[hand];
            if (seconds < remaining)
            {
                washSeconds[hand] += seconds;
                return false;
            }

            washSeconds[hand] = RequiredWashSeconds;
            washed[hand] = true;
            return true;
        }

        public bool TryWearGlove(int hand)
        {
            ValidateHand(hand);
            if (!washed[hand] || gloves[hand])
                return false;

            gloves[hand] = true;
            return true;
        }

        // The tap is separate room state and is left at its current setting.
        public void ResetHands()
        {
            Array.Clear(washSeconds, 0, washSeconds.Length);
            Array.Clear(washed, 0, washed.Length);
            Array.Clear(gloves, 0, gloves.Length);
        }

        private static void ValidateHand(int hand)
        {
            if (hand < 0 || hand > 1)
                throw new ArgumentOutOfRangeException(nameof(hand), hand, "Use hand 0 (left) or 1 (right).");
        }
    }
}
