using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// Stages a captured view of a building of the valley: the lookout tower, the mill and the jetty, the building
/// plots, the playground, the railway, the zoo and the hunter's hut and hunt, each with as many deliveries made as the
/// name of the view says and the car parked where the picture wants it.
/// </summary>
internal static class SiteCaptures
{
    internal static void Prepare(Game game, World world, Vehicle vehicle, string view)
    {
        if (view is "lookout-day" or "lookout-night" && world.Projects.Length > BuildPlans.Tower)
        {
            // The finished tower with the car beside the stargazer; the capture starts the look itself.
            var tower = world.Projects[BuildPlans.Tower];
            tower.SetStage(BuildPlans.Trips);
            var builder = tower.Builder.Position;
            vehicle.SetPose(world.Ground.At(builder.X + 2, builder.Z, .15f), 0);
            if (view == "lookout-night")
                game.Weather.Begin(WeatherKind.Night, instant: true);
        }
        if (CaptureViews.ParseBuildView(view) is var (viewProject, viewStage) && viewProject < world.Projects.Length)
        {
            var project = world.Projects[viewProject];
            project.SetStage(viewStage);
            // The finished mill shows its sacks of flour, ground for the bakery.
            if (project.Complete && project is Mill)
                world.FlourMill?.SetStock(8);
            var drop = project.DropFor(0);
            vehicle.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
        }
        if (
            CaptureViews.ParsePlotView(view) is var (plotIndex, plotStage)
            && plotIndex < world.Village.BuildPlots.Count
        )
        {
            var plot = world.Village.BuildPlots[plotIndex];
            plot.SetStage(plotStage);
            vehicle.SetPose(world.Ground.At(plot.Drop.X, plot.Drop.Z, .15f), 0);
        }
        if (CaptureViews.ParsePlaygroundView(view) is { } playgroundStage && world.Playground is { } yard)
        {
            yard.SetStage(playgroundStage);
            // A few seconds pass so that the swings are swinging and the child is on the slide.
            for (int i = 0; i < 35; i++)
                yard._Process(.1);
            vehicle.SetPose(world.Ground.At(yard.Drop.X, yard.Drop.Z, .15f), 0);
        }
        if (CaptureViews.ParseRailwayView(view) is { } railwayStage && world.Railway is { } railway)
        {
            railway.SetStage(railwayStage);
            // The train has gone a little way round the loop, with its steam and whistle.
            for (int i = 0; i < 160; i++)
                railway._Process(.1);
            var parked = railway.Drop - railway.Site.Inward * (view is "railway-close" or "railway-pet" ? 30 : 6);
            vehicle.SetPose(world.Ground.At(parked.X, parked.Z, .15f), 0);
        }
        if (CaptureViews.ParseZooView(view) is { } zooStage && world.Zoo is { } zoo)
        {
            zoo.SetStage(zooStage);
            // A few seconds pass so that the animals are at their business and the visitors are on the path.
            for (int i = 0; i < 90; i++)
                zoo._Process(.1);
            var parked = zoo.Drop - zoo.Site.Inward * (view is "zoo-close" or "zoo-pet" ? 22 : 6);
            vehicle.SetPose(world.Ground.At(parked.X, parked.Z, .15f), 0);
        }
        if (view.StartsWith("hunter-") && world.Pen is { } pen && world.Hunter is { } hunter)
        {
            // The hut with S of its four deliveries, the finished pen full of animals, the hunter close up or his panel.
            var site = pen.Site;
            pen.SetStage(CaptureViews.ParseHunterView(view) ?? (view == "hunter-panel" ? 1 : HutPlans.Trips));
            if (view is "hunter-pen" or "hunter-close")
            {
                world.Hunt.Restore(
                    new[] { "Rabbit", "Fox", "Deer", "Rabbit", "Deer", "Fox", "Rabbit", "Deer" },
                    false,
                    ""
                );
                pen.SetAnimals(world.Hunt.Penned);
                for (int i = 0; i < 40; i++)
                    pen.Tick(.1f, site.Gate, true);
            }
            var inward = site.Inward;
            vehicle.SetPose(
                world.Ground.At(site.Drop.X, site.Drop.Z, .15f),
                Mathf.Atan2(-inward.X, -inward.Z) + (view == "hunter-panel" ? Mathf.Pi : 0)
            );
            if (view == "hunter-panel")
            {
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                game.OpenHunter(hunter);
            }
        }
        if (view is "hunt" or "hunt-net" or "hunt-carry" && world.Pen is { } huntPen && world.Hunter is { } huntHunter)
        {
            // A hunt under way: the animals ahead of the car, the net come down on one of them, or one on the car's roof.
            huntPen.SetStage(HutPlans.Trips);
            world.Hunt.Restore(new[] { "Rabbit", "Fox" }, true, view == "hunt-carry" ? "Fox" : "");
            huntPen.SetAnimals(world.Hunt.Penned);
            var ahead = new Vector3(1, 0, .3f).Normalized();
            var side = ahead.Cross(Vector3.Up);
            float heading = Mathf.Atan2(-ahead.X, -ahead.Z);
            if (view == "hunt-carry")
            {
                world.HuntGrounds.Clear();
                world.HuntGrounds.Carry(WildKind.Fox, false);
                var road = huntPen.Site.Road;
                var gate = huntPen.Site.Gate;
                var towards = gate - road;
                towards.Y = 0;
                towards = towards.Normalized();
                var from = road - towards * 6;
                vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), Mathf.Atan2(-towards.X, -towards.Z));
            }
            else
            {
                world.HuntGrounds.Begin(huntHunter.Position, world.Hunt.Caught);
                var target = world.HuntGrounds.PositionOf(0);
                world.HuntGrounds.Place(0, world.Ground.At(target.X, target.Z));
                if (view == "hunt")
                {
                    var second = target + side * 4 + ahead * 3;
                    var third = target - side * 5 + ahead * 6;
                    world.HuntGrounds.Place(1, world.Ground.At(second.X, second.Z));
                    world.HuntGrounds.Place(2, world.Ground.At(third.X, third.Z));
                    var from = target - ahead * 15;
                    vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), heading);
                }
                else
                {
                    for (int i = 1; i < world.HuntGrounds.HerdCount; i++)
                    {
                        var aside = target + side * (14 + i * 4) + ahead * 10;
                        world.HuntGrounds.Place(i, world.Ground.At(aside.X, aside.Z));
                    }
                    var from = target - ahead * 14;
                    vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), heading);
                    world.HuntGrounds.Throw();
                    for (int i = 0; i < 10; i++)
                        world.HuntGrounds.Tick(.1f, true);
                    // The car has driven up to its catch.
                    var caught = world.HuntGrounds.NetSpot ?? target;
                    var near = caught - ahead * 6 + side * 2;
                    vehicle.SetPose(world.Ground.At(near.X, near.Z, .15f), heading);
                }
            }
        }
    }
}
