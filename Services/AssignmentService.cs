using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class AssignmentService
    {
        private readonly ApplicationDbContext _db;

        public AssignmentService(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<Assignment> GetCourseAssignments(int courseId)
        {
            return _db.Assignments
                .Include(a => a.Submissions)
                .Where(a => a.CourseId == courseId)
                .OrderBy(a => a.DueDate)
                .ToList();
        }

        public Assignment GetAssignmentWithSubmissions(int assignmentId)
        {
            return _db.Assignments
                .Include(a => a.Course)
                .Include(a => a.Submissions)
                .FirstOrDefault(a => a.AssignmentId == assignmentId);
        }

        public bool SubmitAssignment(int assignmentId, string studentId, string submissionText, string fileUrl, out string errorMessage)
        {
            errorMessage = string.Empty;

            var assignment = _db.Assignments.Include(a => a.Course).FirstOrDefault(a => a.AssignmentId == assignmentId);
            if (assignment == null)
            {
                errorMessage = "Assignment not found.";
                return false;
            }

            // Verify enrollment
            bool isEnrolled = _db.Enrollments.Any(e => e.StudentId == studentId && e.CourseId == assignment.CourseId && e.Status != "Cancelled");
            if (!isEnrolled)
            {
                errorMessage = "You must be enrolled in this course to submit assignments.";
                return false;
            }

            var existing = _db.Submissions.FirstOrDefault(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
            if (existing != null && existing.Status == "Graded")
            {
                errorMessage = "This assignment has already been evaluated and graded by the instructor. Further submissions are not permitted.";
                return false;
            }

            bool isLate = assignment.DueDate.HasValue && DateTime.UtcNow > assignment.DueDate.Value;

            if (existing == null)
            {
                existing = new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    SubmissionText = submissionText,
                    FileUrl = fileUrl,
                    SubmittedDate = DateTime.UtcNow,
                    Status = isLate ? "Late" : "Submitted"
                };
                _db.Submissions.Add(existing);
            }
            else
            {
                existing.SubmissionText = submissionText;
                if (!string.IsNullOrEmpty(fileUrl))
                {
                    existing.FileUrl = fileUrl;
                }
                existing.SubmittedDate = DateTime.UtcNow;
                existing.Status = isLate ? "Late" : "Submitted";
            }

            // Notify instructor
            if (!string.IsNullOrEmpty(assignment.Course.InstructorId))
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = assignment.Course.InstructorId,
                    Title = "New Assignment Submission",
                    Message = $"A student has submitted work for '{assignment.Title}'.",
                    NotificationType = "Submission",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });
            }

            _db.SaveChanges();
            return true;
        }

        public bool GradeSubmission(int submissionId, decimal marks, string feedback, string instructorId, out string errorMessage)
        {
            errorMessage = string.Empty;

            var submission = _db.Submissions
                .Include(s => s.Assignment.Course)
                .FirstOrDefault(s => s.SubmissionId == submissionId);

            if (submission == null)
            {
                errorMessage = "Submission not found.";
                return false;
            }

            // Security check: Only the course instructor or an admin can grade
            var user = _db.Users.Find(instructorId);
            bool isAdmin = user != null && _db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM AspNetUserRoles ur INNER JOIN AspNetRoles r ON ur.RoleId=r.Id WHERE ur.UserId=@uid AND r.Name='Admin'",
                new System.Data.SqlClient.SqlParameter("@uid", instructorId)
            ).FirstOrDefault() > 0;

            if (submission.Assignment.Course.InstructorId != instructorId && !isAdmin)
            {
                errorMessage = "Unauthorized. You cannot grade courses you do not own.";
                return false;
            }

            if (marks < 0 || marks > submission.Assignment.MaxMarks)
            {
                errorMessage = $"Marks must be between 0 and {submission.Assignment.MaxMarks}.";
                return false;
            }

            submission.Marks = marks;
            submission.Feedback = feedback;
            submission.Status = "Graded";
            submission.GradedDate = DateTime.UtcNow;

            // Notify student
            _db.Notifications.Add(new Notification
            {
                UserId = submission.StudentId,
                Title = "Assignment Graded",
                Message = $"Your submission for '{submission.Assignment.Title}' has been graded: {marks}/{submission.Assignment.MaxMarks} marks.",
                NotificationType = "Grade",
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            });

            _db.SaveChanges();
            return true;
        }

        public List<Submission> GetStudentSubmissions(string studentId)
        {
            return _db.Submissions
                .Include(s => s.Assignment.Course)
                .Where(s => s.StudentId == studentId)
                .OrderByDescending(s => s.SubmittedDate)
                .ToList();
        }
    }
}
