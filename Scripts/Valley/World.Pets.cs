using System;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>The pets that hauling and building earn: where they wait and what they count.</summary>
public partial class World
{
    /// <summary>
    /// The pieces a works has made in all, which earn its pet: the counted loads, but never fewer than the stock that
    /// stands on the yard, because that was made there too even if an older save never counted it.
    /// </summary>
    internal int WorksMade(Works works) => Math.Max(Tally.WorksCount(works.Site.Index) * Works.PerLoad, works.Stock);

    /// <summary>
    /// The loads of a raw material that were hauled, which earn the pet of its source: the counted loads, but never fewer
    /// than the loads that the goods standing in the works of this material were made from.
    /// </summary>
    internal int LoadsHauled(HaulLoad load) =>
        AllWorks
            .Where(works => works.Spec.Input == load)
            .Select(works => (works.Stock + Works.PerLoad - 1) / Works.PerLoad)
            .Append(Tally.RawCount(load))
            .Max();

    private void AddWorldPet(CollectorPet pet)
    {
        worldPets.Add(pet);
        allPets = null;
    }

    /// <summary>
    /// Creates a pet for each raw material, each works and the bridge, the mill and the tower (the jetty's duck already
    /// exists). Each waits beside the worker or builder it belongs to. A valley that lacks a piece, such as a works or
    /// a second sand pit, simply has no pet for it.
    /// </summary>
    private void CreateWorldPets()
    {
        int index = 0;
        void Add(WorldPetSpec spec, Character? owner, System.Func<int> count, int goal, Vector3? side = null)
        {
            if (owner == null)
                return;
            var pet = new CollectorPet(spec, owner, count, goal, index++);
            var spot = owner.Position + owner.Basis * (side ?? new Vector3(1.9f, 0, .35f));
            pet.SetHome(Ground.At(spot.X, spot.Z), owner.Rotation.Y);
            AddChild(pet);
            AddWorldPet(pet);
        }

        foreach (var load in new[] { HaulLoad.Logs, HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Wheat })
            Add(
                WorldPetCatalog.ForSource(load),
                SourceWorker(load) ?? ExtraSourceWorker(load),
                () => LoadsHauled(load),
                WorldPetCatalog.SourceLoads
            );
        foreach (var works in AllWorks)
        {
            Add(
                WorldPetCatalog.ForWorks(works.Site.Index),
                works.Worker,
                () => WorksMade(works),
                WorldPetCatalog.WorksPieces
            );
        }
        foreach (int project in new[] { BuildPlans.Bridge, BuildPlans.Mill, BuildPlans.Tower })
        {
            if (project >= Projects.Length || Projects[project] is not { } built)
                continue;
            Add(WorldPetCatalog.ForBuilding(project), built.Builder, () => built.Stage, BuildPlans.Trips);
        }
        // The witch hid the small friends, so their pets wait around her: right, left and in front.
        var seats = new[] { new Vector3(1.9f, 0, .35f), new Vector3(-1.9f, 0, .35f), new Vector3(0, 0, 2.6f) };
        foreach (var kind in System.Enum.GetValues<HiddenKind>())
            Add(
                WorldPetCatalog.ForHidden((int)kind),
                Witch,
                () => Hidden.FoundOf(kind),
                HiddenFriends.PerKind,
                seats[(int)kind]
            );
    }
}
