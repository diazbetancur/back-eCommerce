using Api_eCommerce.Extensions;
using CC.Aplication.Notifications;
using CC.Domain.Interfaces.Notifications;
using CC.Infraestructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Tests.Notifications;

public class NotificationEmailServiceCollectionExtensionsTests
{
  [Fact]
  public void AddNotificationEmailServices_WhenResendEnabled_ResolvesResendProvider()
  {
    var services = BuildServices(new Dictionary<string, string?>
    {
      ["Email:Provider"] = EmailOptions.ResendProvider,
      ["Email:FromEmail"] = "no-reply@notifications.tuecom.online",
      ["Email:FromName"] = "TueCom",
      ["Email:SupportEmail"] = "soporte@tuecom.online",
      ["Email:PublicBaseDomain"] = "tuecom.online",
      ["Email:NotificationsDomain"] = "notifications.tuecom.online",
      ["Email:ActivationPath"] = "/activate-account",
      ["Email:ResetPasswordPath"] = "/reset-password",
      ["Email:EnableEmailSending"] = "true",
      ["Resend:ApiKey"] = "re_test_key"
    });

    using var serviceProvider = services.BuildServiceProvider();

    var provider = serviceProvider.GetRequiredService<IEmailProvider>();

    provider.Should().BeOfType<ResendEmailProvider>();
  }

  [Fact]
  public void AddNotificationEmailServices_WhenSendingDisabled_ResolvesNoOpProvider()
  {
    var services = BuildServices(new Dictionary<string, string?>
    {
      ["Email:Provider"] = EmailOptions.ResendProvider,
      ["Email:EnableEmailSending"] = "false"
    });

    using var serviceProvider = services.BuildServiceProvider();

    var provider = serviceProvider.GetRequiredService<IEmailProvider>();

    provider.Should().BeOfType<NoOpEmailProvider>();
  }

  [Fact]
  public void AddNotificationEmailServices_WhenResendApiKeyMissing_ThrowsValidationError()
  {
    var services = BuildServices(new Dictionary<string, string?>
    {
      ["Email:Provider"] = EmailOptions.ResendProvider,
      ["Email:FromEmail"] = "no-reply@notifications.tuecom.online",
      ["Email:FromName"] = "TueCom",
      ["Email:SupportEmail"] = "soporte@tuecom.online",
      ["Email:PublicBaseDomain"] = "tuecom.online",
      ["Email:NotificationsDomain"] = "notifications.tuecom.online",
      ["Email:ActivationPath"] = "/activate-account",
      ["Email:ResetPasswordPath"] = "/reset-password",
      ["Email:EnableEmailSending"] = "true"
    });

    using var serviceProvider = services.BuildServiceProvider();

    Action act = () => _ = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;

    act.Should().Throw<OptionsValidationException>()
        .WithMessage("*Resend:ApiKey*");
  }

  private static ServiceCollection BuildServices(Dictionary<string, string?> settings)
  {
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(settings)
        .Build();

    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging();
    services.AddNotificationEmailServices(configuration);
    return services;
  }
}