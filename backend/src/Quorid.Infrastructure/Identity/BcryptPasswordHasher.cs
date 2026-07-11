using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Identity;

/// <summary>bcrypt password hashing at cost 12 (spec §10 Security).</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
