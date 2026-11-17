using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CampusLostAndFound.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp", "image/pjpeg" };
        private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadItemImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return (true, null, null);
            }

            if (file.Length > MaxFileSizeInBytes)
            {
                return (false, null, "File size exceeds the 5 MB limit.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return (false, null, $"Unsupported file extension '{extension}'. Only JPG, PNG, and WEBP images are permitted.");
            }

            if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return (false, null, $"Invalid content MIME type '{file.ContentType}'.");
            }

            // Inspect magic bytes for image integrity
            try
            {
                using var stream = file.OpenReadStream();
                var headerBytes = new byte[8];
                int bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);
                if (bytesRead < 4)
                {
                    return (false, null, "Uploaded file content is corrupted or empty.");
                }

                bool isJpeg = headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF;
                bool isPng = headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47;
                bool isWebp = headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46; // RIFF

                if (!isJpeg && !isPng && !isWebp)
                {
                    return (false, null, "File content does not match a valid image signature.");
                }

                // Reset stream position
                stream.Position = 0;

                // Save to safe path
                string uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "items");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = $"{Guid.NewGuid():N}{extension}";
                string destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

                using var fileStream = new FileStream(destinationPath, FileMode.Create);
                await stream.CopyToAsync(fileStream);

                string relativePath = $"/uploads/items/{uniqueFileName}";
                return (true, relativePath, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image file {FileName}", file.FileName);
                return (false, null, "An error occurred while saving the image. Please try again.");
            }
        }

        public void DeleteImage(string? relativeFilePath)
        {
            if (string.IsNullOrWhiteSpace(relativeFilePath)) return;

            try
            {
                string cleanRelative = relativeFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), cleanRelative);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete image at {Path}", relativeFilePath);
            }
        }
    }
}

