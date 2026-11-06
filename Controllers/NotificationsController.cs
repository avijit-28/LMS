using System.Web.Mvc;
using LMS.Data;
using LMS.Services;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly NotificationService _notificationService;

        public NotificationsController()
        {
            _notificationService = new NotificationService(_db);
        }

        // GET: Notifications
        public ActionResult Index()
        {
            string userId = User.Identity.GetUserId();
            var notifications = _notificationService.GetUserNotifications(userId);
            return View(notifications);
        }

        // POST: Notifications/MarkAsRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsRead(int id)
        {
            string userId = User.Identity.GetUserId();
            _notificationService.MarkAsRead(id, userId);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }
            return RedirectToAction("Index");
        }

        // POST: Notifications/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllAsRead()
        {
            string userId = User.Identity.GetUserId();
            _notificationService.MarkAllAsRead(userId);

            TempData["SuccessMessage"] = "All notifications marked as read.";
            return RedirectToAction("Index");
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
