using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The post to the four corners of the valley (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 2): the whole round of a
/// letter (the postman, the arrows to the keeper, the question, the stamp and the coins, the reply, the resident and more
/// coins), the order of the keepers, which visits all four before any again, the pigeon after a stamp from each, the full
/// album, the saves made at every step of the way and the damaged saves that are turned down.
/// </summary>
public partial class PostChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public PostChecks(Game game, World world, Vehicle car)
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

    private bool InWorld(string name) =>
        world.FindChildren(name, "", false, false).Any(node => !node.IsQueuedForDeletion());

    private void Near(Character who) => car.SetPose(world.Ground.At(who.Position.X + 2, who.Position.Z, .15f), 0);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("post-checks/progress.json");
            var post = game.Post;
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"the post restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"post-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            var postman = world.Postman;
            var pigeon = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Pigeon);
            Check(post.Total == 0 && !post.Busy, "the post starts with no letter handed over and none carried");
            Check(pigeon != null && !pigeon.IsOwned && pigeon.Collector == postman, "the pigeon waits by the postman");

            // ---- Saves of the fresh valley ----
            var node = System
                .Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!
                .AsObject();
            node.Remove("Post");
            node.Remove("Lighthouse");
            string oldFile = DiagnosticArtifacts.PathFor("post-checks/old.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(oldFile)!);
            System.IO.File.WriteAllText(oldFile, node.ToJsonString());
            var old = SaveStore.Read(oldFile);
            Check(
                old is { Post: { Carrying: -1, Reply: -1, Delivered.Length: Post.Keepers }, Lighthouse.Stage: 0 }
                    && old.Post.Delivered.All(count => count == 0),
                "a file written before the post loads with no letters and the lighthouse not begun"
            );
            Rejected(data => data.Post = null!, "no post section");
            Rejected(data => data.Post.Delivered = new int[3], "letters for three keepers");
            Rejected(data => data.Post.Delivered = new[] { 0, -1, 0, 0 }, "a negative count of letters");
            Rejected(data => data.Post.Carrying = 4, "a letter for a fifth keeper");
            Rejected(data => data.Post.Reply = 4, "a reply for a fifth house");
            Rejected(
                data =>
                {
                    data.Post.Carrying = 0;
                    data.Post.Reply = 1;
                },
                "a letter and a reply carried together"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.PostStatus;
                    data.Nearby = World.FarmerId;
                },
                "the postman's panel at the farmer"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.KeeperStatus;
                    data.Nearby = World.PostmanId;
                },
                "a keeper's panel at the postman"
            );
            Rejected(data => data.Nearby = World.LastActorId + 1, "a character beyond the postman");

            // ---- Twelve letters fill the album; each keeper is visited before any is visited again ----
            var order = new List<int>();
            for (int letter = 0; letter < Post.Keepers * Post.StampsPerKeeper + 1; letter++)
            {
                bool save = letter < 2;
                int coinsBefore = game.Coins;
                Near(postman);
                game.OpenPostman(postman);
                Check(
                    game.State == GameState.PostStatus && Has("TakeLetter"),
                    $"the postman offers letter {letter + 1}"
                );
                if (save)
                    RoundTrip("the postman's panel");
                game.TakeLetter();
                int keeper = post.Carrying;
                Check(keeper >= 0 && game.State == GameState.Exploring, $"letter {letter + 1} is on the car");
                Check(InWorld("LetterTrip"), "the arrows lead to the keeper");
                order.Add(keeper);
                if (save)
                {
                    RoundTrip("a letter on its way");
                    Check(post.Carrying == keeper && InWorld("LetterTrip"), "the letter and its arrows come back");
                }
                Near(postman);
                game.OpenPostman(postman);
                Check(!Has("TakeLetter"), "the postman gives no second letter while one is carried");
                game.TakeLetter();
                Check(post.Carrying == keeper, "taking a second letter changes nothing");
                game.FinishInteraction();

                var addressee = world.Keepers[keeper]!;
                Near(addressee);
                game.OpenChallenge(addressee);
                Check(game.State == GameState.Challenge, $"the keeper of the {(Border)keeper} asks a question");
                if (save)
                    RoundTrip("the question of the letter");
                ChallengeChecks.Solve(game);
                Check(
                    game.State == GameState.Reward && post.Delivered[keeper] == order.Count(k => k == keeper),
                    "a right answer hands the letter over"
                );
                bool full = post.AlbumFull && letter == Post.Keepers * Post.StampsPerKeeper - 1;
                Check(
                    game.Coins == coinsBefore + Post.LetterCoins + (full ? Post.AlbumCoins : 0),
                    full ? "the last stamp fills the album and pays its coins" : "the letter pays its coins"
                );
                game.FinishInteraction();
                int house = post.Reply;
                Check(house >= 0 && InWorld("LetterTrip"), "the keeper gives a reply for a house");
                if (save)
                    RoundTrip("a reply on its way");
                var resident = world.Village.Residents[house];
                coinsBefore = game.Coins;
                Near(resident);
                game.OpenChallenge(resident);
                Check(
                    game.State == GameState.Reward && !post.Busy && game.Coins == coinsBefore + Post.ReplyCoins,
                    "the reply is brought home for coins"
                );
                game.FinishInteraction();
                Check(!InWorld("LetterTrip"), "no arrows lead anywhere once the reply is home");
                if (letter == Post.Keepers - 1)
                {
                    Check(order.Distinct().Count() == Post.Keepers, "the first four letters go to the four keepers");
                    Check(pigeon!.IsOwned, "a stamp from every keeper brings the pigeon");
                }
                else if (letter < Post.Keepers - 1)
                    Check(!pigeon!.IsOwned, "the pigeon waits for a stamp from every keeper");
            }
            Check(post.AlbumFull && post.AlbumStamps == 12, "twelve letters fill the album");
            for (int round = 0; round < Post.StampsPerKeeper; round++)
                Check(
                    order.Skip(round * Post.Keepers).Take(Post.Keepers).Distinct().Count() == Post.Keepers,
                    $"round {round + 1} of letters visits every keeper once"
                );
            RoundTrip("a full album");
            GD.Print("POST_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
