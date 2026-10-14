using System.Security.Cryptography;

namespace SpyGame.Web.Models.Game;

/// <summary>
/// Where the game gets its random numbers. Who the spy is must not be guessable, so the real
/// game uses the operating system's cryptographic generator; tests pass a fixed sequence.
/// </summary>
public interface IRandomSource
{
    /// <summary>A number from 0 up to (not including) <paramref name="maxExclusive"/>.</summary>
    int Next(int maxExclusive);
}

public sealed class CryptoRandomSource : IRandomSource
{
    public int Next(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
