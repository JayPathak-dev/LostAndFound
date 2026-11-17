# Campus Lost and Found Management System
## Centralized University Belongings Recovery & Custody Management Platform
### Academic B.Tech Computer Science and Engineering Major Project

---

## 1. Project Overview

The **Campus Lost and Found Management System** is a production-quality, three-layer ASP.NET Core MVC web application developed using C#, Entity Framework Core, ASP.NET Core Identity, and Microsoft SQL Server.

The platform provides a centralized, secure, and accountable university mechanism that replaces informal lost-and-found practices such as WhatsApp groups, physical notice boards, verbal security inquiries, and unstructured paper registers.

### Core Lifecycle
```text
[Report Belonging] ──> [Discover on Board] ──> [Rule-Based Matching Engine]
                                                       │
[Maintain Records] <── [Supervised Handover] <── [Verify Ownership] <── [Submit Claim]
```

---

## 2. Technology Stack

- **Programming Language:** C# 13 / .NET 10 LTS compatible
- **Framework:** ASP.NET Core MVC (Three-Layer Architecture)
- **Frontend / Presentation:** Razor Views (`.cshtml`), HTML5, CSS3, Bootstrap 5, Bootstrap Icons
- **Authentication & Security:** ASP.NET Core Identity, PBKDF2 Password Hashing, Anti-Forgery Tokens (CSRF)
- **Authorization:** Role-Based Access Control (RBAC) across 4 discrete roles
- **ORM & Data Access:** Entity Framework Core (Code-First Approach) with SQL Server Migrations
- **Primary Database:** Microsoft SQL Server (with automated dev fallback to SQLite for local cross-platform testing)
- **Unit Testing:** xUnit Test Suite

---

## 3. Application Architecture (Three-Layer)

```text
Browser Client (Desktop / Tablet / Mobile)
        │
        ▼  HTTP(S) Requests + Anti-Forgery Token
┌────────────────────────────────────────────────────────────────────────┐
│ 1. PRESENTATION LAYER (Razor Views + ViewModels)                       │
│    • Public Item Board (strictly redacted - no private attributes)     │
│    • Lost & Found Reporting Forms                                      │
│    • Interactive Ownership Verification Questionnaire                  │
│    • Security Desk Custody & Handover Screens                          │
│    • Campus Administrator & System Administrator Consoles              │
└────────────────────────────────────┬───────────────────────────────────┘
                                     │
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 2. BUSINESS LOGIC LAYER (Controllers + C# Domain Services)             │
│    • ItemService (reporting, status transitions, public board queries) │
│    • MatchingService (rule-based multi-factor weighted scoring engine) │
│    • VerificationService (confidential question generation & scoring)  │
│    • ClaimService (ownership claims & evaluation lifecycle)            │
│    • CustodyService (physical locker custody & ACID handover)          │
│    • FileUploadService (magic byte validation, safe GUID filenames)    │
│    • AuditService (indelible audit logging for all campus actions)     │
└────────────────────────────────────┬───────────────────────────────────┘
                                     │
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 3. DATA ACCESS LAYER (Entity Framework Core + SQL Server)              │
│    • ApplicationDbContext (11 Domain Entities + Identity Tables)       │
│    • Relational Constraints, Foreign Keys & Cascade Restrictions       │
│    • Code-First Database Migrations                                    │
│    • Microsoft SQL Server                                              │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Database Design & Entity Architecture

The relational schema consists of 11 core domain entities plus standard ASP.NET Core Identity tables:

| Entity | Description | Key Attributes |
| :--- | :--- | :--- |
| **`ApplicationUser`** | Extended Identity user | `FullName`, `RegistrationNumber`, `Email`, `PhoneNumber`, `IsActive` |
| **`Item`** | Lost & Found reports | `ItemId`, `Title`, `Description`, `ReportType`, `CategoryId`, `LocationId`, `Status`, `ReporterId` |
| **`ItemCategory`** | Item classifications | `CategoryId`, `CategoryName`, `RetentionPeriodDays`, `IsActive` |
| **`CampusLocation`** | Campus buildings & areas | `LocationId`, `BuildingName`, `BlockName`, `Floor`, `AreaName`, `IsActive` |
| **`PrivateItemDetail`** | Confidential attributes | `PrivateDetailId`, `ItemId`, `DistinctiveMarks`, `HiddenFeatures`, `InternalContents`, `AdditionalIdentifiers` |
| **`MatchSuggestion`** | Algorithmic suggestions | `MatchId`, `LostItemId`, `FoundItemId`, `MatchScore`, `MatchReason`, `ReviewStatus` |
| **`Claim`** | Ownership claims | `ClaimId`, `ItemId`, `ClaimantId`, `ClaimDescription`, `Status`, `SubmittedAt`, `ReviewedBy` |
| **`VerificationChallenge`** | Question challenges | `ChallengeId`, `ClaimId`, `QuestionPrompt`, `ExpectedAnswerHash`, `SubmittedAnswer`, `SimilarityScore` |
| **`CustodyRecord`** | Physical security storage | `CustodyId`, `ItemId`, `CustodyLocation`, `ResponsibleSecurityUserId`, `ReceivedAt`, `ReleasedAt` |
| **`Handover`** | Supervised physical returns | `HandoverId`, `ItemId`, `ClaimId`, `ReceivingUserId`, `SecurityUserId`, `ProofReference`, `HandoverDateTime` |
| **`DisposalRecord`** | Unclaimed item disposals | `DisposalId`, `ItemId`, `DisposalReason`, `AuthorizedBy`, `DisposalDate` |
| **`AuditEntry`** | System audit logging | `AuditId`, `ActorId`, `ActorEmail`, `Action`, `TargetRecord`, `TargetRecordId`, `Timestamp` |

---

## 5. User Roles and Permission Matrix

| Role | Browse & Search | Report Lost/Found | Claim Items | Manage Custody | Supervise Handover | Manage Categories/Locations | Manage Users & Roles |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Student / Staff** (Default) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Security Personnel** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Campus Administrator** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **System Administrator** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

> **Security Enforcement:** All public registrations are automatically assigned the `Student/Staff` role. Elevated roles are assignable only by a System Administrator. Server-side `[Authorize(Roles = "...")]` attributes enforce authorization across all endpoints.

---

## 6. Pre-Configured Demonstration Accounts

The database seed routine (`DbInitializer.cs`) automatically populates standard test accounts:

| Role | Email | Password | Full Name / Description |
| :--- | :--- | :--- | :--- |
| **System Administrator** | `sysadmin@campus.edu` | `Admin@12345` | System Administrator (Reg: SYS-001) |
| **Campus Administrator** | `campusadmin@campus.edu` | `Admin@12345` | Campus Administrator (Reg: ADM-101) |
| **Security Personnel** | `security@campus.edu` | `Security@12345` | Officer Vikram Sharma (Reg: SEC-042) |
| **Student 1** | `john.doe@campus.edu` | `Student@12345` | John Doe (Reg: 21BCE1042) |
| **Student 2** | `jane.smith@campus.edu` | `Student@12345` | Jane Smith (Reg: 21BCE1180) |

---

## 7. Intelligent Rule-Based Matching Engine

When a report is submitted, the C# `MatchingService` analyzes opposite-type active reports using weighted factors:

1. **Title Keywords Similarity (35%):** Tokenization, stop-word filtering, Jaccard overlap, and brand term matching.
2. **Category Match (20%):** Exact category match.
3. **Campus Location Match (20%):** Exact location match (20%) or same campus building match (12%).
4. **Time & Date Proximity (15%):**
   - Within 24 hours: 15%
   - Within 3 days: 10.5%
   - Within 7 days: 6%
   - Within 14 days: 3%
5. **Description Keywords Similarity (10%):** Overlap of descriptive attributes.

*Total Score: 0 to 100%. Scores $\ge 45\%$ are recorded as `MatchSuggestion` entries with human-readable diagnostic explanations.*

---

## 8. Confidential Ownership Verification System

To prevent fraudulent claims:
1. When a user reports a found item, they supply confidential identifying information (e.g., hidden features, internal bag contents, unique scratches, serial number fragments).
2. These details are stored in `PrivateItemDetails` and are **strictly excluded** from the public board and ordinary ViewModels.
3. When a claimant submits a claim, the system generates challenge questions (e.g., *"What specific belongings are stored inside this wallet?"*).
4. The claimant responds without ever seeing the expected answers.
5. The system performs token and keyword similarity analysis, calculating a confidence score.
6. The Security Officer reviews claimant answers side-by-side with confidential finder attributes before authorizing handover.

---

## 9. Supervised Custody & Handover Workflow

Physical handovers require direct supervision:
1. Security logs the found item into a specific custody locker (`CustodyRecord`).
2. When a claim is `Approved`, it enters the Security Officer's Handover Queue.
3. The claimant appears in person at the Security Desk and presents their University ID or Government Photo ID.
4. The Security Officer verifies the physical item, enters the proof reference (e.g., `Verified Student ID: 21BCE1042`), and executes the handover.
5. The system executes an ACID database transaction:
   - Updates item status to `Returned`.
   - Releases active custody.
   - Creates a permanent `Handover` record.
   - Records an immutable `AuditEntry`.

---

## 10. Setup and Running Instructions

### Prerequisites
- .NET 10 SDK (or compatible .NET LTS runtime)
- Visual Studio 2022 (v17.10+) or VS Code
- Microsoft SQL Server / LocalDB / SQL Server Express (optional: automatic SQLite fallback included)

### Visual Studio 2022
1. Clone or open the repository folder `/Users/jaypathak/Desktop/LostAndFound`.
2. Double-click `CampusLostAndFound.sln` to open the solution.
3. Check `appsettings.json` to verify your SQL Server connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=CampusLostAndFoundDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
   }
   ```
4. Set `CampusLostAndFound` as the Startup Project.
5. Press `F5` or `Ctrl + F5` to run. The database is automatically initialized and seeded with test data on initial launch.

### Command Line (.NET CLI)
```bash
# Navigate to the project directory
cd CampusLostAndFound

# Build the application
dotnet build

# Apply EF Core migrations to SQL Server (optional if using automatic code-first initialization)
dotnet ef database update

# Run the web application
dotnet run --urls "http://localhost:5000"
```
Open your browser at `http://localhost:5000`.

### Running Unit Tests
```bash
dotnet test CampusLostAndFound.sln
```
All 9 automated unit tests verifying matching logic, private detail protection, and transactional handovers will execute.

---

## 11. Viva Examination & Demonstration Walkthrough

For academic project evaluation, follow this step-by-step demonstration:

1. **Public Board & Landing Page:**
   - Open `http://localhost:5000` (logged out).
   - Show real-time statistics retrieved directly from database queries.
   - Click **Browse Found Items** to show the public board. Point out that confidential marks and contents are completely hidden.

2. **Login as Student:**
   - Log in using `john.doe@campus.edu` / `Student@12345`.
   - View **My Dashboard** showing user reports, active claims, and automated match suggestions.

3. **Item Reporting:**
   - Click **Report Found Item**. Enter title, category, campus location, and fill in the **Private Ownership Verification Details** section. Submit and inspect the report.

4. **Matching Engine Demo:**
   - Point out the pre-seeded "Dell 65W USB-C Laptop Charger" found in Central Library and the corresponding lost report.
   - Navigate to **Match Suggestions** to show the 85% match score with its full factor breakdown.

5. **Claim Submission & Verification Challenge:**
   - Log in as `jane.smith@campus.edu` / `Student@12345`.
   - Submit an ownership claim for a found item.
   - Answer the verification questions presented on the challenge screen.

6. **Security Review & Supervised Handover:**
   - Log in as `security@campus.edu` / `Security@12345`.
   - Open **Security Desk** $\to$ **Pending Claims**.
   - Click **Review Claim** to demonstrate the **Side-by-Side Verification Screen** comparing the claimant's answers with confidential finder details.
   - Approve the claim.
   - Navigate to **Ready for Handover** and click **Supervise**. Enter proof reference `Verified Student ID: 21BCE1180` and confirm handover.
   - Verify that item status is now `Returned` and custody is officially released.

7. **Campus Administration & Audit Trail:**
   - Log in as `campusadmin@campus.edu` / `Admin@12345`.
   - View system recovery rate percentage, category distribution, and location incident density.
   - Review **Retention Policy** and **Unclaimed Items**.
   - Inspect **System Audit Trail** showing timestamps, actors, and actions for every stage of the lifecycle.
