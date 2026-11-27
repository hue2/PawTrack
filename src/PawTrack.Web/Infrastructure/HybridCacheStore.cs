using Microsoft.Extensions.Caching.Hybrid;

namespace PawTrack.Web.Infrastructure;

public interface IHybridCacheStore
{
    ValueTask<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None, CancellationToken token = default);
}
public class HybridCacheStore(HybridCache hybridCache, LoadDiagnostics diagnostics, IHttpContextAccessor httpContextAccessor) : IHybridCacheStore
{
    public ValueTask<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None, CancellationToken token = default)
    {
        var options = new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl, Flags = flags };

        // The factory only runs on a miss, once per key however many requests are waiting. The client is captured here
        // because the factory may not run on that request's async flow.
        var client = ClientId();
        return hybridCache.GetOrCreateAsync(key, async ct =>
        {
            using var running = diagnostics.CacheMissStarted(client);
            return await factory(ct);
        }, options, tags: null, token);
    }

    // Clients are identified by IP address, so everyone behind one office address counts as one client. Behind a load
    // balancer or reverse proxy this is the proxy's address unless forwarded headers are configured.
    private string ClientId() => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "local";
}
