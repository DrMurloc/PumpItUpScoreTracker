using System.Text;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Rivals.Domain;

/// <summary>
///     The one code a private account hands out (docs/design/rivals.md D23–D25). Shaped to survive
///     being read aloud and retyped: twelve characters in three groups, drawn from an alphabet with
///     no I, O, 0 or 1 in it, because those are the four a person transcribes wrong.
///     <para>
///         Parsing is forgiving — case and dashes are noise — but the stored form is always
///         canonical, so a lookup never has to guess which spelling was saved.
///     </para>
/// </summary>
internal readonly struct RivalInviteCode
{
    /// <summary>32 characters, minus the four that get misread.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const int GroupSize = 4;
    private const int Groups = 3;
    private const int Length = GroupSize * Groups;

    private readonly string _code;

    private RivalInviteCode(string code)
    {
        _code = code;
    }

    public override string ToString()
    {
        return _code;
    }

    public static implicit operator string(RivalInviteCode code)
    {
        return code._code;
    }

    /// <summary>
    ///     Accepts what somebody pasted — any case, dashes or none, surrounding whitespace — and
    ///     returns the canonical form. Throws when the result could not be one of ours.
    /// </summary>
    public static RivalInviteCode From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidRivalInviteCodeException("The code was empty.");

        var stripped = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (stripped.Length != Length)
            throw new InvalidRivalInviteCodeException($"A code is {Length} characters long.");
        if (stripped.Any(c => !Alphabet.Contains(c)))
            throw new InvalidRivalInviteCodeException("The code contains characters no code uses.");

        return new RivalInviteCode(Format(stripped));
    }

    public static bool TryParse(string? value, out RivalInviteCode result)
    {
        try
        {
            result = From(value);
            return true;
        }
        catch (InvalidRivalInviteCodeException)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    ///     Draws a fresh code. 32^12 is far past any need to check for collisions up front — the
    ///     unique index is the backstop, and the caller draws again if it ever fires.
    /// </summary>
    public static RivalInviteCode Generate(IRandomNumberGenerator random)
    {
        var characters = new char[Length];
        for (var i = 0; i < Length; i++) characters[i] = Alphabet[random.Next(Alphabet.Length)];
        return new RivalInviteCode(Format(new string(characters)));
    }

    private static string Format(string stripped)
    {
        var builder = new StringBuilder(Length + Groups - 1);
        for (var i = 0; i < Groups; i++)
        {
            if (i > 0) builder.Append('-');
            builder.Append(stripped, i * GroupSize, GroupSize);
        }

        return builder.ToString();
    }
}
