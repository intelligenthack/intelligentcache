using System;

namespace IntelligentHack.IntelligentCache
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
