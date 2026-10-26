using Godot;
using System;
using System.Linq;

namespace MathRacer;

public enum GameState { Title, Exploring, Challenge, Reward, Paused }

public partial class Game : Node3D
{
    public GameState State { get; private set; } = GameState.Title;
    private readonly World world = new();
    private readonly Vehicle vehicle = new();
    private readonly GameUi ui = new();
    private Animal? nearby;
    internal int RemainingAnimals => world.Animals.Count(animal => animal.RewardAvailable);
    private GameState beforePause;
    private readonly ChallengeGenerator challenges = new(Settings.Seed + 204);
    private readonly LetterChallengeGenerator letterChallenges = new(Settings.Seed + 205);
    private readonly System.Collections.Generic.HashSet<int> rejectedAnswers = new();
    internal IChallenge? CurrentChallenge { get; private set; }
    internal System.Collections.Generic.IReadOnlyList<CarReward> CurrentRewards { get; private set; } = Array.Empty<CarReward>();
    private readonly Random rewardRandom = new();
    private string feedback = "Nikam nespěcháme. Můžeš si to spočítat na prstech.";
    public override void _Ready()
    {
        GameControls.Setup();
        if (OS.GetCmdlineUserArgs().Contains("--road-audit"))
        {
            try
            {
                AddChild(world.Ground); world.Ground.Generate();
                AddChild(world.Routes); world.Routes.Generate(world.Ground);
                WorldLayoutChecks.ValidatePlan(WorldLayout.Plan, Settings.Seed);
                RoadPassabilityChecks.Run(world);
                GetTree().Quit();
            }
            catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            return;
        }
        AddChild(world); world.Generate(); AddChild(vehicle); vehicle.Setup(world.Ground, world.Routes);
        if (OS.GetCmdlineUserArgs().Contains("--road-driving-checks"))
        {
            vehicle.Active = true;
            RoadPassabilityChecks.Run(world);
            AddChild(new RoadDrivingChecks(world, vehicle));
            return;
        }
        world.AddChild(world.Wildlife); world.Wildlife.Generate(world, vehicle);
        var camera = new FollowCamera { Target = vehicle, Ground = world.Ground, Fov = 62, Far = 1200, Current = true }; AddChild(camera); camera.Snap();
        world.AddChild(world.Grass); world.Grass.Setup(world, vehicle);
        AddChild(ui); ui.Setup(); ui.PlayRequested += Start; ui.ResumeRequested += Resume; ui.CloseInteractionRequested += CloseInteraction; ui.Title();
        ui.AnswerSelected += SubmitAnswer; ui.LookSelected += SelectLook;
        ui.AccessorySelected += SelectAccessory;
        ui.ModelSelected += SelectModel;
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        var args = OS.GetCmdlineUserArgs();
        if ((args.Contains("--play") && !args.Contains("--view=title")) || args.Contains("--smoke-test") || args.Contains("--track-checks") || args.Contains("--collision-checks")) Start();
        if (args.Contains("--collision-checks")) AddChild(new CollisionChecks(vehicle, world));
        if (args.Contains("--track-checks")) AddChild(new WheelTrackChecks(vehicle, world.Ground, world.Routes));
        if (args.Contains("--smoke-test")) AddChild(new SmokeTest(this, world, vehicle));
        if (args.Contains("--controller-checks")) AddChild(new ControllerChecks(this, world, vehicle));
        if (args.Contains("--capture"))
        {
            string view = args.FirstOrDefault(a => a.StartsWith("--view="))?.Substring(7) ?? "gameplay";
            if (view == "challenge" || view == "letters" || view == "reward" || view == "accessories")
            {
                OpenChallenge(world.Animals[view == "letters" ? 1 : 0]);
                if (view == "reward" || view == "accessories") SubmitAnswer(CurrentChallenge!.Answer);
                if (view == "accessories")
                {
                    CurrentRewards = new[] { new CarReward("buggy", false, true), new CarReward("ocean", false), new CarReward("surfboard", true) };
                    ui.Reward(world.Animals[0], vehicle.Appearance, CurrentRewards);
                }
            }
            if (view == "decorated") foreach (var accessory in CarAccessory.All) vehicle.Appearance.ToggleAccessory(accessory.Id);
            if (CarBody.All.Any(body => body.Id == view))
            {
                vehicle.Appearance.ApplyModel(view);
                vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == "ocean"));
                foreach (var id in view == "buggy" ? new[] { "surfboard", "lights", "bumper" } :
                    view == "pickup" ? new[] { "spoiler", "exhaust", "antenna" } : new[] { "rack", "suitcase", "spare" })
                    vehicle.Appearance.ToggleAccessory(id);
            }
            AddChild(new VisualCapture(camera, world, vehicle, view));
        }
        GD.Print($"WORLD_READY seed={Settings.Seed} animals={world.Animals.Length} road_samples={world.Routes.Samples.Count}");
    }
    private void Start() { State = GameState.Exploring; vehicle.Active = true; ui.HideModal(); }
    private void CloseInteraction()
    {
        CurrentChallenge = null; rejectedAnswers.Clear(); Start();
    }
    private void Resume()
    {
        State = beforePause; vehicle.Active = State == GameState.Exploring;
        if (State == GameState.Challenge && nearby != null && CurrentChallenge != null) ui.Challenge(nearby, CurrentChallenge, feedback, rejectedAnswers);
        else if (State == GameState.Reward && nearby != null) ui.Reward(nearby, vehicle.Appearance, CurrentRewards);
        else ui.HideModal();
    }
    public override void _Input(InputEvent e)
    {
        if (e.IsEcho()) return;
        if (e.IsActionPressed("pause") && State != GameState.Title)
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("back") && State != GameState.Title)
        {
            if (State == GameState.Paused) Resume();
            else if (State == GameState.Challenge || State == GameState.Reward) CloseInteraction();
            else TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsEcho()) return;
        if (State != GameState.Exploring) return;
        if (e.IsActionPressed("reset")) vehicle.Reset();
        if (e.IsActionPressed("talk") && nearby != null) OpenChallenge(nearby);
    }
    internal void OpenChallenge(Animal animal)
    {
        if (State != GameState.Exploring || !animal.RewardAvailable) return;
        animal.SetRewardAvailable(false);
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        BeginChallenge(animal);
    }
    private void BeginChallenge(Animal animal)
    {
        nearby = animal; vehicle.Active = false;
        CurrentChallenge = Array.IndexOf(world.Animals, animal) % 2 == 0 ? challenges.Next() : letterChallenges.Next();
        rejectedAnswers.Clear();
        feedback = CurrentChallenge is LetterChallenge
            ? "Pojmenuj obrázky. Které slovo začíná na toto písmenko?"
            : "Nikam nespěcháme. Můžeš si to spočítat na prstech.";
        State = GameState.Challenge; ui.Challenge(nearby, CurrentChallenge, feedback, rejectedAnswers);
    }
    internal void SubmitAnswer(int answer)
    {
        if (State != GameState.Challenge || CurrentChallenge == null || nearby == null || !CurrentChallenge.Choices.Contains(answer)) return;
        if (answer == CurrentChallenge.Answer)
        {
            CurrentRewards = CarReward.Draw(rewardRandom, CurrentRewards);
            State = GameState.Reward; ui.Reward(nearby, vehicle.Appearance, CurrentRewards);
        }
        else
        {
            rejectedAnswers.Add(answer);
            feedback = "Nevadí! Zkus jinou odpověď. Společně to zvládneme.";
            ui.Challenge(nearby, CurrentChallenge, feedback, rejectedAnswers);
        }
    }
    internal void SelectLook(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, false))) return;
        var look = CarLook.All.FirstOrDefault(option => option.Id == id);
        if (look == null) return;
        vehicle.Appearance.Apply(look); CloseInteraction();
    }
    internal void SelectModel(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, false, true))) return;
        if (!CarBody.All.Any(body => body.Id == id)) return;
        vehicle.Appearance.ApplyModel(id); CloseInteraction();
    }
    internal void TogglePause()
    {
        if (State == GameState.Paused) Resume();
        else { beforePause = State; State = GameState.Paused; vehicle.Active = false; ui.Pause(); }
    }
    internal void SelectAccessory(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, true))) return;
        vehicle.Appearance.ToggleAccessory(id); CloseInteraction();
    }
    internal void FinishInteraction() => CloseInteraction();
    public override void _Process(double delta)
    {
        if (State == GameState.Exploring)
        {
            nearby = world.Animals.FirstOrDefault(a => a.Position.DistanceTo(vehicle.Position) < Settings.InteractionRange);
            ui.Hint(nearby != null, nearby?.RewardAvailable ?? false);
        }
    }
}


