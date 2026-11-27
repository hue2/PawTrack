using Microsoft.Extensions.Caching.Hybrid;

namespace PawTrack.Web.Infrastructure;

public interface IHybridCacheStore
{
    ValueTask<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None, CancellationToken token = default);
}
public class HybridCacheStore(HybridCache hybridCache) : IHybridCacheStore
{
    public ValueTask<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None, CancellationToken token = default)
    {
        var options = new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl, Flags = flags };
        return hybridCache.GetOrCreateAsync(key, factory, options, tags: null, token);
    }
}