namespace CC.Aplication.Admin
{
    // ==================== ADMIN AUTH ====================

    public record AdminLoginRequest(
        string Email,
        string Password
    );

    public record AdminLoginResponse(
        string Token,
        DateTime ExpiresAt,
        AdminUserDto User
    );

    public record AdminUserDto(
        Guid Id,
        string Email,
        string FullName,
        bool IsActive,
        List<string> Roles,
        DateTime CreatedAt,
        DateTime? LastLoginAt
    );

    // ==================== TENANT MANAGEMENT ====================

    public record TenantListQuery(
        int Page = 1,
        int PageSize = 20,
        string? Search = null,
        string? Status = null,
        Guid? PlanId = null
    );

    public record TenantSummaryDto(
        Guid Id,
        string Slug,
        string Name,
        string DbName,
        string Status,
        string? PlanName,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? LastError
    );

    public record PagedTenantsResponse(
        List<TenantSummaryDto> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record TenantDetailDto(
        Guid Id,
        string Slug,
        string Name,
        string DbName,
        string Status,
        Guid? PlanId,
        string? PlanName,
        string? FeatureFlagsJson,
        string? AllowedOrigins,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? LastError,
        List<ProvisioningStepDto>? RecentProvisioningSteps
    );

    public record ProvisioningStepDto(
        Guid Id,
        string Step,
        string Status,
        DateTime? StartedAt,
        DateTime? EndedAt,
        string? Log
    );

    public record UpdateTenantRequest(
        string? Name,
        Guid? PlanId,
        string? FeatureFlagsJson,
        string? AllowedOrigins,
        bool? IsActive
    );

    public record UpdateTenantStatusRequest(
        string Status // PENDING, READY, SUSPENDED, FAILED
    );

    // ==================== PLAN MANAGEMENT ====================

    public record PlanDto(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        decimal MonthlyPrice,
        string Currency,
        int MaxProducts,
        int MaxUsers,
        int? MaxOrdersPerMonth,
        bool IsActive,
        DateTime CreatedAt
    );

    public record CreatePlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal MonthlyPrice,
        string Currency,
        int MaxProducts,
        int MaxUsers,
        int? MaxOrdersPerMonth
    );

    public record UpdatePlanRequest(
        string? Name,
        string? Description,
        decimal? MonthlyPrice,
        int? MaxProducts,
        int? MaxUsers,
        int? MaxOrdersPerMonth,
        bool? IsActive
    );

    // ==================== ADMIN USER MANAGEMENT ====================

    public record AdminUserListQuery(
        int Page = 1,
        int PageSize = 20,
        string? Search = null,
        bool? IsActive = null
    );

    public record PagedAdminUsersResponse(
        List<AdminUserDto> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record CreateAdminUserRequest(
        string Email,
        string Password,
        string FullName,
        List<string> Roles
    );

    public record UpdateAdminUserRequest(
        string? FullName,
        string? Password,
        bool? IsActive,
        List<string>? Roles
    );
}
