using Api_eCommerce.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Api_eCommerce.Tests.Catalog;

public class NullableDecimalFormValueBinderTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("null")]
  [InlineData("NULL")]
  public async Task BindModelAsync_WhenValueIsMissingOrNullLike_ReturnsNull(string? rawValue)
  {
    var binder = new NullableDecimalFormValueBinder();
    var bindingContext = CreateBindingContext(rawValue);

    await binder.BindModelAsync(bindingContext);

    bindingContext.Result.IsModelSet.Should().BeTrue();
    bindingContext.Result.Model.Should().BeNull();
    bindingContext.ModelState.ErrorCount.Should().Be(0);
  }

  [Fact]
  public async Task BindModelAsync_WhenValueIsValidDecimal_ReturnsParsedDecimal()
  {
    var binder = new NullableDecimalFormValueBinder();
    var bindingContext = CreateBindingContext("19.5");

    await binder.BindModelAsync(bindingContext);

    bindingContext.Result.IsModelSet.Should().BeTrue();
    bindingContext.Result.Model.Should().Be(19.5m);
    bindingContext.ModelState.ErrorCount.Should().Be(0);
  }

  [Fact]
  public async Task BindModelAsync_WhenValueIsInvalid_AddsModelError()
  {
    var binder = new NullableDecimalFormValueBinder();
    var bindingContext = CreateBindingContext("abc");

    await binder.BindModelAsync(bindingContext);

    bindingContext.Result.IsModelSet.Should().BeFalse();
    bindingContext.ModelState.ErrorCount.Should().Be(1);
    bindingContext.ModelState["taxPercentage"]!.Errors.Should().ContainSingle();
    bindingContext.ModelState["taxPercentage"]!.Errors[0].ErrorMessage.Should().Be("The value 'abc' is not valid for taxPercentage.");
  }

  private static ModelBindingContext CreateBindingContext(string? rawValue)
  {
    var values = rawValue is null
        ? new Dictionary<string, StringValues>()
        : new Dictionary<string, StringValues>
        {
          ["taxPercentage"] = rawValue
        };

    var metadataProvider = new EmptyModelMetadataProvider();
    var actionContext = new ActionContext
    {
      HttpContext = new DefaultHttpContext()
    };
    var valueProvider = new FormValueProvider(
        BindingSource.Form,
        new FormCollection(values),
        CultureInfo.InvariantCulture);

    return DefaultModelBindingContext.CreateBindingContext(
        actionContext,
        valueProvider,
        metadataProvider.GetMetadataForType(typeof(decimal?)),
        bindingInfo: null,
        modelName: "taxPercentage");
  }
}