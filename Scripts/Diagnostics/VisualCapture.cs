using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Quizzes;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

public partial class VisualCapture : Node
{
    private readonly FollowCamera camera;
    private readonly World world;
    private readonly Vehicle car;
    private readonly string view;

    public VisualCapture(FollowCamera camera, World world, Vehicle car, string view)
    {
        this.camera = camera;
        this.world = world;
        this.car = car;
        this.view = view;
    }

    public override async void _Ready()
    {
        if (view is "nature" or "riverbank")
        {
            camera.SetProcess(false);
            car.Active = false;
            var focus = view == "nature" ? world.Plants.MeadowPreview : world.Plants.BankPreview;
            if (focus == Vector3.Zero)
                focus = world.Plants.PreviewSpot;
            if (view == "riverbank")
            {
                int side = focus.X < River.CenterX(focus.Z) ? -1 : 1;
                camera.Position = focus + new Vector3(side * 7, 3.8f, 9);
                camera.LookAt(focus + new Vector3(-side * 2, .5f, 0));
            }
            else
            {
                camera.Position = focus + new Vector3(6, 3.3f, 8);
                camera.LookAt(focus + Vector3.Up * .6f);
            }
        }
        else if (view == "constellation")
        {
            // The view from the tower at night: the line of sight is lifted and a picture of stars hangs ahead.
            camera.SetProcess(false);
            car.Active = false;
            var eye = car.Position + Vector3.Up * 6;
            camera.Position = eye;
            camera.LookAt(eye + new Vector3(0, .5f, 1) * 60);
            world.Atmosphere.ShowConstellation(Constellation.All[0], 0, 8);
        }
        else if (view == "square")
        {
            camera.SetProcess(false);
            car.Active = false;
            var square = world.Village.Square;
            var focus = square.GlobalPosition + Vector3.Up;
            camera.Position = focus + square.GlobalBasis * new Vector3(17, 17, 27);
            camera.LookAt(focus);
        }
        else if (view is "events" or "slalom")
        {
            var game = (Game)GetParent();
            int course = view == "slalom" ? 1 : 0;
            game.Events.Begin(course);
            var frame = game.Events.Sample(course, game.Events.StartDistances[course]);
            var before = game.Events.Sample(course, game.Events.StartDistances[course] - 8).Center;
            before.Y = world.Routes.SurfaceHeight(before, world.Ground.Height(before.X, before.Z));
            if (River.OnBridge(before.X, before.Z))
                before.Y = Mathf.Max(before.Y, River.BridgeSurfaceHeight(before.X));
            car.SetPose(before + Vector3.Up * .2f, Mathf.Atan2(-frame.Forward.X, -frame.Forward.Z));
            car.SetPhysicsProcess(false);
            camera.Snap();
        }
        else if (view == "letter-pictures" || view == "letter-pictures-2")
        {
            var layer = new CanvasLayer { Layer = 100 };
            AddChild(layer);
            var background = new ColorRect { Color = new Color("fff5df") };
            layer.AddChild(background);
            background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var center = new CenterContainer();
            layer.AddChild(center);
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var grid = new GridContainer { Columns = 4 };
            center.AddChild(grid);
            grid.AddThemeConstantOverride("h_separation", 28);
            grid.AddThemeConstantOverride("v_separation", 14);
            foreach (var word in LetterChallengeGenerator.Words.Skip(view == "letter-pictures-2" ? 16 : 0).Take(16))
            {
                var card = new VBoxContainer();
                grid.AddChild(card);
                card.AddChild(new WordPicture { PictureId = word.Id, CustomMinimumSize = new Vector2(220, 160) });
                var label = new Label { Text = word.Word, HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeColorOverride("font_color", new Color("3c5960"));
                label.AddThemeFontSizeOverride("font_size", 22);
                card.AddChild(label);
            }
        }
        else if (
            (view.StartsWith("baker") || view.StartsWith("merchant") || view.StartsWith("confectioner"))
            && !view.EndsWith("delivery")
            && !view.EndsWith("reward")
        )
        {
            camera.SetProcess(false);
            car.Active = false;
            int trade =
                view.StartsWith("baker") ? 0
                : view.StartsWith("merchant") ? 1
                : 2;
            var shop = world.Village.Shops[trade];
            var focus = shop.GlobalPosition + Vector3.Up * 2;
            camera.Position = focus + shop.GlobalBasis * new Vector3(11, 9, 19);
            camera.LookAt(focus);
        }
        else if ((view.StartsWith("museum") || view.StartsWith("toys")) && !view.EndsWith("expedition"))
        {
            camera.SetProcess(false);
            car.Active = false;
            var building = view.StartsWith("toys") ? world.Village.ToyBuilding : world.Village.MuseumBuilding;
            var focus = building.GlobalPosition + building.GlobalBasis * new Vector3(5, 2, 0);
            camera.Position = focus + building.GlobalBasis * new Vector3(17, 17, 29);
            camera.LookAt(focus);
        }
        else if (view is "garden" or "garden-full" or "garden-question" or "garden-status")
        {
            camera.SetProcess(false);
            car.Active = false;
            var garden = world.Village.GardenBuilding;
            var focus = garden.GlobalPosition + garden.GlobalBasis * new Vector3(5, 1.5f, 0);
            camera.Position = focus + garden.GlobalBasis * new Vector3(17, 17, 29);
            camera.LookAt(focus);
        }
        else if (view is "farm" or "farm-full" or "farm-question" or "farm-status")
        {
            camera.SetProcess(false);
            car.Active = false;
            var farm = world.Village.FarmBuilding;
            var focus = farm.GlobalPosition + farm.GlobalBasis * new Vector3(5, 2, 0);
            camera.Position = focus + farm.GlobalBasis * new Vector3(18, 18, 30);
            camera.LookAt(focus);
        }
        else if (
            view
            is "village"
                or "village-upgraded"
                or "village-evening"
                or "village-night"
                or "village-question"
                or "village-fruit-offer"
                or "village-material-offer"
                or "village-material-missing"
        )
        {
            camera.SetProcess(false);
            car.Active = false;
            var house = world.Village.Houses[0];
            var focus = house.GlobalPosition + Vector3.Up * 3;
            camera.Position = focus + house.GlobalBasis * new Vector3(12, 8, 20);
            camera.LookAt(focus);
        }
        else if (view == "garage-building")
        {
            camera.SetProcess(false);
            car.Active = false;
            var building = world.Village.GarageBuilding;
            var focus = building.GlobalPosition + Vector3.Up * 2;
            camera.Position = focus + building.GlobalBasis * new Vector3(12, 8, 19);
            camera.LookAt(focus);
        }
        else if (view is "garage" or "garage-shop" or "garage-trails") { }
        else if (view == "coins" && world.GetNodeOrNull<WorldCoins>("WorldCoins") is { } coins)
        {
            camera.SetProcess(false);
            car.Active = false;
            var coin = coins.ActivePositions.MinBy(p => p.DistanceSquaredTo(car.Position));
            camera.Position = coin + new Vector3(4, 2.6f, 5);
            camera.LookAt(coin + Vector3.Up * 1.1f);
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
            if (view == "tracks-right")
                Input.ActionPress("right", .15f);
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
        else if (view is "trail-flowers" or "trail-hearts" or "trail-stars")
        {
            // The car drives with a bought trail and looks back at what its wheels left on the road.
            car.Appearance.ApplyTrail(view[6..]);
            Input.ActionPress("accelerate");
            await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("accelerate");
            Input.ActionPress("brake");
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            camera.SetProcess(false);
            var focus = car.Position + Vector3.Back * 4;
            camera.Position = focus + new Vector3(4, 5.5f, 9);
            camera.LookAt(focus);
        }
        else if (view == "ducklings" && world.Duck is { } duck)
        {
            // The duck of the jetty and its three ducklings waiting beside the fisherman.
            camera.SetProcess(false);
            car.Active = false;
            camera.Position = duck.Position + duck.Basis * new Vector3(0, 2.2f, 7);
            camera.LookAt(duck.Position + duck.Basis * new Vector3(-1, .6f, 0));
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
        else if (view is "tree-impact" or "tree-wind")
        {
            camera.SetProcess(false);
            var focus = world.Plants.PreviewSpot;
            car.SetPose(world.Ground.At(focus.X, focus.Z + 2.7f, .15f), 0);
            camera.Position = focus + new Vector3(7, 4, 9);
            camera.LookAt(focus + Vector3.Up * 2.2f);
            if (view == "tree-wind")
                ((Game)GetParent()).Weather.Begin(WeatherKind.Wind);
            else
            {
                await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
                var trunk = GetTree()
                    .GetNodesInGroup("obstacle_trunks")
                    .Cast<StaticBody3D>()
                    .Where(body => body.GetParent() == world.Plants)
                    .OrderBy(body => body.Position.DistanceSquaredTo(focus))
                    .First();
                world.Plants.HitTree(trunk, Vector3.Forward * 12, Vector3.Back);
            }
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
            var junction = WorldLayout.Plan.VillageJunction;
            var focus = world.Ground.At(junction.X, junction.Z);
            camera.Position = focus + new Vector3(48, 58, 62);
            camera.LookAt(focus);
        }
        else if (view == "world")
        {
            camera.SetProcess(false);
            camera.Position = new Vector3(-460, 320, 480);
            camera.LookAt(new Vector3(0, 0, 0));
        }
        else if (view is "mayor-town" or "mayor-house")
        {
            // The whole village from above, or the front yard of the first house, with every decoration bought.
            camera.SetProcess(false);
            var house = world.Village.Houses[0];
            var focus = view == "mayor-town" ? world.Village.Center : house.GlobalPosition;
            camera.GlobalPosition =
                view == "mayor-town" ? focus + new Vector3(0, 95, 80) : house.ToGlobal(new Vector3(0, 12, 21));
            camera.LookAt(view == "mayor-town" ? focus : house.ToGlobal(new Vector3(0, 1, 2)));
        }
        else if (view == "mayor-close")
        {
            // The mayor from the front and a little above, with her rug and the clock pillar behind her.
            camera.SetProcess(false);
            var mayor = world.Village.Mayor;
            camera.GlobalPosition = mayor.ToGlobal(new Vector3(.9f, 1.9f, 3.4f));
            camera.LookAt(mayor.ToGlobal(new Vector3(0, 1.55f, 0)));
        }
        else if (view == "treasure-captain")
        {
            // The captain from the front and a little above, with her parrot, rug and shovel.
            camera.SetProcess(false);
            var captain = world.Village.Treasurer;
            camera.GlobalPosition = captain.ToGlobal(new Vector3(-.8f, 2.2f, 4.4f));
            camera.LookAt(captain.ToGlobal(new Vector3(.6f, 1.5f, 0)));
        }
        else if (view is "treasure-hunt" or "treasure-chest")
        {
            // The buried treasure from the car's side: the red cross, or the open chest with its sparkles.
            camera.SetProcess(false);
            var site = world.Treasure.GlobalPosition;
            camera.GlobalPosition = site + new Vector3(3.2f, 2.4f, 4.4f);
            camera.LookAt(site + Vector3.Up * .6f);
        }
        else if (view == "glass-close")
        {
            // The lamp maker from the front and a little above, with his table of lamps beside him.
            camera.SetProcess(false);
            var maker = world.Village.Glassmaker;
            camera.GlobalPosition = maker.ToGlobal(new Vector3(-1.2f, 2.4f, 4.6f));
            camera.LookAt(maker.ToGlobal(new Vector3(.9f, 1.1f, -.9f)));
        }
        else if (view is "glass" or "glass-night")
        {
            // The whole square from the road side, with the lamps that stand on it.
            camera.SetProcess(false);
            var square = world.Village.Square;
            camera.GlobalPosition = square.ToGlobal(new Vector3(9, 11, 19));
            camera.LookAt(square.ToGlobal(new Vector3(0, 1.2f, 0)));
        }
        else if (view == "mayor")
        {
            // The whole square from the road side, above the arch.
            camera.SetProcess(false);
            var square = world.Village.Square;
            camera.GlobalPosition = square.ToGlobal(new Vector3(-9, 11, 19));
            camera.LookAt(square.ToGlobal(new Vector3(0, 1.2f, 0)));
        }
        else if (view == "decorated" || view == "car" || view == "car-rear" || Game.ParseModelView(view) != null)
        {
            camera.SetProcess(false);
            var focus = car.Position + Vector3.Up * 1.2f;
            bool rear = view == "car-rear" || Game.ParseModelView(view) is { Rear: true };
            camera.Position = focus + (rear ? new Vector3(-4, 2.5f, 5) : new Vector3(4, 2.5f, -5));
            camera.LookAt(focus);
        }
        else if (
            view
            is "collection"
                or "village-fruit"
                or "farm-expedition"
                or "garden-expedition"
                or "museum-expedition"
                or "toys-expedition"
        )
        {
            camera.SetProcess(false);
            var point = ((Game)GetParent()).Delivery!.PickupPositions[0];
            car.SetPose(world.Ground.At(point.X, point.Z + 7, .15f), 0);
            camera.Position = point + new Vector3(7, 6, 14);
            camera.LookAt(point + Vector3.Up);
        }
        else if (view.StartsWith("ford-") && world.Ford is { } ford)
        {
            camera.SetProcess(false);
            var focus = new Vector3(ford.Site.CenterX, ford.Site.DeckHeight + 1, ford.Site.Z);
            camera.Position = focus + new Vector3(-ford.Site.Side * 9, 11, 20);
            camera.LookAt(focus);
        }
        else if (view == "bridge-roads" && world.Ford is { } bridge)
        {
            // Both approach roads meeting the finished beaver's bridge, seen from above.
            camera.SetProcess(false);
            var site = bridge.Site;
            var focus = new Vector3(site.CenterX, site.DeckHeight, site.Z);
            camera.Position = focus + new Vector3(0, 55, 28);
            camera.LookAt(focus);
        }
        else if (view is "quarry" or "second-quarry" && world.HaulSources.Length > 0)
        {
            // The field path from the road to the quarry's loading yard.
            camera.SetProcess(false);
            var source =
                view == "quarry" ? world.HaulSources[(int)HaulLoad.Stones] : world.SourceSites(HaulLoad.Stones).Last();
            var along = source.Position - source.Road;
            along.Y = 0;
            along = along.Normalized();
            var focus = source.Road.Lerp(source.Position, .55f) + Vector3.Up;
            camera.Position = source.Road - along * 10 + along.Cross(Vector3.Up) * 8 + Vector3.Up * 12;
            camera.LookAt(focus);
        }
        else if (Game.ParseBuildView(view) is var (project, _) && project < world.Projects.Length)
        {
            camera.SetProcess(false);
            var build = world.Projects[project];
            var ring = build.DropFor(0);
            var inward =
                project == BuildPlans.Jetty
                    ? new Vector3(world.Ford!.Site.Side, 0, 0)
                    : (((SitedProject)build).Site.Inward);
            var right = Vector3.Up.Cross(inward);
            float back = project == BuildPlans.Tower ? 26 : 18;
            camera.Position = ring - inward * back + right * 14 + Vector3.Up * (project == BuildPlans.Tower ? 16 : 9);
            camera.LookAt(build.Focus);
        }
        else if (Game.ParseRailwayView(view) is { } && world.Railway is { } railway)
        {
            // The whole loop from the road side and from above; the close view looks along the platform.
            camera.SetProcess(false);
            var inward = railway.Site.Inward;
            var right = Vector3.Up.Cross(inward);
            if (view is "railway-close" or "railway-pet")
            {
                camera.Position = railway.ToGlobal(new Vector3(1.5f, 2.4f, -8.5f));
                camera.LookAt(railway.ToGlobal(new Vector3(6f, 1.1f, -1.5f)));
            }
            else
            {
                camera.Position = railway.Drop - inward * 15 + right * 17 + Vector3.Up * 18;
                camera.LookAt(railway.Drop + inward * 8 + Vector3.Up * 0);
            }
        }
        else if (Game.ParseZooView(view) is { } && world.Zoo is { } zoo)
        {
            // The whole zoo from the road side and from above; the close view looks along the path from the gate.
            camera.SetProcess(false);
            var inward = zoo.Site.Inward;
            var right = Vector3.Up.Cross(inward);
            if (view is "zoo-close" or "zoo-pet")
            {
                camera.Position = zoo.ToGlobal(new Vector3(0f, 3.4f, -6.5f));
                camera.LookAt(zoo.ToGlobal(new Vector3(0f, 1.8f, 14.0f)));
            }
            else
            {
                camera.Position = zoo.Drop - inward * 12 + right * 22 + Vector3.Up * 22;
                camera.LookAt(zoo.Drop + inward * 15 + Vector3.Up * 0);
            }
        }
        else if (Game.ParsePlaygroundView(view) is { } && world.Playground is { } playground)
        {
            // The ring, the arch and all the toys from the road side, from above.
            camera.SetProcess(false);
            var inward = playground.Site.Inward;
            var right = Vector3.Up.Cross(inward);
            camera.Position = playground.Drop - inward * 9 + right * 11 + Vector3.Up * 12;
            camera.LookAt(playground.Drop + inward * 11 + Vector3.Up * 1);
        }
        else if (Game.ParsePlotView(view) is var (plotIndex, _) && plotIndex < world.Village.BuildPlots.Count)
        {
            // The lot, its ring and the street, seen from the road side and from above.
            camera.SetProcess(false);
            var plot = world.Village.BuildPlots[plotIndex];
            var toStreet = plot.Road - plot.Position;
            toStreet.Y = 0;
            toStreet = toStreet.Normalized();
            var right = Vector3.Up.Cross(toStreet);
            camera.Position = plot.Position + toStreet * 11 + right * 7 + Vector3.Up * 6;
            camera.LookAt(plot.Position + Vector3.Up * 2 + toStreet * 1.5f);
        }
        else if (
            view is "sawmill" or "brickworks" or "gravel-works" or "glassworks" or "works-full"
            && world.Industry.Length > 0
        )
        {
            // The ring, the buildings and the stock yard seen from the approach side, from above.
            camera.SetProcess(false);
            var works =
                view == "glassworks" && world.Glassworks is { } glass
                    ? glass
                    : world.Industry[
                        view switch
                        {
                            "brickworks" => 1,
                            "gravel-works" => 2,
                            _ => 0,
                        }
                    ];
            if (view == "glassworks")
                works.SetStock(8);
            var site = works.Site;
            var right = Vector3.Up.Cross(site.Inward);
            camera.Position = site.Position - site.Inward * 15 + right * 13 + Vector3.Up * 11;
            camera.LookAt(site.Position + site.Inward * 8 + right * 2 + Vector3.Up * 1.5f);
        }
        else if (view == "pekarna" && world.Bakery is { } bakery)
        {
            camera.SetProcess(false);
            bakery.SetStock(8);
            var site = bakery.Site;
            var right = Vector3.Up.Cross(site.Inward);
            camera.Position = site.Position - site.Inward * 15 + right * 13 + Vector3.Up * 11;
            camera.LookAt(site.Position + site.Inward * 8 + right * 2 + Vector3.Up * 1.5f);
        }
        else if (view is "wheat-farm" or "wheat-farm-far" && world.Farm is { } farm)
        {
            // The loading ring, the sacks, the barn and the four fields, from the road side and from above.
            camera.SetProcess(false);
            car.Active = false;
            var site = farm.Site;
            var right = Vector3.Up.Cross(site.Inward);
            bool far = view == "wheat-farm-far";
            camera.Position =
                site.Position - site.Inward * (far ? 30 : 17) + right * (far ? 24 : 13) + Vector3.Up * (far ? 26 : 12);
            camera.LookAt(site.Position + site.Inward * 12 + Vector3.Up);
        }
        else if (view is "hidden-ladybug" or "hidden-hedgehog" or "hidden-frog")
        {
            // A close look at one friend who hides, from the road side.
            camera.SetProcess(false);
            car.Active = false;
            var kind = view switch
            {
                "hidden-ladybug" => HiddenKind.Ladybug,
                "hidden-hedgehog" => HiddenKind.Hedgehog,
                _ => HiddenKind.Frog,
            };
            var friend = world.Hidden.Friends.First(f => f.Kind == kind);
            camera.Position =
                friend.Position + friend.Transform.Basis.Z * 2.6f + friend.Transform.Basis.X * 1.4f + Vector3.Up * 1.4f;
            camera.LookAt(friend.Position + Vector3.Up * .6f);
        }
        else if (view == "hidden-near")
        {
            // The car drives towards a friend: the question mark grows over it and the notebook is in the corner.
            var friend = world.Hidden.Friends[0];
            var road = world.Routes.Samples.OrderBy(s => (s - friend.Position).Length()).First();
            var toFriend = friend.Position - road;
            toFriend.Y = 0;
            car.SetPose(
                world.Ground.At(road.X - toFriend.X * 1.2f, road.Z - toFriend.Z * 1.2f, .15f),
                Mathf.Atan2(-toFriend.X, -toFriend.Z)
            );
            camera.Snap();
        }
        else if (view == "hidden-pets")
        {
            // The three pets that wait around the witch until their friends are all found.
            camera.SetProcess(false);
            car.Active = false;
            var witch = world.Witch;
            camera.Position = witch.Position + witch.Basis * new Vector3(0, 2.6f, 9);
            camera.LookAt(witch.Position + witch.Basis * new Vector3(0, 1.1f, 1.2f));
        }
        else if (view == "log-yard" && world.LogYard is { } yard)
        {
            camera.SetProcess(false);
            var along = yard.Position - yard.Road;
            along.Y = 0;
            along = along.Normalized();
            camera.Position = yard.Position - along * 12 + along.Cross(Vector3.Up) * 7 + Vector3.Up * 8;
            camera.LookAt(yard.Position + along * 4 + Vector3.Up);
        }
        else if (view == "works-panel") { }
        else if (view == "trailer")
        {
            camera.SetProcess(false);
            var focus = car.Position + car.Transform.Basis.Z * 2.4f + Vector3.Up * 1.1f;
            camera.Position = focus + car.Transform.Basis.X * 6.5f + car.Transform.Basis.Z * 3 + Vector3.Up * 2.6f;
            camera.LookAt(focus);
        }
        else if (view is "beaver" or "load-question") { }
        else if (view == "delivery" || view.EndsWith("-delivery"))
        {
            camera.SetProcess(false);
            var direction = (((Game)GetParent()).Delivery!.Waypoint - car.Position);
            direction.Y = 0;
            direction = direction.Normalized();
            car.SetPose(car.Position, Mathf.Atan2(-direction.X, -direction.Z));
            camera.Position = car.Position - direction * 12 + Vector3.Up * 7;
            camera.LookAt(car.Position + direction * 5);
        }
        else if (
            view is "house-celebration" or "challenge-2" or "syllables" or "reading"
            || view.StartsWith("postcard-")
        ) { }
        else if (
            !view.EndsWith("-reward")
            && !view.StartsWith("collection-")
            && !view.StartsWith("delivery-")
            && view != "gameplay"
            && view is not ("night-drive" or "evening-drive" or "weather-choice" or "constellation")
            && view != "title"
            && view != "challenge"
            && view != "letters"
            && view != "reward"
            && view != "accessories"
        )
        {
            camera.SetProcess(false);
            Vector3 focus =
                (view == "river" || view == "pond")
                    ? new Vector3(River.CenterX(River.BridgeZ), River.BridgeHeight, River.BridgeZ)
                : view == "village" ? world.Village.Center + Vector3.Up * 2
                : world
                    .Animals[
                        view == "fox" ? 1
                        : view == "bear" ? 2
                        : 0
                    ]
                    .Position
                    + Vector3.Up * 1.5f;
            camera.Position =
                focus
                + (
                    view == "village" ? new Vector3(30, 27, 39)
                    : (view == "river" || view == "pond") ? new Vector3(34, 25, 33)
                    : new Vector3(4, 2, 8)
                );
            camera.LookAt(focus);
        }
        await ToSignal(
            GetTree()
                .CreateTimer(
                    view == "grass" ? .15
                    : view == "tree-impact" ? .5
                    : view == "delivery-reward" || view == "house-celebration" ? 1.2
                    : 3
                ),
            SceneTreeTimer.SignalName.Timeout
        );
        if (DisplayServer.GetName() == "headless")
        {
            GetTree().Quit();
            return;
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        var result = GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/{view}.png");
        GD.Print("CAPTURE: " + result);
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
