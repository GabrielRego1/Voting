namespace Domain.Common;

/// <summary>
/// A typed business error identified by a stable <see cref="Code"/>.
/// </summary>
public sealed record DomainError(string Code, string Message);
