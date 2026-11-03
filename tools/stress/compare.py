#!/usr/bin/env python3
"""Compares two stress runs (summary.json) and prints a Markdown table (pt-BR).

Usage: compare.py benchmarks/v0 benchmarks/v1
"""

import json
import os
import sys


def load(directory):
    with open(os.path.join(directory, "summary.json"), encoding="utf-8") as f:
        return json.load(f)


def delta(old, new, decimals=1):
    if old is None or new is None:
        return "—"
    diff = new - old
    rel = f" ({diff / old:+.1%})" if old else ""
    return f"{diff:+,.{decimals}f}{rel}"


def per_rate(summary):
    """Worst passing-or-not attempt per rate: highest p95 among attempts with a report."""
    rates = {}
    for step in summary["steps"]:
        if "latencyMs" not in step:
            continue
        current = rates.get(step["rate"])
        if current is None or step["latencyMs"]["p95"] > current["latencyMs"]["p95"]:
            rates[step["rate"]] = step
    return rates


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: compare.py benchmarks/<base> benchmarks/<candidate>")
    base, cand = load(sys.argv[1]), load(sys.argv[2])
    a, b = base["version"], cand["version"]
    out = print

    warnings = []
    for key in ("cpuPinning", "parameters", "machine"):
        if base["environment"][key] != cand["environment"][key]:
            warnings.append(f"`{key}` diverge entre {a} e {b}: a comparação pode não ser justa.")

    out(f"# Comparação de capacidade: {a} → {b}")
    out("")
    for w in warnings:
        out(f"> ⚠️ {w}")
    if warnings:
        out("")
    out(f"| Métrica | {a} | {b} | Diferença |")
    out("|---|---:|---:|---:|")
    ra, rb = base["result"], cand["result"]
    out(f"| Capacidade máxima sustentável (votos/s) | {ra['maxSustainableRate']:,} | {rb['maxSustainableRate']:,} | "
        f"{delta(ra['maxSustainableRate'], rb['maxSustainableRate'], 0)} |")
    out(f"| p95 na capacidade máxima (ms) | {ra['maxSustainableWorstP95Ms']} | {rb['maxSustainableWorstP95Ms']} | "
        f"{delta(ra['maxSustainableWorstP95Ms'], rb['maxSustainableWorstP95Ms'])} |")
    bpa, bpb = ra.get("breakingPoint") or {}, rb.get("breakingPoint") or {}
    out(f"| Ponto de ruptura (votos/s) | {bpa.get('rate', '—')} | {bpb.get('rate', '—')} | {delta(bpa.get('rate'), bpb.get('rate'), 0)} |")
    out(f"| Gargalo na ruptura | {bpa.get('bottleneck', '—')} | {bpb.get('bottleneck', '—')} | |")
    out("")

    sa, sb = per_rate(base), per_rate(cand)
    common = sorted(set(sa) & set(sb))
    if common:
        out("## Taxas em comum (pior execução de cada taxa)")
        out("")
        out(f"| Taxa (votos/s) | p95 {a} | p95 {b} | p99 {a} | p99 {b} | CPU API {a} | CPU API {b} | CPU PG {a} | CPU PG {b} |")
        out("|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
        for rate in common:
            x, y = sa[rate], sb[rate]

            def cpu(step, key):
                r = (step.get("resources") or {}).get(key)
                return f"{r['avg']:.0f}%" if r else "—"

            out(f"| {rate:,} | {x['latencyMs']['p95']:.1f} | {y['latencyMs']['p95']:.1f} | "
                f"{x['latencyMs']['p99']:.1f} | {y['latencyMs']['p99']:.1f} | "
                f"{cpu(x, 'apiCpuPercent')} | {cpu(y, 'apiCpuPercent')} | "
                f"{cpu(x, 'postgresCpuPercent')} | {cpu(y, 'postgresCpuPercent')} |")
        out("")
        out("CPU em % de uma CPU lógica (média na janela de envio).")


if __name__ == "__main__":
    main()
