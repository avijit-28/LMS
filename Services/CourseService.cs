using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class CourseService
    {
        private readonly ApplicationDbContext _db;

        public CourseService(ApplicationDbContext db)
        {
            _db = db;
        }

        public CourseFilterViewModel GetFilteredCourses(string search, int? categoryId, string level, string priceType, string sortBy, int page = 1, int pageSize = 9)
        {
            var query = _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Where(c => c.Status == "Published");

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(c => c.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(level) && level != "All")
            {
                query = query.Where(c => c.Level == level);
            }

            if (!string.IsNullOrWhiteSpace(priceType))
            {
                if (priceType == "Free")
                {
                    query = query.Where(c => c.Price == 0);
                }
                else if (priceType == "Paid")
                {
                    query = query.Where(c => c.Price > 0);
                }
            }

            switch (sortBy)
            {
                case "rating":
                    query = query.OrderByDescending(c => c.Rating);
                    break;
                case "popular":
                    query = query.OrderByDescending(c => c.TotalStudents);
                    break;
                case "price_asc":
                    query = query.OrderBy(c => c.Price);
                    break;
                case "price_desc":
                    query = query.OrderByDescending(c => c.Price);
                    break;
                case "newest":
                default:
                    query = query.OrderByDescending(c => c.CreatedDate);
                    break;
            }

            int totalItems = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var categories = _db.Categories.Where(c => c.IsActive).OrderBy(c => c.CategoryName).ToList();

            return new CourseFilterViewModel
            {
                Search = search,
                CategoryId = categoryId,
                Level = level,
                PriceType = priceType,
                SortBy = sortBy,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Courses = items,
                Categories = categories
            };
        }

        public CourseDetailViewModel GetCourseDetail(int courseId, string studentId = null)
        {
            var course = _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Modules.Select(m => m.Lessons))
                .Include(c => c.Modules.Select(m => m.Quizzes))
                .Include(c => c.Modules.Select(m => m.Assignments))
                .Include(c => c.Quizzes)
                .Include(c => c.Assignments)
                .FirstOrDefault(c => c.CourseId == courseId);

            if (course == null) return null;

            ApplicationUser instructor = null;
            if (!string.IsNullOrEmpty(course.InstructorId))
            {
                instructor = _db.Users.Find(course.InstructorId);
            }

            var reviews = _db.CourseReviews
                .Where(r => r.CourseId == courseId)
                .OrderByDescending(r => r.CreatedDate)
                .ToList();

            Enrollment enrollment = null;
            CourseReview userReview = null;

            if (!string.IsNullOrEmpty(studentId))
            {
                enrollment = _db.Enrollments.FirstOrDefault(e => e.CourseId == courseId && e.StudentId == studentId && e.Status != "Cancelled");
                userReview = reviews.FirstOrDefault(r => r.StudentId == studentId);
            }

            var modules = course.Modules.OrderBy(m => m.ModuleOrder).ToList();
            foreach (var m in modules)
            {
                m.Lessons = m.Lessons.OrderBy(l => l.LessonOrder).ToList();
            }

            var allQuizzes = course.Quizzes.Where(q => q.IsActive).ToList();
            var finalExam = allQuizzes.FirstOrDefault(q => q.IsFinalExam);
            var moduleQuizzes = allQuizzes.Where(q => !q.IsFinalExam).OrderBy(q => q.CreatedDate).ToList();

            bool isFinalExamUnlocked = enrollment != null && enrollment.CompletionPercentage >= 100;
            bool hasPassedFinalExam = false;
            if (finalExam != null && !string.IsNullOrEmpty(studentId))
            {
                hasPassedFinalExam = _db.QuizAttempts.Any(a => a.StudentId == studentId && a.QuizId == finalExam.QuizId && a.IsPassed);
            }

            decimal avgRating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : course.Rating;

            return new CourseDetailViewModel
            {
                Course = course,
                Instructor = instructor,
                Modules = modules,
                Quizzes = allQuizzes,
                ModuleQuizzes = moduleQuizzes,
                FinalExam = finalExam,
                IsFinalExamUnlocked = isFinalExamUnlocked,
                HasPassedFinalExam = hasPassedFinalExam,
                Assignments = course.Assignments.OrderBy(a => a.DueDate).ToList(),
                Reviews = reviews,
                IsEnrolled = enrollment != null,
                Enrollment = enrollment,
                HasReviewed = userReview != null,
                UserReview = userReview,
                AverageRating = Math.Round(avgRating, 1),
                TotalReviews = reviews.Count
            };
        }
    }
}
