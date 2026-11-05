using Application.Dtos;

namespace Application.UseCases.CastVote;

public sealed record CastVoteResult(VoteReceipt Receipt, bool IsReplay);
