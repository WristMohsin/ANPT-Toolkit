# ANPT Toolkit

**Automated Network Penetration Testing Toolkit**

Professional Windows desktop application for authorized network security assessments.

[![Build and Test](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml)

---

## Project Overview

ANPT Toolkit is a Senior Design / Final Year Project that provides a modern, professional Windows desktop interface for conducting authorized network penetration tests.

> **Security Notice**  
> This toolkit is intended **only** for systems you own or have explicit written authorization to assess. Unauthorized scanning is illegal.

## Current Status — Phase 2 (Authentication)

| Component | Status |
|-----------|--------|
| Phase 1 foundation | Done |
| Login window | Done |
| Password hashing (PBKDF2-SHA256) | Done |
| Authentication service | Done |
| Session / current user | Done |
| Roles (Admin / Analyst / Viewer) | Done |
| Logout | Done |
| Auth unit tests | Done |

### Authentication

**Flow:** Application start → Login → Session → Main shell → Logout returns to Login.

**Roles:** Admin, Analyst, Viewer

**Password security:** PBKDF2-SHA256, unique salt, 210000 iterations, constant-time verify.

**First-run bootstrap:** On empty DB creates admin account. Change password after first login (see DatabaseInitializer).

## Technology Stack

- C# / .NET 8
- WPF
- Entity Framework Core 8 + SQLite
- Serilog
- xUnit
- GitHub Actions (windows-latest)

## Requirements

- Windows 10 / Windows 11
- .NET 8 SDK

## Installation & Running

```bash
git clone https://github.com/WristMohsin/ANPT-Toolkit.git
cd ANPT-Toolkit
dotnet restore
dotnet build
dotnet run --project src/ANPT.UI/ANPT.UI.csproj
```

## Running Tests

```bash
dotnet test
```

## License

MIT License — see LICENSE.
