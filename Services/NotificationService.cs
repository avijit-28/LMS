using System;
using System.Collections.Generic;
using System.Linq;
using LMS.Data;
using LMS.Models;

namespace LMS.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _db;

        public NotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<Notification> GetUserNotifications(string userId)
        {
            return _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(50)
                .ToList();
        }

        public int GetUnreadCount(string userId)
        {
            return _db.Notifications.Count(n => n.UserId == userId && !n.IsRead);
        }

        public void MarkAsRead(int notificationId, string userId)
        {
            var notif = _db.Notifications.FirstOrDefault(n => n.NotificationId == notificationId && n.UserId == userId);
            if (notif != null)
            {
                notif.IsRead = true;
                _db.SaveChanges();
            }
        }

        public void MarkAllAsRead(string userId)
        {
            var unread = _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            _db.SaveChanges();
        }
    }
}
