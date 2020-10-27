using System;

namespace IntelligentHack.IntelligentCache
{
    public sealed class WallClock : IClock
    {
        private WallClock() { }

        public static readonly IClock Instance = new WallClock();

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
