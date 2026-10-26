using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Actual reward geometry in an isolated, softly lit studio.</summary>
public partial class CarLookPreview : SubViewportContainer
{
    public CarLook Look { get; set; } = CarLook.All[0];
    public string? AccessoryId { get; set; }
    public string ModelId { get; set; } = "classic";

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

        bool rear = AccessoryId is "spare" or "exhaust" or "spoiler" or "antenna" or "flag";
        var camera = new Camera3D {
            Projection = Camera3D.ProjectionType.Orthogonal, Size = 4.5f,
            Position = new Vector3(4, 3.4f, rear ? 5 : -5), Current = true
        };
        studio.AddChild(camera);
        camera.LookAt(new Vector3(0, 1.25f, 0));
    }
}
