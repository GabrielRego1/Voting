---

description: "Lista de tasks da feature 001-paredao-voting (Votação do Paredão)"
---

# Tasks: Votação do Paredão

**Input**: Documentos de design em `/specs/001-paredao-voting/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: OBRIGATÓRIOS. O Princípio II da constitution (TDD, inegociável) exige que todo comportamento comece por um teste que falha. A integração usa PostgreSQL real via Testcontainers, sem mock de banco.

**Organization**: as fases seguem as user stories em ordem de prioridade. Cada task pertence a uma etapa do [plan.md](plan.md), e cada etapa pertence a uma única fase.

## Format: `[ID] [P?] [Story] Description`

- **ID**: `PLN-NN-NNN`, exigido pela constitution (seção "Rastreabilidade").
  - `PLN-NN` é a etapa do plan e `NNN` é o sequencial dentro da etapa.
  - Não existe task sem etapa.
- **[P]**: pode rodar em paralelo, porque usa arquivos diferentes e não depende de task incompleta.
- **[Story]**: user story atendida (US1–US4). Setup, Foundational e Polish não têm rótulo.
- **Ordem TDD**: cada etapa `PLN-NN` pertence a uma única fase. Dentro da etapa, todas as tasks de teste vêm antes de qualquer task de implementação. O teste DEVE falhar antes de a implementação começar.
- **Relógio**: o `TimeProvider` é o relógio de negócio (`receivedAt`, `openedAt`/`closesAt`, decisão de finalizar). A espera entre lotes do writer usa tempo real (research R-04).
- **Nomes de teste**: `Method_Scenario_ExpectedResult`.
- **Commits**: Conventional Commits citando o ID (ex.: `feat(votes): batch durable vote writer [PLN-07-004]`).

## Path Conventions

- Produção: `src/Voting.Domain/`, `src/Voting.Application/`, `src/Voting.Infrastructure/`, `src/Voting.Api/`
- Gerador de carga: `tools/Voting.Workload/` (não referencia projetos de `src/`)
- Testes: `tests/Voting.<Projeto>.Tests/`

---

## Phase 1: Setup (Shared Infrastructure) — PLN-01

**Purpose**: solução, projetos, build sem warnings e infraestrutura de teste.

- [X] PLN-01-001 Criar `Voting.slnx` na raiz.
  - Projetos de produção, todos `net10.0`:
    - `src/Voting.Domain/Voting.Domain.csproj` (classlib);
    - `src/Voting.Application/Voting.Application.csproj` (classlib);
    - `src/Voting.Infrastructure/Voting.Infrastructure.csproj` (classlib);
    - `src/Voting.Api/Voting.Api.csproj` (web);
    - `tools/Voting.Workload/Voting.Workload.csproj` (console).
  - Projetos de teste xUnit v3: `tests/Voting.Domain.Tests`, `tests/Voting.Application.Tests`, `tests/Voting.Infrastructure.Tests`, `tests/Voting.Api.Tests` e `tests/Voting.Workload.Tests`.
  - Referências de produção: Application→Domain; Infrastructure→Application; Api→Application e Infrastructure. `Voting.Workload` não referencia nada de `src/`.
  - Cada projeto de teste referencia o projeto que testa. `Voting.Workload.Tests` referencia também `Voting.Api` e `tests/Voting.Api.Tests`, para o teste de integração.
- [X] PLN-01-002 [P] Criar `Directory.Packages.props` com `ManagePackageVersionsCentrally=true`. Versões estáveis atuais de `Npgsql`, `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` e `Microsoft.Extensions.TimeProvider.Testing`. Os `.csproj` não declaram versões.
- [X] PLN-01-003 [P] Adicionar `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` ao `Directory.Build.props`, mantendo as propriedades existentes.
- [X] PLN-01-004 [P] Criar `docker-compose.yml` com o serviço `postgres` (`postgres:17`).
  - Porta `5432`, banco `voting`, usuário `voting`.
  - Senha lida de `POSTGRES_PASSWORD`, com default de desenvolvimento documentado no próprio arquivo.
  - Healthcheck `pg_isready`.
- [X] PLN-01-005 [P] Criar `tests/Voting.Infrastructure.Tests/Fixtures/PostgresFixture.cs` e `PostgresCollection.cs`.
  - A fixture é um `IAsyncLifetime` que sobe `PostgreSqlContainer` (`postgres:17`) uma vez por coleção e expõe a connection string.
  - A coleção é a definição xUnit compartilhada.
- [X] PLN-01-006 Rodar `dotnet build Voting.slnx` e confirmar `0 Warning(s)` com os projetos vazios.

**Checkpoint**: a solução compila sem warnings e a fixture de PostgreSQL sobe nos testes.

---

## Phase 2: Foundational (Blocking Prerequisites) — PLN-02 a PLN-05

**Purpose**: criar, abrir e consultar um paredão de ponta a ponta. Todas as user stories dependem de existir uma votação aberta.

**⚠️ CRITICAL**: nenhuma user story começa antes desta fase.

### PLN-02 — Domínio: criação e abertura do paredão

- [X] PLN-02-001 Escrever testes que falham em `tests/Voting.Domain.Tests/Polls/PollTests.cs`.
  - `Create` aceita "Exatamente 3, com nomes distintos (case-insensitive, sem espaços nas pontas) e não vazios".
  - Nome com "1–100 caracteres, normalizado com `Trim()`".
  - `Position` "1–3" na ordem recebida.
  - Rejeita com `invalid_participants` para 2 ou 4 nomes, nome vazio, nome com 101 caracteres e `"Ana"`/`" ana "` duplicados.
  - `Open(now)` define `OpenedAt = now` e `ClosesAt = now + 30 s`; um segundo `Open` falha com `poll_already_opened`.
  - `StatusAt(now)` retorna `Created` antes de abrir e `Open` em `OpenedAt` e em `ClosesAt` (limite inclusivo).
  - `Rehydrate(...)` reconstrói o mesmo estado.
- [X] PLN-02-002 Implementar o domínio do paredão.
  - `src/Voting.Domain/Common/Result.cs`: resultado com erro tipado por `code`.
  - `src/Voting.Domain/Polls/Poll.cs`: `Id` Guid v7 via `Guid.CreateVersion7`, constante `Duration` de 30 s, `Create`, `Open`, `StatusAt` e `Rehydrate`.
  - `src/Voting.Domain/Polls/Participant.cs`.
  - `src/Voting.Domain/Polls/PollStatus.cs`: `Created`, `Open`, `Closed`, `Finalized`.
  - `src/Voting.Domain/Polls/PollErrors.cs`.
  - O domínio recebe instantes como parâmetro e não usa `TimeProvider`.

### PLN-03 — Application: casos de uso do paredão

- [X] PLN-03-001 Escrever testes que falham, com `FakeTimeProvider` e o fake `tests/Voting.Application.Tests/Fakes/InMemoryPollRepository.cs`.
  - Arquivos de teste: `tests/Voting.Application.Tests/Polls/CreatePollTests.cs`, `tests/Voting.Application.Tests/Polls/OpenPollTests.cs` e `tests/Voting.Application.Tests/Polls/GetPollTests.cs`.
  - `CreatePoll` persiste e retorna o paredão `Created`.
  - `OpenPoll` usa `TimeProvider.GetUtcNow()` e falha com `another_poll_open` se houver paredão aberto ou ainda não finalizado.
  - `OpenPoll` e `GetPoll` falham com `poll_not_found` para id inexistente.
  - `GetPoll` retorna o status derivado do relógio.
- [X] PLN-03-002 Declarar `IPollRepository` em `src/Voting.Application/Abstractions/IPollRepository.cs`.
  - `AddAsync`, `GetAsync(Guid)`.
  - `TryOpenAsync(Poll)`, que retorna `false` quando outro paredão está aberto ou não finalizado.
  - `ListDueForFinalizationAsync(DateTimeOffset now)`.
- [X] PLN-03-003 Implementar `src/Voting.Application/Polls/CreatePoll.cs`, `src/Voting.Application/Polls/OpenPoll.cs`, `src/Voting.Application/Polls/GetPoll.cs` e `src/Voting.Application/Polls/PollDto.cs`, com registro em `src/Voting.Application/DependencyInjection.cs`.

### PLN-04 — Infrastructure: schema e repositório de paredões

- [X] PLN-04-001 Escrever testes de integração que falham em `tests/Voting.Infrastructure.Tests/Persistence/PollRepositoryTests.cs`, usando `PostgresCollection`.
  - Round-trip de paredão com 3 participantes.
  - `opened_at` e `closes_at` persistidos.
  - **Reinício simulado**: uma nova instância de `PollRepository` com um novo `NpgsqlDataSource` lê o mesmo `closes_at`, que nunca é alterado (FR-020).
  - `TryOpenAsync` concorrente em dois paredões: só um vence (índice parcial `one_open_poll`).
  - `ListDueForFinalizationAsync` retorna apenas paredões com `closes_at < now` e `finalized = false`.
- [X] PLN-04-002 Criar o schema e o inicializador.
  - `src/Voting.Infrastructure/Persistence/Sql/001_schema.sql`, como embedded resource, com `polls`, `participants`, `votes` e `poll_results` exatamente como em [data-model.md](data-model.md#persistência-postgresql).
  - Incluir `UNIQUE (poll_id, idempotency_key)`, `UNIQUE (poll_id, position)`, `UNIQUE (poll_id, lower(name))`, `INDEX (poll_id, participant_id)` e o índice parcial `one_open_poll`.
  - Tudo idempotente com `CREATE ... IF NOT EXISTS`. O `SchemaInitializer` aplica, em ordem, todos os scripts `Sql/NNN_*.sql` embarcados.
  - O trigger de imutabilidade **não** entra aqui: ele é criado em PLN-07-006, depois do teste que o exige.
  - `src/Voting.Infrastructure/Persistence/SchemaInitializer.cs` (`IHostedService`) aplica o script na inicialização.
- [X] PLN-04-003 Implementar `src/Voting.Infrastructure/Persistence/PollRepository.cs` com Npgsql e SQL explícito.
  - `src/Voting.Infrastructure/DependencyInjection.cs` registra o `NpgsqlDataSource` (connection string `ConnectionStrings:Voting`), o `SchemaInitializer` e o `PollRepository`.

### PLN-05 — API: paredões e mapeamento de erros

- [X] PLN-05-001 Criar `tests/Voting.Api.Tests/Fixtures/VotingApiFactory.cs`.
  - É um `WebApplicationFactory<Program>` + `IAsyncLifetime`.
  - Sobe um `PostgreSqlContainer` e sobrescreve `ConnectionStrings:Voting`.
  - Substitui `TimeProvider` por um `FakeTimeProvider` exposto pela factory. Ele substitui apenas o relógio de negócio; testes que avançam o relógio aguardam por polling do endpoint, e não por espera de tempo real.
  - Expõe uma `ApiCollection` xUnit.
- [X] PLN-05-002 Escrever testes de integração que falham em `tests/Voting.Api.Tests/Polls/PollEndpointsTests.cs`, conforme [voting-api.openapi.yaml](contracts/voting-api.openapi.yaml).
  - `POST /polls` → `201` com `Location`, `status: Created` e 3 participantes com `position` 1–3.
  - `POST /polls` → `400 invalid_participants` para 2 nomes, para nome com 101 caracteres e para nomes duplicados.
  - `GET /polls/{id}` → `200`, ou `404 poll_not_found`.
  - `POST /polls/{id}/open` → `200` com `closesAt = openedAt + 30 s`.
  - `POST /polls/{id}/open` → `409 poll_already_opened` na segunda chamada e `409 another_poll_open` com outro paredão aberto.
  - Todos os erros vêm como `application/problem+json` com o campo `code`.
- [X] PLN-05-003 Implementar a API de paredões.
  - `src/Voting.Api/Program.cs` com `public partial class Program`, `TimeProvider.System`, ProblemDetails e porta `5080` em `src/Voting.Api/Properties/launchSettings.json` e `appsettings.json`.
  - `src/Voting.Api/Errors/ErrorMapping.cs` mapeia cada `code` do contrato para seu status HTTP:
    - `invalid_participants`, `missing_idempotency_key` e `invalid_idempotency_key` → 400;
    - `poll_not_found` → 404;
    - `poll_already_opened`, `another_poll_open`, `voting_not_open`, `voting_closed`, `idempotency_key_conflict` e `result_not_available` → 409;
    - `invalid_participant` → 422;
    - `overloaded` → 503 com `Retry-After`.
  - `src/Voting.Api/Endpoints/PollEndpoints.cs` com `POST /polls`, `GET /polls/{pollId}` e `POST /polls/{pollId}/open`.

**Checkpoint**: um paredão pode ser criado, aberto e consultado pela API, com a regra de um único paredão aberto ou não finalizado.

---

## Phase 3: User Story 1 - Votar durante a janela aberta (Priority: P1) 🎯 MVP — PLN-06 a PLN-08

**Goal**: eleitor vota em um dos 3 participantes enquanto a votação está aberta e recebe confirmação somente após gravação durável. Voto múltiplo é permitido e a idempotência é garantida pela chave.

**Independent Test**: abrir um paredão, enviar votos para cada participante (inclusive repetidos e reenvios com a mesma chave) e verificar no banco que cada chave distinta gerou exatamente um voto.

### PLN-06 — Avaliação e registro de voto

- [X] PLN-06-001 [P] [US1] Escrever testes que falham em `tests/Voting.Domain.Tests/Polls/PollVoteEvaluationTests.cs`.
  - `EvaluateVote(participantId, receivedAt)` retorna aceito para participante do paredão com `receivedAt` entre `OpenedAt` e `ClosesAt`.
  - Retorna `invalid_participant` para `participantId` desconhecido.
  - Cinco avaliações seguidas do mesmo participante são todas aceitas (sem restrição de voto único).
- [X] PLN-06-002 [P] [US1] Escrever testes que falham em `tests/Voting.Application.Tests/Votes/CastVoteTests.cs`, com o fake `tests/Voting.Application.Tests/Fakes/FakeVoteWriter.cs`.
  - `receivedAt` vem de `TimeProvider` e é carimbado antes de chamar o writer.
  - Chave ausente → `missing_idempotency_key`.
  - Chave fora de "1–64 caracteres `[A-Za-z0-9-_]`" → `invalid_idempotency_key` (testar 65 caracteres e `"a b"`).
  - Paredão inexistente → `poll_not_found`.
  - Participante inválido → `invalid_participant`, sem chamar o writer.
  - Os resultados do writer são propagados como `Accepted`, `Replayed`, `Conflict` (`idempotency_key_conflict`) e `Overloaded` (`overloaded`).
  - 100 votos da mesma "origem" são todos aceitos, sem limite por origem (FR-018).
- [X] PLN-06-003 [US1] Implementar `EvaluateVote` em `src/Voting.Domain/Polls/Poll.cs`, com `src/Voting.Domain/Polls/VoteEvaluation.cs` e `src/Voting.Domain/Polls/VoteRejection.cs`, cobrindo o aceite e `invalid_participant`.
  - Criar o registro imutável `src/Voting.Domain/Votes/Vote.cs`: `Id` (Guid v7), `PollId`, `ParticipantId`, `IdempotencyKey`, `ReceivedAt`.
- [X] PLN-06-004 [US1] Declarar as abstrações do voto.
  - `IVoteWriter` em `src/Voting.Application/Abstractions/IVoteWriter.cs`: `WriteAsync(Vote, CancellationToken)` retorna `VoteWriteOutcome`.
  - `src/Voting.Application/Votes/VoteWriteOutcome.cs`: `Accepted(VoteReceipt)`, `Replayed(VoteReceipt)`, `Conflict` ou `Overloaded`.
  - `src/Voting.Application/Votes/VoteReceipt.cs`.
- [X] PLN-06-005 [US1] Implementar `src/Voting.Application/Votes/CastVote.cs` e registrá-lo em `src/Voting.Application/DependencyInjection.cs`.

### PLN-07 — Gravação durável em lote

- [ ] PLN-07-001 [P] [US1] Escrever testes de integração que falham em `tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterTests.cs`.
  - Após `await WriteAsync` retornar `Accepted`, a linha já existe em `votes`.
  - 5.000 escritas concorrentes resultam em 5.000 linhas, com a mesma quantidade de `Accepted`.
  - Mesma chave e mesmo participante → `Replayed` com o mesmo `voteId` e o mesmo `receivedAt`.
  - Mesma chave e participante diferente → `Conflict`, com o voto original inalterado.
  - Duas escritas com a mesma chave no mesmo lote: a primeira é `Accepted` e a segunda é `Replayed` ou `Conflict`.
  - Com `Capacity = 1` e o writer parado, a escrita excedente retorna `Overloaded`.
  - O writer grava lotes mesmo com um `FakeTimeProvider` registrado e nunca avançado, provando que a espera entre lotes usa tempo real.
- [ ] PLN-07-002 [P] [US1] Escrever teste de integração que falha em `tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterFailureTests.cs`.
  - Usa um `PostgreSqlContainer` **dedicado**, fora da `PostgresCollection`, para não afetar outros testes.
  - Com o container pausado no meio de um lote, todas as escritas do lote falham e nenhuma é confirmada.
- [ ] PLN-07-003 [P] [US1] Escrever testes que falham.
  - `tests/Voting.Infrastructure.Tests/Persistence/CachedPollRepositoryTests.cs`:
    - depois do primeiro `GetAsync` de um paredão aberto, as leituras seguintes não vão ao banco (contagem de chamadas no repositório interno);
    - `TryOpenAsync` invalida o snapshot.
  - `tests/Voting.Infrastructure.Tests/Persistence/VoteImmutabilityTests.cs`: `UPDATE` e `DELETE` em `votes` falham com a exceção do trigger `votes_append_only` (FR-009).
- [ ] PLN-07-004 [US1] Implementar o writer em lote.
  - `src/Voting.Infrastructure/Votes/VoteWriterOptions.cs`: `MaxBatchSize = 1000`, `MaxBatchDelay = 5 ms`, `Capacity = 50000`, ligados à seção `VoteWriter` da configuração.
  - `src/Voting.Infrastructure/Votes/VoteBatchWriter.cs` é um `BackgroundService` + `IVoteWriter`.
    - `Channel` limitado com `TaskCompletionSource` por voto.
    - Lote por tamanho ou por `MaxBatchDelay` medido com **tempo real** (`PeriodicTimer`/`Stopwatch`), nunca com o `TimeProvider` injetado.
    - Uma transação com `INSERT INTO votes ... SELECT FROM unnest(...) ON CONFLICT (poll_id, idempotency_key) DO NOTHING RETURNING ...`.
    - Releitura das chaves em conflito para decidir entre `Replayed` e `Conflict`.
    - Os TCS só são completados após o commit.
    - Exceção no lote → `TrySetException` em todos os votos do lote.
- [ ] PLN-07-005 [US1] Implementar `src/Voting.Infrastructure/Persistence/CachedPollRepository.cs`, um decorator de `IPollRepository`.
  - Mantém o snapshot do paredão aberto, imutável após `Open`, e o invalida em `TryOpenAsync`.
  - Registrar o decorator e o `VoteBatchWriter` em `src/Voting.Infrastructure/DependencyInjection.cs`.
- [ ] PLN-07-006 [US1] Criar `src/Voting.Infrastructure/Persistence/Sql/002_votes_append_only.sql` (embedded resource), com a função e o trigger `votes_append_only` (`BEFORE UPDATE OR DELETE ON votes` → `RAISE EXCEPTION`) via `CREATE OR REPLACE`. PLN-07-003 (`VoteImmutabilityTests`) deve passar.

### PLN-08 — API: endpoint de voto

- [ ] PLN-08-001 [US1] Escrever testes de integração que falham em `tests/Voting.Api.Tests/Votes/VoteEndpointsTests.cs`.
  - `POST /polls/{id}/votes` com `Idempotency-Key: k1` → `201` com `VoteReceipt`.
  - Repetir com `k1` → `200` com o mesmo `voteId`.
  - `k1` com outro participante → `409 idempotency_key_conflict`.
  - `participantId` aleatório → `422 invalid_participant`.
  - Sem header → `400 missing_idempotency_key`; chave com 65 caracteres → `400 invalid_idempotency_key`.
  - Paredão inexistente → `404 poll_not_found`.
  - 5 votos distintos no mesmo participante → 5 linhas em `votes`.
  - Com `VoteWriter:Capacity` mínimo e carga concorrente, respostas `503 overloaded` trazem `Retry-After`.
- [ ] PLN-08-002 [US1] Implementar `src/Voting.Api/Endpoints/VoteEndpoints.cs` (`POST /polls/{pollId}/votes`).
  - Lê o header `Idempotency-Key` e o corpo `CastVoteRequest`.
  - Responde `201` para `Accepted` e `200` para `Replayed`; os erros saem via `ErrorMapping`.

**Checkpoint**: votos são registrados de forma durável, imutável e idempotente. US1 já é demonstrável sozinha.

---

## Phase 4: User Story 2 - Encerramento automático após 30 segundos (Priority: P1) — PLN-09

**Goal**: a votação aceita votos apenas entre `OpenedAt` e `ClosesAt` (inclusive) e rejeita explicitamente fora da janela.

**Independent Test**: abrir um paredão, votar aos 10 s (aceito), avançar o relógio para depois de 30 s e votar (`409 voting_closed`), confirmando no banco que o voto rejeitado não foi gravado.

### PLN-09 — Janela de votação

- [ ] PLN-09-001 [P] [US2] Acrescentar testes que falham em `tests/Voting.Domain.Tests/Polls/PollVoteEvaluationTests.cs`.
  - `receivedAt == ClosesAt` é aceito.
  - `receivedAt == ClosesAt + 1 tick` → `voting_closed`.
  - Paredão não aberto → `voting_not_open`; `receivedAt < OpenedAt` → `voting_not_open`.
  - `StatusAt(ClosesAt + 1 tick)` → `Closed`.
- [ ] PLN-09-002 [P] [US2] Escrever testes de integração que falham em `tests/Voting.Api.Tests/Votes/VotingWindowTests.cs`, usando o `FakeTimeProvider` da `VotingApiFactory`.
  - Voto antes de abrir → `409 voting_not_open`.
  - Voto com o relógio avançado 10 s → `201`.
  - Voto exatamente em `closesAt` → `201`.
  - Relógio avançado para `closesAt + 1 ms` → `409 voting_closed`, e a contagem em `votes` não muda.
  - `GET /polls/{id}` → `status: Closed`.
- [ ] PLN-09-003 [US2] Completar `EvaluateVote` e `StatusAt` em `src/Voting.Domain/Polls/Poll.cs`.
  - Regras `voting_not_open` e `voting_closed` em `src/Voting.Domain/Polls/VoteRejection.cs`.
  - Estado `Closed`.
  - PLN-09-001 e PLN-09-002 devem passar.

**Checkpoint**: a janela de 30 s é respeitada, inclusive no limite exato.

---

## Phase 5: User Story 3 - Exibição do resultado final (Priority: P2) — PLN-10 a PLN-12

**Goal**: após o encerramento, o sistema apura os votos confirmados e disponibiliza o resultado em até 5 s, com eliminado, empate ou ausência de votos.

**Independent Test**: votar 600/300/100 com contagens determinísticas, avançar o relógio após `closesAt` e consultar `GET /result`. O esperado é 60,00/30,00/10,00 com o primeiro participante eliminado; com 400/400/200, empate.

### PLN-10 — Domínio: apuração

- [ ] PLN-10-001 [P] [US3] Escrever testes que falham em `tests/Voting.Domain.Tests/Results/ResultCalculatorTests.cs`.
  - 600/300/100 → 60.00/30.00/10.00, `Eliminated` com o primeiro.
  - 400/400/200 → `Tie` com os dois primeiros.
  - 1/1/1 → `Tie` com os três, porcentagens somando exatamente `100.00` (ex.: 33.34/33.33/33.33).
  - 0/0/0 → `NoVotes`, todas `0.00`, sem eliminado.
  - 2/1/0 → o participante com 0 votos aparece com `0.00`.
  - Quando a porcentagem arredondada empata mas a contagem não, decide a contagem exata.
  - Para 200 distribuições aleatórias com total > 0, a soma é sempre `100.00`.
- [ ] PLN-10-002 [US3] Implementar a apuração.
  - `src/Voting.Domain/Results/Outcome.cs` (`Eliminated`, `Tie`, `NoVotes`).
  - `src/Voting.Domain/Results/ParticipantTally.cs` (`ParticipantId`, `Name`, `Votes` long, `Percentage` decimal com 2 casas).
  - `src/Voting.Domain/Results/PollResult.cs`.
  - `src/Voting.Domain/Results/ResultCalculator.cs`, com o maior resto (*largest remainder*) em `decimal`.

### PLN-11 — Finalização do paredão e recuperação

- [ ] PLN-11-001 [P] [US3] Escrever testes que falham, com fakes em `tests/Voting.Application.Tests/Fakes/`.
  - Arquivos de teste: `tests/Voting.Application.Tests/Results/FinalizePollTests.cs` e `tests/Voting.Application.Tests/Results/GetResultTests.cs`.
  - `FinalizePoll` chama `FlushUntilAsync(closesAt)` antes de contar.
  - `FinalizePoll` grava `FinalizedAt` a partir do `TimeProvider`.
  - Uma segunda chamada não altera o resultado já gravado.
  - `FinalizePoll` não faz nada se `now <= closesAt`.
  - `GetResult` retorna `result_not_available` para os estados `Created`, `Open` e `Closed` sem resultado; retorna `poll_not_found` para paredão inexistente; retorna o resultado quando ele existe.
- [ ] PLN-11-002 [P] [US3] Acrescentar teste de integração em `tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterTests.cs`.
  - Com votos enfileirados com `receivedAt <= closesAt`, `FlushUntilAsync(closesAt)` só retorna depois que todos estão commitados em `votes`.
- [ ] PLN-11-003 [P] [US3] Escrever testes de integração que falham.
  - `tests/Voting.Infrastructure.Tests/Persistence/VoteTallyTests.cs`: `GROUP BY` correto, incluindo participantes com 0 votos.
  - `tests/Voting.Infrastructure.Tests/Persistence/ResultRepositoryTests.cs`: insere uma única vez e marca `polls.finalized = true` na mesma transação; um segundo `TryAddAsync` não altera nada.
  - `tests/Voting.Infrastructure.Tests/Hosting/PollFinalizerTests.cs`, com `FakeTimeProvider`:
    - finaliza o paredão depois de `closes_at`;
    - ao iniciar, finaliza paredões já vencidos sem resultado (recuperação, FR-020);
    - `FinalizedAt - ClosesAt <= 5 s` (SC-005).
- [ ] PLN-11-004 [US3] Declarar as portas.
  - `src/Voting.Application/Abstractions/IVoteTally.cs`: `CountByParticipantAsync(pollId)`.
  - `src/Voting.Application/Abstractions/IResultRepository.cs`: `TryAddAsync(PollResult)` idempotente e `GetAsync(pollId)`.
  - Acrescentar `FlushUntilAsync(DateTimeOffset, CancellationToken)` a `src/Voting.Application/Abstractions/IVoteWriter.cs`.
- [ ] PLN-11-005 [US3] Implementar `src/Voting.Application/Results/FinalizePoll.cs`, `src/Voting.Application/Results/GetResult.cs` e `src/Voting.Application/Results/PollResultDto.cs`, e registrá-los no DI da Application.
- [ ] PLN-11-006 [US3] Implementar `FlushUntilAsync` em `src/Voting.Infrastructure/Votes/VoteBatchWriter.cs`.
  - Usar um marcador na fila, completado quando todos os votos enfileirados antes dele forem commitados.
- [ ] PLN-11-007 [US3] Implementar a finalização na Infrastructure.
  - `src/Voting.Infrastructure/Persistence/VoteTally.cs`.
  - `src/Voting.Infrastructure/Persistence/ResultRepository.cs`, com `tallies` em JSONB e `outcome`.
  - `src/Voting.Infrastructure/Hosting/PollFinalizer.cs`: `BackgroundService` em laço de 250 ms via `TimeProvider` (relógio de negócio), chamando `ListDueForFinalizationAsync` e `FinalizePoll`. Também roda na inicialização.
  - Registrar tudo em `src/Voting.Infrastructure/DependencyInjection.cs`.

### PLN-12 — API: resultado

- [ ] PLN-12-001 [US3] Escrever testes de integração que falham em `tests/Voting.Api.Tests/Results/ResultEndpointsTests.cs`.
  - `GET /polls/{id}/result` com o paredão aberto → `409 result_not_available`.
  - Após 600/300/100 votos e o relógio além de `closesAt` → `200` com `totalVotes: 1000`, porcentagens `60.00`/`30.00`/`10.00`, `outcome: Eliminated` e `eliminated` igual ao primeiro.
  - 400/400/200 → `outcome: Tie`, `tiedParticipants` com os dois primeiros e `eliminated: null`.
  - Sem votos → `outcome: NoVotes`.
  - O resultado fica disponível em até 5 s de relógio após `closesAt`.
  - Paredão inexistente → `404 poll_not_found`.
- [ ] PLN-12-002 [US3] Implementar `src/Voting.Api/Endpoints/ResultEndpoints.cs` (`GET /polls/{pollId}/result`), conforme o schema `PollResult` do contrato.

**Checkpoint**: US1 + US2 + US3 formam o paredão completo de ponta a ponta.

---

## Phase 6: User Story 4 - Gerador de carga de votos (workload) (Priority: P2) — PLN-13

**Goal**: ferramenta externa que envia votos pela API pública numa taxa configurável, reenvia parte deles com a mesma chave, mede a latência e confere o total aceito com o resultado.

**Independent Test**: executar o gerador contra a API com distribuição conhecida e verificar que o relatório mostra `acceptedEqualsTotalVotes = true` e o resultado final.

### PLN-13 — Gerador de carga

- [ ] PLN-13-001 [P] [US4] Escrever testes que falham em `tests/Voting.Workload.Tests/WorkloadOptionsTests.cs`, cobrindo todas as opções de [workload-cli.md](contracts/workload-cli.md).
  - Os defaults da tabela são aplicados.
  - `--distribution` deve ter 3 valores somando 100.
  - `--retry-fraction` deve estar em `0..1`.
  - `--rate`, `--duration` e `--connections` devem ser > 0.
  - `--output` aceita apenas `text` ou `json`.
  - Erro de uso → código de saída `2`.
- [ ] PLN-13-002 [P] [US4] Escrever testes que falham.
  - `tests/Voting.Workload.Tests/ParticipantPickerTests.cs`: com `Random` semeado e 100.000 sorteios, 50/30/20 fica a ±1 ponto percentual.
  - `tests/Voting.Workload.Tests/LatencyRecorderTests.cs`: p50/p95/p99/max corretos para amostras conhecidas.
  - `tests/Voting.Workload.Tests/ReportBuilderTests.cs`:
    - `accepted` conta chaves distintas confirmadas (`201`, ou `200` em retentativa de chave nunca confirmada antes);
    - reenvios intencionais vão para `replayed`;
    - rejeições são agrupadas por `code`;
    - `checks` e código de saída `0`/`1` seguem o contrato.
- [ ] PLN-13-003 [P] [US4] Escrever teste de integração que falha em `tests/Voting.Workload.Tests/WorkloadIntegrationTests.cs`, usando `VotingApiFactory` (via handler HTTP da factory).
  - Cria e abre o paredão, roda a ~100 votos/s por 3 s com `--retry-fraction 0.1` e avança o `FakeTimeProvider` além de `closesAt`.
  - O relatório tem `acceptedEqualsTotalVotes = true` e `noDuplicatesFromReplays = true`.
- [ ] PLN-13-004 [US4] Implementar `tools/Voting.Workload/WorkloadOptions.cs` e `tools/Voting.Workload/OptionsParser.cs`, um parser manual das opções do contrato, sem dependência extra.
- [ ] PLN-13-005 [US4] Implementar as peças de medição e relatório.
  - `tools/Voting.Workload/ParticipantPicker.cs`.
  - `tools/Voting.Workload/LatencyRecorder.cs`.
  - `tools/Voting.Workload/Report.cs`, `tools/Voting.Workload/ReportBuilder.cs` e `tools/Voting.Workload/ReportWriter.cs`. Os formatos `text` e `json` usam os campos do contrato.
- [ ] PLN-13-006 [US4] Implementar `tools/Voting.Workload/VotingApiClient.cs`.
  - Cliente HTTP do contrato; aceita `HttpMessageHandler` injetável para o teste de integração.
  - `SocketsHttpHandler` com `MaxConnectionsPerServer = --connections`.
  - Retentativa automática com a mesma chave em `503` ou erro de transporte, até 3 tentativas, contada em `retried`.
- [ ] PLN-13-007 [US4] Implementar o executor e o ponto de entrada.
  - `tools/Voting.Workload/LoadRunner.cs`: modelo aberto com ticks de 10 ms enviando `rate/100` votos por tick, independente das respostas. Gera chaves únicas, reenvia a fração `--retry-fraction` de votos aceitos com a mesma chave e registra a latência de cada requisição.
  - `tools/Voting.Workload/Program.cs`:
    - cria e abre o paredão quando `--poll-id` é omitido;
    - executa o `LoadRunner`;
    - faz polling de `GET /result` até `--result-timeout`;
    - imprime o relatório e encerra com o código do contrato.

**Checkpoint**: o gerador valida, sozinho, que a contagem bate sob carga.

---

## Phase 7: Polish & Cross-Cutting Concerns — PLN-14

**Purpose**: validar os critérios de sucesso sob carga real e registrar os resultados.

- [ ] PLN-14-001 Executar as seções 1–3 do [quickstart.md](quickstart.md).
  - `dotnet build Voting.slnx -c Release` com `0 Warning(s)`.
  - `dotnet test Voting.slnx -c Release` todo verde, sem `Skip`.
  - Cenário manual de 10 passos.
  - Divergências viram correções na etapa de origem, começando por um teste que reproduz o problema.
- [ ] PLN-14-002 Executar a seção 4 do [quickstart.md](quickstart.md) (10.000 votos/s, `--duration 35`) e registrar em `specs/001-paredao-voting/validation.md`: `achievedRate`, contadores, `latencyMs`, `checks` e o tempo entre `closesAt` e `finalizedAt`.
  - Validar SC-001, SC-002, SC-003, SC-004, SC-005, SC-006 e SC-008.
  - Se a meta não for atingida, ajustar apenas `VoteWriter` (`MaxBatchSize`, `MaxBatchDelay`, `Capacity`) e o pool do Npgsql em `src/Voting.Api/appsettings.json`, sem relaxar a durabilidade.
- [ ] PLN-14-003 Executar a seção 6 do [quickstart.md](quickstart.md) (queda da API no meio da votação) e registrar em `specs/001-paredao-voting/validation.md` que `closesAt` ficou inalterado e que `acceptedEqualsTotalVotes = true` (FR-020).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (PLN-01)**: sem dependências.
- **Foundational (PLN-02 a PLN-05)**: depende do Setup e bloqueia todas as user stories. Ordem: PLN-02 → PLN-03 → PLN-04 → PLN-05.
- **US1 (PLN-06 a PLN-08)**: depende da Foundational. Ordem: PLN-06 → PLN-07 → PLN-08.
- **US2 (PLN-09)**: depende da US1, porque estende `EvaluateVote` (PLN-06-003) e usa o endpoint de voto (PLN-08-002).
- **US3 (PLN-10 a PLN-12)**: PLN-10 (domínio puro) pode começar logo após a Foundational. PLN-11 depende da US1 (`VoteBatchWriter`). PLN-12 depende de PLN-11.
- **US4 (PLN-13)**: os testes e as implementações PLN-13-001, PLN-13-002, PLN-13-004 e PLN-13-005 só dependem do Setup. PLN-13-003, PLN-13-006 e PLN-13-007 dependem de US1 e US3.
- **Polish (PLN-14)**: depende de todas as user stories.

### Within Each Step

- Todas as tasks de teste da etapa vêm antes de qualquer task de implementação, e cada teste deve ser visto falhando primeiro.
- Domain → Application (portas e casos de uso) → Infrastructure → API.

### Parallel Opportunities

- **Setup**: PLN-01-002, PLN-01-003, PLN-01-004 e PLN-01-005 em paralelo, após PLN-01-001.
- **US1**: PLN-06-001 e PLN-06-002 em paralelo; PLN-07-001, PLN-07-002 e PLN-07-003 em paralelo.
- **US2**: PLN-09-001 e PLN-09-002 em paralelo.
- **US3**: PLN-10 inteira em paralelo com a US1/US2; PLN-11-001, PLN-11-002 e PLN-11-003 em paralelo.
- **US4**: PLN-13-001, PLN-13-002 e PLN-13-003 em paralelo.

---

## Parallel Example: User Story 1

```bash
# Testes da PLN-07 (arquivos distintos, todos devem falhar antes de PLN-07-004):
Task: "PLN-07-001 VoteBatchWriterTests em tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterTests.cs"
Task: "PLN-07-002 VoteBatchWriterFailureTests em tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterFailureTests.cs"
Task: "PLN-07-003 CachedPollRepositoryTests e VoteImmutabilityTests em tests/Voting.Infrastructure.Tests/Persistence/"
```

## Parallel Example: User Story 3

```bash
Task: "PLN-11-001 FinalizePollTests/GetResultTests em tests/Voting.Application.Tests/Results/"
Task: "PLN-11-002 FlushUntil em tests/Voting.Infrastructure.Tests/Votes/VoteBatchWriterTests.cs"
Task: "PLN-11-003 VoteTally/ResultRepository/PollFinalizer tests em tests/Voting.Infrastructure.Tests/"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Setup (PLN-01) → Foundational (PLN-02 a PLN-05).
2. US1 (PLN-06 a PLN-08): votos duráveis, imutáveis e idempotentes.
3. **PARAR e VALIDAR**: testes da US1 verdes e votos conferidos no banco.

### Incremental Delivery

1. Setup + Foundational → paredão criável e abrível.
2. \+ US1 → votação funcional (MVP).
3. \+ US2 → janela de 30 s garantida.
4. \+ US3 → resultado final (paredão completo).
5. \+ US4 → gerador de carga e conferência automática.
6. Polish → validação dos critérios de sucesso a 10k/s.

---

## Notes

- `/speckit-analyze` é obrigatório antes de `/speckit-implement`, conforme a constitution.
- Tasks com [P] usam arquivos diferentes e não dependem de task incompleta.
- Commit após cada task ou grupo lógico, citando o ID `PLN-NN-NNN`.
- Nenhum teste com `Skip` sem justificativa; build sempre com zero warnings.
