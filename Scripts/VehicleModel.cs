using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Rounded coachwork and fitted details, all in the vehicle's local space.</summary>
public static class VehicleModel
{
    private static readonly SphereMesh SmoothSphere = new() { Radius = 1, Height = 2, RadialSegments = 32, Rings = 16 };
    private static readonly Dictionary<string, StandardMaterial3D> Paints = new();
    public static StandardMaterial3D Paint(string color)
    {
        if (!Paints.TryGetValue(color, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = new Color(color).SrgbToLinear(), Roughness = .3f, Metallic = .12f };
            Paints[color] = material;
        }
        return material;
    }

    public static void BuildDisplay(Node3D model, VehicleAppearance appearance, List<Node3D> wheels, string? onlyModel = null)
    {
        var classic = new Node3D { Name = "Coachwork_classic" }; model.AddChild(classic);
        Build(classic, appearance, wheels);
        foreach (var wheel in wheels) wheel.Reparent(model, false);
        appearance.RegisterModel("classic", classic);
        foreach (var body in CarBody.All)
        {
            if (body.Id == "classic" || (onlyModel != null && body.Id != onlyModel)) continue;
            var variant = new Node3D { Name = "Coachwork_" + body.Id }; model.AddChild(variant);
            BuildVariant(variant, appearance, body.Id);
            appearance.RegisterModel(body.Id, variant);
        }
        appearance.SetupStripes(model);
        appearance.SetupAccessories(model);
        if (onlyModel != null) appearance.ApplyModel(onlyModel);
    }

    public static void Build(Node3D root, VehicleAppearance appearance, List<Node3D> wheels)
    {
        MeshInstance3D Round(Node3D parent, Vector3 position, Vector3 scale, string color)
            => Art.Mesh(parent, SmoothSphere, position, scale, color);
        void Body(MeshInstance3D mesh) => appearance.RegisterBody(mesh);
        void Trim(MeshInstance3D mesh) => appearance.RegisterTrim(mesh);

        // Horizontal rounded rings give the body continuous shoulders and a flat bonnet.
        Body(Loft(root, new[] {
            new Ring(.42f, .72f, 1.28f, 0, .25f),
            new Ring(.53f, .92f, 1.50f, 0, .30f),
            new Ring(.83f, .98f, 1.56f, 0, .34f),
            new Ring(1.07f, .94f, 1.48f, 0, .34f),
            new Ring(1.20f, .82f, 1.30f, .02f, .30f)
        }, "f54257"));
        var cabin = Loft(root, new[] {
            new Ring(1.13f, .88f, .98f, .18f, .18f),
            new Ring(1.32f, .89f, .92f, .18f, .18f),
            new Ring(1.91f, .65f, .52f, .18f, .16f),
            new Ring(2.01f, .54f, .44f, .18f, .17f)
        }, "ff8992");
        appearance.RegisterRoof(cabin);
        appearance.RegisterRoof(Round(root, new(0, 1.994f, .18f), new(.56f, .065f, .45f), "ff8992"));

        // Windows follow the cabin planes; dark gaskets keep the pillars legible.
        Vector3 Front(float x, float y) => new(x, y, -.74f + (y - 1.32f) * (.40f / .59f) - .012f);
        Vector3 Rear(float x, float y) => new(x, y, 1.10f - (y - 1.32f) * (.40f / .59f) + .012f);
        Window(root, new[] { Front(-.67f, 1.39f), Front(.67f, 1.39f), Front(.57f, 1.83f), Front(-.57f, 1.83f) }, "72becf");
        Window(root, new[] { Rear(.67f, 1.39f), Rear(-.67f, 1.39f), Rear(-.57f, 1.83f), Rear(.57f, 1.83f) }, "609daf");
        Panel(root, new[] { Front(-.54f, 1.46f) + Vector3.Forward * .012f, Front(-.45f, 1.46f) + Vector3.Forward * .012f,
            Front(-.15f, 1.77f) + Vector3.Forward * .012f, Front(-.29f, 1.77f) + Vector3.Forward * .012f }, "b9e0e8");
        Panel(root, new[] { Rear(.48f, 1.46f) + Vector3.Back * .012f, Rear(.42f, 1.46f) + Vector3.Back * .012f,
            Rear(.12f, 1.76f) + Vector3.Back * .012f, Rear(.23f, 1.76f) + Vector3.Back * .012f }, "acd3de");
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 Side(float z, float y) => new(side * (.89f - (y - 1.32f) * (.24f / .59f) + .012f), y, z);
            Window(root, new[] { Side(-.51f, 1.39f), Side(.12f, 1.39f), Side(.12f, 1.83f), Side(-.27f, 1.83f) }, "82c8d7");
            Window(root, new[] { Side(.21f, 1.39f), Side(.87f, 1.39f), Side(.63f, 1.83f), Side(.21f, 1.83f) }, "71b2c5");
            // Glints are small inset strips on the same glass plane.
            Panel(root, new[] { Side(-.43f, 1.44f) + Vector3.Right * side * .007f, Side(-.37f, 1.44f) + Vector3.Right * side * .007f,
                Side(-.18f, 1.77f) + Vector3.Right * side * .007f, Side(-.25f, 1.77f) + Vector3.Right * side * .007f }, "c8edf0");
            Trim(Round(root, new(side * .91f, 1.23f, .49f), new(.035f, .035f, .15f), "fff0ce"));
            Trim(Round(root, new(side * .953f, .64f, .02f), new(.04f, .045f, .48f), "fff0ce"));
            // A narrow door shut line and a separate lower sill.
            Art.Block(root, new(side * .965f, .91f, .25f), new(.012f, .35f, .018f), "a63249");
            Round(root, new(side * 1.01f, 1.36f, -.49f), new(.13f, .035f, .045f), "344555");
            Body(Round(root, new(side * 1.10f, 1.41f, -.52f), new(.14f, .10f, .17f), "f54257"));
            Round(root, new(side * 1.105f, 1.414f, -.397f), new(.105f, .066f, .025f), "b6dae0");

            foreach (float z in new[] { -.93f, .96f })
            {
                // An open arch, rather than a solid oval over the tyre.
                Arch(root, new(side * .97f, .51f, z), side, appearance);
                var wheel = new Node3D { Position = new(side * .9875f, .51f, z) };
                root.AddChild(wheel); wheels.Add(wheel); appearance.RegisterWheel(wheel);
                var tyre = Art.Cylinder(wheel, Vector3.Zero, .49f, .49f, .30f, "263440");
                ((CylinderMesh)tyre.Mesh).RadialSegments = 40;
                tyre.Rotation = new(0, 0, Mathf.Pi / 2);
                var rim = Art.Cylinder(wheel, new(side * .161f, 0, 0), .32f, .32f, .035f, "fff0ce");
                ((CylinderMesh)rim.Mesh).RadialSegments = 32;
                rim.Rotation = new(0, 0, Mathf.Pi / 2); Trim(rim);
                Round(wheel, new(side * .188f, 0, 0), new(.018f, .248f, .248f), "344555");
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.Tau / 8;
                    var spoke = Art.Block(wheel, new(side * .209f, Mathf.Cos(angle) * .153f, Mathf.Sin(angle) * .153f), new(.025f, .22f, .055f), "fff0ce");
                    spoke.Rotation = new(angle, 0, 0); Trim(spoke);
                }
                Trim(Round(wheel, new(side * .224f, 0, 0), new(.035f, .108f, .108f), "fff0ce"));
                Body(Round(wheel, new(side * .255f, 0, 0), new(.018f, .055f, .055f), "f54257"));
                // Raised shoulder rings and subtle tread blocks also show wheel rotation.
                var ring = Art.Mesh(wheel, new TorusMesh { InnerRadius = .39f, OuterRadius = .485f, Rings = 32, RingSegments = 8 }, new(side * .12f, 0, 0), Vector3.One, "3b4b57");
                ring.Rotation = new(0, 0, Mathf.Pi / 2);
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.Tau / 24;
                    var tread = Art.Block(wheel, new(0, Mathf.Cos(angle) * .483f, Mathf.Sin(angle) * .483f), new(.27f, .018f, .045f), "31414c");
                    tread.Rotation = new(angle, 0, 0);
                }
            }
            // Chrome bezels, cream lenses, and tiny amber indicators.
            Trim(Round(root, new(side * .65f, .98f, -1.405f), new(.245f, .235f, .11f), "fff0ce"));
            var lamp = Round(root, new(side * .65f, .985f, -1.485f), new(.183f, .177f, .06f), "fff3c4");
            var glow = (StandardMaterial3D)Art.Mat("fff3c4").Duplicate();
            glow.EmissionEnabled = true; glow.Emission = new Color("ffe6a1").SrgbToLinear(); glow.EmissionEnergyMultiplier = .35f;
            lamp.MaterialOverride = glow;
            Round(root, new(side * .71f, .73f, -1.565f), new(.09f, .045f, .027f), "ffb54d");
            Trim(Round(root, new(side * .68f, .97f, 1.53f), new(.14f, .18f, .065f), "fff0ce"));
            Round(root, new(side * .68f, 1.01f, 1.59f), new(.103f, .105f, .031f), "ed394e");
            Round(root, new(side * .68f, .88f, 1.59f), new(.097f, .04f, .029f), "ffbb60");
        }
        Round(root, new(0, .79f, -1.555f), new(.36f, .105f, .025f), "263440");
        for (int i = -2; i <= 2; i++)
            Trim(Round(root, new(i * .105f, .79f, -1.58f), new(.014f, .069f, .014f), "fff0ce"));
        Trim(Round(root, new(0, .58f, -1.48f), new(.84f, .075f, .15f), "fff0ce"));
        Trim(Round(root, new(0, .58f, 1.48f), new(.84f, .075f, .15f), "fff0ce"));
        Round(root, new(0, .79f, 1.553f), new(.27f, .092f, .02f), "344555");
        Art.Block(root, new(0, .79f, 1.575f), new(.40f, .105f, .015f), "fff0ce");
        // Bonnet badge, twin decorative bonnet lines and rear hatch handle.
        Trim(Round(root, new(0, 1.17f, -1.19f), new(.07f, .026f, .09f), "fff0ce"));
        foreach (float x in new[] { -.37f, .37f })
            Round(root, new(x, 1.198f, -1.00f), new(.012f, .006f, .18f), "d23950");
        Trim(Round(root, new(0, 1.19f, 1.18f), new(.15f, .028f, .035f), "fff0ce"));
        appearance.Apply(CarLook.All[0]);
    }

    public static void BuildVariant(Node3D root, VehicleAppearance appearance, string id)
    {
        void Body(MeshInstance3D mesh) => appearance.RegisterBody(mesh);
        void Roof(MeshInstance3D mesh) => appearance.RegisterRoof(mesh);
        void Trim(MeshInstance3D mesh) => appearance.RegisterTrim(mesh);
        bool buggy = id == "buggy", pickup = id == "pickup";
        Body(Loft(root, new[] {
            new Ring(.48f, .78f, 1.32f, 0, .16f),
            new Ring(.72f, .94f, 1.50f, 0, .20f),
            new Ring(1.08f, .89f, 1.45f, 0, .22f),
            new Ring(1.20f, .79f, 1.28f, 0, .20f)
        }, "f54257"));
        if (buggy)
        {
            // Open sides, two seats, a visible roll cage and a narrow sun canopy.
            Art.Block(root, new(0, 1.23f, .22f), new(1.35f, .08f, 1.55f), "344555");
            foreach (float side in new[] { -1f, 1f })
            {
                Art.Block(root, new(side * .35f, 1.35f, .35f), new(.48f, .20f, .55f), "dbab75");
                Art.Block(root, new(side * .35f, 1.57f, .59f), new(.48f, .53f, .12f), "dbab75");
                foreach (float z in new[] { -.32f, .77f })
                    Trim(Art.Cylinder(root, new(side * .67f, 1.62f, z), .045f, .045f, .82f, "fff0ce"));
            }
            Roof(Art.Block(root, new(0, 2.01f, .18f), new(1.44f, .09f, 1.23f), "ff8992"));
            Window(root, new[] { new Vector3(-.63f, 1.37f, -.34f), new(.63f, 1.37f, -.34f), new(.63f, 1.91f, -.34f), new(-.63f, 1.91f, -.34f) }, "82c8d7");
        }
        else
        {
            float rear = pickup ? .57f : 1.26f;
            Roof(Loft(root, new[] {
                new Ring(1.19f, .83f, (rear + .69f) / 2, (rear - .69f) / 2, .12f),
                new Ring(1.94f, .69f, (rear + .40f) / 2, (rear - .40f) / 2, .12f),
                new Ring(2.05f, .64f, (rear + .36f) / 2, (rear - .36f) / 2, .12f)
            }, "ff8992"));
            Window(root, new[] { new Vector3(-.69f, 1.36f, -.64f), new(.69f, 1.36f, -.64f), new(.59f, 1.91f, -.42f), new(-.59f, 1.91f, -.42f) }, "82c8d7");
            Window(root, new[] { new Vector3(.61f, 1.39f, rear + .012f), new(-.61f, 1.39f, rear + .012f), new(-.57f, 1.89f, rear + .012f), new(.57f, 1.89f, rear + .012f) }, "72becf");
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 Side(float z, float y) => new(side * (.83f - (y - 1.19f) * .187f + .012f), y, z);
                Window(root, new[] { Side(-.48f, 1.36f), Side(.42f, 1.36f), Side(.42f, 1.89f), Side(-.28f, 1.89f) }, "82c8d7");
                if (!pickup) Window(root, new[] { Side(.53f, 1.36f), Side(1.10f, 1.36f), Side(1.10f, 1.89f), Side(.53f, 1.89f) }, "72becf");
            }
            if (pickup)
            {
                Art.Block(root, new(0, 1.22f, 1.01f), new(1.30f, .025f, .71f), "805b48");
                foreach (float side in new[] { -1f, 1f })
                    Body(Art.Block(root, new(side * .76f, 1.33f, 1.02f), new(.13f, .27f, .81f), "f54257"));
                Body(Art.Block(root, new(0, 1.33f, 1.40f), new(1.50f, .27f, .12f), "f54257"));
            }
        }
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float z in new[] { -.93f, .96f }) Arch(root, new(side * .97f, .51f, z), side, appearance);
            Trim(Art.Block(root, new(side * .91f, .62f, 0), new(.12f, .11f, 1.13f), "fff0ce"));
            Art.Block(root, new(side * .87f, 1.40f, -.48f), new(.22f, .045f, .06f), "344555");
            Body(Art.Block(root, new(side * .99f, 1.43f, -.48f), new(.20f, .15f, .22f), "f54257"));
            Trim(Art.Block(root, new(side * .66f, .96f, -1.46f), new(.32f, .27f, .07f), "fff0ce"));
            Art.Block(root, new(side * .66f, .96f, -1.51f), new(.24f, .19f, .04f), "fff3c4");
            Art.Block(root, new(side * .68f, .94f, 1.49f), new(.20f, .26f, .05f), "ed394e");
        }
        foreach (float z in new[] { -1.48f, 1.48f })
            Trim(Art.Block(root, new(0, .57f, z), new(1.73f, .13f, .18f), "fff0ce"));
        Art.Block(root, new(0, .83f, -1.51f), new(.65f, .17f, .035f), "344555");
    }

    private readonly record struct Ring(float Y, float Width, float Length, float Z, float Radius);
    private static MeshInstance3D Loft(Node3D parent, Ring[] rings, string color)
    {
        const int segments = 32;
        var vertices = new List<Vector3>();
        foreach (var ring in rings)
            for (int i = 0; i < segments; i++)
            {
                int corner = i / 8;
                float a = (corner * 90 + (i % 8) * 90f / 7) * Mathf.Pi / 180;
                float x = (corner == 0 || corner == 3 ? 1 : -1) * (ring.Width - ring.Radius);
                float z = (corner < 2 ? 1 : -1) * (ring.Length - ring.Radius);
                vertices.Add(new(x + Mathf.Cos(a) * ring.Radius, ring.Y, ring.Z + z + Mathf.Sin(a) * ring.Radius));
            }
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c) { surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c); }
        for (int j = 0; j < rings.Length - 1; j++)
            for (int i = 0; i < segments; i++)
            {
                int a = j * segments + i, b = j * segments + (i + 1) % segments;
                Triangle(vertices[a], vertices[b], vertices[a + segments]);
                Triangle(vertices[b], vertices[b + segments], vertices[a + segments]);
            }
        for (int i = 0; i < segments; i++)
        {
            Triangle(new(0, rings[0].Y, rings[0].Z), vertices[(i + 1) % segments], vertices[i]);
            int offset = (rings.Length - 1) * segments;
            Triangle(new(0, rings[^1].Y, rings[^1].Z), vertices[offset + i], vertices[offset + (i + 1) % segments]);
        }
        surface.Index(); surface.GenerateNormals();
        return Art.Mesh(parent, surface.Commit(), Vector3.Zero, Vector3.One, color);
    }

    private static void Window(Node3D root, Vector3[] corners, string color)
    {
        Vector3[] CutCorners(Vector3[] points)
        {
            var result = new Vector3[points.Length * 2];
            for (int i = 0; i < points.Length; i++)
            {
                result[i * 2] = points[i].Lerp(points[(i + points.Length - 1) % points.Length], .07f);
                result[i * 2 + 1] = points[i].Lerp(points[(i + 1) % points.Length], .07f);
            }
            return result;
        }
        Panel(root, CutCorners(corners), "293e50");
        Vector3 center = Vector3.Zero;
        foreach (var corner in corners) center += corner / corners.Length;
        var outward = new Vector3(center.X, .3f, center.Z - .18f).Normalized() * .005f;
        var inset = new Vector3[corners.Length];
        for (int i = 0; i < corners.Length; i++) inset[i] = center.Lerp(corners[i], .88f) + outward;
        var glass = Panel(root, CutCorners(inset), color);
        var material = (StandardMaterial3D)glass.MaterialOverride.Duplicate();
        material.Roughness = .18f; material.Metallic = .25f;
        material.EmissionEnabled = true; material.Emission = new Color(color).SrgbToLinear(); material.EmissionEnergyMultiplier = .22f;
        glass.MaterialOverride = material;
    }

    private static MeshInstance3D Panel(Node3D root, Vector3[] corners, string color)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        var normal = (corners[1] - corners[0]).Cross(corners[2] - corners[0]).Normalized();
        if (normal.Dot(new Vector3(corners[0].X, .1f, corners[0].Z - .18f)) < 0) normal = -normal;
        for (int i = 1; i < corners.Length - 1; i++)
            foreach (int k in new[] { 0, i, i + 1 }) { surface.SetNormal(normal); surface.AddVertex(corners[k]); }
        var instance = Art.Mesh(root, surface.Commit(), Vector3.Zero, Vector3.One, color);
        var material = (StandardMaterial3D)Art.Mat(color).Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        instance.MaterialOverride = material;
        return instance;
    }

    private static void Arch(Node3D root, Vector3 center, float side, VehicleAppearance appearance)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 24; i++)
        {
            float a = -.12f + i * (Mathf.Pi + .24f) / 24;
            float b = -.12f + (i + 1) * (Mathf.Pi + .24f) / 24;
            Vector3 P(float angle, float radius) => center + new Vector3(side * .04f, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
            var points = new[] { P(a, .52f), P(b, .52f), P(a, .62f), P(b, .52f), P(b, .62f), P(a, .62f) };
            if (side > 0) System.Array.Reverse(points);
            foreach (var p in points)
            { surface.SetNormal(Vector3.Right * side); surface.AddVertex(p); }
        }
        var arch = Art.Mesh(root, surface.Commit(), Vector3.Zero, Vector3.One, "f54257");
        // Both sides share a narrow painted lip, with no dark cap hiding the wheel.
        appearance.RegisterBody(arch);
    }
}
