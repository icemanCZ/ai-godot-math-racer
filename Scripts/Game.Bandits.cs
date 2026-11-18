using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class Game
{
    internal const int BanditActorId = ActorIds.Bandit;
    internal const int BanditQuestionCount = 3;
    internal const int MaxBanditSites = 8;
    private const float BanditRunTime = 1.15f;
    internal const float BanditCooldownTime = 35;

    private readonly record struct BanditSite(Vector3 Road, Vector3 Verge, Vector3 Cover, Vector3 Along);

    private BanditSite[] banditSites = Array.Empty<BanditSite>();
    private readonly Animal[] bandits = new Animal[3];
    private int banditSite = -1;
    private int banditSolved;
    private float banditCooldown;
    private float banditArrival;
    private float banditRetreat;
    private bool InBanditQuiz => nearby != null && nearby == bandits[0];
    internal int BanditSiteCount => banditSites.Length;

    internal (Vector3 Road, Vector3 Along) BanditApproach(int index) =>
        (banditSites[index].Road, banditSites[index].Along);

    private void SetupBandits()
    {
        var anchors = world.Routes.NavigationAnchors.ToArray();
        new Random(Settings.Seed + 622).Shuffle(anchors);
        var trees = world.Plants.TreePositions.ToArray();
        var sites = new System.Collections.Generic.List<BanditSite>();
        foreach (var (road, along) in anchors)
        {
            if (
                world.Village.IsNearVillage(road)
                || FlatDistanceSquared(road, Vector3.Zero) < 150 * 150
                || FlatDistanceSquared(road, Vector3.Zero) > 475 * 475
                || world.Animals.Any(a => FlatDistanceSquared(a.Position, road) < 36 * 36)
                || FlatDistanceSquared(world.Sorcerer.Position, road) < 42 * 42
                || FlatDistanceSquared(world.Witch.Position, road) < 36 * 36
                || sites.Any(site => FlatDistanceSquared(site.Road, road) < 70 * 70)
                || Events.Points.Cast<Vector3>().Any(point => FlatDistanceSquared(point, road) < 30 * 30)
                || viewpoints.Any(view => FlatDistanceSquared(view.Stop, road) < 30 * 30)
            )
                continue;

            var side = along.Cross(Vector3.Up).Normalized();
            foreach (float sign in new[] { 1f, -1f })
            {
                var verge = road + side * (sign * 5.2f);
                var cover = road + side * (sign * 15f);
                if (
                    world.Routes.Distance(verge) < 4.5f
                    || world.Routes.Distance(cover) < 10
                    || trees.Count(tree => FlatDistanceSquared(tree, cover) < 10 * 10) < 3
                    || !ClearBanditRoute(cover, verge, along)
                )
                    continue;
                sites.Add(new BanditSite(road, verge, cover, along));
                break;
            }
            if (sites.Count == MaxBanditSites)
                break;
        }
        banditSites = sites.ToArray();
        for (int i = 0; i < bandits.Length; i++)
        {
            var bandit = Animal.Create("", "", i, new[] { "d4af92", "c87963", "9c806e" }[i]);
            bandit.DressAsBandit(new[] { "9664a8", "bd7068", "648baa" }[i]);
            world.AddChild(bandit);
            bandit.Guide(false);
            bandit.Hide();
            bandit.AnimationPaused = true;
            bandits[i] = bandit;
        }
    }

    private bool ClearBanditRoute(Vector3 cover, Vector3 verge, Vector3 along)
    {
        for (int member = -1; member <= 1; member++)
        for (int step = 0; step <= 10; step++)
        {
            var point = cover.Lerp(verge, step / 10f) + along * (member * 2.5f);
            if (River.Distance(point.X, point.Z) < River.HalfWidth(point.Z) + 3 || world.Plants.NearTrunk(point, 1.05f))
                return false;
        }
        return true;
    }

    private void PositionBandits(float progress, bool retreating)
    {
        var site = banditSites[banditSite];
        for (int i = 0; i < bandits.Length; i++)
        {
            var bandit = bandits[i];
            float fraction = retreating ? 1 - progress : progress;
            float step = fraction >= 1 ? 1 : Mathf.Clamp(fraction * 1.24f - i * .11f, 0, 1);
            var p =
                site.Cover.Lerp(site.Verge, step)
                + site.Along
                    * (
                        i == 0 ? 0
                        : i == 1 ? -2.5f
                        : 2.5f
                    );
            bandit.Position = world.Ground.At(p.X, p.Z);
            var direction =
                retreating ? site.Cover - site.Verge
                : step < 1 ? site.Verge - site.Cover
                : vehicle.Position - bandit.Position;
            bandit.Rotation = new(0, Mathf.Atan2(direction.X, direction.Z), 0);
            bandit.Running = step > 0 && step < 1;
            bandit.Visible = step > 0;
            bandit.AnimationPaused = false;
            bandit.Guide(i == 0 && !retreating && step >= 1);
        }
    }

    private void TickBandits(float delta)
    {
        if (State is GameState.Paused or GameState.Title)
            return;
        if (banditArrival > 0 && State == GameState.Challenge && InBanditQuiz)
        {
            banditArrival = Mathf.Max(0, banditArrival - delta);
            PositionBandits(1 - banditArrival / BanditRunTime, retreating: false);
            if (banditArrival == 0)
            {
                ShowChallenge();
                Speak("Stůj, pocestný! Vyřeš tři úkoly, nebo tě dál nepustíme!", bandits[0]);
            }
            return;
        }
        if (banditRetreat > 0)
        {
            banditRetreat = Mathf.Max(0, banditRetreat - delta);
            PositionBandits(1 - banditRetreat / BanditRunTime, retreating: true);
        }
        if (State != GameState.Exploring)
            return;
        banditCooldown = Mathf.Max(0, banditCooldown - delta);
        if (
            banditCooldown > 0
            || Events.Active >= 0
            || CelebrationShowing
            || nearby != null
            || Mathf.Abs(vehicle.Speed) < 1.5f
            || world.Routes.Distance(vehicle.Position) > 4
        )
            return;
        for (int i = 0; i < banditSites.Length; i++)
        {
            if (i == banditSite || FlatDistanceSquared(banditSites[i].Road, vehicle.Position) > 13 * 13)
                continue;
            banditSite = i;
            banditSolved = 0;
            banditArrival = BanditRunTime;
            nearby = bandits[0];
            vehicle.Active = false;
            PositionBandits(0, retreating: false);
            BeginQuiz();
            SaveProgress();
            return;
        }
    }

    private void AdvanceBanditQuiz()
    {
        if (++banditSolved == BanditQuestionCount)
        {
            var position = bandits[0].Position;
            CloseInteraction();
            CelebrateAt(position, "✓  3 / 3");
            AddCoins(BanditCoins);
        }
        else
            BeginQuiz();
        SaveProgress();
    }

    private void DismissBandits()
    {
        if (!InBanditQuiz)
            return;
        banditRetreat = BanditRunTime - banditArrival;
        banditArrival = 0;
        banditCooldown = BanditCooldownTime;
        banditSolved = 0;
        nearby = null;
    }

    private bool RestoreBandits(SaveData data)
    {
        banditSite = data.Bandits.Site < banditSites.Length ? data.Bandits.Site : -1;
        banditCooldown = data.Bandits.Cooldown;
        banditSolved = data.Bandits.Solved;
        banditArrival = banditRetreat = 0;
        foreach (var bandit in bandits)
            bandit.Hide();
        if (data.Nearby != BanditActorId)
            return true;
        if (banditSite < 0)
            return false;
        PositionBandits(1, retreating: false);
        return true;
    }
}
