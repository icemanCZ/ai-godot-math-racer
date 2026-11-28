using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Quizzes;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// Where <see cref="VisualCapture"/> puts the camera for a view of what lies beyond the old valley: the dragon, the
/// balloon, the tracks, the sheep, the owl's map and its landmarks, the shells, the castle, the ferry and the great
/// railway. A view of a panel or a ride keeps the game's own camera.
/// </summary>
internal static class OutlandFraming
{
    /// <summary>Frames <paramref name="view"/> and says whether it was one of these views.</summary>
    internal static bool Frame(FollowCamera camera, World world, Vehicle car, string view)
    {
        if (
            view is "dragon" or "dragon-nest" or "dragon-flight"
            && world.DragonPlace is { } dragonSite
            && world.DragonLair is { } dragonLair
            && world.DragonBody is { } dragonBody
        )
        {
            camera.SetProcess(false);
            if (view != "dragon")
            {
                // The finished nest by its burning fire, and the dragon well again.
                dragonLair.SetStage(DragonPlans.Trips);
                dragonLair.SetRiddle(DragonPlans.Riddles);
                dragonBody.Healthy = true;
            }
            if (view == "dragon-flight" && world.DragonFlight is { } flight)
            {
                // A third of the way round the valley, seen from beside and a little above him.
                dragonBody.Flying = true;
                dragonBody.Transform = flight.Pose(DragonFlight.Duration / 3);
                for (int i = 0; i < 30; i++)
                    dragonBody.Tick(.1f, car.GlobalPosition);
                var pose = dragonBody.GlobalTransform;
                camera.GlobalPosition = pose * new Vector3(-16, 7, 10);
                camera.LookAt(pose * new Vector3(0, 2.5f, 0));
            }
            else
            {
                // From in front of him and to the side, past the drop ring: his head, the nest and the cave behind him.
                for (int i = 0; i < 20; i++)
                    dragonBody.Tick(.1f, car.GlobalPosition);
                var eye = dragonSite.Local(dragonSite.FireSide * 13, DragonSite.DropAhead + 9);
                camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 7;
                camera.LookAt(dragonSite.Local(0, 4) + Vector3.Up * 3);
            }
        }
        else if (view is "balloon" or "balloon-night" && world.BalloonPlace is { } balloonSite)
        {
            // From the road, a little to the side and above: the whole balloon, the target, the hut and the windsock.
            camera.SetProcess(false);
            var eye = balloonSite.Local(-17, -6);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 6;
            camera.LookAt(world.Ground.At(balloonSite.Pad.X, balloonSite.Pad.Z) + Vector3.Up * 9);
        }
        else if (view is "balloon-far" or "balloon-far-night" && world.BalloonPlace is { } farSite)
        {
            // From above the middle of the village, looking out to the station: the balloon stands out on the skyline.
            camera.SetProcess(false);
            var from = world.Village.Center;
            camera.GlobalPosition = world.Ground.At(from.X, from.Z) + Vector3.Up * 14;
            camera.LookAt(world.Ground.At(farSite.Pad.X, farSite.Pad.Z) + Vector3.Up * 12);
            camera.Fov = 40;
        }
        else if (view is "balloon-panel" or "balloon-flight") { }
        else if (view is "tracking-den" or "tracking-dens" or "tracking-dens-night" && world.TrackingTrails.Count > 2)
        {
            // In front of a den, a little above, as the car arrives along the trail: the den and its animal.
            camera.SetProcess(false);
            var trail = world.TrackingTrails[view == "tracking-den" ? 2 : 1];
            var toward = new Vector3(Mathf.Sin(trail.Yaw), 0, Mathf.Cos(trail.Yaw));
            var eye = trail.Den + toward * 7 + Vector3.Up.Cross(toward) * 2;
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 2.6f;
            camera.LookAt(trail.Den + Vector3.Up * .6f);
        }
        else if (view is "tracking" or "tracking-panel" or "tracking-trail" or "tracking-night" or "tracking-lost") { }
        else if (view == "sheep-herd")
        {
            // In front of the car and to the side, looking back past it at the sheep that trot behind it.
            camera.SetProcess(false);
            var back = car.GlobalBasis.Z with { Y = 0 };
            back = back.Normalized();
            var side = Vector3.Up.Cross(back);
            camera.GlobalPosition = car.GlobalPosition - back * 9 + side * 6 + Vector3.Up * 4.5f;
            camera.LookAt(car.GlobalPosition + back * 9 + Vector3.Up * .5f);
        }
        else if (view == "sheep-fold" && world.BorderHomes[(int)Border.Mountains] != null)
        {
            // The fold from its front corner and above, clear of the shepherd at the gate: the sheep at home behind its
            // rails, the hut beside them.
            camera.SetProcess(false);
            camera.GlobalPosition = world.FoldPoint(11.5f, -.5f) + Vector3.Up * 5.5f;
            camera.LookAt(world.FoldMiddle + Vector3.Up * .6f);
        }
        else if (view == "explorer" && world.CartographerPlace is { } owlStand)
        {
            // From across the road and above: the owl on her perch, her big map on its easel, the table and the sign.
            camera.SetProcess(false);
            camera.GlobalPosition = owlStand.Local(1, -9) + Vector3.Up * 5;
            camera.LookAt(owlStand.Local(0, 8) + Vector3.Up * 2);
        }
        else if (view.StartsWith("landmark-") && int.TryParse(view["landmark-".Length..], out int wonder))
        {
            // From the road beside it, a little along and above: the whole wonder with the owl's flag.
            if (wonder < world.Landmarks.Count)
            {
                var landmark = world.Landmarks[wonder];
                var along = Vector3.Up.Cross(landmark.Facing).Normalized();
                camera.SetProcess(false);
                var eye = landmark.Road + landmark.Facing * 6 + along * 10;
                camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 5;
                camera.LookAt(landmark.Center + Vector3.Up * 3);
            }
        }
        else if (view == "shells-close" && world.ShellCove is { })
        {
            // Low over the wet sand behind the car: the shells and their twinkles before the sea.
            camera.SetProcess(false);
            camera.GlobalPosition = car.GlobalPosition + new Vector3(-5, 3, -6);
            camera.LookAt(car.GlobalPosition + new Vector3(0, 0, 14));
        }
        else if (view == "castle-far" && world.CastlePlace is { } farCastle)
        {
            // From above the middle of the village, looking out to the castle on its hill.
            camera.SetProcess(false);
            var from = world.Village.Center;
            camera.GlobalPosition = world.Ground.At(from.X, from.Z) + Vector3.Up * 14;
            camera.LookAt(world.Ground.At(farCastle.Center.X, farCastle.Center.Z) + Vector3.Up * 14);
            camera.Fov = 35;
        }
        else if (view == "beacon" && world.Beacons.FirstOrDefault(b => b != null) is { } beaconTower)
        {
            // From the place of its logs, a little aside and below the fire: the whole beacon burning.
            camera.SetProcess(false);
            var away = (beaconTower.Site.Drop - beaconTower.Site.Foot).Normalized();
            var eye = beaconTower.Site.Foot + away * 14 + Vector3.Up.Cross(away) * 5;
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 3;
            camera.LookAt(beaconTower.Position + Vector3.Up * 6);
        }
        else if (view == "castle-close" && world.CastlePlace is { } closeCastle)
        {
            // Over the front wall into the courtyard: the palace, the keep, the well and the table of the feast.
            camera.SetProcess(false);
            var eye = closeCastle.Local(-12, 8);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 16;
            camera.LookAt(world.Ground.At(closeCastle.Center.X, closeCastle.Center.Z) + Vector3.Up * 2);
        }
        else if (view.StartsWith("castle") && view != "castle-guests" && world.CastlePlace is { } castleSite)
        {
            // From the road before the castle, aside and above: the walls, the towers, the gate and the keep.
            camera.SetProcess(false);
            var eye = castleSite.Local(-26, -14);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 12;
            camera.LookAt(world.Ground.At(castleSite.Center.X, castleSite.Center.Z) + Vector3.Up * 8);
        }
        else if (view == "castle-guests" && world.CastlePlace is { } guestCastle)
        {
            // Beside the trailer, a little above: the guest on its bench, and the castle behind.
            camera.SetProcess(false);
            var behind = car.GlobalPosition - guestCastle.Inward * 3.2f;
            camera.GlobalPosition = behind + guestCastle.Right * 3.5f - guestCastle.Inward * 2.5f + Vector3.Up * 2.2f;
            camera.LookAt(behind + Vector3.Up * .8f);
        }
        else if (view.StartsWith("castle")) { }
        else if (view == "ferry-island" && world.FerryPlace is { } islandSite)
        {
            // From over the water before the island's pier: the punt at the pier, the car, the heron and the hide.
            camera.SetProcess(false);
            camera.GlobalPosition = islandSite.IslandBerth + new Vector3(16, ValleyBorders.SeaLevel + 9, -18);
            camera.LookAt(islandSite.Hide + Vector3.Up * 2);
        }
        else if (
            view.StartsWith("ferry")
            && view is not ("ferry-panel" or "ferry-ride" or "ferry-telescope")
            && world.FerryPlace is { } ferrySite
        )
        {
            // From inside the valley and above, past the landing and its pier to the punt on the border river.
            camera.SetProcess(false);
            var behind = ferrySite.Landing - ferrySite.Inward * 16 + ferrySite.Right * 14;
            camera.GlobalPosition = world.Ground.At(behind.X, behind.Z) + Vector3.Up * 9;
            camera.LookAt(ferrySite.PierStart + ferrySite.Inward * 6);
        }
        else if (view.StartsWith("ferry")) { }
        else if (view == "line-forge" && world.LineForge is { } forgeView)
        {
            // Before the forge from the road: the hut with its glowing door, the anvil, the stack of rails and the stone.
            camera.SetProcess(false);
            var eye = forgeView.ToGlobal(new Vector3(-5, 4, 15));
            camera.GlobalPosition = eye;
            camera.LookAt(forgeView.ToGlobal(new Vector3(0, 1.8f, 0)));
        }
        else if (view == "line-far" && world.MainLinePlace is { } lineFarSite)
        {
            // From high over the first arm, looking along it from the hub.
            camera.SetProcess(false);
            var route = lineFarSite.Arms[0];
            var from = route.Ground(Mathf.Min(40, route.Count - 1));
            var toward = route.Ground(Mathf.Min(route.Count - 1, 160));
            camera.GlobalPosition = from + Vector3.Up * 70 + (from - toward).Normalized() * 40;
            camera.LookAt(toward);
        }
        else if (view == "line-terminal" && world.MainLinePlace is { } terminalSite)
        {
            // From the apron of the first arm's terminal, aside and above: its platform, its building and its lamps.
            camera.SetProcess(false);
            var terminal = terminalSite.Terminals[0];
            var eye = terminal.Local(terminal.Side * 24, -26);
            camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 11;
            camera.LookAt(world.Ground.At(terminal.Position.X, terminal.Position.Z) + Vector3.Up * 1.5f);
        }
        else if (view.StartsWith("line-ride") || view.StartsWith("line-panel")) { }
        else if (view.StartsWith("line") && world.MainLine is { } lineView && world.MainLinePlace is { } lineSpot)
        {
            // From behind and above the end of the track laid so far, looking on along the way it takes; the finished
            // line is seen from the road side of the hub.
            camera.SetProcess(false);
            int stage = view.StartsWith("line-") && int.TryParse(view["line-".Length..], out int laid) ? laid : -1;
            if (view == "line-night")
                stage = -1;
            if (stage is >= 0 and < MainLinePlans.Trips)
            {
                var front = lineView.Front(stage);
                var way = lineView.FrontWay(stage);
                var eye = front - way * 24 + Vector3.Up.Cross(way) * 11;
                camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 9;
                camera.LookAt(front + way * 18);
            }
            else
            {
                var hub = lineSpot.Hub;
                var eye = hub.Local(hub.Side * 36, -44);
                camera.GlobalPosition = world.Ground.At(eye.X, eye.Z) + Vector3.Up * 13;
                camera.LookAt(world.Ground.At(hub.Position.X, hub.Position.Z) + Vector3.Up * 2);
            }
        }
        else if (view.StartsWith("explorer") || view.StartsWith("shells")) { }
        else if (view is "sheep" or "sheep-panel" or "sheep-lost") { }
        else if (view is "rally" or "rally-gate" or "rally-panel") { }
        else
            return false;
        return true;
    }
}
