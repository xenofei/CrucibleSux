using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace CrucibleSux.Core;

public enum AoeKind
{
    /// <summary>Single target, or a shape the Action row does not describe.</summary>
    None,
    Circle,
    Donut,
    Cone,
    Rect,
    Cross,
}

/// <summary>
/// An enemy attack's area, built from its Action row: CastType picks the kind, EffectRange the size, XAxisModifier a
/// rect's full width, and the Omen path a cone's angle (<c>gl_fan120…</c>) or a donut's inner radius
/// (<c>gl_sircle_4008…</c> is 8 to 40). The caster's and the player's hitboxes are not added: the margin is a diagnosis
/// aid, not a dodging rule. Positions are the game's X and Z; a rotation of 0 faces +Z.
/// </summary>
public readonly partial record struct AoeShape(AoeKind Kind, float Range, float Inner = 0f, float HalfAngleDegrees = 0f, float Width = 0f)
{
    private const float DefaultConeDegrees = 90f;

    public static AoeShape FromAction(byte castType, float effectRange, float xAxisModifier, string? omenPath)
    {
        if (effectRange <= 0)
            return default;
        return castType switch
        {
            2 or 5 or 6 or 7 => new(AoeKind.Circle, effectRange),
            3 or 13 => new(AoeKind.Cone, effectRange, HalfAngleDegrees: ConeDegrees(omenPath) / 2f),
            4 or 8 or 12 => new(AoeKind.Rect, effectRange, Width: xAxisModifier),
            10 => new(AoeKind.Donut, effectRange, Inner: DonutInner(omenPath)),
            11 => new(AoeKind.Cross, effectRange, Width: xAxisModifier),
            _ => default,
        };
    }

    /// <summary>
    /// How far inside the area <paramref name="point"/> is, in yalms: positive inside, negative outside, NaN for
    /// <see cref="AoeKind.None"/>.
    /// </summary>
    public float Margin(Vector2 origin, float rotation, Vector2 point)
    {
        var d = point - origin;
        float dist = d.Length();
        var forward = new Vector2(MathF.Sin(rotation), MathF.Cos(rotation));
        var right = new Vector2(forward.Y, -forward.X);
        float f = Vector2.Dot(d, forward);
        float s = Vector2.Dot(d, right);
        switch (this.Kind)
        {
            case AoeKind.Circle:
                return this.Range - dist;
            case AoeKind.Donut:
                return MathF.Min(dist - this.Inner, this.Range - dist);
            case AoeKind.Cone:
                {
                    float radial = this.Range - dist;
                    if (dist < 0.001f)
                        return radial;
                    float angle = MathF.Acos(Math.Clamp(f / dist, -1f, 1f)) * 180f / MathF.PI;
                    float spare = (this.HalfAngleDegrees - angle) * MathF.PI / 180f;
                    float lateral = dist * MathF.Sin(Math.Clamp(spare, -MathF.PI / 2f, MathF.PI / 2f));
                    return MathF.Min(radial, lateral);
                }
            case AoeKind.Rect:
                return MathF.Min(MathF.Min(f, this.Range - f), this.Width / 2f - MathF.Abs(s));
            case AoeKind.Cross:
                {
                    float along = MathF.Min(this.Range - MathF.Abs(f), this.Width / 2f - MathF.Abs(s));
                    float across = MathF.Min(this.Range - MathF.Abs(s), this.Width / 2f - MathF.Abs(f));
                    return MathF.Max(along, across);
                }
            default:
                return float.NaN;
        }
    }

    public override string ToString() => this.Kind switch
    {
        AoeKind.Circle => string.Create(CultureInfo.InvariantCulture, $"circle r{this.Range:0.#}"),
        AoeKind.Donut => string.Create(CultureInfo.InvariantCulture, $"donut {this.Inner:0.#}-{this.Range:0.#}"),
        AoeKind.Cone => string.Create(CultureInfo.InvariantCulture, $"cone {this.HalfAngleDegrees * 2:0}° r{this.Range:0.#}"),
        AoeKind.Rect => string.Create(CultureInfo.InvariantCulture, $"rect {this.Range:0.#}x{this.Width:0.#}"),
        AoeKind.Cross => string.Create(CultureInfo.InvariantCulture, $"cross {this.Range:0.#}x{this.Width:0.#}"),
        _ => "no area",
    };

    private static float ConeDegrees(string? omen) =>
        omen is not null && FanRegex().Match(omen) is { Success: true } m ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : DefaultConeDegrees;

    private static float DonutInner(string? omen) =>
        omen is not null && SircleRegex().Match(omen) is { Success: true } m ? float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0f;

    [GeneratedRegex(@"fan(\d{3})")]
    private static partial Regex FanRegex();

    [GeneratedRegex(@"sircle_(\d\d)(\d\d)")]
    private static partial Regex SircleRegex();
}
