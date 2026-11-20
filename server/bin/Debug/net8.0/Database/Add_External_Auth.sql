/* Run with:  sqlcmd -S localhost -d DataDiode -E -C -I -f 65001 -i Add_External_Auth.sql   (-f 65001 = UTF-8, needed for the Arabic names)

   Dashboard sign-in + DataDiode roles, permissions and audit log.
   Safe to run more than once (every step checks first). The server ALSO runs this script by itself at start-up
   (server/SchemaUpgrader.cs), so running it by hand is optional - it is here for DBAs who prefer to see it.

   - Users.Email / FullName / IsActive / ExternalId / CreatedAt : what a dashboard user needs
                            (ExternalId = the dashboard user's GUID; the dashboard identifies people by GUID, DataDiode by the integer Id)
   - Roles                : Admin and User (system roles, cannot be deleted) + any role created in the Roles screen
   - Permissions          : what can be done (integer id + a stable code used by the code and the menu, e.g. 'users.manage')
   - RolePermissions      : which permissions a role has
   - UserRoles            : a user can have many roles
   - AuditLogs            : refused calls, sign-ins, user / role / file changes
   Existing users keep working: Users.IsAdmin is copied into UserRoles (Admin or User). */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------- Users: the new columns (one batch each: a new column cannot be used in the batch that adds it) */
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'Email') IS NULL
    ALTER TABLE dbo.Users ADD Email NVARCHAR(256) NULL;
GO
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'FullName') IS NULL
    ALTER TABLE dbo.Users ADD FullName NVARCHAR(256) NULL;
GO
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'IsActive') IS NULL
    ALTER TABLE dbo.Users ADD IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1;
GO
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'ExternalId') IS NULL
    ALTER TABLE dbo.Users ADD ExternalId UNIQUEIDENTIFIER NULL;
GO
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'CreatedAt') IS NULL
    ALTER TABLE dbo.Users ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME();
GO
SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_ExternalId' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX UX_Users_ExternalId ON dbo.Users (ExternalId) WHERE ExternalId IS NOT NULL;
GO
SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Email' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users (Email) WHERE Email IS NOT NULL;
GO

/* ---------------------------------------------------------------- Roles */
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
CREATE TABLE dbo.Roles (
    RoleId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name        NVARCHAR(80)  NOT NULL,
    NameAr      NVARCHAR(100) NULL,
    Description NVARCHAR(300) NULL,
    IsSystem    BIT NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT 0
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Roles_Name' AND object_id = OBJECT_ID('dbo.Roles'))
    CREATE UNIQUE INDEX UX_Roles_Name ON dbo.Roles (Name);
GO

/* ---------------------------------------------------------------- Permissions */
IF OBJECT_ID('dbo.Permissions', 'U') IS NULL
CREATE TABLE dbo.Permissions (
    PermissionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Code         NVARCHAR(80)  NOT NULL,
    NameAr       NVARCHAR(150) NOT NULL,
    NameEn       NVARCHAR(150) NOT NULL,
    GroupName    NVARCHAR(60)  NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Permissions_Code' AND object_id = OBJECT_ID('dbo.Permissions'))
    CREATE UNIQUE INDEX UX_Permissions_Code ON dbo.Permissions (Code);
GO

/* ---------------------------------------------------------------- RolePermissions */
IF OBJECT_ID('dbo.RolePermissions', 'U') IS NULL
CREATE TABLE dbo.RolePermissions (
    RoleId       INT NOT NULL,
    PermissionId INT NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(PermissionId) ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------------- UserRoles (+ copy of the old Users.IsAdmin flag, once, when the table is created here) */
DECLARE @fresh BIT = 0;
IF OBJECT_ID('dbo.UserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles (
        UserId INT NOT NULL,
        RoleId INT NOT NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId) ON DELETE CASCADE
    );
    SET @fresh = 1;
END
IF @fresh = 1 PRINT N'UserRoles created: the existing users are copied in after the roles exist (below).';
GO

/* ---------------------------------------------------------------- AuditLogs */
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        AuditLogId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        UserId       INT NULL,
        Username     NVARCHAR(200) NULL,
        Action       NVARCHAR(100) NOT NULL,
        EntityType   NVARCHAR(100) NULL,
        EntityId     NVARCHAR(200) NULL,
        Details      NVARCHAR(2000) NULL,
        IpAddress    NVARCHAR(45) NULL,
        UserAgent    NVARCHAR(300) NULL,
        Success      BIT NOT NULL CONSTRAINT DF_AuditLogs_Success DEFAULT 1,
        ErrorMessage NVARCHAR(500) NULL,
        CreatedAt    DATETIME2 NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_CreatedAt' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_CreatedAt ON dbo.AuditLogs (CreatedAt DESC);
GO
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_UserId' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_UserId ON dbo.AuditLogs (UserId);
GO

/* ---------------------------------------------------------------- the system roles */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Admin')
    INSERT INTO dbo.Roles (Name, NameAr, Description, IsSystem) VALUES ('Admin', N'مشرف', N'Full access: users, groups, roles, audit log and files.', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'User')
    INSERT INTO dbo.Roles (Name, NameAr, Description, IsSystem) VALUES ('User', N'مستخدم', N'Sees, downloads and deletes the files of the assigned folder.', 1);
UPDATE dbo.Roles SET IsSystem = 1, NameAr = ISNULL(NameAr, N'مشرف')  WHERE Name = 'Admin' AND (IsSystem = 0 OR NameAr IS NULL);
UPDATE dbo.Roles SET IsSystem = 1, NameAr = ISNULL(NameAr, N'مستخدم') WHERE Name = 'User'  AND (IsSystem = 0 OR NameAr IS NULL);
GO

/* ---------------------------------------------------------------- the permission catalogue (add a row here + use the code in the controller / menu) */
DECLARE @p TABLE (Code NVARCHAR(80), NameAr NVARCHAR(150), NameEn NVARCHAR(150), GroupName NVARCHAR(60));
INSERT INTO @p VALUES
 ('files.view',       N'عرض الملفات',                    'View my files',                     'Files'),
 ('files.download',   N'تنزيل الملفات',                  'Download files',                    'Files'),
 ('files.delete',     N'حذف الملفات',                    'Delete files',                      'Files'),
 ('users.manage',     N'إدارة المستخدمين والمجموعات',    'Manage users and groups',           'People'),
 ('roles.manage',     N'إدارة الأدوار وصلاحياتها',       'Manage roles and their permissions','Security'),
 ('userroles.manage', N'تعيين الأدوار للمستخدمين',       'Assign roles to users',             'Security'),
 ('audit.view',       N'عرض سجل التدقيق',                'View the audit log',                'Security');
INSERT INTO dbo.Permissions (Code, NameAr, NameEn, GroupName)
SELECT s.Code, s.NameAr, s.NameEn, s.GroupName FROM @p s WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions x WHERE x.Code = s.Code);
GO

/* Admin: every permission; User: the file permissions (only added the first time a role has none, so edits made in the Roles screen are never undone) */
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Name = 'Admin' AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions x WHERE x.RoleId = r.RoleId)
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions y WHERE y.RoleId = r.RoleId AND y.PermissionId = p.PermissionId);
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Name = 'User' AND p.GroupName = 'Files' AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions x WHERE x.RoleId = r.RoleId)
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions y WHERE y.RoleId = r.RoleId AND y.PermissionId = p.PermissionId);
GO
/* the Admin role always keeps the two permissions that manage roles (so nobody can lock the system) */
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM dbo.Roles r JOIN dbo.Permissions p ON p.Code IN ('roles.manage', 'userroles.manage')
WHERE r.Name = 'Admin' AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);
GO

/* ---------------------------------------------------------------- existing local users: the old IsAdmin flag becomes a role (only users that have no role row at all) */
IF OBJECT_ID('dbo.UserRoles', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles)       /* the table is empty: first run on an old database (a fresh database has no users yet) */
INSERT INTO dbo.UserRoles (UserId, RoleId)
SELECT u.Id, r.RoleId FROM dbo.Users u
JOIN dbo.Roles r ON r.Name = CASE WHEN u.IsAdmin = 1 THEN 'Admin' ELSE 'User' END
WHERE u.ExternalId IS NULL;
GO

PRINT N'External auth / roles / permissions / audit log: done.';
