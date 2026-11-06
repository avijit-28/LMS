using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class RagKnowledgeService : IRagKnowledgeService
    {
        private readonly ApplicationDbContext _db;

        public RagKnowledgeService()
        {
            _db = new ApplicationDbContext();
        }

        public RagKnowledgeService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task EnsureIndexPopulatedAsync()
        {
            int count = await _db.KnowledgeChunks.CountAsync();
            if (count == 0)
            {
                await RebuildIndexAsync();
            }
        }

        public async Task RebuildIndexAsync()
        {
            // Clear existing knowledge chunks
            _db.Database.ExecuteSqlCommand("DELETE FROM KnowledgeChunks");

            var chunks = new List<KnowledgeChunk>();

            // 1. Index Courses & Categories
            var courses = await _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Modules.Select(m => m.Lessons))
                .Where(c => c.Status == "Published")
                .ToListAsync();

            foreach (var course in courses)
            {
                int totalLessons = course.Modules.SelectMany(m => m.Lessons).Count();
                int totalDuration = course.Modules.SelectMany(m => m.Lessons).Sum(l => l.DurationInMinutes);
                string instructorName = course.Instructor?.FullName ?? "Staff Faculty";
                string categoryName = course.Category?.CategoryName ?? "General";
                string priceStr = course.Price == 0 ? "Free" : $"₹{course.Price:N2}";

                // Course Overview Chunk
                chunks.Add(new KnowledgeChunk
                {
                    SourceType = "Course",
                    SourceId = course.CourseId,
                    Title = $"Course Overview: {course.Title}",
                    Content = $"Course: {course.Title}. Category: {categoryName}. Difficulty Level: {course.Level}. Price: {priceStr}. Instructor: {instructorName}. " +
                              $"Total Lessons: {totalLessons} across {course.Modules.Count} modules. Estimated Duration: {totalDuration} minutes. " +
                              $"Description: {course.Description} " +
                              (!string.IsNullOrEmpty(course.Level) ? $"Target Skill Level: {course.Level}. " : ""),
                    Keywords = $"{course.Title} {categoryName} {course.Level} {instructorName} syllabus learn curriculum",
                    UrlPath = $"/Courses/Details/{course.CourseId}",
                    LastUpdated = DateTime.UtcNow
                });

                // 2. Index Modules & Lessons
                foreach (var module in course.Modules.OrderBy(m => m.ModuleOrder))
                {
                    var lessonTitles = module.Lessons.OrderBy(l => l.LessonOrder).Select(l => $"{l.Title} ({l.DurationInMinutes}m)").ToList();
                    string lessonsSummary = string.Join(", ", lessonTitles);

                    chunks.Add(new KnowledgeChunk
                    {
                        SourceType = "Module",
                        SourceId = module.ModuleId,
                        Title = $"Curriculum Module: {course.Title} - {module.ModuleName}",
                        Content = $"Module #{module.ModuleOrder}: {module.ModuleName} for course '{course.Title}'. Description: {module.Description}. " +
                                  $"Lessons included: {(string.IsNullOrEmpty(lessonsSummary) ? "Lessons being prepared" : lessonsSummary)}.",
                        Keywords = $"{course.Title} {module.ModuleName} syllabus topics lessons chapters",
                        UrlPath = $"/Learning?courseId={course.CourseId}",
                        LastUpdated = DateTime.UtcNow
                    });

                    foreach (var lesson in module.Lessons.OrderBy(l => l.LessonOrder))
                    {
                        chunks.Add(new KnowledgeChunk
                        {
                            SourceType = "Lesson",
                            SourceId = lesson.LessonId,
                            Title = $"Lesson: {course.Title} > {lesson.Title}",
                            Content = $"Lesson in course '{course.Title}', Module '{module.ModuleName}': '{lesson.Title}'. Duration: {lesson.DurationInMinutes} minutes. " +
                                      $"Preview Available: {(lesson.IsPreview ? "Yes, free preview" : "Enrolled students only")}. " +
                                      (!string.IsNullOrEmpty(lesson.Content) ? $"Summary: {StripHtml(lesson.Content)}" : ""),
                            Keywords = $"{course.Title} {lesson.Title} {module.ModuleName} video lesson tutorial",
                            UrlPath = $"/Learning?courseId={course.CourseId}",
                            LastUpdated = DateTime.UtcNow
                        });
                    }
                }
            }

            // 3. Index Faculty & Instructors
            var instructors = await _db.Users
                .Where(u => u.IsApproved)
                .ToListAsync();

            foreach (var inst in instructors)
            {
                var instCourses = courses.Where(c => c.InstructorId == inst.Id).Select(c => c.Title).ToList();
                if (instCourses.Any())
                {
                    chunks.Add(new KnowledgeChunk
                    {
                        SourceType = "Instructor",
                        SourceId = 0,
                        Title = $"Faculty Profile: {inst.FullName}",
                        Content = $"Faculty Instructor: {inst.FullName}. Email: {inst.Email}. " +
                                  (!string.IsNullOrEmpty(inst.Bio) ? $"Biography: {inst.Bio}. " : "Specialist educator and industry practitioner. ") +
                                  $"Courses taught: {string.Join(", ", instCourses)}.",
                        Keywords = $"{inst.FullName} {inst.Email} instructor teacher faculty mentor",
                        UrlPath = "/Home/Instructors",
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }

            // 4. Platform Policies, Certification, and Navigation FAQs
            chunks.Add(new KnowledgeChunk
            {
                SourceType = "FAQ",
                SourceId = 1,
                Title = "Platform Credentialing: How Certificates Work",
                Content = "Students earn an official Certificate of Completion upon finishing 100% of curriculum lessons and passing the final exam or assessment quizzes. " +
                          "Every certificate comes with a unique Cryptographic ID (e.g. LMS-2026-XXXXXX) signed by Director of Academic Standards and the Faculty Lead Instructor. " +
                          "Credentials can be viewed, downloaded as an official PDF, or publicly verified at /Certificate/VerifyCertificate using the certificate verification code.",
                Keywords = "certificate graduation verify credential pdf download final exam passing",
                UrlPath = "/Certificate/VerifyCertificate",
                LastUpdated = DateTime.UtcNow
            });

            chunks.Add(new KnowledgeChunk
            {
                SourceType = "FAQ",
                SourceId = 2,
                Title = "Enrolling in Courses & Payment Options",
                Content = "Learners can browse all published courses at /Courses. Both Free and Premium courses are available. " +
                          "For paid tracks, EduPlatform supports secure online payment via Stripe credit/debit card and simulated checkout. " +
                          "Once enrolled, courses appear immediately on the student's My Courses page (/Student/MyCourses) and the interactive learning room (/Learning).",
                Keywords = "enroll buy purchase price pay free courses payment stripe fees",
                UrlPath = "/Courses",
                LastUpdated = DateTime.UtcNow
            });

            chunks.Add(new KnowledgeChunk
            {
                SourceType = "FAQ",
                SourceId = 3,
                Title = "Payments Ledger & Transaction Receipts",
                Content = "Students can view and download printed official receipts for their course transactions. " +
                          "Go to Student Portal > Receipts or visit /Student/Payments to download individual course receipts or /Student/AllReceipts to export all transaction histories.",
                Keywords = "receipt invoice payment proof transaction billing print download",
                UrlPath = "/Student/Payments",
                LastUpdated = DateTime.UtcNow
            });

            chunks.Add(new KnowledgeChunk
            {
                SourceType = "FAQ",
                SourceId = 4,
                Title = "Password Reset & Account Recovery",
                Content = "If you forget your password, navigate to /Account/ForgotPassword. Submit your registered email and a verification reason. " +
                          "The administration reviews and approves requests in the Admin Security Center. Once approved, return to the page, check your status, and set your new password directly.",
                Keywords = "password reset forgot recovery admin approval unlock login help",
                UrlPath = "/Account/ForgotPassword",
                LastUpdated = DateTime.UtcNow
            });

            chunks.Add(new KnowledgeChunk
            {
                SourceType = "FAQ",
                SourceId = 5,
                Title = "Becoming an Instructor on EduPlatform",
                Content = "Professionals can apply to become instructors by registering at /Account/Register and selecting the Instructor role. " +
                          "New instructor dossiers are reviewed by the administration. Once verified, instructors gain course creation studio privileges (/Instructor/Studio) to upload modules, video lessons, assignments, and quizzes.",
                Keywords = "teach instructor application become teacher studio author courses",
                UrlPath = "/Account/Register",
                LastUpdated = DateTime.UtcNow
            });

            _db.KnowledgeChunks.AddRange(chunks);
            await _db.SaveChangesAsync();
        }

        public async Task<List<KnowledgeChunk>> SearchKnowledgeAsync(string query, int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<KnowledgeChunk>();
            }

            await EnsureIndexPopulatedAsync();

            string cleanQuery = query.ToLower().Trim();
            var tokens = Regex.Matches(cleanQuery, @"\b[a-z0-9#\+]{2,}\b")
                              .Cast<Match>()
                              .Select(m => m.Value)
                              .Where(w => !IsStopword(w))
                              .Distinct()
                              .ToList();

            var allChunks = await _db.KnowledgeChunks.ToListAsync();

            var scoredChunks = new List<Tuple<KnowledgeChunk, double>>();

            foreach (var chunk in allChunks)
            {
                double score = 0;
                string chunkTitle = (chunk.Title ?? "").ToLower();
                string chunkContent = (chunk.Content ?? "").ToLower();
                string chunkKeywords = (chunk.Keywords ?? "").ToLower();

                // Exact phrase match
                if (chunkContent.Contains(cleanQuery) || chunkTitle.Contains(cleanQuery))
                {
                    score += 6.0;
                }

                // Title token matching
                foreach (var token in tokens)
                {
                    if (chunkTitle.Contains(token)) score += 3.5;
                    if (chunkKeywords.Contains(token)) score += 2.5;
                    if (chunkContent.Contains(token)) score += 1.0;
                }

                if (score > 0)
                {
                    scoredChunks.Add(new Tuple<KnowledgeChunk, double>(chunk, score));
                }
            }

            return scoredChunks
                .OrderByDescending(x => x.Item2)
                .Take(topK)
                .Select(x => x.Item1)
                .ToList();
        }

        public async Task<string> GetUserLearningContextAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return "Learner Profile: Guest visitor browsing the catalog. Not currently logged in.";
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return "Learner Profile: Guest visitor.";

            var enrollments = await _db.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId)
                .ToListAsync();

            var certs = await _db.Certificates
                .Include(c => c.Course)
                .Where(c => c.StudentId == userId)
                .ToListAsync();

            string enrollmentsSummary = enrollments.Any()
                ? string.Join("; ", enrollments.Select(e => $"'{e.Course?.Title}' ({(int)e.CompletionPercentage}% completed)"))
                : "None currently enrolled.";

            string certsSummary = certs.Any()
                ? string.Join(", ", certs.Select(c => $"'{c.Course?.Title}' (ID: {c.CertificateNumber})"))
                : "None yet.";

            return $"Active Learner: {user.FullName} ({user.Email}). " +
                   $"Enrolled Courses: {enrollmentsSummary}. " +
                   $"Earned Certificates: {certsSummary}.";
        }

        public async Task<List<CourseRecommendationCard>> ExtractCourseRecommendationsAsync(string query, List<KnowledgeChunk> chunks, string userId = null)
        {
            var recommendedCourseIds = new HashSet<int>();

            // 1. Collect course IDs from chunks
            foreach (var chunk in chunks)
            {
                if (chunk.SourceType == "Course" && chunk.SourceId > 0)
                {
                    recommendedCourseIds.Add(chunk.SourceId);
                }
            }

            // 2. Fallback: match course titles in query
            if (!recommendedCourseIds.Any())
            {
                var queryLower = query.ToLower();
                var matchingCourses = await _db.Courses
                    .Where(c => c.Status == "Published")
                    .ToListAsync();

                foreach (var c in matchingCourses)
                {
                    if (queryLower.Contains(c.Title.ToLower()) || 
                        (c.Category != null && queryLower.Contains(c.Category.CategoryName.ToLower())))
                    {
                        recommendedCourseIds.Add(c.CourseId);
                    }
                }
            }

            if (!recommendedCourseIds.Any())
            {
                return new List<CourseRecommendationCard>();
            }

            var enrollments = new Dictionary<int, Enrollment>();
            if (!string.IsNullOrEmpty(userId))
            {
                var userEnrollments = await _db.Enrollments
                    .Where(e => e.StudentId == userId && recommendedCourseIds.Contains(e.CourseId))
                    .ToListAsync();
                foreach (var e in userEnrollments)
                {
                    enrollments[e.CourseId] = e;
                }
            }

            var courseEntities = await _db.Courses
                .Include(c => c.Category)
                .Where(c => recommendedCourseIds.Contains(c.CourseId))
                .Take(3)
                .ToListAsync();

            var cards = new List<CourseRecommendationCard>();
            foreach (var c in courseEntities)
            {
                bool isEnrolled = enrollments.ContainsKey(c.CourseId);
                int progress = isEnrolled ? (int)enrollments[c.CourseId].CompletionPercentage : 0;

                cards.Add(new CourseRecommendationCard
                {
                    CourseId = c.CourseId,
                    Title = c.Title,
                    CategoryName = c.Category?.CategoryName ?? "General",
                    Level = c.Level,
                    Price = c.Price,
                    ThumbnailUrl = string.IsNullOrEmpty(c.Thumbnail) ? "/Content/images/course-default.jpg" : c.Thumbnail,
                    DetailUrl = $"/Courses/Details/{c.CourseId}",
                    LearnUrl = $"/Learning?courseId={c.CourseId}",
                    IsEnrolled = isEnrolled,
                    ProgressPercent = progress
                });
            }

            return cards;
        }

        private static bool IsStopword(string word)
        {
            var stopwords = new HashSet<string>
            {
                "the", "and", "for", "with", "that", "this", "what", "which", "how", "can",
                "you", "tell", "about", "show", "give", "help", "please", "course", "lms"
            };
            return stopwords.Contains(word);
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var stripped = Regex.Replace(input, "<.*?>", " ");
            return Regex.Replace(stripped, @"\s+", " ").Trim();
        }
    }
}
