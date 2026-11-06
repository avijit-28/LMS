-- ==============================================================================
-- LMS RAG AI Learning Assistant - Enterprise Database Schema Migration
-- Compliant with Plan 1 (Enterprise Advancements) & Plan 2 (Architecture Blueprint)
-- ==============================================================================

USE [LMS];
GO

-- 1. AI Documents Table
IF OBJECT_ID('dbo.AI_Documents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_Documents (
        DocumentId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        ModuleId INT NULL,
        LessonId INT NULL,
        Title NVARCHAR(255) NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        FileType NVARCHAR(50) NOT NULL, -- PDF, DOCX, PPTX, TXT
        FileSize INT NOT NULL,
        TotalPages INT NOT NULL DEFAULT 1,
        TotalChunks INT NOT NULL DEFAULT 0,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Uploaded', -- Uploaded, Processing, Indexed, Failed
        ErrorMessage NVARCHAR(MAX) NULL,
        Visibility NVARCHAR(50) NOT NULL DEFAULT 'student', -- student, instructor_only, admin
        UploadedBy NVARCHAR(128) NOT NULL,
        UploadedAt DATETIME NOT NULL DEFAULT GETDATE(),
        IndexedAt DATETIME NULL,
        CONSTRAINT FK_AI_Documents_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
        CONSTRAINT FK_AI_Documents_Modules FOREIGN KEY (ModuleId) REFERENCES dbo.Modules(ModuleId),
        CONSTRAINT FK_AI_Documents_Lessons FOREIGN KEY (LessonId) REFERENCES dbo.Lessons(LessonId)
    );
    CREATE INDEX IX_AI_Documents_CourseId ON dbo.AI_Documents(CourseId);
    CREATE INDEX IX_AI_Documents_Status ON dbo.AI_Documents(Status);
END
GO

-- 2. AI Document Chunks Table (Metadata & Lexical Grounding)
IF OBJECT_ID('dbo.AI_DocumentChunks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_DocumentChunks (
        ChunkId INT IDENTITY(1,1) PRIMARY KEY,
        DocumentId INT NOT NULL,
        CourseId INT NOT NULL,
        ModuleId INT NULL,
        LessonId INT NULL,
        VectorPointId NVARCHAR(64) NOT NULL, -- UUID in Qdrant
        ChunkIndex INT NOT NULL,
        PageNumber INT NULL,
        SectionTitle NVARCHAR(255) NULL,
        ChunkText NVARCHAR(MAX) NOT NULL,
        TokenCount INT NOT NULL DEFAULT 0,
        Visibility NVARCHAR(50) NOT NULL DEFAULT 'student',
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_AI_Chunks_Documents FOREIGN KEY (DocumentId) REFERENCES dbo.AI_Documents(DocumentId) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_Chunks_DocumentId ON dbo.AI_DocumentChunks(DocumentId);
    CREATE INDEX IX_AI_Chunks_CourseId ON dbo.AI_DocumentChunks(CourseId);
END
GO

-- 3. AI Chat Sessions Table
IF OBJECT_ID('dbo.AI_ChatSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatSessions (
        SessionId INT IDENTITY(1,1) PRIMARY KEY,
        UserId NVARCHAR(128) NOT NULL,
        CourseId INT NULL,
        SessionTitle NVARCHAR(255) NOT NULL DEFAULT 'New Learning Session',
        LearningMode NVARCHAR(50) NOT NULL DEFAULT 'CourseAssistant', -- CourseAssistant, SocraticTutor, ExamPrep, PracticeQuiz, CodeDebugger
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        LastActiveAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_AI_ChatSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AI_ChatSessions_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId)
    );
    CREATE INDEX IX_AI_ChatSessions_UserId ON dbo.AI_ChatSessions(UserId);
    CREATE INDEX IX_AI_ChatSessions_CourseId ON dbo.AI_ChatSessions(CourseId);
END
GO

-- 4. AI Chat Messages Table
IF OBJECT_ID('dbo.AI_ChatMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatMessages (
        MessageId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId INT NOT NULL,
        SenderRole NVARCHAR(50) NOT NULL, -- User, Assistant, System
        MessageText NVARCHAR(MAX) NOT NULL,
        IsGroundedInRag BIT NOT NULL DEFAULT 0,
        LlmProvider NVARCHAR(50) NULL, -- gemini, groq, local
        ModelUsed NVARCHAR(100) NULL,
        PromptTokens INT NOT NULL DEFAULT 0,
        CompletionTokens INT NOT NULL DEFAULT 0,
        TotalTokens INT NOT NULL DEFAULT 0,
        LatencyMs INT NOT NULL DEFAULT 0,
        WasCacheHit BIT NOT NULL DEFAULT 0,
        SentAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_AI_ChatMessages_Sessions FOREIGN KEY (SessionId) REFERENCES dbo.AI_ChatSessions(SessionId) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_ChatMessages_SessionId ON dbo.AI_ChatMessages(SessionId);
END
GO

-- 5. AI Chat Sources (Clickable Citations)
IF OBJECT_ID('dbo.AI_ChatSources', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_ChatSources (
        SourceId INT IDENTITY(1,1) PRIMARY KEY,
        MessageId INT NOT NULL,
        DocumentId INT NULL,
        SourceTitle NVARCHAR(255) NOT NULL,
        PageNumber INT NULL,
        RelevanceScore FLOAT NOT NULL DEFAULT 0.0,
        Excerpt NVARCHAR(1000) NOT NULL,
        CONSTRAINT FK_AI_ChatSources_Messages FOREIGN KEY (MessageId) REFERENCES dbo.AI_ChatMessages(MessageId) ON DELETE CASCADE
    );
    CREATE INDEX IX_AI_ChatSources_MessageId ON dbo.AI_ChatSources(MessageId);
END
GO

-- 6. AI Usage Logs & Observability (Pillar 3)
IF OBJECT_ID('dbo.AI_UsageLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_UsageLogs (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        UserId NVARCHAR(128) NOT NULL,
        CourseId INT NULL,
        QueryType NVARCHAR(50) NOT NULL, -- RAG, QuizGen, Summarize, CodeExplain
        LlmProvider NVARCHAR(50) NOT NULL,
        ModelName NVARCHAR(100) NOT NULL,
        PromptTokens INT NOT NULL,
        CompletionTokens INT NOT NULL,
        TotalTokens INT NOT NULL,
        EstimatedCost DECIMAL(18, 6) NOT NULL DEFAULT 0.0,
        LatencyMs INT NOT NULL,
        WasCacheHit BIT NOT NULL DEFAULT 0,
        Timestamp DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_AI_UsageLogs_UserId ON dbo.AI_UsageLogs(UserId);
    CREATE INDEX IX_AI_UsageLogs_CourseId ON dbo.AI_UsageLogs(CourseId);
END
GO

-- 7. AI Student Feedback
IF OBJECT_ID('dbo.AI_Feedback', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_Feedback (
        FeedbackId INT IDENTITY(1,1) PRIMARY KEY,
        MessageId INT NOT NULL,
        UserId NVARCHAR(128) NOT NULL,
        IsHelpful BIT NOT NULL,
        FeedbackText NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_AI_Feedback_Messages FOREIGN KEY (MessageId) REFERENCES dbo.AI_ChatMessages(MessageId) ON DELETE CASCADE
    );
END
GO

-- 8. AI Semantic Cache Table (Pillar 4)
IF OBJECT_ID('dbo.AI_SemanticCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_SemanticCache (
        CacheId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NULL,
        QueryHash NVARCHAR(64) NOT NULL,
        NormalizedQuery NVARCHAR(1000) NOT NULL,
        CachedAnswer NVARCHAR(MAX) NOT NULL,
        SourcesJson NVARCHAR(MAX) NULL,
        HitCount INT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        LastAccessedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_AI_SemanticCache_Hash ON dbo.AI_SemanticCache(QueryHash);
    CREATE INDEX IX_AI_SemanticCache_CourseId ON dbo.AI_SemanticCache(CourseId);
END
GO

PRINT 'Enterprise AI RAG tables created successfully.';
