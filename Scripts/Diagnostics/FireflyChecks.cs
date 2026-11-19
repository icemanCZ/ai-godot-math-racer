using System;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The witch's firefly hunt: the collection of stars and its counting, that the hunt is switched on only at the witch with a
/// button of its own and only on a starry night, that fireflies hover near the car and are caught by driving into them, that the
/// lantern shines brighter with every one, that ten light a star in the sky and the pictures are drawn one after another, that
/// the morning ends the hunt, that the collection is saved and a complete one needs no more fireflies, and the damaged saves.
/// </summary>
public partial class FireflyChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public FireflyChecks(Game game, World world, Vehicle car)
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

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private void CarAt(Vector3 spot) => car.SetPose(world.Ground.At(spot.X, spot.Z, .15f), 0);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var hunt = game.Fireflies;
            var weather = game.Weather;
            var witch = world.Witch;
            var sparks = world.Sparks;
            string path = DiagnosticArtifacts.PathFor("firefly-checks/progress.json");
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"firefly-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The collection and its counting ----
            Check(
                FireflyHunt.TotalStars == Constellation.All.Sum(picture => picture.Stars.Length)
                    && FireflyHunt.Limit == FireflyHunt.TotalStars * FireflyHunt.PerStar
                    && FireflyHunt.PerStar == 10,
                "ten fireflies light a star and the collection holds every star of every picture"
            );
            var counting = new FireflyHunt();
            Check(!counting.Catch() && counting.Caught == 0, "nothing is caught before the lantern is lit");
            Check(counting.Start() && counting.Active && !counting.Start(), "the lantern is lit once");
            for (int i = 1; i < FireflyHunt.PerStar; i++)
                Check(!counting.Catch() && counting.TowardsStar == i, $"firefly {i} lights no star yet");
            Check(counting.Lantern > .85f, "the lantern is nearly full with nine fireflies in it");
            Check(
                counting.Catch() && counting.Stars == 1 && counting.TowardsStar == 0,
                "the tenth firefly lights a star"
            );
            Check(counting.Lantern == 0, "the lantern starts again for the next star");
            var first = Constellation.All[0].Stars.Length;
            Check(
                FireflyHunt.StarAt(0) == (0, 0)
                    && FireflyHunt.StarAt(first - 1) == (0, first - 1)
                    && FireflyHunt.StarAt(first) == (1, 0)
                    && FireflyHunt.StarAt(FireflyHunt.TotalStars - 1).Picture == Constellation.All.Count - 1,
                "the stars light one picture after another in the order of its points"
            );
            counting.Restore(first * FireflyHunt.PerStar + 20);
            var litStars = counting.LitStars();
            Check(
                litStars[0] == first
                    && litStars[1] == 2
                    && litStars.Skip(2).All(lit => lit == 0)
                    && counting.Finished == 1,
                "a restored count shows the first picture drawn to the end and two stars of the second"
            );
            Check(!counting.Active, "no hunt is on after a restore");
            counting.Restore(FireflyHunt.Limit + 500);
            Check(
                counting.Complete && counting.Caught == FireflyHunt.Limit && !counting.Start() && !counting.Catch(),
                "a complete collection needs no more fireflies"
            );

            // ---- By day the witch asks the weather question, as before ----
            var driving = game.CaptureProgress();
            game.RestoreProgress(driving);
            Check(hunt.Caught == 0 && !hunt.Active, "a new game has caught no firefly");
            CarAt(witch.Position + witch.Basis.Z * 3);
            Frames(2);
            game.Interact();
            Check(
                game.State == GameState.Challenge && !Has("StartFireflyHunt"),
                "by day the witch asks her weather question, with no fireflies about"
            );
            game.RestoreProgress(driving);

            // ---- At night the witch has a panel with a button of its own ----
            weather.Begin(WeatherKind.Night, instant: true);
            weather.Tick(1);
            Check(weather.StarsOut, "the witch's night brings the stars");
            CarAt(witch.Position + witch.Basis.Z * 3);
            Frames(2);
            game.Interact();
            Check(game.State == GameState.FireflyStatus, $"at night the witch opens her panel (state {game.State})");
            Check(
                Has("StartFireflyHunt") && Has("ChangeWeather") && Has("Back"),
                "at night the witch offers the firefly hunt, the weather and the way back"
            );
            Check(
                Has("StarCollection") && Enumerable.Range(0, Constellation.All.Count).All(i => Has("Stars" + i)),
                "her panel shows every picture of the collection"
            );
            Check(!hunt.Active && !sparks.Shown, "she lends nothing yet");
            var panelSave = game.CaptureProgress();
            Check(
                SaveStore.Write(panelSave, path) && SaveStore.Read(path)?.State == GameState.FireflyStatus,
                "her panel can be saved"
            );
            game.RestoreProgress(SaveStore.Read(path)!);
            Check(
                game.State == GameState.FireflyStatus && Has("StartFireflyHunt"),
                "her panel comes back after a load"
            );
            game.BeginWeatherQuiz();
            Check(game.State == GameState.Challenge, "the other button leads to the weather question");
            game.RestoreProgress(SaveStore.Read(path)!);
            // The save keeps the hour, so the same panel saved by day.
            var byDay = SaveStore.Read(path)!;
            byDay.Day.Clock = Weather.StartOf(DayTime.Day);
            game.RestoreProgress(byDay);
            weather.Tick(1);
            Check(
                game.State == GameState.FireflyStatus && !Has("StartFireflyHunt") && Has("ChangeWeather"),
                "by day the same panel has no button for the hunt"
            );
            game.BeginFireflyHunt();
            Check(!hunt.Active, "the hunt cannot be started by day");
            game._Input(new InputEventAction { Action = "back", Pressed = true });
            Check(game.State == GameState.Exploring, "the red X leaves the witch's panel");

            // ---- The lantern is lit ----
            weather.Begin(WeatherKind.Night, instant: true);
            weather.Tick(1);
            CarAt(witch.Position + witch.Basis.Z * 3);
            Frames(2);
            game.Interact();
            game.BeginFireflyHunt();
            Check(
                game.State == GameState.Exploring && hunt.Active,
                "the green check mark lights the lantern and sends the child out"
            );
            var open = world.TreasureSpot(0);
            CarAt(open);
            Frames(30);
            var lantern = car.GetNode<Node3D>("Lantern");
            var light = lantern.GetNode<OmniLight3D>("Glow/LanternLight");
            Check(sparks.Shown && lantern.Visible && light.Visible, "the lantern hangs on the car and shines");
            float dim = sparks.LanternLevel;
            Check(sparks.Hovering == FireflyField.Count, "the fireflies are about");
            for (int i = 0; i < FireflyField.Count; i++)
            {
                float distance = Flat(sparks.PositionOf(i), car.GlobalPosition);
                Check(distance > 8 && distance < 45, $"firefly {i + 1} hovers a short drive away ({distance:0} m)");
            }
            for (int i = 0; i < 40; i++)
            {
                var spot = world.FireflySpot(car.GlobalPosition, new Random(i))
                    ?? throw new InvalidOperationException("no spot");
                float distance = Flat(spot, car.GlobalPosition);
                Check(
                    world.IsFireflySpotFree(spot) && distance >= 14 && distance <= 38.01f,
                    $"a firefly's place {i + 1} is free ground 14 to 38 m away"
                );
            }

            // ---- Catching ----
            void CatchOne()
            {
                sparks.Place(0, world.Ground.At(car.GlobalPosition.X, car.GlobalPosition.Z));
                Frames(1);
            }
            int before = hunt.Caught;
            CatchOne();
            Check(hunt.Caught == before + 1, "driving into a firefly catches it");
            Frames(8);
            Check(sparks.Hovering == FireflyField.Count, "a new firefly appears for the one that was caught");
            for (int i = 0; i < 4; i++)
                CatchOne();
            Frames(15);
            Check(
                hunt.Caught == 5 && sparks.LanternLevel > dim + .2f,
                "the more fireflies are caught, the brighter the lantern shines"
            );
            float half = light.LightEnergy;
            for (int i = 0; i < 4; i++)
                CatchOne();
            Frames(15);
            Check(
                light.LightEnergy > half && hunt.TowardsStar == 9,
                "the lantern is brightest with nine fireflies in it"
            );
            Check(world.Atmosphere.ShiningStars == 0, "no star shines yet");
            CatchOne();
            Frames(2);
            Check(hunt.Stars == 1 && hunt.Caught == 10, "the tenth firefly lights the first star");
            Check(world.Atmosphere.ShiningStars == 1, "the star shines in the sky");
            Check(game.CaptureProgress().Fireflies.Caught == 10, "the number caught is in the save");
            Frames(30);
            Check(sparks.LanternLevel < .3f && light.LightEnergy < half, "the lantern dims again for the next star");

            // ---- The collection is saved ----
            Check(SaveStore.Write(game.CaptureProgress(), path), "a save with a star is written");
            var withStar = SaveStore.Read(path) ?? throw new InvalidOperationException("save refused");
            game.RestoreProgress(withStar);
            Check(
                hunt.Caught == 10 && !hunt.Active && hunt.Stars == 1,
                "the star is back after a load, and no hunt is on"
            );
            Frames(12);
            Check(
                world.Atmosphere.ShiningStars == 1 && !sparks.Shown,
                "the sky shows the star again, and no lantern hangs on the car"
            );
            // The clock of the day has gone on for the frames since the load; everything else is the same.
            var again = game.CaptureProgress();
            Check(
                again.Day.Clock >= withStar.Day.Clock && again.Day.Clock - withStar.Day.Clock < 2,
                "the clock of the day goes on from the hour of the save"
            );
            again.Day.Clock = withStar.Day.Clock;
            Check(
                JsonSerializer.Serialize(withStar) == JsonSerializer.Serialize(again),
                "restoring and capturing again gives the same save"
            );

            // ---- A picture is finished ----
            hunt.Restore((first - 1) * FireflyHunt.PerStar + 9);
            hunt.Start();
            CarAt(open);
            Frames(30);
            CatchOne();
            Frames(2);
            Check(
                hunt.Stars == first && hunt.Finished == 1 && world.Atmosphere.ShiningStars == first,
                "the last star of the first picture finishes it"
            );
            Check(FireflyHunt.StarAt(hunt.Stars).Picture == 1, "the next star belongs to the next picture");

            // ---- The morning ends the hunt ----
            weather.SetTime(DayTime.Day);
            weather.Tick(30);
            Frames(3);
            Check(!hunt.Active && !weather.StarsOut, "the morning puts the hunt out");
            Frames(20);
            Check(!sparks.Shown && sparks.Hovering == 0, "the lantern and the fireflies are gone");
            Check(hunt.Caught == first * FireflyHunt.PerStar, "the fireflies that were caught stay caught");

            // ---- Every star ----
            weather.Begin(WeatherKind.Night, instant: true);
            weather.Tick(1);
            hunt.Restore(FireflyHunt.Limit - 1);
            hunt.Start();
            CarAt(open);
            Frames(30);
            CatchOne();
            Frames(2);
            Check(
                hunt.Complete && !hunt.Active && world.Atmosphere.ShiningStars == FireflyHunt.TotalStars,
                "the last star lights the whole collection and puts the lantern out"
            );
            CarAt(witch.Position + witch.Basis.Z * 3);
            Frames(2);
            game.Interact();
            Check(
                game.State == GameState.FireflyStatus && !Has("StartFireflyHunt") && Has("ChangeWeather"),
                "with every star lit the witch offers no more fireflies"
            );
            game.BeginFireflyHunt();
            Check(!hunt.Active, "a complete collection starts no hunt");
            Check(
                SaveStore.Write(game.CaptureProgress(), path) && SaveStore.Read(path) != null,
                "a complete collection is saved"
            );

            // ---- Old and damaged saves ----
            hunt.Restore(0);
            string json = JsonSerializer.Serialize(game.CaptureProgress());
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            node.Remove("Fireflies");
            string oldFile = DiagnosticArtifacts.PathFor("firefly-checks/old.json");
            System.IO.File.WriteAllText(oldFile, node.ToJsonString());
            Check(
                SaveStore.Read(oldFile) is { Fireflies.Caught: 0 },
                "a file written before the fireflies loads with none caught"
            );
            Rejected(data => data.Fireflies.Caught = -1, "a negative number of fireflies");
            Rejected(data => data.Fireflies.Caught = FireflyHunt.Limit + 1, "more fireflies than the collection holds");
            Rejected(data => data.Fireflies = null!, "no firefly section");
            Rejected(
                data =>
                {
                    data.State = GameState.FireflyStatus;
                    data.Nearby = World.MayorId;
                },
                "the witch's night panel beside the mayor"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.FireflyStatus;
                    data.Nearby = -1;
                },
                "the witch's night panel without the witch"
            );
            GD.Print("FIREFLY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
