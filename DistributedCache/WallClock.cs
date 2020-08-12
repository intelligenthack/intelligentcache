using System;

namespace IntelligentHack.DistributedCache
{
    public sealed class WallClock : IClock
    {
        private WallClock() { }

        public static readonly IClock Instance = new WallClock();

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
