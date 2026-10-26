using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Recolors registered visual parts without changing driving or shared materials.</summary>
public sealed class VehicleAppearance
{
    private readonly List<MeshInstance3D> body = new();
    private readonly List<MeshInstance3D> roof = new();
    private readonly List<MeshInstance3D> trim = new();
    private readonly Dictionary<string, Node3D> models = new();
    private readonly List<MeshInstance3D> stripes = new();
    public bool HasStripes => stripes.Count > 0 && stripes.TrueForAll(mesh => mesh.IsVisibleInTree());
    public string CurrentModelId { get; private set; } = "classic";
    public void RegisterModel(string id, Node3D node) { models.Add(id, node); node.Visible = id == CurrentModelId; }
    public void ApplyModel(string id)
    {
        if (!models.ContainsKey(id)) return;
        foreach (var model in models) model.Value.Visible = model.Key == id;
        CurrentModelId = id;
    }
    public void SetupStripes(Node3D model)
    {
        foreach (float x in new[] { -.22f, .22f })
        {
            stripes.Add(Art.Block(model, new(x, 1.211f, -1.03f), new(.15f, .012f, .43f), "ffffff"));
            stripes.Add(Art.Block(model, new(x, 2.063f, .18f), new(.15f, .012f, .65f), "ffffff"));
        }
        Apply(CarLook.All[0]);
    }
    private readonly Dictionary<string, Node3D> accessories = new();
    private readonly List<(Node3D Node, Vector3 Position)> wheels = new();
    public bool BigWheels { get; private set; }
    public void RegisterWheel(Node3D wheel) => wheels.Add((wheel, wheel.Position));
    public bool HasAccessory(string id) => id == "wheels" ? BigWheels : accessories.TryGetValue(id, out var node) && node.Visible;
    public void SetupAccessories(Node3D model)
    {
        foreach (var accessory in CarAccessory.All)
            if (accessory.Id != "wheels") accessories.Add(accessory.Id, accessory.Create(model));
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
        if (accessories.TryGetValue(id, out var node)) node.Visible = !node.Visible;
    }
    public string CurrentLookId { get; private set; } = CarLook.All[0].Id;
    public void RegisterBody(MeshInstance3D mesh) => body.Add(mesh);
    public void RegisterRoof(MeshInstance3D mesh) => roof.Add(mesh);
    public void RegisterTrim(MeshInstance3D mesh) => trim.Add(mesh);

    public void Apply(CarLook look)
    {
        foreach (var mesh in body) mesh.MaterialOverride = VehicleModel.Paint(look.BodyColor);
        foreach (var mesh in roof) mesh.MaterialOverride = VehicleModel.Paint(look.RoofColor);
        foreach (var mesh in trim) mesh.MaterialOverride = VehicleModel.Paint(look.TrimColor);
        foreach (var mesh in stripes)
        {
            mesh.Visible = look.StripeColor != null;
            if (look.StripeColor != null) mesh.MaterialOverride = VehicleModel.Paint(look.StripeColor);
        }
        CurrentLookId = look.Id;
    }
}
