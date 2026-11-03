-- FR-009: votes are append-only. Any UPDATE or DELETE, even by the table owner, is rejected.

CREATE OR REPLACE FUNCTION votes_append_only() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'votes_append_only: % on votes is not allowed', TG_OP;
END;
$$;

CREATE OR REPLACE TRIGGER votes_append_only
    BEFORE UPDATE OR DELETE ON votes
    FOR EACH ROW EXECUTE FUNCTION votes_append_only();
