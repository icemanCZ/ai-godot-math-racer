using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>): a square of walls with four round towers beside a road of the land
/// beyond the old valley, its gate towards the road. In the castle's frame +Z (<see cref="Inward"/>) points from the road
/// through the gate into the courtyard and +X (<see cref="Right"/>) to its right; every point is on the flat.
/// </summary>
/// <param name="Road">The middle of the road before the castle, where its way leaves the road.</param>
/// <param name="Inward">From the road towards the gate, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
internal sealed record CastleSite(Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>How far from the road the front wall with the gate stands, and the back wall.</summary>
    public const float FrontAlong = 16,
        BackAlong = 48;

    /// <summary>How far from the middle the side walls stand.</summary>
    public const float HalfWidth = 16;

    /// <summary>The ground the castle, its way from the road and the place of the loads take, in the castle's frame.</summary>
    public const float ZoneAlongMin = 2,
        ZoneAlongMax = BackAlong + 5,
        ZoneAcross = HalfWidth + 5;

    /// <summary>Where the loads are tipped out, in front of the gate.</summary>
    public const float DropAlong = 10;

    /// <summary>Local +X of the castle's frame.</summary>
    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the castle's frame (across to the right, along away from the road) on the flat.</summary>
    public Vector3 Local(float across, float along) => Road + Right * across + Inward * along;

    /// <summary>The middle of the courtyard.</summary>
    public Vector3 Center => Local(0, (FrontAlong + BackAlong) / 2);

    /// <summary>The middle of the gate in the front wall.</summary>
    public Vector3 Gate => Local(0, FrontAlong);

    /// <summary>The ring where the loads are tipped out, before the gate.</summary>
    public Vector3 Drop => Local(0, DropAlong);

    /// <summary>Where the princess waits, beside the way to the gate, so that the car meets her before the ring.</summary>
    public Vector3 Talk => Local(-5.5f, 11.5f);
}

/// <summary>
/// A beacon of the castle (<c>docs/CASTLE_PLAN.md</c>, phase 2): a wooden lookout with a basket of fire beside the
/// homestead of a border, raised by one load of logs, whose fire is seen from the castle.
/// </summary>
/// <param name="Border">The border it watches.</param>
/// <param name="Foot">The middle of the tower's foot, on the ground.</param>
/// <param name="Drop">Where its logs are tipped out, between the road's end and the tower.</param>
/// <param name="Road">The end of the road to the border, where the way to the drop starts.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z from the tower towards the drop.</param>
internal sealed record BeaconSite(Border Border, Vector3 Foot, Vector3 Drop, Vector3 Road, float Yaw)
{
    /// <summary>The ground the tower takes round its foot.</summary>
    public const float Radius = 3.5f;

    /// <summary>How tall the tower stands up to the basket of fire.</summary>
    public const float Height = 9;
}
