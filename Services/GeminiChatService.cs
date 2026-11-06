using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LMS.Models;
using LMS.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMS.Services
{
    public class GeminiChatService : IGeminiChatService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        public async Task<ChatResponseViewModel> GenerateResponseAsync(
            string userPrompt,
            List<KnowledgeChunk> retrievedChunks,
            string userLearningContext,
            List<ChatMessage> conversationHistory)
        {
            var response = new ChatResponseViewModel();
            string apiKey = EnvConfig.GeminiApiKey;
            // Check if we can make a remote Gemini API call
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    string geminiReply = await CallGeminiApiAsync(apiKey, userPrompt, retrievedChunks, userLearningContext, conversationHistory);
                    if (!string.IsNullOrWhiteSpace(geminiReply))
                    {
                        response.Success = true;
                        response.Reply = geminiReply;
                        response.ModelUsed = "Gemini 1.5 Flash (RAG Augmented)";
                        response.Suggestions = GenerateDynamicSuggestions(userPrompt, retrievedChunks);
                        return response;
                    }
                }
                catch
                {
                    // Fall through to smart local fallback if external API is unreachable or rate limited
                }
            }
            // Smart Local Grounded Semantic Fallback
            response.Success = true;
            response.Reply = GenerateLocalSemanticResponse(userPrompt, retrievedChunks, userLearningContext);
            response.ModelUsed = "EduBot Local Semantic Grounding";
            response.Suggestions = GenerateDynamicSuggestions(userPrompt, retrievedChunks);
            return response;
        }
        private async Task<string> CallGeminiApiAsync(
            string apiKey,
            string userPrompt,
            List<KnowledgeChunk> retrievedChunks,
            string userLearningContext,
            List<ChatMessage> conversationHistory)
        {
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var systemContextBuilder = new StringBuilder();
            systemContextBuilder.AppendLine("You are EduBot, the friendly, supportive, and knowledgeable AI Academic Advisor for EduPlatform LMS.");
            systemContextBuilder.AppendLine("Your job is to assist students and instructors with curriculum navigation, lesson questions, pricing, certification, and platform policies.");
            systemContextBuilder.AppendLine("RULES:");
            systemContextBuilder.AppendLine("1. Ground your response ONLY in the following RETRIEVED LMS KNOWLEDGE and ACTIVE LEARNER STATE.");
            systemContextBuilder.AppendLine("2. Do not invent courses, pricing, or features that do not exist in the retrieved context.");
            systemContextBuilder.AppendLine("3. If the user asks about their progress, reference their active enrollment data.");
            systemContextBuilder.AppendLine("4. Format with clean Markdown (bullet points, bold text for key titles). Keep responses friendly, encouraging, and concise (under 200 words).");
            systemContextBuilder.AppendLine();
            systemContextBuilder.AppendLine("=== RETRIEVED LMS KNOWLEDGE BASE ===");
            if (retrievedChunks != null && retrievedChunks.Any())
            {
                foreach (var chunk in retrievedChunks)
                {
                    systemContextBuilder.AppendLine($"[{chunk.SourceType}: {chunk.Title}]");
                    systemContextBuilder.AppendLine(chunk.Content);
                    systemContextBuilder.AppendLine();
                }
            }
            else
            {
                systemContextBuilder.AppendLine("No specific course chunks retrieved for this general query.");
            }
            systemContextBuilder.AppendLine("=== ACTIVE LEARNER CONTEXT ===");
            systemContextBuilder.AppendLine(userLearningContext ?? "Guest visitor.");
            systemContextBuilder.AppendLine();
            var contentsList = new List<object>();
            // Include system context in first turn or user prompt
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(systemContextBuilder.ToString());
            if (conversationHistory != null && conversationHistory.Any())
            {
                promptBuilder.AppendLine("=== RECENT CONVERSATION ===");
                foreach (var msg in conversationHistory.Take(4))
                {
                    promptBuilder.AppendLine($"{msg.Sender}: {msg.Content}");
                }
                promptBuilder.AppendLine();
            }
            promptBuilder.AppendLine($"User Query: {userPrompt}");
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = promptBuilder.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 600
                }
            };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync(endpoint, jsonContent);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return null;
            }
            string responseJson = await httpResponse.Content.ReadAsStringAsync();
            var parsed = JObject.Parse(responseJson);
            var replyText = parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            return replyText?.Trim();
        }
        private string GenerateLocalSemanticResponse(
            string userPrompt,
            List<KnowledgeChunk> retrievedChunks,
            string userLearningContext)
        {
            string pLower = (userPrompt ?? "").ToLower().Trim();
            // 1. User Progress Queries
            if (pLower.Contains("progress") || pLower.Contains("my course") || pLower.Contains("enrolled") || pLower.Contains("my status"))
            {
                if (userLearningContext.Contains("Guest visitor"))
                {
                    return "You are currently exploring EduPlatform as a **Guest**. To track your study progress, please [Sign In](/Account/Login) or [Register](/Account/Register) for free. Once signed in, your active tracks, lesson progress, and quiz scores will be displayed right here!";
                }
                return $"Here is your active learning summary from EduPlatform:\n\n{userLearningContext}\n\nKeep up the great momentum! You can resume your lessons anytime from your [Personal Dashboard](/Student) or [My Courses](/Student/MyCourses).";
            }
            // 2. Certification Queries
            if (pLower.Contains("certificate") || pLower.Contains("verify") || pLower.Contains("credential") || pLower.Contains("degree"))
            {
                return "🎓 **How Certification Works on EduPlatform:**\n\n" +
                       "1. **Complete All Lessons**: Reach 100% completion in all modules of your enrolled track.\n" +
                       "2. **Assessment Passing**: Pass the course quizzes and final exam with a minimum 40% threshold.\n" +
                       "3. **Official Cryptographic Credential**: Your official certificate is generated with a unique ID (e.g. `LMS-2026-XXXXXX`) and co-signed by the Director of Academic Standards and Faculty Lead.\n" +
                       "4. **Public Verification**: Any employer can verify credentials live at our [Certificate Verification Portal](/Certificate/VerifyCertificate).";
            }
            // 3. Pricing / Payment / Receipt Queries
            if (pLower.Contains("price") || pLower.Contains("cost") || pLower.Contains("free") || pLower.Contains("payment") || pLower.Contains("receipt") || pLower.Contains("stripe"))
            {
                if (pLower.Contains("receipt"))
                {
                    return "🧾 **Accessing Your Payment Receipts:**\n\n" +
                           "You can view and print official tax-compliant invoices for all your enrolled courses:\n" +
                           "- Go to **Student Portal > Receipts** or visit [Payment Receipts](/Student/Payments).\n" +
                           "- Click **Print Official Receipt** for any transaction, or export all your course statements at once via [All Receipts Summary](/Student/AllReceipts).";
                }
                return "💳 **Course Pricing & Enrollment Options:**\n\n" +
                       "- EduPlatform offers both **Free** foundational courses and **Premium Masterclasses**.\n" +
                       "- Transactions are processed securely via Stripe. Enrolled courses give you lifetime access to curriculum modules, video lessons, and certification.\n" +
                       "- Browse our full catalog anytime on the [Explore Courses](/Courses) page!";
            }
            // 4. Grounded in Retrieved Knowledge Chunks
            if (retrievedChunks != null && retrievedChunks.Any())
            {
                var sb = new StringBuilder();
                var primaryChunk = retrievedChunks.First();
                sb.AppendLine($"Here is what I found regarding **{primaryChunk.Title}** from the EduPlatform curriculum:");
                sb.AppendLine();
                sb.AppendLine(primaryChunk.Content);
                if (retrievedChunks.Count > 1)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Related Curriculum Details:**");
                    foreach (var chunk in retrievedChunks.Skip(1).Take(2))
                    {
                        sb.AppendLine($"- **{chunk.Title}**: {chunk.Content.Substring(0, Math.Min(140, chunk.Content.Length))}...");
                    }
                }
                if (!string.IsNullOrEmpty(primaryChunk.UrlPath))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Explore the full curriculum: [View Course Details]({primaryChunk.UrlPath})");
                }
                return sb.ToString();
            }
            // 5. Default Friendly Fallback
            return "Hello! I am **EduBot**, your AI Academic Advisor for EduPlatform LMS. " +
                   "I can help you explore our course catalog, view syllabus topics, check your learning progress, explain certification, or guide you through platform features.\n\n" +
                   "Try asking me:\n" +
                   "- *'What courses are available for beginners?'*\n" +
                   "- *'Tell me about Data Science modules and lessons'*;\n" +
                   "- *'How do I earn and verify my certificate?'*\n" +
                   "- *'What is my current course progress?'*";
        }
        private List<string> GenerateDynamicSuggestions(string query, List<KnowledgeChunk> chunks)
        {
            var suggestions = new List<string>();
            if (chunks != null && chunks.Any(c => c.SourceType == "Course"))
            {
                var courseChunk = chunks.FirstOrDefault(c => c.SourceType == "Course");
                if (courseChunk != null)
                {
                    string courseTitle = courseChunk.Title.Replace("Course Overview: ", "").Trim();
                    suggestions.Add($"What is the syllabus for {courseTitle}?");
                    suggestions.Add($"How much does {courseTitle} cost?");
                }
            }
            suggestions.Add("What courses are available for beginners?");
            suggestions.Add("How do I get certified?");
            suggestions.Add("What is my current progress?");
            return suggestions.Distinct().Take(4).ToList();
        }
    }
}