"""Summarize actual race frames, excluding the separately flagged loading phase."""
import csv
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / 'Logs' / 'FinalAudit'
summary = []
for path in sorted(folder.glob('*_Circuit_0*_Final24.csv')):
    with path.open(encoding='utf-8-sig', newline='') as handle:
        all_rows = list(csv.DictReader(handle))
    rows = [r for r in all_rows if r['race_ready'] == 'True']
    if not rows:
        raise RuntimeError(f'No race frames: {path}')
    values = sorted(float(r['wall_ms']) for r in rows)
    def percentile(p):
        return values[min(len(values)-1, math.floor((len(values)-1)*p))]
    phase, _, number, mode, label = path.stem.split('_')
    summary.append({
        'phase': phase, 'circuit': 'Circuit_' + number, 'mode': mode,
        'frames': len(rows), 'average_fps': round(1000*len(values)/sum(values), 2),
        'p50_ms': round(percentile(.5), 3), 'p95_ms': round(percentile(.95), 3),
        'p99_ms': round(percentile(.99), 3), 'max_ms': max(values),
        'over_50ms': sum(v > 50 for v in values),
        'unfocused': sum(r['focused'] != 'True' for r in rows),
        'gc_collections': sum(int(r['gc_collections']) for r in rows),
        'max_checkpoint': max(int(r['checkpoint']) for r in rows),
        'max_speed_kph': max(float(r['speed_kph']) for r in rows),
    })
with (folder/'frame-summary.csv').open('w', encoding='utf-8', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=summary[0].keys())
    writer.writeheader()
    writer.writerows(summary)
(folder/'frame-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
table = ['| Circuito | Modo | FPS medios antes / después | P95 ms antes / después | Frames >50 ms antes / después |',
         '|---|---|---:|---:|---:|']
for circuit in ('Circuit_01', 'Circuit_02', 'Circuit_03'):
    for mode in ('1P', '2P'):
        before = next((r for r in summary if (r['phase'],r['circuit'],r['mode']) == ('Before',circuit,mode)), None)
        after = next((r for r in summary if (r['phase'],r['circuit'],r['mode']) == ('After',circuit,mode)), None)
        def pair(key):
            return ' / '.join((str(r[key])+('*' if r['unfocused'] else '')) if r else 'pendiente' for r in (before,after))
        table.append(f"| {circuit} | {mode} | {pair('average_fps')} | {pair('p95_ms')} | {pair('over_50ms')} |")
table.append('\n* Captura con frames sin foco: no usar como comparación estricta de rendimiento.')
(folder/'frame-summary.md').write_text('\n'.join(table)+'\n', encoding='utf-8')
print('\n'.join(table))
print('Race phases only; 25s samples, diagnostic 120fps cap; not full-lap or GPU bottleneck proof.')
