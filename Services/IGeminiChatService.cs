using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public interface IGeminiChatService
    {
        Task<ChatResponseViewModel> GenerateResponseAsync(
            string userPrompt,
            List<KnowledgeChunk> retrievedChunks,
            string userLearningContext,
            List<ChatMessage> conversationHistory);
    }
}
