# TeacherControl · xumo.dev

Windows desktop application for authorized school staff.

## Current foundation
- .NET 8 WPF desktop shell with a responsive dark dashboard.
- Student roster table, local name search, refresh action and explicit empty state.
- No fabricated online counts, attendance records, or pretend remote-control buttons.

## Planned connected features
1. Authenticated school server and role-based teacher accounts.
2. Student registration and device enrollment with clear on-screen notice.
3. Live online/offline presence and attendance history.
4. Teacher-to-student announcements and messaging.
5. Permission-based classroom tools, with visible student notification and audit logs.

## Privacy and safety
Screen viewing, remote input, app restrictions, file collection and device power actions are **not** enabled in this starter. Any future classroom-management capability must be authorized by the school, visible to the student, limited to managed school devices, protected by role permissions, and recorded in an audit log. No hidden monitoring or tamper-resistant stealth installation.

## Build
Open `TeacherControl.csproj` in Visual Studio 2022 with the .NET desktop development workload, then build and run.
