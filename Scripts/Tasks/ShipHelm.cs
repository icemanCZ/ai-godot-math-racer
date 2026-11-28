using System;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Tasks;

/// <summary>
/// What the player does with the helm in a frame: <see cref="Steer"/> from -1 (left) to 1 (right) as the car's steering,
/// <see cref="Sails"/> 1 for full sails (the accelerator), -1 to furl them (the brake), 0 for neither.
/// </summary>
internal readonly record struct HelmInput(float Steer, float Sails);

/// <summary>
/// How the ship moves on a voyage (<c>docs/SHIP_PLAN.md</c>), without a scene, so that the checks can sail it with any
/// input: the rudder follows the steering and turns the ship slowly, the sails set how fast it goes (it never stops), the
/// steering left alone hands it to the autopilot, which turns it towards the next buoy, and water too shallow ahead turns
/// its bow back to open water, so that it can neither run aground nor sail off the sea.
/// </summary>
internal sealed class ShipHelm
{
    internal Vector3 Position { get; private set; }
    internal float Heading { get; private set; }
    internal float Speed { get; private set; }

    /// <summary>Where the rudder stands, -1 (left) to 1 (right).</summary>
    internal float Rudder { get; private set; }

    /// <summary>How full the sails stand, 0 furled to 1 full.</summary>
    internal float Fill { get; private set; } = .5f;

    /// <summary>How long the steering has been left alone.</summary>
    internal float Idle { get; private set; }

    internal bool Autopilot => Idle >= ShipPlans.AutopilotAfter;

    /// <summary>The bow was turned away from shallow water in the last step.</summary>
    internal bool Shoal { get; private set; }

    internal ShipHelm(Vector3 position, float heading)
    {
        Position = position with { Y = 0 };
        Heading = heading;
        Speed = ShipPlans.HalfSpeed * .5f;
    }

    internal static Vector3 Forward(float heading) => new(Mathf.Sin(heading), 0, Mathf.Cos(heading));

    internal static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>
    /// One step of <paramref name="dt"/> seconds towards <paramref name="target"/> (for the autopilot);
    /// <paramref name="depth"/> tells how deep the water is at a point, negative on land.
    /// </summary>
    internal void Step(float dt, HelmInput input, Vector3 target, Func<Vector3, float> depth)
    {
        Idle = Mathf.Abs(input.Steer) > .1f ? 0 : Idle + dt;
        float want;
        if (!Autopilot)
            want = Mathf.Clamp(input.Steer, -1, 1);
        else
        {
            float turn = Mathf.Wrap(YawTo(Position, target) - Heading, -Mathf.Pi, Mathf.Pi);
            want = Mathf.Clamp(-turn * 2.2f, -.7f, .7f);
        }
        // Shallow water ahead: the bow turns to whichever side is deeper, whatever the helm wants.
        float here = depth(Position);
        float ahead = depth(Position + Forward(Heading) * ShipPlans.LookAhead);
        Shoal = ahead < ShipPlans.LeastDepth && ahead <= here + .05f;
        if (Shoal)
        {
            float left = depth(Position + Forward(Heading + .6f) * ShipPlans.LookAhead),
                right = depth(Position + Forward(Heading - .6f) * ShipPlans.LookAhead);
            want = left >= right ? -1 : 1;
        }
        Rudder = Mathf.MoveToward(Rudder, want, dt * 2.5f);
        // The sails: full with the accelerator, furled with the brake, half otherwise; slower in the shallows.
        float sails =
            Shoal ? 0
            : input.Sails > .1f ? 1
            : input.Sails < -.1f ? 0
            : .5f;
        Fill = Mathf.MoveToward(Fill, sails, dt * .8f);
        float speed =
            Fill >= .5f
                ? Mathf.Lerp(ShipPlans.HalfSpeed, ShipPlans.FullSpeed, (Fill - .5f) * 2)
                : Mathf.Lerp(ShipPlans.FurledSpeed, ShipPlans.HalfSpeed, Fill * 2);
        Speed = Mathf.MoveToward(Speed, speed, dt * 1.2f);
        // The rudder turns the ship more the faster it sails, but a slow ship still answers it.
        float grip = .55f + .45f * Mathf.Clamp(Speed / ShipPlans.FullSpeed, 0, 1);
        Heading = Mathf.Wrap(Heading - Rudder * ShipPlans.TurnRate * grip * dt, -Mathf.Pi, Mathf.Pi);
        var next = Position + Forward(Heading) * Speed * dt;
        // It never sails into water where it would ground: there it only turns.
        float there = depth(next);
        if (there >= 1 || there > here)
            Position = next;
    }

    /// <summary>Puts the ship somewhere new, as the docking does.</summary>
    internal void Place(Vector3 position, float heading)
    {
        Position = position with { Y = 0 };
        Heading = heading;
    }
}

/// <summary>
/// One voyage of the ship (<c>docs/SHIP_PLAN.md</c>): out from the anchorage along the buoys to the turtle island, or home
/// again straight to the anchorage. A buoy the ship passes near enough rings; one it has passed by is left behind; near
/// the pier at the end the ship docks by itself, and a voyage that takes too long docks all the same.
/// </summary>
internal sealed class ShipVoyage
{
    private readonly Vector3[] buoys;
    private Vector3 dockFrom;
    private float dockFromHeading;

    internal ShipHelm Helm { get; }

    /// <summary>Whether this is the voyage home from the island.</summary>
    internal bool Home { get; }

    /// <summary>The pier at the end of the voyage, and the heading the ship lies there with.</summary>
    internal Vector3 Berth { get; }
    internal float BerthHeading { get; }

    internal float Time { get; private set; }

    /// <summary>The buoy the ship makes for now; past the last one, the berth.</summary>
    internal int NextBuoy { get; private set; }

    /// <summary>How many buoys rang on this voyage.</summary>
    internal int Rung { get; private set; }

    /// <summary>Seconds into the docking, or less than 0 while the ship still sails.</summary>
    internal float Docking { get; private set; } = -1;

    internal bool Done { get; private set; }

    internal ShipVoyage(Vector3 from, Vector3[] buoys, Vector3 berth, float berthHeading, bool home)
    {
        this.buoys = home ? Array.Empty<Vector3>() : buoys;
        Home = home;
        Berth = berth with { Y = 0 };
        BerthHeading = berthHeading;
        var first = this.buoys.Length > 0 ? this.buoys[0] : Berth;
        Helm = new ShipHelm(from, ShipHelm.YawTo(from, first));
    }

    internal Vector3 Target => NextBuoy < buoys.Length ? buoys[NextBuoy] : Berth;

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>One step of the voyage; the number of the buoy that rang in it, or -1.</summary>
    internal int Step(float dt, HelmInput input, Func<Vector3, float> depth)
    {
        Time += dt;
        if (Docking >= 0)
        {
            Docking += dt;
            float u = Mathf.Clamp(Docking / 3, 0, 1),
                eased = u * u * (3 - 2 * u);
            Helm.Place(dockFrom.Lerp(Berth, eased), Mathf.LerpAngle(dockFromHeading, BerthHeading, eased));
            Done = u >= 1;
            return -1;
        }
        Helm.Step(dt, input, Target, depth);
        var at = Helm.Position;
        int rang = -1;
        while (NextBuoy < buoys.Length)
        {
            var buoy = buoys[NextBuoy];
            float distance = Flat(at, buoy);
            bool behind = (buoy - at).Dot(ShipHelm.Forward(Helm.Heading)) < 0;
            bool nearerNext = NextBuoy + 1 < buoys.Length && Flat(at, buoys[NextBuoy + 1]) < distance - 30;
            if (distance < ShipPlans.BuoyReach)
            {
                rang = NextBuoy;
                Rung++;
                NextBuoy++;
            }
            else if (behind && distance < 60 || nearerNext)
                NextBuoy++;
            else
                break;
        }
        if (Flat(at, Berth) < ShipPlans.DockReach || Time > ShipPlans.LongestVoyage)
            Dock();
        return rang;
    }

    /// <summary>The ship turns in to the pier by itself.</summary>
    internal void Dock()
    {
        if (Docking >= 0)
            return;
        Docking = 0;
        dockFrom = Helm.Position;
        dockFromHeading = Helm.Heading;
        NextBuoy = buoys.Length;
    }
}
