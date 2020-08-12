using System;

namespace IntelligentHack.DistributedCache
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
