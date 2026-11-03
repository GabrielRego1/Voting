-- Schema for feature 001-paredao-voting (see specs/001-paredao-voting/data-model.md).
-- Every statement is idempotent so the script can run on every startup.

CREATE TABLE IF NOT EXISTS polls (
    id          uuid        PRIMARY KEY,
    created_at  timestamptz NOT NULL,
    opened_at   timestamptz NULL,
    closes_at   timestamptz NULL,
    finalized   boolean     NOT NULL DEFAULT false
);

-- FR-019: at most one poll may be open or awaiting finalization at any time.
CREATE UNIQUE INDEX IF NOT EXISTS one_open_poll
    ON polls ((true))
    WHERE opened_at IS NOT NULL AND finalized = false;

CREATE TABLE IF NOT EXISTS participants (
    id        uuid         PRIMARY KEY,
    poll_id   uuid         NOT NULL REFERENCES polls (id),
    name      varchar(100) NOT NULL,
    position  smallint     NOT NULL CHECK (position BETWEEN 1 AND 3),
    CONSTRAINT participants_poll_position_key UNIQUE (poll_id, position)
);

CREATE UNIQUE INDEX IF NOT EXISTS participants_poll_name_key
    ON participants (poll_id, lower(name));

CREATE TABLE IF NOT EXISTS votes (
    id               uuid        PRIMARY KEY,
    poll_id          uuid        NOT NULL REFERENCES polls (id),
    participant_id   uuid        NOT NULL REFERENCES participants (id),
    idempotency_key  varchar(64) NOT NULL,
    received_at      timestamptz NOT NULL,
    CONSTRAINT votes_poll_idempotency_key_key UNIQUE (poll_id, idempotency_key)
);

CREATE INDEX IF NOT EXISTS votes_poll_participant_idx
    ON votes (poll_id, participant_id);

CREATE TABLE IF NOT EXISTS poll_results (
    poll_id                    uuid        PRIMARY KEY REFERENCES polls (id),
    total_votes                bigint      NOT NULL,
    outcome                    text        NOT NULL,
    eliminated_participant_id  uuid        NULL REFERENCES participants (id),
    tallies                    jsonb       NOT NULL,
    finalized_at               timestamptz NOT NULL
);
