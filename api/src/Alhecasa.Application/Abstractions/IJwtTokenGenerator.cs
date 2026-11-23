using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
