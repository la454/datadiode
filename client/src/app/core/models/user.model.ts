/** What any signed-in user receives about themself (login and /auth/me). */
export interface Self {
  username: string;
  fullName: string;
  email: string | null;
  /** "Admin" / "User" by what the user may do; "None" when their roles give no access yet (never a custom role name). */
  role: 'Admin' | 'User' | 'None' | string;
  isAdmin: boolean;
  fromDashboard: boolean;
  allowedExtensions: string[];
  quotaMb: number;
  usedBytes: number;
}

/** The signed-in user's access as the server sees it (GET /api/authorization/me). */
export interface AccessInfo {
  userId: number;
  role: string | null;
  isAdmin: boolean;
  isUser: boolean;
  roles: string[];
  permissions: string[];
}

/** Full record, only returned by the admin endpoints. */
export interface User {
  id: number;
  username: string;
  email: string | null;
  fullName: string | null;
  isActive: boolean;
  fromDashboard: boolean;
  isAdmin: boolean;
  roles: string[];
  groupId: number | null;
  folderPath: string;
  allowedExtensions: string[];
  quotaMb: number;
  usedBytes: number;
}

export interface LoginResponse { token: string; user: Self; }

export interface NewUser {
  username: string;
  password: string;
  isAdmin: boolean;
  allowedExtensions: string;
  quotaMb: number;
  folderPath: string;
  groupId: number | null;
  email: string;
  fullName: string;
}

export interface Group { id: number; name: string; memberCount: number; }

export interface PermissionItem { permissionId: number; code: string; nameAr: string; nameEn: string; group: string; }

export interface RoleRow {
  roleId: number;
  name: string;
  nameAr: string | null;
  description: string | null;
  isSystem: boolean;
  userCount: number;
  permissionIds: number[];
}

export interface RoleBody { name: string; nameAr: string; description: string; permissionIds: number[]; }

export interface RoleName { roleId: number; name: string; nameAr: string | null; }

export interface UserRolesRow {
  userId: number;
  username: string;
  fullName: string;
  email: string | null;
  externalId: string | null;
  isActive: boolean;
  roleIds: number[];
}

export interface AuditRow {
  id: number;
  userId: number | null;
  username: string | null;
  action: string;
  entityType: string | null;
  entityId: string | null;
  details: string | null;
  ipAddress: string | null;
  success: boolean;
  errorMessage: string | null;
  createdAt: string;
}
