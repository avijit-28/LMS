using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public interface IRagKnowledgeService
    {
        Task EnsureIndexPopulatedAsync();
        Task RebuildIndexAsync();
        Task<List<KnowledgeChunk>> SearchKnowledgeAsync(string query, int topK = 4);
        Task<string> GetUserLearningContextAsync(string userId);
        Task<List<CourseRecommendationCard>> ExtractCourseRecommendationsAsync(string query, List<KnowledgeChunk> chunks, string userId = null);
    }
}
