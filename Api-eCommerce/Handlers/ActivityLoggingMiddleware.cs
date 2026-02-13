using CC.Domain.Entities;
using CC.Infraestructure.Configurations;
using System.IdentityModel.Tokens.Jwt;

namespace Api_eCommerce.Handlers
{
    public class ActivityLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public ActivityLoggingMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Method == HttpMethods.Put ||
                context.Request.Method == HttpMethods.Post ||
                context.Request.Method == HttpMethods.Delete)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<DBContext>();
                        var userId = GetUserIdFromToken(context);

                        if (userId != "Unknown" && userId != "Anonymous")
                        {
                            var log = new UserActivityLog
                            {
                                UserId = new Guid(userId),
                                Action = $"{context.Request.Method} {context.Request.Path}",
                                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                                DateCreated = DateTime.UtcNow,
                                Id = Guid.NewGuid()
                            };

                            dbContext.UserActivityLogs.Add(log);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in UserActivityLog: {ex.Message}");
                }
            }
            await _next(context);
        }

        private string GetUserIdFromToken(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authorizationHeader != null && authorizationHeader.StartsWith("Bearer "))
            {
                var token = authorizationHeader.Substring("Bearer ".Length);
                var handler = new JwtSecurityTokenHandler();

                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId");

                return userIdClaim?.Value ?? "Unknown";
            }

            return "Anonymous";
        }
    }
}