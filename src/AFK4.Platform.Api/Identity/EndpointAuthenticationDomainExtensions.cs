namespace AFK4.Platform.Api.Identity;

public static class EndpointAuthenticationDomainExtensions
{
    public static TBuilder RequirePlatformDomain<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AuthenticationDomainMetadata(AuthenticationDomain.Platform));
    }

    public static TBuilder RequireOrganizationDomain<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AuthenticationDomainMetadata(AuthenticationDomain.Organization));
    }
}
