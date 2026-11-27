using StackExchange.Redis;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace PawTrack.Web.Infrastructure;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(connectionString);
        });
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>((options, multiplexer) =>
            options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
        services.AddSingleton<IHybridCacheStore, HybridCacheStore>();
        services.AddHybridCache();
        return services;
    }
}
