-- ==========================================================================================
-- EduPlatform LMS - COMPLETE UNIFIED DATABASE SETUP SCRIPT
-- ==========================================================================================
-- Database System : Microsoft SQL Server 2014 / 2016 / 2019 / 2022 / Express / Azure SQL
-- Target Database : [LMS]
-- Application     : ASP.NET MVC 5 (.NET Framework 4.8) + Python FastAPI AI Learning Assistant
-- Description     : Creates the LMS database, all 35 schema tables, relationships, indexes,
--                   and seeds default administrative, instructor, student accounts & courses.
--
-- EXECUTION INSTRUCTIONS:
-- 1. Open SQL Server Management Studio (SSMS) or Azure Data Studio.
-- 2. Connect to your SQL Server instance (e.g. '.', 'localhost', or '.\SQLEXPRESS').
-- 3. Open this file and execute the script (F5 or click Execute).
-- 4. Verify your Web.config connection string points to this database:
--    <add name="DefaultConnection" 
--         connectionString="Data Source=.;Initial Catalog=LMS;Integrated Security=True;MultipleActiveResultSets=True;" 
--         providerName="System.Data.SqlClient" />
-- ==========================================================================================

-- 1. CREATE DATABASE
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LMS')
BEGIN
    PRINT 'Creating database [LMS]...';
    CREATE DATABASE [LMS];
END
GO

USE [LMS];
GO

PRINT 'Deploying tables for [LMS]...';
GO

-- ==========================================================================================
-- 2. ASP.NET IDENTITY 2.0 SECURITY & USER MEMBERSHIP TABLES
-- ==========================================================================================

-- AspNetRoles
IF OBJECT_ID('dbo.AspNetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles (
        [Id] NVARCHAR(128) NOT NULL,
        [Name] NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles ([Name]);
END
GO

-- AspNetUsers (with full custom LMS properties)
IF OBJECT_ID('dbo.AspNetUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers (
        [Id] NVARCHAR(128) NOT NULL,
        [Email] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0,
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
        [LockoutEndDateUtc] DATETIME NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 1,
        [AccessFailedCount] INT NOT NULL DEFAULT 0,
        [UserName] NVARCHAR(256) NOT NULL,
        [FullName] NVARCHAR(150) NULL,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ProfilePicture] NVARCHAR(500) NULL,
        [Bio] NVARCHAR(MAX) NULL,
        [IsApproved] BIT NOT NULL DEFAULT 1,
        [ApprovalStatus] NVARCHAR(50) NOT NULL DEFAULT 'Approved',
        [RequestedRole] NVARCHAR(50) NULL,
        [RejectionReason] NVARCHAR(500) NULL,
        [RegisteredDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [SignaturePath] NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUsers PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers ([UserName]);
END
GO

-- AspNetUserRoles
IF OBJECT_ID('dbo.AspNetUserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles (
        [UserId] NVARCHAR(128) NOT NULL,
        [RoleId] NVARCHAR(128) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers FOREIGN KEY ([UserId]) REFERENCES dbo.AspNetUsers ([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles FOREIGN KEY ([RoleId]) REFERENCES dbo.AspNetRoles ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserRoles_UserId ON dbo.AspNetUserRoles ([UserId]);
    CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles ([RoleId]);
END
GO

-- AspNetUserClaims
IF OBJECT_ID('dbo.AspNetUserClaims', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY ([Id]),
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers FOREIGN KEY ([UserId]) REFERENCES dbo.AspNetUsers ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims ([UserId]);
END
GO

-- AspNetUserLogins
IF OBJECT_ID('dbo.AspNetUserLogins', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins (
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [ProviderKey] NVARCHAR(128) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY ([LoginProvider], [ProviderKey], [UserId]),
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers FOREIGN KEY ([UserId]) REFERENCES dbo.AspNetUsers ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins ([UserId]);
END
GO

-- PasswordResetRequests (Administrator review portal)
IF OBJECT_ID('dbo.PasswordResetRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetRequests (
        [RequestId] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [Role] NVARCHAR(50) NULL,
        [RequestReason] NVARCHAR(500) NULL,
        [RequestDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        [ResetToken] NVARCHAR(100) NULL,
        [TokenExpiry] DATETIME NULL,
        [ApprovedByAdminId] NVARCHAR(128) NULL,
        [ApprovedDate] DATETIME NULL,
        [AdminNotes] NVARCHAR(500) NULL,
        [IsApproved] BIT NOT NULL DEFAULT 0,
        CONSTRAINT PK_PasswordResetRequests PRIMARY KEY ([RequestId]),
        CONSTRAINT FK_PasswordResetRequests_AspNetUsers FOREIGN KEY ([UserId]) REFERENCES dbo.AspNetUsers ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX IX_PasswordResetRequests_UserId ON dbo.PasswordResetRequests ([UserId]);
    CREATE INDEX IX_PasswordResetRequests_Email ON dbo.PasswordResetRequests ([Email]);
END
GO

-- ==========================================================================================
-- 3. CORE LMS ACADEMIC ENTITIES (CATEGORIES, COURSES, MODULES, LESSONS)
-- ==========================================================================================

-- Categories
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        [CategoryId] INT IDENTITY(1,1) NOT NULL,
        [CategoryName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Categories PRIMARY KEY ([CategoryId])
    );
END
GO

-- Courses
IF OBJECT_ID('dbo.Courses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Courses (
        [CourseId] INT IDENTITY(1,1) NOT NULL,
        [CategoryId] INT NOT NULL,
        [InstructorId] NVARCHAR(450) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Thumbnail] NVARCHAR(500) NULL,
        [Price] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [Level] NVARCHAR(50) NOT NULL DEFAULT 'Beginner',
        [DurationInHours] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Draft',
        [Rating] DECIMAL(3,2) NOT NULL DEFAULT 0.00,
        [TotalStudents] INT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedDate] DATETIME2 NULL,
        [PublishedDate] DATETIME2 NULL,
        [ValidityInDays] INT NOT NULL DEFAULT 30,
        CONSTRAINT PK_Courses PRIMARY KEY ([CourseId]),
        CONSTRAINT FK_Courses_Categories FOREIGN KEY ([CategoryId]) REFERENCES dbo.Categories ([CategoryId])
    );
    CREATE INDEX IX_Courses_CategoryId ON dbo.Courses ([CategoryId]);
    CREATE INDEX IX_Courses_InstructorId ON dbo.Courses ([InstructorId]);
    CREATE INDEX IX_Courses_Status ON dbo.Courses ([Status]);
END
GO

-- Modules
IF OBJECT_ID('dbo.Modules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Modules (
        [ModuleId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NOT NULL,
        [ModuleName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [ModuleOrder] INT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Modules PRIMARY KEY ([ModuleId]),
        CONSTRAINT FK_Modules_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Modules_CourseId ON dbo.Modules ([CourseId]);
END
GO

-- Lessons (Supports Video Streams, PDF Guides via Cloudinary, Articles, Quizzes, Assignments)
IF OBJECT_ID('dbo.Lessons', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lessons (
        [LessonId] INT IDENTITY(1,1) NOT NULL,
        [ModuleId] INT NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [LessonType] NVARCHAR(30) NOT NULL DEFAULT 'Video',
        [VideoUrl] NVARCHAR(500) NULL,
        [Content] NVARCHAR(MAX) NULL,
        [ResourceUrl] NVARCHAR(500) NULL,
        [DurationInMinutes] INT NOT NULL DEFAULT 10,
        [LessonOrder] INT NOT NULL DEFAULT 1,
        [IsPreview] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedDate] DATETIME2 NULL,
        CONSTRAINT PK_Lessons PRIMARY KEY ([LessonId]),
        CONSTRAINT FK_Lessons_Modules FOREIGN KEY ([ModuleId]) REFERENCES dbo.Modules ([ModuleId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Lessons_ModuleId ON dbo.Lessons ([ModuleId]);
END
GO

-- Enrollments
IF OBJECT_ID('dbo.Enrollments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Enrollments (
        [EnrollmentId] INT IDENTITY(1,1) NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [CourseId] INT NOT NULL,
        [EnrollmentDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Active',
        [CompletionPercentage] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
        [CompletionDate] DATETIME2 NULL,
        [ExpiryDate] DATETIME2 NULL,
        CONSTRAINT PK_Enrollments PRIMARY KEY ([EnrollmentId]),
        CONSTRAINT FK_Enrollments_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Enrollments_StudentId ON dbo.Enrollments ([StudentId]);
    CREATE INDEX IX_Enrollments_CourseId ON dbo.Enrollments ([CourseId]);
END
GO

-- LessonProgress
IF OBJECT_ID('dbo.LessonProgress', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LessonProgress (
        [ProgressId] INT IDENTITY(1,1) NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [LessonId] INT NOT NULL,
        [IsCompleted] BIT NOT NULL DEFAULT 0,
        [WatchTimeInSeconds] INT NOT NULL DEFAULT 0,
        [CompletedDate] DATETIME2 NULL,
        [LastAccessedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LessonProgress PRIMARY KEY ([ProgressId]),
        CONSTRAINT FK_LessonProgress_Lessons FOREIGN KEY ([LessonId]) REFERENCES dbo.Lessons ([LessonId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_LessonProgress_StudentId ON dbo.LessonProgress ([StudentId]);
    CREATE INDEX IX_LessonProgress_LessonId ON dbo.LessonProgress ([LessonId]);
END
GO

-- ==========================================================================================
-- 4. ASSESSMENT & EVALUATION (QUIZZES, QUESTIONS, OPTIONS, ATTEMPTS, ASSIGNMENTS)
-- ==========================================================================================

-- Quizzes
IF OBJECT_ID('dbo.Quizzes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Quizzes (
        [QuizId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NOT NULL,
        [LessonId] INT NULL,
        [ModuleId] INT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [PassingPercentage] DECIMAL(5,2) NOT NULL DEFAULT 40.00,
        [TimeLimitMinutes] INT NULL DEFAULT 30,
        [TotalMarks] INT NOT NULL DEFAULT 100,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [IsFinalExam] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Quizzes PRIMARY KEY ([QuizId]),
        CONSTRAINT FK_Quizzes_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE,
        CONSTRAINT FK_Quizzes_Lessons FOREIGN KEY ([LessonId]) REFERENCES dbo.Lessons ([LessonId]),
        CONSTRAINT FK_Quizzes_Modules FOREIGN KEY ([ModuleId]) REFERENCES dbo.Modules ([ModuleId])
    );
    CREATE INDEX IX_Quizzes_CourseId ON dbo.Quizzes ([CourseId]);
END
GO

-- Questions
IF OBJECT_ID('dbo.Questions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Questions (
        [QuestionId] INT IDENTITY(1,1) NOT NULL,
        [QuizId] INT NOT NULL,
        [QuestionText] NVARCHAR(MAX) NOT NULL,
        [Marks] INT NOT NULL DEFAULT 1,
        [QuestionOrder] INT NOT NULL DEFAULT 1,
        CONSTRAINT PK_Questions PRIMARY KEY ([QuestionId]),
        CONSTRAINT FK_Questions_Quizzes FOREIGN KEY ([QuizId]) REFERENCES dbo.Quizzes ([QuizId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Questions_QuizId ON dbo.Questions ([QuizId]);
END
GO

-- QuestionOptions
IF OBJECT_ID('dbo.QuestionOptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionOptions (
        [OptionId] INT IDENTITY(1,1) NOT NULL,
        [QuestionId] INT NOT NULL,
        [OptionText] NVARCHAR(1000) NOT NULL,
        [IsCorrect] BIT NOT NULL DEFAULT 0,
        [OptionOrder] INT NOT NULL DEFAULT 1,
        CONSTRAINT PK_QuestionOptions PRIMARY KEY ([OptionId]),
        CONSTRAINT FK_QuestionOptions_Questions FOREIGN KEY ([QuestionId]) REFERENCES dbo.Questions ([QuestionId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_QuestionOptions_QuestionId ON dbo.QuestionOptions ([QuestionId]);
END
GO

-- QuizAttempts
IF OBJECT_ID('dbo.QuizAttempts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuizAttempts (
        [AttemptId] INT IDENTITY(1,1) NOT NULL,
        [QuizId] INT NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [Score] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
        [TotalMarks] INT NOT NULL DEFAULT 0,
        [ObtainedMarks] INT NOT NULL DEFAULT 0,
        [IsPassed] BIT NOT NULL DEFAULT 0,
        [StartedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAt] DATETIME2 NULL,
        CONSTRAINT PK_QuizAttempts PRIMARY KEY ([AttemptId]),
        CONSTRAINT FK_QuizAttempts_Quizzes FOREIGN KEY ([QuizId]) REFERENCES dbo.Quizzes ([QuizId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_QuizAttempts_QuizId ON dbo.QuizAttempts ([QuizId]);
    CREATE INDEX IX_QuizAttempts_StudentId ON dbo.QuizAttempts ([StudentId]);
END
GO

-- QuizAnswers
IF OBJECT_ID('dbo.QuizAnswers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuizAnswers (
        [AnswerId] INT IDENTITY(1,1) NOT NULL,
        [AttemptId] INT NOT NULL,
        [QuestionId] INT NOT NULL,
        [SelectedOptionId] INT NULL,
        [IsCorrect] BIT NOT NULL DEFAULT 0,
        [MarksObtained] INT NOT NULL DEFAULT 0,
        CONSTRAINT PK_QuizAnswers PRIMARY KEY ([AnswerId]),
        CONSTRAINT FK_QuizAnswers_QuizAttempts FOREIGN KEY ([AttemptId]) REFERENCES dbo.QuizAttempts ([AttemptId]) ON DELETE CASCADE,
        CONSTRAINT FK_QuizAnswers_Questions FOREIGN KEY ([QuestionId]) REFERENCES dbo.Questions ([QuestionId]),
        CONSTRAINT FK_QuizAnswers_QuestionOptions FOREIGN KEY ([SelectedOptionId]) REFERENCES dbo.QuestionOptions ([OptionId])
    );
    CREATE INDEX IX_QuizAnswers_AttemptId ON dbo.QuizAnswers ([AttemptId]);
END
GO

-- Assignments
IF OBJECT_ID('dbo.Assignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Assignments (
        [AssignmentId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NOT NULL,
        [ModuleId] INT NULL,
        [LessonId] INT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [DueDate] DATETIME2 NULL,
        [MaxMarks] INT NOT NULL DEFAULT 100,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Assignments PRIMARY KEY ([AssignmentId]),
        CONSTRAINT FK_Assignments_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE,
        CONSTRAINT FK_Assignments_Modules FOREIGN KEY ([ModuleId]) REFERENCES dbo.Modules ([ModuleId]),
        CONSTRAINT FK_Assignments_Lessons FOREIGN KEY ([LessonId]) REFERENCES dbo.Lessons ([LessonId])
    );
    CREATE INDEX IX_Assignments_CourseId ON dbo.Assignments ([CourseId]);
END
GO

-- Submissions
IF OBJECT_ID('dbo.Submissions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Submissions (
        [SubmissionId] INT IDENTITY(1,1) NOT NULL,
        [AssignmentId] INT NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [SubmissionText] NVARCHAR(MAX) NULL,
        [FileUrl] NVARCHAR(500) NULL,
        [SubmittedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [Marks] DECIMAL(6,2) NULL,
        [Feedback] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Submitted',
        [GradedDate] DATETIME2 NULL,
        CONSTRAINT PK_Submissions PRIMARY KEY ([SubmissionId]),
        CONSTRAINT FK_Submissions_Assignments FOREIGN KEY ([AssignmentId]) REFERENCES dbo.Assignments ([AssignmentId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Submissions_AssignmentId ON dbo.Submissions ([AssignmentId]);
END
GO

-- ==========================================================================================
-- 5. CERTIFICATION, REVIEWS, PAYMENTS & COMMUNICATIONS
-- ==========================================================================================

-- Certificates
IF OBJECT_ID('dbo.Certificates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Certificates (
        [CertificateId] INT IDENTITY(1,1) NOT NULL,
        [CertificateNumber] NVARCHAR(100) NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [CourseId] INT NOT NULL,
        [IssueDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [FinalScore] DECIMAL(5,2) NULL,
        [CertificateUrl] NVARCHAR(500) NULL,
        CONSTRAINT PK_Certificates PRIMARY KEY ([CertificateId]),
        CONSTRAINT FK_Certificates_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_Certificates_CourseId ON dbo.Certificates ([CourseId]);
    CREATE INDEX IX_Certificates_StudentId ON dbo.Certificates ([StudentId]);
END
GO

-- CourseReviews
IF OBJECT_ID('dbo.CourseReviews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseReviews (
        [ReviewId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [Rating] INT NOT NULL DEFAULT 5,
        [ReviewText] NVARCHAR(2000) NULL,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedDate] DATETIME2 NULL,
        CONSTRAINT PK_CourseReviews PRIMARY KEY ([ReviewId]),
        CONSTRAINT FK_CourseReviews_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_CourseReviews_CourseId ON dbo.CourseReviews ([CourseId]);
END
GO

-- Payments
IF OBJECT_ID('dbo.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        [PaymentId] INT IDENTITY(1,1) NOT NULL,
        [StudentId] NVARCHAR(450) NOT NULL,
        [CourseId] INT NOT NULL,
        [EnrollmentId] INT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [PaymentMethod] NVARCHAR(50) NOT NULL DEFAULT 'Online',
        [TransactionId] NVARCHAR(200) NULL,
        [PaymentStatus] NVARCHAR(30) NOT NULL DEFAULT 'Completed',
        [PaymentDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [AdminCommission] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [InstructorEarnings] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        CONSTRAINT PK_Payments PRIMARY KEY ([PaymentId]),
        CONSTRAINT FK_Payments_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE,
        CONSTRAINT FK_Payments_Enrollments FOREIGN KEY ([EnrollmentId]) REFERENCES dbo.Enrollments ([EnrollmentId])
    );
    CREATE INDEX IX_Payments_CourseId ON dbo.Payments ([CourseId]);
    CREATE INDEX IX_Payments_StudentId ON dbo.Payments ([StudentId]);
END
GO

-- Announcements
IF OBJECT_ID('dbo.Announcements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Announcements (
        [AnnouncementId] INT IDENTITY(1,1) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [CreatedBy] NVARCHAR(450) NOT NULL,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_Announcements PRIMARY KEY ([AnnouncementId])
    );
END
GO

-- Notifications
IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications (
        [NotificationId] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(1000) NOT NULL,
        [NotificationType] NVARCHAR(50) NULL,
        [IsRead] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Notifications PRIMARY KEY ([NotificationId])
    );
    CREATE INDEX IX_Notifications_UserId ON dbo.Notifications ([UserId]);
END
GO

-- Legacy Chat Sessions & Messages (UI fallbacks)
IF OBJECT_ID('dbo.ChatSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatSessions (
        [SessionId] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(128) NULL,
        [SessionGuid] NVARCHAR(50) NOT NULL,
        [Title] NVARCHAR(150) NOT NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [LastActiveAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_ChatSessions PRIMARY KEY ([SessionId])
    );
END
GO

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatMessages (
        [MessageId] INT IDENTITY(1,1) NOT NULL,
        [SessionId] INT NOT NULL,
        [Sender] NVARCHAR(20) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [RetrievedChunkIds] NVARCHAR(500) NULL,
        [ActionPayloadJson] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_ChatMessages PRIMARY KEY ([MessageId]),
        CONSTRAINT FK_ChatMessages_ChatSessions FOREIGN KEY ([SessionId]) REFERENCES dbo.ChatSessions ([SessionId]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.KnowledgeChunks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeChunks (
        [ChunkId] INT IDENTITY(1,1) NOT NULL,
        [SourceType] NVARCHAR(50) NOT NULL,
        [SourceId] INT NOT NULL,
        [Title] NVARCHAR(250) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [Keywords] NVARCHAR(500) NULL,
        [UrlPath] NVARCHAR(250) NULL,
        [LastUpdated] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_KnowledgeChunks PRIMARY KEY ([ChunkId])
    );
END
GO

-- ==========================================================================================
-- 6. ENTERPRISE AI / RAG LEARNING ASSISTANT TABLES (FASTAPI + QDRANT BACKEND)
-- ==========================================================================================

-- AI_Documents
IF OBJECT_ID('dbo.AI_Documents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_Documents (
        [DocumentId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NOT NULL,
        [ModuleId] INT NULL,
        [LessonId] INT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [FileName] NVARCHAR(255) NOT NULL,
        [FilePath] NVARCHAR(500) NOT NULL,
        [FileType] NVARCHAR(50) NOT NULL, -- PDF, DOCX, PPTX, TXT
        [FileSize] INT NOT NULL,
        [TotalPages] INT NOT NULL DEFAULT 1,
        [TotalChunks] INT NOT NULL DEFAULT 0,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Uploaded', -- Uploaded, Processing, Indexed, Failed
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [Visibility] NVARCHAR(50) NOT NULL DEFAULT 'student',
        [UploadedBy] NVARCHAR(128) NOT NULL,
        [UploadedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [IndexedAt] DATETIME NULL,
        CONSTRAINT PK_AI_Documents PRIMARY KEY ([DocumentId]),
        CONSTRAINT FK_AI_Documents_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId]) ON DELETE CASCADE,
        CONSTRAINT FK_AI_Documents_Modules FOREIGN KEY ([ModuleId]) REFERENCES dbo.Modules ([ModuleId]),
        CONSTRAINT FK_AI_Documents_Lessons FOREIGN KEY ([LessonId]) REFERENCES dbo.Lessons ([LessonId])
    );
    CREATE INDEX IX_AI_Documents_CourseId ON dbo.AI_Documents ([CourseId]);
    CREATE INDEX IX_AI_Documents_Status ON dbo.AI_Documents ([Status]);
END
GO

-- AI_DocumentChunks (Metadata & lexical grounding)
IF OBJECT_ID('dbo.AI_DocumentChunks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_DocumentChunks (
        [ChunkId] INT IDENTITY(1,1) NOT NULL,
        [DocumentId] INT NOT NULL,
        [CourseId] INT NOT NULL,
        [ModuleId] INT NULL,
        [LessonId] INT NULL,
        [VectorPointId] NVARCHAR(64) NOT NULL, -- UUID in Qdrant
        [ChunkIndex] INT NOT NULL,
        [PageNumber] INT NULL,
        [SectionTitle] NVARCHAR(255) NULL,
        [ChunkText] NVARCHAR(MAX) NOT NULL,
        [TokenCount] INT NOT NULL DEFAULT 0,
        [Visibility] NVARCHAR(50) NOT NULL DEFAULT 'student',
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_DocumentChunks PRIMARY KEY ([ChunkId]),
        CONSTRAINT FK_AI_DocumentChunks_AI_Documents FOREIGN KEY ([DocumentId]) REFERENCES dbo.AI_Documents ([DocumentId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_DocumentChunks_DocumentId ON dbo.AI_DocumentChunks ([DocumentId]);
    CREATE INDEX IX_AI_DocumentChunks_CourseId ON dbo.AI_DocumentChunks ([CourseId]);
END
GO

-- AI_ChatSessions
IF OBJECT_ID('dbo.AI_ChatSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatSessions (
        [SessionId] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        [CourseId] INT NULL,
        [SessionTitle] NVARCHAR(255) NOT NULL DEFAULT 'New Learning Session',
        [LearningMode] NVARCHAR(50) NOT NULL DEFAULT 'CourseAssistant',
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [LastActiveAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_ChatSessions PRIMARY KEY ([SessionId]),
        CONSTRAINT FK_AI_ChatSessions_AspNetUsers FOREIGN KEY ([UserId]) REFERENCES dbo.AspNetUsers ([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_AI_ChatSessions_Courses FOREIGN KEY ([CourseId]) REFERENCES dbo.Courses ([CourseId])
    );
    CREATE INDEX IX_AI_ChatSessions_UserId ON dbo.AI_ChatSessions ([UserId]);
    CREATE INDEX IX_AI_ChatSessions_CourseId ON dbo.AI_ChatSessions ([CourseId]);
END
GO

-- AI_ChatMessages
IF OBJECT_ID('dbo.AI_ChatMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatMessages (
        [MessageId] INT IDENTITY(1,1) NOT NULL,
        [SessionId] INT NOT NULL,
        [SenderRole] NVARCHAR(50) NOT NULL, -- User, Assistant, System
        [MessageText] NVARCHAR(MAX) NOT NULL,
        [IsGroundedInRag] BIT NOT NULL DEFAULT 0,
        [LlmProvider] NVARCHAR(50) NULL,
        [ModelUsed] NVARCHAR(100) NULL,
        [PromptTokens] INT NOT NULL DEFAULT 0,
        [CompletionTokens] INT NOT NULL DEFAULT 0,
        [TotalTokens] INT NOT NULL DEFAULT 0,
        [LatencyMs] INT NOT NULL DEFAULT 0,
        [WasCacheHit] BIT NOT NULL DEFAULT 0,
        [SentAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_ChatMessages PRIMARY KEY ([MessageId]),
        CONSTRAINT FK_AI_ChatMessages_AI_ChatSessions FOREIGN KEY ([SessionId]) REFERENCES dbo.AI_ChatSessions ([SessionId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_ChatMessages_SessionId ON dbo.AI_ChatMessages ([SessionId]);
END
GO

-- AI_ChatSources (Citations)
IF OBJECT_ID('dbo.AI_ChatSources', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatSources (
        [SourceId] INT IDENTITY(1,1) NOT NULL,
        [MessageId] INT NOT NULL,
        [DocumentId] INT NULL,
        [SourceTitle] NVARCHAR(255) NOT NULL,
        [PageNumber] INT NULL,
        [RelevanceScore] FLOAT NOT NULL DEFAULT 0.0,
        [Excerpt] NVARCHAR(1000) NOT NULL,
        CONSTRAINT PK_AI_ChatSources PRIMARY KEY ([SourceId]),
        CONSTRAINT FK_AI_ChatSources_AI_ChatMessages FOREIGN KEY ([MessageId]) REFERENCES dbo.AI_ChatMessages ([MessageId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_ChatSources_MessageId ON dbo.AI_ChatSources ([MessageId]);
END
GO

-- AI_UsageLogs
IF OBJECT_ID('dbo.AI_UsageLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_UsageLogs (
        [LogId] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        [CourseId] INT NULL,
        [QueryType] NVARCHAR(50) NOT NULL,
        [LlmProvider] NVARCHAR(50) NOT NULL,
        [ModelName] NVARCHAR(100) NOT NULL,
        [PromptTokens] INT NOT NULL,
        [CompletionTokens] INT NOT NULL,
        [TotalTokens] INT NOT NULL,
        [EstimatedCost] DECIMAL(18,6) NOT NULL DEFAULT 0.000000,
        [LatencyMs] INT NOT NULL,
        [WasCacheHit] BIT NOT NULL DEFAULT 0,
        [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_UsageLogs PRIMARY KEY ([LogId])
    );
    CREATE INDEX IX_AI_UsageLogs_UserId ON dbo.AI_UsageLogs ([UserId]);
    CREATE INDEX IX_AI_UsageLogs_CourseId ON dbo.AI_UsageLogs ([CourseId]);
END
GO

-- AI_Feedback
IF OBJECT_ID('dbo.AI_Feedback', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_Feedback (
        [FeedbackId] INT IDENTITY(1,1) NOT NULL,
        [MessageId] INT NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        [IsHelpful] BIT NOT NULL,
        [FeedbackText] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_Feedback PRIMARY KEY ([FeedbackId]),
        CONSTRAINT FK_AI_Feedback_AI_ChatMessages FOREIGN KEY ([MessageId]) REFERENCES dbo.AI_ChatMessages ([MessageId]) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_Feedback_MessageId ON dbo.AI_Feedback ([MessageId]);
END
GO

-- AI_SemanticCache
IF OBJECT_ID('dbo.AI_SemanticCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_SemanticCache (
        [CacheId] INT IDENTITY(1,1) NOT NULL,
        [CourseId] INT NULL,
        [QueryHash] NVARCHAR(64) NOT NULL,
        [NormalizedQuery] NVARCHAR(1000) NOT NULL,
        [CachedAnswer] NVARCHAR(MAX) NOT NULL,
        [SourcesJson] NVARCHAR(MAX) NULL,
        [HitCount] INT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [LastAccessedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_AI_SemanticCache PRIMARY KEY ([CacheId])
    );
    CREATE INDEX IX_AI_SemanticCache_QueryHash ON dbo.AI_SemanticCache ([QueryHash]);
    CREATE INDEX IX_AI_SemanticCache_CourseId ON dbo.AI_SemanticCache ([CourseId]);
END
GO

PRINT 'Schema tables and indexes successfully verified!';
GO

-- ==========================================================================================
-- 7. INITIAL SEED DATA INSERTION
-- ==========================================================================================
PRINT 'Inserting complete initial seed data...';
GO
-- ------------------------------------------------------------
-- Data for table [AspNetRoles]
-- ------------------------------------------------------------
INSERT INTO [AspNetRoles] ([Id], [Name]) VALUES (N'1E6B6FA1-F7A7-4943-8726-C1779089DDAC', N'Instructor');
INSERT INTO [AspNetRoles] ([Id], [Name]) VALUES (N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41', N'Student');
INSERT INTO [AspNetRoles] ([Id], [Name]) VALUES (N'231A01BA-5FA9-4FB9-8E0B-2605775B87F9', N'Admin');

-- ------------------------------------------------------------
-- Data for table [AspNetUsers]
-- ------------------------------------------------------------
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'07389ec7-c553-4f01-aced-b454162b0369', N'student@lms.com', 1, N'ACw4K2zDlRXP5hEfhLSIFLusExQ6B2uwSMP1P/I2dM6vhhrSfGNg1/S7aNr9KS3/Fg==', N'3fa2da03-5154-41ba-8797-0f8e716a228e', NULL, 0, 0, NULL, 0, 0, N'student@lms.com', N'Avijit Pakhira', '2026-10-30 19:21:21.349', NULL, N'Passionate learner and software developer', 1, N'Approved', N'Student', NULL, '2026-10-31 19:13:08.553', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'3429de13-3cf2-4fd3-9444-5217035d0d27', N'banashree@gmail.com', 0, N'AJmrwCcy3YMBBCanfbag3eqj41ifrUJv5B1u59WqqjTLgR6HyP1gb3B/xG4pLcbK2Q==', N'feae9077-1202-468b-a935-2496f14ba2f5', N'7044419685', 0, 0, NULL, 0, 0, N'banashree@gmail.com', N'Banashree Mandal', '2026-11-01 06:36:57.908', N'https://krishnaphoto.co.in/wp-content/uploads/2026/07/image-84.png', N'Data Scientist', 1, N'Approved', N'Student', NULL, '2026-11-01 06:36:57.908', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'45aea39e-1994-47f3-bab3-a2504a6f5b84', N'rn@gmail.com', 0, N'AE2gzEOu+Q1mwCg1QDfXIp/qd/y+YKdoSShhiCf3Y+RqPB4QE9vqjH0E1UI8/VlY6g==', N'd046804e-bfa0-47fb-ae96-b64c19a2c8c6', NULL, 0, 0, NULL, 0, 0, N'rn@gmail.com', N'Rabindra Nath Pakhira', '2026-10-31 12:22:44.849', NULL, NULL, 1, N'Approved', N'Student', NULL, '2026-10-31 19:13:08.553', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'74475a22-b99b-4e13-96f2-ebd47179dada', N'manoj23@gmail.com', 0, N'ABvQmePvkWYradXMefQyWrVvBIW4OnDanAUxBW6/KHL33J/Bh2ulaeGvwZiPFhxZkQ==', N'0e08b4b4-a04a-469c-bedc-e38ea19c0b1c', NULL, 0, 0, NULL, 0, 0, N'manoj23@gmail.com', N'Manoj Gupta', '2026-11-04 02:22:54.751', NULL, NULL, 0, N'Declined', N'Instructor', N'nothin', '2026-11-04 02:22:54.751', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'86991e3d-236a-4033-9180-25fccb2b77c7', N'anubhaw@gmail.com', 0, N'AHAt9oAiau4wX14M8r/PFDhfIpP+a0kcGedtFJhpsJyVPvCmnInpsChCN0JMoF88Xw==', N'7f02c22b-cdf6-4aa7-959f-14e221a5ce52', NULL, 0, 0, NULL, 0, 0, N'anubhaw@gmail.com', N'Anubhaw Gupta', '2026-10-31 16:58:39.197', NULL, NULL, 1, N'Approved', N'Instructor', NULL, '2026-10-31 16:58:39.197', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'87fd9351-e4cb-49cb-9555-88ca91a67dff', N'manoj@gmail.com', 0, N'ABPVX4xycKWV9eaPwIEslzNkhDrnvjn0S75evLGqmPUDhp3XTECKRjbbzrF1nJtYDg==', N'e02c67dd-b2f4-43f5-b398-688e556eff51', NULL, 0, 0, NULL, 0, 0, N'manoj@gmail.com', N'Manoj Kumar', '2026-11-01 03:25:25.819', NULL, NULL, 1, N'Approved', N'Student', NULL, '2026-11-01 03:25:25.819', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'8ee085cf-6684-4b09-a72a-d491739745a8', N'instructor@lms.com', 1, N'APcRRBnxFVkminZg8CQ8f2LoI/nuvLl2IVB4Yzn6yF+mTN3+3PXGmGHPzBqjM9nOcg==', N'7c8acd11-dfa2-44f8-9a22-ed9a5a82e76e', NULL, 0, 0, NULL, 0, 0, N'instructor@lms.com', N'Dr. Sarah Connor', '2026-10-30 19:21:21.296', NULL, N'Senior Software Architect & Data Science Consultant', 1, N'Approved', N'Instructor', NULL, '2026-10-31 19:13:08.553', N'/Content/uploads/signatures/signature_8ee085cf-6684-4b09-a72a-d491739745a8_639293550211089170.png');
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', N'admin@lms.com', 1, N'ABZxyEeEIscn0OXZExxe/ZZ/gFqkYcNwEYFLPkqsh4w/qCL1q/Nmyqtf1sRV4/HHfg==', N'd554caa5-0f11-4311-bad3-91b500d51f30', NULL, 0, 0, NULL, 0, 0, N'admin@lms.com', N'System Administrator', '2026-10-30 19:21:20.650', NULL, NULL, 1, N'Approved', N'Admin', NULL, '2026-10-31 19:13:08.553', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'b31be10a-3f8b-4134-a010-cc0c93dea48a', N'suman@gmail.com', 0, N'AIdKbaAlj8yv8BKo9eoQxxos5/LKHGvV9FEh7m1rHPkqmmDOr8fXfXWDl97efXKaYg==', N'd97c8da5-5e23-467c-bea2-565b0a298f4b', NULL, 0, 0, NULL, 0, 0, N'suman@gmail.com', N'Suman', '2026-11-01 05:47:45.840', NULL, NULL, 1, N'Approved', N'Student', NULL, '2026-11-01 05:47:45.840', NULL);
INSERT INTO [AspNetUsers] ([Id], [Email], [EmailConfirmed], [PasswordHash], [SecurityStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEndDateUtc], [LockoutEnabled], [AccessFailedCount], [UserName], [FullName], [CreatedDate], [ProfilePicture], [Bio], [IsApproved], [ApprovalStatus], [RequestedRole], [RejectionReason], [RegisteredDate], [SignaturePath]) VALUES (N'd6eaa339-16b5-4d94-aead-1027f686d6d2', N'sanju@gmail.com', 0, N'AIkuW18+ALF26bbSk1GbK7IzPNWaWn1oBDHv0oDZ0cKgtPlXBHz2cRBLfcu876AFzw==', N'19690713-5374-45f2-9fe9-5872e9e793c6', NULL, 0, 0, NULL, 0, 0, N'sanju@gmail.com', N'Sanju', '2026-11-02 02:28:57.046', NULL, NULL, 1, N'Approved', N'Student', NULL, '2026-11-02 02:28:57.046', NULL);

-- ------------------------------------------------------------
-- Data for table [AspNetUserRoles]
-- ------------------------------------------------------------
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'07389ec7-c553-4f01-aced-b454162b0369', N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'3429de13-3cf2-4fd3-9444-5217035d0d27', N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'45aea39e-1994-47f3-bab3-a2504a6f5b84', N'1E6B6FA1-F7A7-4943-8726-C1779089DDAC');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'86991e3d-236a-4033-9180-25fccb2b77c7', N'1E6B6FA1-F7A7-4943-8726-C1779089DDAC');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'87fd9351-e4cb-49cb-9555-88ca91a67dff', N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'8ee085cf-6684-4b09-a72a-d491739745a8', N'1E6B6FA1-F7A7-4943-8726-C1779089DDAC');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', N'231A01BA-5FA9-4FB9-8E0B-2605775B87F9');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'b31be10a-3f8b-4134-a010-cc0c93dea48a', N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41');
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'd6eaa339-16b5-4d94-aead-1027f686d6d2', N'1F0F7748-2B5B-4707-A75C-A9D3CBFD8A41');

-- ------------------------------------------------------------
-- Data for table [Categories]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Categories] ON;
GO
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (1, N'Programming', N'Programming languages and software development', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (2, N'Data Science', N'Data science, analytics and statistics', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (3, N'Machine Learning', N'Machine learning and artificial intelligence', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (4, N'Web Development', N'Frontend and backend web development', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (5, N'Database', N'Database management and SQL', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (6, N'Computer Science', N'Computer science fundamentals', 1, '2026-10-30 23:35:03.143');
INSERT INTO [Categories] ([CategoryId], [CategoryName], [Description], [IsActive], [CreatedDate]) VALUES (7, N'Couputer Vision', N'for image, video analysis', 1, '2026-11-01 13:55:20.609');
SET IDENTITY_INSERT [Categories] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Courses]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Courses] ON;
GO
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (1, 1, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'C# Programming Fundamentals', N'Learn C# programming from beginner to intermediate level.', N'https://images.unsplash.com/photo-1515879218367-8466d910aaa4?w=600&auto=format&fit=crop&q=80', 999.00, N'Beginner', 20.00, N'Published', 0.00, 5, '2026-10-30 23:35:03.170', NULL, NULL, 30);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (2, 3, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'Machine Learning Fundamentals', N'Learn machine learning algorithms and practical implementation.', N'https://images.unsplash.com/photo-1555949963-aa79dcee981c?w=600&auto=format&fit=crop&q=80', 1499.00, N'Intermediate', 30.00, N'Published', 0.00, 2, '2026-10-30 23:35:03.170', NULL, NULL, 30);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (3, 5, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'SQL Server Masterclass', N'Learn SQL Server, queries, joins, procedures and database design.', N'https://images.unsplash.com/photo-1544383835-bda2bc66a55d?w=600&auto=format&fit=crop&q=80', 799.00, N'Beginner', 15.00, N'Published', 0.00, 2, '2026-10-30 23:35:03.170', NULL, NULL, 30);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (4, 3, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'Numpy', N'You get all basic knowledge about the Numpy Library', N'https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=600&auto=format&fit=crop&q=80', 0.00, N'Beginner', 1.00, N'Draft', 0.00, 1, '2026-10-31 17:24:30.044', '2026-10-31 18:44:38.116', '2026-10-31 17:24:30.044', 30);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (5, 2, N'86991e3d-236a-4033-9180-25fccb2b77c7', N'Data Science', N'Data Science 
this is the begninner course what want to make their carrer in Data Science
One Step forward to their Goal', N'https://abctrainings.in/media/thumbnails/data-science.jpg', 2000.00, N'Beginner', 10.00, N'Published', 3.50, 3, '2026-11-01 03:57:13.187', '2026-11-01 04:32:39.004', '2026-11-01 04:08:43.368', 30);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (6, 6, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'OOPs', NULL, N'https://ladderpython.com/lesson/concepts-of-oops/', 999.00, N'Beginner', 5.00, N'Draft', 0.00, 0, '2026-11-04 02:35:39.651', NULL, NULL, 180);
INSERT INTO [Courses] ([CourseId], [CategoryId], [InstructorId], [Title], [Description], [Thumbnail], [Price], [Level], [DurationInHours], [Status], [Rating], [TotalStudents], [CreatedDate], [UpdatedDate], [PublishedDate], [ValidityInDays]) VALUES (7, 6, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'OOPs', NULL, N'https://ladderpython.com/lesson/concepts-of-oops/', 999.00, N'Beginner', 5.00, N'Draft', 0.00, 0, '2026-11-04 02:35:42.441', NULL, NULL, 180);
SET IDENTITY_INSERT [Courses] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Modules]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Modules] ON;
GO
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (1, 1, N'C# Basics', N'Introduction to C# programming', 1, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (2, 1, N'Object Oriented Programming', N'Classes, objects and inheritance', 2, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (3, 2, N'Introduction to Machine Learning', N'ML fundamentals and workflow', 1, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (4, 2, N'Supervised Learning', N'Classification and regression', 2, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (5, 3, N'SQL Fundamentals', N'SQL Server basics', 1, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (6, 3, N'Advanced SQL', N'Stored procedures, views and optimization', 2, '2026-10-30 23:35:03.186');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (7, 4, N'About Numpy', N'What is Numpy?
why we used Numpy?
How to install ?', 1, '2026-10-31 17:26:00.592');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (8, 5, N'Module 1: Mathematical Foundations & Exploratory Data Analysis (EDA)', N'Focus: Linear algebra basics, descriptive statistics (mean, variance, distributions), and data cleansing using Python libraries like Pandas and NumPy. You learn how to load a dataset, handle missing values, and calculate baseline statistics.', 1, '2026-11-01 03:58:45.696');
INSERT INTO [Modules] ([ModuleId], [CourseId], [ModuleName], [Description], [ModuleOrder], [CreatedDate]) VALUES (9, 7, N'Introducing of OOPs', NULL, 1, '2026-11-04 02:37:23.621');
SET IDENTITY_INSERT [Modules] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Lessons]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Lessons] ON;
GO
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (1, 1, N'Introduction to C#', N'Introduction to C# and .NET', N'Video', N'https://example.com/csharp-intro', NULL, NULL, 30, 1, 1, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (2, 1, N'Variables and Data Types', N'Understanding C# variables', N'Video', N'https://example.com/csharp-variables', NULL, NULL, 40, 2, 0, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (3, 2, N'Classes and Objects', N'Introduction to OOP', N'Video', N'https://example.com/classes', NULL, NULL, 45, 1, 0, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (4, 3, N'What is Machine Learning?', N'Introduction to ML', N'Video', N'https://example.com/ml-intro', NULL, NULL, 35, 1, 1, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (5, 3, N'ML Workflow', N'Machine learning workflow', N'Article', NULL, NULL, NULL, 25, 2, 0, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (6, 5, N'SQL SELECT Statement', N'Introduction to SELECT', N'Video', N'https://example.com/sql-select', NULL, NULL, 30, 1, 1, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (7, 5, N'WHERE Clause', N'Filtering SQL data', N'Video', N'https://example.com/sql-where', NULL, NULL, 25, 2, 0, '2026-10-30 23:35:03.203', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (8, 8, N'Introduction of Data Science', N'This Data Science tutorial will help you in understanding what is Data Science, why we need Data Science, the prerequisites for learning Data Science, what a Data Scientist does, the Data Science lifecycle with an example, and career opportunities in the Data Science domain. You will also learn the differences between Data Science and Business intelligence. The role of a data scientist is one of the sexiest jobs of the century. Every day, companies are looking out for more and more skilled data scientists and studies show that there is expected to be a continued shortfall in qualified candidates to fill the roles. So, let us dive deep into Data Science and understand what is Data Science all about.

', N'Video', N'https://youtu.be/KxryzSO1Fjs?si=jp1589sE8Mna_yyr', N'What is a Data Scientist?
If learning what is data science sounded interesting, understanding what does this job roles is all about will me much more interesting to you. Data scientists are among the most recent analytical data professionals who have the technical ability to handle complicated issues as well as the desire to investigate what questions need to be answered. They''re a mix of mathematicians, computer scientists, and trend forecasters. They''re also in high demand and well-paid because they work in both the business and IT sectors. On a daily basis, a data scientist may do the following tasks:

Discover patterns and trends in datasets to get insights
Create forecasting algorithms and data models 
Improve the quality of data or product offerings by utilising machine learning techniques
Distribute suggestions to other teams and top management
In data analysis, use data tools such as R, SAS, Python, or SQL
Top the field of data science innovations

What Does a Data Scientist Do?
You know what is data science, and you must be wondering what exactly is this job role like - here''s the answer. A data scientist analyzes business data to extract meaningful insights. In other words, a data scientist solves business problems through a series of steps, including:

Before tackling the data collection and analysis, the data scientist determines the problem by asking the right questions and gaining understanding.
The data scientist then determines the correct set of variables and data sets.
The data scientist gathers structured and unstructured data from many disparate sources—enterprise data, public data, etc.
Once the data is collected, the data scientist processes the raw data and converts it into a format suitable for analysis. This involves cleaning and validating the data to guarantee uniformity, completeness, and accuracy.
After the data has been rendered into a usable form, it’s fed into the analytic system—ML algorithm or a statistical model. This is where the data scientists analyze and identify patterns and trends.
When the data has been completely rendered, the data scientist interprets the data to find opportunities and solutions.
The data scientists finish the task by preparing the results and insights to share with the appropriate stakeholders and communicating the results.
Why Become a Data Scientist?
You learnt what is data science. Did it sound exciting? Here''s another solid reason why you should pursue data science as your work-field. According to Glassdoor and Forbes, demand for data scientists will increase by 28 percent by 2026, which speaks of the profession’s durability and longevity, so if you want a secure career, data science offers you that chance. So, if you’re looking for an exciting career that offers stability and generous compensation, then look no further!

Did You Know?🔎
According to the U.S. Bureau of Labor Statistics, employment of data scientists is projected to grow 36% from 2023 to 2033, significantly outpacing the average for all occupations.
Uses of Data Science
Data science may detect patterns in seemingly unstructured or unconnected data, allowing conclusions and predictions to be made.
Tech businesses that acquire user data can utilise strategies to transform that data into valuable or profitable information.
Data Science has also made inroads into the transportation industry, such as with driverless cars. It is simple to lower the number of accidents with the use of driverless cars. For example, with driverless cars, training data is supplied to the algorithm, and the data is examined using data Science approaches, such as the speed limit on the highway, busy streets, etc.
Data Science applications provide a better level of therapeutic customisation through genetics and genomics research.', N'https://github.com/microsoft/data-science-for-beginners', 50, 1, 1, '2026-11-01 04:07:58.573', NULL);
INSERT INTO [Lessons] ([LessonId], [ModuleId], [Title], [Description], [LessonType], [VideoUrl], [Content], [ResourceUrl], [DurationInMinutes], [LessonOrder], [IsPreview], [CreatedDate], [UpdatedDate]) VALUES (9, 8, N'Maths Statistic', NULL, N'Quiz', NULL, NULL, NULL, 15, 2, 0, '2026-11-01 06:09:03.139', NULL);
SET IDENTITY_INSERT [Lessons] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Quizzes]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Quizzes] ON;
GO
INSERT INTO [Quizzes] ([QuizId], [CourseId], [LessonId], [Title], [Description], [PassingPercentage], [TimeLimitMinutes], [TotalMarks], [IsActive], [CreatedDate], [IsFinalExam], [ModuleId]) VALUES (1, 2, NULL, N'Machine Learning Fundamentals Quiz', N'Basic machine learning assessment', 40.00, 30, 10, 1, '2026-10-30 23:35:03.210', 0, NULL);
INSERT INTO [Quizzes] ([QuizId], [CourseId], [LessonId], [Title], [Description], [PassingPercentage], [TimeLimitMinutes], [TotalMarks], [IsActive], [CreatedDate], [IsFinalExam], [ModuleId]) VALUES (2, 5, NULL, N'Data Science', NULL, 40.00, 30, 2, 1, '2026-11-01 06:11:32.969', 0, NULL);
INSERT INTO [Quizzes] ([QuizId], [CourseId], [LessonId], [Title], [Description], [PassingPercentage], [TimeLimitMinutes], [TotalMarks], [IsActive], [CreatedDate], [IsFinalExam], [ModuleId]) VALUES (3, 5, NULL, N'Final', N'Take time and Attempt all Questions', 40.00, 45, 4, 1, '2026-11-01 12:58:51.723', 1, NULL);
SET IDENTITY_INSERT [Quizzes] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Questions]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Questions] ON;
GO
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (1, 1, N'Which of the following is a supervised learning algorithm?', 1, 1);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (2, 1, N'Which library is commonly used for machine learning in Python?', 1, 2);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (3, 1, N'What is the purpose of a training dataset?', 1, 3);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (4, 2, N'
1. Which algorithm is commonly used for predicting a continuous numerical target?
   ', 1, 1);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (5, 2, N'Which Python library is primarily used for working with tabular data and DataFrames?', 1, 2);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (6, 3, N'Which measure of central tendency is generally less affected by extreme outliers?', 1, 1);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (7, 3, N'What is the main purpose of splitting data into training and test sets?', 1, 2);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (8, 3, N'Which metric is commonly used to measure the average squared difference between predicted and actual numerical values?', 1, 3);
INSERT INTO [Questions] ([QuestionId], [QuizId], [QuestionText], [Marks], [QuestionOrder]) VALUES (9, 3, N'In SQL, which clause is used to filter rows based on a condition?', 1, 4);
SET IDENTITY_INSERT [Questions] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [QuestionOptions]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [QuestionOptions] ON;
GO
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (1, 1, N'Linear Regression', 1, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (2, 1, N'K-Means', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (3, 1, N'Apriori', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (4, 1, N'PCA', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (5, 2, N'Scikit-learn', 1, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (6, 2, N'Photoshop', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (7, 2, N'Bootstrap', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (8, 2, N'IIS', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (9, 3, N'Train the model', 1, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (10, 3, N'Only store images', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (11, 3, N'Create a website', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (12, 3, N'Send emails', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (13, 4, N'K-Means', 0, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (14, 4, N'Naive Bayes Classifier', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (15, 4, N'Linear Regression', 1, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (16, 4, N'K-Nearest Neighbors Classifier', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (17, 5, N'NumPy', 0, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (18, 5, N'Pandas', 1, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (19, 5, N'Matplotlib', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (20, 5, N'Scikit-learn', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (21, 6, N'Mean', 0, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (22, 6, N'Median', 1, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (23, 6, N'Variance', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (24, 6, N'Standard deviation', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (25, 7, N'To increase the number of features', 0, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (26, 7, N'To evaluate generalization on unseen data', 1, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (27, 7, N'To remove all missing values', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (28, 7, N'To convert categorical data into numbers', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (29, 8, N'MSE', 1, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (30, 8, N'Accuracy', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (31, 8, N'Precision', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (32, 8, N'Recall', 0, 4);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (33, 9, N'WHERE', 1, 1);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (34, 9, N'ORDER BY', 0, 2);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (35, 9, N'GROUP BY', 0, 3);
INSERT INTO [QuestionOptions] ([OptionId], [QuestionId], [OptionText], [IsCorrect], [OptionOrder]) VALUES (36, 9, N'JOIN', 0, 4);
SET IDENTITY_INSERT [QuestionOptions] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Assignments]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Assignments] ON;
GO
INSERT INTO [Assignments] ([AssignmentId], [CourseId], [LessonId], [Title], [Description], [DueDate], [MaxMarks], [CreatedDate], [ModuleId]) VALUES (1, 5, NULL, N'Make a project to identify that student has passed or not', N'Google Colab
ML Librarise
Proper EDA
Proper Feature Extraction
Choose Correct Model
Make a classification Report', NULL, 100, '2026-11-01 06:28:03.735', NULL);
SET IDENTITY_INSERT [Assignments] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [CourseReviews]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [CourseReviews] ON;
GO
INSERT INTO [CourseReviews] ([ReviewId], [CourseId], [StudentId], [Rating], [ReviewText], [CreatedDate], [UpdatedDate]) VALUES (1, 5, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 3, N'Good but not Best', '2026-11-01 13:45:34.439', '2026-11-01 13:45:49.601');
INSERT INTO [CourseReviews] ([ReviewId], [CourseId], [StudentId], [Rating], [ReviewText], [CreatedDate], [UpdatedDate]) VALUES (2, 5, N'07389ec7-c553-4f01-aced-b454162b0369', 4, N'Better
', '2026-11-01 13:49:34.066', NULL);
SET IDENTITY_INSERT [CourseReviews] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Enrollments]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Enrollments] ON;
GO
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (1, N'07389ec7-c553-4f01-aced-b454162b0369', 1, '2026-10-31 00:51:21.526', N'Completed', 100.00, '2026-10-30 19:23:12.297', '2026-11-30 00:51:21.526');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (2, N'07389ec7-c553-4f01-aced-b454162b0369', 4, '2026-10-31 22:59:28.643', N'Active', 0.00, NULL, '2026-11-30 22:59:28.643');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (3, N'07389ec7-c553-4f01-aced-b454162b0369', 2, '2026-11-01 00:13:35.803', N'Active', 0.00, NULL, '2026-12-01 00:13:35.803');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (4, N'86991e3d-236a-4033-9180-25fccb2b77c7', 1, '2026-11-01 00:15:54.490', N'Active', 0.00, NULL, '2026-12-01 00:15:54.490');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (5, N'87fd9351-e4cb-49cb-9555-88ca91a67dff', 1, '2026-11-01 08:56:57.406', N'Completed', 100.00, '2026-11-01 03:27:19.811', '2026-12-01 08:56:57.406');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (6, N'07389ec7-c553-4f01-aced-b454162b0369', 5, '2026-11-01 09:55:46.350', N'Completed', 75.00, '2026-11-01 05:43:14.455', '2026-12-01 09:55:46.350');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (7, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 1, '2026-11-01 11:28:59.646', N'Completed', 100.00, '2026-11-01 06:00:44.263', '2026-12-01 11:28:59.646');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (8, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 5, '2026-11-01 11:39:42.343', N'Active', 50.00, NULL, '2026-12-01 11:39:42.343');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (9, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 5, '2026-11-01 14:10:42.830', N'Completed', 100.00, '2026-11-01 13:07:36.169', '2026-12-01 14:10:42.830');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (10, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 1, '2026-11-01 19:11:32.620', N'Active', 0.00, NULL, '2026-12-01 19:11:32.620');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (11, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 2, '2026-11-01 21:35:42.410', N'Active', 0.00, NULL, '2026-12-01 21:35:42.410');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (12, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 3, '2026-11-01 21:37:30.223', N'Active', 0.00, NULL, '2026-12-01 21:37:30.223');
INSERT INTO [Enrollments] ([EnrollmentId], [StudentId], [CourseId], [EnrollmentDate], [Status], [CompletionPercentage], [CompletionDate], [ExpiryDate]) VALUES (13, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 3, '2026-11-01 21:39:41.930', N'Active', 0.00, NULL, '2026-12-01 21:39:41.930');
SET IDENTITY_INSERT [Enrollments] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [LessonProgress]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [LessonProgress] ON;
GO
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (1, N'07389ec7-c553-4f01-aced-b454162b0369', 1, 1, 0, '2026-10-31 00:53:11.990', '2026-10-31 00:53:11.990');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (2, N'07389ec7-c553-4f01-aced-b454162b0369', 2, 1, 0, '2026-10-31 00:53:12.130', '2026-10-31 00:53:12.130');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (3, N'07389ec7-c553-4f01-aced-b454162b0369', 3, 1, 0, '2026-10-31 00:53:12.290', '2026-10-31 00:53:12.290');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (4, N'87fd9351-e4cb-49cb-9555-88ca91a67dff', 1, 1, 0, '2026-11-01 08:57:08.120', '2026-11-01 08:57:08.120');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (5, N'87fd9351-e4cb-49cb-9555-88ca91a67dff', 2, 1, 0, '2026-11-01 08:57:14.706', '2026-11-01 08:57:14.706');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (6, N'87fd9351-e4cb-49cb-9555-88ca91a67dff', 3, 1, 0, '2026-11-01 08:57:19.780', '2026-11-01 08:57:19.780');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (7, N'07389ec7-c553-4f01-aced-b454162b0369', 8, 1, 0, '2026-11-01 11:13:14.420', '2026-11-01 11:13:14.420');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (8, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 1, 1, 0, '2026-11-01 11:30:36.930', '2026-11-01 11:30:36.930');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (9, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 2, 1, 0, '2026-11-01 11:30:39.733', '2026-11-01 11:30:39.733');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (10, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 3, 1, 0, '2026-11-01 11:30:44.233', '2026-11-01 11:30:44.233');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (11, N'b31be10a-3f8b-4134-a010-cc0c93dea48a', 8, 1, 0, '2026-11-01 11:53:01.226', '2026-11-01 11:53:01.226');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (12, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 8, 1, 0, '2026-11-01 15:27:02.083', '2026-11-01 15:27:02.083');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (13, N'3429de13-3cf2-4fd3-9444-5217035d0d27', 9, 1, 0, '2026-11-01 15:27:22.210', '2026-11-01 15:27:22.210');
INSERT INTO [LessonProgress] ([ProgressId], [StudentId], [LessonId], [IsCompleted], [WatchTimeInSeconds], [CompletedDate], [LastAccessedDate]) VALUES (14, N'07389ec7-c553-4f01-aced-b454162b0369', 9, 1, 0, '2026-11-01 23:24:06.750', '2026-11-01 23:24:06.750');
SET IDENTITY_INSERT [LessonProgress] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [PasswordResetRequests]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [PasswordResetRequests] ON;
GO
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (1, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'instructor@lms.com', N'Instructor', N'Forgot instructor account password. Please approve reset link.', '2026-11-04 02:44:31.513', N'Approved', N'2092f94a2b5642a99e9898d58a91a940', '2026-11-07 02:44:50.063', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 02:44:50.063', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (2, N'8ee085cf-6684-4b09-a72a-d491739745a8', N'instructor@lms.com', N'Instructor', N'Forgot instructor account password. Please approve reset link.', '2026-11-04 02:45:24.890', N'Approved', N'a78667dbf28f45999891431632dfe47c', '2026-11-07 02:45:29.873', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 02:45:29.873', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (3, N'86991e3d-236a-4033-9180-25fccb2b77c7', N'anubhaw@gmail.com', N'Instructor', N'forget it', '2026-11-04 04:05:54.857', N'Approved', N'07703b8c3f104fda80034fd9ab3c8c80', '2026-11-07 04:11:39.170', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:11:39.170', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (4, N'87fd9351-e4cb-49cb-9555-88ca91a67dff', N'manoj@gmail.com', N'Student', NULL, '2026-11-04 04:12:21.100', N'Approved', N'133da35675c84a14aadac5f6b050e079', '2026-11-07 04:12:55.450', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:12:55.450', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (5, N'07389ec7-c553-4f01-aced-b454162b0369', N'student@lms.com', N'Student', NULL, '2026-11-04 04:14:21.273', N'Approved', N'046bba87e6dd47f896a17df28090d8d7', '2026-11-07 04:14:41.220', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:14:41.220', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (6, N'86991e3d-236a-4033-9180-25fccb2b77c7', N'anubhaw@gmail.com', N'Instructor', N'bhulk gya', '2026-11-04 04:20:21.603', N'Approved', N'11be0f35cda94692babca0e56c261908', '2026-11-07 04:20:40.420', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:20:40.420', NULL, 1);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (7, N'd6eaa339-16b5-4d94-aead-1027f686d6d2', N'sanju@gmail.com', N'Student', N'Need password reset to access course materials before exam.', '2026-11-04 04:38:40.080', N'Completed', N'324dc004cffa4d0687aa3f628b521249', '2026-11-07 04:38:54.777', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:38:54.777', NULL, 0);
INSERT INTO [PasswordResetRequests] ([RequestId], [UserId], [Email], [Role], [RequestReason], [RequestDate], [Status], [ResetToken], [TokenExpiry], [ApprovedByAdminId], [ApprovedDate], [AdminNotes], [IsApproved]) VALUES (8, N'86991e3d-236a-4033-9180-25fccb2b77c7', N'anubhaw@gmail.com', N'Instructor', NULL, '2026-11-04 04:41:57.220', N'Approved', N'2baa114c40f54725b32276bf20c938e7', '2026-11-07 04:42:07.310', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-04 04:42:07.310', NULL, 1);
SET IDENTITY_INSERT [PasswordResetRequests] OFF;
GO

-- ------------------------------------------------------------
-- Data for table [Announcements]
-- ------------------------------------------------------------
SET IDENTITY_INSERT [Announcements] ON;
GO
INSERT INTO [Announcements] ([AnnouncementId], [Title], [Message], [CreatedBy], [CreatedDate], [IsActive]) VALUES (1, N'Diwali Special Occasion', N'Course price will be 20% discount
Learn More, Gain More', N'a02a5e0d-e3fb-4557-b6c7-ad73406cdc0c', '2026-11-01 14:08:08.756', 1);
SET IDENTITY_INSERT [Announcements] OFF;
GO


-- ==========================================================================================
-- 8. VERIFICATION QUERY
-- ==========================================================================================
PRINT '=========================================================================';
PRINT 'LMS DATABASE SETUP COMPLETED SUCCESSFULLY!';
PRINT '=========================================================================';

SELECT 
    t.name AS [TableName], 
    p.rows AS [RecordCount]
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
ORDER BY t.name;
GO