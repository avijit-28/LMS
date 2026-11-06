using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using LMS.Data;
using LMS.Models;

namespace LMS.Services
{
    public class EnrollmentService
    {
        private readonly ApplicationDbContext _db;

        public EnrollmentService(ApplicationDbContext db)
        {
            _db = db;
        }

        public bool IsEnrolled(string studentId, int courseId)
        {
            return _db.Enrollments.Any(e => e.StudentId == studentId && e.CourseId == courseId && e.Status != "Cancelled");
        }

        public bool HasActiveAccess(string studentId, int courseId)
        {
            var enrollment = _db.Enrollments.FirstOrDefault(e => e.StudentId == studentId && e.CourseId == courseId);
            if (enrollment == null) return false;
            if (enrollment.Status == "Cancelled") return false;
            if (enrollment.Status == "Completed") return true;
            if (enrollment.ExpiryDate.HasValue && DateTime.UtcNow > enrollment.ExpiryDate.Value)
            {
                return false; // Expired access
            }
            return true;
        }

        public bool EnrollStudent(string studentId, int courseId, out string errorMessage)
        {
            errorMessage = string.Empty;

            var existing = _db.Enrollments.FirstOrDefault(e => e.StudentId == studentId && e.CourseId == courseId);
            if (existing != null && existing.Status != "Cancelled" && (existing.Status == "Completed" || !existing.IsExpired))
            {
                errorMessage = "You already have active access to this course.";
                return false;
            }

            try
            {
                // Execute stored procedure sp_EnrollStudent
                _db.Database.ExecuteSqlCommand(
                    "EXEC sp_EnrollStudent @StudentId, @CourseId",
                    new SqlParameter("@StudentId", studentId),
                    new SqlParameter("@CourseId", courseId)
                );
                return true;
            }
            catch (Exception)
            {
                // Fallback to direct EF save
                try
                {
                    var course = _db.Courses.Find(courseId);
                    if (course == null)
                    {
                        errorMessage = "Course not found.";
                        return false;
                    }

                    int validityDays = course.ValidityInDays > 0 ? course.ValidityInDays : 30;

                    if (existing != null)
                    {
                        existing.Status = "Active";
                        existing.EnrollmentDate = DateTime.UtcNow;
                        existing.ExpiryDate = DateTime.UtcNow.AddDays(validityDays);
                        existing.CompletionPercentage = 0;
                        existing.CompletionDate = null;
                    }
                    else
                    {
                        var enrollment = new Enrollment
                        {
                            StudentId = studentId,
                            CourseId = courseId,
                            Status = "Active",
                            EnrollmentDate = DateTime.UtcNow,
                            ExpiryDate = DateTime.UtcNow.AddDays(validityDays),
                            CompletionPercentage = 0
                        };
                        _db.Enrollments.Add(enrollment);
                        course.TotalStudents += 1;
                    }
                    _db.SaveChanges();
                    return true;
                }
                catch (Exception inner)
                {
                    errorMessage = inner.Message;
                    return false;
                }
            }
        }

        public List<Enrollment> GetStudentEnrollments(string studentId)
        {
            return _db.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Course.Category)
                .Where(e => e.StudentId == studentId && e.Status != "Cancelled")
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();
        }
    }
}
