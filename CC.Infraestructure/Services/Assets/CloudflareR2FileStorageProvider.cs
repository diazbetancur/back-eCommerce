using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using CC.Domain.Assets;
using Microsoft.Extensions.Options;

namespace CC.Infraestructure.Services.Assets;

public sealed class CloudflareR2FileStorageProvider : IFileStorageProvider
{
  private readonly TenantAssetsOptions _options;
  private readonly IAmazonS3 _s3Client;

  public CloudflareR2FileStorageProvider(IOptions<TenantAssetsOptions> options)
  {
    _options = options.Value;
    ValidateSettings(_options.CloudflareR2);

    var cfg = new AmazonS3Config
    {
      ServiceURL = _options.CloudflareR2.Endpoint,
      AuthenticationRegion = "auto",
      ForcePathStyle = true,
      UseHttp = false,
      RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
      ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
    };

    _s3Client = new AmazonS3Client(_options.CloudflareR2.AccessKey, _options.CloudflareR2.SecretKey, cfg);
  }

  public string ProviderName => "CloudflareR2";

  public async Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken ct = default)
  {
    var key = request.StorageKey.TrimStart('/');

    if (request.Content.CanSeek)
    {
      request.Content.Position = 0;
    }

    var putRequest = new PutObjectRequest
    {
      BucketName = _options.CloudflareR2.BucketName,
      Key = key,
      InputStream = request.Content,
      ContentType = request.ContentType,
      UseChunkEncoding = false,
      DisablePayloadSigning = true
    };

    await _s3Client.PutObjectAsync(putRequest, ct);

    var publicUrl = BuildPublicUrl(key);
    return new StorageUploadResult
    {
      StorageKey = key,
      UrlOrPath = publicUrl,
      StorageBucket = _options.CloudflareR2.BucketName,
      PublicUrl = publicUrl,
      Provider = ProviderName
    };
  }

  public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(storageKey))
    {
      return;
    }

    var deleteRequest = new DeleteObjectRequest
    {
      BucketName = _options.CloudflareR2.BucketName,
      Key = storageKey.TrimStart('/')
    };

    await _s3Client.DeleteObjectAsync(deleteRequest, ct);
  }

  private string BuildPublicUrl(string key)
  {
    if (string.IsNullOrWhiteSpace(_options.CloudflareR2.PublicBaseUrl))
    {
      return key;
    }

    return $"{_options.CloudflareR2.PublicBaseUrl.TrimEnd('/')}/{key}";
  }

  private static void ValidateSettings(CloudflareR2AssetsOptions options)
  {
    if (string.IsNullOrWhiteSpace(options.BucketName) ||
        string.IsNullOrWhiteSpace(options.Endpoint) ||
        string.IsNullOrWhiteSpace(options.AccessKey) ||
        string.IsNullOrWhiteSpace(options.SecretKey))
    {
      throw new InvalidOperationException("TenantAssets:CloudflareR2 configuration is missing (BucketName/Endpoint/AccessKey/SecretKey).");
    }
  }
}
