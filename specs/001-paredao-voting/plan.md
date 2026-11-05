# Implementation Plan: Votação do Paredão

**Branch**: `001-paredao-voting` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-paredao-voting/spec.md`

## Summary

Paredão com 3 participantes e votação aberta por exatamente 30 s, voto múltiplo e contabilização exatamente-uma-vez via chave de idempotência, resultado final (eliminado ou empate) e um gerador de carga externo de 10.000 votos/s.

Abordagem técnica (detalhes em [research.md](research.md)):
- API HTTP em ASP.NET Core Minimal APIs sobre Clean Architecture.
- PostgreSQL como único armazenamento.
- Gravação dos votos em lote (*group commit*): o voto é confirmado somente após o commit, o que garante durabilidade com vazão alta e p95 < 200 ms.
- Janela de votação derivada do relógio (`closes_at` persistido), sem timers que mudem o estado.
- Finalização em background que drena os votos pendentes antes de apurar e persistir o resultado.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.114)

**Primary Dependencies**: ASP.NET Core 10 (Minimal APIs), Npgsql 10, `System.Threading.Channels`, `TimeProvider`

**Storage**: PostgreSQL 17 (docker compose local; Testcontainers nos testes)

**Testing**: xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing`

**Target Platform**: Linux (desenvolvimento local; API e PostgreSQL na mesma máquina)

**Project Type**: Web service (API HTTP) + ferramenta CLI de carga

**Performance Goals**: 10.000 votos/s sustentados por 30 s (~300 mil votos) — SC-001; resultado em até 5 s após o fechamento — SC-005

**Constraints**: p95 ≤ 200 ms na confirmação sob pico (SC-008); confirmação somente após gravação durável (FR-008); zero warnings no build (Princípio IV)

**Scale/Scope**: 1 votação aberta por vez; 3 participantes; ~300 mil votos por votação; instância única da API

Não há itens `NEEDS CLARIFICATION`; todas as decisões abertas estão resolvidas em [research.md](research.md) (R-01 a R-10).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Princípio / Regra | Avaliação pré-research | Pós-design |
|---|---|---|---|
| I | Camadas Domain/Application/Infrastructure/Api; dependências para dentro | ✅ Regras de janela, validação e apuração em Domain/Application; I/O via interfaces declaradas em Application (`IPollRepository`, `IVoteWriter`, `IResultRepository`) | ✅ Domain sem referência a Npgsql/ASP.NET; `VoteBatchWriter` e `PollFinalizer` (hosting) ficam em Infrastructure, orquestrando casos de uso da Application |
| II | TDD; unit em Domain/Application; integração com Testcontainers, sem mock de banco | ✅ Cada etapa pertence a uma única fase/user story, e dentro dela todos os testes vêm antes de qualquer implementação | ✅ Testes de integração da API e do writer contra PostgreSQL real; relógio controlado por `FakeTimeProvider` |
| III | Voto múltiplo; exatamente-uma-vez com chave de idempotência; append-only; rejeição explícita após encerramento; alta vazão | ✅ Coberto por FR-006..FR-009, FR-005 | ✅ `UNIQUE (poll_id, idempotency_key)`; trigger `votes_append_only` rejeita `UPDATE`/`DELETE` em `votes`; confirmação após commit; códigos de rejeição estáveis no contrato |
| III | Anti-abuso configurável sem bloquear voto múltiplo | ✅ O texto é "Proteções anti-abuso [...] DEVEM ser configuráveis e não podem bloquear o voto múltiplo legítimo": a norma restringe **como** as proteções devem ser quando existirem, não exige que existam. Esta feature não introduz nenhuma proteção anti-abuso (FR-018, decisão da clarificação de 2026-11-02). Qualquer feature futura que as introduza DEVE torná-las configuráveis | ✅ Nada no design limita votos por eleitor ou origem |
| IV | Zero warnings; nullable; inglês no código | ✅ `TreatWarningsAsErrors` no `Directory.Build.props` (R-10) | ✅ |
| — | Restrições técnicas: .NET 10, xUnit, Testcontainers; persistência justificada no plan | ✅ PostgreSQL justificado em R-01 | ✅ |
| — | Rastreabilidade: etapas `PLN-NN` justificadas por FR/US/SC | ✅ Ver "Etapas do plan" | ✅ Toda etapa cita requisitos |

**Resultado**: GATE aprovado nas duas avaliações. As três decisões que acrescentam estrutura estão registradas em Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-paredao-voting/
├── plan.md              # Este arquivo
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── voting-api.openapi.yaml
│   └── workload-cli.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
Voting.slnx
Directory.Build.props            # + TreatWarningsAsErrors
Directory.Packages.props         # Central Package Management
docker-compose.yml               # postgres:17

src/
├── Domain/
│   ├── Entities/                # Poll, Vote, PollResult
│   ├── ValueObjects/            # Participant, ParticipantTally, VoteEvaluation
│   ├── Enums/                   # PollStatus, Outcome
│   ├── Errors/                  # PollErrors, VoteRejection
│   ├── Services/                # ResultCalculator
│   └── Common/                  # Result, Result<T>, DomainError, Instants
├── Application/
│   ├── Abstractions/
│   │   └── Persistence/         # IPollRepository, IVoteWriter, IResultRepository, IVoteTally, VoteWriteOutcome
│   ├── UseCases/                # uma pasta por caso de uso
│   │   ├── CreatePoll/          # CreatePollUseCase
│   │   ├── OpenPollVoting/      # OpenPollVotingUseCase
│   │   ├── GetPollDetails/      # GetPollDetailsUseCase
│   │   ├── CastVote/            # CastVoteUseCase (validação + idempotência), CastVoteResult
│   │   ├── FinalizePollResult/  # FinalizePollResultUseCase
│   │   └── GetPollResult/       # GetPollResultUseCase, PollResultDto, ParticipantTallyDto
│   ├── Dtos/                    # DTOs compartilhados: PollDto, ParticipantDto, VoteReceipt
│   └── Errors/                  # ApplicationErrors, VoteErrors
├── Infrastructure/
│   ├── Persistence/
│   │   ├── Repositories/        # PollRepository, CachedPollRepository, ResultRepository, VoteTally
│   │   ├── Writers/             # VoteBatchWriter (Channel + group commit)
│   │   └── Migrations/          # SchemaInitializer, NNN_*.sql
│   ├── BackgroundServices/      # PollFinalizer
│   └── Configuration/           # VoteWriterOptions
└── WebApi/
    ├── Endpoints/               # Polls, Votes, Results
    ├── Contracts/Requests/      # CreatePollRequest, CastVoteRequest
    └── Errors/                  # mapeamento de rejeições → Problem Details

tools/
└── Workload/                    # CLI do gerador de carga

tests/
├── Domain.Tests/                # unit
├── Application.Tests/           # unit (repositórios fake em memória, FakeTimeProvider)
├── Infrastructure.Tests/        # integração (Testcontainers)
├── WebApi.Tests/                # integração (WebApplicationFactory + Testcontainers)
└── Workload.Tests/              # unit (agendamento, percentis, relatório) + integração em taxa baixa
```

**Structure Decision**: as quatro camadas exigidas pelo Princípio I ficam em `src/`. O gerador de carga fica em `tools/`, porque é um cliente externo da API (não é camada do sistema) e só referencia contratos HTTP, nunca projetos de `src/`. Cada projeto de produção tem um projeto de testes correspondente.

Dentro de cada camada, o código é organizado por **papel** (entidades, value objects, casos de uso, repositórios...), com estas regras:
- cada caso de uso tem a própria pasta em `Application/UseCases/`, com nome descritivo do comportamento e sufixo `UseCase`;
- um tipo por arquivo;
- o namespace segue a pasta, garantido no build pelo IDE0130 (`.editorconfig`).

## Etapas do plan

Regras (Princípio II e "Rastreabilidade"):
- Cada etapa pertence a **uma única fase** do `tasks.md` (Setup, Foundational, uma user story ou Polish).
- Dentro da etapa, todas as tasks de teste vêm antes de qualquer task de implementação, e cada teste deve falhar antes de a implementação começar.
- As tasks são numeradas `PLN-NN-NNN`.

**Relógio**: o `TimeProvider` é o relógio de **negócio**. Ele carimba `receivedAt`, define `openedAt` e `closesAt` e decide quando finalizar. Os mecanismos de throughput, como a espera entre lotes do writer, usam tempo real e nunca o `TimeProvider` (research R-04).

### PLN-01 — Fundação da solução *(Setup)*
**Justificativa**: FR-001 e SC-001 (base para todos os requisitos), Princípio IV.

- Criar `Voting.slnx` com os 4 projetos de `src/`, `tools/Voting.Workload` e os 5 projetos de teste, com as referências apontando para dentro.
- Criar `Directory.Packages.props` e acrescentar `TreatWarningsAsErrors` ao `Directory.Build.props`.
- Criar `docker-compose.yml` com PostgreSQL 17.
- Fixture compartilhada do PostgreSQL via Testcontainers.

### PLN-02 — Domínio: criação e abertura do paredão *(Foundational)*
**Justificativa**: FR-001, FR-002, FR-003.

- `Poll.Create` com exatamente 3 participantes válidos e distintos.
- `Open(now)` define `ClosesAt = now + 30 s` e não pode ser chamado duas vezes.
- `StatusAt(now)` retorna os estados `Created` e `Open` (limite inclusivo).

### PLN-03 — Application: casos de uso do paredão *(Foundational)*
**Justificativa**: FR-002, FR-019.

- Porta `IPollRepository`.
- `CreatePoll` e `GetPoll`.
- `OpenPoll` rejeita `another_poll_open` enquanto houver paredão aberto ou ainda não finalizado.

### PLN-04 — Infrastructure: schema e repositório de paredões *(Foundational)*
**Justificativa**: FR-001, FR-002, FR-019, FR-020.

- Scripts SQL embarcados e `SchemaInitializer`.
- Tabelas do [data-model.md](data-model.md). O trigger `votes_append_only` fica em PLN-07, junto com o teste que o exige.
- `PollRepository`.
- Índice parcial `one_open_poll`.
- Testes de integração: persistência, reidratação de `closes_at` após reinício simulado e abertura concorrente.

### PLN-05 — API: paredões e mapeamento de erros *(Foundational)*
**Justificativa**: FR-001, FR-002, FR-005; SC-007.

- `VotingApiFactory` de testes.
- Endpoints `POST /polls`, `GET /polls/{id}` e `POST /polls/{id}/open`.
- `ErrorMapping` com todos os `code` do [contrato](contracts/voting-api.openapi.yaml).

### PLN-06 — Avaliação e registro de voto *(US1)*
**Justificativa**: FR-004, FR-005, FR-006, FR-007, FR-018; US-1.

- `EvaluateVote` com aceite e `invalid_participant`.
- Registro `Vote`.
- Porta `IVoteWriter`.
- `CastVote`:
  - carimba `receivedAt`;
  - valida a `Idempotency-Key`;
  - não impõe limite por origem;
  - devolve `Accepted`, `Replayed`, `Conflict` ou `Overloaded`.

### PLN-07 — Gravação durável em lote *(US1)*
**Justificativa**: FR-007, FR-008, FR-009; SC-001, SC-004, SC-008.

- `VoteBatchWriter`:
  - `Channel` limitado;
  - lotes de até 1.000 votos ou 5 ms de tempo real;
  - `INSERT ... unnest ... ON CONFLICT DO NOTHING RETURNING`;
  - confirmação somente após o commit;
  - `Overloaded` com a fila cheia.
- `CachedPollRepository`, para não ler o banco a cada voto.
- Testes de integração:
  - durabilidade, idempotência e conflito de chave;
  - falha de banco sem confirmação parcial (em container dedicado);
  - imutabilidade de `votes` garantida pelo trigger `votes_append_only`, criado nesta etapa (`002_votes_append_only.sql`).

### PLN-08 — API: endpoint de voto *(US1)*
**Justificativa**: FR-005, FR-007; SC-007; US-1.

- `POST /polls/{id}/votes` com o header `Idempotency-Key`.
- `201` para o primeiro aceite e `200` para reenvio.
- `503` com `Retry-After`.

### PLN-09 — Janela de votação *(US2)*
**Justificativa**: FR-003, FR-004, FR-005; SC-003; US-2.

- Regras `voting_not_open` e `voting_closed` e estado `Closed` no domínio.
- Testes de domínio e de API (com `FakeTimeProvider`) escritos antes da implementação: votar antes de abrir, exatamente em `closesAt` e depois de `closesAt`.

### PLN-10 — Domínio: apuração do resultado *(US3)*
**Justificativa**: FR-010, FR-011, FR-012; SC-006; US-3.

- `ResultCalculator` com o maior resto em 2 casas.
- Desfechos `Eliminated`, `Tie` e `NoVotes`.
- Decisão do eliminado por contagem exata.

### PLN-11 — Finalização do paredão e recuperação *(US3)*
**Justificativa**: FR-003, FR-010, FR-014, FR-020; SC-002, SC-005; US-3.

- `FinalizePoll` e `GetResult`.
- `FlushUntilAsync` no writer.
- `VoteTally`, `ResultRepository` (gravação única e idempotente) e `PollFinalizer`, que também finaliza paredões vencidos na inicialização.

### PLN-12 — API: resultado *(US3)*
**Justificativa**: FR-013, FR-014; US-3.

- `GET /polls/{id}/result` conforme o schema `PollResult`.
- `409 result_not_available` antes da finalização.

### PLN-13 — Gerador de carga *(US4)*
**Justificativa**: FR-015, FR-016, FR-017; US-4; SC-001, SC-002, SC-004, SC-008.

- CLI de [workload-cli.md](contracts/workload-cli.md).
- Modelo aberto, distribuição, reenvios, retentativas, percentis, polling do resultado, `checks` e código de saída.
- Teste de integração em taxa baixa.

### PLN-14 — Validação ponta a ponta sob carga *(Polish)*
**Justificativa**: SC-001, SC-002, SC-003, SC-004, SC-005, SC-006, SC-008; FR-020.

- Executar as seções 1–4 e 6 do [quickstart.md](quickstart.md).
- Registrar os números em `specs/001-paredao-voting/validation.md`.
- Se a meta não for atingida, ajustar somente os parâmetros do writer e do pool, sem relaxar a durabilidade.

## Complexity Tracking

| Decisão | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| Gravação em lote com `Channel` + `BackgroundService` (group commit) | Sustentar 10k votos/s com confirmação somente após gravação durável e p95 ≤ 200 ms (FR-008, SC-001, SC-008) — etapa PLN-07 | Insert por requisição fica limitado ao `fsync` por commit; `synchronous_commit=off` perderia votos confirmados (Princípio III) |
| 5º projeto de produção (`tools/Voting.Workload`) fora das 4 camadas | A spec exige um gerador externo que use o mesmo canal dos eleitores (FR-015) | Embutir na API violaria "externo" e misturaria a ferramenta de teste com o sistema; usar k6 tiraria a conferência do resultado e o relatório da suíte de testes .NET |
| SQL explícito com Npgsql em vez de ORM | O caminho crítico depende de `unnest` + `ON CONFLICT ... RETURNING` | EF Core exigiria SQL bruto no ponto principal e somaria uma segunda forma de acesso a dados para apenas 4 tabelas |
