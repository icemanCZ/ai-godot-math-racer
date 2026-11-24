using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Ui;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>Checks generated questions, single visits, the HUD and real reward UI signals.</summary>
public static class ChallengeChecks
{
    internal static void Solve(Game game)
    {
        if (game.CurrentChallenge is not { } question)
            return;
        foreach (int choice in question.Choices.Where(question.Accepts))
            game.SubmitAnswer(choice);
        if (question is SyllableChallenge)
            game.ConfirmSyllables();
    }

    /// <summary>The picture on an icon button of the open panel: "check" accepts and "cross" declines, in every dialog.</summary>
    internal static string? AnswerIcon(Game game, string button) =>
        game.FindChild(button, true, false)?.GetChildren().OfType<DeliveryPicture>().FirstOrDefault()?.Icon;

    public static void Run(Game game, World world, Vehicle car, Action<bool, string> check)
    {
        var generator = new ChallengeGenerator(53);
        var repeat = new ChallengeGenerator(53);
        var slots = new System.Collections.Generic.HashSet<int>();
        var operations = new System.Collections.Generic.HashSet<MathOperation>();
        for (int i = 0; i < 1000; i++)
        {
            var q = generator.Next();
            var other = repeat.Next();
            if (
                q.Left < 0
                || q.Left > 10
                || q.Right < 0
                || q.Right > 10
                || q.Answer < 0
                || q.Answer > 10
                || q.Choices.Count != 3
                || q.Choices.Distinct().Count() != 3
                || q.Choices.Any(v => v < 0 || v > 10)
                || q.Choices.Count(v => v == q.Answer) != 1
            )
                throw new InvalidOperationException("Question bounds or unique choices failed.");
            if (q.Expression != other.Expression || !q.Choices.SequenceEqual(other.Choices))
                throw new InvalidOperationException("Question stream is not deterministic.");
            slots.Add(q.Choices.ToList().IndexOf(q.Answer));
            operations.Add(q.Operation);
        }
        check(
            slots.Count == 3 && operations.Count == 2,
            "1000 valid deterministic questions; both operations and all answer positions"
        );
        var answers = new int[11];
        var pairs = new System.Collections.Generic.HashSet<(MathOperation, MathOperation)>();
        for (int i = 0; i < 1000; i++)
        {
            var q = generator.Next(threeTerms: true);
            var other = repeat.Next(threeTerms: true);
            int middle = MathChallenge.Apply(q.Left, q.Operation, q.Right);
            if (
                q.Left is < 1 or > 10
                || q.Right is < 1 or > 10
                || q.Third is not (>= 1 and <= 10)
                || middle is < 0 or > 10
                || q.Answer != MathChallenge.Apply(middle, q.SecondOperation, q.Third.Value)
                || q.Answer is < 0 or > 10
                || q.Choices.Count != 3
                || q.Choices.Distinct().Count() != 3
                || q.Choices.Any(v => v < 0 || v > 10)
                || q.Choices.Count(q.Accepts) != 1
                || q.Expression != other.Expression
                || !q.Choices.SequenceEqual(other.Choices)
            )
                throw new InvalidOperationException("Invalid or nondeterministic three-term question: " + q.Expression);
            answers[q.Answer]++;
            pairs.Add((q.Operation, q.SecondOperation));
        }
        check(
            pairs.Count == 4 && answers.All(count => count > 20),
            "1000 deterministic three-term questions add and subtract in every order, with every result from 0 to 10"
        );

        var comparisons = new CompareChallengeGenerator(53);
        var repeatComparisons = new CompareChallengeGenerator(53);
        var signCounts = new int[3];
        for (int i = 0; i < 1000; i++)
        {
            var q = comparisons.Next();
            var other = repeatComparisons.Next();
            if (!q.IsValid() || q.Choices.Count(q.Accepts) != 1 || q != other)
                throw new InvalidOperationException("Invalid or nondeterministic comparison: " + q.Expression);
            signCounts[q.Answer]++;
        }
        check(
            signCounts.All(count => count > 120)
                && signCounts[CompareChallenge.Smaller] > signCounts[CompareChallenge.Same],
            "1000 deterministic comparisons of numbers from 0 to 10 ask for every sign, equality less often"
        );

        var letters = new LetterChallengeGenerator(53);
        var repeatLetters = new LetterChallengeGenerator(53);
        var letterSlots = new System.Collections.Generic.HashSet<int>();
        var initials = new System.Collections.Generic.HashSet<string>();
        var pictureDeck = new System.Collections.Generic.HashSet<string>();
        check(
            LetterChallengeGenerator.Words.Count == 32
                && LetterChallengeGenerator.Words.Select(p => p.Id).Distinct().Count() == 32,
            "catalogue contains 32 distinct pictures"
        );
        for (int i = 0; i < 1000; i++)
        {
            var q = letters.Next();
            var other = repeatLetters.Next();
            if (
                q.Pictures.Count != 3
                || q.Pictures.Select(p => p.Id).Distinct().Count() != 3
                || q.Pictures.Count(p => p.Initial == q.Letter) != 1
                || q.Answer < 0
                || q.Answer > 2
                || q.Letter != other.Letter
                || !q.Pictures.SequenceEqual(other.Pictures)
            )
                throw new InvalidOperationException("Invalid or nondeterministic letter question.");
            letterSlots.Add(q.Answer);
            initials.Add(q.Letter);
            if (i < LetterChallengeGenerator.Words.Count && !pictureDeck.Add(q.Pictures[q.Answer].Id))
                throw new InvalidOperationException("Correct picture repeated before the deck was exhausted.");
            if (i == LetterChallengeGenerator.Words.Count - 1)
                check(
                    pictureDeck.SetEquals(LetterChallengeGenerator.Words.Select(p => p.Id)),
                    "all 32 pictures appear as targets before the deck repeats"
                );
        }
        check(
            letterSlots.Count == 3 && new[] { "Š", "Ž", "Č" }.All(initials.Contains),
            "1000 letter tasks have one matching picture despite shared initials, including Š, Ž and Č"
        );
        var syllables = new SyllableChallengeGenerator(53);
        var repeatSyllables = new SyllableChallengeGenerator(53);
        foreach (var _ in SyllableChallengeGenerator.Words)
        {
            var q = syllables.Next();
            var other = repeatSyllables.Next();
            var word = SyllableChallengeGenerator.Words.Single(word => word.Id == q.PictureId);
            check(
                q.IsValid()
                    && q.Tiles.Count == 6
                    && q.Tiles.Distinct().Count() == 6
                    && q.Expression == word.Word.ToUpperInvariant()
                    && q.PictureId == other.PictureId
                    && q.Tiles.SequenceEqual(other.Tiles),
                "syllable task has the word's syllables and shuffled distractors in a deterministic six-tile grid"
            );
        }
        var reading = new ReadingChallengeGenerator(53);
        var readingWords = new System.Collections.Generic.HashSet<string>();
        foreach (var _ in ReadingChallengeGenerator.Words)
        {
            var question = reading.Next();
            if (!question.IsValid() || !readingWords.Add(question.Word))
                throw new InvalidOperationException(
                    "Reading quiz has an invalid word, picture choice or repeated target."
                );
        }
        check(
            readingWords.Contains("LES") && readingWords.Contains("PES"),
            "the reading deck has distinct three-letter Czech words and one matching picture each"
        );

        check(
            CarLook.All.All(l => new CarReward(l.Id, false).Price == CarReward.PaintPrice)
                && CarAccessory.All.All(a => new CarReward(a.Id, true).Price == CarReward.AccessoryPrice)
                && CarBody.All.All(b => new CarReward(b.Id, false, true).Price == CarReward.ModelPrice)
                && new CarReward(CarAccessory.ValleyMap.Id, true).Price == null,
            "every standard paint, accessory and model has a garage price; special stickers have none"
        );

        void Press(string name)
        {
            var button = game.FindChild(name, true, false) as Button;
            if (button == null || button.Disabled)
                throw new InvalidOperationException("Missing or disabled UI button: " + name);
            button.EmitSignal(Button.SignalName.Pressed);
        }
        // SmokeTest has already visited and left the first animal without answering.
        check(
            game.RemainingAnimals == world.Animals.Length - 1 && !world.Animals[0].RewardAvailable,
            "leaving a question consumes exactly one visit"
        );
        game.OpenChallenge(world.Animals[0]);
        check(
            game.State == GameState.Exploring && game.CurrentChallenge == null,
            "cancelled animal cannot be reopened"
        );
        string originalLook = car.Appearance.CurrentLookId;
        int startCoins = game.Coins;
        game.ConfigureCar(new CarReward("blueberry", false));
        check(
            car.Appearance.CurrentLookId == originalLook && game.Coins == startCoins,
            "car items cannot be bought outside the garage"
        );
        int visits = 1;
        Type[] rotation =
        {
            typeof(MathChallenge),
            typeof(SyllableChallenge),
            typeof(LetterChallenge),
            typeof(ReadingChallenge),
            typeof(CompareChallenge),
        };
        var ui = game.GetChildren().OfType<GameUi>().Single();
        string? spoken = null;
        ui.Intended += RecordSpeech;
        void RecordSpeech(UiIntent intent)
        {
            if (intent is UiIntent.Speak speak)
                spoken = speak.Text;
        }
        foreach (var animal in world.Animals.Skip(1))
        {
            int before = game.RemainingAnimals;
            game.OpenChallenge(animal);
            visits++;
            var question = game.CurrentChallenge!;
            check(
                question.GetType() == rotation[(visits - 1) % rotation.Length],
                "visits rotate equally through all five quiz types"
            );
            if (question is SyllableChallenge)
            {
                check(
                    game.FindChildren("*", "", true, false).OfType<WordPicture>().Count() == 1
                        && question.Choices.All(choice => game.FindChild($"Answer{choice}", true, false) is Button),
                    "syllable UI displays one picture and six tiles"
                );
                game.ConfirmSyllables();
                check(
                    game.State == GameState.Challenge
                        && ((Button)game.FindChild("ConfirmSyllables", true, false)).Disabled,
                    "a syllable word cannot be confirmed before all tiles are selected"
                );
                check(
                    AnswerIcon(game, "ConfirmSyllables") == "check" && AnswerIcon(game, "LeaveChallenge") == "cross",
                    "a question is accepted with a check mark and left with an X"
                );
            }
            else if (question is IPictureChallenge)
                check(
                    game.FindChildren("*", "", true, false).OfType<WordPicture>().Count() == 3,
                    "initial-letter and reading tasks display three illustrated choices"
                );
            spoken = null;
            int option = question.Choices[1];
            ((Button)game.FindChild($"Answer{option}", true, false)).EmitSignal(Control.SignalName.MouseEntered);
            check(
                spoken
                    == (
                        question switch
                        {
                            SyllableChallenge => null,
                            IPictureChallenge pictures => pictures.Pictures[option].Word,
                            ISignChallenge signs => signs.Spoken(option),
                            _ => option.ToString(),
                        }
                    ),
                "answer buttons speak their own number or picture name, and syllable buttons stay silent"
            );
            check(
                game.State == GameState.Challenge
                    && !car.Active
                    && !animal.RewardAvailable
                    && game.RemainingAnimals == before - 1,
                "opening a question consumes one visit and stops car"
            );
            game.SubmitAnswer(-1);
            check(
                game.State == GameState.Challenge && ReferenceEquals(question, game.CurrentChallenge),
                "invalid answer does not complete task"
            );
            int wrong = question.Choices.First(value => !question.Accepts(value));
            Press($"Answer{wrong}");
            game.TogglePause();
            game.TogglePause();
            var replaced = game.CurrentChallenge;
            check(
                game.State == GameState.Challenge
                    && replaced != null
                    && replaced.GetType() == question.GetType()
                    && replaced.Expression != question.Expression
                    && replaced.Choices.All(choice =>
                        game.FindChild($"Answer{choice}", true, false) is Button { Disabled: false }
                    )
                    && game.FindChild("MissFace", true, false) is Control { Visible: true }
                    && !car.Active
                    && game.RemainingAnimals == before - 1,
                "a wrong answer shows a sad face and a new question of the same kind, with no answer crossed out"
            );
            question = replaced!;
            var correct = question is SyllableChallenge wordQuestion
                ? wordQuestion
                    .Syllables.Reverse()
                    .Select(tile => wordQuestion.Choices.Single(choice => wordQuestion.Tiles[choice] == tile))
                    .ToArray()
                : question.Choices.Where(question.Accepts).ToArray();
            for (int i = 0; i < correct.Length; i++)
            {
                Press($"Answer{correct[i]}");
                if (i + 1 < correct.Length)
                {
                    game.TogglePause();
                    game.TogglePause();
                    game.ConfirmSyllables();
                    check(
                        game.State == GameState.Challenge
                            && (game.FindChild($"Answer{correct[i]}", true, false) as Button)?.Disabled == true,
                        "partial syllable selection survives pause and waits for the rest of the word"
                    );
                }
            }
            if (question is SyllableChallenge)
            {
                check(game.State == GameState.Challenge, "selecting the final tile leaves the confirmation open");
                Press("ConfirmSyllables");
            }
            check(
                game.State == GameState.ExpeditionChoice && !car.Active,
                "correct answer opens task choice before reward"
            );
            game.TogglePause();
            game.TogglePause();
            check(game.State == GameState.ExpeditionChoice, "pause preserves task choice");
            Press(visits % 2 == 0 ? "ChooseCollection" : "ChooseDelivery");
            var expedition = game.Delivery!;
            if (expedition.Collection)
                foreach (var point in expedition.PickupPositions)
                {
                    car.SetPose(point + Vector3.Up * .2f, 0);
                    expedition.Tick(.25);
                }
            car.SetPose(expedition.Recipient.Position + Vector3.Right * 2, 0);
            game.BeginDeliveryQuestion();
            int coinsBefore = game.Coins;
            int ownedBefore = game.OwnedRewards.Count;
            Press($"Answer{game.CurrentChallenge!.Choices.Single(game.CurrentChallenge.Accepts)}");
            check(game.State == GameState.Reward && !car.Active, "completing chosen expedition opens reward");
            check(
                game.Coins == coinsBefore + Game.RoadsideDeliveryCoins && game.OwnedRewards.Count == ownedBefore,
                "a finished delivery pays coins instead of a car item"
            );
            check(
                game.FindChild("RewardCoins", true, false) is Label { Text: var paid }
                    && paid == $"+{Game.RoadsideDeliveryCoins}"
                    && game.FindChild("CoinCount", true, false) is Label { Text: var total }
                    && total == game.Coins.ToString(),
                "reward panel and HUD show the coins"
            );
            game.TogglePause();
            game.TogglePause();
            check(
                game.State == GameState.Reward && game.Coins == coinsBefore + Game.RoadsideDeliveryCoins,
                "pause preserves the reward panel without paying twice"
            );
            check(
                game.FindChild("NewChallenge", true, false) == null,
                "reward cannot start another example at the same animal"
            );
            Press("TakeCoins");
            check(game.State == GameState.Exploring && car.Active, "reward panel resumes driving");
            car.Reset();
            game.TogglePause();
            game.TogglePause();
            foreach (var visited in world.Animals.Take(visits))
                game.OpenChallenge(visited);
            check(
                game.State == GameState.Exploring
                    && game.CurrentChallenge == null
                    && game.RemainingAnimals == before - 1,
                "all previous visits stay blocked after other rewards, reset and pause"
            );
        }
        check(
            game.RemainingAnimals == 0 && world.Animals.All(a => !a.RewardAvailable) && car.Active,
            "all 10 visits complete and free driving remains available"
        );
        ui.Intended -= RecordSpeech;

        // Verify the complete cosmetic catalogue independently of finite gameplay visits.
        foreach (var accessory in CarAccessory.All)
        {
            if (!car.Appearance.HasAccessory(accessory.Id))
                car.Appearance.ToggleAccessory(accessory.Id);
            check(car.Appearance.HasAccessory(accessory.Id), "accessory equips: " + accessory.Id);
            if (accessory.Id != "wheels")
            {
                var attachment = car.FindChild("Attachment_" + accessory.Id, true, false) as Node3D;
                check(
                    attachment != null && attachment.IsVisibleInTree() && attachment.GetChildCount() > 0,
                    "accessory has visible geometry"
                );
            }
        }
        car.Reset();
        foreach (var body in CarBody.All)
        {
            car.Appearance.ApplyModel(body.Id);
            var visibleModels = car.FindChildren("Coachwork_*", "Node3D", true, false)
                .OfType<Node3D>()
                .Where(n => n.IsVisibleInTree())
                .ToArray();
            check(
                visibleModels.Length == 1
                    && visibleModels[0].Name == "Coachwork_" + body.Id
                    && CarAccessory.All.All(a => car.Appearance.HasAccessory(a.Id)),
                "model swaps preserve accessories and show one coachwork: " + body.Id
            );
        }
        foreach (var look in CarLook.All)
        {
            car.Appearance.Apply(look);
            var meshes = car.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
            check(
                car.Appearance.HasStripes == (look.StripeColor != null)
                    && meshes.Any(m => m.MaterialOverride == VehicleModel.Paint(look.BodyColor))
                    && CarAccessory.All.All(a => car.Appearance.HasAccessory(a.Id)),
                "paint materials and stripes apply while preserving accessories: " + look.Id
            );
        }
        car.Appearance.ToggleAccessory("flower");
        car.Appearance.ToggleAccessory("wheels");
        check(
            !car.Appearance.HasAccessory("flower") && !car.Appearance.BigWheels && car.Appearance.HasAccessory("bow"),
            "accessories can be removed independently"
        );
    }
}
