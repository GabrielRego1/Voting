# Validação: Votação do Paredão

**Feature**: `001-paredao-voting` | **Data**: 2026-11-03 | **Guia**: [quickstart.md](quickstart.md)

Ambiente: máquina local Linux com 16 CPUs, .NET SDK 10.0.114, PostgreSQL 17 (`docker compose`), API em `Release` e gerador em `Release` na mesma máquina. Writer com os valores padrão (`MaxBatchSize = 1000`, `MaxBatchDelay = 5 ms`, `Capacity = 50000`) e pool padrão do Npgsql: nenhum ajuste foi necessário.

## Seções 1–3: build, testes e cenário manual (PLN-14-001)

| Item | Resultado |
|---|---|
| `dotnet build Voting.slnx -c Release` | `0 Warning(s)`, `0 Error(s)` |
| `dotnet test --solution Voting.slnx -c Release` | 164 testes, 164 aprovados, 0 ignorados (`Skip`) |
| Cenário manual de 10 passos | 10/10 conforme o esperado; resultado com `totalVotes: 1`, `100.00` e `outcome: Eliminated`, finalizado 0,26 s após `closesAt` |

Divergência encontrada e corrigida: com o xUnit v3 sobre o Microsoft.Testing.Platform (opt-in no `global.json`), o `dotnet test` do .NET 10 exige `--solution`. O quickstart foi atualizado.

## Seção 4: carga de 10.000 votos/s (PLN-14-002)

Comando: `--rate 10000 --duration 35 --distribution 50,30,20 --retry-fraction 0.05`. Código de saída `0`.

| Campo | Valor |
|---|---|
| `achievedRate` | 9.997,5 votos/s |
| `sent` / `accepted` / `replayed` / `retried` | 350.000 / 299.878 / 14.957 / 0 |
| `rejected` | `voting_closed`: 50.126 |
| `transportErrors` | 0 |
| `latencyMs` | p50 6,1 · p95 8,5 · p99 24,3 · max 116,4 |
| Resultado | `totalVotes` 299.878; Ana 49,96 · Bruno 29,98 · Carla 20,06 (soma 100,00); `outcome: Eliminated` (Ana) |
| `checks` | `acceptedEqualsTotalVotes`, `noDuplicatesFromReplays` e `p95WithinTarget` verdadeiros |
| `finalizedAt - closesAt` | 0,30 s |

O `achievedRate` fica um pouco abaixo de 10.000 por construção: o modelo aberto nunca envia acima de `--rate`, e o último tick de 10 ms termina logo depois dos 35 s. Para medir a folga da API, a carga foi repetida a 12.000 votos/s:

| Campo (12.000 votos/s, 35 s) | Valor |
|---|---|
| `achievedRate` | 11.998,4 votos/s |
| `sent` / `accepted` / `replayed` / `retried` | 420.000 / 359.899 / 18.206 / 0 |
| `rejected` / `transportErrors` | `voting_closed`: 60.105 / 0 |
| `latencyMs` | p50 6,8 · p95 9,6 · p99 20,7 · max 49,3 |
| Resultado | `totalVotes` 359.899; 49,90 · 30,05 · 20,05; `Eliminated` |
| `checks` | todas verdadeiras (código de saída `0`) |
| `finalizedAt - closesAt` | 0,30 s |

### Critérios de sucesso

| Critério | Evidência | Situação |
|---|---|---|
| SC-001 (10k votos/s por 30 s) | 299.878 votos aceitos nos 30 s de janela, sem `overloaded`; a 12k/s, 359.899 aceitos e p95 de 9,6 ms | ✅ |
| SC-002 (resultado = votos confirmados) | `acceptedEqualsTotalVotes = true` nas duas execuções | ✅ |
| SC-003 (rejeição após o encerramento) | 50.126 e 60.105 `voting_closed`, fora do resultado | ✅ |
| SC-004 (reenvio sem duplicar) | 14.957 e 18.206 reenvios, todos `200`; `noDuplicatesFromReplays = true` | ✅ |
| SC-005 (resultado em até 5 s) | 0,30 s após `closesAt` | ✅ |
| SC-006 (porcentagens somando 100) | soma exatamente 100,00 | ✅ |
| SC-008 (p95 ≤ 200 ms) | p95 de 8,5 ms (10k/s) e 9,6 ms (12k/s) | ✅ |

## Seção 6: queda da API no meio da votação (PLN-14-003)

Roteiro: gerador com `--rate 10000 --duration 30`; aos ~10 s a API recebeu `kill -9` e foi reiniciada a partir do binário `Release`.

| Campo | Valor |
|---|---|
| `sent` / `accepted` / `replayed` / `retried` | 300.000 / 299.865 / 15.025 / 5.015 |
| `rejected` / `transportErrors` | `voting_closed`: 136 / 0 |
| `latencyMs` | p50 6,9 · p95 211,4 · p99 317,1 · max 367,0 |
| `openedAt` / `closesAt` após o reinício | `01:19:56.658401` / `01:20:26.658401` (exatamente 30 s; lido do banco, inalterado) |
| Resultado | `totalVotes` 299.865 (50,01 · 30,06 · 19,93), `Eliminated`, finalizado 0,20 s após `closesAt` |
| `checks.acceptedEqualsTotalVotes` | `true` |
| `checks.noDuplicatesFromReplays` | `true` |

- FR-020 atendido: o `closesAt` persistido continuou valendo depois do reinício, e os 5.015 votos sem confirmação durante a queda foram reenviados com as mesmas chaves e confirmados (nenhum perdido, nenhum duplicado).
- O `p95WithinTarget` saiu `false` nesta execução (código de saída `1`) porque a latência inclui as retentativas durante a queda proposital. A seção 6 não exige esse check; o SC-008 é medido na seção 4.
- Ajuste feito para viabilizar o cenário: o backoff entre as 3 tentativas do gerador passou a ser de 1 s e 2 s. Com esperas curtas, um voto já commitado cuja resposta se perdeu na queda esgotaria as tentativas antes do reinício e não seria contado em `accepted`.

## Limite de capacidade (baseline v0)

O teste de limite (rampa até a ruptura, com CPUs fixas para API, PostgreSQL e gerador) está em [`benchmarks/v0/README.md`](../../benchmarks/v0/README.md). Para repetir em versões futuras: `make stress BENCH_VERSION=<versão>` e `python3 tools/stress/compare.py benchmarks/v0 benchmarks/<versão>`.
