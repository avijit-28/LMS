using System;

namespace LMS.Helpers
{
    public static class UserHelper
    {
        public static string GetFirstName(string fullNameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(fullNameOrEmail)) return "Learner";

            fullNameOrEmail = fullNameOrEmail.Trim();

            // If it's an email address like suman@gmail.com
            if (fullNameOrEmail.Contains("@"))
            {
                var username = fullNameOrEmail.Split('@')[0];
                var first = username.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (first.Length > 0 && !string.IsNullOrEmpty(first[0]))
                {
                    var clean = first[0];
                    return char.ToUpper(clean[0]) + (clean.Length > 1 ? clean.Substring(1).ToLower() : "");
                }
            }

            // If it's a full name like "Dr. Sarah Connor" or "Suman Sharma"
            var parts = fullNameOrEmail.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var first = parts[0];
                if ((first.Equals("Dr.", StringComparison.OrdinalIgnoreCase) ||
                     first.Equals("Mr.", StringComparison.OrdinalIgnoreCase) ||
                     first.Equals("Ms.", StringComparison.OrdinalIgnoreCase) ||
                     first.Equals("Mrs.", StringComparison.OrdinalIgnoreCase)) && parts.Length > 1)
                {
                    first = parts[1];
                }
                return char.ToUpper(first[0]) + (first.Length > 1 ? first.Substring(1) : "");
            }

            return fullNameOrEmail;
        }
    }
}
