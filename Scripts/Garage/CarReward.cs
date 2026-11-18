using System.Linq;

namespace MathRacer.Garage;

public sealed record CarReward(string Id, bool IsAccessory, bool IsModel = false)
{
    public string ButtonName =>
        (
            IsModel ? "Model_"
            : IsAccessory ? "Accessory_"
            : "Look_"
        ) + Id;

    /// <summary>Garage price in coins. Special items earned by activities, such as the valley map, have none.</summary>
    public int? Price =>
        IsModel ? (CarBody.All.Any(b => b.Id == Id) ? ModelPrice : null)
        : IsAccessory ? (CarAccessory.All.Any(a => a.Id == Id) ? AccessoryPrice : null)
        : CarLook.All.Any(l => l.Id == Id) ? PaintPrice
        : null;

    public const int PaintPrice = 15;
    public const int AccessoryPrice = 20;
    public const int ModelPrice = 40;
}
