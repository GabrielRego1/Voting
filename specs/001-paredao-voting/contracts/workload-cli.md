# Contrato: CLI do gerador de carga (`Voting.Workload`)

**Feature**: `001-paredao-voting` | Requisitos: FR-015, FR-016, FR-017 | Critérios: SC-001, SC-002, SC-008

O gerador usa apenas a [API pública](voting-api.openapi.yaml), o mesmo canal de um eleitor real.

## Uso

```text
dotnet run --project tools/Voting.Workload -- [options]
```

| Opção | Padrão | Descrição |
|---|---|---|
| `--base-url <url>` | `http://localhost:5080` | Endereço da API. |
| `--poll-id <guid>` | — | Paredão já existente. Se omitido, o gerador cria um com `--participants` e o abre. |
| `--participants <a,b,c>` | `Ana,Bruno,Carla` | Nomes usados quando o gerador cria o paredão. |
| `--rate <n>` | `10000` | Votos por segundo (modelo aberto: envia no ritmo, independente das respostas). |
| `--duration <s>` | `35` | Duração do envio em segundos. O padrão ultrapassa os 30 s para exercitar rejeições `voting_closed`. |
| `--distribution <p1,p2,p3>` | `50,30,20` | Porcentagem dos votos por participante, na ordem de `position`; deve somar 100. |
| `--retry-fraction <0..1>` | `0.05` | Fração de votos aceitos reenviados com a mesma `Idempotency-Key` (FR-017). |
| `--connections <n>` | `256` | Máximo de conexões HTTP simultâneas. |
| `--result-timeout <s>` | `15` | Tempo máximo esperando `GET /result` deixar de responder `409`. |
| `--output <text\|json>` | `text` | Formato do relatório. |

Respostas `503 overloaded` e erros de transporte são reenviados automaticamente com a mesma chave (até 3 tentativas) e contabilizados em `retried`.

## Relatório (saída)

O stdout contém apenas o relatório. O progresso sai no stderr: as fases da execução (poll aberta, carga concluída, espera do resultado) e, durante o envio, uma linha a cada 5 s com `sent`, `accepted`, `rejected`, `transport errors` e `in flight`.

Formato `json` (o formato `text` apresenta os mesmos campos em tabela):

```json
{
  "pollId": "0193…",
  "parameters": { "rate": 10000, "durationSeconds": 35, "distribution": [50, 30, 20], "retryFraction": 0.05, "connections": 256 },
  "counts": {
    "sent": 350000,
    "accepted": 300000,
    "replayed": 15000,
    "retried": 120,
    "rejected": { "voting_closed": 50000 },
    "transportErrors": 0
  },
  "achievedRate": 10003.4,
  "latencyMs": { "p50": 4.1, "p95": 18.7, "p99": 41.2, "max": 130.5 },
  "result": { "…": "corpo de GET /polls/{id}/result" },
  "checks": {
    "acceptedEqualsTotalVotes": true,
    "noDuplicatesFromReplays": true,
    "p95WithinTarget": true
  }
}
```

- `accepted`: quantidade de chaves de idempotência **distintas** confirmadas (`201`, ou `200` quando a primeira tentativa se perdeu por queda/timeout e a retentativa encontrou o voto já gravado). Reenvios intencionais (`--retry-fraction`) contam em `replayed`, não em `accepted`.
- `acceptedEqualsTotalVotes`: `accepted == result.totalVotes` (SC-002, SC-004).
- `p95WithinTarget`: `latencyMs.p95 <= 200` (SC-008).

## Código de saída

| Código | Significado |
|---|---|
| `0` | Execução concluída e todas as `checks` verdadeiras. |
| `1` | Execução concluída com alguma `check` falsa. |
| `2` | Erro de uso (parâmetros inválidos) ou API inacessível. |
