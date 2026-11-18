using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>The pets that hauling and building earn: where they wait and what they count.</summary>
public partial class World
{
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
        void Add(WorldPetSpec spec, Character? owner, System.Func<int> count, int goal)
        {
            if (owner == null)
                return;
            var pet = new CollectorPet(spec, owner, count, goal, index++);
            var spot = owner.Position + owner.Basis * new Vector3(1.9f, 0, .35f);
            pet.SetHome(Ground.At(spot.X, spot.Z), owner.Rotation.Y);
            AddChild(pet);
            AddWorldPet(pet);
        }

        foreach (var load in new[] { HaulLoad.Logs, HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Wheat })
            Add(
                WorldPetCatalog.ForSource(load),
                SourceWorker(load) ?? ExtraSourceWorker(load),
                () => Tally.RawCount(load),
                WorldPetCatalog.SourceLoads
            );
        foreach (var works in AllWorks)
        {
            int worksIndex = works.Site.Index;
            Add(
                WorldPetCatalog.ForWorks(worksIndex),
                works.Worker,
                () => Tally.WorksCount(worksIndex),
                WorldPetCatalog.WorksLoads
            );
        }
        foreach (int project in new[] { BuildPlans.Bridge, BuildPlans.Mill, BuildPlans.Tower })
        {
            if (project >= Projects.Length || Projects[project] is not { } built)
                continue;
            Add(WorldPetCatalog.ForBuilding(project), built.Builder, () => built.Stage, BuildPlans.Trips);
        }
    }
}
