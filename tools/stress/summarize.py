#!/usr/bin/env python3
"""Builds summary.json and README.md (pt-BR) for a stress run from benchmarks/<version>/raw.

Usage: summarize.py benchmarks/v0
"""

import csv
import json
import os
import statistics
import sys

SCHEMA_VERSION = 1
SATURATION = 0.85  # fraction of the pinned cores considered saturated


def cpuset_size(spec):
    total = 0
    for part in spec.split(","):
        if "-" in part:
            lo, hi = part.split("-")
            total += int(hi) - int(lo) + 1
        else:
            total += 1
    return total


def load_json(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return None


def load_resources(path, duration):
    """Average and max over the sending window (skips poll creation and result polling)."""
    try:
        with open(path, encoding="ascii") as f:
            rows = [{k: float(v) for k, v in row.items()} for row in csv.DictReader(f)]
    except OSError:
        return None
    window = [r for r in rows if 2 <= r["t"] <= duration + 1] or rows
    if not window:
        return None

    def stats(key):
        values = [r[key] for r in window]
        return {"avg": round(statistics.fmean(values), 1), "max": round(max(values), 1)}

    return {
        "samples": len(window),
        "apiCpuPercent": stats("api_cpu"),
        "postgresCpuPercent": stats("pg_cpu"),
        "generatorCpuPercent": stats("gen_cpu"),
        "apiRssMbMax": round(max(r["api_rss_mb"] for r in window), 1),
        "generatorRssMbMax": round(max(r["gen_rss_mb"] for r in window), 1),
        "postgresMemMbMax": round(max(r["pg_mem_mb"] for r in window), 1),
    }


def bottleneck(step, cores):
    """Most likely limiting component of a failed step, from its failures and resource usage."""
    failures = step["failures"]
    res = step.get("resources") or {}
    usage = {
        name: (res.get(key, {}).get("avg", 0) / (100 * cores[name]))
        for name, key in (("generator", "generatorCpuPercent"), ("api", "apiCpuPercent"), ("postgres", "postgresCpuPercent"))
    }
    if failures == ["generatorError"]:
        return "generator", "o gerador terminou com erro (ver log do degrau)"
    if failures == ["achievedRate"] and usage["generator"] >= SATURATION:
        return "generator", f"o gerador não sustentou a taxa com {usage['generator']:.0%} das suas CPUs ocupadas"
    if usage["postgres"] >= SATURATION:
        return "postgres", f"PostgreSQL com {usage['postgres']:.0%} das CPUs reservadas"
    if usage["api"] >= SATURATION:
        return "api", f"API com {usage['api']:.0%} das CPUs reservadas"
    if "rejected.overloaded" in failures:
        return "writer", "fila do writer cheia (`overloaded`) sem CPU saturada: o commit em lote não acompanhou a chegada"
    top = max(usage, key=usage.get)
    return "unknown", f"nenhum componente acima de {SATURATION:.0%} de CPU; o mais ocupado foi {top} ({usage[top]:.0%})"


def build_summary(directory):
    raw = os.path.join(directory, "raw")
    env = load_json(os.path.join(raw, "environment.json"))
    if env is None:
        sys.exit(f"error: {raw}/environment.json not found")
    duration = env["parameters"]["durationSeconds"]
    window = min(duration, 30)
    cores = {
        "api": cpuset_size(env["cpuPinning"]["api"]),
        "postgres": cpuset_size(env["cpuPinning"]["postgres"]),
        "generator": cpuset_size(env["cpuPinning"]["generator"]),
    }

    steps = []
    with open(os.path.join(raw, "steps.jsonl"), encoding="utf-8") as f:
        for line in f:
            entry = json.loads(line)
            if entry["phase"] == "warmup":
                continue
            report = load_json(os.path.join(raw, f"{entry['tag']}.json"))
            step = dict(entry)
            step["resources"] = load_resources(os.path.join(raw, f"{entry['tag']}.resources.csv"), duration)
            if report:
                counts = report["counts"]
                step.update({
                    "achievedRate": round(report["achievedRate"], 1),
                    "sent": counts["sent"],
                    "accepted": counts["accepted"],
                    "acceptedPerSecond": round(counts["accepted"] / window, 1),
                    "replayed": counts["replayed"],
                    "retried": counts["retried"],
                    "rejected": counts["rejected"],
                    "transportErrors": counts["transportErrors"],
                    "latencyMs": report["latencyMs"],
                    "totalVotes": (report.get("result") or {}).get("totalVotes"),
                    "checks": report["checks"],
                })
            if not entry["passed"]:
                step["bottleneck"], step["bottleneckDetail"] = bottleneck(step, cores)
            steps.append(step)

    by_rate = {}
    for step in steps:
        by_rate.setdefault(step["rate"], []).append(step)
    sustainable = [r for r, attempts in by_rate.items() if all(s["passed"] for s in attempts)]
    max_rate = max(sustainable, default=0)
    failing = sorted((s for s in steps if not s["passed"] and s["rate"] > max_rate), key=lambda s: s["rate"])
    unstable = sorted(r for r, attempts in by_rate.items() if r < max_rate and not all(s["passed"] for s in attempts))
    best = max((s for s in by_rate.get(max_rate, []) if "latencyMs" in s), key=lambda s: s["latencyMs"]["p95"], default=None)

    return {
        "schemaVersion": SCHEMA_VERSION,
        "version": env["version"],
        "environment": env,
        "criteria": {
            "minAchievedFraction": env["parameters"]["minAchievedFraction"],
            "rejectionsAllowed": ["voting_closed"],
            "transportErrorsAllowed": 0,
            "checks": ["acceptedEqualsTotalVotes", "noDuplicatesFromReplays", "p95WithinTarget (p95 <= 200 ms)"],
        },
        "result": {
            "maxSustainableRate": max_rate,
            "maxSustainableWorstP95Ms": best["latencyMs"]["p95"] if best else None,
            "maxSustainableAttempts": len(by_rate.get(max_rate, [])),
            "breakingPoint": None if not failing else {
                "rate": failing[0]["rate"],
                "tag": failing[0]["tag"],
                "failures": failing[0]["failures"],
                "bottleneck": failing[0]["bottleneck"],
                "bottleneckDetail": failing[0]["bottleneckDetail"],
            },
            "unstableRates": unstable,
        },
        "steps": steps,
    }


def num(value, decimals=0):
    """pt-BR number formatting: 12.345,6."""
    if value is None:
        return "—"
    text = f"{value:,.{decimals}f}"
    return text.replace(",", "X").replace(".", ",").replace("X", ".")


def pct(value):
    return "—" if value is None else f"{num(value * 100, 0)}%"


def render_readme(summary):
    env = summary["environment"]
    params = env["parameters"]
    pin = env["cpuPinning"]
    res = summary["result"]
    cores = {k: cpuset_size(pin[k]) for k in ("api", "postgres", "generator")}
    steps = summary["steps"]
    ok_steps = [s for s in steps if "latencyMs" in s]
    lines = []
    w = lines.append

    w(f"# Benchmark de capacidade: {summary['version']}")
    w("")
    w(f"**Data**: {env['date'][:10]} | **Commit**: `{env['commit'][:7]}`"
      + (" (árvore com alterações não commitadas em `src`/gerador)" if env.get("dirtyTree") else "")
      + " | Gerado por `tools/stress/summarize.py` a partir de [`raw/`](raw/)")
    w("")
    w("## Resumo")
    w("")
    if res["maxSustainableRate"]:
        w(f"- **Capacidade máxima sustentável: {num(res['maxSustainableRate'])} votos/s**, "
          f"aprovada em {res['maxSustainableAttempts']} execução(ões) de "
          f"{params['durationSeconds']} s, com p95 de até {num(res['maxSustainableWorstP95Ms'], 1)} ms.")
    else:
        w("- Nenhum degrau foi aprovado.")
    bp = res["breakingPoint"]
    if bp:
        w(f"- **Ponto de ruptura: {num(bp['rate'])} votos/s** (`{bp['tag']}`), reprovado por "
          f"{', '.join(f'`{f}`' for f in bp['failures'])}.")
        w(f"- **Gargalo provável**: {bp['bottleneckDetail']}.")
        if bp["bottleneck"] == "generator":
            w("- ⚠️ O limite encontrado é do **gerador**, não da API: repita com mais CPUs em `GEN_CPUS` para medir a API.")
    else:
        w("- Nenhum degrau falhou: o limite está acima da maior taxa testada; amplie `RATES`.")
    if res["unstableRates"]:
        w(f"- Taxas abaixo do máximo com alguma execução reprovada (instáveis): {', '.join(num(r) for r in res['unstableRates'])}.")
    w(f"- Referência anterior: o requisito SC-001 é 10.000 votos/s (validado em `specs/001-paredao-voting/validation.md`).")
    w("")

    w("## Ambiente")
    w("")
    w("| Item | Valor |")
    w("|---|---|")
    w(f"| CPU | {env['machine']['cpu']} ({env['machine']['logicalCpus']} CPUs lógicas) |")
    w(f"| Memória | {env['machine']['memory']} |")
    w(f"| Kernel | {env['machine']['kernel']} |")
    power = env["machine"].get("power")
    if power:
        w(f"| Energia | {'tomada' if power['onAc'] else 'bateria'}; perfil `{power['profile']}`; governor `{power['governor']}` |")
    w(f"| .NET SDK | {env['software']['dotnet']} |")
    w(f"| PostgreSQL | {env['software']['postgres']} (`docker compose`, banco recriado no início) |")
    w(f"| CPUs da API | `{pin['api']}` ({cores['api']} CPUs lógicas) |")
    w(f"| CPUs do PostgreSQL | `{pin['postgres']}` ({cores['postgres']} CPUs lógicas) |")
    w(f"| CPUs do gerador | `{pin['generator']}` ({cores['generator']} CPUs lógicas) |")
    w("")

    w("## Metodologia")
    w("")
    w(f"- Rampa de taxas `{' '.join(str(r) for r in params['rampRates'])}` votos/s, cada degrau com um paredão novo e "
      f"{params['durationSeconds']} s de envio em modelo aberto (`Voting.Workload`), distribuição `{params['distribution']}`, "
      f"`--retry-fraction {params['retryFraction']}` e até {params['connections']} conexões HTTP.")
    w("- Um warm-up curto (descartado) aquece JIT, pool de conexões e cache antes da rampa.")
    w("- A rampa para no primeiro degrau reprovado"
      + (f"; depois uma busca binária (passo mínimo de {num(params['refineStep'])} votos/s) refina o limite" if params["refine"] else "")
      + (f" e o maior degrau aprovado é repetido {params['confirmRuns']} vez(es) para confirmar a reprodutibilidade." if params["confirmRuns"] else "."))
    w("- Uma taxa é **sustentável** quando todas as suas execuções são aprovadas. Critérios de aprovação:")
    w(f"  - `achievedRate` ≥ {num(params['minAchievedFraction'] * 100)}% da taxa alvo;")
    w("  - nenhuma rejeição além de `voting_closed` (em especial, nenhum `overloaded`) e zero erros de transporte;")
    w("  - todas as `checks` do gerador: `acceptedEqualsTotalVotes`, `noDuplicatesFromReplays` e `p95WithinTarget` (p95 ≤ 200 ms, SC-008).")
    w(f"- CPU e memória amostradas a cada 1 s (`tools/stress/sample.py`) durante a janela de envio; CPU em % de uma CPU lógica "
      f"(ex.: {cores['api'] * 100}% = todas as CPUs da API ocupadas). Gargalo considerado saturado acima de {num(SATURATION * 100)}%.")
    w("")

    w("## Degraus")
    w("")
    w("| Degrau | Alvo (votos/s) | Alcançado | Aceitos/s | p50 | p95 | p99 | max (ms) | overloaded | Erros transp. | CPU API | CPU PG | CPU gerador | Resultado |")
    w("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")
    for s in steps:
        lat = s.get("latencyMs") or {}
        r = s.get("resources") or {}

        def cpu(key, name):
            v = r.get(key)
            return "—" if not v else f"{pct(v['avg'] / (100 * cores[name]))} (máx {pct(v['max'] / (100 * cores[name]))})"

        verdict = "✅" if s["passed"] else "❌ " + ", ".join(f"`{f}`" for f in s["failures"])
        w(f"| `{s['tag']}` | {num(s['rate'])} | {num(s.get('achievedRate'), 1)} | {num(s.get('acceptedPerSecond'), 0)} | "
          f"{num(lat.get('p50'), 1)} | {num(lat.get('p95'), 1)} | {num(lat.get('p99'), 1)} | {num(lat.get('max'), 1)} | "
          f"{num((s.get('rejected') or {}).get('overloaded', 0))} | {num(s.get('transportErrors'))} | "
          f"{cpu('apiCpuPercent', 'api')} | {cpu('postgresCpuPercent', 'postgres')} | {cpu('generatorCpuPercent', 'generator')} | {verdict} |")
    w("")
    w("CPU em % das CPUs reservadas a cada componente (média na janela de envio, com o pico entre parênteses).")
    w("")

    if ok_steps:
        w("### Latência p95 por taxa")
        w("")
        w("```text")
        top = max(s["latencyMs"]["p95"] for s in ok_steps) or 1
        for s in sorted(ok_steps, key=lambda s: (s["rate"], s["tag"])):
            bar = "█" * max(1, round(40 * s["latencyMs"]["p95"] / top))
            mark = "ok " if s["passed"] else "FAIL"
            w(f"{s['rate']:>7} votos/s {mark} {bar} {s['latencyMs']['p95']:.1f} ms")
        w("```")
        w("")

    w("### Memória")
    w("")
    w("| Degrau | RSS máx. da API (MB) | Memória máx. do PostgreSQL (MB) | RSS máx. do gerador (MB) |")
    w("|---|---:|---:|---:|")
    for s in steps:
        r = s.get("resources") or {}
        w(f"| `{s['tag']}` | {num(r.get('apiRssMbMax'))} | {num(r.get('postgresMemMbMax'))} | {num(r.get('generatorRssMbMax'))} |")
    w("")

    w("## Limitações")
    w("")
    w("- API, PostgreSQL e gerador rodam na mesma máquina, via `localhost`: não há latência de rede real, e as CPUs são "
      "separadas por `taskset`/`cpuset`, mas cache L3, memória e disco são compartilhados.")
    w("- O PostgreSQL usa a configuração padrão da imagem oficial e o volume local do Docker.")
    w("- A latência medida inclui as retentativas do gerador (1 s e 2 s após `503`), por isso explode assim que surge `overloaded`.")
    w("- Comparações entre versões só valem com o mesmo ambiente, o mesmo split de CPUs e os mesmos parâmetros "
      "(`tools/stress/compare.py` avisa quando divergem).")
    w("")

    w("## Como reproduzir")
    w("")
    w("```bash")
    w(f"make stress BENCH_VERSION={summary['version']}  # ATENÇÃO: recria o banco local")
    w(f"python3 tools/stress/compare.py benchmarks/{summary['version']} benchmarks/<nova-versão>")
    w("```")
    w("")
    return "\n".join(lines)


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: summarize.py benchmarks/<version>")
    directory = sys.argv[1]
    summary = build_summary(directory)
    with open(os.path.join(directory, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2, ensure_ascii=False)
        f.write("\n")
    with open(os.path.join(directory, "README.md"), "w", encoding="utf-8") as f:
        f.write(render_readme(summary))
    print(f"maxSustainableRate={summary['result']['maxSustainableRate']}")


if __name__ == "__main__":
    main()
