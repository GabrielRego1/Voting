# Data Model: Votação do Paredão

**Feature**: `001-paredao-voting` | **Data**: 2026-11-02 | **Spec**: [spec.md](spec.md)

Nomes de tipos e colunas em inglês (código); descrições em português.

## Visão geral

```text
Poll (Paredão) 1 ──── 3 Participant
     │
     ├── 1 ──── N Vote          (append-only)
     │
     └── 1 ──── 0..1 PollResult (gravado uma única vez, na finalização)

WorkloadRun — existe apenas no gerador de carga (não persistido no sistema)
```

---

## Poll (Paredão) — aggregate root

| Campo | Tipo | Regras |
|---|---|---|
| `Id` | `Guid` (v7) | Gerado na criação. |
| `Participants` | lista de `Participant` | Exatamente 3, com nomes distintos (case-insensitive, sem espaços nas pontas) e não vazios (FR-001). |
| `CreatedAt` | `DateTimeOffset` | Instante da criação. |
| `OpenedAt` | `DateTimeOffset?` | Definido uma única vez por `Open(now)` (FR-002). |
| `ClosesAt` | `DateTimeOffset?` | `OpenedAt + 30 s`; nunca alterado, nem após reinício (FR-003, FR-020). |
| `Duration` | constante | 30 s, fixa nesta feature. |

**Estado derivado** (nunca armazenado como flag mutável — ver research R-04):

```text
                 Open(now)                    now > ClosesAt
   Created ───────────────────▶ Open ───────────────────────▶ Closed ──(resultado gravado)──▶ Finalized
```

- `Created`: `OpenedAt == null`.
- `Open`: `OpenedAt <= now <= ClosesAt` (limite inclusivo).
- `Closed`: `now > ClosesAt` e sem `PollResult`.
- `Finalized`: existe `PollResult`.

**Regras**:
- `Open` só é permitido a partir de `Created`; abrir duas vezes → erro `poll_already_opened`.
- Só uma votação pode estar em `Open` por vez no sistema (FR-019) → erro `another_poll_open`. Verificado na Application e garantido no banco por índice parcial (ver Persistência).
- `EvaluateVote(participantId, receivedAt)` retorna aceito ou o motivo de rejeição (FR-004, FR-005):
  - `receivedAt < OpenedAt` ou não aberta → `voting_not_open`
  - `receivedAt > ClosesAt` → `voting_closed`
  - participante fora do paredão → `invalid_participant`

## Participant

| Campo | Tipo | Regras |
|---|---|---|
| `Id` | `Guid` | Único dentro do paredão. |
| `Name` | `string` | 1–100 caracteres, normalizado com `Trim()`. |
| `Position` | `int` | 1–3; ordem de exibição no resultado. |

## Vote — imutável (append-only)

| Campo | Tipo | Regras |
|---|---|---|
| `Id` | `Guid` (v7) | Gerado pelo servidor. |
| `PollId` | `Guid` | Votação existente. |
| `ParticipantId` | `Guid` | Participante da votação. |
| `IdempotencyKey` | `string` | Obrigatória; 1–64 caracteres `[A-Za-z0-9-_]`; única por `PollId` (FR-007). |
| `ReceivedAt` | `DateTimeOffset` | Carimbado pelo servidor na chegada, antes de enfileirar; decide se está dentro da janela (R-04). |

**Regras**:
- Nunca atualizado nem removido (FR-009).
- Mesma `IdempotencyKey` + mesmo `ParticipantId` → retorna a confirmação original (mesmo `Id` e `ReceivedAt`).
- Mesma `IdempotencyKey` + `ParticipantId` diferente → `idempotency_key_conflict`, voto original intacto.
- Não há vínculo com o eleitor (eleitor anônimo; voto múltiplo permitido — FR-006, FR-018).

## PollResult — gravado uma única vez

| Campo | Tipo | Regras |
|---|---|---|
| `PollId` | `Guid` | 1 por votação. |
| `TotalVotes` | `long` | Soma de `Tallies[].Votes`. |
| `Tallies` | lista de `ParticipantTally` | Um por participante, inclusive com 0 votos. |
| `Outcome` | `Eliminated` \| `Tie` \| `NoVotes` | Ver regras. |
| `EliminatedParticipantId` | `Guid?` | Preenchido somente se `Outcome = Eliminated`. |
| `TiedParticipantIds` | lista de `Guid` | Preenchida somente se `Outcome = Tie` (2 ou 3 itens). |
| `FinalizedAt` | `DateTimeOffset` | Instante da apuração (SC-005: `FinalizedAt - ClosesAt <= 5 s`). |

**ParticipantTally**: `ParticipantId`, `Name`, `Votes` (`long`), `Percentage` (`decimal`, 2 casas).

**Regras de apuração** (FR-010..FR-012, R-06):
- `TotalVotes = 0` → `Outcome = NoVotes`, todas as porcentagens `0.00`, sem eliminado.
- Maior contagem única → `Eliminated` com esse participante.
- Maior contagem compartilhada → `Tie` com os participantes empatados.
- Porcentagens pelo maior resto; soma exatamente `100.00` quando `TotalVotes > 0`. A decisão usa contagens, não porcentagens.

## WorkloadRun — apenas no gerador

| Campo | Descrição |
|---|---|
| `Rate`, `Duration`, `Distribution`, `RetryFraction`, `Connections` | Parâmetros da execução (FR-015, FR-017). |
| `Sent`, `Accepted`, `Replayed`, `RejectedByCode`, `TransportErrors` | Contadores (FR-016). |
| `Latency` (p50/p95/p99/max) | Tempo de resposta observado (SC-008). |
| `Result` + `Matches` | Resultado consultado e a verificação `Accepted == Result.TotalVotes` (SC-002). |

---

## Persistência (PostgreSQL)

```text
polls           (id PK, created_at, opened_at NULL, closes_at NULL)
                UNIQUE INDEX one_open_poll ON polls ((true)) WHERE opened_at IS NOT NULL AND finalized = false
participants    (id PK, poll_id FK, name, position, UNIQUE (poll_id, position), UNIQUE (poll_id, lower(name)))
votes           (id PK, poll_id FK, participant_id FK, idempotency_key, received_at,
                 UNIQUE (poll_id, idempotency_key))
                INDEX (poll_id, participant_id)
                TRIGGER votes_append_only BEFORE UPDATE OR DELETE → RAISE EXCEPTION
poll_results    (poll_id PK FK, total_votes, outcome, eliminated_participant_id NULL,
                 tallies JSONB, finalized_at)
```

- `polls.finalized` é derivado na prática da existência de `poll_results`; mantido como coluna (atualizada na mesma transação que insere `poll_results`) apenas para viabilizar o índice parcial que impõe FR-019. Consequência intencional: uma nova votação só pode ser aberta depois que a anterior for finalizada (até ~5 s após `closes_at`), o que evita duas apurações concorrentes.
- A tabela `votes` só recebe `INSERT`: o trigger `votes_append_only` rejeita qualquer `UPDATE` ou `DELETE`, inclusive do dono da tabela (FR-009).
