using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;

namespace LMS.Services
{
    public class PaymentService
    {
        private readonly ApplicationDbContext _db;
        private readonly EnrollmentService _enrollmentService;

        public PaymentService(ApplicationDbContext db)
        {
            _db = db;
            _enrollmentService = new EnrollmentService(db);
        }

        public bool ProcessCoursePayment(string studentId, int courseId, string paymentMethod, out string transactionId, out string errorMessage, string customTransactionId = null)
        {
            errorMessage = string.Empty;
            transactionId = !string.IsNullOrWhiteSpace(customTransactionId) ? customTransactionId : ("TXN-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper());

            var course = _db.Courses.Find(courseId);
            if (course == null)
            {
                errorMessage = "Course not found.";
                return false;
            }

            if (_enrollmentService.HasActiveAccess(studentId, courseId))
            {
                errorMessage = "You already have active access to this course.";
                return false;
            }

            decimal adminFee = Math.Round(course.Price * 0.05m, 2);
            decimal teacherShare = course.Price - adminFee;

            var payment = new Payment
            {
                StudentId = studentId,
                CourseId = courseId,
                Amount = course.Price,
                AdminCommission = adminFee,
                InstructorEarnings = teacherShare,
                PaymentMethod = paymentMethod ?? "Credit Card (Mock/Test)",
                TransactionId = transactionId,
                PaymentStatus = "Completed",
                PaymentDate = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            _db.SaveChanges();

            // Enroll student
            bool enrolled = _enrollmentService.EnrollStudent(studentId, courseId, out string enrollError);
            if (enrolled)
            {
                var enrollment = _db.Enrollments.FirstOrDefault(e => e.StudentId == studentId && e.CourseId == courseId);
                if (enrollment != null)
                {
                    payment.EnrollmentId = enrollment.EnrollmentId;
                    _db.SaveChanges();
                }

                // Notify buyer
                _db.Notifications.Add(new Notification
                {
                    UserId = studentId,
                    Title = "Payment Successful",
                    Message = $"Your payment of Rs. {course.Price:N2} for '{course.Title}' was successful. You now have full access!",
                    NotificationType = "Payment",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });

                // Notify course instructor who created the course
                if (!string.IsNullOrEmpty(course.InstructorId) && course.InstructorId != studentId)
                {
                    var buyer = _db.Users.Find(studentId);
                    string buyerName = buyer != null ? (!string.IsNullOrWhiteSpace(buyer.FullName) ? buyer.FullName : buyer.UserName) : "A learner";
                    _db.Notifications.Add(new Notification
                    {
                        UserId = course.InstructorId,
                        Title = "New Course Purchase!",
                        Message = $"{buyerName} has purchased your course '{course.Title}' for Rs. {course.Price:N2}. Transaction: {transactionId}.",
                        NotificationType = "CourseSale",
                        IsRead = false,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                _db.SaveChanges();
                return true;
            }
            else
            {
                errorMessage = enrollError;
                return false;
            }
        }

        private string DetermineUserRole(ApplicationUser user, Dictionary<string, string> rolesMap)
        {
            if (user == null) return "Student";

            var userRoleNames = new List<string>();
            if (user.Roles != null && user.Roles.Any())
            {
                foreach (var r in user.Roles)
                {
                    if (rolesMap.TryGetValue(r.RoleId, out string rName) && !string.IsNullOrEmpty(rName))
                    {
                        userRoleNames.Add(rName);
                    }
                }
            }

            // Highest privilege / role specificity first
            if (userRoleNames.Any(r => string.Equals(r, "Instructor", StringComparison.OrdinalIgnoreCase)))
                return "Instructor";

            if (userRoleNames.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
                return "Admin";

            if (userRoleNames.Any(r => string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase)))
                return "Student";

            if (!string.IsNullOrEmpty(user.RequestedRole))
            {
                if (string.Equals(user.RequestedRole, "Instructor", StringComparison.OrdinalIgnoreCase)) return "Instructor";
                if (string.Equals(user.RequestedRole, "Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
            }
            return "Student";
        }

        public InstructorPaymentsViewModel GetInstructorPayments(string instructorId, string filter = "All", int? courseId = null, string search = null)
        {
            var instructorCourses = _db.Courses
                .Where(c => c.InstructorId == instructorId)
                .OrderBy(c => c.Title)
                .ToList();

            var courseIds = instructorCourses.Select(c => c.CourseId).ToList();

            var payments = courseIds.Any()
                ? _db.Payments
                    .Include(p => p.Course)
                    .Where(p => courseIds.Contains(p.CourseId))
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList()
                : new List<Payment>();

            var rolesMap = _db.Roles.ToDictionary(r => r.Id, r => r.Name);
            var buyerIds = payments.Select(p => p.StudentId).Distinct().ToList();
            var buyers = _db.Users
                .Include(u => u.Roles)
                .Where(u => buyerIds.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u);

            var records = new List<CoursePaymentRecordViewModel>();
            foreach (var p in payments)
            {
                buyers.TryGetValue(p.StudentId, out var buyer);
                string buyerRole = DetermineUserRole(buyer, rolesMap);

                decimal adminComm = p.AdminCommission > 0 ? p.AdminCommission : Math.Round(p.Amount * 0.05m, 2);
                decimal teacherEarn = p.InstructorEarnings > 0 ? p.InstructorEarnings : (p.Amount - adminComm);

                records.Add(new CoursePaymentRecordViewModel
                {
                    PaymentId = p.PaymentId,
                    TransactionId = p.TransactionId ?? ("TXN-" + p.PaymentId),
                    CourseId = p.CourseId,
                    CourseTitle = p.Course != null ? p.Course.Title : "Course #" + p.CourseId,
                    InstructorId = instructorId,
                    BuyerId = p.StudentId,
                    BuyerName = buyer != null ? (!string.IsNullOrWhiteSpace(buyer.FullName) ? buyer.FullName : buyer.UserName) : "Enrolled Learner",
                    BuyerEmail = buyer != null ? buyer.Email : "N/A",
                    BuyerRole = buyerRole,
                    Amount = p.Amount,
                    AdminCommission = adminComm,
                    InstructorEarnings = teacherEarn,
                    PaymentMethod = p.PaymentMethod,
                    PaymentStatus = p.PaymentStatus,
                    PaymentDate = p.PaymentDate
                });
            }

            var completed = records.Where(r => r.PaymentStatus == "Completed").ToList();
            decimal totalGross = completed.Sum(r => r.Amount);
            decimal totalNetEarnings = completed.Sum(r => r.InstructorEarnings);
            decimal totalCommissionDeducted = completed.Sum(r => r.AdminCommission);
            int totalPaidEnrollments = completed.Count;
            decimal avgOrderValue = totalPaidEnrollments > 0 ? Math.Round(totalGross / totalPaidEnrollments, 2) : 0;

            int studentPurchasesCount = records.Count(r => r.BuyerRole == "Student");
            int instructorPurchasesCount = records.Count(r => r.BuyerRole == "Instructor");

            var filteredRecords = records;
            if (!string.IsNullOrEmpty(filter) && filter != "All")
            {
                filteredRecords = filteredRecords.Where(r => string.Equals(r.BuyerRole, filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (courseId.HasValue && courseId.Value > 0)
            {
                filteredRecords = filteredRecords.Where(r => r.CourseId == courseId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                filteredRecords = filteredRecords.Where(r =>
                    (r.TransactionId != null && r.TransactionId.ToLower().Contains(q)) ||
                    (r.BuyerName != null && r.BuyerName.ToLower().Contains(q)) ||
                    (r.BuyerEmail != null && r.BuyerEmail.ToLower().Contains(q)) ||
                    (r.CourseTitle != null && r.CourseTitle.ToLower().Contains(q))
                ).ToList();
            }

            var courseSelectList = new List<System.Web.Mvc.SelectListItem>
            {
                new System.Web.Mvc.SelectListItem { Value = "", Text = "All My Courses", Selected = !courseId.HasValue }
            };
            foreach (var c in instructorCourses)
            {
                courseSelectList.Add(new System.Web.Mvc.SelectListItem
                {
                    Value = c.CourseId.ToString(),
                    Text = c.Title,
                    Selected = courseId.HasValue && courseId.Value == c.CourseId
                });
            }

            return new InstructorPaymentsViewModel
            {
                TotalGrossSales = totalGross,
                TotalEarnings = totalNetEarnings,
                TotalCommissionDeducted = totalCommissionDeducted,
                TotalPaidEnrollments = totalPaidEnrollments,
                AverageOrderValue = avgOrderValue,
                CurrentFilter = filter ?? "All",
                SelectedCourseId = courseId,
                SearchQuery = search,
                CourseList = courseSelectList,
                TotalTransactionsCount = records.Count,
                TotalStudentPurchasesCount = studentPurchasesCount,
                TotalInstructorPurchasesCount = instructorPurchasesCount,
                Transactions = filteredRecords
            };
        }

        public AdminPaymentsViewModel GetAdminPayments(string filter = "All", int? courseId = null, string search = null)
        {
            var payments = _db.Payments
                .Include(p => p.Course)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();

            var rolesMap = _db.Roles.ToDictionary(r => r.Id, r => r.Name);
            var buyerIds = payments.Select(p => p.StudentId).Distinct().ToList();
            var instructorIds = payments.Where(p => p.Course != null).Select(p => p.Course.InstructorId).Distinct().ToList();
            var allUserIds = buyerIds.Union(instructorIds).ToList();

            var users = _db.Users
                .Include(u => u.Roles)
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u);

            var records = new List<CoursePaymentRecordViewModel>();
            foreach (var p in payments)
            {
                users.TryGetValue(p.StudentId, out var buyer);
                ApplicationUser instructor = null;
                if (p.Course != null && !string.IsNullOrEmpty(p.Course.InstructorId))
                {
                    users.TryGetValue(p.Course.InstructorId, out instructor);
                }

                string buyerRole = DetermineUserRole(buyer, rolesMap);
                decimal adminComm = p.AdminCommission > 0 ? p.AdminCommission : Math.Round(p.Amount * 0.05m, 2);
                decimal teacherEarn = p.InstructorEarnings > 0 ? p.InstructorEarnings : (p.Amount - adminComm);

                records.Add(new CoursePaymentRecordViewModel
                {
                    PaymentId = p.PaymentId,
                    TransactionId = p.TransactionId ?? ("TXN-" + p.PaymentId),
                    CourseId = p.CourseId,
                    CourseTitle = p.Course != null ? p.Course.Title : "Course #" + p.CourseId,
                    InstructorId = p.Course != null ? p.Course.InstructorId : null,
                    InstructorName = instructor != null ? (!string.IsNullOrWhiteSpace(instructor.FullName) ? instructor.FullName : instructor.UserName) : "Course Instructor",
                    InstructorEmail = instructor != null ? instructor.Email : "N/A",
                    BuyerId = p.StudentId,
                    BuyerName = buyer != null ? (!string.IsNullOrWhiteSpace(buyer.FullName) ? buyer.FullName : buyer.UserName) : "Customer",
                    BuyerEmail = buyer != null ? buyer.Email : "N/A",
                    BuyerRole = buyerRole,
                    Amount = p.Amount,
                    AdminCommission = adminComm,
                    InstructorEarnings = teacherEarn,
                    PaymentMethod = p.PaymentMethod,
                    PaymentStatus = p.PaymentStatus,
                    PaymentDate = p.PaymentDate
                });
            }

            var completed = records.Where(r => r.PaymentStatus == "Completed").ToList();
            decimal totalRevenue = completed.Sum(r => r.Amount);
            decimal totalAdminCommission = completed.Sum(r => r.AdminCommission);
            decimal totalInstructorPayouts = completed.Sum(r => r.InstructorEarnings);
            int totalCount = records.Count;

            var studentCompleted = completed.Where(r => r.BuyerRole == "Student").ToList();
            decimal studentTotal = studentCompleted.Sum(r => r.Amount);
            int studentCount = records.Count(r => r.BuyerRole == "Student");

            var instructorCompleted = completed.Where(r => r.BuyerRole == "Instructor").ToList();
            decimal instructorTotal = instructorCompleted.Sum(r => r.Amount);
            int instructorCount = records.Count(r => r.BuyerRole == "Instructor");

            var filteredRecords = records;
            if (!string.IsNullOrEmpty(filter) && filter != "All")
            {
                filteredRecords = filteredRecords.Where(r => string.Equals(r.BuyerRole, filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (courseId.HasValue && courseId.Value > 0)
            {
                filteredRecords = filteredRecords.Where(r => r.CourseId == courseId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                filteredRecords = filteredRecords.Where(r =>
                    (r.TransactionId != null && r.TransactionId.ToLower().Contains(q)) ||
                    (r.BuyerName != null && r.BuyerName.ToLower().Contains(q)) ||
                    (r.BuyerEmail != null && r.BuyerEmail.ToLower().Contains(q)) ||
                    (r.InstructorName != null && r.InstructorName.ToLower().Contains(q)) ||
                    (r.CourseTitle != null && r.CourseTitle.ToLower().Contains(q))
                ).ToList();
            }

            var allCourses = _db.Courses.OrderBy(c => c.Title).ToList();
            var courseSelectList = new List<System.Web.Mvc.SelectListItem>
            {
                new System.Web.Mvc.SelectListItem { Value = "", Text = "All Platform Courses", Selected = !courseId.HasValue }
            };
            foreach (var c in allCourses)
            {
                courseSelectList.Add(new System.Web.Mvc.SelectListItem
                {
                    Value = c.CourseId.ToString(),
                    Text = c.Title,
                    Selected = courseId.HasValue && courseId.Value == c.CourseId
                });
            }

            return new AdminPaymentsViewModel
            {
                TotalRevenue = totalRevenue,
                TotalAdminCommission = totalAdminCommission,
                TotalInstructorPayouts = totalInstructorPayouts,
                TotalTransactionsCount = totalCount,
                StudentPurchasesTotal = studentTotal,
                StudentPurchasesCount = studentCount,
                InstructorPurchasesTotal = instructorTotal,
                InstructorPurchasesCount = instructorCount,
                CurrentFilter = filter ?? "All",
                SelectedCourseId = courseId,
                SearchQuery = search,
                CourseList = courseSelectList,
                Transactions = filteredRecords
            };
        }
    }
}
