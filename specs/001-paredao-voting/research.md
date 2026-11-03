# Research: Votação do Paredão

**Feature**: `001-paredao-voting` | **Data**: 2026-11-02 | **Spec**: [spec.md](spec.md)

Cada item resolve uma decisão técnica aberta no Technical Context do [plan.md](plan.md).

---

## R-01 — Persistência dos votos e das votações

- **Decision**: PostgreSQL 17 como único armazenamento (votações, participantes, votos e resultado), acessado via Npgsql (`NpgsqlDataSource`) com SQL explícito. Schema versionado em scripts SQL embarcados no `Voting.Infrastructure`, aplicados na inicialização (`CREATE TABLE IF NOT EXISTS`).
- **Rationale**: atende ao registro durável antes da confirmação (FR-008) com commit transacional; a restrição `UNIQUE (poll_id, idempotency_key)` garante a contabilização exatamente-uma-vez no próprio banco (FR-007); a apuração é um `GROUP BY` sobre ~300 mil linhas, bem abaixo de 1 s (SC-005). Uma única dependência de infraestrutura mantém o projeto simples (Restrições Técnicas da constitution) e é testável com Testcontainers (Princípio II).
- **Alternatives considered**:
  - *EF Core*: traz migrations prontas, mas o caminho crítico (insert em lote com `ON CONFLICT ... RETURNING`) exigiria SQL bruto de qualquer forma; duas formas de acesso para um modelo com 4 tabelas não se paga.
  - *Redis (contadores `INCR`)*: muito rápido, mas a durabilidade depende de AOF com `fsync always` e perde-se o registro imutável de cada voto (Princípio III).
  - *Kafka/RabbitMQ + consumidor*: padrão clássico de alta vazão, mas adiciona broker, consumidor e semântica de entrega a coordenar; 10k votos/s cabem em Postgres com escrita em lote.

## R-02 — Atingir 10.000 votos/s com confirmação após gravação durável

- **Decision**: *group commit* em processo. O endpoint valida o voto, carimba `received_at` e o enfileira em um `System.Threading.Channels.Channel` limitado, aguardando um `TaskCompletionSource`. Um `BackgroundService` (`VoteBatchWriter`) drena a fila em lotes de até 1.000 votos ou 5 ms (o que ocorrer primeiro) e grava cada lote em **uma** transação com `INSERT ... SELECT FROM unnest(...) ON CONFLICT (poll_id, idempotency_key) DO NOTHING RETURNING ...`. Só após o commit cada `TaskCompletionSource` é completado e a resposta HTTP é enviada.
- **Rationale**: 10k inserts individuais por segundo saturariam o `fsync` do WAL; agrupando, são ~10–200 commits/s, folgados para Postgres local. A espera máxima de 5 ms + duração do lote mantém o p95 bem abaixo de 200 ms (SC-008). O voto nunca é confirmado antes de estar no disco (FR-008); se o processo cair com votos na fila, eles não foram confirmados e o cliente os reenvia com a mesma chave (FR-007).
- **Alternatives considered**:
  - *Insert por requisição*: mais simples, porém limitado pelo `fsync` por commit; não sustenta 10k/s com p95 < 200 ms.
  - *`synchronous_commit = off`*: aumenta a vazão, mas permite perder votos já confirmados — viola o Princípio III.
  - *`COPY` binário*: mais rápido, mas não suporta `ON CONFLICT`; exigiria tabela de staging e uma etapa extra de deduplicação.
- **Backpressure**: o `Channel` é limitado (ex.: 50.000 itens). Quando cheio, a API responde `503` com `Retry-After`, sem confirmar o voto; o gerador trata como retentativa com a mesma chave.

## R-03 — Idempotência e conflito de chave

- **Decision**: o cliente envia a chave no header `Idempotency-Key` (UUID ou string de até 64 caracteres), escopada por votação. Linhas não inseridas pelo `ON CONFLICT` são relidas em lote no mesmo commit: se o participante coincide, devolve-se a confirmação original (`200`, mesmo corpo); se difere, `409 idempotency_key_conflict`. Duplicatas dentro do mesmo lote são resolvidas da mesma forma (a primeira vence).
- **Rationale**: atende FR-007, SC-004 e o edge case "mesma chave, participante diferente". O banco é o árbitro final, sem estado em memória que se perca em restart.
- **Alternatives considered**: cache de chaves em memória (perde-se em restart; não é fonte de verdade); chave gerada pelo servidor (não protege contra retentativas do cliente).

## R-04 — Prazo de 30 s e referência de tempo

- **Decision**: o domínio recebe instantes (`DateTimeOffset`) como parâmetros; Application e API usam `TimeProvider` (injetado; `FakeTimeProvider` nos testes). Ao abrir, grava-se `opened_at` e `closes_at = opened_at + 30 s`. Um voto é aceito se `opened_at <= received_at <= closes_at`, com `received_at` carimbado na chegada da requisição, **antes** de enfileirar. O estado "aberta/encerrada" é derivado do relógio, nunca de um flag mutável que precise ser alterado a tempo.
- **Rationale**: atende FR-003/FR-004 sem depender de um timer para fechar a votação; o edge case do limite exato (inclusivo) fica determinístico e testável; após um reinício, o mesmo `closes_at` persistido continua valendo (FR-020).
- **Relógio de negócio vs. tempo real**: o `TimeProvider` é o relógio de **negócio** (carimbo de `receivedAt`, `openedAt`/`closesAt` e decisão de finalizar no `PollFinalizer`). Mecanismos de throughput — a espera entre lotes do `VoteBatchWriter` (`MaxBatchDelay`) — usam tempo real (`PeriodicTimer`/`Stopwatch`) e nunca o `TimeProvider`; caso contrário, com `FakeTimeProvider` nos testes, os lotes nunca seriam gravados e nenhum voto seria confirmado.
- **Alternatives considered**: timer que muda o status para "encerrada" (sujeito a atraso de agendamento e perda em restart); usar o instante do commit como referência (penalizaria votos enviados a tempo que esperam na fila).

## R-05 — Finalização e disponibilidade do resultado

- **Decision**: um `BackgroundService` (`PollFinalizer`) aguarda `closes_at`, solicita ao `VoteBatchWriter` um *flush* de todos os votos com `received_at <= closes_at` já enfileirados, apura via `GROUP BY` e persiste o resultado em `poll_results` (status `Finalized`). `GET /result` responde `409 result_not_available` até o resultado existir. Na inicialização, o finalizador procura votações com `closes_at` vencido e sem resultado e as finaliza (recuperação, FR-020).
- **Rationale**: garante que o resultado reflita todos os votos confirmados (SC-002) e fique pronto em até 5 s (SC-005); persistir o resultado o torna imutável e consultável após reinícios.
- **Alternatives considered**: apurar a cada consulta (correto, mas pode ler antes do flush dos últimos lotes e retornar números que mudam); contadores incrementais (dispensáveis, já que totais derivam dos votos — Princípio III).

## R-06 — Cálculo e arredondamento das porcentagens

- **Decision**: cálculo no domínio com `decimal`. A porcentagem de cada participante é arredondada para 2 casas pelo método do maior resto (*largest remainder*), garantindo que a soma exibida seja exatamente 100,00% quando há votos. O eliminado é definido pela **contagem exata** de votos, nunca pela porcentagem arredondada; empate na maior contagem gera `outcome = Tie` com a lista de empatados.
- **Rationale**: cumpre SC-006 com folga e o edge case de dízimas (1/3); a decisão por contagem evita empates artificiais causados por arredondamento.
- **Alternatives considered**: arredondamento simples `MidpointRounding.ToEven` (pode somar 99,99% ou 100,01% — ainda dentro da tolerância, mas menos previsível nos testes).

## R-07 — Estilo de API

- **Decision**: ASP.NET Core Minimal APIs no `Voting.Api`, erros no formato Problem Details (RFC 9457) com um campo `code` estável por motivo de rejeição, e `Kestrel` com HTTP/1.1 keep-alive.
- **Rationale**: menos cerimônia que controllers e menor overhead por requisição; o `code` permite que o gerador agrupe rejeições por motivo (FR-016) e cumpre SC-007.
- **Alternatives considered**: Controllers MVC (mais código sem ganho aqui); gRPC (exigiria cliente específico e foge do "canal usado por eleitores reais").

## R-08 — Gerador de carga (workload)

- **Decision**: aplicação console .NET `Voting.Workload` em `tools/`, usando `HttpClient` com `SocketsHttpHandler` (pool de conexões configurável) e agendamento por taxa fixa (*open model*: N votos por tick de 10 ms, independentemente das respostas). Mede latência por requisição com `Stopwatch` e calcula percentis (p50/p95/p99) ao final; reenvia uma fração configurável de votos com a mesma chave; ao terminar, aguarda o resultado (`GET /result` com polling) e imprime relatório + resultado + verificação `aceitos == total apurado`.
- **Rationale**: atende FR-015..FR-017 exercitando o mesmo endpoint dos eleitores; o *open model* evita o "coordinated omission" que esconderia a latência real sob carga; fica na mesma stack, testável com xUnit.
- **Alternatives considered**: k6 (excelente para carga, mas a lógica de reenvio, conferência com o resultado e relatório customizado ficaria em JavaScript fora da suíte de testes); NBomber (dependência adicional sem ganho sobre um loop simples).

## R-09 — Testes

- **Decision**: xUnit v3; `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) + `Testcontainers.PostgreSql` para integração da API e da Infrastructure; `Microsoft.Extensions.TimeProvider.Testing` para controlar o relógio. Asserções com o `Assert` nativo do xUnit. O teste de carga completo (10k/s) não roda na suíte padrão: é um cenário do [quickstart.md](quickstart.md); a suíte inclui um teste de integração do gerador com taxa baixa.
- **Rationale**: segue o Princípio II (Testcontainers, sem mock de banco) e mantém a suíte rápida e determinística.
- **Alternatives considered**: FluentAssertions (licença comercial a partir da v8); Shouldly (dependência extra sem necessidade).

## R-10 — Build sem warnings

- **Decision**: adicionar `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` ao `Directory.Build.props` existente e `Directory.Packages.props` com Central Package Management.
- **Rationale**: transforma a exigência "zero warnings" do Princípio IV em falha de build automática; versões centralizadas evitam divergência entre os projetos de teste.
- **Alternatives considered**: checar warnings apenas na revisão de PR (manual e sujeito a esquecimento).
