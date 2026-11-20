using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Ui;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

public partial class GarageChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public GarageChecks(Game game, World world, Vehicle car)
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

    /// <summary>Every model fits the car's footprint and trailer hitch, and swaps in and out cleanly.</summary>
    private void CheckModels()
    {
        foreach (var body in CarBody.All)
        {
            var holder = new Node3D();
            AddChild(holder);
            var wheels = new System.Collections.Generic.List<Node3D>();
            VehicleModel.BuildDisplay(holder, new VehicleAppearance(), wheels, body.Id);
            Aabb? bounds = null;
            foreach (var node in holder.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mesh = (MeshInstance3D)node;
                if (!mesh.IsVisibleInTree())
                    continue;
                var box = mesh.GlobalTransform * mesh.GetAabb();
                bounds = bounds is { } known ? known.Merge(box) : box;
            }
            var area = bounds ?? default;
            Check(
                bounds != null
                    && area.Position.X >= -1.4f
                    && area.End.X <= 1.4f
                    && area.Position.Z >= -1.95f
                    && area.End.Z <= 1.75f
                    && area.Position.Y >= -.02f
                    && area.Position.Y <= .1f
                    && area.End.Y <= 3.2f,
                $"model {body.Id} stands on the ground inside the car's footprint, clear of the trailer hitch: {area}"
            );
            Check(
                wheels.Count >= 4 && (!body.OwnWheels || wheels.Count > 4),
                "the four tyres turn, and a model with wheels of its own adds them: " + body.Id
            );
            holder.QueueFree();
        }
        var ocean = CarLook.All.First(look => look.Id == "ocean");
        var plain = CarLook.All[0];
        var homes = car.FindChildren("Attachment_*", "Node3D", true, false)
            .OfType<Node3D>()
            .ToDictionary(node => node.Name.ToString(), node => node.Position);
        foreach (var body in CarBody.All)
        {
            car.Appearance.ApplyModel(body.Id);
            car.Appearance.Apply(ocean);
            bool striped = car.Appearance.HasStripes;
            car.Appearance.Apply(plain);
            var shared = car.Wheels.Take(4);
            Check(
                striped
                    && !car.Appearance.HasStripes
                    && car.Appearance.RearWheel == (body.TrackX, body.TrackZ)
                    && shared.All(wheel => wheel.IsVisibleInTree() == !body.OwnWheels)
                    && car.Wheels.Any(wheel => wheel.IsVisibleInTree()),
                "model shows its own stripes, tyre-mark position and wheels: " + body.Id
            );
        }
        car.Appearance.ApplyModel("classic");
        Check(
            car.FindChildren("Attachment_*", "Node3D", true, false)
                .OfType<Node3D>()
                .All(node =>
                    node.Name == "Attachment_valley-map" || node.Position.IsEqualApprox(homes[node.Name.ToString()])
                ),
            "accessories return to their places on the car after a toy"
        );
    }

    /// <summary>
    /// The steam engine smokes hard while it gathers speed, less while it holds it, not at all when it rolls without
    /// the pedal and only a little while it stands.
    /// </summary>
    private void CheckTrainSmoke()
    {
        float gathering = Vehicle.EngineEffort(1, false, 3, Settings.MaxSpeed);
        float holding = Vehicle.EngineEffort(1, false, Settings.MaxSpeed, Settings.MaxSpeed);
        float reversing = Vehicle.EngineEffort(-1, false, 4, -Settings.ReverseSpeed);
        Check(
            gathering == 1
                && holding is > 0 and < 1
                && reversing == 1
                && Vehicle.EngineEffort(0, false, 10, 0) == 0
                && Vehicle.EngineEffort(1, true, 10, 0) == 0,
            "the engine works hard while it gathers speed, a little while it holds it and not without the pedal"
        );
        float Smoke(float effort, float speed)
        {
            var holder = new Node3D();
            AddChild(holder);
            var smoke = new TrainSmoke();
            holder.AddChild(smoke);
            float before = smoke.Blown;
            for (int i = 0; i < 180; i++)
                smoke.Step(1 / 60f, effort, new Vector3(0, 0, -speed));
            float blown = smoke.Blown - before;
            holder.Free();
            return blown;
        }
        float hard = Smoke(gathering, 6),
            steady = Smoke(holding, Settings.MaxSpeed),
            coasting = Smoke(0, 10),
            standing = Smoke(0, 0);
        Check(
            hard > steady * 2 && steady > standing * 2 && standing > 0 && coasting == 0,
            $"the chimney smokes most while gathering speed ({hard:0.00}), less while holding it ({steady:0.00}), "
                + $"a little standing ({standing:0.00}) and not at all coasting ({coasting:0.00})"
        );
        var train = car.FindChild("ChimneySmoke", true, false) as TrainSmoke;
        Check(train != null && !train.IsVisibleInTree(), "the train's chimney smoke only shows with the train");
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            Check(game.State == GameState.Garage && !car.Active, "garage stops driving at the village entrance");
            var buttons = game.FindChildren("Garage_*", "Button", true, false).Cast<Button>().ToArray();
            Check(
                buttons.Length
                    == CarLook.Catalogue.Count
                        + CarBody.All.Count
                        + CarAccessory.Catalogue.Count
                        + CarTrail.Catalogue.Count
                    && buttons.Count(b => !b.Disabled) == 3
                    && game.Coins == 0,
                "entire catalogue contains all rewards with only the starting paint, model and tyre marks unlocked"
            );
            Check(
                game.FindChildren("Price_*", "HBoxContainer", true, false).Count
                    == CarLook.All.Count + CarBody.All.Count + CarAccessory.All.Count + CarTrail.All.Count - 2,
                "every standard item not owned shows its price; the special sticker has none"
            );
            CheckModels();
            CheckTrainSmoke();
            var defaults = game.CaptureProgress();
            void Press(string name) =>
                ((Button)game.FindChild(name, true, false)).EmitSignal(Button.SignalName.Pressed);
            Check(
                defaults.Quiz.Types == QuizType.All && !defaults.Quiz.ThreeTermMath,
                "all four quiz types and level-one arithmetic are the defaults"
            );
            Check(
                game.FindChildren("GarageQuiz_*", "Button", true, false)
                    .Cast<Button>()
                    .Count(button =>
                        button.Text == ""
                        && button.GetChildren().OfType<DeliveryPicture>().Count() == 1
                        && !button.Disabled
                    ) == 4,
                "garage exposes an illustrated, independently selectable control for each quiz type"
            );
            Press("GarageQuiz_Reading");
            Press("GarageQuiz_Syllables");
            Press("GarageQuiz_Math");
            Press("GarageMathLevel2");
            Check(
                game.CaptureProgress() is { Quiz: { Types: QuizType.Letters, ThreeTermMath: true } },
                "quiz toggles and the arithmetic level can be selected independently"
            );
            game.FinishInteraction();
            game.OpenChallenge(world.Witch);
            Check(
                game.CurrentChallenge is LetterChallenge && game.PostcardQuestion(0) is LetterChallenge,
                "letters-only setting reaches arithmetic slots and postcards"
            );
            game.FinishInteraction();
            game.OpenGarage();
            Press("GarageQuiz_Math");
            Press("GarageQuiz_Letters");
            game.FinishInteraction();
            game.OpenChallenge(world.Witch);
            Check(
                game.CurrentChallenge is MathChallenge { Third: > 0 } advanced
                    && advanced.Answer <= 10
                    && game.PostcardQuestion(0) is MathChallenge { Third: > 0 },
                "selected level produces three-term examples in the game"
            );
            game.RestoreProgress(defaults);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var ui = game.GetChildren().OfType<GameUi>().Single();
            var selectable = game.FindChildren("*", "Button", true, false)
                .OfType<Button>()
                .Where(button => button.IsVisibleInTree() && !button.Disabled)
                .ToHashSet();
            var buttonsSeen = new System.Collections.Generic.HashSet<Button>();
            var scroll = (ScrollContainer)game.FindChild("GarageScroll", true, false);
            bool reachedBelowFold = false;
            for (int i = 0; i < selectable.Count; i++)
            {
                ui._Input(
                    new InputEventKey
                    {
                        Keycode = Key.Right,
                        PhysicalKeycode = Key.Right,
                        Pressed = true,
                    }
                );
                reachedBelowFold |= scroll.ScrollVertical > 0;
                if (GetViewport().GuiGetFocusOwner() is Button button)
                    buttonsSeen.Add(button);
            }
            Check(
                buttonsSeen.SetEquals(selectable) && reachedBelowFold,
                "arrow keys reach every enabled garage button, including scrolled items and Back"
            );
            Check(
                ChallengeChecks.AnswerIcon(game, "LeaveGarage") == "cross",
                "the garage is left with the same red X as every other panel"
            );
            ui._Input(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 1 });
            ui._Process(.4);
            ui._Input(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 0 });
            Check(scroll.ScrollVertical > 0, "right stick scrolls the catalogue even when rewards are locked");
            game.ConfigureCar(new("buggy", false, true));
            Check(
                car.Appearance.CurrentModelId == "classic" && game.Coins == 0,
                "a model the player cannot afford cannot be bought or equipped"
            );
            game.TogglePause();
            game.TogglePause();
            Check(game.State == GameState.Garage, "pause resumes garage");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[0]);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(false);
            var delivery = game.Delivery!;
            car.SetPose(delivery.Recipient.Position + Vector3.Right * 2, 0);
            game.BeginDeliveryQuestion();
            ChallengeChecks.Solve(game);
            Check(
                game.Coins == Game.RoadsideDeliveryCoins && game.OwnedRewards.Count == 2,
                "a finished expedition pays coins and no car item"
            );
            game.FinishInteraction();
            game.AddCoins(CarReward.AccessoryPrice);
            int coins = game.Coins;
            var reward = new CarReward(CarAccessory.All[0].Id, true);
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            Check(
                game.FindChild("Garage_" + reward.ButtonName, true, false) is Button { Disabled: false }
                    && game.FindChild("Garage_Model_buggy", true, false) is Button { Disabled: true },
                "items the player can afford are offered for sale; dearer ones stay locked"
            );
            Press("Garage_" + reward.ButtonName);
            Check(
                game.OwnedRewards.Contains(reward)
                    && car.Appearance.HasAccessory(reward.Id)
                    && game.Coins == coins - CarReward.AccessoryPrice
                    && game.FindChild("CoinCount", true, false) is Label { Text: var shown }
                    && shown == game.Coins.ToString(),
                "buying an accessory spends its price, equips it and updates the coin HUD"
            );
            game.ConfigureCar(reward);
            Check(
                !car.Appearance.HasAccessory(reward.Id) && game.OwnedRewards.Contains(reward),
                "garage removes accessories without losing ownership"
            );
            game.ConfigureCar(reward);
            Check(
                car.Appearance.HasAccessory(reward.Id) && game.Coins == coins - CarReward.AccessoryPrice,
                "owned accessory can be equipped again without paying"
            );
            // ---- Trails of the wheels ----
            Check(
                car.Appearance.CurrentTrailId == CarTrail.Plain.Id && game.CaptureProgress().Vehicle.Trail == "tyres",
                "the car starts with the plain tyre marks"
            );
            game.AddCoins(CarReward.TrailPrice);
            int trailCoins = game.Coins;
            game.FinishInteraction();
            game.OpenGarage();
            var hearts = new CarReward("hearts", false, false, true);
            Check(
                hearts.Price == CarReward.TrailPrice
                    && new CarReward("tyres", false, false, true).Price == null
                    && game.FindChild("Garage_" + hearts.ButtonName, true, false) is Button { Disabled: false },
                "a trail of hearts is for sale and the plain marks are free"
            );
            Press("Garage_" + hearts.ButtonName);
            Check(
                game.OwnedRewards.Contains(hearts)
                    && car.Appearance.CurrentTrailId == "hearts"
                    && game.Coins == trailCoins - CarReward.TrailPrice,
                "buying a trail spends its price and puts it on the wheels"
            );
            car.Tracks.Update(.05f, car, world.Ground, world.Routes);
            Check(
                car.Tracks.Stamps is { Style: "hearts" } && car.Tracks.SegmentCount == 0,
                "hearts take the place of the tyre marks"
            );
            var trailSave = game.CaptureProgress();
            string trailPath = DiagnosticArtifacts.PathFor("garage-checks/trail.json");
            Check(
                trailSave.Vehicle.Trail == "hearts" && SaveStore.Write(trailSave, trailPath),
                "the chosen trail is saved"
            );
            game.ConfigureCar(new CarReward("tyres", false, false, true));
            Check(
                car.Appearance.CurrentTrailId == "tyres" && game.OwnedRewards.Contains(hearts),
                "the plain marks come back without losing the hearts"
            );
            game.RestoreProgress(SaveStore.Read(trailPath)!);
            Check(car.Appearance.CurrentTrailId == "hearts", "the trail comes back after a reload");
            game.ConfigureCar(new CarReward("stars", false, false, true));
            Check(
                car.Appearance.CurrentTrailId == "hearts" && game.Coins == trailCoins - CarReward.TrailPrice,
                "a trail the player cannot afford is not bought"
            );
            void Damaged(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor("garage-checks/trail-damaged.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, "rejected: " + what);
            }
            Damaged(data => data.Vehicle.Trail = "stars", "a trail that was never bought");
            Damaged(data => data.Vehicle.Trail = "sparkles", "a trail that does not exist");
            Damaged(data => data.Vehicle.Trail = null!, "no trail at all");
            var beforeTrails = System.Text.Json.JsonSerializer.SerializeToNode(game.CaptureProgress())!.AsObject();
            ((System.Text.Json.Nodes.JsonObject)beforeTrails["Vehicle"]!).Remove("Trail");
            string oldTrailPath = DiagnosticArtifacts.PathFor("garage-checks/before-trails.json");
            System.IO.File.WriteAllText(oldTrailPath, beforeTrails.ToJsonString());
            game.RestoreProgress(
                SaveStore.Read(oldTrailPath) ?? throw new InvalidOperationException("older save refused")
            );
            Check(car.Appearance.CurrentTrailId == "tyres", "a save from before trails has the plain tyre marks");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[1]);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(true);
            var collection = game.Delivery;
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            game.TogglePause();
            game.TogglePause();
            game.FinishInteraction();
            Check(
                game.Delivery == collection && game.State == GameState.Exploring && car.Active,
                "garage and pause preserve an active expedition"
            );
            game.OpenGarage();
            Press("GarageQuiz_Math");
            Press("GarageQuiz_Letters");
            Press("GarageQuiz_Reading");
            Check(
                ((Button)game.FindChild("GarageQuiz_Syllables", true, false)).Disabled,
                "last active quiz type is locked in the garage"
            );
            Press("GarageQuiz_Syllables");
            game.FinishInteraction();
            Check(
                game.CaptureProgress().Quiz.Types == QuizType.Syllables
                    && game.PostcardQuestion(0) is SyllableChallenge,
                "the last active quiz type cannot be switched off"
            );
            foreach (var point in collection!.PickupPositions)
            {
                car.SetPose(point, 0);
                collection.Tick(.25);
            }
            car.SetPose(collection.Recipient.Position + Vector3.Right * 2, 0);
            game.BeginDeliveryQuestion();
            Check(
                game.State == GameState.Challenge && game.CurrentChallenge is SyllableChallenge,
                "the enabled quiz type replaces cargo counting when arithmetic is off"
            );
            string path = DiagnosticArtifacts.PathFor("save-checks/handover.json");
            Check(SaveStore.Write(game.CaptureProgress(), path), "replacement handover quiz saves to disk");
            game.RestoreProgress(
                SaveStore.Read(path) ?? throw new InvalidOperationException("Handover save failed validation")
            );
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Reward
                    && game.CurrentChallenge == null
                    && game.CaptureProgress().Task.DeliveryReward,
                "replacement handover quiz unlocks the delivery reward after reload"
            );
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[2]);
            Check(
                game.State == GameState.Challenge && game.CurrentChallenge is SyllableChallenge,
                "disabled quiz types are replaced at new expeditions as well"
            );
            Check(
                world.Village.IsReserved(world.Village.GarageEntrance),
                "garage plot excludes plants and animal placement"
            );
            game.FinishInteraction();
            var coinSpots = game.WorldCoins.ActivePositions.ToArray();
            Check(
                coinSpots.Length == WorldCoins.Count && coinSpots.All(p => world.Routes.Distance(p) < 1.5f),
                "coins are scattered along the roads of the valley"
            );
            game.WorldCoins.PickupEnabled = true;
            int beforeCoin = game.Coins;
            car.SetPose(coinSpots[0], 0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(
                game.Coins > beforeCoin && game.WorldCoins.ActiveCount < WorldCoins.Count,
                "driving over a coin collects it and adds it to the counter"
            );
            game.WorldCoins.PickupEnabled = false;
            GD.Print("GARAGE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
