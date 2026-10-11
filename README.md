<div align="center">

# 🔐 NeX_Drop
### A Windows Endpoint Control & Policy Enforcement Prototype

**A Windows desktop shell with remote mode synchronization, endpoint monitoring, and administrative control concepts.**

<br>

![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![Language](https://img.shields.io/badge/Language-C%23-512BD4?style=for-the-badge&logo=csharp&logoColor=white)
![Framework](https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![UI](https://img.shields.io/badge/UI-WPF-0B7285?style=for-the-badge)
![Status](https://img.shields.io/badge/Project-Prototype-orange?style=for-the-badge)

</div>

---

## 🧭 Overview

**NeX_Drop** is a Windows endpoint-management prototype built with C#, .NET 8, and Windows Presentation Foundation (WPF). It explores how a managed Windows device can present a dedicated shell, receive a remotely configured operating mode, report device heartbeat status, and record operational events.

The repository separates the desktop experience, Windows background service, and shared data models into distinct projects. Firebase Realtime Database is used by the current implementation for selected synchronization and device-state features.

> **Important:** This project includes system-level behavior such as Windows shell registry changes, process enforcement, keyboard-hook handling, and Task Manager policy changes. It is intended for authorized development and testing in a disposable Windows virtual machine. Do not install it on a primary, personal, or production computer without a complete security review and tested recovery plan.

## ✨ Core Capabilities

- **Dedicated Windows shell** — WPF-based desktop interface and theme resources.
- **Remote mode synchronization** — listens for the device's active mode from Firebase.
- **Device heartbeat and connectivity status** — periodically reports device status and detects missed heartbeats.
- **Process policy enforcement** — checks running processes against mode-specific allowlists and a blacklist.
- **Shell watchdog** — checks whether the NeX_Drop shell is running and attempts to restart it.
- **Registry policy enforcement** — reapplies the configured shell value and includes Task Manager policy handling.
- **Remote application delivery prototype** — listens for a remote application link and downloads the referenced file.
- **Operational event logging** — writes structured events to a local log and, where configured, the Windows Application Event Log.
- **Mode history** — reads and displays recent device mode changes.

Capabilities depend on configuration and the current implementation; they should not be treated as production-ready security guarantees.

## 🏗️ Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                         NeX_Drop                             │
└──────────────────────────────┬──────────────────────────────┘
                               │
             ┌─────────────────┼──────────────────┐
             ▼                 ▼                  ▼
┌────────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  NeX_Drop.Shell    │ │ NeX_Drop.Service  │ │ NeX_Drop.Common  │
│                    │ │                  │ │                  │
│ WPF interface      │ │ Process monitor   │ │ Device state     │
│ View models        │ │ Shell watchdog    │ │ Mode history     │
│ Theme resources    │ │ Registry policy   │ │ Remote app model │
│ Firebase services  │ │ Keyboard hook     │ │ Shared contracts │
└─────────┬──────────┘ └─────────┬────────┘ └──────────────────┘
          │                      │
          └───────────┬──────────┘
                      ▼
          ┌─────────────────────────────┐
          │ Firebase Realtime Database  │
          │ Mode / heartbeat / history  │
          │ Remote application metadata │
          └─────────────────────────────┘
```

## 🧰 Technology Stack

| Technology | Role |
|---|---|
| C# | Application and service logic |
| .NET 8 | Runtime and project framework |
| WPF / XAML | Windows desktop UI and theming |
| Windows Worker Service hosting | Background service lifecycle |
| Firebase Realtime Database | Device state and mode synchronization |
| `FirebaseDatabase.net` | Firebase client integration |
| PowerShell | Installation and removal scripts |
| Windows Registry APIs | Shell and policy configuration |

## 📁 Repository Structure

```text
NeX_Drop_v2/
├── NeX_Drop.sln
├── install.ps1
├── uninstall.ps1
├── NeX_Drop.Shell/
│   ├── Views/
│   ├── ViewModels/
│   ├── Services/
│   ├── Themes/
│   └── App.xaml
├── NeX_Drop.Service/
│   ├── Hooks/
│   ├── Monitors/
│   ├── Registry/
│   └── Program.cs
├── NeX_Drop.Common/
│   └── Models/
└── docs/
    └── firebase_security_rules.json
```

## ⚙️ Prerequisites

- Windows 10/11 (x64) test environment
- Visual Studio 2022 with the **.NET desktop development** workload, or the .NET 8 SDK
- Git (optional, for cloning)
- A dedicated test VM and a snapshot taken before testing
- A Firebase project configured with restrictive Realtime Database rules, if cloud synchronization is being tested

## 🚀 Build

Clone the repository and move into the project directory:

```powershell
git clone <YOUR_GITHUB_REPOSITORY_URL>
cd NeX_Drop_v2
```

Restore dependencies and build the solution:

```powershell
dotnet restore .\NeX_Drop.sln
dotnet build .\NeX_Drop.sln -c Release
```

The project targets `net8.0-windows` and uses WPF. Build it on Windows with the required .NET SDK and Windows targeting support.

## 🧪 Safe Testing Guidance

1. Create a Windows virtual machine specifically for testing.
2. Take a VM snapshot before making system-level changes.
3. Review the service, keyboard hook, process rules, registry modifications, and installer scripts before execution.
4. Configure Firebase authentication and database rules before enabling cloud features.
5. Use a test device ID and non-production Firebase data.
6. Test installation, normal operation, failure scenarios, and recovery inside the VM.
7. Confirm that the standard Windows shell can be restored before considering any broader deployment.

### ⚠️ Installation warning

The supplied `install.ps1` is not a harmless application installer. It requests administrator privileges, publishes and copies binaries, registers a Windows service, and changes the Winlogon `Shell` registry value. The service also includes process enforcement and system policy behavior. Incorrect configuration can interrupt normal desktop access or terminate applications.

**Do not run the installer on your everyday computer.** Review the script first and use only a disposable, recoverable test environment. The uninstall script is provided as a recovery aid, but it should not be assumed to recover every possible failure.

## 🔒 Security & Configuration Notes

Before using this project beyond local experimentation:

- Replace placeholder Firebase credentials with a secure configuration flow; never commit real credentials, tokens, or service-account keys.
- Apply Firebase Realtime Database rules that deny unauthenticated access and enforce least privilege for each device and administrator.
- Review every database read/write path and confirm that the Windows client cannot modify administrator-controlled state.
- Validate remote download URLs against an explicit trusted-host allowlist.
- Verify downloaded executables using a trusted signature or hash before allowing them to be used. Downloading a file must not automatically mean it is trusted.
- Add integrity checks, robust error handling, and auditable recovery behavior.
- Review keyboard-hook behavior for necessity, transparency, and privacy before enabling it.
- Test process allowlists carefully to avoid terminating essential Windows or accessibility processes.
- Do not publish secrets, personal device identifiers, internal endpoints, logs, or sensitive screenshots in the public repository.

The included Firebase URL and sample identifiers are implementation details, not proof that the database is securely configured. Check the Firebase Console rules and credentials before connecting a real device.

## 🗺️ Roadmap

- [ ] Move all environment-specific values into safe configuration
- [ ] Harden Firebase Authentication and database access rules
- [ ] Validate and verify remote application downloads
- [ ] Add explicit administrator consent and recovery safeguards
- [ ] Add automated tests for mode changes, heartbeat handling, and policy enforcement
- [ ] Document expected process policies and recovery scenarios
- [ ] Add sanitized screenshots of the UI and test results

## 🎓 Learning Outcomes

This project provides hands-on experience with:

- Windows desktop development using WPF and XAML
- Background services and process monitoring
- Windows Registry interaction and system configuration
- Cloud-backed state synchronization
- Heartbeat-based connectivity monitoring
- Structured event logging and troubleshooting
- Separation of UI, service logic, and shared models
- Security reviews for privileged Windows applications

## 📸 Screenshots

Add screenshots captured from your own test environment here. Keep them sanitized and avoid exposing credentials, device identifiers, tokens, or private infrastructure.

```text
screenshots/
├── dashboard.png
├── mode-status.png
└── event-logs.png
```

## 👤 Author

**M. Thangamani**  
Cybersecurity-focused developer | Windows Core Logic

This repository is a learning and prototyping project. Update this section with your preferred GitHub, LinkedIn, and team acknowledgements before publishing.

## ⚖️ Responsible Use

NeX_Drop is intended for transparent, authorized endpoint-management experimentation. Run it only on systems you own or are explicitly authorized to administer. You are responsible for reviewing the code, understanding its privileged behavior, and maintaining a tested recovery path.

---

<div align="center">

**Built to explore Windows endpoint control, cloud synchronization, and defensive systems engineering.**

</div>
