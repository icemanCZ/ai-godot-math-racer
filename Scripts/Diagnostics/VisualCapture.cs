using Godot;
using System.Linq;

namespace MathRacer;

public partial class VisualCapture : Node
{
    private readonly FollowCamera camera;
    private readonly World world;
    private readonly Vehicle car;
    private readonly string view;
    public VisualCapture(FollowCamera camera, World world, Vehicle car, string view) { this.camera = camera; this.world = world; this.car = car; this.view = view; }
    public override async void _Ready()
    {
        if (view == "letter-pictures" || view == "letter-pictures-2")
        {
            var layer = new CanvasLayer { Layer = 100 }; AddChild(layer);
            var background = new ColorRect { Color = new Color("fff5df") }; layer.AddChild(background);
            background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var center = new CenterContainer(); layer.AddChild(center); center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var grid = new GridContainer { Columns = 4 }; center.AddChild(grid);
            grid.AddThemeConstantOverride("h_separation", 28); grid.AddThemeConstantOverride("v_separation", 14);
            foreach (var word in LetterChallengeGenerator.Words.Skip(view == "letter-pictures-2" ? 16 : 0).Take(16))
            {
                var card = new VBoxContainer(); grid.AddChild(card);
                card.AddChild(new WordPicture { PictureId = word.Id, CustomMinimumSize = new Vector2(220, 160) });
                var label = new Label { Text = word.Word, HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeColorOverride("font_color", new Color("3c5960"));
                label.AddThemeFontSizeOverride("font_size", 22); card.AddChild(label);
            }
        }
        else if (view == "grass")
        {
            // A drive through the generated meadow exposes bending and the recovering wake.
            var start = world.Ground.At(15, 8, .15f);
            car.SetPose(start, 0);
            camera.SetProcess(false);
            camera.Position = start + new Vector3(7, 4, 8);
            camera.LookAt(start + Vector3.Up * .5f);
            await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            Input.ActionPress("accelerate");
            await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("accelerate");
            Input.ActionPress("brake");
            camera.Position = car.Position + new Vector3(7, 3.5f, 7);
            camera.LookAt(car.Position + Vector3.Back * 1.5f);
        }
        else if (view == "tracks" || view == "tracks-right")
        {
            Input.ActionPress("accelerate");
            if (view == "tracks-right") Input.ActionPress("right", .15f);
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            if (view == "tracks-right")
            {
                Input.ActionRelease("right");
                await ToSignal(GetTree().CreateTimer(8), SceneTreeTimer.SignalName.Timeout);
            }
            Input.ActionRelease("accelerate");
            Input.ActionRelease("right");
            Input.ActionPress("brake");
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            camera.SetProcess(false);
            var focus = car.Position + Vector3.Back * 3;
            camera.Position = focus + new Vector3(7, 11, 10);
            camera.LookAt(focus);
        }
        else if (view == "wildlife")
        {
            camera.SetProcess(false);
            car.Active = false;
            var focus = world.Wildlife.PreviewSpot;
            // A close view of the real generated deer and its surroundings.
            camera.Position = focus + new Vector3(5, 3, 7);
            camera.LookAt(focus + Vector3.Up);
        }
        else if (view == "tree-impact")
        {
            camera.SetProcess(false);
            var focus = world.Plants.PreviewSpot;
            car.SetPose(world.Ground.At(focus.X, focus.Z + 2.7f, .15f), 0);
            camera.Position = focus + new Vector3(7, 4, 9);
            camera.LookAt(focus + Vector3.Up * 2.2f);
            await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            var trunk = GetTree().GetNodesInGroup("obstacle_trunks").Cast<StaticBody3D>()
                .Where(body => body.GetParent() == world.Plants).OrderBy(body => body.Position.DistanceSquaredTo(focus)).First();
            world.Plants.HitTree(trunk, Vector3.Forward * 12, Vector3.Back);
        }
        else if (view == "vegetation" || view == "vegetation-detail")
        {
            camera.SetProcess(false);
            var focus = world.Plants.PreviewSpot + Vector3.Up * 2;
            camera.Position = focus + (view == "vegetation-detail" ? new Vector3(6, 2.5f, 8) : new Vector3(16, 10, 19));
            camera.LookAt(focus);
        }
        else if (view == "junction")
        {
            camera.SetProcess(false);
            var junction = WorldLayout.Plan.Routes[1][0];
            var focus = world.Ground.At(junction.X, junction.Z);
            camera.Position = focus + new Vector3(16, 20, 22);
            camera.LookAt(focus);
        }
        else if (view == "world")
        {
            camera.SetProcess(false);
            camera.Position = new Vector3(-460, 320, 480);
            camera.LookAt(new Vector3(0, 0, 0));
        }
        else if (view == "decorated" || view == "car" || view == "car-rear" || view == "buggy" || view == "pickup" || view == "wagon")
        {
            camera.SetProcess(false);
            var focus = car.Position + Vector3.Up * 1.2f;
            camera.Position = focus + (view == "car-rear" ? new Vector3(-4, 2.5f, 5) : new Vector3(4, 2.5f, -5));
            camera.LookAt(focus);
        }
        else if (view != "gameplay" && view != "title" && view != "challenge" && view != "letters" && view != "reward" && view != "accessories")
        {
            camera.SetProcess(false);
            Vector3 focus = (view == "river" || view == "pond") ? new Vector3(River.CenterX(River.BridgeZ), River.BridgeHeight, River.BridgeZ) : view == "village" ? world.Village.Center + Vector3.Up * 2 : world.Animals[view == "fox" ? 1 : view == "bear" ? 2 : 0].Position + Vector3.Up * 1.5f;
            camera.Position = focus + (view == "village" ? new Vector3(30, 27, 39) : (view == "river" || view == "pond") ? new Vector3(34, 25, 33) : new Vector3(4, 2, 8));
            camera.LookAt(focus);
        }
        await ToSignal(GetTree().CreateTimer(view == "grass" ? .15 : view == "tree-impact" ? .5 : 3), SceneTreeTimer.SignalName.Timeout);
        if (DisplayServer.GetName() == "headless") { GetTree().Quit(); return; }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        var result = GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/{view}.png");
        GD.Print("CAPTURE: " + result); GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}

