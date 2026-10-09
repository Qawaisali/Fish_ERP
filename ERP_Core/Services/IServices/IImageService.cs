using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace ERP_Core.Services.IServices
{
    public interface IImageService
    {
        string? UploadImage(IFormFile file, string folderPath);
        List<string> UploadImages(IEnumerable<IFormFile> files, string folderPath);
        bool DeleteImage(string fileName, string folderPath);
        string? GetImagePath(string fileName, string folderPath);
    }
}
