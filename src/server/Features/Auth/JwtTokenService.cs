using System.Security.Claims;
using System.Text;
using MyHospitalAI.Features.Doctors;
using MyHospitalAI.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MyHospitalAI.Features.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AccessToken Create(UserAccount user) =>
        Create(user.Id.ToString(), user.Name, Roles.Patient, new Claim(JwtRegisteredClaimNames.Email, user.Email));

    public AccessToken CreateAdmin(string username) => Create("admin", username, Roles.Admin);

    // The subject is the doctor's id, so User.GetUserId() gives the doctor.
    public AccessToken CreateDoctor(Doctor doctor) => Create(doctor.Id.ToString(), doctor.Name, Roles.Doctor);

    private AccessToken Create(string subject, string name, string role, params Claim[] extra)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Name, name),
                new Claim("role", role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                .. extra
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(_options.Key), SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public static SymmetricSecurityKey SigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
