using System.Security.Cryptography;
using System.Text;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.PartnerCompanion;

public class PartnerMobilePreference : TenantEntity
{
    public Guid UserId { get; set; }
    public string? QuickPinHash { get; set; }
    public string? QuickPinSalt { get; set; }
    public string? BiometricCredentialId { get; set; }
    public bool BiometricEnabled { get; set; }
    public int AutoLockMinutes { get; set; } = 5;
    public string? PushNotificationEndpoint { get; set; }
    public DateTime? LastActiveAt { get; set; }

    public static (string hash, string salt) HashPin(string pin)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToBase64String(saltBytes);

        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    public static bool VerifyPin(string pin, string hash, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
        string computed = Convert.ToBase64String(hashBytes);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash)
        );
    }
}
