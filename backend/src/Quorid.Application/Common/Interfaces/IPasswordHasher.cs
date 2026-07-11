namespace Quorid.Application.Common.Interfaces;

/// <summary>Password hashing (bcrypt, cost 12 per spec §10 Security).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
