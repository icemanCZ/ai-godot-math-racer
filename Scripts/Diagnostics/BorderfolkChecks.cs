using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The new people of the valley (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phases 1 and 2): the four keepers of the borders
/// at the ends of the roads to them, on dry and fairly level ground, each with a homestead whose ground the plants, the
/// grass and the wayside things keep off and a road route from the village short enough to drive; the places of the
/// lighthouse and the coast quarry; and the post office at the edge of the village, beside a road, on ground of its own,
/// with the postman in front of it whom a car on the road can reach.
/// </summary>
public partial class BorderfolkChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private readonly Node3D arrows = new() { Name = "BorderfolkArrows" };

    public BorderfolkChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            world.AddChild(arrows);
            CheckKeepers();
            CheckCoast();
            CheckPostOffice();
            GD.Print("BORDERFOLK_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckKeepers()
    {
        Check(world.Keepers.All(keeper => keeper != null), "a keeper lives at each of the four borders");
        var samples = world.Routes.Samples;
        int start = RouteGuide.NearestSample(world, world.Village.Center);
        for (int border = 0; border < 4; border++)
        {
            var keeper = world.Keepers[border]!;
            var site = world.BorderHomes[border]!;
            int id = ActorIds.FirstBorderKeeper + border;
            Check(
                world.ActorId(keeper) == id
                    && keeper.Role is BorderKeeperRole role
                    && (int)role.Border == border
                    && keeper.DisplayName == CharacterCatalog.BorderKeepers[border].Name,
                $"{keeper.DisplayName} keeps the {(Border)border} border under ID {id}"
            );
            var p = keeper.Position;
            Check(
                ValleyBounds.Contains(p.X, p.Z, 8)
                    && world.Ground.Height(p.X, p.Z) > ValleyBorders.SeaLevel + 1
                    && Mathf.Abs(p.Y - site.Road.Y) < 2.5f,
                $"the keeper of the {(Border)border} stands inside the valley, on dry ground near the road's end"
            );
            Check(world.Homesteads[border] != null, $"the homestead of the {(Border)border} stands");
            int inZone = 0,
                bare = 0;
            for (float along = BorderHomeSite.YardStart; along <= BorderHomeSite.YardEnd; along += 3)
            for (float across = -BorderHomeSite.HalfWidth; across <= BorderHomeSite.HalfWidth; across += 3)
            {
                var q = world.Ground.At(site.Local(across, along).X, site.Local(across, along).Z);
                if (world.InBorderfolkZone(q))
                    inZone++;
                if (world.Plants.OutlandCleared?.Invoke(q) == true && !world.Grass.CanGrow(q))
                    bare++;
            }
            Check(inZone > 30 && bare == inZone, $"no plant and no grass grows in the yard of the {(Border)border}");
            // The road route from the village, as the post's arrows show it.
            foreach (var child in arrows.GetChildren())
                child.Free();
            var toward = samples[Math.Min(samples.Count - 1, start + 2)] - samples[start];
            car.SetPose(world.Ground.At(samples[start].X, samples[start].Z, .15f), Mathf.Atan2(-toward.X, -toward.Z));
            var guide = new RouteGuide(world, car, arrows, () => keeper.Position);
            guide.Retarget();
            guide.CreateArrows();
            float length = 0;
            var route = guide.RoutePoints;
            for (int i = 1; i < route.Count; i++)
                length += Flat(route[i - 1], route[i]);
            Check(
                length < RouteChecks.LongestRoute,
                $"the keeper of the {(Border)border} is {length:0} m from the village by road"
            );
            // A car at the end of the road reaches the keeper, and the keeper's panel opens.
            car.SetPose(world.Ground.At(site.Road.X, site.Road.Z, .15f), Mathf.Atan2(-site.Inward.X, -site.Inward.Z));
            game._Process(.1);
            Check(
                game.NearbyActor == keeper,
                $"a car at the end of the road is beside the keeper of the {(Border)border}"
            );
            game.Interact();
            // The shepherd shows his fold from the start in a valley with his sheep (docs/SHEEP_PLAN.md).
            bool fold = border == (int)Border.Mountains && world.SheepFlock != null;
            Check(
                fold
                    ? game.State == GameState.ShepherdStatus && Has("SheepFold")
                    : game.State == GameState.KeeperStatus && Has("KeeperStamps"),
                $"talking to the keeper of the {(Border)border} opens the keeper's panel"
            );
            game.FinishInteraction();
        }
    }

    private void CheckCoast()
    {
        var light = world.LighthousePlace;
        var quarry = world.QuarryPlace;
        Check(light != null && quarry != null, "the lighthouse and the coast quarry have their places");
        Check(
            world.Ground.Height(light!.Foot.X, light.Foot.Z) > ValleyBorders.SeaLevel
                && Flat(light.Foot, world.BorderHomes[(int)Border.Sea]!.Road) > BorderHomeSite.YardEnd,
            "the lighthouse stands on dry ground beyond the keeper's yard"
        );
        Check(
            ValleyBounds.Contains(quarry!.Position.X, quarry.Position.Z, 8)
                && world.OutlandRoutes.Distance(quarry.Position) > CoastQuarrySite.Radius
                && world.Routes.Distance(quarry.Position) > 15,
            "the coast quarry stands beside the road to the sea, off every road"
        );
        Check(
            world.Lighthouse != null && world.NightBoat != null,
            "the lighthouse and the boat of the night are built"
        );
    }

    private void CheckPostOffice()
    {
        var site = world.PostOfficePlace;
        var postman = world.Postman;
        Check(site != null && world.PostOffice != null, "the post office has its place at the edge of the village");
        Check(
            world.ActorId(postman) == World.PostmanId
                && postman.Role is PostmanRole
                && postman.DisplayName == CharacterCatalog.Postman.Name
                && ActorIds.Postman == 78,
            "the postman is registered under the last actor ID"
        );
        Check(
            world.Routes.Distance(postman.Position) < Settings.InteractionRange - 2,
            "a car on the road can reach the postman"
        );
        int total = 0,
            blocked = 0;
        for (float along = 1.5f; along <= PostOfficeSite.AlongMax - 1.5f; along += 1)
        for (float across = -PostOfficeSite.HouseWidth / 2; across <= PostOfficeSite.HouseWidth / 2; across += 1)
        {
            var q = site!.Local(across, along);
            q = world.Ground.At(q.X, q.Z);
            total++;
            if (
                !world.InPostOfficeZone(q)
                || world.Routes.Distance(q) < 4
                || world.Village.IsReserved(q)
                || world.Plants.NearTrunk(q, 1)
                || world.Grass.CanGrow(q)
                || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 2
            )
                blocked++;
        }
        Check(
            total > 40 && blocked == 0,
            "the post office stands on cleared ground of its own, off the roads and the village"
        );
        Check(
            world.Hidden.Friends.All(friend => !world.InPostOfficeZone(friend.GlobalPosition))
                && world.InteractionActors.All(actor =>
                    actor == postman || Flat(actor.Position, postman.Position) > Settings.InteractionRange
                ),
            "no small friend hides at the post office and no other character stands within reach of the postman"
        );
        float farthest = 0;
        foreach (float along in new[] { PostOfficeSite.AlongMin, PostOfficeSite.AlongMax })
        foreach (float across in new[] { -PostOfficeSite.AcrossMax, PostOfficeSite.AcrossMax })
            farthest = Math.Max(farthest, world.Village.PlotDistance(site!.Local(across, along)));
        Check(
            farthest < Village.TaskAnimalClearance - 5,
            $"the post office is {Flat(site!.Front, world.Village.Center):0} m from the middle of the village and within "
                + $"{farthest:0} m of a house, nearer than a hidden friend, a treasure or a firefly may be"
        );
        car.SetPose(world.Ground.At(site.Road.X, site.Road.Z, .15f), Mathf.Atan2(-site.Inward.X, -site.Inward.Z));
        game._Process(.1);
        Check(game.NearbyActor == postman, "a car on the road in front of the post office is beside the postman");
        game.Interact();
        Check(game.State == GameState.PostStatus && Has("StampAlbum"), "talking to the postman opens the album");
        game.FinishInteraction();
    }
}
