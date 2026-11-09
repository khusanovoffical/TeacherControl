# SchoolControl Server

ASP.NET Core 8 Web API + SQLite. This is the first backend foundation for the TeacherControl and Student desktop apps.

## Included
- Health endpoint: `GET /health`
- Student registration: `POST /api/students/register` (first name and last name only)
- Teacher student list: `GET /api/teacher/students` (requires `X-Teacher-Key`)
- Teacher dashboard counts: `GET /api/teacher/dashboard` (requires `X-Teacher-Key`)
- SQLite persistence and a basic registration rate limit
- Swagger UI in Development only

## Run locally (Windows / .NET 8 SDK)

From this folder:

```powershell
dotnet restore
$env:SCHOOLCONTROL_TEACHER_API_KEY = "replace-with-a-long-random-secret"
dotnet run
```

Open the local URL printed by `dotnet run`; in Development, add `/swagger`.

## API examples

Student registration:

```http
POST /api/students/register
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Valiyev"
}
```

Teacher-only list:

```http
GET /api/teacher/students
X-Teacher-Key: <the-secret-set-in-your-environment>
```

## Important setup notes
- Never commit the teacher API key or any real student information to GitHub.
- The public registration endpoint stores only the supplied name, surname, generated ID and creation timestamp. Add school-approved identity verification and a privacy notice before real deployment.
- Teacher endpoints remain unavailable until `SCHOOLCONTROL_TEACHER_API_KEY` is configured.
- CORS origins must be restricted to the actual app origins before deployment.
- Online presence, attendance, messaging and screen sharing are not implemented yet. Screen sharing must be explicitly initiated by the student and authorized by school policy.
- This starter uses `EnsureCreated` for a simple first run. Use EF Core migrations before production schema changes.
