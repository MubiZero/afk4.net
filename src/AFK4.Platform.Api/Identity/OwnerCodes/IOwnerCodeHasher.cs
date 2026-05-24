namespace AFK4.Platform.Api.Identity.OwnerCodes;

public interface IOwnerCodeHasher
{
    string Normalize(string rawCode);

    string Hash(string normalizedCode);

    string Suffix(string normalizedCode);
}
