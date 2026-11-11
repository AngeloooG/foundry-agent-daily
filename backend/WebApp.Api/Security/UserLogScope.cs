namespace WebApp.Api.Security;

public static class UserLogScopeExtensions
{
    /// <summary>
    /// Adds a <c>UserKey</c> scope (first 16 hex chars of the hashed tenant:oid) to every log entry
    /// of an authenticated request, so actions can be audited per user in Application Insights
    /// without logging names, emails or raw object ids. Must run after UseAuthentication.
    /// </summary>
    public static IApplicationBuilder UseUserLogScope(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("WebApp.Api.UserScope");

        return app.Use(async (context, next) =>
        {
            var userKey = UserIdentity.GetLogKey(context.User);
            if (userKey is null)
            {
                await next();
                return;
            }

            using (logger.BeginScope(new Dictionary<string, object> { ["UserKey"] = userKey }))
            {
                await next();
            }
        });
    }
}
