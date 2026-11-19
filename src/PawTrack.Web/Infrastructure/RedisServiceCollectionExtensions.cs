using StackExchange.Redis;

namespace PawTrack.Web.Infrastructure;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        services.AddScoped<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PawTrack.Redis");
            try
            {
                return ConnectionMultiplexer.Connect(connectionString);
            }
            catch (RedisConnectionException ex)
            {
                logger.LogError(ex, "Redis connection exhausted: unable to open a connection to {Endpoint}", connectionString);
                throw;
            }
        });

        services.AddScoped<ICacheStore, RedisCache>();

        return services;
    }
}
