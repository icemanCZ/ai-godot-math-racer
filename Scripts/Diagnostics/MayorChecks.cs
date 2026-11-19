using System;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Settlement;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The mayor on the village square and her shop: that every valley has her under the next actor ID, that she stands on
/// the square, that a decoration costs coins and appears (with its solid parts) only once it is bought, that the shop
/// panel is restored, and that the decorations are saved, validated and absent from a save that never had them.
/// </summary>
public partial class MayorChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public MayorChecks(Game game, World world, Vehicle car)
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

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("mayor-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"decorations restore: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"mayor-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The mayor ----
            var village = world.Village;
            var mayor = village.Mayor;
            Check(
                mayor.Role is MayorRole
                    && world.ActorId(mayor) == World.MayorId
                    && world.ActorAt(World.MayorId) == mayor
                    && World.IsMayor(World.MayorId)
                    && ActorIds.Mayor == 68,
                "the mayor is registered under the next free actor ID"
            );
            Check(mayor.DisplayName == CharacterCatalog.Mayor.Name, "the mayor has her name");
            var local = village.Square.Transform.AffineInverse() * mayor.Position;
            Check(Mathf.Abs(local.X) < 11 && Mathf.Abs(local.Z) < 6.5f, "the mayor stands on the village square");
            var hit = world
                .GetWorld3D()
                .DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(
                        mayor.Position + Vector3.Up * .25f,
                        mayor.Position + Vector3.Down * .25f
                    )
                );
            Check(
                hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - mayor.Position.Y) < .1f,
                "the mayor stands on the paving"
            );
            Check(village.IsReserved(mayor.Position), "the square, where the mayor stands, is reserved ground");

            // ---- The decorations ----
            var all = TownDecorations.All;
            Check(
                all.Count == 8 && all.Select(d => d.Id).Distinct().Count() == 8,
                "eight decorations, each with an id of its own"
            );
            Check(
                all.Select(d => d.Id)
                    .SequenceEqual(
                        new[] { "flowerbeds", "flags", "lanterns", "statue", "well", "clock", "tree", "arch" }
                    ),
                "the ids of the decorations are the ones saved games use"
            );
            foreach (var decoration in all)
            {
                Check(decoration.Price is >= 1 and <= 99, $"{decoration.Id} has a price");
                Check(decoration.Slots.Length > 0, $"{decoration.Id} has a place in the village");
                foreach (var slot in decoration.Slots)
                    Check(
                        village.IsReserved(village.SlotPosition(slot)),
                        $"{decoration.Id} stands on reserved ground ({slot.Place})"
                    );
                var nodes = village.DecorationNodes(decoration.Id);
                Check(nodes.Count() == decoration.Slots.Length, $"{decoration.Id} is built once for every place");
                Check(nodes.All(node => !node.Visible), $"{decoration.Id} is hidden before it is bought");
                Check(
                    nodes
                        .SelectMany(node => node.FindChildren("*", "CollisionShape3D", true, false))
                        .Cast<CollisionShape3D>()
                        .All(shape => shape.Disabled),
                    $"{decoration.Id} does not block the car before it is bought"
                );
            }
            var places = all.SelectMany(d => d.Slots).Select(slot => slot.Place).Distinct().ToArray();
            Check(places.Length >= 7, $"the decorations are spread over the village ({places.Length} places)");
            foreach (var slot in all.SelectMany(d => d.Slots))
            foreach (var actor in world.InteractionActors.Where(a => a.GetParent() == village))
                Check(
                    new Vector2(
                        village.SlotPosition(slot).X - actor.Position.X,
                        village.SlotPosition(slot).Z - actor.Position.Z
                    ).Length() > 2.2f,
                    $"a decoration at {slot.Place} does not stand on {actor.DisplayName}"
                );
            Check(village.OwnedDecorationIds.Length == 0, "a new game owns no decoration");

            // ---- The shop ----
            void Near() => car.SetPose(world.Ground.At(mayor.Position.X + 2, mayor.Position.Z + 2, .15f), 0);
            Near();
            game.OpenMayor(mayor);
            Check(game.State == GameState.MayorShop && !car.Active, "talking to the mayor opens her shop");
            Check(Has("Decoration_flowerbeds") && Has("MayorCoins"), "the shop shows the decorations and the coins");
            int before = game.Coins;
            game.BuyDecoration("flowerbeds");
            Check(
                game.Coins == before && !village.OwnsDecoration("flowerbeds"),
                "a decoration is not bought without the coins"
            );
            game.AddCoins(TownDecorations.TotalPrice);
            before = game.Coins;
            var bed = TownDecorations.Find("flowerbeds")!;
            game.BuyDecoration("flowerbeds");
            Check(
                game.Coins == before - bed.Price && village.OwnsDecoration("flowerbeds"),
                "the coins buy the flower beds"
            );
            var bedNodes = village.DecorationNodes("flowerbeds");
            Check(bedNodes.All(node => node.Visible), "the flower beds stand on the square");
            Check(
                bedNodes
                    .SelectMany(node => node.FindChildren("*", "CollisionShape3D", true, false))
                    .Cast<CollisionShape3D>()
                    .All(shape => !shape.Disabled),
                "the flower beds are solid once they stand"
            );
            Check(game.State == GameState.MayorShop, "the shop stays open after a purchase");
            game.BuyDecoration("flowerbeds");
            Check(game.Coins == before - bed.Price, "a decoration is bought only once");
            game.BuyDecoration("no-such-decoration");
            Check(
                game.Coins == before - bed.Price && village.OwnedDecorationIds.Length == 1,
                "an unknown id buys nothing"
            );
            RoundTrip("flower beds in the open shop");

            game._Process(.1);
            game.BuyDecoration("arch");
            Check(
                village.OwnedDecorationIds.SequenceEqual(new[] { "flowerbeds", "arch" }),
                "owned ids keep the order of the shop"
            );
            // The pause and the shop's own panel come back from a save.
            RoundTrip("two decorations");
            Check(
                game.State == GameState.MayorShop && Has("Decoration_well"),
                "the shop panel is shown again after loading"
            );
            foreach (var decoration in all)
                game.BuyDecoration(decoration.Id);
            Check(village.OwnedDecorationIds.Length == all.Count, "every decoration can be bought");
            Check(Has("MayorCoins"), "the full shop is shown");
            RoundTrip("every decoration");
            game.OpenMayor(mayor);
            var back = game.FindChild("Back", true, false) as Button;
            Check(back != null, "the shop has a Back button");
            back!.EmitSignal(BaseButton.SignalName.Pressed);
            Check(game.State == GameState.Exploring, "Back leaves the shop");

            // ---- What a save may not say ----
            Rejected(data => data.Decorations.Owned = new[] { "no-such-decoration" }, "an unknown decoration");
            Rejected(data => data.Decorations.Owned = new[] { "arch", "arch" }, "a decoration owned twice");
            Rejected(data => data.Decorations.Owned = null!, "no list of decorations");
            Rejected(
                data =>
                {
                    data.State = GameState.MayorShop;
                    data.Nearby = World.SorcererId;
                },
                "the mayor's shop beside someone else"
            );
            Rejected(data => data.Nearby = World.MayorId + 1, "a character beyond the mayor");

            // A file made before there were decorations loads with none.
            var plain = game.CaptureProgress();
            var json = JsonSerializer.SerializeToNode(plain)!.AsObject();
            json.Remove("Decorations");
            string oldFile = DiagnosticArtifacts.PathFor("mayor-checks/before-decorations.json");
            System.IO.File.WriteAllText(oldFile, json.ToJsonString());
            var old = SaveStore.Read(oldFile);
            Check(
                old != null && old.Decorations.Owned.Length == 0,
                "a save from before the decorations loads without any"
            );
            game.RestoreProgress(old!);
            Check(village.OwnedDecorationIds.Length == 0, "loading such a save takes the decorations off the square");

            GD.Print("MAYOR_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
