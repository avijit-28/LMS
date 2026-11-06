using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using dotenv.net;

namespace LMS.Services
{
    /// <summary>
    /// Centralized Environment Configuration Service for EduPlatform LMS.
    /// Provides secure, decoupled access to external credentials and secrets.
    /// Priority order:
    /// 1. Machine/User/Process Environment Variables (Production IIS/Hosting environment)
    /// 2. Local untracked .env file (Local Development)
    /// 3. Web.config AppSettings / ConnectionStrings (Legacy Fallback)
    /// </summary>
    public static class EnvConfig
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();
        private static string _loadedEnvFilePath = null;

        /// <summary>
        /// Loads .env configuration into Process environment variables if not already initialized.
        /// Guaranteed safe to call multiple times (idempotent).
        /// Never overwrites existing production environment variables.
        /// </summary>
        public static void Initialize(string customEnvPath = null)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    var candidatePaths = new List<string>();

                    if (!string.IsNullOrWhiteSpace(customEnvPath))
                    {
                        candidatePaths.Add(customEnvPath);
                    }

                    // 1. Web application root via HostingEnvironment (IIS / IIS Express)
                    try
                    {
                        if (HostingEnvironment.IsHosted)
                        {
                            string mapped = HostingEnvironment.MapPath("~/.env");
                            if (!string.IsNullOrWhiteSpace(mapped)) candidatePaths.Add(mapped);
                        }
                    }
                    catch { }

                    // 2. BaseDirectory (AppDomain)
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrWhiteSpace(baseDir))
                    {
                        candidatePaths.Add(Path.Combine(baseDir, ".env"));

                        // 3. Parent directory of BaseDirectory (useful when running from bin/ or subfolder)
                        var parent = Directory.GetParent(baseDir);
                        if (parent != null)
                        {
                            candidatePaths.Add(Path.Combine(parent.FullName, ".env"));
                        }
                    }

                    // 4. Current working directory
                    candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

                    string targetEnvFile = candidatePaths.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

                    if (targetEnvFile != null)
                    {
                        _loadedEnvFilePath = targetEnvFile;

                        // Load via official dotenv.net without overwriting production environment variables
                        try
                        {
                            DotEnv.Load(new DotEnvOptions(
                                envFilePaths: new[] { targetEnvFile },
                                overwriteExistingVars: false,
                                ignoreExceptions: true
                            ));
                        }
                        catch
                        {
                            // Fallback to internal parser if library encounters encoding issues
                            ParseEnvFileFallback(targetEnvFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning("EnvConfig initialization warning: " + ex.Message);
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Native fallback parser to ensure .env is parsed even if external library fails.
        /// </summary>
        private static void ParseEnvFileFallback(string filePath)
        {
            if (!File.Exists(filePath)) return;

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                int eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = line.Substring(0, eqIdx).Trim();
                string val = line.Substring(eqIdx + 1).Trim();

                // Strip quotes if present
                if (val.Length >= 2 && ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'"))))
                {
                    val = val.Substring(1, val.Length - 2);
                }

                // Never overwrite existing system/process environment variable
                if (Environment.GetEnvironmentVariable(key) == null)
                {
                    Environment.SetEnvironmentVariable(key, val, EnvironmentVariableTarget.Process);
                }
            }
        }

        /// <summary>
        /// Reads an environment variable with fallback checks across Process, User, Machine, and Web.config.
        /// </summary>
        public static string Get(string key, string fallback = null)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (string.IsNullOrWhiteSpace(key)) return fallback;

            // 1. Check Process Environment (includes variables loaded from .env)
            string val = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

            // 2. Check User Environment
            val = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

            // 3. Check Machine Environment (IIS Application Pool environment variables)
            val = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

            // 4. Check Web.config AppSettings
            val = ConfigurationManager.AppSettings[key];
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

            return fallback;
        }

        #region Strongly-Typed Credential Accessors

        // --- Database ---
        public static string DatabaseConnectionString =>
            Get("DATABASE_CONNECTION_STRING") ??
            ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString;

        // --- Stripe Payment Gateway ---
        public static string StripeSecretKey =>
            Get("STRIPE_SECRET_KEY") ??
            Get("StripeSecretKey");

        public static string StripePublishableKey =>
            Get("STRIPE_PUBLISHABLE_KEY") ??
            Get("StripePublishableKey");

        public static string StripeWebhookSecret =>
            Get("STRIPE_WEBHOOK_SECRET") ??
            Get("StripeWebhookSecret");

        // --- Google Gemini AI ---
        public static string GeminiApiKey =>
            Get("GEMINI_API_KEY") ??
            Get("GeminiApiKey");

        // --- Groq AI ---
        public static string GroqApiKey =>
            Get("GROQ_API_KEY") ??
            Get("GroqApiKey");

        // --- Enterprise RAG AI Microservice ---
        public static string AiServiceBaseUrl =>
            Get("AISERVICE_BASE_URL") ??
            Get("AIService:BaseUrl", "http://127.0.0.1:8000");

        public static string AiServiceInternalApiKey =>
            Get("AISERVICE_INTERNAL_KEY") ??
            Get("AISERVICE_INTERNAL_API_KEY") ??
            Get("AIService:InternalApiKey");

        // --- Cloudinary Document & Media Storage ---
        public static string CloudinaryApiKey =>
            Get("CLOUDINARY_API_KEY") ??
            Get("Cloudinary:ApiKey");

        public static string CloudinaryCloudName =>
            Get("CLOUDINARY_CLOUD_NAME") ??
            Get("Cloudinary:CloudName");

        public static string CloudinaryApiSecret =>
            Get("CLOUDINARY_API_SECRET") ??
            Get("Cloudinary:ApiSecret");

        public static string CloudinaryUploadPreset =>
            Get("CLOUDINARY_UPLOAD_PRESET") ??
            Get("Cloudinary:UploadPreset");

        // --- SMTP / Email ---
        public static string SmtpHost => Get("SMTP_HOST");
        public static int SmtpPort => int.TryParse(Get("SMTP_PORT", "587"), out int port) ? port : 587;
        public static string SmtpUsername => Get("SMTP_USERNAME");
        public static string SmtpPassword => Get("SMTP_PASSWORD");
        public static bool SmtpEnableSsl => bool.TryParse(Get("SMTP_ENABLE_SSL", "true"), out bool ssl) ? ssl : true;

        #endregion

        #region Security & Validation Helpers

        /// <summary>
        /// Returns whether a local .env file was discovered and loaded.
        /// </summary>
        public static bool IsEnvFileLoaded => !string.IsNullOrEmpty(_loadedEnvFilePath);

        /// <summary>
        /// Returns the path of the loaded .env file (if any).
        /// </summary>
        public static string LoadedEnvFilePath => _loadedEnvFilePath;

        /// <summary>
        /// Validates required configuration keys and returns a list of missing key names.
        /// Never exposes any secret values.
        /// </summary>
        public static List<string> ValidateRequiredConfiguration(params string[] requiredKeys)
        {
            if (!_initialized) Initialize();

            var missing = new List<string>();
            foreach (var key in requiredKeys)
            {
                if (string.IsNullOrWhiteSpace(Get(key)))
                {
                    missing.Add(key);
                }
            }
            return missing;
        }

        /// <summary>
        /// Masks sensitive credentials for safe logging or diagnostic views.
        /// e.g. "sk_test_51UWz..." -> "sk_t...Ac4p"
        /// Never exposes the complete secret.
        /// </summary>
        public static string MaskSecret(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret)) return "[NOT CONFIGURED]";
            if (secret.Length <= 8) return "********";
            return $"{secret.Substring(0, 4)}...{secret.Substring(secret.Length - 4)}";
        }

        #endregion
    }
}
