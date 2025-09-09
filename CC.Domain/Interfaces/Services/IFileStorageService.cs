using System.Threading.Tasks;

namespace CC.Domain.Interfaces.Services
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(string bucketName, string objectName, byte[] fileBytes, string contentType);
        Task DeleteFileAsync(string bucketName, string objectName);
        Task<string> UpdateFileAsync(string bucketName, string objectName, byte[] fileBytes, string contentType);
    }
}
