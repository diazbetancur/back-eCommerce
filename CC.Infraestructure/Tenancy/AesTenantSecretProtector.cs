using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CC.Infraestructure.Tenancy;

public sealed class TenantSecretProtectionException : Exception
{
  public TenantSecretProtectionException(string message)
      : base(message)
  {
  }

  public TenantSecretProtectionException(string message, Exception innerException)
      : base(message, innerException)
  {
  }
}

public sealed class AesTenantSecretProtector : ITenantSecretProtector
{
  private const int KeySizeBytes = 32;
  private const int NonceSizeBytes = 12;
  private const int TagSizeBytes = 16;

  private readonly byte[] _key;

  public AesTenantSecretProtector(IOptions<TenantSecretsOptions> options)
  {
    var configuredOptions = options.Value;
    _key = DecodeAndValidateMasterKey(configuredOptions.MasterKey);

    if (!string.Equals(configuredOptions.Algorithm, "AES-256-GCM", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
          $"Unsupported TenantSecrets:Algorithm '{configuredOptions.Algorithm}'. Supported value: AES-256-GCM.");
    }
  }

  public string Encrypt(string plainText)
  {
    if (string.IsNullOrWhiteSpace(plainText))
    {
      throw new TenantSecretProtectionException("Cannot encrypt an empty tenant secret.");
    }

    var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
    var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
    var cipherBytes = new byte[plaintextBytes.Length];
    var tag = new byte[TagSizeBytes];

    using var aes = new AesGcm(_key, TagSizeBytes);
    aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

    return string.Join('.',
        Convert.ToBase64String(nonce),
        Convert.ToBase64String(tag),
        Convert.ToBase64String(cipherBytes));
  }

  public string Decrypt(string cipherText)
  {
    if (string.IsNullOrWhiteSpace(cipherText))
    {
      throw new TenantSecretProtectionException("Cannot decrypt an empty tenant secret.");
    }

    try
    {
      var parts = cipherText.Split('.');
      if (parts.Length != 3)
      {
        throw new TenantSecretProtectionException(
            "Invalid encrypted tenant secret format. Expected '<nonce>.<tag>.<ciphertext>'.");
      }

      var nonce = Convert.FromBase64String(parts[0]);
      var tag = Convert.FromBase64String(parts[1]);
      var cipherBytes = Convert.FromBase64String(parts[2]);

      if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
      {
        throw new TenantSecretProtectionException(
            "Invalid encrypted tenant secret payload. Nonce or tag size is invalid.");
      }

      var plaintextBytes = new byte[cipherBytes.Length];
      using var aes = new AesGcm(_key, TagSizeBytes);
      aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);

      return Encoding.UTF8.GetString(plaintextBytes);
    }
    catch (CryptographicException ex)
    {
      throw new TenantSecretProtectionException(
          "Failed to decrypt tenant secret. The master key may be different from the key used to encrypt the value.", ex);
    }
    catch (FormatException ex)
    {
      throw new TenantSecretProtectionException(
          "Encrypted tenant secret is not a valid Base64 payload.", ex);
    }
  }

  private static byte[] DecodeAndValidateMasterKey(string masterKey)
  {
    if (string.IsNullOrWhiteSpace(masterKey))
    {
      throw new InvalidOperationException(
          "TenantSecrets:MasterKey is required. Provide a Base64-encoded 32-byte key.");
    }

    try
    {
      var keyBytes = Convert.FromBase64String(masterKey);
      if (keyBytes.Length != KeySizeBytes)
      {
        throw new InvalidOperationException(
            "TenantSecrets:MasterKey must decode to exactly 32 bytes (AES-256).");
      }

      return keyBytes;
    }
    catch (FormatException ex)
    {
      throw new InvalidOperationException(
          "TenantSecrets:MasterKey must be a valid Base64 string.", ex);
    }
  }
}
