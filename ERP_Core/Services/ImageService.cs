using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;

namespace ERP_Core.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ImageService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public string? UploadImage(IFormFile file, string folderPath)
        {
            if (file == null || file.Length == 0)
                return null;

            string rootPath = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadFolder = Path.Combine(rootPath, folderPath.TrimStart('/', '\\'));

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            string ext = Path.GetExtension(file.FileName);
            string fileName = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(uploadFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            return fileName;
        }

        public List<string> UploadImages(IEnumerable<IFormFile> files, string folderPath)
        {
            var uploadedFileNames = new List<string>();
            if (files == null) return uploadedFileNames;

            foreach (var file in files)
            {
                var fileName = UploadImage(file, folderPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    uploadedFileNames.Add(fileName);
                }
            }

            return uploadedFileNames;
        }

        public bool DeleteImage(string fileName, string folderPath)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string rootPath = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string cleanFileName = Path.GetFileName(fileName);
            string filePath = Path.Combine(rootPath, folderPath.TrimStart('/', '\\'), cleanFileName);

            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public string? GetImagePath(string fileName, string folderPath)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            string normalizedFolder = folderPath.Replace("\\", "/").Trim('/');
            string cleanFileName = Path.GetFileName(fileName);
            return $"/{normalizedFolder}/{cleanFileName}";
        }
    }
}
