# ANPT Toolkit

**Automated Network Penetration Testing Toolkit**

Professional Windows desktop application for authorized network security assessments.

[![Build and Test](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml)

---

## Project Overview

ANPT Toolkit is a Senior Design / Final Year Project that provides a modern, professional Windows desktop interface for conducting authorized network penetration tests.

> **Security Notice**  
> This toolkit is intended **only** for systems you own or have explicit written authorization to assess. Unauthorized scanning is illegal.

## Current Status — Phase 5B (Real Nmap Scan Execution & Lifecycle)

| Component | Status |
|-----------|--------|
| Phase 1 foundation | Done |
| Phase 2 authentication | Done |
| Phase 3 target management | Done |
| Phase 4 scan management | Done |
| Phase 5A safe Nmap runner | Done |
| Phase 5B real execution & lifecycle | Done |
| XML parsing / hosts / findings | Not started |

### Targets (Phase 3)

- Create, edit, search, and archive assessment targets.
- **AuthorizationConfirmed** must be set before a target is eligible for scans.

### Scans (Phase 4)

- Create scans against **eligible** targets only (Active + AuthorizationConfirmed).
- Select a built-in **ScanProfile**.
- Scans are created as **Queued**.

### Execution (Phase 5A + 5B)

- Safe **Nmap process runner** (no shell, `ProcessStartInfo.ArgumentList` only).
- **Authorization re-validated** immediately before every start.
- Allow-listed arguments from ScanProfile flags only (no raw user switches, no NSE vuln scripts).
- **Start** from Scans view runs real Nmap against the authorized target.
- Lifecycle: `Queued → Running → Completed` | `Failed` | `Cancelled`.
- **Completed** only when Nmap exits with code 0.
- Cancellation terminates the Nmap process tree (no orphans).
- Controlled unique XML output under application `ScanOutput/` directory.
- Process tracking prevents duplicate starts and cross-scan cancellation.

**Still NOT implemented:** Nmap XML parsing, Hosts/Ports/Services persistence, Findings, Vulnerability Analysis, CVE matching, Reporting, Scheduling.

## Technology Stack

- C# / .NET 8 · WPF · EF Core 8 + SQLite · Serilog · xUnit · GitHub Actions

## Requirements

- Windows 10 / Windows 11
- .NET 8 SDK (for development)
- Nmap installed on PATH or at a standard location (optional `Config/nmap.path` file)

## Installation & Running

```bash
git clone https://github.com/WristMohsin/ANPT-Toolkit.git
cd ANPT-Toolkit
dotnet restore
dotnet build
dotnet run --project src/ANPT.UI/ANPT.UI.csproj
```

Or download the **ANPT-Toolkit-Windows** artifact from the latest successful Actions run.

**Bootstrap login:** `admin` / `Admin@ChangeMe1` (change after first login).

## Running Tests

```bash
dotnet test
```

Unit tests do **not** require Nmap to be installed (process runner is faked).

## License

MIT License — see LICENSE.
