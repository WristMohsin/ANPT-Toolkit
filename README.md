# ANPT Toolkit

**Automated Network Penetration Testing Toolkit**

Professional Windows desktop application for authorized network security assessments.

[![Build and Test](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/WristMohsin/ANPT-Toolkit/actions/workflows/build.yml)

---

## Project Overview

ANPT Toolkit is a Senior Design / Final Year Project that provides a modern, professional Windows desktop interface for conducting authorized network penetration tests.

> **Security Notice**  
> This toolkit is intended **only** for systems you own or have explicit written authorization to assess. Unauthorized scanning is illegal.

## Current Status — Phase 5F (Findings Enrichment)

| Component | Status |
|-----------|--------|
| Phase 1 foundation | Done |
| Phase 2 authentication | Done |
| Phase 3 target management | Done |
| Phase 4 scan management | Done |
| Phase 5A safe Nmap runner | Done |
| Phase 5B real execution & lifecycle | Done |
| Phase 5C Nmap XML parsing foundation | Done |
| Phase 5D parsed results persistence | Done |
| Phase 5E security analysis / findings foundation | Done |
| Phase 5F finding enrichment, risk context, reporting foundation | Done |
| CVE enrichment / exploit verification | Not started |
| Advanced PDF reporting | Not started |

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

### Parsing (Phase 5C)

- Secure **Nmap XML parser** (`INmapXmlParser`) using `System.Xml.Linq` with DTD/external entity resolution **disabled**.
- Typed in-memory models: scan metadata, hosts, addresses, hostnames, ports, services, runstats.
- Controlled **XML file reader** (`INmapXmlResultReader`) restricted to the application `ScanOutput/` directory.

### Persistence (Phase 5D)

- End-to-end after exit code 0: **XML → INmapXmlResultReader → INmapXmlParser → INmapResultPersistenceService → SQLite**.
- **Transactional** persistence under the owning Scan.
- Relationships: `Scan → Host → Address / Hostname / Service (port)`.
- Hosts support IPv4, IPv6, MAC, and multiple hostnames; ports may exist without service details.
- **Reprocessing** replaces prior result rows for that Scan only.

### Findings & Analysis (Phase 5E–5F)

- Deterministic analysis rules on **persisted** Nmap data only (no re-scanning, no network I/O from rules).
- Rules: `OPEN-SERVICE-EXPOSURE`, `SERVICE-WITHOUT-IDENTIFICATION`, `SENSITIVE-SERVICE-EXPOSURE`.
- Findings include title, description, evidence, impact, recommendation, RuleId, severity, and status.
- **Risk context**: priority and risk label derived deterministically from severity (no fake CVSS/CVE).
- Findings UI: search, severity/status/rule filters, professional details window, dashboard summary counts.
- Scan details shows per-scan finding summary; **assessment report** read model (`AssessmentReport`) for future reporting.
- Analysis is **idempotent** and restricted to **Completed** scans.
- Explicit **Analyze** action on Scans view.

**Still NOT implemented:** CVE enrichment, exploit verification, credential attacks, brute force, destructive testing, advanced PDF reporting, scheduling, Hosts/Services management UI.

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
