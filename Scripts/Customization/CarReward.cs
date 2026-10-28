using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public sealed record CarReward(string Id, bool IsAccessory, bool IsModel = false)
{
    public string ButtonName => (IsModel ? "Model_" : IsAccessory ? "Accessory_" : "Look_") + Id;

    public static IReadOnlyList<CarReward> Draw(Random random, IReadOnlyList<CarReward> previous)
    {
        // A paint, a vehicle and two distinct upgrades.
        CarReward[] result;
        do
        {
            int first = random.Next(CarAccessory.All.Count);
            int second = random.Next(CarAccessory.All.Count - 1);
            if (second >= first) second++;
            result = new[] {
                new CarReward(CarLook.All[random.Next(CarLook.All.Count)].Id, false),
                new CarReward(CarAccessory.All[first].Id, true),
                new CarReward(CarAccessory.All[second].Id, true),
                new CarReward(CarBody.All[random.Next(CarBody.All.Count)].Id, false, true)
            };
        } while (result.ToHashSet().SetEquals(previous));
        random.Shuffle(result);
        return Array.AsReadOnly(result);
    }
}
