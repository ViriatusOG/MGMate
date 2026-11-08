using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MGMate.Api.Services;

/// <summary>
/// The few shapes of Home Assistant template the gateway writes into its discovery configs, worked
/// out without a template engine: a value as it is or as a whole number, a field of a JSON value,
/// and an if/elif/else on what the value equals. Anything else is not understood, and the Debug
/// page then shows the raw value, or offers no control.
/// </summary>
public static partial class GatewayTemplate
{
    // {{ value }}, {{ value | int }}, {{ value_json["key"] }}, {{ value_json.key | int(0) }}
    [GeneratedRegex("""^\{\{\s*(?<src>value_json(?:\[\s*["'](?<key>[^"']+)["']\s*\]|\.(?<key>\w+))|value)\s*(?<int>\|\s*int(?:\(\s*-?\d+\s*\))?\s*)?\}\}$""")]
    private static partial Regex Expression();

    // {% set v = value | int %} at the start, which turns the comparisons below into numbers.
    [GeneratedRegex("""^\{%-?\s*set\s+(?<var>\w+)\s*=\s*value\s*\|\s*int\s*-?%\}""")]
    private static partial Regex SetInt();

    // {% if X == "a" %}A{% elif X == 1 %}B{% else %}C{% endif %}
    [GeneratedRegex("""\{%-?\s*(?<kw>if|elif)\s+(?<var>\w+)\s*==\s*(?:"(?<lit>[^"]*)"|'(?<lit>[^']*)'|(?<num>-?\d+))\s*-?%\}(?<out>[^{]*)|\{%-?\s*else\s*-?%\}(?<else>[^{]*)|\{%-?\s*endif\s*-?%\}""")]
    private static partial Regex Branch();

    /// <summary>The template applied to <paramref name="value"/>, or null when its shape is not one understood here.</summary>
    public static string? Render(string? template, string value)
    {
        if (string.IsNullOrWhiteSpace(template)) return value;
        template = template.Trim();

        if (Expression().Match(template) is { Success: true } expr)
        {
            var result = value;
            if (expr.Groups["key"].Success)
            {
                result = JsonField(value, expr.Groups["key"].Value);
                if (result is null) return null;
            }
            if (expr.Groups["int"].Success)
                return double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                    ? ((long)Math.Truncate(n)).ToString(CultureInfo.InvariantCulture)
                    : null;
            return result;
        }

        return RenderBranches(template, value);
    }

    private static string? RenderBranches(string template, string value)
    {
        var variable = "value";
        double? number = null;
        var rest = template;
        if (SetInt().Match(template) is { Success: true } set)
        {
            variable = set.Groups["var"].Value;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
            number = Math.Truncate(n);
            rest = template[set.Length..];
        }

        // The whole template has to be branches, end to end, or it is some other shape.
        var at = 0;
        string? chosen = null;
        var sawIf = false;
        var sawEnd = false;
        foreach (Match m in Branch().Matches(rest))
        {
            if (m.Index != at || sawEnd) return null;
            at = m.Index + m.Length;

            if (m.Groups["kw"].Success)
            {
                if (m.Groups["kw"].Value == "if") { if (sawIf) return null; sawIf = true; }
                else if (!sawIf) return null;
                if (m.Groups["var"].Value != variable) return null;

                bool equal = m.Groups["num"].Success
                    ? (number ?? (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN))
                        == double.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture)
                    : number is null && value == m.Groups["lit"].Value;
                if (chosen is null && equal) chosen = m.Groups["out"].Value;
            }
            else if (m.Groups["else"].Success)
            {
                if (!sawIf) return null;
                chosen ??= m.Groups["else"].Value;
            }
            else
            {
                sawEnd = true;
            }
        }

        if (!sawIf || !sawEnd || at != rest.Length) return null;
        return chosen ?? string.Empty;
    }

    private static string? JsonField(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty(key, out var el)) return null;
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
