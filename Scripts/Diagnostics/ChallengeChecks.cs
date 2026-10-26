using Godot;
using System;
using System.Linq;

namespace MathRacer;

/// <summary>Checks generated questions, single visits, the HUD and real reward UI signals.</summary>
public static class ChallengeChecks
{
    public static void Run(Game game, World world, Vehicle car, Action<bool, string> check)
    {
        var generator = new ChallengeGenerator(53);
        var repeat = new ChallengeGenerator(53);
        var slots = new System.Collections.Generic.HashSet<int>();
        var operations = new System.Collections.Generic.HashSet<MathOperation>();
        for (int i = 0; i < 1000; i++)
        {
            var q = generator.Next(); var other = repeat.Next();
            if (q.Left < 0 || q.Left > 10 || q.Right < 0 || q.Right > 10 || q.Answer < 0 || q.Answer > 10 ||
                q.Choices.Count != 3 || q.Choices.Distinct().Count() != 3 || q.Choices.Any(v => v < 0 || v > 10) ||
                q.Choices.Count(v => v == q.Answer) != 1)
                throw new InvalidOperationException("Question bounds or unique choices failed.");
            if (q.Expression != other.Expression || !q.Choices.SequenceEqual(other.Choices))
                throw new InvalidOperationException("Question stream is not deterministic.");
            slots.Add(q.Choices.ToList().IndexOf(q.Answer)); operations.Add(q.Operation);
        }
        check(slots.Count == 3 && operations.Count == 2, "1000 valid deterministic questions; both operations and all answer positions");

        var letters = new LetterChallengeGenerator(53);
        var repeatLetters = new LetterChallengeGenerator(53);
        var letterSlots = new System.Collections.Generic.HashSet<int>();
        var initials = new System.Collections.Generic.HashSet<string>();
        var pictureDeck = new System.Collections.Generic.HashSet<string>();
        check(LetterChallengeGenerator.Words.Count == 32 && LetterChallengeGenerator.Words.Select(p => p.Id).Distinct().Count() == 32,
            "catalogue contains 32 distinct pictures");
        for (int i = 0; i < 1000; i++)
        {
            var q = letters.Next(); var other = repeatLetters.Next();
            if (q.Pictures.Count != 3 || q.Pictures.Select(p => p.Id).Distinct().Count() != 3 ||
                q.Pictures.Count(p => p.Initial == q.Letter) != 1 || q.Answer < 0 || q.Answer > 2 ||
                q.Letter != other.Letter || !q.Pictures.SequenceEqual(other.Pictures))
                throw new InvalidOperationException("Invalid or nondeterministic letter question.");
            letterSlots.Add(q.Answer); initials.Add(q.Letter);
            if (i < LetterChallengeGenerator.Words.Count && !pictureDeck.Add(q.Pictures[q.Answer].Id))
                throw new InvalidOperationException("Correct picture repeated before the deck was exhausted.");
            if (i == LetterChallengeGenerator.Words.Count - 1)
                check(pictureDeck.SetEquals(LetterChallengeGenerator.Words.Select(p => p.Id)), "all 32 pictures appear as targets before the deck repeats");
        }
        check(letterSlots.Count == 3 && new[] { "Š", "Ž", "Č" }.All(initials.Contains),
            "1000 letter tasks have one matching picture despite shared initials, including Š, Ž and Č");

        var rewardRandom = new Random(83);
        System.Collections.Generic.IReadOnlyList<CarReward> drawn = Array.Empty<CarReward>();
        var seen = new System.Collections.Generic.HashSet<CarReward>();
        for (int i = 0; i < 1000; i++)
        {
            var next = CarReward.Draw(rewardRandom, drawn);
            if (next.Count != 3 || next.Distinct().Count() != 3 || !next.Any(r => r.IsAccessory) ||
                !next.Any(r => !r.IsAccessory && !r.IsModel) || !next.Any(r => r.IsModel) || next.ToHashSet().SetEquals(drawn))
                throw new InvalidOperationException("Invalid mixed reward set.");
            seen.UnionWith(next); drawn = next;
        }
        check(seen.Count == CarLook.All.Count + CarAccessory.All.Count + CarBody.All.Count, "1000 mixed reward sets can offer every paint, accessory and model");

        void Press(string name)
        {
            var button = game.FindChild(name, true, false) as Button;
            if (button == null || button.Disabled) throw new InvalidOperationException("Missing or disabled UI button: " + name);
            button.EmitSignal(Button.SignalName.Pressed);
        }
        void CheckCounter()
        {
            var label = game.FindChild("AnimalCount", true, false) as Label;
            string expected = game.RemainingAnimals == 0 ? "Všechna zvířátka navštívena!" : $"Zbývá zvířátek: {game.RemainingAnimals} / {world.Animals.Length}";
            check(label != null && label.IsVisibleInTree() && label.Text == expected, "visible HUD matches remaining animals");
        }

        // SmokeTest has already visited and left the first animal without answering.
        check(game.RemainingAnimals == world.Animals.Length - 1 && !world.Animals[0].RewardAvailable, "leaving a question consumes exactly one visit");
        game.OpenChallenge(world.Animals[0]);
        check(game.State == GameState.Exploring && game.CurrentChallenge == null, "cancelled animal cannot be reopened");
        CheckCounter();
        string originalLook = car.Appearance.CurrentLookId;
        game.SelectLook("blueberry");
        check(car.Appearance.CurrentLookId == originalLook, "reward unavailable before correct answer");
        int visits = 1;
        int letterVisits = 0;
        int mathVisits = 0;
        foreach (var animal in world.Animals.Skip(1))
        {
            int before = game.RemainingAnimals;
            game.OpenChallenge(animal);
            visits++;
            var question = game.CurrentChallenge!;
            check((Array.IndexOf(world.Animals, animal) % 2 == 0) == (question is MathChallenge), "animal has its assigned task type");
            if (question is LetterChallenge letter)
            {
                letterVisits++;
                check(game.FindChildren("*", "", true, false).OfType<WordPicture>().Count() == 3 &&
                    letter.Pictures.Count(p => p.Initial == letter.Letter) == 1, "letter UI displays exactly three pictures with one correct initial");
            }
            else mathVisits++;
            check(game.State == GameState.Challenge && !car.Active && !animal.RewardAvailable && game.RemainingAnimals == before - 1,
                "opening a question consumes one visit and stops car");
            CheckCounter();
            game.SubmitAnswer(-1);
            check(game.State == GameState.Challenge && ReferenceEquals(question, game.CurrentChallenge), "invalid answer does not complete task");
            int wrong = question.Choices.First(value => value != question.Answer);
            Press($"Answer{wrong}");
            game.TogglePause(); game.TogglePause();
            check(game.State == GameState.Challenge && ReferenceEquals(question, game.CurrentChallenge) && !car.Active &&
                (game.FindChild($"Answer{wrong}", true, false) as Button)?.Disabled == true && game.RemainingAnimals == before - 1,
                "wrong answer and pause preserve the visit, question and rejected choice");
            int secondWrong = question.Choices.First(value => value != question.Answer && value != wrong);
            Press($"Answer{secondWrong}");
            Press($"Answer{question.Answer}");
            check(game.State == GameState.Reward && !car.Active, "correct answer opens reward");
            var rewards = game.CurrentRewards.ToArray();
            game.TogglePause(); game.TogglePause();
            check(game.State == GameState.Reward && game.CurrentRewards.SequenceEqual(rewards), "pause preserves exact reward choices");
            check(game.FindChild("NewChallenge", true, false) == null, "reward cannot start another example at the same animal");
            var absent = CarAccessory.All.First(a => !rewards.Contains(new CarReward(a.Id, true)));
            bool wasEquipped = car.Appearance.HasAccessory(absent.Id);
            game.SelectAccessory(absent.Id);
            check(game.State == GameState.Reward && car.Appearance.HasAccessory(absent.Id) == wasEquipped, "cannot select an unoffered reward");
            if (visits % 4 == 0) Press("KeepLook");
            else
            {
                var reward = rewards.First(r => visits % 4 == 1 ? r.IsModel : visits % 4 == 2 ? !r.IsModel && !r.IsAccessory : r.IsAccessory);
                bool hadAccessory = car.Appearance.HasAccessory(reward.Id);
                Press(reward.ButtonName);
                check(reward.IsModel ? car.Appearance.CurrentModelId == reward.Id : reward.IsAccessory
                    ? car.Appearance.HasAccessory(reward.Id) != hadAccessory : car.Appearance.CurrentLookId == reward.Id,
                    "offered reward UI applies selected model, paint or accessory");
            }
            check(game.State == GameState.Exploring && car.Active, "reward choice resumes driving");
            car.Reset(); game.TogglePause(); game.TogglePause();
            foreach (var visited in world.Animals.Take(visits)) game.OpenChallenge(visited);
            check(game.State == GameState.Exploring && game.CurrentChallenge == null && game.RemainingAnimals == before - 1,
                "all previous visits stay blocked after other rewards, reset and pause");
        }
        check(game.RemainingAnimals == 0 && world.Animals.All(a => !a.RewardAvailable) && car.Active, "all 30 visits complete and free driving remains available");
        check(letterVisits == 15 && mathVisits == 14, "world has 15 letter and 15 math animals including the cancelled first math visit");
        CheckCounter();

        // Verify the complete cosmetic catalogue independently of finite gameplay visits.
        foreach (var accessory in CarAccessory.All)
        {
            if (!car.Appearance.HasAccessory(accessory.Id)) car.Appearance.ToggleAccessory(accessory.Id);
            check(car.Appearance.HasAccessory(accessory.Id), "accessory equips: " + accessory.Id);
            if (accessory.Id != "wheels")
            {
                var attachment = car.FindChild("Attachment_" + accessory.Id, true, false) as Node3D;
                check(attachment != null && attachment.IsVisibleInTree() && attachment.GetChildCount() > 0, "accessory has visible geometry");
            }
        }
        car.Reset();
        foreach (var body in CarBody.All)
        {
            car.Appearance.ApplyModel(body.Id);
            var visibleModels = car.FindChildren("Coachwork_*", "Node3D", true, false).OfType<Node3D>().Where(n => n.IsVisibleInTree()).ToArray();
            check(visibleModels.Length == 1 && visibleModels[0].Name == "Coachwork_" + body.Id && CarAccessory.All.All(a => car.Appearance.HasAccessory(a.Id)),
                "model swaps preserve accessories and show one coachwork: " + body.Id);
        }
        foreach (var look in CarLook.All)
        {
            car.Appearance.Apply(look);
            var meshes = car.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
            check(car.Appearance.HasStripes == (look.StripeColor != null) && meshes.Any(m => m.MaterialOverride == VehicleModel.Paint(look.BodyColor)) &&
                CarAccessory.All.All(a => car.Appearance.HasAccessory(a.Id)), "paint materials and stripes apply while preserving accessories: " + look.Id);
        }
        car.Appearance.ToggleAccessory("flower"); car.Appearance.ToggleAccessory("wheels");
        check(!car.Appearance.HasAccessory("flower") && !car.Appearance.BigWheels && car.Appearance.HasAccessory("bow"), "accessories can be removed independently");
    }
}
