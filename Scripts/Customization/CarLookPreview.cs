using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Actual reward geometry in an isolated, softly lit studio.</summary>
public partial class CarLookPreview : SubViewportContainer
{
    public CarLook Look { get; set; } = CarLook.All[0];
    public string? AccessoryId { get; set; }
    public string ModelId { get; set; } = "classic";
    public string? ExpeditionCargo { get; set; }
    public int CargoAmount { get; set; } = 1;
    public IReadOnlyList<string> Accessories { get; set; } = System.Array.Empty<string>();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(205, 160);
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
    }

    public override void _Process(double delta)
    {
        // Build only once the menu survives a frame, avoiding unused render scenes.
        if (!IsVisibleInTree()) return;
        SetProcess(false);
        var viewport = new SubViewport {
            Size = new Vector2I(410, 320), OwnWorld3D = true, TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X
        };
        AddChild(viewport);
        var studio = new Node3D(); viewport.AddChild(studio);
        studio.AddChild(new WorldEnvironment {
            Environment = new Godot.Environment {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("fff3df"), AmbientLightEnergy = .85f,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        studio.AddChild(new DirectionalLight3D {
            RotationDegrees = new Vector3(-45, -35, 0), LightEnergy = 1.5f,
            LightColor = new Color("fff6e5")
        });
        studio.AddChild(new DirectionalLight3D {
            RotationDegrees = new Vector3(-25, 140, 0), LightEnergy = .65f,
            LightColor = new Color("d6efff")
        });
        var car = new Node3D(); studio.AddChild(car);
        var appearance = new VehicleAppearance();
        VehicleModel.BuildDisplay(car, appearance, new List<Node3D>(), ModelId);
        appearance.Apply(Look);
        if (AccessoryId != null) appearance.ToggleAccessory(AccessoryId);
        foreach (var id in Accessories) appearance.ToggleAccessory(id);
        if (ExpeditionCargo != null)
        {
            var rack = new Node3D { Position = new(0, 2.4f, .3f) }; car.AddChild(rack);
            foreach (float x in new[] { -.65f, .65f }) Art.Block(rack, new(x, -.23f, 0), new(.1f, .4f, .8f), "795b43");
            Art.Block(rack, Vector3.Zero, new(1.8f, .28f, 1.05f), "bc8b61");
            for (int i = 0; i < CargoAmount; i++) Expedition.AddCargo(rack, new(-.6f + i * .6f, .5f, 0), ExpeditionCargo);
            // A destination pennant distinguishes the expedition from a cosmetic upgrade.
            Art.Cylinder(studio, new(-1.6f, 1.35f, .4f), .045f, .045f, 2.7f, "795b43");
            var flag = new SurfaceTool(); flag.Begin(Mesh.PrimitiveType.Triangles);
            flag.AddVertex(new(-1.6f, 2.7f, .4f)); flag.AddVertex(new(-.72f, 2.42f, .4f)); flag.AddVertex(new(-1.6f, 2.14f, .4f));
            studio.AddChild(new MeshInstance3D { Mesh = flag.Commit(), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("27bd62").SrgbToLinear(), CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = .8f } });
        }

        bool rear = AccessoryId is "spare" or "exhaust" or "spoiler" or "antenna" or "flag";
        var camera = new Camera3D {
            Projection = Camera3D.ProjectionType.Orthogonal, Size = 4.5f,
            Position = new Vector3(4, 3.4f, rear ? 5 : -5), Current = true
        };
        studio.AddChild(camera);
        camera.LookAt(new Vector3(0, ExpeditionCargo != null ? 1.5f : 1.25f, 0));
    }
}
