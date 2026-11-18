using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Tasks;
using MathRacer.Vehicles;

namespace MathRacer.Garage;

/// <summary>Actual reward geometry in an isolated, softly lit studio.</summary>
public partial class CarLookPreview : SubViewportContainer
{
    public CarLook Look { get; set; } = CarLook.All[0];
    public string? AccessoryId { get; set; }
    public string ModelId { get; set; } = "classic";
    public string? ExpeditionCargo { get; set; }
    public int CargoAmount { get; set; } = 1;
    public IReadOnlyList<string> Accessories { get; set; } = System.Array.Empty<string>();

    /// <summary>Shows <see cref="AccessoryId"/> alone, without the car, framed to fill the picture.</summary>
    public bool AccessoryOnly { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(205, 160);
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
    }

    public override void _Process(double delta)
    {
        // Build only once the menu survives a frame, avoiding unused render scenes.
        if (!IsVisibleInTree())
            return;
        SetProcess(false);
        var viewport = new SubViewport
        {
            Size = new Vector2I(410, 320),
            OwnWorld3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(viewport);
        var studio = CreateStudio(viewport);
        if (AccessoryOnly && AccessoryId != null)
        {
            BuildAccessoryOnly(studio, AccessoryId);
            return;
        }
        var car = new Node3D();
        studio.AddChild(car);
        var appearance = new VehicleAppearance();
        VehicleModel.BuildDisplay(car, appearance, new List<Node3D>(), ModelId);
        appearance.Apply(Look);
        if (AccessoryId != null)
            appearance.ToggleAccessory(AccessoryId);
        foreach (var id in Accessories)
            appearance.ToggleAccessory(id);
        if (ExpeditionCargo != null)
        {
            var rack = new Node3D { Position = new(0, 2.4f, .3f) };
            car.AddChild(rack);
            foreach (float x in new[] { -.65f, .65f })
                Art.Block(rack, new(x, -.23f, 0), new(.1f, .4f, .8f), "795b43");
            Art.Block(rack, Vector3.Zero, new(1.8f, .28f, 1.05f), "bc8b61");
            for (int i = 0; i < CargoAmount; i++)
                Expedition.AddCargo(rack, new(-.6f + i * .6f, .5f, 0), ExpeditionCargo);
            // A destination pennant distinguishes the expedition from a cosmetic upgrade.
            Art.Cylinder(studio, new(-1.6f, 1.35f, .4f), .045f, .045f, 2.7f, "795b43");
            var flag = new SurfaceTool();
            flag.Begin(Mesh.PrimitiveType.Triangles);
            flag.AddVertex(new(-1.6f, 2.7f, .4f));
            flag.AddVertex(new(-.72f, 2.42f, .4f));
            flag.AddVertex(new(-1.6f, 2.14f, .4f));
            studio.AddChild(
                new MeshInstance3D
                {
                    Mesh = flag.Commit(),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color("27bd62").SrgbToLinear(),
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                        Roughness = .8f,
                    },
                }
            );
        }

        bool rear = AccessoryId != null && System.Array.IndexOf(RearAccessories, AccessoryId) >= 0;
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 4.5f,
            Position = new Vector3(4, 3.4f, rear ? 5 : -5),
            Current = true,
        };
        studio.AddChild(camera);
        camera.LookAt(new Vector3(0, ExpeditionCargo != null ? 1.5f : 1.25f, 0));
    }

    private static readonly string[] RearAccessories =
    {
        "spare",
        "exhaust",
        "spoiler",
        "antenna",
        "flag",
        "valley-map",
        "flour-sack",
    };

    private static void BuildAccessoryOnly(Node3D studio, string id)
    {
        Node3D piece;
        if (id == "wheels")
        {
            // The bigger wheels are a scale change on the car, so show one tyre on its own.
            piece = new Node3D();
            studio.AddChild(piece);
            Art.Cylinder(piece, new(0, .6f, 0), .66f, .66f, .4f, "263440").RotationDegrees = new(0, 0, 90);
            Art.Cylinder(piece, new(.22f, .6f, 0), .42f, .42f, .05f, "fff0ce").RotationDegrees = new(0, 0, 90);
            Art.Ball(piece, new(.25f, .6f, 0), new(.14f, .14f, .14f), "48bfc2");
        }
        else
        {
            piece = new CarAccessory(id, id).Create(studio);
            piece.Visible = true;
        }
        Aabb? bounds = null;
        foreach (var node in piece.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = (MeshInstance3D)node;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } known ? known.Merge(box) : box;
        }
        if (piece is MeshInstance3D own)
            bounds ??= own.GlobalTransform * own.GetAabb();
        var area = bounds ?? new Aabb(Vector3.Zero, Vector3.One);
        var center = area.GetCenter();
        bool rear = id == "wheels" || System.Array.IndexOf(RearAccessories, id) >= 0;
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = Mathf.Max(.45f, area.Size.Length() * .9f),
            Position = center + new Vector3(.8f, .6f, rear ? 1 : -1).Normalized() * 12,
            Current = true,
        };
        studio.AddChild(camera);
        camera.LookAt(center);
    }

    internal static Node3D CreateStudio(SubViewport viewport)
    {
        var studio = new Node3D();
        viewport.AddChild(studio);
        studio.AddChild(
            new WorldEnvironment
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.ClearColor,
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color("fff3df"),
                    AmbientLightEnergy = .85f,
                    ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                    TonemapMode = Godot.Environment.ToneMapper.Filmic,
                },
            }
        );
        studio.AddChild(
            new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-45, -35, 0),
                LightEnergy = 1.5f,
                LightColor = new Color("fff6e5"),
            }
        );
        studio.AddChild(
            new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-25, 140, 0),
                LightEnergy = .65f,
                LightColor = new Color("d6efff"),
            }
        );
        return studio;
    }
}
