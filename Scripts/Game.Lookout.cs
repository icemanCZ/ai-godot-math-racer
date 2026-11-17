using Godot;

namespace MathRacer;

/// <summary>
/// The finished lookout tower: the stargazer lets the player climb up for a look round the valley. The camera
/// turns once on the platform while the coins and the friends who wait for a quiz are lit by tall beams. After
/// that the stargazer needs a while before the next look.
/// </summary>
public partial class Game
{
    internal const float LookoutSeconds = 9;
    internal const float LookoutCooldown = 60;
    internal const float RadarSeconds = 14;

    private float lookoutTime;
    private float lookoutRest;

    /// <summary>0 when the tower is not finished, 1 when a look is ready, 2 while the stargazer rests.</summary>
    private int LookoutState =>
        world.Projects.Length > BuildPlans.Tower && world.Projects[BuildPlans.Tower].Complete
            ? (lookoutRest > 0 ? 2 : 1)
            : 0;

    internal bool LookingAround => lookoutTime > 0;

    internal void BeginLookout()
    {
        if (
            State != GameState.BuildStatus
            || ProjectOf(nearby) is not Lookout tower
            || LookoutState != 1
            || lookoutTime > 0
            || Haul != null
        )
            return;
        Start();
        vehicle.Active = false;
        lookoutTime = LookoutSeconds;
        lookoutRest = LookoutCooldown;
        var platform = tower.Position + tower.Site.Inward * 12 + Vector3.Up * (tower.PlatformHeight + 1.8f);
        float facing = Mathf.Atan2(tower.Site.Inward.X, tower.Site.Inward.Z);
        followCamera.ShowPanorama(platform, LookoutSeconds, facing);
        WorldCoins.Flash(RadarSeconds);
        foreach (var animal in world.Animals)
            if (animal.RewardAvailable)
                animal.Ping(RadarSeconds);
        tower.Builder.Celebrate();
        Speak("Podívej se, co všechno je v údolí! Zlaté sloupy ukazují mince a modré kamarády.", tower.Builder);
    }

    private void TickLookout(float delta)
    {
        if (lookoutRest > 0 && State is not (GameState.Paused or GameState.Title))
            lookoutRest = Mathf.Max(0, lookoutRest - delta);
        if (lookoutTime <= 0)
            return;
        if (State == GameState.Exploring)
        {
            // The car waits while the camera turns, even after a pause has brought the controls back.
            vehicle.Active = false;
            lookoutTime = Mathf.Max(0, lookoutTime - delta);
            if (lookoutTime <= 0)
                vehicle.Active = true;
        }
    }
}
