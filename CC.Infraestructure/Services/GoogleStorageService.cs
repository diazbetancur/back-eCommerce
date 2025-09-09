using CC.Domain.Interfaces.Services;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace CC.Infraestructure.Services
{
    public class GoogleStorageService : IFileStorageService
    {
        private readonly StorageClient storageClient;

        public GoogleStorageService(IConfiguration configuration)
        {
            var firebaseSection = configuration.GetSection("GoogleStorage:firebase");
            var json = firebaseSection.GetChildren()
                .ToDictionary(x => x.Key, x => x.Value);
            var jsonString = System.Text.Json.JsonSerializer.Serialize(json);
            GoogleCredential credential = GoogleCredential.FromJson(jsonString);
            storageClient = StorageClient.Create(credential);
        }

        public async Task<string> UploadFileAsync(string bucketName, string objectName, byte[] fileBytes, string contentType)
        {
            using var stream = new System.IO.MemoryStream(fileBytes);
            var result = await storageClient.UploadObjectAsync(
                bucketName,
                objectName,
                contentType,
                stream
            );
            return result.MediaLink;
        }

        public async Task DeleteFileAsync(string bucketName, string objectName)
        {
            await storageClient.DeleteObjectAsync(bucketName, objectName);
        }

        public async Task<string> UpdateFileAsync(string bucketName, string objectName, byte[] fileBytes, string contentType)
        {
            return await UploadFileAsync(bucketName, objectName, fileBytes, contentType);
        }
    }
}