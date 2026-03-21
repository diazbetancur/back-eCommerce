namespace CC.Infraestructure.Tenancy;

public interface ITenantSecretProtector
{
  string Encrypt(string plainText);
  string Decrypt(string cipherText);
}
