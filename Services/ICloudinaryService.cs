using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace LMS.Services
{
    public class CloudinaryUploadResult
    {
        public bool Success { get; set; }
        public string SecureUrl { get; set; }
        public string PublicId { get; set; }
        public string Format { get; set; }
        public long Bytes { get; set; }
        public string OriginalFilename { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsLocalFallback { get; set; }
    }

    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResult> UploadPdfAsync(HttpPostedFileBase file, string folder = "lms_course_materials");
        Task<CloudinaryUploadResult> UploadPdfFromStreamAsync(Stream fileStream, string fileName, string folder = "lms_course_materials");
        bool IsCloudinaryConfigured { get; }
        string ApiKey { get; }
        string CloudName { get; }
    }
}
