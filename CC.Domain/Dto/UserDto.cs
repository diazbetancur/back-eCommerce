namespace CC.Domain.Dto
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string? Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    /// <summary>
    /// Detalle completo de un usuario del tenant
    /// </summary>
    public record TenantUserDetailDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        List<string> Roles,
        bool IsActive,
        bool MustChangePassword,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    /// <summary>
    /// Request para actualizar roles de un usuario (reemplazo completo)
    /// </summary>
    public record UpdateUserRolesRequest(
        List<string> RoleNames
    );

    /// <summary>
    /// Request para activar/desactivar un usuario
    /// </summary>
    public record UpdateUserActiveStatusRequest(
        bool IsActive
    );
}