using System.Text.Json;
using StackExchange.Redis;

namespace PawTrack.Web.Infrastructure;

public interface ICacheStore
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);
}

public class RedisCache(IConnectionMultiplexer redis, ILogger<RedisCache> logger) : ICacheStore
{
    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        var db = redis.GetDatabase();

        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<T>(cached.ToString())!;
        }

        logger.LogDebug("Cache miss for {Key}", key);
        var value = await factory();
        await db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        return value;
    }
}
