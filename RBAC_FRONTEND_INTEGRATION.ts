/**
 * 🎯 RBAC API Service - Frontend Integration Guide
 * TypeScript/JavaScript examples for Angular, React, Vue, etc.
 */

// =============================================
// 📦 Configuration & Types
// =============================================

const API_BASE_URL = 'https://api-ecommerce-d9fxeccbeeehdjd3.eastus-01.azurewebsites.net';
// const API_BASE_URL = 'http://localhost:5000'; // Development

interface ApiHeaders {
  'Authorization': string;
  'X-Tenant-Slug': string;
  'Content-Type': string;
}

// =============================================
// 🔐 Base API Client
// =============================================

class RBACApiClient {
  private baseUrl: string;
  private token: string;
  private tenantSlug: string;

  constructor(baseUrl: string, token: string, tenantSlug: string) {
    this.baseUrl = baseUrl;
    this.token = token;
    this.tenantSlug = tenantSlug;
  }

  private getHeaders(): ApiHeaders {
    return {
      'Authorization': `Bearer ${this.token}`,
      'X-Tenant-Slug': this.tenantSlug,
      'Content-Type': 'application/json'
    };
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || error.title || 'API Error');
    }
    
    if (response.status === 204) {
      return {} as T;
    }
    
    return response.json();
  }

  async get<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'GET',
      headers: this.getHeaders()
    });
    return this.handleResponse<T>(response);
  }

  async post<T>(endpoint: string, data: any): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(data)
    });
    return this.handleResponse<T>(response);
  }

  async put<T>(endpoint: string, data: any): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'PUT',
      headers: this.getHeaders(),
      body: JSON.stringify(data)
    });
    return this.handleResponse<T>(response);
  }

  async patch<T>(endpoint: string, data: any): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'PATCH',
      headers: this.getHeaders(),
      body: JSON.stringify(data)
    });
    return this.handleResponse<T>(response);
  }

  async delete<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'DELETE',
      headers: this.getHeaders()
    });
    return this.handleResponse<T>(response);
  }
}

// =============================================
// 📋 TypeScript Interfaces
// =============================================

// User Types
interface TenantUserDetailDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  isActive: boolean;
  emailConfirmed: boolean;
  roles: RoleSummaryDto[];
  createdAt: string;
  lastLoginAt?: string;
  lastModifiedAt?: string;
}

interface AdminUsersResponse {
  users: TenantUserDetailDto[];
  totalUsers: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  password: string;
  roleIds?: string[];
}

interface UpdateUserRolesRequest {
  roleIds: string[];
}

interface UpdateUserActiveStatusRequest {
  isActive: boolean;
}

// Role Types
interface RoleDetailDto {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  users: UserSummaryDto[];
  permissions: ModulePermissionDto[];
  usersCount: number;
  createdAt: string;
  lastModifiedAt?: string;
}

interface RoleSummaryDto {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
}

interface RolesResponse {
  roles: RoleSummaryDto[];
  totalRoles: number;
}

interface CreateRoleRequest {
  name: string;
  description?: string;
}

interface UpdateRoleRequest {
  name?: string;
  description?: string;
}

// Permission Types
interface ModuleDto {
  code: string;
  name: string;
  description: string;
  icon: string;
  isActive: boolean;
  availablePermissions: string[];
}

interface AvailableModulesResponse {
  modules: ModuleDto[];
}

interface ModulePermissionDto {
  moduleCode: string;
  moduleName: string;
  canView: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

interface RolePermissionsResponse {
  roleId: string;
  roleName: string;
  permissions: ModulePermissionDto[];
}

interface UpdateRolePermissionsRequest {
  permissions: {
    moduleCode: string;
    canView: boolean;
    canCreate: boolean;
    canUpdate: boolean;
    canDelete: boolean;
  }[];
}

interface UserSummaryDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
}

// =============================================
// 👥 User Service
// =============================================

class UserService {
  constructor(private api: RBACApiClient) {}

  /**
   * Get all users with optional filters
   */
  async getUsers(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    roleId?: string;
    isActive?: boolean;
  }): Promise<AdminUsersResponse> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.roleId) queryParams.append('roleId', params.roleId);
    if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());

    const query = queryParams.toString();
    return this.api.get<AdminUsersResponse>(`/admin/users${query ? '?' + query : ''}`);
  }

  /**
   * Get user by ID
   */
  async getUserById(userId: string): Promise<TenantUserDetailDto> {
    return this.api.get<TenantUserDetailDto>(`/admin/users/${userId}`);
  }

  /**
   * Create new user
   */
  async createUser(request: CreateUserRequest): Promise<TenantUserDetailDto> {
    return this.api.post<TenantUserDetailDto>('/admin/users', request);
  }

  /**
   * Update user roles
   */
  async updateUserRoles(userId: string, request: UpdateUserRolesRequest): Promise<TenantUserDetailDto> {
    return this.api.put<TenantUserDetailDto>(`/admin/users/${userId}/roles`, request);
  }

  /**
   * Activate or deactivate user
   */
  async updateUserStatus(userId: string, isActive: boolean): Promise<TenantUserDetailDto> {
    return this.api.patch<TenantUserDetailDto>(`/admin/users/${userId}/status`, { isActive });
  }

  /**
   * Delete user (soft delete)
   */
  async deleteUser(userId: string): Promise<void> {
    return this.api.delete<void>(`/admin/users/${userId}`);
  }
}

// =============================================
// 🎭 Role Service
// =============================================

class RoleService {
  constructor(private api: RBACApiClient) {}

  /**
   * Get all roles
   */
  async getRoles(): Promise<RolesResponse> {
    return this.api.get<RolesResponse>('/admin/roles');
  }

  /**
   * Get role by ID with details
   */
  async getRoleById(roleId: string): Promise<RoleDetailDto> {
    return this.api.get<RoleDetailDto>(`/admin/roles/${roleId}`);
  }

  /**
   * Create new role
   */
  async createRole(request: CreateRoleRequest): Promise<RoleDetailDto> {
    return this.api.post<RoleDetailDto>('/admin/roles', request);
  }

  /**
   * Update role information
   */
  async updateRole(roleId: string, request: UpdateRoleRequest): Promise<RoleDetailDto> {
    return this.api.put<RoleDetailDto>(`/admin/roles/${roleId}`, request);
  }

  /**
   * Delete role
   */
  async deleteRole(roleId: string): Promise<void> {
    return this.api.delete<void>(`/admin/roles/${roleId}`);
  }
}

// =============================================
// 🔑 Permission Service
// =============================================

class PermissionService {
  constructor(private api: RBACApiClient) {}

  /**
   * Get available modules catalog
   */
  async getAvailableModules(): Promise<AvailableModulesResponse> {
    return this.api.get<AvailableModulesResponse>('/admin/roles/available-modules');
  }

  /**
   * Get role permissions
   */
  async getRolePermissions(roleId: string): Promise<RolePermissionsResponse> {
    return this.api.get<RolePermissionsResponse>(`/admin/roles/${roleId}/permissions`);
  }

  /**
   * Update role permissions
   */
  async updateRolePermissions(
    roleId: string,
    request: UpdateRolePermissionsRequest
  ): Promise<RolePermissionsResponse> {
    return this.api.put<RolePermissionsResponse>(`/admin/roles/${roleId}/permissions`, request);
  }
}

// =============================================
// 🚀 Usage Examples
// =============================================

// Initialize the API client
const token = 'your-jwt-token';
const tenantSlug = 'demo-store';
const apiClient = new RBACApiClient(API_BASE_URL, token, tenantSlug);

// Initialize services
const userService = new UserService(apiClient);
const roleService = new RoleService(apiClient);
const permissionService = new PermissionService(apiClient);

// ============= EXAMPLE 1: List Users =============
async function listUsersExample() {
  try {
    const response = await userService.getUsers({
      page: 1,
      pageSize: 10,
      isActive: true
    });
    
    console.log(`Total users: ${response.totalUsers}`);
    response.users.forEach(user => {
      console.log(`- ${user.firstName} ${user.lastName} (${user.email})`);
      console.log(`  Roles: ${user.roles.map(r => r.name).join(', ')}`);
    });
  } catch (error) {
    console.error('Error listing users:', error);
  }
}

// ============= EXAMPLE 2: Create User =============
async function createUserExample() {
  try {
    const newUser = await userService.createUser({
      email: 'john.doe@example.com',
      firstName: 'John',
      lastName: 'Doe',
      password: 'SecurePass123!',
      phoneNumber: '+1234567890',
      roleIds: [] // Will get default "Customer" role
    });
    
    console.log('User created:', newUser);
  } catch (error) {
    console.error('Error creating user:', error);
  }
}

// ============= EXAMPLE 3: Update User Roles =============
async function updateUserRolesExample(userId: string, roleIds: string[]) {
  try {
    const updatedUser = await userService.updateUserRoles(userId, { roleIds });
    console.log('User roles updated:', updatedUser.roles);
  } catch (error) {
    console.error('Error updating user roles:', error);
  }
}

// ============= EXAMPLE 4: Create Role and Assign Permissions =============
async function createRoleWithPermissionsExample() {
  try {
    // Step 1: Create role
    const role = await roleService.createRole({
      name: 'Store Manager',
      description: 'Full store management access'
    });
    
    console.log('Role created:', role);

    // Step 2: Assign permissions
    const permissions = await permissionService.updateRolePermissions(role.id, {
      permissions: [
        {
          moduleCode: 'inventory',
          canView: true,
          canCreate: true,
          canUpdate: true,
          canDelete: true
        },
        {
          moduleCode: 'sales',
          canView: true,
          canCreate: true,
          canUpdate: true,
          canDelete: false
        },
        {
          moduleCode: 'reports',
          canView: true,
          canCreate: false,
          canUpdate: false,
          canDelete: false
        }
      ]
    });
    
    console.log('Permissions assigned:', permissions);
  } catch (error) {
    console.error('Error creating role with permissions:', error);
  }
}

// ============= EXAMPLE 5: Get Available Modules =============
async function getAvailableModulesExample() {
  try {
    const response = await permissionService.getAvailableModules();
    
    console.log('Available modules:');
    response.modules.forEach(module => {
      console.log(`${module.icon} ${module.name} (${module.code})`);
      console.log(`  Permissions: ${module.availablePermissions.join(', ')}`);
    });
  } catch (error) {
    console.error('Error getting modules:', error);
  }
}

// ============= EXAMPLE 6: Search Users =============
async function searchUsersExample(searchTerm: string) {
  try {
    const response = await userService.getUsers({
      search: searchTerm,
      page: 1,
      pageSize: 20
    });
    
    console.log(`Found ${response.totalUsers} users matching "${searchTerm}"`);
    return response.users;
  } catch (error) {
    console.error('Error searching users:', error);
    return [];
  }
}

// ============= EXAMPLE 7: Deactivate User =============
async function deactivateUserExample(userId: string) {
  try {
    const updatedUser = await userService.updateUserStatus(userId, false);
    console.log(`User ${updatedUser.email} deactivated`);
  } catch (error) {
    console.error('Error deactivating user:', error);
  }
}

// ============= EXAMPLE 8: Delete Role =============
async function deleteRoleExample(roleId: string) {
  try {
    await roleService.deleteRole(roleId);
    console.log('Role deleted successfully');
  } catch (error) {
    console.error('Error deleting role:', error);
  }
}

// ============= EXAMPLE 9: Get Role with Details =============
async function getRoleDetailsExample(roleId: string) {
  try {
    const role = await roleService.getRoleById(roleId);
    
    console.log(`Role: ${role.name}`);
    console.log(`Users assigned: ${role.usersCount}`);
    console.log('Permissions:');
    role.permissions.forEach(perm => {
      const actions = [];
      if (perm.canView) actions.push('view');
      if (perm.canCreate) actions.push('create');
      if (perm.canUpdate) actions.push('update');
      if (perm.canDelete) actions.push('delete');
      console.log(`  ${perm.moduleName}: ${actions.join(', ')}`);
    });
  } catch (error) {
    console.error('Error getting role details:', error);
  }
}

// ============= EXAMPLE 10: Complete Workflow =============
async function completeWorkflowExample() {
  try {
    // 1. Get available modules
    const modules = await permissionService.getAvailableModules();
    console.log('Step 1: Retrieved available modules');

    // 2. Create a new role
    const role = await roleService.createRole({
      name: 'Sales Representative',
      description: 'Handles customer sales'
    });
    console.log('Step 2: Created role:', role.name);

    // 3. Assign permissions to the role
    await permissionService.updateRolePermissions(role.id, {
      permissions: [
        {
          moduleCode: 'sales',
          canView: true,
          canCreate: true,
          canUpdate: true,
          canDelete: false
        },
        {
          moduleCode: 'customers',
          canView: true,
          canCreate: false,
          canUpdate: true,
          canDelete: false
        }
      ]
    });
    console.log('Step 3: Assigned permissions to role');

    // 4. Create a new user with that role
    const user = await userService.createUser({
      email: 'sales@example.com',
      firstName: 'Sales',
      lastName: 'Rep',
      password: 'SecurePass123!',
      roleIds: [role.id]
    });
    console.log('Step 4: Created user:', user.email);

    console.log('✅ Workflow completed successfully!');
  } catch (error) {
    console.error('❌ Workflow failed:', error);
  }
}

// =============================================
// 🎨 React Hook Example
// =============================================

// Custom React Hook for user management
function useUsers() {
  const [users, setUsers] = React.useState<TenantUserDetailDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const loadUsers = async (params?: any) => {
    setLoading(true);
    setError(null);
    try {
      const response = await userService.getUsers(params);
      setUsers(response.users);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const createUser = async (request: CreateUserRequest) => {
    setLoading(true);
    setError(null);
    try {
      const newUser = await userService.createUser(request);
      setUsers(prev => [...prev, newUser]);
      return newUser;
    } catch (err: any) {
      setError(err.message);
      throw err;
    } finally {
      setLoading(false);
    }
  };

  const updateUserRoles = async (userId: string, roleIds: string[]) => {
    setLoading(true);
    setError(null);
    try {
      const updatedUser = await userService.updateUserRoles(userId, { roleIds });
      setUsers(prev => prev.map(u => u.id === userId ? updatedUser : u));
      return updatedUser;
    } catch (err: any) {
      setError(err.message);
      throw err;
    } finally {
      setLoading(false);
    }
  };

  return { users, loading, error, loadUsers, createUser, updateUserRoles };
}

// =============================================
// 📱 Angular Service Example
// =============================================

/*
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class RBACService {
  private baseUrl = 'https://api-ecommerce-d9fxeccbeeehdjd3.eastus-01.azurewebsites.net';
  
  constructor(private http: HttpClient) {}
  
  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    const tenantSlug = localStorage.getItem('tenantSlug');
    
    return new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'X-Tenant-Slug': tenantSlug || '',
      'Content-Type': 'application/json'
    });
  }
  
  getUsers(params?: any): Observable<AdminUsersResponse> {
    const headers = this.getHeaders();
    return this.http.get<AdminUsersResponse>(`${this.baseUrl}/admin/users`, { headers, params });
  }
  
  createUser(request: CreateUserRequest): Observable<TenantUserDetailDto> {
    const headers = this.getHeaders();
    return this.http.post<TenantUserDetailDto>(`${this.baseUrl}/admin/users`, request, { headers });
  }
  
  getRoles(): Observable<RolesResponse> {
    const headers = this.getHeaders();
    return this.http.get<RolesResponse>(`${this.baseUrl}/admin/roles`, { headers });
  }
}
*/

// =============================================
// 📝 Export for use in other modules
// =============================================

export {
  RBACApiClient,
  UserService,
  RoleService,
  PermissionService,
  // Types
  type TenantUserDetailDto,
  type AdminUsersResponse,
  type CreateUserRequest,
  type UpdateUserRolesRequest,
  type RoleDetailDto,
  type RoleSummaryDto,
  type RolesResponse,
  type CreateRoleRequest,
  type UpdateRoleRequest,
  type ModuleDto,
  type AvailableModulesResponse,
  type ModulePermissionDto,
  type RolePermissionsResponse,
  type UpdateRolePermissionsRequest
};
