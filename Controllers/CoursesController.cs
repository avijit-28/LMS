using System;
using System.Linq;
using System.Web.Mvc;
using LMS.Data;
using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly CourseService _courseService;
        private readonly EnrollmentService _enrollmentService;
        private readonly PaymentService _paymentService;
        private readonly StripeService _stripeService;

        public CoursesController()
        {
            _courseService = new CourseService(_db);
            _enrollmentService = new EnrollmentService(_db);
            _paymentService = new PaymentService(_db);
            _stripeService = new StripeService();
        }

        // GET: Courses
        public ActionResult Index(string search, int? categoryId, string level, string priceType, string sortBy, int page = 1)
        {
            var model = _courseService.GetFilteredCourses(search, categoryId, level, priceType, sortBy, page, 9);
            return View(model);
        }

        // GET: Courses/Details/5
        public ActionResult Details(int id)
        {
            string studentId = User.Identity.IsAuthenticated ? User.Identity.GetUserId() : null;
            var model = _courseService.GetCourseDetail(id, studentId);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        // GET: Courses/FreePreview?lessonId=5
        [HttpGet]
        [AllowAnonymous]
        public ActionResult FreePreview(int lessonId)
        {
            var lesson = _db.Lessons.Include("Module.Course").FirstOrDefault(l => l.LessonId == lessonId);
            if (lesson == null)
            {
                return Json(new { success = false, message = "Lesson not found." }, JsonRequestBehavior.AllowGet);
            }

            if (!lesson.IsPreview)
            {
                return Json(new { success = false, message = "This lesson is not marked for free preview." }, JsonRequestBehavior.AllowGet);
            }

            string embedUrl = !string.IsNullOrWhiteSpace(lesson.VideoUrl) ? LMS.Helpers.VideoHelper.ToEmbedUrl(lesson.VideoUrl) : string.Empty;
            bool isDirectVideo = !string.IsNullOrWhiteSpace(lesson.VideoUrl) && LMS.Helpers.VideoHelper.IsDirectVideoFile(lesson.VideoUrl);

            return Json(new
            {
                success = true,
                lessonId = lesson.LessonId,
                title = lesson.Title,
                duration = lesson.DurationInMinutes,
                lessonType = lesson.LessonType,
                videoUrl = lesson.VideoUrl ?? string.Empty,
                embedUrl = embedUrl,
                isDirectVideo = isDirectVideo,
                summary = lesson.Description ?? string.Empty,
                content = lesson.Content ?? string.Empty,
                resourceUrl = lesson.ResourceUrl ?? string.Empty,
                courseTitle = lesson.Module?.Course?.Title ?? string.Empty
            }, JsonRequestBehavior.AllowGet);
        }

        // POST: Courses/Enroll
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult Enroll(int courseId)
        {
            string currentUserId = User.Identity.GetUserId();
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            // If instructor of the course or Admin, grant direct access to learning
            bool isAdmin = User.IsInRole("Admin");
            if (course.InstructorId == currentUserId || isAdmin)
            {
                TempData["InfoMessage"] = isAdmin
                    ? "As an Administrator, you have full access to the classroom."
                    : "You are the instructor of this course. You have direct access to the classroom.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            if (_enrollmentService.HasActiveAccess(currentUserId, courseId))
            {
                TempData["InfoMessage"] = "You already have active access to this course.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            if (course.Price > 0)
            {
                // Must go through payment flow
                return RedirectToAction("Payment", new { courseId });
            }

            bool success = _enrollmentService.EnrollStudent(currentUserId, courseId, out string errorMessage);
            if (!success)
            {
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("Details", new { id = courseId });
            }

            TempData["SuccessMessage"] = "Successfully enrolled in the course! Start learning now.";
            return RedirectToAction("Index", "Learning", new { courseId });
        }

        // GET: Courses/Purchase
        [Authorize]
        public ActionResult Purchase(int courseId)
        {
            return RedirectToAction("Payment", new { courseId });
        }

        // POST: Courses/Purchase
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult Purchase(int courseId, string paymentMethod)
        {
            return Payment(courseId, paymentMethod);
        }

        // GET: Courses/Payment
        [Authorize]
        public ActionResult Payment(int courseId)
        {
            string currentUserId = User.Identity.GetUserId();
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            bool isAdmin = User.IsInRole("Admin");
            if (course.InstructorId == currentUserId || isAdmin)
            {
                TempData["InfoMessage"] = isAdmin
                    ? "As an Administrator, you have full access to the classroom."
                    : "You are the instructor of this course. You have direct access to the classroom.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            if (_enrollmentService.HasActiveAccess(currentUserId, courseId))
            {
                TempData["InfoMessage"] = "You already have active access to this course.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            if (course.Price <= 0)
            {
                _enrollmentService.EnrollStudent(currentUserId, courseId, out string _);
                TempData["SuccessMessage"] = "You have been enrolled in this free course! Start learning now.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            return View(course);
        }

        // POST: Courses/Payment
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult Payment(int courseId, string paymentMethod, string cardNumber = null, string expMonth = null, string expYear = null, string cvc = null)
        {
            string currentUserId = User.Identity.GetUserId();
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            bool isAdmin = User.IsInRole("Admin");
            if (course.InstructorId == currentUserId || isAdmin)
            {
                TempData["InfoMessage"] = isAdmin
                    ? "As an Administrator, you have full access to the classroom."
                    : "You are the instructor of this course. You have direct access to the classroom.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            if (_enrollmentService.HasActiveAccess(currentUserId, courseId))
            {
                TempData["InfoMessage"] = "You already have active access to this course.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            string stripeTransactionId = null;

            // If user selected Stripe Card payment or provided card details
            if (!string.IsNullOrWhiteSpace(cardNumber) || (paymentMethod != null && paymentMethod.IndexOf("Stripe", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                string cleanCard = (cardNumber ?? "").Replace(" ", "").Trim();
                if (string.IsNullOrWhiteSpace(cleanCard))
                {
                    cleanCard = "4242424242424242"; // Default Stripe standard test card
                }

                var chargeResult = _stripeService.ProcessDirectCardCharge(
                    currentUserId,
                    courseId,
                    course.Title,
                    course.Price,
                    cleanCard,
                    expMonth ?? "12",
                    expYear ?? "2028",
                    cvc ?? "123");

                if (!chargeResult.Success)
                {
                    TempData["ErrorMessage"] = "Stripe Authorization Error: " + (chargeResult.ErrorMessage ?? "Payment was declined by Stripe test gateway.");
                    return RedirectToAction("Payment", new { courseId });
                }

                stripeTransactionId = chargeResult.PaymentIntentId ?? ("ST-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper());
                paymentMethod = "Stripe Card (Test Gateway)";
            }

            bool success = _paymentService.ProcessCoursePayment(
                currentUserId,
                courseId,
                paymentMethod,
                out string transactionId,
                out string error,
                stripeTransactionId);

            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Payment", new { courseId });
            }

            TempData["SuccessMessage"] = $"Payment successful via {paymentMethod}! Transaction ID: {transactionId}. Welcome to the course!";
            return RedirectToAction("Index", "Learning", new { courseId });
        }

        // POST: Courses/CreateStripeCheckout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult CreateStripeCheckout(int courseId)
        {
            string currentUserId = User.Identity.GetUserId();
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            if (_enrollmentService.HasActiveAccess(currentUserId, courseId))
            {
                TempData["InfoMessage"] = "You already have active access to this course.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            string host = Request.Url.Scheme + "://" + Request.Url.Authority;
            string successUrl = $"{host}/Courses/StripeSuccess?session_id={{CHECKOUT_SESSION_ID}}&courseId={courseId}";
            string cancelUrl = $"{host}/Courses/StripeCancel?courseId={courseId}";

            var sessionResult = _stripeService.CreateCheckoutSession(
                currentUserId,
                courseId,
                course.Title,
                course.Price,
                successUrl,
                cancelUrl);

            if (!sessionResult.Success || string.IsNullOrEmpty(sessionResult.CheckoutUrl))
            {
                TempData["ErrorMessage"] = "Stripe Gateway Error: " + (sessionResult.ErrorMessage ?? "Could not initiate checkout session.");
                return RedirectToAction("Payment", new { courseId });
            }

            return Redirect(sessionResult.CheckoutUrl);
        }

        // GET: Courses/StripeSuccess
        [HttpGet]
        [Authorize]
        public ActionResult StripeSuccess(string session_id, int courseId)
        {
            string currentUserId = User.Identity.GetUserId();
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(session_id))
            {
                TempData["ErrorMessage"] = "Invalid Stripe session reference.";
                return RedirectToAction("Payment", new { courseId });
            }

            // Verify with Stripe API
            var verification = _stripeService.VerifyCheckoutSession(session_id);
            if (!verification.IsPaid)
            {
                TempData["ErrorMessage"] = "Stripe payment verification failed or payment is still pending. " + (verification.ErrorMessage ?? "");
                return RedirectToAction("Payment", new { courseId });
            }

            if (_enrollmentService.HasActiveAccess(currentUserId, courseId))
            {
                TempData["SuccessMessage"] = "Payment confirmed! Your enrollment is active.";
                return RedirectToAction("Index", "Learning", new { courseId });
            }

            string stripeTxn = !string.IsNullOrEmpty(verification.PaymentIntentId) ? verification.PaymentIntentId : session_id;
            bool success = _paymentService.ProcessCoursePayment(
                currentUserId,
                courseId,
                "Stripe Hosted Checkout",
                out string transactionId,
                out string error,
                stripeTxn);

            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Payment", new { courseId });
            }

            TempData["SuccessMessage"] = $"Payment successful via Stripe Gateway! Stripe Transaction ID: {stripeTxn}. Welcome to {course.Title}!";
            return RedirectToAction("Index", "Learning", new { courseId });
        }

        // GET: Courses/StripeCancel
        [HttpGet]
        [Authorize]
        public ActionResult StripeCancel(int courseId)
        {
            TempData["InfoMessage"] = "Stripe checkout was cancelled. You have not been charged.";
            return RedirectToAction("Payment", new { courseId });
        }

        // POST: Courses/AddReview
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult AddReview(CourseReviewInputModel model)
        {
            string studentId = User.Identity.GetUserId();
            var course = _db.Courses.Find(model.CourseId);
            if (course == null) return HttpNotFound();

            // Verify enrollment
            if (!_enrollmentService.IsEnrolled(studentId, model.CourseId))
            {
                TempData["ErrorMessage"] = "Only enrolled students can review this course.";
                return RedirectToAction("Details", new { id = model.CourseId });
            }

            var existing = _db.CourseReviews.FirstOrDefault(r => r.CourseId == model.CourseId && r.StudentId == studentId);
            if (existing != null)
            {
                existing.Rating = model.Rating;
                existing.ReviewText = model.ReviewText;
                existing.UpdatedDate = DateTime.UtcNow;
            }
            else
            {
                _db.CourseReviews.Add(new CourseReview
                {
                    CourseId = model.CourseId,
                    StudentId = studentId,
                    Rating = model.Rating,
                    ReviewText = model.ReviewText,
                    CreatedDate = DateTime.UtcNow
                });
            }
            _db.SaveChanges();

            // Recalculate average rating
            var reviews = _db.CourseReviews.Where(r => r.CourseId == model.CourseId).ToList();
            if (reviews.Any())
            {
                course.Rating = Math.Round((decimal)reviews.Average(r => r.Rating), 2);
                _db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Thank you! Your review has been submitted.";
            return RedirectToAction("Details", new { id = model.CourseId });
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
