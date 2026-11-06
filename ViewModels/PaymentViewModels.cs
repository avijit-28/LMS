using System;
using System.Collections.Generic;

namespace LMS.ViewModels
{
    public class CoursePaymentRecordViewModel
    {
        public int PaymentId { get; set; }
        public string TransactionId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public string InstructorId { get; set; }
        public string InstructorName { get; set; }
        public string InstructorEmail { get; set; }
        public string BuyerId { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
        public string BuyerRole { get; set; } // "Student", "Instructor", "Admin"
        public decimal Amount { get; set; }
        public decimal AdminCommission { get; set; } // 5% platform charge
        public decimal InstructorEarnings { get; set; } // 95% teacher earnings
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; } // "Completed", "Pending", "Failed"
        public DateTime PaymentDate { get; set; }
    }

    public class InstructorPaymentsViewModel
    {
        public decimal TotalGrossSales { get; set; }
        public decimal TotalEarnings { get; set; } // 95% net earnings
        public decimal TotalCommissionDeducted { get; set; } // 5% platform fee
        public int TotalPaidEnrollments { get; set; }
        public decimal AverageOrderValue { get; set; }
        public string CurrentFilter { get; set; } = "All"; // "All", "Student", "Instructor"
        public int? SelectedCourseId { get; set; }
        public string SearchQuery { get; set; }
        public List<System.Web.Mvc.SelectListItem> CourseList { get; set; } = new List<System.Web.Mvc.SelectListItem>();
        public int TotalTransactionsCount { get; set; }
        public int TotalStudentPurchasesCount { get; set; }
        public int TotalInstructorPurchasesCount { get; set; }
        public List<CoursePaymentRecordViewModel> Transactions { get; set; } = new List<CoursePaymentRecordViewModel>();
    }

    public class AdminPaymentsViewModel
    {
        public decimal TotalRevenue { get; set; } // Gross Platform Sales
        public decimal TotalAdminCommission { get; set; } // 5% Admin Platform Fee
        public decimal TotalInstructorPayouts { get; set; } // 95% Instructor Net Payouts
        public int TotalTransactionsCount { get; set; }
        public decimal StudentPurchasesTotal { get; set; }
        public int StudentPurchasesCount { get; set; }
        public decimal InstructorPurchasesTotal { get; set; }
        public int InstructorPurchasesCount { get; set; }
        public string CurrentFilter { get; set; } = "All"; // "All", "Student", "Instructor"
        public int? SelectedCourseId { get; set; }
        public string SearchQuery { get; set; }
        public List<System.Web.Mvc.SelectListItem> CourseList { get; set; } = new List<System.Web.Mvc.SelectListItem>();
        public List<CoursePaymentRecordViewModel> Transactions { get; set; } = new List<CoursePaymentRecordViewModel>();
    }
}
