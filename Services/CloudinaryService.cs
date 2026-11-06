using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;

namespace LMS.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        private readonly string _apiKey;
        private readonly string _cloudName;
        private readonly string _apiSecret;
        private readonly string _uploadPreset;

        public CloudinaryService()
        {
            _apiKey = EnvConfig.CloudinaryApiKey;
            _cloudName = EnvConfig.CloudinaryCloudName;
            _apiSecret = EnvConfig.CloudinaryApiSecret;
            _uploadPreset = EnvConfig.CloudinaryUploadPreset;
        }

        public bool IsCloudinaryConfigured =>
            !string.IsNullOrWhiteSpace(_cloudName) &&
            (!string.IsNullOrWhiteSpace(_apiSecret) || !string.IsNullOrWhiteSpace(_uploadPreset));

        public string ApiKey => _apiKey;
        public string CloudName => _cloudName;

        public async Task<CloudinaryUploadResult> UploadPdfAsync(HttpPostedFileBase file, string folder = "lms_course_materials")
        {
            if (file == null || file.ContentLength == 0)
            {
                return new CloudinaryUploadResult
                {
                    Success = false,
                    ErrorMessage = "No file was provided for upload."
                };
            }

            var extension = Path.GetExtension(file.FileName)?.ToLower();
            if (extension != ".pdf")
            {
                return new CloudinaryUploadResult
                {
                    Success = false,
                    ErrorMessage = "Invalid file type. Only PDF documents (.pdf) are permitted."
                };
            }

            return await UploadPdfFromStreamAsync(file.InputStream, file.FileName, folder);
        }

        public async Task<CloudinaryUploadResult> UploadPdfFromStreamAsync(Stream fileStream, string fileName, string folder = "lms_course_materials")
        {
            // If Cloudinary credentials are fully configured, upload directly to Cloudinary
            if (IsCloudinaryConfigured)
            {
                try
                {
                    var result = await UploadToCloudinaryApiAsync(fileStream, fileName, folder);
                    if (result.Success)
                    {
                        return result;
                    }
                    // If Cloudinary returned an error, log and fallback to local storage
                    System.Diagnostics.Trace.TraceWarning("Cloudinary upload failed, falling back to local storage: " + result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Exception uploading to Cloudinary: " + ex.Message);
                }
            }

            // Resilient Local Storage Fallback
            return SaveFileLocally(fileStream, fileName, folder);
        }

        private async Task<CloudinaryUploadResult> UploadToCloudinaryApiAsync(Stream fileStream, string fileName, string folder)
        {
            var uploadUrl = $"https://api.cloudinary.com/v1_1/{_cloudName}/raw/upload";
            var timestamp = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString();

            using (var form = new MultipartFormDataContent())
            {
                // Reset stream position if possible
                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }

                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(streamContent, "file", fileName);

                form.Add(new StringContent(timestamp), "timestamp");

                if (!string.IsNullOrWhiteSpace(folder))
                {
                    form.Add(new StringContent(folder), "folder");
                }

                if (!string.IsNullOrWhiteSpace(_apiSecret))
                {
                    // Signed Upload: signature of sorted params + api_secret
                    var stringToSign = string.IsNullOrWhiteSpace(folder)
                        ? $"timestamp={timestamp}{_apiSecret}"
                        : $"folder={folder}&timestamp={timestamp}{_apiSecret}";

                    var signature = ComputeSha1Hash(stringToSign);
                    form.Add(new StringContent(_apiKey), "api_key");
                    form.Add(new StringContent(signature), "signature");
                }
                else if (!string.IsNullOrWhiteSpace(_uploadPreset))
                {
                    // Unsigned Upload
                    form.Add(new StringContent(_uploadPreset), "upload_preset");
                }

                var response = await _httpClient.PostAsync(uploadUrl, form);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Cloudinary API error ({response.StatusCode}): {responseContent}"
                    };
                }

                var json = JObject.Parse(responseContent);
                var secureUrl = json["secure_url"]?.ToString() ?? json["url"]?.ToString();
                var publicId = json["public_id"]?.ToString();
                var bytes = json["bytes"]?.ToObject<long>() ?? 0;
                var format = json["format"]?.ToString() ?? "pdf";

                return new CloudinaryUploadResult
                {
                    Success = true,
                    SecureUrl = secureUrl,
                    PublicId = publicId,
                    Format = format,
                    Bytes = bytes,
                    OriginalFilename = fileName,
                    IsLocalFallback = false
                };
            }
        }

        private CloudinaryUploadResult SaveFileLocally(Stream fileStream, string fileName, string folder)
        {
            try
            {
                var relativeDir = $"~/Content/uploads/{folder}";
                var physicalDir = HostingEnvironment.MapPath(relativeDir);

                if (!Directory.Exists(physicalDir))
                {
                    Directory.CreateDirectory(physicalDir);
                }

                var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
                var fullPath = Path.Combine(physicalDir, uniqueFileName);

                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }

                using (var dest = File.Create(fullPath))
                {
                    fileStream.CopyTo(dest);
                }

                var localUrl = $"/Content/uploads/{folder}/{uniqueFileName}";

                return new CloudinaryUploadResult
                {
                    Success = true,
                    SecureUrl = localUrl,
                    PublicId = $"local_{uniqueFileName}",
                    Format = "pdf",
                    Bytes = new FileInfo(fullPath).Length,
                    OriginalFilename = fileName,
                    IsLocalFallback = true,
                    ErrorMessage = IsCloudinaryConfigured
                        ? null
                        : "Cloudinary Cloud Name is not configured in Web.config. Saved locally to server storage."
                };
            }
            catch (Exception ex)
            {
                return new CloudinaryUploadResult
                {
                    Success = false,
                    ErrorMessage = "Failed to store file: " + ex.Message
                };
            }
        }

        private static string ComputeSha1Hash(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha1.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
