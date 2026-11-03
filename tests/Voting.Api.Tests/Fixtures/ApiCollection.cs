namespace Voting.Api.Tests.Fixtures;

/// <summary>
/// Groups API integration tests so they run sequentially and share the PostgreSQL container
/// started by <see cref="VotingApiFactory"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection
{
    public const string Name = "Api";
}
