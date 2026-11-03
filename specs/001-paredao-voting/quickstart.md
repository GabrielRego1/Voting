# Quickstart: validação da votação do paredão

**Feature**: `001-paredao-voting` | Contratos: [API](contracts/voting-api.openapi.yaml), [CLI do gerador](contracts/workload-cli.md) | Modelo: [data-model.md](data-model.md)

Guia de validação ponta a ponta. Não contém implementação; os passos devem funcionar ao fim do `/speckit-implement`.

## Pré-requisitos

- .NET SDK 10.0.x (`dotnet --list-sdks`)
- Docker em execução (PostgreSQL local e Testcontainers)
- Porta `5080` (API) e `5432` (PostgreSQL) livres

## 1. Build sem warnings e suíte de testes

```bash
dotnet build Voting.slnx -c Release   # deve terminar com 0 Warning(s), 0 Error(s)
dotnet test  Voting.slnx -c Release   # unitários + integração (Testcontainers sobe o PostgreSQL sozinho)
```

**Esperado**: build com zero warnings (Princípio IV) e todos os testes verdes, sem `Skip`.

## 2. Subir o ambiente local

```bash
docker compose up -d postgres
dotnet run --project src/Voting.Api -c Release
```

**Esperado**: API respondendo em `http://localhost:5080`; schema criado na inicialização.

## 3. Cenário manual (US-1, US-2, US-3)

Use os exemplos do contrato da API (com `curl` ou um arquivo `.http`):

| Passo | Ação | Esperado |
|---|---|---|
| 1 | `POST /polls` com 3 nomes | `201`, `status: Created` |
| 2 | `POST /polls/{id}/votes` antes de abrir | `409`, `code: voting_not_open` |
| 3 | `POST /polls/{id}/open` | `200`, `closesAt = openedAt + 30 s` |
| 4 | Votar em um participante com `Idempotency-Key: k1` | `201` com `voteId` |
| 5 | Repetir o passo 4 com a mesma chave | `200` com o **mesmo** `voteId` |
| 6 | Mesma chave `k1`, outro participante | `409`, `code: idempotency_key_conflict` |
| 7 | Votar em um `participantId` aleatório | `422`, `code: invalid_participant` |
| 8 | `GET /polls/{id}/result` com a votação aberta | `409`, `code: result_not_available` |
| 9 | Aguardar 30 s e votar | `409`, `code: voting_closed` |
| 10 | `GET /polls/{id}/result` (até 5 s após o fechamento) | `200`; `totalVotes = 1`; o participante votado com `100.00` e `outcome: Eliminated` |

## 4. Carga de 10.000 votos/s (US-4, SC-001, SC-002, SC-004, SC-008)

Com a API rodando em `Release`:

```bash
dotnet run --project tools/Voting.Workload -c Release -- --rate 10000 --duration 35 --distribution 50,30,20 --retry-fraction 0.05
echo $?   # 0 = todas as checks passaram
```

**Esperado** (ver campos em [workload-cli.md](contracts/workload-cli.md)):
- `achievedRate` ≥ 10.000 durante os 30 s de janela (SC-001)
- `checks.acceptedEqualsTotalVotes = true` (SC-002) e nenhum reenvio contado em duplicidade (SC-004)
- Votos enviados após os 30 s aparecem em `rejected.voting_closed` e não constam no resultado (SC-003)
- `latencyMs.p95 ≤ 200` (SC-008)
- Resultado com porcentagens próximas de 50/30/20, somando `100.00` (SC-006) e `outcome: Eliminated` para o primeiro participante

## 5. Empate e votação sem votos (US-3)

```bash
# empate exato entre os dois primeiros: distribuição com proporções iguais e taxa baixa para controle
dotnet run --project tools/Voting.Workload -- --rate 100 --duration 10 --distribution 50,50,0 --retry-fraction 0
```

Como a distribuição é probabilística, o empate exato é verificado pelos testes de integração (contagens determinísticas). Aqui, valide apenas a consistência do relatório. Para votação sem votos: crie e abra um paredão, aguarde 30 s e consulte o resultado → `totalVotes: 0`, todas as porcentagens `0.00`, `outcome: NoVotes`.

## 6. Recuperação após queda (FR-020)

1. Inicie o gerador com `--duration 30`.
2. Por volta de 10 s, encerre a API (`Ctrl+C` ou `kill -9`) e suba-a novamente em seguida.
3. **Esperado**: `closesAt` inalterado (`GET /polls/{id}`); o gerador reenvia com as mesmas chaves os votos não confirmados; ao fim, `acceptedEqualsTotalVotes = true`. Se a API voltar depois de `closesAt`, o resultado é finalizado na inicialização.
