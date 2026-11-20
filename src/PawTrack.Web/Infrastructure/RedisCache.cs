using System.Text.Json;
using StackExchange.Redis;

namespace PawTrack.Web.Infrastructure;

public interface ICacheStore
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);
}

public class RedisCache(IServiceProvider services, ILogger<RedisCache> logger) : ICacheStore
{
    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        IDatabase db;
        try
        {
            // Resolved here rather than injected so a failed connection falls back to the database instead of failing the request.
            db = services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            var cached = await db.StringGetAsync(key);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<T>(cached.ToString())!;
            }
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            logger.LogWarning("Redis unavailable, reading {Key} from the database: {Message}", key, ex.Message);
            return await factory();
        }

        logger.LogDebug("Cache miss for {Key}", key);
        var value = await factory();
        try
        {
            await db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            logger.LogWarning("Redis unavailable, not caching {Key}: {Message}", key, ex.Message);
        }

        return value;
    }
}
