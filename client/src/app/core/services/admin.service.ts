import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { API } from '../config/api.config';
import { AuditRow, Group, NewUser, PermissionItem, RoleBody, RoleName, RoleRow, User, UserRolesRow } from '../models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);

  // ---- users + groups (users.manage)
  users() { return this.http.get<User[]>(`${API}/admin/users`); }
  createUser(u: NewUser) { return this.http.post<User>(`${API}/admin/users`, u); }
  saveSettings(id: number, s: { allowedExtensions: string; quotaMb: number; folderPath: string; groupId: number | null; isActive: boolean; email: string; fullName: string }) {
    return this.http.put<User>(`${API}/admin/users/${id}/settings`, s);
  }
  resetPassword(id: number, password: string) { return this.http.put(`${API}/admin/users/${id}/password`, { password }); }
  deleteUser(id: number) { return this.http.delete(`${API}/admin/users/${id}`); }

  groups() { return this.http.get<Group[]>(`${API}/admin/groups`); }
  createGroup(name: string) { return this.http.post<Group>(`${API}/admin/groups`, { name }); }
  renameGroup(id: number, name: string) { return this.http.put<Group>(`${API}/admin/groups/${id}`, { name }); }
  deleteGroup(id: number) { return this.http.delete(`${API}/admin/groups/${id}`); }

  // ---- roles and permissions (roles.manage)
  permissions() { return this.http.get<PermissionItem[]>(`${API}/admin/permissions`); }
  roles() { return this.http.get<RoleRow[]>(`${API}/admin/roles`); }
  createRole(b: RoleBody) { return this.http.post<{ roleId: number }>(`${API}/admin/roles`, b); }
  updateRole(id: number, b: RoleBody) { return this.http.put<{ roleId: number }>(`${API}/admin/roles/${id}`, b); }
  deleteRole(id: number) { return this.http.delete(`${API}/admin/roles/${id}`); }

  // ---- roles of each user (userroles.manage)
  roleNames() { return this.http.get<RoleName[]>(`${API}/admin/role-names`); }
  userRoles(search: string) {
    let p = new HttpParams();
    if (search.trim()) p = p.set('search', search.trim());
    return this.http.get<UserRolesRow[]>(`${API}/admin/user-roles`, { params: p });
  }
  setUserRoles(userId: number, roleIds: number[]) { return this.http.put(`${API}/admin/user-roles/${userId}`, { roleIds }); }

  // ---- audit log (audit.view)
  audit(f: { action: string; search: string; success: boolean | null; take: number }) {
    let p = new HttpParams().set('take', f.take);
    if (f.action) p = p.set('action', f.action);
    if (f.search.trim()) p = p.set('search', f.search.trim());
    if (f.success !== null) p = p.set('success', f.success);
    return this.http.get<AuditRow[]>(`${API}/admin/audit-logs`, { params: p });
  }
  auditActions() { return this.http.get<string[]>(`${API}/admin/audit-logs/actions`); }
}
