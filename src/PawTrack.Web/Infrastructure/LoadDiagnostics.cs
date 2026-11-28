using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Npgsql;

namespace PawTrack.Web.Infrastructure;

// Counts what is actually running, independently of CacheMissLimiter, so a load test can compare peaks against the
// configured limits, and so the same numbers can be collected from code that has no limits at all.
public sealed class LoadDiagnostics : IDisposable
{
    private readonly ConcurrentDictionary<string, int> missesByClient = new();
    private int connections, peakConnections;
    private int misses, peakMisses;
    private int peakClientMisses;
    private string? busiestClient;
    private readonly Lock busiestLock = new();
    private readonly Lock sampleLock = new();
    private readonly MeterListener? poolListener;
    private readonly Timer? poolSampler;
    private long usedInSample;

    public LoadDiagnostics(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("LoadDiagnostics:Enabled"))
        {
            return;
        }

        // Connections in use come from Npgsql's own pool count rather than from counting opens and closes, which misses
        // connections Npgsql closes itself after a cancelled query. The pool's count can never exceed the pool size.
        poolListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Npgsql" && instrument.Name == "db.client.connection.count")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        poolListener.SetMeasurementEventCallback<int>((_, value, tags, _) => AddIfUsed(value, tags));
        poolListener.SetMeasurementEventCallback<long>((_, value, tags, _) => AddIfUsed(value, tags));
        poolListener.Start();
        poolSampler = new Timer(_ => SamplePool(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(20));
    }

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

    // The pool reports its count per state (used or idle) when asked, so each sample adds up the used ones.
    private void SamplePool()
    {
        if (!sampleLock.TryEnter())
        {
            return;
        }
        try
        {
            usedInSample = 0;
            poolListener!.RecordObservableInstruments();
            Volatile.Write(ref connections, (int)usedInSample);
            RaisePeak(ref peakConnections, (int)usedInSample);
        }
        finally
        {
            sampleLock.Exit();
        }
    }

    private void AddIfUsed(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key.EndsWith(".state", StringComparison.Ordinal) && tag.Value as string == "used")
            {
                usedInSample += value;
            }
        }
    }

    public void Dispose()
    {
        poolSampler?.Dispose();
        poolListener?.Dispose();
    }

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
