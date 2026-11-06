using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using LMS.Data;
using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly DashboardService _dashboardService;
        private readonly EnrollmentService _enrollmentService;
        private readonly CertificateService _certificateService;
        private readonly QuizService _quizService;
        private readonly AssignmentService _assignmentService;

        public StudentController()
        {
            _dashboardService = new DashboardService(_db);
            _enrollmentService = new EnrollmentService(_db);
            _certificateService = new CertificateService(_db);
            _quizService = new QuizService(_db);
            _assignmentService = new AssignmentService(_db);
        }

        // GET: Student/Index
        public ActionResult Index()
        {
            string studentId = User.Identity.GetUserId();
            var model = _dashboardService.GetStudentDashboard(studentId);
            return View(model);
        }

        // GET: Student/MyCourses
        public ActionResult MyCourses()
        {
            string studentId = User.Identity.GetUserId();
            var enrollments = _enrollmentService.GetStudentEnrollments(studentId);
            return View(enrollments);
        }

        // GET: Student/Certificates
        public ActionResult Certificates()
        {
            string studentId = User.Identity.GetUserId();
            var certificates = _certificateService.GetStudentCertificates(studentId);
            return View(certificates);
        }

        // GET: Student/Quizzes
        public ActionResult Quizzes()
        {
            string studentId = User.Identity.GetUserId();
            var results = _quizService.GetStudentQuizHistory(studentId);
            return View(results);
        }

        // GET: Student/Assignments
        public ActionResult Assignments()
        {
            string studentId = User.Identity.GetUserId();
            var submissions = _assignmentService.GetStudentSubmissions(studentId);
            return View(submissions);
        }

        // GET: Student/Payments
        public ActionResult Payments()
        {
            string studentId = User.Identity.GetUserId();
            var payments = _db.Payments
                .Include(p => p.Course)
                .Include(p => p.Course.Category)
                .Include(p => p.Course.Instructor)
                .Where(p => p.StudentId == studentId)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();

            var student = _db.Users.Find(studentId);
            string studentName = student != null ? (!string.IsNullOrWhiteSpace(student.FullName) ? student.FullName : student.UserName) : "Student";

            var receipts = payments.Select(p => new StudentReceiptViewModel
            {
                PaymentId = p.PaymentId,
                InvoiceNumber = $"INV-{p.PaymentDate.Year}-{p.PaymentId:D6}",
                TransactionId = p.TransactionId ?? $"TXN-{p.PaymentId}",
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod,
                PaymentStatus = p.PaymentStatus,
                Amount = p.Amount,
                StudentId = studentId,
                StudentName = studentName,
                StudentEmail = student != null ? student.Email : "",
                CourseId = p.CourseId,
                CourseTitle = p.Course != null ? p.Course.Title : "Course #" + p.CourseId,
                CategoryName = p.Course?.Category?.CategoryName ?? "General",
                InstructorName = p.Course?.Instructor != null ? (!string.IsNullOrWhiteSpace(p.Course.Instructor.FullName) ? p.Course.Instructor.FullName : p.Course.Instructor.UserName) : "Faculty Lead",
                DurationHours = p.Course?.DurationInHours ?? 0
            }).ToList();

            return View(receipts);
        }

        // GET: Student/Receipt/{id}
        public ActionResult Receipt(int id)
        {
            string studentId = User.Identity.GetUserId();
            var payment = _db.Payments
                .Include(p => p.Course)
                .Include(p => p.Course.Category)
                .Include(p => p.Course.Instructor)
                .FirstOrDefault(p => p.PaymentId == id);

            if (payment == null)
            {
                return HttpNotFound("Receipt record not found.");
            }

            // Security check: only the student who paid or an admin can access this receipt
            if (payment.StudentId != studentId && !User.IsInRole("Admin"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Access Denied");
            }

            var student = _db.Users.Find(payment.StudentId);
            string studentName = student != null ? (!string.IsNullOrWhiteSpace(student.FullName) ? student.FullName : student.UserName) : "Student";

            var model = new StudentReceiptViewModel
            {
                PaymentId = payment.PaymentId,
                InvoiceNumber = $"INV-{payment.PaymentDate.Year}-{payment.PaymentId:D6}",
                TransactionId = payment.TransactionId ?? $"TXN-{payment.PaymentId}",
                PaymentDate = payment.PaymentDate,
                PaymentMethod = payment.PaymentMethod,
                PaymentStatus = payment.PaymentStatus,
                Amount = payment.Amount,
                StudentId = payment.StudentId,
                StudentName = studentName,
                StudentEmail = student != null ? student.Email : "student@lms.com",
                CourseId = payment.CourseId,
                CourseTitle = payment.Course != null ? payment.Course.Title : "Course #" + payment.CourseId,
                CategoryName = payment.Course?.Category?.CategoryName ?? "Technical Curriculum",
                InstructorName = payment.Course?.Instructor != null ? (!string.IsNullOrWhiteSpace(payment.Course.Instructor.FullName) ? payment.Course.Instructor.FullName : payment.Course.Instructor.UserName) : "Faculty Lead",
                DurationHours = payment.Course?.DurationInHours ?? 0
            };

            return View(model);
        }

        // GET: Student/AllReceipts
        public ActionResult AllReceipts()
        {
            string studentId = User.Identity.GetUserId();
            var payments = _db.Payments
                .Include(p => p.Course)
                .Include(p => p.Course.Category)
                .Include(p => p.Course.Instructor)
                .Where(p => p.StudentId == studentId && p.PaymentStatus == "Completed")
                .OrderByDescending(p => p.PaymentDate)
                .ToList();

            var student = _db.Users.Find(studentId);
            string studentName = student != null ? (!string.IsNullOrWhiteSpace(student.FullName) ? student.FullName : student.UserName) : "Student";

            var receipts = payments.Select(p => new StudentReceiptViewModel
            {
                PaymentId = p.PaymentId,
                InvoiceNumber = $"INV-{p.PaymentDate.Year}-{p.PaymentId:D6}",
                TransactionId = p.TransactionId ?? $"TXN-{p.PaymentId}",
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod,
                PaymentStatus = p.PaymentStatus,
                Amount = p.Amount,
                StudentId = studentId,
                StudentName = studentName,
                StudentEmail = student != null ? student.Email : "",
                CourseId = p.CourseId,
                CourseTitle = p.Course != null ? p.Course.Title : "Course #" + p.CourseId,
                CategoryName = p.Course?.Category?.CategoryName ?? "General",
                InstructorName = p.Course?.Instructor != null ? (!string.IsNullOrWhiteSpace(p.Course.Instructor.FullName) ? p.Course.Instructor.FullName : p.Course.Instructor.UserName) : "Faculty Lead",
                DurationHours = p.Course?.DurationInHours ?? 0
            }).ToList();

            var model = new StudentAllReceiptsViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                StudentEmail = student != null ? student.Email : "",
                GeneratedDate = DateTime.UtcNow,
                TotalAmountPaid = receipts.Sum(r => r.Amount),
                TotalCoursesPurchased = receipts.Count,
                Receipts = receipts
            };

            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
