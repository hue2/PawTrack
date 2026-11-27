using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace PawTrack.Web.Infrastructure;

// Counts what is actually running, independently of CacheMissLimiter, so a load test can compare peaks against the
// configured limits, and so the same numbers can be collected from code that has no limits at all.
public sealed class LoadDiagnostics
{
    private readonly ConcurrentDictionary<string, int> missesByClient = new();
    private int connections, peakConnections;
    private int misses, peakMisses;
    private int peakClientMisses;
    private string? busiestClient;
    private readonly Lock busiestLock = new();

    public void ConnectionOpened() => RaisePeak(ref peakConnections, Interlocked.Increment(ref connections));
    public void ConnectionClosed() => Interlocked.Decrement(ref connections);

    // Call once a cache-miss query is allowed to run (after any limiter), and dispose when it finishes.
    public IDisposable CacheMissStarted(string client)
    {
        RaisePeak(ref peakMisses, Interlocked.Increment(ref misses));
        var forClient = missesByClient.AddOrUpdate(client, 1, (_, n) => n + 1);
        lock (busiestLock)
        {
            if (forClient > peakClientMisses)
            {
                peakClientMisses = forClient;
                busiestClient = client;
            }
        }
        return new Finished(this, client);
    }

    // Peaks restart from what is running now, so a test can reset them in setup without counting earlier traffic.
    public void Reset()
    {
        Volatile.Write(ref peakConnections, Volatile.Read(ref connections));
        Volatile.Write(ref peakMisses, Volatile.Read(ref misses));
        lock (busiestLock)
        {
            peakClientMisses = 0;
            busiestClient = null;
        }
    }

    public LoadSnapshot Snapshot() => new(peakConnections, peakMisses, peakClientMisses, busiestClient);

    private static void RaisePeak(ref int peak, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref peak)) && Interlocked.CompareExchange(ref peak, value, current) != current)
        {
        }
    }

    private sealed class Finished(LoadDiagnostics owner, string client) : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Decrement(ref owner.misses);
            owner.missesByClient.AddOrUpdate(client, 0, (_, n) => n - 1);
        }
    }
}

public record LoadSnapshot(int PeakConnections, int PeakCacheMissQueries, int PeakClientCacheMissQueries, string? BusiestClient);

// Counts database connections the app has open, including ones that never go through the cache, like the pending queue.
public sealed class ConnectionUsageInterceptor(LoadDiagnostics diagnostics) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        diagnostics.ConnectionOpened();

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        diagnostics.ConnectionOpened();
        return Task.CompletedTask;
    }

    // Counted as closed when closing starts, before the connection goes back to the pool, so a request that takes it
    // straight away can't be counted while this one still is.
    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        diagnostics.ConnectionClosed();
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        diagnostics.ConnectionClosed();
        return ValueTask.FromResult(result);
    }
}

public static class LoadDiagnosticsExtensions
{
    // Only for load tests: set LoadDiagnostics:Enabled to expose the peaks. The numbers aren't sensitive, but there's no
    // reason for production to serve them.
    public static IEndpointRouteBuilder MapLoadDiagnostics(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("LoadDiagnostics:Enabled"))
        {
            return app;
        }

        var poolSize = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PawTrack")).MaxPoolSize;
        // Read from configuration rather than CacheMissLimits, so this file also builds against code without the limits,
        // where both are null and a test reports "none".
        var limits = configuration.GetSection("CacheMissLimits");
        var globalLimit = limits.GetValue<int?>("Global");
        var perClientLimit = limits.GetValue<int?>("PerClient");
        app.MapGet("/api/diagnostics/load", (LoadDiagnostics diagnostics) =>
            Results.Ok(new { poolSize, globalLimit, perClientLimit, peaks = diagnostics.Snapshot() }));
        app.MapPost("/api/diagnostics/load/reset", (LoadDiagnostics diagnostics) =>
        {
            diagnostics.Reset();
            return Results.NoContent();
        });
        return app;
    }
}
