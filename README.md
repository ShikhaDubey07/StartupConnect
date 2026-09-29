# 🚀 StartupConnect

> **Connect. Build. Launch.**
> A platform for Indian youth to find co-founders, skilled teammates, and investors.

---

## 📋 Table of Contents

- [About](#about)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Database Setup](#database-setup)
- [Running the Server](#running-the-server)
- [Default Seed Accounts](#default-seed-accounts)
- [User Roles](#user-roles)
- [Key Pages & Routes](#key-pages--routes)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## About

**StartupConnect** is an ASP.NET Core 8 MVC web application built to solve a real problem: Indian youth with great startup ideas struggle to find co-founders, teammates, and early-stage funding.

The platform lets users:
- Submit startup ideas for expert panel review
- Browse and filter approved ideas
- Show interest in joining an idea as a **worker**, **investor**, or both
- Build complete profiles with skills, availability, and investment capacity
- Receive real-time notifications on idea activity

---

## Features

| Feature | Description |
|---|---|
| 🔐 Authentication | Register/Login with ASP.NET Core Identity |
| 📝 Idea Submission | Submit startup ideas with full detail |
| 🔍 Browse & Filter | Search and filter ideas by category, funding range, and sort order |
| ✅ Panel Review | Admin/Panel role approves or rejects submitted ideas |
| 🤝 Show Interest | Express interest as a co-founder, investor, or both |
| 👤 Profile Builder | Skills, availability, investment capacity, LinkedIn/Portfolio links |
| 📊 Dashboard | Personalized stats — your ideas, interests, notifications |
| 🔔 Notifications | In-app alerts when ideas are approved, rejected, or get new interest |
| 🛡️ Admin Panel | Manage pending ideas, view platform stats |
| 📬 Contact Form | Public contact page with database storage |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| Language | C# 12 |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (LocalDB for development) |
| Authentication | ASP.NET Core Identity |
| UI | Bootstrap 5, Bootstrap Icons, Google Fonts |

---

## Project Structure

`
StartupConnect/
├── Controllers/
│   ├── AccountController.cs     # Register, Login, Profile, Dashboard
│   ├── AdminController.cs       # Admin dashboard, idea review
│   ├── HomeController.cs        # Index, HowItWorks, Contact, Privacy
│   ├── IdeasController.cs       # Browse, Submit, Detail, MyIdeas
│   └── InterestsController.cs   # Show interest in ideas
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext
│   └── DbInitializer.cs         # DB migration + seed on startup
├── Models/                      # Domain models
├── Services/                    # Business logic services
├── ViewModels/                  # ViewModels for all views
├── Views/                       # Razor views
├── wwwroot/                     # Static assets (CSS, JS, lib)
├── appsettings.json             # App configuration
└── Program.cs                   # App startup and DI configuration
`

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.x)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or VS Code with C# Dev Kit

Verify your .NET version:

`ash
dotnet --version
# Should output: 8.x.x
`

---

## Getting Started

### 1. Clone / Open the project

`ash
git clone <your-repo-url>
cd StartupConnect
`

### 2. Restore NuGet packages

`ash
dotnet restore
`

### 3. Configure the database connection

Open ppsettings.json and check/update the connection string:

`json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StartupConnectDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
`

> **Using a full SQL Server instance?**
> Replace (localdb)\\mssqllocaldb with your server name.

---

## Database Setup

The application **automatically runs migrations and seeds the database on startup** via DbInitializer. You do **not** need to run dotnet ef database update manually.

On first run, the initializer will:
1. Apply all pending EF Core migrations (creates all tables)
2. Seed 6 categories (Technology, Healthcare, Education, Agriculture, Social Impact, E-Commerce)
3. Create an **Admin** user and a demo **Member** user
4. Seed 3 sample approved startup ideas

> **Manual migration (if ever needed):**
> `ash
> dotnet ef migrations add InitialCreate
> dotnet ef database update
> `

---

## Running the Server

### Option 1: .NET CLI (Recommended)

`ash
cd d:\My_projects\MyNewProject\StartupConnect
dotnet run
`

The app will print the listening URL:

`
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7xxx
      Now listening on: http://localhost:5xxx
`

Open your browser at the https://localhost:7xxx URL shown.

### Option 2: Visual Studio

1. Open StartupConnect.csproj in Visual Studio 2022
2. Select the https launch profile from the run dropdown
3. Press **F5** or click the ▶ Run button

### Option 3: Watch mode (auto-reload on file changes)

`ash
dotnet watch run
`

---

## Default Seed Accounts

On first launch, these accounts are automatically created:

| Role | Email | Password |
|---|---|---|
| **Admin / Panel** | dmin@startupconnect.in | Admin@123 |
| **Member** (demo) | 
ahul@demo.in | Demo@123 |

> ⚠️ Change these credentials before deploying to production!

---

## User Roles

| Role | Permissions |
|---|---|
| Member | Register, submit ideas, browse, show interest, manage profile |
| Investor | All Member permissions + shown as investor on interest forms |
| Panel | Review and approve/reject submitted ideas |
| Admin | Full access — Admin dashboard, all Panel permissions |

---

## Key Pages & Routes

| Route | Description |
|---|---|
| / | Home page — featured ideas, stats, how it works |
| /Home/HowItWorks | Step-by-step guide to the platform |
| /Home/Contact | Public contact form |
| /Account/Register | New user registration |
| /Account/Login | User login |
| /Account/Dashboard | Logged-in user dashboard |
| /Account/Profile | Edit profile, skills, availability |
| /Ideas/Browse | Browse & filter all approved ideas |
| /Ideas/Submit | Submit a new startup idea (auth required) |
| /Ideas/Detail/{id} | Full idea detail + show interest form |
| /Ideas/MyIdeas | View your submitted ideas |
| /Admin/Dashboard | Admin panel — pending ideas, platform stats |

---

## Configuration

### ppsettings.json

`json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."       // SQL Server connection string
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
`

### Links, password reset, CORS and the public API

| Key | Default | Purpose |
|---|---|---|
| `App:BaseUrl` | *(request host)* | Absolute base URL used in email links (set it in production, e.g. `https://startupconnect.in`). |
| `Auth:PasswordResetTokenHours` | `2` | Lifetime of password reset links (1–48). |
| `Cors:AllowedOrigins` | *(empty = CORS off)* | Origins allowed to call the read-only `/api/*` endpoints from a browser (GET only). |
| `RateLimiting:ApiPerMinute` | `60` (Dev 600) | Public API requests per IP per minute. |

Read-only JSON API (anonymous, approved data only, privacy-aware):
`GET /api/categories`, `GET /api/ideas?search=&categoryId=&stage=&page=1&pageSize=20` (max 50), `GET /api/ideas/{id}`.
Founder names appear only for Public profiles (location only if shared); emails/ages are never exposed.

### Identity Password Policy (configured in Program.cs)

| Rule | Value |
|---|---|
| Require Digit | ✅ Yes |
| Require Lowercase | ✅ Yes |
| Require Uppercase | ✅ Yes |
| Minimum Length | 6 characters |
| Require Unique Email | ✅ Yes |

---

## Troubleshooting

### Page keeps loading / App hangs on startup

**Cause:** The database migration or seeding is failing, often because SQL Server LocalDB is not running.

**Fix:**
1. Check that SQL Server LocalDB is installed and running
2. Run sqllocaldb info in a terminal to list instances
3. Start LocalDB if stopped: sqllocaldb start MSSQLLocalDB
4. Check the terminal/console for EF Core error messages

### Build errors: CS0246 - type not found (RegisterViewModel, etc.)

**Cause:** _ViewImports.cshtml was missing @using StartupConnect.ViewModels.

**Fix:** Ensure Views/_ViewImports.cshtml contains:

`cshtml
@using StartupConnect.Models
@using StartupConnect.ViewModels
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
`

### Login redirect loops

**Cause:** Cookie auth redirects to /Account/Login on unauthorized access — this is expected.
Register a new account or use the seed credentials above.

### No migrations found error

`ash
dotnet ef migrations add InitialCreate --project StartupConnect.csproj
`

---

## License

This project is for educational/portfolio purposes. Feel free to adapt it for your own use.

---

*Built with ❤️ for India's next generation of entrepreneurs.*
