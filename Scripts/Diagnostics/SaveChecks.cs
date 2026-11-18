using System;
using System.IO;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

public partial class SaveChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public SaveChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    /// <summary>
    /// A save the game cannot load, or one written by an older version, is copied aside before the first autosave
    /// of the new game can replace it. The copies are made once and are never overwritten.
    /// </summary>
    private void CheckPreservedCopies()
    {
        string directory = Path.GetDirectoryName(DiagnosticArtifacts.PathFor("save-checks/preserve/x"))!;
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "progress.json");
        string[] Copies() =>
            Directory
                .GetFiles(directory)
                .Select(file => Path.GetFileName(file))
                .Where(name => name != "progress.json")
                .Order()
                .ToArray();

        // A current save needs no copy.
        Check(SaveStore.Write(game.CaptureProgress(), path), "current save written");
        SaveStore.Preserve(path);
        Check(Copies().Length == 0, "a current save is not copied");

        // A save of an older version is kept as progress.vN.bak, once, even if the file changes afterwards.
        File.WriteAllText(path, LegacySaves.FromCurrent(game.CaptureProgress(), 2).ToJsonString());
        SaveStore.Preserve(path);
        Check(
            Copies().Contains("progress.v2.bak")
                && File.ReadAllText(Path.Combine(directory, "progress.v2.bak")) == File.ReadAllText(path),
            "a save of an older version is copied to progress.v2.bak"
        );
        string kept = File.ReadAllText(Path.Combine(directory, "progress.v2.bak"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Coins\":", "\"Coins\":1"));
        SaveStore.Preserve(path);
        Check(
            File.ReadAllText(Path.Combine(directory, "progress.v2.bak")) == kept,
            "an existing progress.v2.bak is never overwritten"
        );

        // A file that cannot be loaded at all is kept under a name made from its content.
        File.Delete(Path.Combine(directory, "progress.v2.bak"));
        File.WriteAllText(path, "{\"Version\":3,\"Seed\":-5}");
        File.WriteAllText(path + ".bak", "{}");
        SaveStore.Preserve(path);
        var unreadable = Copies().Where(f => f.StartsWith("progress.nenacteno-")).ToArray();
        Check(
            unreadable.Length == 1
                && File.ReadAllText(Path.Combine(directory, unreadable[0])) == File.ReadAllText(path),
            "a save that cannot be loaded is copied to progress.nenacteno-HASH.json"
        );
        SaveStore.Preserve(path);
        Check(
            Copies().Count(f => f.StartsWith("progress.nenacteno-")) == 1,
            "the same unreadable save is not copied again at the next start"
        );

        // The primary file is damaged but the backup works: the damaged file is still set aside.
        File.Delete(Path.Combine(directory, unreadable[0]));
        Check(SaveStore.Write(game.CaptureProgress(), path + ".bak"), "backup written");
        File.WriteAllText(path, "{broken");
        SaveStore.Preserve(path);
        Check(
            Copies().Any(f => f.StartsWith("progress.nenacteno-")),
            "a damaged primary file is set aside even when the backup can be loaded"
        );
    }

    public override void _Ready()
    {
        try
        {
            string path = DiagnosticArtifacts.PathFor("save-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "save written atomically");
                var loaded = SaveStore.Read(path)!;
                Check(loaded != null && loaded.Seed == Settings.Seed, "world seed survives disk round trip");
                game.RestoreProgress(loaded!);
                Check(
                    System.Text.Json.JsonSerializer.Serialize(expected)
                        == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    "full progress and interaction survive restoration: " + expected.State
                );
            }
            car.SetPose(world.Animals[0].Position + Vector3.Right * 2, .7f);
            game.OpenChallenge(world.Animals[0]);
            game.SubmitAnswer(game.CurrentChallenge!.Choices.First(choice => !game.CurrentChallenge.Accepts(choice)));
            RoundTrip();
            ChallengeChecks.Solve(game);
            RoundTrip();
            game.SelectExpedition(true);
            car.SetPose(game.Delivery!.PickupPositions[1], 1.2f);
            game.Delivery.Tick(.25);
            Check(game.Delivery.Collected >= 1, "collection in progress");
            RoundTrip();
            Check(
                game.Delivery!.GetChildren()
                    .OfType<Node3D>()
                    .Count(n => n.Name.ToString().StartsWith("CollectibleApple") && !n.Visible)
                    == game.Delivery.Collected,
                "picked fruit stays hidden after restore"
            );
            foreach (var point in game.Delivery.PickupPositions)
            {
                car.SetPose(point, 0);
                game.Delivery.Tick(.25);
            }
            car.SetPose(game.Delivery.Recipient.Position + Vector3.Right * 2, .5f);
            game.BeginDeliveryQuestion();
            RoundTrip();
            int coins = game.Coins;
            ChallengeChecks.Solve(game);
            RoundTrip();
            Check(
                game.State == GameState.Reward && game.Coins == coins + Game.RoadsideDeliveryCoins,
                "a restored reward panel keeps the paid coins without paying again"
            );
            var legacyReward = LegacySaves.FromCurrent(game.CaptureProgress(), 2);
            legacyReward["Coins"] = coins;
            legacyReward["RewardCoins"] = 0;
            var migratedReward = LegacySaves.Read(legacyReward);
            Check(
                migratedReward is { Version: SaveData.CurrentVersion, State: GameState.Reward }
                    && migratedReward.Economy.Coins == coins + Game.RoadsideDeliveryCoins
                    && migratedReward.Economy.RewardCoins == Game.RoadsideDeliveryCoins,
                "an open version 2 reward choice migrates to the delivery's coins"
            );
            game.FinishInteraction();
            game.AddCoins(CarReward.ModelPrice);
            int beforePurchase = game.Coins;
            var reward = new CarReward("buggy", false, true);
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            game.ConfigureCar(reward);
            Check(
                game.Coins == beforePurchase - CarReward.ModelPrice && game.OwnedRewards.Contains(reward),
                "the garage sells a model for coins"
            );
            game.FinishInteraction();
            game.OwnedRewards.Add(new("wheels", true));
            car.Appearance.ToggleAccessory("wheels");
            RoundTrip();
            Check(
                game.OwnedRewards.Contains(reward)
                    && car.Appearance.CurrentModelId == reward.Id
                    && car.Appearance.BigWheels
                    && game.Coins == beforePurchase - CarReward.ModelPrice,
                "bought items, coins and equipped appearance restored"
            );
            game.OpenChallenge(world.Animals[1]);
            RoundTrip();
            var syllables = (SyllableChallenge)game.CurrentChallenge!;
            game.SubmitAnswer(syllables.Choices.First(choice => !syllables.Accepts(choice)));
            if (syllables.Syllables.Count > 1)
                game.SubmitAnswer(syllables.Choices.Last(syllables.Accepts));
            RoundTrip();
            game.TogglePause();
            RoundTrip();
            Check(game.State == GameState.Challenge, "paused session restores the underlying question");
            foreach (int tile in syllables.Choices.Where(syllables.Accepts))
                game.SubmitAnswer(tile);
            RoundTrip();
            Check(
                game.State == GameState.Challenge
                    && game.FindChild("ConfirmSyllables", true, false) is Button { Disabled: false },
                "all selected syllables survive reload and still require confirmation"
            );
            game.FinishInteraction();
            car.SetPose(world.Village.GarageEntrance, .2f);
            game.OpenGarage();
            RoundTrip();
            ((Button)game.FindChild("GarageMathLevel2", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)game.FindChild("GarageQuiz_Syllables", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)game.FindChild("GarageQuiz_Reading", true, false)).EmitSignal(Button.SignalName.Pressed);
            RoundTrip();
            Check(
                game.CaptureProgress() is { Quiz: { Types: QuizType.Math | QuizType.Letters, ThreeTermMath: true } },
                "garage task settings and the arithmetic level survive reload"
            );
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[2]);
            RoundTrip();
            Check(game.CurrentChallenge is LetterChallenge, "the original initial-letter question survives reload");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[3]);
            RoundTrip();
            Check(
                game.CurrentChallenge is MathChallenge { Third: > 0 },
                "the exact three-term example survives reload"
            );
            game.FinishInteraction();
            car.SetPose(world.Village.GarageEntrance, .2f);
            game.OpenGarage();
            RoundTrip();
            var backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{broken");
            Check(SaveStore.Read(path) != null, "damaged primary save falls back to backup");
            File.WriteAllText(path + ".bak", "{}");
            Check(SaveStore.Read(path) == null, "invalid saves are safely ignored");
            File.WriteAllText(path, backup);
            var legacy = game.CaptureProgress();
            legacy.Roadside.Animals = legacy.Roadside.Animals.Concat(Enumerable.Repeat(true, 20)).ToArray();
            legacy.Roadside.Unlocked = new[] { 0, 12, 29 };
            legacy.Roadside.Completed = new[] { 1, 22 };
            legacy.State = GameState.Exploring;
            legacy.Nearby = 30;
            legacy.Task.Sender = -1;
            legacy.Task.Picked = Array.Empty<bool>();
            legacy.Task.DeliveryReward = false;
            Check(SaveStore.Write(legacy, path), "legacy thirty-animal save written");
            var migrated = SaveStore.Read(path)!;
            Check(
                migrated != null
                    && migrated.Roadside.Animals.Length == 10
                    && migrated.Roadside.Unlocked.SequenceEqual(new[] { 0 })
                    && migrated.Roadside.Completed.SequenceEqual(new[] { 1 })
                    && migrated.Nearby == 30,
                "old saves retain ten animal records and stable village IDs"
            );
            game.RestoreProgress(migrated!);
            Check(game.RemainingAnimals <= 10, "migrated progress restores in the ten-animal world");
            legacy.State = GameState.Challenge;
            legacy.Nearby = 22;
            legacy.Quiz.Challenge = new ChallengeGenerator(42).Next();
            Check(SaveStore.Write(legacy, path), "removed animal question saved");
            migrated = SaveStore.Read(path)!;
            Check(
                migrated is { State: GameState.Exploring, Nearby: -1, Quiz: { Challenge: null } },
                "removed animal interaction returns safely to driving"
            );
            // A version 1 save keeps the open question in its per-kind field.
            var oldQuestion = new ChallengeGenerator(42).Next();
            var versionOne = LegacySaves.FromCurrent(game.CaptureProgress(), 1);
            versionOne["State"] = (int)GameState.Challenge;
            versionOne["Nearby"] = 0;
            versionOne["Sender"] = -1;
            versionOne["Picked"] = new System.Text.Json.Nodes.JsonArray();
            versionOne["HandoverQuiz"] = false;
            versionOne["DeliveryReward"] = false;
            versionOne.Remove("Challenge");
            versionOne["Math"] = System.Text.Json.JsonSerializer.SerializeToNode(oldQuestion);
            var upgraded = LegacySaves.Read(versionOne);
            Check(
                upgraded is { Version: SaveData.CurrentVersion, Quiz: { Challenge: MathChallenge migratedQuestion } }
                    && migratedQuestion.Expression == oldQuestion.Expression
                    && migratedQuestion.Choices.SequenceEqual(oldQuestion.Choices),
                "version 1 question migrates into the single challenge field"
            );
            Check(
                versionOne.ContainsKey("Math")
                    && !versionOne.ContainsKey("Challenge")
                    && !versionOne.ContainsKey("Quiz"),
                "version 1 save on disk uses the old per-kind field"
            );
            legacy.State = GameState.Exploring;
            legacy.Nearby = -1;
            legacy.Task.Sender = 22;
            legacy.Task.Collection = true;
            legacy.Task.Picked = new[] { true, false, false };
            Check(SaveStore.Write(legacy, path), "removed animal expedition saved");
            migrated = SaveStore.Read(path)!;
            Check(
                migrated is { Task: { Sender: -1 }, State: GameState.Exploring }
                    && migrated.Task.Picked.Length == 0
                    && migrated.Economy.Owned.SequenceEqual(legacy.Economy.Owned),
                "removed sender cancels only its expedition and preserves earned rewards"
            );
            var beforeLeveling = game.CaptureProgress();
            var villageSpot = world.Village.Center;
            float villageGround = world.Ground.Height(villageSpot.X, villageSpot.Z);
            beforeLeveling.Vehicle.Position = new[] { villageSpot.X, villageGround - 3, villageSpot.Z };
            game.RestoreProgress(beforeLeveling);
            Check(
                Mathf.Abs(car.Position.X - villageSpot.X) < .001f
                    && Mathf.Abs(car.Position.Z - villageSpot.Z) < .001f
                    && car.Position.Y >= villageGround,
                "old village position below the leveled terrain restores above ground without moving horizontally"
            );
            CheckPreservedCopies();
            GD.Print("SAVE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
