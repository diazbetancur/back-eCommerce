using CC.Aplication.Notifications;
using CC.Domain.Interfaces.Notifications;
using CC.Infraestructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Api_eCommerce.Extensions;

public static class NotificationEmailServiceCollectionExtensions
{
  public static IServiceCollection AddNotificationEmailServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>());
    services.AddOptions<EmailOptions>()
        .Bind(configuration.GetSection(EmailOptions.SectionName))
        .ValidateOnStart();

    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ResendOptions>, ResendOptionsValidator>());
    services.AddOptions<ResendOptions>()
        .Bind(configuration.GetSection(ResendOptions.SectionName))
        .ValidateOnStart();

    services.AddHttpClient<ResendEmailProvider>((serviceProvider, client) =>
    {
      var options = serviceProvider.GetRequiredService<IOptions<ResendOptions>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    });

    services.AddScoped<NoOpEmailProvider>();
    services.AddScoped<IEmailProvider>(serviceProvider =>
    {
      var options = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
      if (options.EnableEmailSending
          && string.Equals(options.Provider, EmailOptions.ResendProvider, StringComparison.OrdinalIgnoreCase))
      {
        return serviceProvider.GetRequiredService<ResendEmailProvider>();
      }

      return serviceProvider.GetRequiredService<NoOpEmailProvider>();
    });

    return services;
  }
}