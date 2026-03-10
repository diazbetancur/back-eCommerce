using System.Reflection;
using CC.Aplication.Loyalty;

namespace Api_eCommerce.Tests.Loyalty;

public class LoyaltyManualAdjustRulesTests
{
  [Fact]
  public void ValidateManualAdjustmentPoints_WithZero_ThrowsArgumentException()
  {
    // Arrange
    var method = GetValidateManualAdjustmentPointsMethod();

    // Act
    var action = () => method.Invoke(null, new object?[] { 0 });

    // Assert
    var exception = action.Should().Throw<TargetInvocationException>().Which;
    exception.InnerException.Should().BeOfType<ArgumentException>();
    exception.InnerException!.Message.Should().Be("Points must be different from 0");
  }

  [Fact]
  public void ResolveManualAdjustmentExpiration_PositivePoints_WithExpirationDays_ReturnsCalculatedDate()
  {
    // Arrange
    var method = GetResolveManualAdjustmentExpirationMethod();
    var now = new DateTime(2026, 03, 09, 10, 00, 00, DateTimeKind.Utc);

    // Act
    var result = method.Invoke(null, new object?[] { 20, 30, now });

    // Assert
    result.Should().Be(now.AddDays(30));
  }

  [Fact]
  public void ResolveManualAdjustmentExpiration_PositivePoints_WithoutExpirationDays_ReturnsNull()
  {
    // Arrange
    var method = GetResolveManualAdjustmentExpirationMethod();
    var now = new DateTime(2026, 03, 09, 10, 00, 00, DateTimeKind.Utc);

    // Act
    var result = method.Invoke(null, new object?[] { 20, null, now });

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public void ResolveManualAdjustmentExpiration_NegativePoints_ReturnsNull()
  {
    // Arrange
    var method = GetResolveManualAdjustmentExpirationMethod();
    var now = new DateTime(2026, 03, 09, 10, 00, 00, DateTimeKind.Utc);

    // Act
    var result = method.Invoke(null, new object?[] { -20, 30, now });

    // Assert
    result.Should().BeNull();
  }

  private static MethodInfo GetValidateManualAdjustmentPointsMethod()
  {
    var method = typeof(LoyaltyService).GetMethod(
        "ValidateManualAdjustmentPoints",
        BindingFlags.NonPublic | BindingFlags.Static);

    method.Should().NotBeNull("ValidateManualAdjustmentPoints must exist in LoyaltyService");
    return method!;
  }

  private static MethodInfo GetResolveManualAdjustmentExpirationMethod()
  {
    var method = typeof(LoyaltyService).GetMethod(
        "ResolveManualAdjustmentExpiration",
        BindingFlags.NonPublic | BindingFlags.Static);

    method.Should().NotBeNull("ResolveManualAdjustmentExpiration must exist in LoyaltyService");
    return method!;
  }
}
