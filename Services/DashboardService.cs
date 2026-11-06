using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public AdminDashboardViewModel GetAdminDashboard()
        {
            var vm = new AdminDashboardViewModel();

            vm.TotalUsers = _db.Users.Count();

            // Count users by role
            var studentRoleId = _db.Roles.Where(r => r.Name == "Student").Select(r => r.Id).FirstOrDefault();
            var instructorRoleId = _db.Roles.Where(r => r.Name == "Instructor").Select(r => r.Id).FirstOrDefault();

            if (!string.IsNullOrEmpty(studentRoleId))
            {
                vm.TotalStudents = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = @rid",
                    new System.Data.SqlClient.SqlParameter("@rid", studentRoleId)
                ).FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(instructorRoleId))
            {
                vm.TotalInstructors = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = @rid",
                    new System.Data.SqlClient.SqlParameter("@rid", instructorRoleId)
                ).FirstOrDefault();
            }

            vm.TotalCourses = _db.Courses.Count();
            vm.PublishedCourses = _db.Courses.Count(c => c.Status == "Published");
            vm.TotalEnrollments = _db.Enrollments.Count(e => e.Status != "Cancelled");
            vm.TotalCertificates = _db.Certificates.Count();
            vm.TotalCategories = _db.Categories.Count();
            vm.PendingApprovalsCount = _db.Users.Count(u => !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null));
            vm.PendingInstructorsCount = _db.Users.Count(u => !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null) && (u.RequestedRole == "Instructor" || (instructorRoleId != null && u.Roles.Any(r => r.RoleId == instructorRoleId))));

            vm.TotalRevenue = _db.Payments
                .Where(p => p.PaymentStatus == "Completed")
                .Select(p => (decimal?)p.Amount)
                .Sum() ?? 0;

            vm.RecentEnrollments = _db.Enrollments
                .Include(e => e.Course)
                .OrderByDescending(e => e.EnrollmentDate)
                .Take(5)
                .ToList();

            vm.RecentUsers = _db.Users
                .OrderByDescending(u => u.CreatedDate)
                .Take(5)
                .ToList();

            vm.RecentPayments = _db.Payments
                .Include(p => p.Course)
                .OrderByDescending(p => p.PaymentDate)
                .Take(5)
                .ToList();

            vm.CategoryStats = _db.Categories
                .Select(c => new CategoryStat
                {
                    CategoryName = c.CategoryName,
                    CourseCount = c.Courses.Count
                })
                .ToList();

            return vm;
        }

        public InstructorDashboardViewModel GetInstructorDashboard(string instructorId)
        {
            var courses = _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Enrollments)
                .Where(c => c.InstructorId == instructorId)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();

            var courseIds = courses.Select(c => c.CourseId).ToList();

            decimal totalRevenue = courseIds.Count > 0
                ? (_db.Payments
                    .Where(p => courseIds.Contains(p.CourseId) && p.PaymentStatus == "Completed")
                    .Select(p => (decimal?)p.Amount)
                    .Sum() ?? 0)
                : 0;

            int totalStudents = courses.Sum(c => c.TotalStudents);
            decimal avgRating = courses.Any(c => c.Rating > 0)
                ? (decimal)courses.Where(c => c.Rating > 0).Average(c => c.Rating)
                : 0;

            var recentEnrollments = courseIds.Count > 0
                ? _db.Enrollments
                    .Include(e => e.Course)
                    .Where(e => courseIds.Contains(e.CourseId))
                    .OrderByDescending(e => e.EnrollmentDate)
                    .Take(5)
                    .ToList()
                : new List<Enrollment>();

            return new InstructorDashboardViewModel
            {
                TotalCourses = courses.Count,
                TotalStudents = totalStudents,
                TotalRevenue = totalRevenue,
                AverageRating = Math.Round(avgRating, 1),
                Courses = courses,
                RecentEnrollments = recentEnrollments
            };
        }

        public StudentDashboardViewModel GetStudentDashboard(string studentId)
        {
            var enrollments = _db.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Course.Category)
                .Where(e => e.StudentId == studentId && e.Status != "Cancelled")
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            int activeCount = enrollments.Count(e => e.Status == "Active");
            int completedCount = enrollments.Count(e => e.Status == "Completed");

            var certificates = _db.Certificates
                .Include(c => c.Course)
                .Where(c => c.StudentId == studentId)
                .OrderByDescending(c => c.IssueDate)
                .ToList();

            var completedQuizzes = _db.QuizAttempts.Count(q => q.StudentId == studentId && q.CompletedAt != null);

            var studentUser = _db.Users.Find(studentId);
            string displayName = !string.IsNullOrWhiteSpace(studentUser?.FullName) ? studentUser.FullName : (studentUser?.UserName ?? "Learner");
            string firstName = LMS.Helpers.UserHelper.GetFirstName(displayName);

            var enrolledCourseIds = enrollments.Select(e => e.CourseId).ToList();
            var upcomingAssignments = enrolledCourseIds.Count > 0
                ? _db.Assignments
                    .Include(a => a.Course)
                    .Where(a => enrolledCourseIds.Contains(a.CourseId))
                    .OrderByDescending(a => a.AssignmentId)
                    .Take(6)
                    .ToList()
                : new List<Assignment>();

            var studentSubmissions = _db.Submissions
                .Where(s => s.StudentId == studentId)
                .ToList();

            var practicalTasks = new List<StudentTaskItemViewModel>();
            foreach (var a in upcomingAssignments)
            {
                var sub = studentSubmissions.FirstOrDefault(s => s.AssignmentId == a.AssignmentId);
                practicalTasks.Add(new StudentTaskItemViewModel
                {
                    AssignmentId = a.AssignmentId,
                    Title = a.Title,
                    CourseTitle = a.Course?.Title ?? "Course",
                    CourseId = a.CourseId,
                    MaxMarks = a.MaxMarks,
                    DueDate = a.DueDate,
                    IsSubmitted = sub != null,
                    SubmissionStatus = sub != null ? sub.Status : "Not Submitted",
                    MarksAwarded = sub?.Marks,
                    Feedback = sub?.Feedback
                });
            }

            var recentQuizResults = _db.QuizAttempts
                .Include(q => q.Quiz.Course)
                .Where(q => q.StudentId == studentId && q.CompletedAt != null)
                .OrderByDescending(q => q.CompletedAt)
                .Take(5)
                .ToList();

            var announcements = _db.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToList();

            return new StudentDashboardViewModel
            {
                StudentFirstName = firstName,
                ActiveCourses = activeCount,
                CompletedCourses = completedCount,
                CertificatesCount = certificates.Count,
                CompletedQuizzes = completedQuizzes,
                EnrolledCourses = enrollments,
                Certificates = certificates,
                Announcements = announcements,
                UpcomingAssignments = upcomingAssignments,
                PracticalTasks = practicalTasks,
                RecentQuizResults = recentQuizResults
            };
        }
    }
}
