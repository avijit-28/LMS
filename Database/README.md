# EduPlatform LMS - Complete Database Setup

This folder contains the complete, self-contained SQL script to provision and initialize the entire database required by the EduPlatform LMS application on any SQL Server instance.

---

## 📁 File Overview

- **`LMS_Complete_Database.sql`**  
  A single, unified SQL script containing:
  1. **Database Creation**: Automatically creates `[LMS]` if it does not already exist.
  2. **Security & Membership**: Full ASP.NET Identity 2.0 tables (`AspNetRoles`, `AspNetUsers`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `PasswordResetRequests`).
  3. **Core LMS Academics**: `Categories`, `Courses`, `Modules`, `Lessons`, `Enrollments`, `LessonProgress`.
  4. **Examinations & Assignments**: `Quizzes`, `Questions`, `QuestionOptions`, `QuizAttempts`, `QuizAnswers`, `Assignments`, `Submissions`.
  5. **Certificates & Commerce**: `Certificates`, `CourseReviews`, `Payments`, `Announcements`, `Notifications`.
  6. **Enterprise AI & RAG Engine**: `AI_Documents`, `AI_DocumentChunks`, `AI_ChatSessions`, `AI_ChatMessages`, `AI_ChatSources`, `AI_UsageLogs`, `AI_Feedback`, `AI_SemanticCache`.
  7. **Relationships & Indexes**: Foreign key constraints with proper cascade actions and performance indexes.
  8. **Seed Data**: Default roles, pre-configured verified users (Admin, Instructor, Student), course categories, sample courses, curriculum modules, lessons (videos + Cloudinary PDF resources), quizzes, questions, and practical tasks.

---

## 🚀 How to Execute on Another Machine

### Option A: Using SQL Server Management Studio (SSMS) or Azure Data Studio
1. Open **SSMS** or **Azure Data Studio**.
2. Connect to your SQL Server instance (e.g., `.` or `localhost` or `.\SQLEXPRESS`).
3. Open `LMS_Complete_Database.sql` (`File` > `Open` > `File...`).
4. Click **Execute** (or press **F5**).
5. The output window will confirm table creation, seed data insertion, and display record counts.

### Option B: Using Command Prompt / PowerShell (sqlcmd)
```powershell
sqlcmd -S . -E -i "D:\All\.net\MVC\LMS\Database\LMS_Complete_Database.sql"
```
*(Replace `.` with your SQL Server instance name if different, e.g. `.\SQLEXPRESS`)*

---

## 🔑 Default Test Accounts & Credentials

| Role | Email Address | Password | Name |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@lms.com` | `Admin@123` | System Administrator |
| **Instructor** | `instructor@lms.com` | `Instructor@123` | Dr. Sarah Connor |
| **Instructor** | `anubhaw@gmail.com` | `Admin@123` | Anubhaw Gupta |
| **Student** | `student@lms.com` | `Student@123` | Avijit Pakhira |

---

## ⚙️ Web.config Connection String

In the target machine's `Web.config`, ensure the `DefaultConnection` points to the new database:

```xml
<connectionStrings>
  <add name="DefaultConnection" 
       connectionString="Data Source=.;Initial Catalog=LMS;Persist Security Info=True;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True" 
       providerName="System.Data.SqlClient" />
</connectionStrings>
```
