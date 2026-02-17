namespace CC.Aplication.Auth
{
    // ==================== REQUEST DTOs ====================

    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string? PhoneNumber = null
    );

    public record LoginRequest(
        string Email,
        string Password
    );

    public record UpdateProfileRequest(
        string FirstName,
        string LastName,
        string? PhoneNumber = null,
        string? DocumentType = null,
        string? DocumentNumber = null,
        DateTime? BirthDate = null,
        string? Address = null,
        string? City = null,
        string? Country = null
    );

    // ==================== RESPONSE DTOs ====================

    public record AuthResponse(
        string Token,
        DateTime ExpiresAt,
        UserDto User
    );

    public record UserDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        DateTime CreatedAt,
        bool IsActive
    );

    public record UserProfileDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? DocumentType,
        string? DocumentNumber,
        DateTime? BirthDate,
        string? Address,
        string? City,
        string? Country,
        DateTime CreatedAt,
        bool IsActive
    );
}
