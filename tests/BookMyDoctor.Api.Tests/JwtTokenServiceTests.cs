using BookMyDoctor.Features.Auth;
using BookMyDoctor.Persistence;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "test-signing-key-that-is-at-least-32-bytes",
        ExpiresMinutes = 30
    };

    [Fact]
    public async Task Create_IssuesValidTokenWithUserClaims()
    {
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options));
        var user = new UserAccount(Guid.NewGuid(), "Hari Bahadur", "hb@gmail.com", "secret");

        var token = service.Create(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(Options.Key)
        });

        Assert.True(result.IsValid);
        Assert.Equal(user.Id.ToString(), result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(user.Name, result.Claims[JwtRegisteredClaimNames.Name]);
        Assert.Equal(user.Email, result.Claims[JwtRegisteredClaimNames.Email]);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(29));
    }

    [Fact]
    public async Task Create_TokenFailsValidationWithDifferentKey()
    {
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options));
        var token = service.Create(new UserAccount(Guid.NewGuid(), "A", "a@b.c", "x"));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey("a-completely-different-key-also-32-bytes!")
        });

        Assert.False(result.IsValid);
    }
}
