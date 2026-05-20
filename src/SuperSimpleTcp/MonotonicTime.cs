namespace SuperSimpleTcp
{
    using System;
    using System.Diagnostics;

    internal static class MonotonicTime
    {
        private static readonly double TimestampTicksPerMillisecond = Stopwatch.Frequency / 1000d;

        internal static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static long FromMilliseconds(int milliseconds)
        {
            if (milliseconds <= 0) return 0;
            return (long)(milliseconds * TimestampTicksPerMillisecond);
        }

        internal static bool HasElapsed(long sinceTimestamp, int milliseconds)
        {
            if (milliseconds <= 0) return false;
            return GetTimestamp() - sinceTimestamp >= FromMilliseconds(milliseconds);
        }

        internal static int RemainingMilliseconds(long sinceTimestamp, int timeoutMilliseconds)
        {
            if (timeoutMilliseconds <= 0) return 0;

            long elapsedTicks = GetTimestamp() - sinceTimestamp;
            double elapsedMilliseconds = elapsedTicks / TimestampTicksPerMillisecond;
            int remaining = timeoutMilliseconds - (int)Math.Floor(elapsedMilliseconds);
            return remaining > 0 ? remaining : 0;
        }
    }
}
