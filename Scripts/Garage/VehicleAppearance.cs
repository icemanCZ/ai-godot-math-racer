using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Vehicles;

namespace MathRacer.Garage;

/// <summary>Recolors registered visual parts without changing driving or shared materials.</summary>
public sealed class VehicleAppearance
{
    private readonly List<MeshInstance3D> body = new();
    private readonly List<MeshInstance3D> roof = new();
    private readonly List<MeshInstance3D> trim = new();
    private readonly Dictionary<string, Node3D> models = new();

    /// <summary>
    /// The racing stripes. Those of the cars (no model) lie on the bonnet and the roof and are shared by all four cars;
    /// every other model has stripes of its own, on a part of its own shape.
    /// </summary>
    private readonly List<(MeshInstance3D Mesh, string? Model)> stripes = new();
    private CarLook look = CarLook.All[0];

    public bool HasStripes
    {
        get
        {
            var shown = stripes.FindAll(IsShown);
            return shown.Count > 0 && shown.TrueForAll(stripe => stripe.Mesh.IsVisibleInTree());
        }
    }

    public string CurrentModelId { get; private set; } = "classic";

    /// <summary>Where the rear wheel on the right stands, which is where the tyre marks and the stamps begin.</summary>
    public (float X, float Z) RearWheel
    {
        get
        {
            var body = CarBody.Find(CurrentModelId);
            return (body?.TrackX ?? CarBody.CarTrackX, body?.TrackZ ?? CarBody.CarTrackZ);
        }
    }

    private bool IsShown((MeshInstance3D Mesh, string? Model) stripe) =>
        stripe.Model == null ? !stripes.Exists(other => other.Model == CurrentModelId) : stripe.Model == CurrentModelId;

    public void RegisterModel(string id, Node3D node)
    {
        models.Add(id, node);
        node.Visible = id == CurrentModelId;
    }

    public void ApplyModel(string id)
    {
        if (!models.ContainsKey(id))
            return;
        foreach (var model in models)
            model.Value.Visible = model.Key == id;
        CurrentModelId = id;
        // A model with wheels of its own (the tank's road wheels, the plane's landing gear) hides the four tyres.
        bool ownWheels = CarBody.Find(id)?.OwnWheels ?? false;
        foreach (var wheel in wheels)
            if (wheel.Model == null)
                wheel.Node.Visible = !ownWheels;
        RefreshStripes();
        foreach (var (accessory, node) in accessories)
            if (accessory == CarAccessory.ValleyMap.Id)
                CarAccessory.PlaceValleyMap(node, id);
            else if (accessoryHome.TryGetValue(accessory, out var home))
                node.Position = home + CarAccessory.Offset(id, accessory);
    }

    public void SetupStripes(Node3D model)
    {
        foreach (float x in new[] { -.22f, .22f })
        {
            stripes.Add((Art.Block(model, new(x, 1.211f, -1.03f), new(.15f, .012f, .43f), "ffffff"), null));
            stripes.Add((Art.Block(model, new(x, 2.063f, .18f), new(.15f, .012f, .65f), "ffffff"), null));
        }
        Apply(CarLook.All[0]);
    }

    /// <summary>A stripe on a part of a model that is not a car; it shows only while that model is on.</summary>
    public void RegisterStripe(MeshInstance3D mesh, string model) => stripes.Add((mesh, model));

    private readonly Dictionary<string, Node3D> accessories = new();
    private readonly Dictionary<string, Vector3> accessoryHome = new();
    private readonly List<(Node3D Node, Vector3 Position, string? Model)> wheels = new();
    public bool BigWheels { get; private set; }

    /// <summary>A wheel that bigger wheels from the garage enlarge; <paramref name="model"/> is the toy that owns it.</summary>
    public void RegisterWheel(Node3D wheel, string? model = null) => wheels.Add((wheel, wheel.Position, model));

    /// <summary>Whether <paramref name="node"/> is a wheel of the model that is on, not a hidden tyre or a propeller.</summary>
    public bool IsTyreShown(Node3D node) => wheels.Exists(wheel => wheel.Node == node) && node.IsVisibleInTree();

    public bool HasAccessory(string id) =>
        id == "wheels" ? BigWheels : accessories.TryGetValue(id, out var node) && node.Visible;

    public void SetupAccessories(Node3D model)
    {
        foreach (var accessory in CarAccessory.Catalogue)
            if (accessory.Id != "wheels")
            {
                var node = accessory.Create(model);
                accessories.Add(accessory.Id, node);
                accessoryHome[accessory.Id] = node.Position;
            }
    }

    public void ToggleAccessory(string id)
    {
        if (id == "wheels")
        {
            BigWheels = !BigWheels;
            foreach (var wheel in wheels)
            {
                wheel.Node.Scale = BigWheels ? new Vector3(1.15f, 1.3f, 1.3f) : Vector3.One;
                wheel.Node.Position = wheel.Position + (BigWheels ? Vector3.Up * .147f : Vector3.Zero);
            }
            return;
        }
        if (accessories.TryGetValue(id, out var node))
            node.Visible = !node.Visible;
    }

    public string CurrentLookId { get; private set; } = CarLook.All[0].Id;

    /// <summary>What the wheels leave behind: <see cref="CarTrail.Plain"/> or the id of a stamp.</summary>
    public string CurrentTrailId { get; private set; } = CarTrail.Plain.Id;

    public void ApplyTrail(string? id)
    {
        if (CarTrail.IsKnown(id))
            CurrentTrailId = id!;
    }

    public void RegisterBody(MeshInstance3D mesh) => body.Add(mesh);

    public void RegisterRoof(MeshInstance3D mesh) => roof.Add(mesh);

    public void RegisterTrim(MeshInstance3D mesh) => trim.Add(mesh);

    public void Apply(CarLook look)
    {
        foreach (var mesh in body)
            mesh.MaterialOverride = VehicleModel.Paint(look.BodyColor);
        foreach (var mesh in roof)
            mesh.MaterialOverride = VehicleModel.Paint(look.RoofColor);
        foreach (var mesh in trim)
            mesh.MaterialOverride = VehicleModel.Paint(look.TrimColor);
        this.look = look;
        RefreshStripes();
        CurrentLookId = look.Id;
    }

    private void RefreshStripes()
    {
        foreach (var stripe in stripes)
        {
            bool shown = look.StripeColor != null && IsShown(stripe);
            stripe.Mesh.Visible = shown;
            if (shown)
                stripe.Mesh.MaterialOverride = VehicleModel.Paint(look.StripeColor!);
        }
    }
}
