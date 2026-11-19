using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

public partial class World
{
    internal const int StationId = ActorIds.Station;

    internal static bool IsStationHelper(int id) => id == StationId;

    /// <summary>The little railway round the playground, or null in a valley that has no playground.</summary>
    internal Railway? Railway { get; private set; }

    /// <summary>
    /// Raises the railway and its stationmaster, who has the next free actor ID. It lies in the yard's frame, inside the
    /// ground that the playground already clears, and the stationmaster is registered last, after every search that
    /// keeps clear of the actors, so nothing else in a valley moves for either.
    /// </summary>
    private void BuildRailway()
    {
        if (playgroundSite is not { } site)
            return;
        var railway = new Railway(site, Ground);
        AddChild(railway);
        var master = Character.Create(CharacterCatalog.Stationmaster);
        master.Role = new StationRole(railway);
        master.DressAsStationmaster();
        master.Position = railway.HelperSpot;
        // The stationmaster faces the track, which lies on the road side of the platform.
        master.Rotation = new Vector3(0, Mathf.Atan2(-site.Inward.X, -site.Inward.Z), 0);
        AddChild(master);
        railway.Helper = master;
        Register(StationId, master);
        Railway = railway;
        // The finished railway's pet, a little stationmaster, waits on the platform beside the big one.
        var pet = new CollectorPet(
            WorldPetCatalog.Stationmaster,
            master,
            () => railway.Stage,
            RailwayPlans.Trips,
            worldPets.Count
        );
        var right = Vector3.Up.Cross(site.Inward);
        pet.SetHome(railway.HelperSpot - right * 1.2f - site.Inward * .3f, master.Rotation.Y);
        AddChild(pet);
        AddWorldPet(pet);
    }
}
