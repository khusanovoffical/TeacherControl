# TeacherControl · xumo.dev

Windows 10/11 teacher desktop client for the SchoolControl school computer-lab platform.

## Current working features
- Modern WPF teacher dashboard.
- Loads registered student names from the SchoolControl Server.
- Search by first name or surname.
- Shows explicit server connection and error states.
- Does not fabricate online/device status.
- Teacher navigation is organized around students, screens, files and managed device controls.
- Attendance tracking and messaging are intentionally excluded from the product scope.

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
Next implementation stages: authenticated classroom sessions; visible, session-scoped screen previews and teacher screen broadcast; file distribution and collection; and authorized device administration (lock, restart/shutdown, and app allow/block policies). These controls must be restricted to school-managed PCs, protected by teacher/admin permissions, and recorded in an audit log. Screen viewing/sharing must show a clear student-side indicator and follow the school's disclosed policy. Attendance tracking and messaging are out of scope. No hidden monitoring, stealth persistence, or covert remote control is implemented.
