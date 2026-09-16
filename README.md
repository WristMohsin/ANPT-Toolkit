# ANPT Toolkit

**Automated Network Penetration Testing Toolkit**

Professional Windows desktop application for authorized network security assessments.

[![Build and Test](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml)

---

## Project Overview

ANPT Toolkit is a Senior Design / Final Year Project that provides a modern, professional Windows desktop interface for conducting authorized network penetration tests.

> **Security Notice**  
> This toolkit is intended **only** for systems you own or have explicit written authorization to assess. Unauthorized scanning is illegal.

## Current Status — Phase 3 (Target Management)

| Component | Status |
|-----------|--------|
| Phase 1 foundation | Done |
| Phase 2 authentication | Done |
| Target list / search / filter | Done |
| Create / edit targets | Done |
| Archive targets | Done |
| Authorization confirmation | Done |
| Target repository + service | Done |
| Dashboard real Target count | Done |
| Target unit tests | Done |

### Authentication (Phase 2)

**Flow:** Application start → Login → Session → Main shell → Logout returns to Login.

**Roles:** Admin, Analyst, Viewer

**Password security:** PBKDF2-SHA256, unique salt, 210000 iterations, constant-time verify.

**First-run bootstrap:** On empty DB creates admin account (`admin` / `Admin@ChangeMe1`). Change password after first login.

### Targets (Phase 3)

- Create, edit, search, and archive assessment targets.
- **AuthorizationConfirmed** must be explicitly set by the operator before a target is eligible for future scans.
- The application only records the user’s confirmation; it does not verify ownership or legal authorization.
- Dashboard “Total Targets” is loaded from the database.

**Out of scope (future phases):** actual network scanning, hosts, services, findings, reports, scan profiles UI, logs UI, settings UI.

## Technology Stack

- C# / .NET 8
- WPF
- Entity Framework Core 8 + SQLite
- Serilog
- xUnit
- GitHub Actions (windows-latest)

## Requirements

- Windows 10 / Windows 11
- .NET 8 SDK (for development)

## Installation & Running

```bash
git clone https://github.com/WristMohsin/ANPT-Toolkit.git
cd ANPT-Toolkit
dotnet restore
dotnet build
dotnet run --project src/ANPT.UI/ANPT.UI.csproj
```

Or download the self-contained **ANPT-Toolkit-Windows** artifact from the latest successful GitHub Actions run.

## Running Tests

```bash
dotnet test
```

## License

MIT License — see LICENSE.
