# Offline setup

## 1. Install first (bring the installers, Windows x64)
- .NET SDK 9.x (dev PC uses 9.0.203)
- .NET 8 ASP.NET Core Runtime (the project targets `net8.0`)
- Node.js v24.12.0 (includes npm 11.6.2)
- SQL Server (Express/Developer/Standard) reachable from the backend, with SSMS or sqlcmd if you want to inspect it
- VS Code (+ `.vsix` extensions: C# Dev Kit, Angular Language Service)

Check: `dotnet --list-runtimes`, `node -v`, `npm -v`.

## 2. Copy the folder
Copy the whole `DataDiode` folder to a short path, e.g. `C:\Dev\DataDiode`
(long paths can break `node_modules`). Copy the folder directly or with 7-Zip.
It contains `node_modules` and `offline-packages/` (includes the SQL Server EF provider).

## 3. Database (SQL Server)
Set the connection string in `server/appsettings.json` (`ConnectionStrings:Default`), e.g.

    Server=localhost;Database=DataDiode;Trusted_Connection=True;TrustServerCertificate=True
    Server=SQLHOST\SQLEXPRESS;Database=DataDiode;User Id=sa;Password=...;TrustServerCertificate=True

The database and all tables are created on first run (the login needs CREATE DATABASE rights, or create an empty
`DataDiode` database first and give the login db_owner). The admin user is seeded when the `Users` table is empty.

Tables: `Users`, `Groups`, and the sign-in / authorization tables `Roles`, `Permissions`, `RolePermissions`, `UserRoles`,
`AuditLogs`. At every start the server runs `database/Add_External_Auth.sql`, which is safe to run again and **upgrades an
older database in place** (adds the missing tables and the new `Users` columns `Email`, `FullName`, `IsActive`, `ExternalId`,
`CreatedAt`, creates the `Admin` / `User` roles and the permission catalogue, and turns the old `Users.IsAdmin` flag into a role).
You can also run it yourself:

    sqlcmd -S localhost -d DataDiode -E -C -I -f 65001 -i database\Add_External_Auth.sql

## 4. Backend
Package sources are restricted to `offline-packages/` by `nuget.config` (project root).

    cd server
    dotnet build
    dotnet run --launch-profile http      # http://localhost:5233, Swagger at /swagger

## 5. Frontend
`node_modules` is included, so no install is needed.

    cd client
    npm start                             # http://localhost:4200

Login: admin / Admin@123 (seeded on first run when the Users table is empty).

## 6. Sign-in, roles and permissions (same approach as the Learny app)
Two ways in, side by side (`server/Authentication/DashboardAuthExtensions.cs`):
- **DataDiode's own login** (username or e-mail + password -> JWT signed with `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`).
- **The company dashboard** (`Auth:Enabled = true`): the dashboard's JWT is read from its cookie (`Auth:CookieName`, default
  `learny_token`; JSON `{AccessToken, User}` or a bare JWT), verified with `Auth:Jwt:Key/Issuer/Audience`, and must still be a live row
  in the dashboard's `AccessTokenSessions` table (`ConnectionStrings:ExternalToken`). The dashboard's opaque refresh cookie
  (`Auth:RefreshCookieName`) is looked up in the same table. `ConnectionStrings:ExternalAuth` (the dashboard `User` table) is optional
  and only fills in a missing name / e-mail. Put your own verification into `DashboardTokenValidation` if the dashboard signs differently.
  With dashboard mode on, the own password login is closed. Every state-changing request from the page carries `X-DataDiode-Request: 1`
  (cookie CSRF guard).

A dashboard user that DataDiode has never seen is created automatically (`Auth:AutoProvision`) **with no role**, so they see "No access
yet" until an administrator assigns a role in **Users & roles**. Permissions are always read from the database, never from the token:

| Permission | Allows |
|---|---|
| `files.view` / `files.download` / `files.delete` | the user's own folder |
| `users.manage` | users and groups |
| `roles.manage` / `userroles.manage` | roles + their permissions / assigning roles to users |
| `audit.view` | the audit log |

`Admin` has all of them, `User` has the three file permissions; new roles are created in **Roles & permissions**. The server refuses to
leave nobody able to assign roles. Refused calls (401/403), sign-ins and every user / group / role / file change are written to `AuditLogs`.

Front end (`client/src/app/core/config/api.config.ts`): served by the API itself or through the dashboard on the same address, the API
is `api` next to the page; `ng serve` (4200) talks to `:5233`. Set `DASHBOARD_URL` / `AUTH_API_URL` there (or `Auth:DashboardUrl` on the
server) to send signed-out users to the dashboard and renew an expired session silently. To serve the built front end from the API,
copy `client/dist/client/browser` to `server/wwwroot`.

## Notes
- Set Jwt:Key in `server/appsettings.json` and change the admin password.
- Adding a NuGet/npm package needs internet. Download it (and its dependencies) beforehand, then
  put NuGet packages in `offline-packages/` (`dotnet restore server --packages offline-packages` with internet).
- `node_modules` contains win32-x64 native binaries: the offline PC must be Windows x64.
- User folders set by the admin must already exist (created/shared by the domain admin); the app only checks they exist.
  Use a full path: local `D:\Incoming\alice` or UNC `\\server\share\My Folder` (spaces are fine, no quotes needed).
  The account running the backend needs read/write/delete rights on those folders. Avoid mapped drive letters.
- Back up the SQL Server database regularly. `server/app.db` is the old SQLite file and is no longer used.
- An already-created database from an older build is upgraded automatically at start-up (see section 3); nothing needs to be
  dropped or added by hand. (If the start-up upgrade is blocked by a restricted SQL login, run `database/Add_External_Auth.sql` once
  as a user with ALTER / CREATE TABLE rights.)
