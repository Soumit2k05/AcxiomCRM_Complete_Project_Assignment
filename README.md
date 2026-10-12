# AcxiomCRM

AcxiomCRM is a complete, enterprise-grade Customer Relationship Management (CRM) web application engineered with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core (SQLite)**, **ASP.NET Core Identity**, **Bootstrap 5**, and **Chart.js**.

The solution implements every functional module, business rule, role-based authorization model, security condition, and acceptance criterion defined in the AcxiomCRM assignment specification with a clean, startup-grade UI.

---

## 🏗️ Architecture & Modules

```
AcxiomCRM/
├── Controllers/
│   ├── AccountController.cs            # Login, Registration, Logout, Lockout & Audit handling
│   ├── HomeController.cs               # Role-scoped Executive Dashboard & Chart.js Visualizations
│   ├── CustomersController.cs          # Customer CRUD, Uniqueness checks & Deactivation
│   ├── LeadsController.cs              # Lead CRUD, Status Workflow & Transactional Conversion
│   ├── OpportunitiesController.cs      # Sales Pipeline Deals & Weighted Valuation
│   ├── FollowUpsController.cs          # Activities Scheduling, Overdue Detection & Status Updates
│   ├── ActivitiesController.cs         # Customer/Lead Interaction Log (Calls, Meetings, Emails, Tasks)
│   ├── UsersController.cs              # Admin: User Management, Roles, Lockout & Password Resets
│   ├── AuditLogsController.cs          # Admin/Manager: Append-only Forensic Audit Log
│   ├── ReportsController.cs            # Pipeline, Customer, Conversion, Follow-Up & Activity Reports + CSV
│   └── Api/
│       ├── AuthController.cs           # POST /api/auth/login, POST /api/auth/logout
│       ├── CustomersController.cs      # GET, POST, PUT, DELETE /api/customers
│       ├── LeadsController.cs          # GET, POST, PUT, DELETE /api/leads
│       ├── OpportunitiesController.cs  # GET, POST, PUT /api/opportunities
│       ├── FollowUpsController.cs      # GET, POST /api/followups
│       └── ReportsApiController.cs     # GET /api/reports/pipeline
├── Data/
│   ├── ApplicationDbContext.cs         # Relational schema, indexes & foreign keys
│   └── DbInitializer.cs                # Schema migrations, Roles, Default users & Seed data
├── DTOs/
│   ├── CustomerDto.cs                  # Safe REST API Data Transfer Objects
│   ├── LeadDto.cs
│   ├── OpportunityDto.cs
│   ├── AdditionalDtos.cs               # FollowUpDto, PipelineReportDto, ApiAuth DTOs
│   └── ApiResponse.cs                  # Standardized JSON response contract
├── Migrations/                         # EF Core SQLite database migrations
├── Models/
│   ├── ApplicationUser.cs              # IdentityUser extension
│   ├── ApplicationRole.cs              # IdentityRole extension
│   ├── Customer.cs                     # Customer master entity
│   ├── Lead.cs                         # Lead lifecycle entity & status workflow
│   ├── Opportunity.cs                  # Sales opportunities with weighted pipeline derivation
│   ├── FollowUp.cs                     # Follow-up scheduling & status tracking
│   ├── Activity.cs                     # Touchpoints (Calls, Meetings, Emails, Tasks)
│   ├── AuditLog.cs                     # Append-only security & mutation logs
│   └── ErrorViewModel.cs
├── Services/
│   ├── IAuditService.cs & AuditService.cs
│   ├── ILeadService.cs & LeadService.cs
│   ├── IOpportunityService.cs & OpportunityService.cs
│   └── IFollowUpService.cs & FollowUpService.cs
├── ViewModels/
│   ├── DashboardViewModel.cs
│   ├── LoginViewModel.cs
│   ├── RegisterViewModel.cs
│   └── UserViewModels.cs
├── Views/
│   ├── Account/ (Login, Register, AccessDenied)
│   ├── Home/ (Dashboard with 8 KPIs & 3 Chart.js graphs, Privacy)
│   ├── Customers/ (Index, Details, Create, Edit, Delete)
│   ├── Leads/ (Index, Details, Create, Edit, Convert, Delete)
│   ├── Opportunities/ (Index, Details, Create, Edit, Delete, Pipeline Board)
│   ├── FollowUps/ (Index, Schedule, Delete)
│   ├── Activities/ (Index, Create)
│   ├── Users/ (Index, Create, Edit, ResetPassword)
│   ├── AuditLogs/ (Index, Details)
│   ├── Reports/ (Index, PipelineReport, CustomerReport, ConversionReport, FollowUpReport, UserActivityReport)
│   └── Shared/ (_Layout, _LoginPartial, _ValidationScriptsPartial)
├── wwwroot/
│   ├── css/site.css                    # Clean, Instagram-inspired UI theme
│   ├── js/chart.umd.min.js             # Local Chart.js bundle
│   └── lib/bootstrap/ & bootstrap-icons/
├── AcxiomCRM.Tests/                    # xUnit Test Project verifying business rules
├── AcxiomCRM.csproj
├── appsettings.json
└── .gitignore
```

---

## 🔒 Security & Compliance Implementations

1. **Framework-Managed Password Hashing**:
   - Securely hashed using ASP.NET Core Identity (PBKDF2 with HMAC-SHA256).
   - Zero plaintext passwords or custom password tables.
2. **Account Lockout Policy**:
   - Accounts are locked for 15 minutes after 5 consecutive failed login attempts.
   - Lockout and unlock events are captured in the append-only audit trail.
3. **Anti-Forgery (CSRF) Protection**:
   - Configured globally with `AutoValidateAntiforgeryTokenAttribute` for all state-changing POST requests.
4. **Role-Based Authorization (RBAC)**:
   - Server-enforced roles: `Admin`, `Manager`, `SalesExecutive`.
   - Data and charts are server-scoped:
     - **Admin**: Full application administration, user/role management, audit logs, all CRM data.
     - **Manager**: Team-level visibility across all customers, leads, opportunities, follow-ups, and reports.
     - **SalesExecutive**: Access strictly constrained to own/assigned records.
5. **Append-Only Audit Logging**:
   - Tamper-protected audit trail logging user, timestamp, action (`Login`, `Failed Login`, `Logout`, `Create`, `Update`, `Delete`, `Role Change`, `Security`, `Conversion`), before/after state, and IP address.
6. **Relational Integrity & Validation**:
   - Customer uniqueness on `CustomerCode`, `Email`, and `Phone`.
   - Opportunity amount > 0 and probability 0–100 enforced.
   - Expected close date cannot be in the past for active opportunities.
   - Follow-up date cannot be in the past for new/planned activities.
   - Lead status transitions enforced according to configured workflow rules.
   - Lead-to-Customer conversion executed inside an atomic database transaction.

---

## 👤 Pre-Configured Test Accounts

The SQLite database (`AcxiomCRM.db`) automatically migrates and seeds 3 role accounts on startup:

| Role | Username / Email | Default Password | Primary Permissions |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiomcrm.local` | `Admin@12345!` | Full system administration, users, audit logs, all records |
| **Manager** | `manager@acxiomcrm.local` | `Manager@12345!` | Team pipeline, deals, follow-ups, management reports |
| **SalesExecutive** | `sales@acxiomcrm.local` | `Sales@12345!` | Assigned customers, leads, opportunities & sales reports |

*Note: The login page includes quick-fill chips to auto-populate any of these credentials with one click.*

---

## 🚀 Running and Testing the Application

### 1. Restore & Build
```powershell
dotnet restore
dotnet build
```

### 2. Run Automated Business Rule Tests
```powershell
dotnet test
```

### 3. Launch the Application
```powershell
dotnet run
```
Open your browser at `https://localhost:5001` or `http://localhost:5000`.

### 4. Database Migrations (Optional / Development)
Database migrations are automatically applied on application launch using `context.Database.MigrateAsync()`. If adding new models:
```powershell
dotnet dotnet-ef migrations add <MigrationName>
```
