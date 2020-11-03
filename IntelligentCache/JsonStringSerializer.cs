using Newtonsoft.Json;

namespace IntelligentHack.IntelligentCache
{

    public partial class RedisCache
    {
        public class JsonStringSerializer : IStringSerializer
        {
            public T Deserialize<T>(string value)
            {
                return JsonConvert.DeserializeObject<T>(value);
            }

            public string Serialize<T>(T instance)
            {
                return JsonConvert.SerializeObject(instance);
            }
        }

    }
}
