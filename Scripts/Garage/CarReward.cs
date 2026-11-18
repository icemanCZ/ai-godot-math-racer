using System.Linq;
using System.Text.Json.Serialization;

namespace MathRacer.Garage;

/// <summary>
/// A garage item: a paint, a model, an accessory or, with <paramref name="IsTrail"/>, the stamps the wheels leave. The
/// trail flag is left out of a save while it is false, so the saves of items that were bought before trails look as
/// they always did.
/// </summary>
public sealed record CarReward(
    string Id,
    bool IsAccessory,
    bool IsModel = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsTrail = false
)
{
    public string ButtonName =>
        (
            IsTrail ? "Trail_"
            : IsModel ? "Model_"
            : IsAccessory ? "Accessory_"
            : "Look_"
        ) + Id;

    /// <summary>Garage price in coins. Special items earned by activities, such as the valley map, have none.</summary>
    public int? Price =>
        IsTrail ? (CarTrail.All.Any(t => t.Id == Id) ? TrailPrice : null)
        : IsModel ? (CarBody.All.Any(b => b.Id == Id) ? ModelPrice : null)
        : IsAccessory ? (CarAccessory.All.Any(a => a.Id == Id) ? AccessoryPrice : null)
        : CarLook.All.Any(l => l.Id == Id) ? PaintPrice
        : null;

    public const int PaintPrice = 15;
    public const int AccessoryPrice = 20;
    public const int ModelPrice = 40;
    public const int TrailPrice = 25;
}
