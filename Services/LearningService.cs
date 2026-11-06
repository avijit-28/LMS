using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class LearningService
    {
        private readonly ApplicationDbContext _db;
        private readonly CertificateService _certificateService;

        public LearningService(ApplicationDbContext db)
        {
            _db = db;
            _certificateService = new CertificateService(db);
        }

        public LearningViewModel GetLearningState(int courseId, string studentId, int? lessonId = null)
        {
            var course = _db.Courses
                .Include(c => c.Modules.Select(m => m.Lessons))
                .Include(c => c.Modules.Select(m => m.Quizzes))
                .Include(c => c.Modules.Select(m => m.Assignments))
                .Include(c => c.Quizzes)
                .Include(c => c.Assignments)
                .FirstOrDefault(c => c.CourseId == courseId);

            if (course == null) return null;

            var enrollment = _db.Enrollments
                .FirstOrDefault(e => e.CourseId == courseId && e.StudentId == studentId && e.Status != "Cancelled");

            if (enrollment == null)
            {
                // Allow course instructor or admin to preview classroom
                bool isInstructor = !string.IsNullOrEmpty(studentId) && course.InstructorId == studentId;
                bool isAdmin = false;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var adminRoleId = _db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefault();
                    if (!string.IsNullOrEmpty(adminRoleId))
                    {
                        isAdmin = _db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM AspNetUserRoles WHERE UserId = @uid AND RoleId = @rid",
                            new System.Data.SqlClient.SqlParameter("@uid", studentId),
                            new System.Data.SqlClient.SqlParameter("@rid", adminRoleId)
                        ).FirstOrDefault() > 0;
                    }
                }

                if (!isInstructor && !isAdmin)
                {
                    return null;
                }
            }

            var modules = course.Modules.OrderBy(m => m.ModuleOrder).ToList();
            var allLessons = new List<Lesson>();
            foreach (var m in modules)
            {
                m.Lessons = m.Lessons.OrderBy(l => l.LessonOrder).ToList();
                allLessons.AddRange(m.Lessons);
            }

            // Find current lesson
            Lesson currentLesson = null;
            if (lessonId.HasValue && lessonId.Value > 0)
            {
                currentLesson = allLessons.FirstOrDefault(l => l.LessonId == lessonId.Value);
            }

            // If not found or not specified, pick first incomplete lesson or first lesson
            var allLessonIds = allLessons.Select(al => al.LessonId).ToList();
            var completedLessonIds = allLessonIds.Count > 0
                ? new HashSet<int>(
                    _db.LessonProgresses
                        .Where(lp => lp.StudentId == studentId && lp.IsCompleted && allLessonIds.Contains(lp.LessonId))
                        .Select(lp => lp.LessonId)
                )
                : new HashSet<int>();

            if (currentLesson == null)
            {
                currentLesson = allLessons.FirstOrDefault(l => !completedLessonIds.Contains(l.LessonId)) ?? allLessons.FirstOrDefault();
            }

            Module currentModule = null;
            if (currentLesson != null)
            {
                currentModule = modules.FirstOrDefault(m => m.ModuleId == currentLesson.ModuleId);
            }

            int currentIndex = currentLesson != null ? allLessons.IndexOf(currentLesson) : -1;
            int? prevLessonId = currentIndex > 0 ? allLessons[currentIndex - 1].LessonId : (int?)null;
            int? nextLessonId = (currentIndex >= 0 && currentIndex < allLessons.Count - 1) ? allLessons[currentIndex + 1].LessonId : (int?)null;

            // Tasks and Quizzes completion tracking
            var submittedAssignmentIds = !string.IsNullOrEmpty(studentId)
                ? new HashSet<int>(_db.Submissions.Where(s => s.StudentId == studentId && s.Assignment.CourseId == courseId).Select(s => s.AssignmentId))
                : new HashSet<int>();

            var passedQuizIds = !string.IsNullOrEmpty(studentId)
                ? new HashSet<int>(_db.QuizAttempts.Where(a => a.StudentId == studentId && a.Quiz.CourseId == courseId && a.IsPassed).Select(a => a.QuizId))
                : new HashSet<int>();

            var allQuizzes = course.Quizzes.Where(q => q.IsActive).ToList();
            var finalExam = allQuizzes.FirstOrDefault(q => q.IsFinalExam);
            var moduleQuizzes = allQuizzes.Where(q => !q.IsFinalExam).ToList();
            var courseAssignments = course.Assignments.ToList();

            // Total Module curriculum items (Lessons + Tasks + Module Quizzes)
            int totalModuleItems = allLessons.Count + moduleQuizzes.Count + courseAssignments.Count;
            int completedModuleItems = completedLessonIds.Count
                + moduleQuizzes.Count(q => passedQuizIds.Contains(q.QuizId))
                + courseAssignments.Count(a => submittedAssignmentIds.Contains(a.AssignmentId));

            decimal progress = 0;
            if (totalModuleItems > 0)
            {
                progress = Math.Round((decimal)completedModuleItems * 100m / totalModuleItems, 1);
            }
            else
            {
                progress = 100m;
            }

            bool isModulesComplete = totalModuleItems == 0 || completedModuleItems >= totalModuleItems;
            bool isFinalExamUnlocked = isModulesComplete;

            bool hasPassedFinalExam = false;
            decimal finalExamScore = 0;
            if (finalExam != null && !string.IsNullOrEmpty(studentId))
            {
                hasPassedFinalExam = passedQuizIds.Contains(finalExam.QuizId);
                var bestAttempt = _db.QuizAttempts
                    .Where(a => a.QuizId == finalExam.QuizId && a.StudentId == studentId)
                    .OrderByDescending(a => a.Score)
                    .FirstOrDefault();
                if (bestAttempt != null)
                {
                    finalExamScore = bestAttempt.Score;
                }
            }

            bool isCourseCompleted = false;
            if (finalExam != null)
            {
                isCourseCompleted = isModulesComplete && hasPassedFinalExam;
            }
            else
            {
                isCourseCompleted = isModulesComplete;
            }

            Certificate certificate = null;
            if (isCourseCompleted && !string.IsNullOrEmpty(studentId))
            {
                certificate = _db.Certificates.FirstOrDefault(c => c.CourseId == courseId && c.StudentId == studentId);
                if (certificate == null && enrollment != null)
                {
                    _certificateService.GenerateCertificate(studentId, courseId, finalExam != null ? finalExamScore : progress);
                    certificate = _db.Certificates.FirstOrDefault(c => c.CourseId == courseId && c.StudentId == studentId);
                }
            }

            if (enrollment != null)
            {
                enrollment.CompletionPercentage = progress;
                if (isCourseCompleted && enrollment.Status != "Completed")
                {
                    enrollment.Status = "Completed";
                    enrollment.CompletionDate = DateTime.UtcNow;
                }
                _db.SaveChanges();
            }

            return new LearningViewModel
            {
                Course = course,
                CurrentModule = currentModule,
                CurrentLesson = currentLesson,
                Modules = modules,
                CompletedLessonIds = completedLessonIds,
                SubmittedAssignmentIds = submittedAssignmentIds,
                PassedQuizIds = passedQuizIds,
                CourseAssignments = courseAssignments,
                ModuleQuizzes = moduleQuizzes,
                FinalExam = finalExam,
                IsFinalExamUnlocked = isFinalExamUnlocked,
                HasPassedFinalExam = hasPassedFinalExam,
                FinalExamScore = finalExamScore,
                ProgressPercentage = progress,
                PreviousLessonId = prevLessonId,
                NextLessonId = nextLessonId,
                IsCurrentCompleted = currentLesson != null && completedLessonIds.Contains(currentLesson.LessonId),
                IsCourseCompleted = isCourseCompleted,
                Certificate = certificate
            };
        }

        public decimal MarkLessonComplete(string studentId, int lessonId, out bool isCourseCompleted)
        {
            isCourseCompleted = false;

            var lesson = _db.Lessons.Include(l => l.Module).FirstOrDefault(l => l.LessonId == lessonId);
            if (lesson == null) return 0;

            int courseId = lesson.Module.CourseId;

            // Execute stored procedure or fallback update
            try
            {
                _db.Database.ExecuteSqlCommand(
                    "EXEC sp_MarkLessonComplete @StudentId, @LessonId",
                    new SqlParameter("@StudentId", studentId),
                    new SqlParameter("@LessonId", lessonId)
                );
            }
            catch
            {
                var progressRecord = _db.LessonProgresses.FirstOrDefault(p => p.StudentId == studentId && p.LessonId == lessonId);
                if (progressRecord != null)
                {
                    progressRecord.IsCompleted = true;
                    progressRecord.CompletedDate = DateTime.UtcNow;
                    progressRecord.LastAccessedDate = DateTime.UtcNow;
                }
                else
                {
                    _db.LessonProgresses.Add(new LessonProgress
                    {
                        StudentId = studentId,
                        LessonId = lessonId,
                        IsCompleted = true,
                        CompletedDate = DateTime.UtcNow,
                        LastAccessedDate = DateTime.UtcNow
                    });
                }
                _db.SaveChanges();
            }

            // Recalculate Course Curriculum Completion (Lessons + Tasks + Module Quizzes)
            var allLessons = _db.Lessons.Where(l => l.Module.CourseId == courseId).Select(l => l.LessonId).ToList();
            int totalLessons = allLessons.Count;
            int completedLessons = totalLessons > 0
                ? _db.LessonProgresses.Count(p => p.StudentId == studentId && p.IsCompleted && allLessons.Contains(p.LessonId))
                : 0;

            var moduleQuizzes = _db.Quizzes.Where(q => q.CourseId == courseId && !q.IsFinalExam && q.IsActive).Select(q => q.QuizId).ToList();
            int totalModuleQuizzes = moduleQuizzes.Count;
            int passedModuleQuizzes = totalModuleQuizzes > 0
                ? _db.QuizAttempts.Where(a => a.StudentId == studentId && a.IsPassed && moduleQuizzes.Contains(a.QuizId)).Select(a => a.QuizId).Distinct().Count()
                : 0;

            var assignments = _db.Assignments.Where(a => a.CourseId == courseId).Select(a => a.AssignmentId).ToList();
            int totalAssignments = assignments.Count;
            int submittedAssignments = totalAssignments > 0
                ? _db.Submissions.Where(s => s.StudentId == studentId && assignments.Contains(s.AssignmentId)).Select(s => s.AssignmentId).Distinct().Count()
                : 0;

            int totalModuleItems = totalLessons + totalModuleQuizzes + totalAssignments;
            int completedModuleItems = completedLessons + passedModuleQuizzes + submittedAssignments;

            decimal completionPercentage = totalModuleItems > 0
                ? Math.Round((decimal)completedModuleItems * 100m / totalModuleItems, 2)
                : 100m;

            var finalExam = _db.Quizzes.FirstOrDefault(q => q.CourseId == courseId && q.IsFinalExam && q.IsActive);
            bool hasFinalExam = (finalExam != null);
            bool hasPassedFinalExam = hasFinalExam && _db.QuizAttempts.Any(a => a.StudentId == studentId && a.QuizId == finalExam.QuizId && a.IsPassed);

            var enrollment = _db.Enrollments.FirstOrDefault(e => e.CourseId == courseId && e.StudentId == studentId);
            if (enrollment != null)
            {
                // If course validity has expired, block completing and certificate issuance until re-enrolled
                if (enrollment.IsExpired && enrollment.Status != "Completed")
                {
                    return enrollment.CompletionPercentage;
                }

                enrollment.CompletionPercentage = completionPercentage;

                if (completedModuleItems >= totalModuleItems)
                {
                    if (!hasFinalExam)
                    {
                        // No final exam required - module completion directly awards certificate
                        enrollment.Status = "Completed";
                        enrollment.CompletionDate = DateTime.UtcNow;
                        isCourseCompleted = true;
                        _certificateService.GenerateCertificate(studentId, courseId, 100);
                    }
                    else if (hasPassedFinalExam)
                    {
                        // Final exam already cleared
                        enrollment.Status = "Completed";
                        enrollment.CompletionDate = DateTime.UtcNow;
                        isCourseCompleted = true;
                        var bestAttempt = _db.QuizAttempts.Where(a => a.QuizId == finalExam.QuizId && a.StudentId == studentId).OrderByDescending(a => a.Score).FirstOrDefault();
                        _certificateService.GenerateCertificate(studentId, courseId, bestAttempt != null ? bestAttempt.Score : 100);
                    }
                    else
                    {
                        // 100% module complete, but final exam is unlocked and mandatory!
                        isCourseCompleted = false;
                    }
                }
                _db.SaveChanges();
            }

            return completionPercentage;
        }
    }
}
