using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Admin
{
  public interface IAdminUserManagementService
  {
    Task<PagedAdminUsersResponse> GetUsersAsync(AdminUserListQuery query, CancellationToken ct = default);
    Task<AdminUserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<AdminUserDetailDto> CreateUserAsync(CreateAdminUserRequest request, CancellationToken ct = default);
    Task<AdminUserDetailDto> UpdateUserAsync(Guid userId, UpdateAdminUserRequest request, CancellationToken ct = default);
    Task<AdminUserDetailDto> UpdateUserRolesAsync(Guid userId, UpdateAdminUserRolesRequest request, CancellationToken ct = default);
    Task<bool> UpdateUserPasswordAsync(Guid userId, UpdateAdminPasswordRequest request, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<AdminRoleDto>> GetAllRolesAsync(CancellationToken ct = default);
  }

  public class AdminUserManagementService : IAdminUserManagementService
  {
    private readonly AdminDbContext _adminDb;
    private readonly ILogger<AdminUserManagementService> _logger;

    public AdminUserManagementService(
        AdminDbContext adminDb,
        ILogger<AdminUserManagementService> logger)
    {
      _adminDb = adminDb;
      _logger = logger;
    }

    public async Task<PagedAdminUsersResponse> GetUsersAsync(AdminUserListQuery query, CancellationToken ct = default)
    {
      var queryable = _adminDb.AdminUsers
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.AdminRole)
          .AsQueryable();

      // Search filter
      if (!string.IsNullOrWhiteSpace(query.Search))
      {
        var search = query.Search.ToLower();
        queryable = queryable.Where(u =>
            u.Email.ToLower().Contains(search) ||
            u.FullName.ToLower().Contains(search));
      }

      // IsActive filter
      if (query.IsActive.HasValue)
      {
        queryable = queryable.Where(u => u.IsActive == query.IsActive.Value);
      }

      // Role filter
      if (!string.IsNullOrWhiteSpace(query.RoleName))
      {
        queryable = queryable.Where(u =>
            u.UserRoles.Any(ur => ur.AdminRole.Name == query.RoleName));
      }

      // Total count before pagination
      var totalCount = await queryable.CountAsync(ct);

      // Pagination
      var items = await queryable
          .OrderBy(u => u.CreatedAt)
          .Skip((query.Page - 1) * query.PageSize)
          .Take(query.PageSize)
          .Select(u => new AdminUserDto(
              u.Id,
              u.Email,
              u.FullName,
              u.IsActive,
              u.UserRoles.Select(ur => ur.AdminRole.Name).ToList(),
              u.CreatedAt,
              u.LastLoginAt
          ))
          .ToListAsync(ct);

      return new PagedAdminUsersResponse(
          items,
          totalCount,
          query.Page,
          query.PageSize,
          (int)Math.Ceiling(totalCount / (double)query.PageSize)
      );
    }

    public async Task<AdminUserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
      var user = await _adminDb.AdminUsers
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.AdminRole)
          .FirstOrDefaultAsync(u => u.Id == userId, ct);

      if (user == null)
        return null;

      return new AdminUserDetailDto(
          user.Id,
          user.Email,
          user.FullName,
          user.IsActive,
          user.UserRoles.Select(ur => new AdminRoleDto(
              ur.AdminRole.Id,
              ur.AdminRole.Name,
              ur.AdminRole.Description
          )).ToList(),
          user.CreatedAt,
          user.UpdatedAt,
          user.LastLoginAt
      );
    }

    public async Task<AdminUserDetailDto> CreateUserAsync(CreateAdminUserRequest request, CancellationToken ct = default)
    {
      // Validate email uniqueness
      var emailExists = await _adminDb.AdminUsers
          .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

      if (emailExists)
      {
        throw new InvalidOperationException($"User with email '{request.Email}' already exists");
      }

      // Validate roles exist
      var roles = await _adminDb.AdminRoles
          .Where(r => request.RoleNames.Contains(r.Name))
          .ToListAsync(ct);

      if (roles.Count != request.RoleNames.Count)
      {
        var foundRoles = roles.Select(r => r.Name).ToList();
        var missingRoles = request.RoleNames.Except(foundRoles).ToList();
        throw new InvalidOperationException($"Roles not found: {string.Join(", ", missingRoles)}");
      }

      // Hash password using AdminAuthService method
      var (hash, salt) = AdminAuthService.HashPassword(request.Password);

      // Create user
      var user = new AdminUser
      {
        Id = Guid.NewGuid(),
        Email = request.Email.ToLower(),
        FullName = request.FullName,
        PasswordHash = hash,
        PasswordSalt = salt,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      };

      _adminDb.AdminUsers.Add(user);

      // Assign roles
      foreach (var role in roles)
      {
        _adminDb.AdminUserRoles.Add(new AdminUserRole
        {
          AdminUserId = user.Id,
          AdminRoleId = role.Id,
          AssignedAt = DateTime.UtcNow
        });
      }

      await _adminDb.SaveChangesAsync(ct);

      _logger.LogInformation("Admin user created: {Email} with roles: {Roles}",
          user.Email, string.Join(", ", request.RoleNames));

      return new AdminUserDetailDto(
          user.Id,
          user.Email,
          user.FullName,
          user.IsActive,
          roles.Select(r => new AdminRoleDto(r.Id, r.Name, r.Description)).ToList(),
          user.CreatedAt,
          null,
          null
      );
    }

    public async Task<AdminUserDetailDto> UpdateUserAsync(Guid userId, UpdateAdminUserRequest request, CancellationToken ct = default)
    {
      var user = await _adminDb.AdminUsers
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.AdminRole)
          .FirstOrDefaultAsync(u => u.Id == userId, ct);

      if (user == null)
      {
        throw new InvalidOperationException($"User with id '{userId}' not found");
      }

      // Update fields if provided
      if (!string.IsNullOrWhiteSpace(request.FullName))
      {
        user.FullName = request.FullName;
      }

      if (request.IsActive.HasValue)
      {
        // Prevent deactivating the last active SuperAdmin
        if (!request.IsActive.Value)
        {
          var isSuperAdmin = user.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin);
          if (isSuperAdmin)
          {
            var activeSuperAdminsCount = await _adminDb.AdminUsers
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.AdminRole)
                .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin))
                .CountAsync(ct);

            if (activeSuperAdminsCount <= 1)
            {
              throw new InvalidOperationException("Cannot deactivate the last active SuperAdmin user");
            }
          }
        }

        user.IsActive = request.IsActive.Value;
      }

      user.UpdatedAt = DateTime.UtcNow;

      await _adminDb.SaveChangesAsync(ct);

      _logger.LogInformation("Admin user updated: {Email}", user.Email);

      return new AdminUserDetailDto(
          user.Id,
          user.Email,
          user.FullName,
          user.IsActive,
          user.UserRoles.Select(ur => new AdminRoleDto(
              ur.AdminRole.Id,
              ur.AdminRole.Name,
              ur.AdminRole.Description
          )).ToList(),
          user.CreatedAt,
          user.UpdatedAt,
          user.LastLoginAt
      );
    }

    public async Task<AdminUserDetailDto> UpdateUserRolesAsync(Guid userId, UpdateAdminUserRolesRequest request, CancellationToken ct = default)
    {
      var user = await _adminDb.AdminUsers
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.AdminRole)
          .FirstOrDefaultAsync(u => u.Id == userId, ct);

      if (user == null)
      {
        throw new InvalidOperationException($"User with id '{userId}' not found");
      }

      // Validate roles exist
      var roles = await _adminDb.AdminRoles
          .Where(r => request.RoleNames.Contains(r.Name))
          .ToListAsync(ct);

      if (roles.Count != request.RoleNames.Count)
      {
        var foundRoles = roles.Select(r => r.Name).ToList();
        var missingRoles = request.RoleNames.Except(foundRoles).ToList();
        throw new InvalidOperationException($"Roles not found: {string.Join(", ", missingRoles)}");
      }

      // Check if removing SuperAdmin role from last SuperAdmin
      var currentlySuperAdmin = user.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin);
      var willBeSuperAdmin = request.RoleNames.Contains(AdminRoleNames.SuperAdmin);

      if (currentlySuperAdmin && !willBeSuperAdmin)
      {
        var activeSuperAdminsCount = await _adminDb.AdminUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.AdminRole)
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin))
            .CountAsync(ct);

        if (activeSuperAdminsCount <= 1)
        {
          throw new InvalidOperationException("Cannot remove SuperAdmin role from the last active SuperAdmin user");
        }
      }

      // Remove all existing roles
      _adminDb.AdminUserRoles.RemoveRange(user.UserRoles);

      // Assign new roles
      foreach (var role in roles)
      {
        _adminDb.AdminUserRoles.Add(new AdminUserRole
        {
          AdminUserId = user.Id,
          AdminRoleId = role.Id,
          AssignedAt = DateTime.UtcNow
        });
      }

      user.UpdatedAt = DateTime.UtcNow;

      await _adminDb.SaveChangesAsync(ct);

      // Reload to get updated roles
      await _adminDb.Entry(user).Collection(u => u.UserRoles).LoadAsync(ct);

      _logger.LogInformation("Admin user roles updated: {Email}, New roles: {Roles}",
          user.Email, string.Join(", ", request.RoleNames));

      return new AdminUserDetailDto(
          user.Id,
          user.Email,
          user.FullName,
          user.IsActive,
          roles.Select(r => new AdminRoleDto(r.Id, r.Name, r.Description)).ToList(),
          user.CreatedAt,
          user.UpdatedAt,
          user.LastLoginAt
      );
    }

    public async Task<bool> UpdateUserPasswordAsync(Guid userId, UpdateAdminPasswordRequest request, CancellationToken ct = default)
    {
      var user = await _adminDb.AdminUsers.FindAsync(new object[] { userId }, ct);

      if (user == null)
      {
        return false;
      }

      // Hash new password
      var (hash, salt) = AdminAuthService.HashPassword(request.NewPassword);

      user.PasswordHash = hash;
      user.PasswordSalt = salt;
      user.UpdatedAt = DateTime.UtcNow;

      await _adminDb.SaveChangesAsync(ct);

      _logger.LogInformation("Admin user password updated: {Email}", user.Email);

      return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
      var user = await _adminDb.AdminUsers
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.AdminRole)
          .FirstOrDefaultAsync(u => u.Id == userId, ct);

      if (user == null)
      {
        return false;
      }

      // Prevent deleting the last active SuperAdmin
      var isSuperAdmin = user.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin);
      if (isSuperAdmin)
      {
        var activeSuperAdminsCount = await _adminDb.AdminUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.AdminRole)
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.AdminRole.Name == AdminRoleNames.SuperAdmin))
            .CountAsync(ct);

        if (activeSuperAdminsCount <= 1)
        {
          throw new InvalidOperationException("Cannot delete the last active SuperAdmin user");
        }
      }

      // Soft delete (set IsActive = false)
      user.IsActive = false;
      user.UpdatedAt = DateTime.UtcNow;

      await _adminDb.SaveChangesAsync(ct);

      _logger.LogWarning("Admin user deleted (soft): {Email}", user.Email);

      return true;
    }

    public async Task<List<AdminRoleDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
      return await _adminDb.AdminRoles
          .Select(r => new AdminRoleDto(r.Id, r.Name, r.Description))
          .ToListAsync(ct);
    }
  }
}
