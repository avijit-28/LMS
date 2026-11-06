using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LMS.Models;

namespace LMS.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalInstructors { get; set; }
        public int TotalCourses { get; set; }
        public int PublishedCourses { get; set; }
        public int TotalEnrollments { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalCertificates { get; set; }
        public int TotalCategories { get; set; }
        public int PendingApprovalsCount { get; set; }
        public int PendingInstructorsCount { get; set; }

        public List<Enrollment> RecentEnrollments { get; set; } = new List<Enrollment>();
        public List<ApplicationUser> RecentUsers { get; set; } = new List<ApplicationUser>();
        public List<Payment> RecentPayments { get; set; } = new List<Payment>();
        public List<CategoryStat> CategoryStats { get; set; } = new List<CategoryStat>();
    }

    public class CategoryStat
    {
        public string CategoryName { get; set; }
        public int CourseCount { get; set; }
    }

    public class UserManagementViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsApproved { get; set; } = true;
        public string ApprovalStatus { get; set; } = "Approved";
        public string RequestedRole { get; set; }
    }

    public class PendingUserApprovalViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string RequestedRole { get; set; }
        public DateTime RegisteredDate { get; set; }
        public string ApprovalStatus { get; set; }
        public string RejectionReason { get; set; }
    }

    public class InstructorApplicationViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Bio { get; set; }
        public DateTime RegisteredDate { get; set; }
        public string ApprovalStatus { get; set; } // Pending, Approved, Declined
        public string RejectionReason { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class CategoryViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100)]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public int CourseCount { get; set; }
    }

    public class AnnouncementFormViewModel
    {
        public int AnnouncementId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
