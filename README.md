# TeacherControl · xumo.dev

Windows 10/11 teacher desktop client for the SchoolControl school computer-lab platform.

## Current working features
- Modern WPF teacher dashboard.
- Loads registered student names from the SchoolControl Server.
- Search by first name or surname.
- Shows explicit server connection and error states.
- Does not fabricate online or attendance values.

## Configure
Set these environment variables on the teacher PC before launching:

```powershell
$env:SCHOOLCONTROL_API_URL = "http://SCHOOL-SERVER-IP:5080"
$env:SCHOOLCONTROL_TEACHER_API_KEY = "the-same-secret-configured-on-server"
dotnet run
```

The API key must be kept private and must never be committed to this repository. For production, replace the shared key with individual teacher accounts and role-based authentication.

## Build
Open `TeacherControl.csproj` in Visual Studio 2022 with the .NET desktop development workload, or run:

```powershell
dotnet restore
dotnet build -c Release
```

## Roadmap
Live device presence, attendance events, class messaging, file distribution/collection and classroom screen sharing are separate modules. Screen sharing should be visible to the student, initiated for a specific class session, limited to school-managed PCs and logged. This repository does not implement hidden monitoring, stealth persistence, or covert remote control.
