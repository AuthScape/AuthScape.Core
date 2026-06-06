using System.Linq.Expressions;
using AuthScape.Models.Users;

namespace AuthScape.Scim.Filters;

/// <summary>
/// Minimal SCIM 2.0 filter expression parser, sufficient for the filters Okta / Azure AD / OneLogin send:
///   userName eq "value"
///   userName sw "prefix"
///   active eq true
///   userName eq "x" and active eq true
///   email eq "x@y.com"
///
/// Returns a strongly-typed <see cref="Expression{TDelegate}"/> over <see cref="AppUser"/> usable
/// with EF Core's IQueryable.Where. Throws ScimFilterException on unsupported syntax.
///
/// Not implemented (deliberate): nested complex filters, OR expressions, NOT, complex attributes
/// (e.g., emails[type eq "work"]). Real-world IdP filters that go beyond the supported subset
/// can be added when an actual customer integration needs them.
/// </summary>
public static class ScimFilterParser
{
    public static Expression<Func<AppUser, bool>>? Parse(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;

        var tokens = Tokenize(filter);
        var pos = 0;
        var expr = ParseAndChain(tokens, ref pos);
        if (pos < tokens.Count)
            throw new ScimFilterException($"Unexpected token at position {pos}: {tokens[pos]}");
        return expr;
    }

    private static Expression<Func<AppUser, bool>> ParseAndChain(List<string> tokens, ref int pos)
    {
        var left = ParseComparison(tokens, ref pos);
        while (pos < tokens.Count && string.Equals(tokens[pos], "and", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseComparison(tokens, ref pos);
            left = AndAlso(left, right);
        }
        return left;
    }

    private static Expression<Func<AppUser, bool>> ParseComparison(List<string> tokens, ref int pos)
    {
        if (pos + 2 >= tokens.Count)
            throw new ScimFilterException("Filter expression truncated");

        var attr = tokens[pos++];
        var op = tokens[pos++].ToLowerInvariant();
        var rawValue = tokens[pos++];
        var value = StripQuotes(rawValue);

        return (attr.ToLowerInvariant(), op) switch
        {
            ("username", "eq") => u => u.UserName == value,
            ("username", "sw") => u => u.UserName != null && u.UserName.StartsWith(value),
            ("username", "co") => u => u.UserName != null && u.UserName.Contains(value),
            ("email", "eq") or ("emails.value", "eq") => u => u.Email == value,
            ("email", "sw") or ("emails.value", "sw") => u => u.Email != null && u.Email.StartsWith(value),
            ("active", "eq") => ParseBool(value)
                ? u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow
                : u => u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
            ("name.givenname", "eq") => u => u.FirstName == value,
            ("name.familyname", "eq") => u => u.LastName == value,
            ("externalid", "eq") => u => u.UserName == value,  // we map externalId → UserName for SCIM
            _ => throw new ScimFilterException($"Unsupported filter: {attr} {op}")
        };
    }

    private static bool ParseBool(string s) =>
        bool.TryParse(s, out var b) ? b : throw new ScimFilterException($"Invalid boolean value: {s}");

    private static string StripQuotes(string s) =>
        s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;

    private static Expression<Func<AppUser, bool>> AndAlso(
        Expression<Func<AppUser, bool>> a,
        Expression<Func<AppUser, bool>> b)
    {
        var p = Expression.Parameter(typeof(AppUser), "u");
        var aBody = new ParameterReplacer(a.Parameters[0], p).Visit(a.Body)!;
        var bBody = new ParameterReplacer(b.Parameters[0], p).Visit(b.Body)!;
        return Expression.Lambda<Func<AppUser, bool>>(Expression.AndAlso(aBody, bBody), p);
    }

    private class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i])) { i++; continue; }

            if (input[i] == '"')
            {
                var start = i++;
                while (i < input.Length && input[i] != '"')
                {
                    if (input[i] == '\\' && i + 1 < input.Length) i++;   // skip escaped char
                    i++;
                }
                if (i >= input.Length) throw new ScimFilterException("Unterminated quoted string");
                i++; // consume closing "
                tokens.Add(input.Substring(start, i - start));
                continue;
            }

            var s = i;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && input[i] != '"')
                i++;
            tokens.Add(input.Substring(s, i - s));
        }
        return tokens;
    }
}

public class ScimFilterException : Exception
{
    public ScimFilterException(string message) : base(message) { }
}
