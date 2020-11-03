namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> based on Redis.
    /// </summary>

    public interface IStringSerializer
    {
        string Serialize<T>(T instance);
        T Deserialize<T>(string value);
    }
}
