using System.Reflection;
using CC.Aplication.Loyalty;

namespace Api_eCommerce.Tests.Loyalty;

public class LoyaltyRewardsValidationTests
{
  [Fact]
  public void ValidateRewardTargeting_Product_WithSingleProductId_DoesNotThrow()
  {
    // Arrange
    var method = GetValidateRewardTargetingMethod();
    var rewardType = "PRODUCT";
    var productId = Guid.NewGuid();
    var productIds = new List<Guid> { productId };

    // Act
    var action = () => method.Invoke(null, new object?[]
    {
            rewardType,
            productId,
            productIds,
            null,
            true,
            null
    });

    // Assert
    action.Should().NotThrow();
  }

  [Fact]
  public void ValidateRewardTargeting_Product_WithMoreThanOneProductId_ThrowsArgumentException()
  {
    // Arrange
    var method = GetValidateRewardTargetingMethod();
    var productIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

    // Act
    var action = () => method.Invoke(null, new object?[]
    {
            "PRODUCT",
            Guid.NewGuid(),
            productIds,
            null,
            true,
            null
    });

    // Assert
    var exception = action.Should().Throw<TargetInvocationException>().Which;
    exception.InnerException.Should().BeOfType<ArgumentException>();
    exception.InnerException!.Message.Should().Be("ProductIds must contain exactly one ProductId when RewardType is PRODUCT");
  }

  [Fact]
  public void ValidateRewardTargeting_Product_WithNoProductIds_ThrowsArgumentException()
  {
    // Arrange
    var method = GetValidateRewardTargetingMethod();

    // Act
    var action = () => method.Invoke(null, new object?[]
    {
            "PRODUCT",
            null,
            new List<Guid>(),
            null,
            true,
            null
    });

    // Assert
    var exception = action.Should().Throw<TargetInvocationException>().Which;
    exception.InnerException.Should().BeOfType<ArgumentException>();
    exception.InnerException!.Message.Should().Be("ProductIds must contain exactly one ProductId when RewardType is PRODUCT");
  }

  private static MethodInfo GetValidateRewardTargetingMethod()
  {
    var method = typeof(LoyaltyRewardsService).GetMethod(
        "ValidateRewardTargeting",
        BindingFlags.NonPublic | BindingFlags.Static);

    method.Should().NotBeNull("ValidateRewardTargeting must exist in LoyaltyRewardsService");
    return method!;
  }
}
