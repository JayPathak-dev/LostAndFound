using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CampusLostAndFound.Services
{
    public interface IFileUploadService
    {
        Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadItemImageAsync(IFormFile file);
        void DeleteImage(string? relativeFilePath);
    }
}
