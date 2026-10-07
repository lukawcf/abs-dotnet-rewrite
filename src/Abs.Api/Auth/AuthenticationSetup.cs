using Microsoft.AspNetCore.Authentication;

namespace Abs.Api.Auth;

public static class AuthenticationSetup
{
    public const string Scheme = "AbsAuth";

    public static IServiceCollection AddAbsAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        var authentication = services.AddAuthentication(Scheme);
#if DEBUG
        // The dev login exists only in Debug builds (the Docker image is built in Release)
        // and is additionally gated on the Development environment at runtime.
        if (environment.IsDevelopment())
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(Scheme, null);
            return services;
        }
#endif
        // Real token authentication belongs to the Auth module. Until it exists, nobody is authenticated.
        authentication.AddScheme<AuthenticationSchemeOptions, UnauthenticatedHandler>(Scheme, null);
        return services;
    }
}
