using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LMS.Services
{
    public class StripeCheckoutSessionResult
    {
        public bool Success { get; set; }
        public string SessionId { get; set; }
        public string CheckoutUrl { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class StripeSessionVerificationResult
    {
        public bool IsPaid { get; set; }
        public string SessionId { get; set; }
        public string PaymentIntentId { get; set; }
        public decimal AmountInr { get; set; }
        public string CustomerEmail { get; set; }
        public int CourseId { get; set; }
        public string StudentId { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class StripeDirectChargeResult
    {
        public bool Success { get; set; }
        public string PaymentIntentId { get; set; }
        public string ChargeId { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class StripeService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static string SecretKey => EnvConfig.StripeSecretKey;

        public static string PublishableKey => EnvConfig.StripePublishableKey;

        public static string WebhookSecret => EnvConfig.StripeWebhookSecret;

        /// <summary>
        /// Creates an official Stripe Hosted Checkout Session
        /// </summary>
        public async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
            string studentId,
            int courseId,
            string courseTitle,
            decimal amountInr,
            string successUrl,
            string cancelUrl)
        {
            var result = new StripeCheckoutSessionResult();

            try
            {
                long unitAmountPaise = (long)Math.Round(amountInr * 100m);
                if (unitAmountPaise < 50) unitAmountPaise = 5000; // Minimum Stripe amount safeguard

                var postData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("mode", "payment"),
                    new KeyValuePair<string, string>("success_url", successUrl),
                    new KeyValuePair<string, string>("cancel_url", cancelUrl),
                    new KeyValuePair<string, string>("line_items[0][price_data][currency]", "inr"),
                    new KeyValuePair<string, string>("line_items[0][price_data][unit_amount]", unitAmountPaise.ToString()),
                    new KeyValuePair<string, string>("line_items[0][price_data][product_data][name]", courseTitle ?? "Course Enrollment"),
                    new KeyValuePair<string, string>("line_items[0][quantity]", "1"),
                    new KeyValuePair<string, string>("client_reference_id", $"{studentId}_{courseId}"),
                    new KeyValuePair<string, string>("metadata[studentId]", studentId ?? string.Empty),
                    new KeyValuePair<string, string>("metadata[courseId]", courseId.ToString())
                };

                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
                    request.Content = new FormUrlEncodedContent(postData);

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var obj = JObject.Parse(json);

                        if (response.IsSuccessStatusCode)
                        {
                            result.Success = true;
                            result.SessionId = obj["id"]?.ToString();
                            result.CheckoutUrl = obj["url"]?.ToString();
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = obj["error"]?["message"]?.ToString() ?? "Stripe checkout session failed to initialize.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Synchronous wrapper for ASP.NET MVC controllers
        /// </summary>
        public StripeCheckoutSessionResult CreateCheckoutSession(
            string studentId,
            int courseId,
            string courseTitle,
            decimal amountInr,
            string successUrl,
            string cancelUrl)
        {
            return Task.Run(() => CreateCheckoutSessionAsync(studentId, courseId, courseTitle, amountInr, successUrl, cancelUrl)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Verifies a completed Stripe Checkout Session
        /// </summary>
        public async Task<StripeSessionVerificationResult> VerifyCheckoutSessionAsync(string sessionId)
        {
            var result = new StripeSessionVerificationResult { SessionId = sessionId };

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/checkout/sessions/{sessionId}"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var obj = JObject.Parse(json);

                        if (response.IsSuccessStatusCode)
                        {
                            string paymentStatus = obj["payment_status"]?.ToString();
                            result.IsPaid = string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
                            result.PaymentIntentId = obj["payment_intent"]?.ToString() ?? obj["id"]?.ToString();

                            if (long.TryParse(obj["amount_total"]?.ToString(), out long totalPaise))
                            {
                                result.AmountInr = totalPaise / 100m;
                            }

                            result.CustomerEmail = obj["customer_details"]?["email"]?.ToString();

                            if (int.TryParse(obj["metadata"]?["courseId"]?.ToString(), out int cId))
                            {
                                result.CourseId = cId;
                            }
                            result.StudentId = obj["metadata"]?["studentId"]?.ToString();
                        }
                        else
                        {
                            result.IsPaid = false;
                            result.ErrorMessage = obj["error"]?["message"]?.ToString() ?? "Unable to verify Stripe checkout session.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsPaid = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public StripeSessionVerificationResult VerifyCheckoutSession(string sessionId)
        {
            return Task.Run(() => VerifyCheckoutSessionAsync(sessionId)).GetAwaiter().GetResult();
        }

        public static string ResolveStripeTestToken(string cardOrToken)
        {
            if (string.IsNullOrWhiteSpace(cardOrToken)) return "tok_visa";
            string clean = cardOrToken.Replace(" ", "").Trim();
            if (clean.StartsWith("tok_", StringComparison.OrdinalIgnoreCase)) return clean;

            if (clean.EndsWith("4242")) return "tok_visa";
            if (clean.EndsWith("4444") || clean.StartsWith("5555")) return "tok_mastercard";
            if (clean.EndsWith("0002") || clean.IndexOf("decline", StringComparison.OrdinalIgnoreCase) >= 0) return "tok_chargeDeclined";
            if (clean.EndsWith("9995") || clean.IndexOf("funds", StringComparison.OrdinalIgnoreCase) >= 0) return "tok_chargeDeclinedInsufficientFunds";
            if (clean.EndsWith("0069") || clean.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0) return "tok_chargeDeclinedExpiredCard";
            if (clean.EndsWith("0127") || clean.IndexOf("cvc", StringComparison.OrdinalIgnoreCase) >= 0) return "tok_chargeDeclinedIncorrectCvc";

            return "tok_visa";
        }

        /// <summary>
        /// Processes a direct test card charge via Stripe PaymentIntents API
        /// </summary>
        public async Task<StripeDirectChargeResult> ProcessDirectCardChargeAsync(
            string studentId,
            int courseId,
            string courseTitle,
            decimal amountInr,
            string cardNumber,
            string expMonth,
            string expYear,
            string cvc)
        {
            var result = new StripeDirectChargeResult();

            try
            {
                long unitAmountPaise = (long)Math.Round(amountInr * 100m);
                if (unitAmountPaise < 50) unitAmountPaise = 5000;

                string token = ResolveStripeTestToken(cardNumber);

                var piData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("amount", unitAmountPaise.ToString()),
                    new KeyValuePair<string, string>("currency", "inr"),
                    new KeyValuePair<string, string>("payment_method_data[type]", "card"),
                    new KeyValuePair<string, string>("payment_method_data[card][token]", token),
                    new KeyValuePair<string, string>("confirm", "true"),
                    new KeyValuePair<string, string>("automatic_payment_methods[enabled]", "true"),
                    new KeyValuePair<string, string>("automatic_payment_methods[allow_redirects]", "never"),
                    new KeyValuePair<string, string>("description", $"LMS Course Enrollment: {courseTitle}"),
                    new KeyValuePair<string, string>("metadata[studentId]", studentId ?? string.Empty),
                    new KeyValuePair<string, string>("metadata[courseId]", courseId.ToString()),
                    new KeyValuePair<string, string>("metadata[testToken]", token)
                };

                using (var piRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_intents"))
                {
                    piRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
                    piRequest.Content = new FormUrlEncodedContent(piData);

                    using (var piResponse = await _httpClient.SendAsync(piRequest))
                    {
                        var piJson = await piResponse.Content.ReadAsStringAsync();
                        var piObj = JObject.Parse(piJson);

                        if (piResponse.IsSuccessStatusCode)
                        {
                            string status = piObj["status"]?.ToString();
                            result.PaymentIntentId = piObj["id"]?.ToString();
                            result.Status = status;

                            if (status == "succeeded")
                            {
                                result.Success = true;
                            }
                            else
                            {
                                result.Success = false;
                                result.ErrorMessage = $"Stripe payment status: {status}. Please try again.";
                            }
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = piObj["error"]?["message"]?.ToString() ?? "Payment authorization failed.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public StripeDirectChargeResult ProcessDirectCardCharge(
            string studentId,
            int courseId,
            string courseTitle,
            decimal amountInr,
            string cardNumber,
            string expMonth,
            string expYear,
            string cvc)
        {
            return Task.Run(() => ProcessDirectCardChargeAsync(studentId, courseId, courseTitle, amountInr, cardNumber, expMonth, expYear, cvc)).GetAwaiter().GetResult();
        }
    }
}
