# Benchmark de capacidade: v0

**Data**: 2026-11-02 | **Commit**: `6218bfa` | Gerado por `tools/stress/summarize.py` a partir de [`raw/`](raw/)

## Resumo

- **Capacidade máxima sustentável: 15.000 votos/s**, aprovada em 1 execução(ões) de 30 s, com p95 de até 11,0 ms.
- **Ponto de ruptura: 20.000 votos/s** (`confirm-20000-1`), reprovado por `checks.p95WithinTarget`.
- **Gargalo provável**: nenhum componente acima de 85% de CPU; o mais ocupado foi generator (50%).
- Referência anterior: o requisito SC-001 é 10.000 votos/s (validado em `specs/001-paredao-voting/validation.md`).

## Ambiente

| Item | Valor |
|---|---|
| CPU | AMD Ryzen 7 5825U with Radeon Graphics (16 CPUs lógicas) |
| Memória | 14Gi |
| Kernel | 7.0.0-42-generic |
| Energia | bateria; perfil `power-saver`; governor `powersave` |
| .NET SDK | 10.0.114 |
| PostgreSQL | postgres (PostgreSQL) 17.11 (Debian 17.11-1.pgdg13+2) (`docker compose`, banco recriado no início) |
| CPUs da API | `0-7` (8 CPUs lógicas) |
| CPUs do PostgreSQL | `8-11` (4 CPUs lógicas) |
| CPUs do gerador | `12-15` (4 CPUs lógicas) |

## Metodologia

- Rampa de taxas `10000 15000 20000 25000 30000 40000 50000 60000` votos/s, cada degrau com um paredão novo e 30 s de envio em modelo aberto (`Voting.Workload`), distribuição `50,30,20`, `--retry-fraction 0.05` e até 1024 conexões HTTP.
- Um warm-up curto (descartado) aquece JIT, pool de conexões e cache antes da rampa.
- A rampa para no primeiro degrau reprovado; depois uma busca binária (passo mínimo de 1.000 votos/s) refina o limite e o maior degrau aprovado é repetido 2 vez(es) para confirmar a reprodutibilidade.
- Uma taxa é **sustentável** quando todas as suas execuções são aprovadas. Critérios de aprovação:
  - `achievedRate` ≥ 98% da taxa alvo;
  - nenhuma rejeição além de `voting_closed` (em especial, nenhum `overloaded`) e zero erros de transporte;
  - todas as `checks` do gerador: `acceptedEqualsTotalVotes`, `noDuplicatesFromReplays` e `p95WithinTarget` (p95 ≤ 200 ms, SC-008).
- CPU e memória amostradas a cada 1 s (`tools/stress/sample.py`) durante a janela de envio; CPU em % de uma CPU lógica (ex.: 800% = todas as CPUs da API ocupadas). Gargalo considerado saturado acima de 85%.

## Degraus

| Degrau | Alvo (votos/s) | Alcançado | Aceitos/s | p50 | p95 | p99 | max (ms) | overloaded | Erros transp. | CPU API | CPU PG | CPU gerador | Resultado |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `ramp-10000` | 10.000 | 9.997,3 | 9.996 | 6,5 | 8,4 | 14,2 | 46,2 | 0 | 0 | 16% (máx 19%) | 9% (máx 10%) | 11% (máx 25%) | ✅ |
| `ramp-15000` | 15.000 | 14.997,4 | 14.992 | 7,8 | 11,0 | 26,1 | 61,3 | 0 | 0 | 21% (máx 29%) | 12% (máx 15%) | 15% (máx 34%) | ✅ |
| `ramp-20000` | 20.000 | 19.994,4 | 19.990 | 9,6 | 21,6 | 52,6 | 124,4 | 0 | 0 | 27% (máx 35%) | 14% (máx 16%) | 21% (máx 45%) | ✅ |
| `ramp-25000` | 25.000 | 24.993,3 | 22.824 | 13,7 | 4.505,3 | 4.886,6 | 5.086,0 | 0 | 0 | 34% (máx 53%) | 21% (máx 77%) | 33% (máx 63%) | ❌ `checks.p95WithinTarget` |
| `refine-22500` | 22.500 | 22.495,5 | 15.917 | 6.191,2 | 9.071,8 | 9.194,0 | 9.283,3 | 0 | 0 | 32% (máx 44%) | 22% (máx 57%) | 52% (máx 75%) | ❌ `checks.p95WithinTarget` |
| `refine-21000` | 21.000 | 20.991,9 | 16.039 | 4.820,2 | 7.501,9 | 7.813,6 | 7.942,8 | 0 | 0 | 32% (máx 43%) | 22% (máx 68%) | 52% (máx 77%) | ❌ `checks.p95WithinTarget` |
| `confirm-20000-1` | 20.000 | 19.978,1 | 15.896 | 3.610,0 | 6.450,8 | 6.627,4 | 6.738,5 | 0 | 0 | 31% (máx 48%) | 22% (máx 76%) | 50% (máx 76%) | ❌ `checks.p95WithinTarget` |
| `confirm-20000-2` | 20.000 | 19.465,1 | 14.901 | 4.357,2 | 8.334,6 | 8.784,5 | 8.987,7 | 0 | 0 | 30% (máx 44%) | 22% (máx 71%) | 48% (máx 65%) | ❌ `achievedRate`, `checks.p95WithinTarget` |

CPU em % das CPUs reservadas a cada componente (média na janela de envio, com o pico entre parênteses).

### Latência p95 por taxa

```text
  10000 votos/s ok  █ 8.4 ms
  15000 votos/s ok  █ 11.0 ms
  20000 votos/s FAIL ████████████████████████████ 6450.8 ms
  20000 votos/s FAIL █████████████████████████████████████ 8334.6 ms
  20000 votos/s ok  █ 21.6 ms
  21000 votos/s FAIL █████████████████████████████████ 7501.9 ms
  22500 votos/s FAIL ████████████████████████████████████████ 9071.8 ms
  25000 votos/s FAIL ████████████████████ 4505.3 ms
```

### Memória

| Degrau | RSS máx. da API (MB) | Memória máx. do PostgreSQL (MB) | RSS máx. do gerador (MB) |
|---|---:|---:|---:|
| `ramp-10000` | 186 | 474 | 183 |
| `ramp-15000` | 201 | 958 | 220 |
| `ramp-20000` | 220 | 1.468 | 306 |
| `ramp-25000` | 277 | 1.822 | 896 |
| `refine-22500` | 271 | 1.866 | 2.248 |
| `refine-21000` | 278 | 1.458 | 2.135 |
| `confirm-20000-1` | 294 | 1.738 | 1.416 |
| `confirm-20000-2` | 264 | 1.953 | 1.895 |

## Limitações

- API, PostgreSQL e gerador rodam na mesma máquina, via `localhost`: não há latência de rede real, e as CPUs são separadas por `taskset`/`cpuset`, mas cache L3, memória e disco são compartilhados.
- O PostgreSQL usa a configuração padrão da imagem oficial e o volume local do Docker.
- A latência medida inclui as retentativas do gerador (1 s e 2 s após `503`), por isso explode assim que surge `overloaded`.
- Comparações entre versões só valem com o mesmo ambiente, o mesmo split de CPUs e os mesmos parâmetros (`tools/stress/compare.py` avisa quando divergem).

## Como reproduzir

```bash
make stress BENCH_VERSION=v0  # ATENÇÃO: recria o banco local
python3 tools/stress/compare.py benchmarks/v0 benchmarks/<nova-versão>
```
