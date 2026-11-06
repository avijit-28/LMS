using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LMS.Services
{
    public class AiServiceClient : IAiServiceClient
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };

        private readonly string _baseUrl;
        private readonly string _defaultInternalApiKey;

        public AiServiceClient()
        {
            _baseUrl = EnvConfig.AiServiceBaseUrl;
            _defaultInternalApiKey = EnvConfig.AiServiceInternalApiKey;
            if (!_baseUrl.EndsWith("/"))
            {
                _baseUrl += "/";
            }
        }

        private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string relativeUri, string internalApiKey = null, string authorizedCourses = null)
        {
            var request = new HttpRequestMessage(method, new Uri(new Uri(_baseUrl), relativeUri));
            var key = !string.IsNullOrWhiteSpace(internalApiKey) ? internalApiKey : _defaultInternalApiKey;
            request.Headers.Add("X-Internal-API-Key", key);

            if (!string.IsNullOrWhiteSpace(authorizedCourses))
            {
                request.Headers.Add("X-Authorized-Courses", authorizedCourses);
            }

            return request;
        }

        public async Task<AiChatResponseDto> AskQuestionAsync(AiChatRequestDto request, string internalApiKey, string authorizedCourses)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/chat/ask", internalApiKey, authorizedCourses))
                {
                    var jsonContent = JsonConvert.SerializeObject(request);
                    httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return new AiChatResponseDto
                        {
                            answer = $"Error connecting to AI service ({response.StatusCode}): {responseBody}",
                            grounded_in_rag = false
                        };
                    }

                    return JsonConvert.DeserializeObject<AiChatResponseDto>(responseBody);
                }
            }
            catch (Exception ex)
            {
                return new AiChatResponseDto
                {
                    answer = $"Failed to reach AI service: {ex.Message}. Please check if the AI engine is running.",
                    grounded_in_rag = false
                };
            }
        }

        public async Task<AiDocumentIndexResponseDto> IndexDocumentAsync(int documentId, string filePath, int courseId, int? moduleId, int? lessonId, string visibility, string title, string internalApiKey)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/documents/index", internalApiKey))
                {
                    var reqBody = new
                    {
                        document_id = documentId,
                        course_id = courseId,
                        module_id = moduleId,
                        lesson_id = lessonId,
                        title = title ?? Path.GetFileName(filePath),
                        file_path = filePath,
                        visibility = visibility ?? "student"
                    };

                    httpRequest.Content = new StringContent(JsonConvert.SerializeObject(reqBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return new AiDocumentIndexResponseDto
                        {
                            status = "Failed",
                            document_id = documentId,
                            message = $"Indexing failed ({response.StatusCode}): {responseBody}"
                        };
                    }

                    return JsonConvert.DeserializeObject<AiDocumentIndexResponseDto>(responseBody);
                }
            }
            catch (Exception ex)
            {
                return new AiDocumentIndexResponseDto
                {
                    status = "Failed",
                    document_id = documentId,
                    message = $"Exception during document indexing: {ex.Message}"
                };
            }
        }

        public async Task<bool> DeleteDocumentIndexAsync(int documentId, string internalApiKey)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Delete, $"api/v1/documents/{documentId}", internalApiKey))
                {
                    var response = await _httpClient.SendAsync(httpRequest);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<AiQuizResponseDto> GenerateQuizAsync(AiQuizRequestDto request, string internalApiKey, string authorizedCourses)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/features/generate-quiz", internalApiKey, authorizedCourses))
                {
                    httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) return null;
                    return JsonConvert.DeserializeObject<AiQuizResponseDto>(responseBody);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<AiSummarizeResponseDto> SummarizeAsync(AiSummarizeRequestDto request, string internalApiKey, string authorizedCourses)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/features/summarize", internalApiKey, authorizedCourses))
                {
                    httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) return null;
                    return JsonConvert.DeserializeObject<AiSummarizeResponseDto>(responseBody);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<AiCodeExplainResponseDto> ExplainCodeAsync(AiCodeExplainRequestDto request, string internalApiKey, string authorizedCourses)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/features/code-explain", internalApiKey, authorizedCourses))
                {
                    httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) return null;
                    return JsonConvert.DeserializeObject<AiCodeExplainResponseDto>(responseBody);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<AiStudyPlanResponseDto> GenerateStudyPlanAsync(AiStudyPlanRequestDto request, string internalApiKey, string authorizedCourses)
        {
            try
            {
                using (var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/features/study-plan", internalApiKey, authorizedCourses))
                {
                    httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) return null;
                    return JsonConvert.DeserializeObject<AiStudyPlanResponseDto>(responseBody);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<AiHealthResponseDto> GetHealthAsync()
        {
            try
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_baseUrl), "api/v1/health")))
                {
                    var response = await _httpClient.SendAsync(httpRequest);
                    if (!response.IsSuccessStatusCode) return null;
                    var responseBody = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<AiHealthResponseDto>(responseBody);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
