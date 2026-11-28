using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The capture views of the ship Albatros (<c>docs/SHIP_PLAN.md</c>), staged through a save like a loaded game:
/// "ship-S" the ship on the slipway with S of its fourteen loads, "ship" afloat at its anchorage, "ship-night" at night,
/// "ship-far" from high over the yard, the buoys and the island, "ship-panel" and "ship-panel-S" the captain's panel,
/// "ship-sail" under sail on the way, "ship-island" at the island's pier, "ship-turtle" a turtle in its nest and
/// "ship-cabin" the deck with every treasure and the cat on the bow.
/// </summary>
internal static class ShipCaptures
{
    internal static void Stage(Game game, World world, Vehicle car, string view)
    {
        if (world.ShipPlace is not { } site || world.Ship is not { } ship || world.Captain is not { } captain)
            return;
        int dash = view.LastIndexOf('-');
        bool numbered = dash > 0 && int.TryParse(view[(dash + 1)..], out _);
        int stage = numbered ? Mathf.Clamp(int.Parse(view[(dash + 1)..]), 0, ShipPlans.Trips) : ShipPlans.Trips;
        string name = numbered ? view[..dash] : view;
        var data = game.CaptureProgress();
        data.Ship.Stage = stage;
        data.Ship.Anchor = false;
        data.Ship.Voyage = stage >= ShipPlans.Trips ? 4 : 0;
        data.Ship.Kinds =
            name == "ship-cabin" ? Enumerable.Repeat(1, ShipPlans.Kinds).ToArray()
            : stage >= ShipPlans.Trips ? new[] { 1, 0, 2, 0, 1, 0, 0, 1 }
            : new int[ShipPlans.Kinds];
        game.RestoreProgress(data);
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
                game._Process(.1);
        }
        void At(Vector3 p, float yaw) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), yaw);
        At(site.Return, site.Yaw + Mathf.Pi);
        if (name == "ship-night")
            game.Weather.Begin(WeatherKind.Night, instant: true);
        if (name == "ship-panel")
        {
            At(site.Talk + site.Along * 3, site.Yaw);
            game.OpenCaptain(captain);
        }
        if (name is "ship-sail" or "ship-island" or "ship-turtle")
        {
            game.ShipInputOverride = new Tasks.HelmInput(0, 1);
            At(site.Talk, site.Yaw);
            game.OpenCaptain(captain);
            game.BeginShipSail();
            ChallengeChecks.Solve(game);
            if (name == "ship-sail")
                Frames(110);
            else
                for (int i = 0; i < 3000 && game.ShipSailing; i++)
                    Frames(1);
            game.ShipInputOverride = null;
        }
        if (name == "ship-turtle" && world.TurtleIsland is { } island)
        {
            int nest = Enumerable.Range(0, ShipPlans.Nests).FirstOrDefault(island.Waiting);
            var by = island.NestAt(nest) - site.Seaward * 9;
            car.SetPose(island.GroundAt(by) + Vector3.Up * .3f, site.Yaw);
        }
        Frames(20);
    }

    /// <summary>Frames a view of the ship, or says it is not one; the panel and the voyage keep the game's camera.</summary>
    internal static bool Frame(FollowCamera camera, World world, string view)
    {
        if (!view.StartsWith("ship") || world.ShipPlace is not { } site || world.Ship is not { } ship)
            return false;
        if (view.StartsWith("ship-panel") || view == "ship-sail")
            return true;
        camera.SetProcess(false);
        if (view == "ship-far")
        {
            // From high over the coast before the yard: the beach, the anchorage, the buoys and the island far out.
            var middle = site.Anchorage.Lerp(site.Island, .45f);
            camera.GlobalPosition = site.Edge - site.Seaward * 120 + site.Along * 60 + Vector3.Up * 150;
            camera.LookAt(middle);
        }
        else if (view == "ship-island")
        {
            // From the sea before the island's pier: the ship at the pier, the beach, the palms and the nests.
            var eye = site.IslandBerth - site.Seaward * 26 + site.Along * 30;
            camera.GlobalPosition = eye with { Y = ValleyBorders.SeaLevel + 14 };
            camera.LookAt(site.Island);
        }
        else if (view == "ship-turtle" && world.TurtleIsland is { } island)
        {
            int nest = Enumerable.Range(0, ShipPlans.Nests).FirstOrDefault(island.Waiting);
            var at = island.NestAt(nest);
            camera.GlobalPosition = at - site.Seaward * 6 + site.Along * 4 + Vector3.Up * 4;
            camera.LookAt(at + Vector3.Up * .6f);
        }
        else if (view == "ship-cabin")
        {
            // Over the deck from behind the stern: the shelf of treasures, the cabin, the wheel and the cat on the bow.
            camera.GlobalPosition = ship.ToGlobal(new Vector3(7, 9, -20));
            camera.LookAt(ship.ToGlobal(new Vector3(0, Ship.DeckHeight + 1, 4)));
        }
        else if (ship.Afloat)
        {
            // From the edge of the yard over the beach and the pier to the ship at its anchorage.
            var eye = site.OnEdge(16, -6);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 9;
            camera.LookAt(ship.GlobalPosition + Vector3.Up * 4);
        }
        else
        {
            // From aside the slipway and above: the cradle, the scaffolding, the ship as far as it stands, and Hoblík.
            var eye = site.Local(26, -24);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 11;
            camera.LookAt(ship.GlobalPosition + Vector3.Up * 2);
        }
        return true;
    }
}
