# TeacherControl · xumo.dev

Windows 10/11 teacher desktop client and ASP.NET Core API for the SchoolControl school computer-lab platform.

## Current working foundation
- Modern WPF teacher dashboard with student and registered-device list.
- Search by student name, surname, and Windows device name.
- API registration stores a stable device ID and Windows computer name.
- Repeated launches from the same configured Student installation update its existing record instead of creating a new student row.
- Existing SQLite databases are upgraded with missing device columns at API startup.
- Explicit server connection and error states.
- No attendance tracking or messaging features.

## Configure the teacher PC
Set these environment variables before launching:

```powershell
$env:SCHOOLCONTROL_API_URL = "http://SCHOOL-SERVER-IP:5080"
$env:SCHOOLCONTROL_TEACHER_API_KEY = "your-private-development-key"
dotnet run --project TeacherControl.csproj
```

The API key must remain private and must never be committed. For production, replace the shared development key with individual teacher accounts, role-based permissions, HTTPS and secure secret storage.

## Run the API
From the repository root:

```powershell
$env:SCHOOLCONTROL_TEACHER_API_KEY = "your-private-development-key"
dotnet run --project Server/SchoolControl.Server.csproj --urls "http://0.0.0.0:5080"
```

Student PCs must be configured by authorized school IT staff to use the server address. Do not use localhost on student PCs unless the API is running on that same PC.

## Build
Open `TeacherControl.csproj` in Visual Studio 2022 with the .NET desktop development workload, or run:

```powershell
dotnet restore TeacherControl.csproj
dotnet build TeacherControl.csproj -c Release
dotnet build Server/SchoolControl.Server.csproj -c Release
```

A GitHub Actions workflow builds the teacher app and API on Windows for each push and pull request.

## Next implementation stages
1. Teacher/admin authentication and device enrollment approval.
2. Authorized classroom sessions with visible student-side indicators.
3. Session-scoped screen previews and teacher screen broadcast.
4. Managed file distribution and collection.
5. Authorized lock, restart/shutdown and application allow/block policies with audit records.

All remote controls must be limited to school-managed PCs, authorized teacher/admin roles and a clearly identified class session. Screen viewing/sharing must be visible to students and governed by the school's disclosed policy. No hidden monitoring, stealth persistence or covert remote control is implemented. Attendance and messaging are out of scope.
