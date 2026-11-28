namespace PawTrack.Web.Infrastructure;

// The page served at /diagnostics/live: polls /api/diagnostics/load four times a second and shows connections in use
// against the pool, and cache-miss queries running against the limit, with the last 60 seconds as a chart.
public static class LoadDiagnosticsPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>PawTrack Live Load</title>
<style>
  :root { --bg:#fbfaf8; --fg:#1f2328; --muted:#5b636d; --line:#e3e0da; --card:#fff; --ok:#2f6f5e; --bad:#a2412f; --q:#3b5ba5; }
  @media (prefers-color-scheme: dark) { :root { --bg:#17191c; --fg:#e6e6e3; --muted:#9aa1a9; --line:#2d3136; --card:#1e2124; --ok:#7cc4ae; --bad:#e59a87; --q:#9db4ec; } }
  * { box-sizing:border-box; }
  body { margin:0; background:var(--bg); color:var(--fg); font:16px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif; }
  main { max-width:960px; margin:0 auto; padding:24px 16px; }
  h1 { font-size:1.3rem; margin:0 0 4px; } .sub { color:var(--muted); margin:0 0 20px; font-size:.9rem; }
  .tiles { display:grid; grid-template-columns:repeat(3,1fr); gap:12px; }
  .tile { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:16px; }
  .tile .label { color:var(--muted); font-size:.85rem; }
  .tile .value { font-size:3rem; font-weight:700; line-height:1.1; font-variant-numeric:tabular-nums; }
  .tile .of { font-size:1.2rem; color:var(--muted); font-weight:500; }
  .bar { height:8px; background:var(--line); border-radius:4px; margin-top:10px; overflow:hidden; }
  .bar div { height:100%; background:var(--ok); transition:width .2s; }
  .full .value, .full .bar div { color:var(--bad); background:var(--bad); } .full .value { background:none; }
  canvas { width:100%; height:220px; margin-top:16px; background:var(--card); border:1px solid var(--line); border-radius:10px; }
  .legend { color:var(--muted); font-size:.85rem; margin-top:6px; } .legend b { font-weight:600; }
  .peaks { color:var(--muted); font-size:.9rem; margin-top:12px; }
  @media (max-width:640px) { .tiles { grid-template-columns:1fr; } }
</style>
</head>
<body>
<main>
  <h1>PawTrack live load</h1>
  <p class="sub" id="limits">Loading…</p>
  <div class="tiles">
    <div class="tile" id="t-conn"><div class="label">Database connections in use</div><div class="value"><span id="conn">–</span> <span class="of">/ <span id="pool">–</span></span></div><div class="bar"><div id="conn-bar" style="width:0"></div></div></div>
    <div class="tile" id="t-free"><div class="label">Connections free for the pending queue</div><div class="value" id="free">–</div></div>
    <div class="tile"><div class="label">Cache-miss queries running</div><div class="value"><span id="miss">–</span> <span class="of" id="miss-limit"></span></div></div>
  </div>
  <canvas id="chart" width="1800" height="440"></canvas>
  <div class="legend"><b style="color:var(--bad)">—</b> connections in use &nbsp; <b style="color:var(--q)">—</b> cache-miss queries running &nbsp; dashed: pool size · last 60 seconds</div>
  <div class="peaks" id="peaks"></div>
</main>
<script>
  const history = [];
  const css = (name) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  const $ = (id) => document.getElementById(id);

  function draw(pool) {
    const c = $('chart'), g = c.getContext('2d'), w = c.width, h = c.height, pad = 30;
    g.clearRect(0, 0, w, h);
    const max = Math.max(pool, ...history.map((p) => Math.max(p.conn, p.miss)), 1) * 1.1;
    const y = (v) => h - pad - (v / max) * (h - 2 * pad);
    const x = (i) => pad + (i / 239) * (w - 2 * pad);
    g.setLineDash([10, 8]); g.strokeStyle = css('--muted'); g.lineWidth = 2;
    g.beginPath(); g.moveTo(pad, y(pool)); g.lineTo(w - pad, y(pool)); g.stroke(); g.setLineDash([]);
    g.fillStyle = css('--muted'); g.font = '26px system-ui'; g.fillText('pool ' + pool, pad + 6, y(pool) - 8);
    for (const [key, color] of [['miss', css('--q')], ['conn', css('--bad')]]) {
      g.strokeStyle = color; g.lineWidth = 4; g.beginPath();
      history.forEach((p, i) => (i ? g.lineTo(x(i), y(p[key])) : g.moveTo(x(i), y(p[key]))));
      g.stroke();
    }
  }

  async function tick() {
    try {
      const d = await (await fetch('/api/diagnostics/load', { cache: 'no-store' })).json();
      const conn = d.now.connectionsInUse, pool = d.poolSize, miss = d.now.cacheMissQueriesRunning;
      $('conn').textContent = conn; $('pool').textContent = pool;
      $('conn-bar').style.width = Math.min(100, (conn / pool) * 100) + '%';
      $('free').textContent = Math.max(0, pool - conn);
      $('t-conn').classList.toggle('full', conn >= pool); $('t-free').classList.toggle('full', conn >= pool);
      $('miss').textContent = miss;
      $('miss-limit').textContent = d.globalLimit == null ? '(no limit)' : '/ ' + d.globalLimit;
      $('limits').textContent = d.globalLimit == null
        ? 'No cache-miss limits configured · pool ' + pool
        : 'Limits: ' + d.globalLimit + ' cache-miss queries overall, ' + d.perClientLimit + ' per client · pool ' + pool;
      $('peaks').textContent = 'Peaks since the last test started: ' + d.peaks.peakConnections + ' connections, '
        + d.peaks.peakCacheMissQueries + ' cache-miss queries (' + d.peaks.peakClientCacheMissQueries + ' from the busiest client)';
      history.push({ conn, miss }); if (history.length > 240) history.shift();
      draw(pool);
    } catch (e) {
      $('limits').textContent = 'Waiting for the app…';
    }
  }
  tick(); setInterval(tick, 250);
</script>
</body>
</html>
""";
}
