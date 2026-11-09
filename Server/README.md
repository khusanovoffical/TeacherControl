# SchoolControl Server

ASP.NET Core 8 Web API + SQLite backend for the TeacherControl and Student Windows desktop apps.

## Included
- Health endpoint: `GET /health`
- Student/device registration: `POST /api/students/register` (first name, surname, stable installation ID and Windows device name)
- Teacher student/device list: `GET /api/teacher/students` (requires `X-Teacher-Key`)
- Teacher dashboard counts: `GET /api/teacher/dashboard` (requires `X-Teacher-Key`)
- SQLite persistence and IP-based registration rate limit
- Safe startup upgrade for added device columns in existing SQLite databases
- Swagger UI in Development only

## Run locally (Windows / .NET 8 SDK)

From this folder:

```powershell
dotnet restore
$env:SCHOOLCONTROL_TEACHER_API_KEY = "replace-with-a-long-random-secret"
dotnet run --urls "http://0.0.0.0:5080"
```

Keep the teacher key private. Never commit it or real student information. For production, use HTTPS, individual teacher accounts, role-based authorization, school-approved device enrollment and secure secret storage.

## Student registration example

```http
POST /api/students/register
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Valiyev",
  "deviceId": "stable-installation-id",
  "deviceName": "LAB-PC-01"
}
```

The same non-empty device ID updates its existing profile on later launches. The API stores the last registration time for basic device setup troubleshooting; it is not an attendance record or a continuous online-presence signal.

## Teacher-only list

```http
GET /api/teacher/students
X-Teacher-Key: <the-secret-set-in-your-environment>
```

## Deployment notes
- The registration endpoint is rate-limited but currently does not require an enrollment code. Do not expose it to the public internet until school-approved enrollment and transport security are implemented.
- CORS origins must be restricted to the actual app origins before deployment.
- Attendance tracking and messaging are intentionally out of scope.
- Screen previews/broadcast, file transfer and managed device actions are not implemented yet. Future screen viewing must be visible to students, session-scoped, teacher-authorized and governed by the school's disclosed policy.
- The app does not implement hidden monitoring, stealth persistence or covert remote control.
