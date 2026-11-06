using System;
using System.Collections.Generic;
using LMS.Models;

namespace LMS.ViewModels
{
    public class CourseFilterViewModel
    {
        public string Search { get; set; }
        public int? CategoryId { get; set; }
        public string Level { get; set; } // All, Beginner, Intermediate, Advanced
        public string PriceType { get; set; } // All, Free, Paid
        public string SortBy { get; set; } // newest, rating, popular, price_asc, price_desc
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalItems { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 9));

        public List<Course> Courses { get; set; } = new List<Course>();
        public List<Category> Categories { get; set; } = new List<Category>();
    }
}
