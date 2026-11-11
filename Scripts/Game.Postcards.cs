using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class Game
{
    private sealed record Postcard(string Name, string Clue, string Letter, string[] Pictures, int Correct);
    private static readonly Postcard[] Postcards =
    {
        new("Most a lekníny", "U příjezdu k mostu",
            "M", new[] { "postcard-village", "postcard-bridge", "postcard-farm" }, 1),
        new("Domečky pod kopcem", "Na silnici nad vesnicí",
            "D", new[] { "postcard-bridge", "postcard-farm", "postcard-village" }, 2),
        new("Farma mezi loukami", "Na silnici u farmy",
            "F", new[] { "postcard-farm", "postcard-village", "postcard-bridge" }, 0),
        new("Garáž u cesty", "Poblíž garáže", "G",
            new[] { "postcard-garden", "postcard-garage", "postcard-meadow" }, 1),
        new("Zahrada plná květin", "U zahradnictví", "Z",
            new[] { "postcard-meadow", "postcard-garage", "postcard-garden" }, 2),
        new("Louka u lesní cesty", "U lučních květin", "L",
            new[] { "postcard-meadow", "postcard-garden", "postcard-garage" }, 0)
    };
    internal static int PostcardCount => Postcards.Length;
    internal const int PostcardRewardStamps = 3;
    internal const int OriginalPostcardCount = 3;
    private static readonly CarReward PostcardReward = new(CarAccessory.ValleyMap.Id, true);
    private readonly PostcardProgress?[] postcards = new PostcardProgress?[PostcardCount];
    private readonly (Vector3 Stop, Vector3 Subject, Label3D Sign)[] viewpoints =
        new (Vector3, Vector3, Label3D)[PostcardCount];
    private Vector3 albumStop;
    private int postcardPage = -1;
    private int pendingPhoto = -1;
    private double photoTime;
    private int rejectedPostcardPictures;
    private string postcardFeedback = "";
    private Camera3D? photoCamera;
    private SubViewport? photoViewport;

    private void SetupPostcards()
    {
        var square = world.Village.Square;
        albumStop = square.ToGlobal(new Vector3(0, 0, 4.4f));
        PostcardMarker(square, new Vector3(0, 3.3f, 4.4f));
        Art.Sign(square, new Vector3(0, 2.35f, 4.4f), "Album pohlednic", 38);

        var bridge = new Vector3(River.CenterX(River.BridgeZ), River.BridgeHeight, River.BridgeZ);
        var village = square.GlobalPosition;
        var farm = world.Village.FarmBuilding.GlobalPosition;
        var subjects = new[]
        {
            bridge, village, farm, world.Village.GarageBuilding.GlobalPosition,
            world.Village.GardenBuilding.GlobalPosition, world.Plants.MeadowPreview
        };
        var actors = world.Animals.Concat(world.Village.Residents).Concat(world.Village.Traders)
            .Append(world.Village.Farmer).Append(world.Village.Gardener)
            .Append(world.Village.GemCollector).Append(world.Village.Toymaker).Append(world.Witch).ToArray();
        var candidates = world.Routes.Samples.Select(point =>
        {
            point.Y = world.Routes.SurfaceHeight(point, world.Ground.Height(point.X, point.Z));
            return point;
        }).Where(point => !River.OnBridge(point.X, point.Z) &&
            actors.All(actor => actor.Position.DistanceTo(point) > Settings.InteractionRange + 5) &&
            point.DistanceTo(world.Village.GarageEntrance) > Settings.InteractionRange + 5 &&
            point.DistanceTo(albumStop) > 13 &&
            Events.Points.Cast<Vector3>().All(gate => gate.DistanceTo(point) > 10)).ToArray();
        for (int i = 0; i < Postcards.Length; i++)
        {
            var subject = subjects[i];
            float preferredDistance = i == 1 ? 44 : i == 5 ? 16 : 27;
            var stop = candidates.Where(point => viewpoints.Take(i).All(view => view.Stop.DistanceTo(point) > 15))
                .MinBy(point => Mathf.Abs(point.DistanceTo(subject) - preferredDistance)
                    + (i == 1 ? 2 * Mathf.Max(0, subject.Y + 4 - point.Y) : 0));
            var sign = PostcardMarker(world, stop + Vector3.Up * 4);
            Art.Sign(sign, Vector3.Down * .85f, Postcards[i].Name, 34);
            viewpoints[i] = (stop, subject, sign);
        }
    }

    private static TaskMarker PostcardMarker(Node3D parent, Vector3 position)
    {
        var marker = Art.TaskMarker(parent, position, "");
        Art.RoleTaskIcon(marker, "envelope");
        return marker;
    }

    private static int PostcardIndex(string icon) => Array.FindIndex(Postcards, card => card.Pictures[card.Correct] == icon);

    internal Vector3 PostcardStop(int index) => viewpoints[index].Stop;

    private int NearbyPostcard() => Array.FindIndex(viewpoints, view =>
        view.Sign.Visible && vehicle.Position.DistanceTo(view.Stop) < 4.5f);

    private bool InteractWithPostcards()
    {
        if (vehicle.Position.DistanceTo(albumStop) < 4.5f)
        {
            vehicle.Active = false;
            nearby = null;
            State = GameState.PostcardAlbum;
            OpenPostcardPage(-1);
            return true;
        }

        int index = NearbyPostcard();
        if (index < 0 || Mathf.Abs(vehicle.Speed) > 1) return false;
        nearby = null;
        vehicle.Active = false;
        State = GameState.PostcardAlbum;
        postcardPage = -1;
        pendingPhoto = index;
        photoTime = 1.5;
        ui.HideModal();
        ui.DeliveryHint(false);

        var subject = viewpoints[index].Subject;
        var car = vehicle.GlobalPosition;
        var toward = subject - car;
        toward.Y = 0;
        toward = toward.Normalized();
        var position = car - toward * 12 + toward.Cross(Vector3.Up) * 5 + Vector3.Up * 7;
        position.Y = Mathf.Max(position.Y, world.Ground.Height(position.X, position.Z) + 5);
        var target = car.Lerp(subject, .42f) + Vector3.Up * 1.7f;
        photoCamera = new Camera3D { Fov = 57, Far = 1200, CullMask = followCamera.CullMask & ~Art.SignLayer };
        AddChild(photoCamera);
        photoCamera.LookAtFromPosition(position, target);
        photoCamera.MakeCurrent();

        if (DisplayServer.GetName() == "headless") return true;
        photoViewport = new SubViewport
        {
            World3D = GetViewport().World3D, Size = new Vector2I(640, 360),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once
        };
        AddChild(photoViewport);
        var snapshot = new Camera3D
        {
            Fov = photoCamera.Fov, Far = photoCamera.Far, CullMask = photoCamera.CullMask, Current = true
        };
        photoViewport.AddChild(snapshot);
        snapshot.GlobalTransform = photoCamera.GlobalTransform;
        return true;
    }

    private void TickPostcards(double delta)
    {
        if (State == GameState.PostcardAlbum && pendingPhoto >= 0)
        {
            photoTime -= delta;
            if (photoTime > 0) return;
            using var image = photoViewport?.GetTexture().GetImage();
            byte[] jpeg = image != null && !image.IsEmpty() ? image.SaveJpgToBuffer(.82f) : Array.Empty<byte>();
            int index = pendingPhoto;
            CancelPostcardPhoto();
            postcards[index] = new PostcardProgress(jpeg, false);
            viewpoints[index].Sign.Hide();
            OpenPostcardPage(index);
            SaveProgress();
        }
        if (State != GameState.Exploring || AtGarage || nearby != null) return;
        if (vehicle.Position.DistanceTo(albumStop) < 4.5f) ui.EventHint("Album");
        else if (NearbyPostcard() >= 0)
        {
            bool stopped = Mathf.Abs(vehicle.Speed) <= 1;
            if (stopped) ui.EventHint("Vyfotit");
            else if (Events.Active < 0) ui.EventHud("Zastav na vyhlídce a vyfoť pohlednici.");
        }
    }

    private void CancelPostcardPhoto()
    {
        if (pendingPhoto < 0) return;
        pendingPhoto = -1;
        followCamera.MakeCurrent();
        photoCamera?.QueueFree();
        photoViewport?.QueueFree();
        photoCamera = null;
        photoViewport = null;
    }

    private void OpenPostcardPage(int index)
    {
        postcardPage = index;
        rejectedPostcardPictures = 0;
        postcardFeedback = "";
        ShowPostcards();
    }

    private void ShowPostcards()
    {
        int count = postcards.Count(card => card?.Stamped == true);
        if (postcardPage < 0)
        {
            ui.PostcardAlbum(count, Postcards.Select((card, i) =>
                (card.Name, card.Clue, card.Pictures[card.Correct], postcards[i] != null, postcards[i]?.Stamped == true)).ToArray(), OpenPostcardPage);
            return;
        }
        var card = Postcards[postcardPage];
        var progress = postcards[postcardPage]!;
        ui.PostcardDetail(card.Name, card.Letter, card.Pictures, progress, rejectedPostcardPictures, postcardFeedback,
            AnswerPostcard, () => OpenPostcardPage(-1));
    }

    private void AnswerPostcard(int answer)
    {
        if (State != GameState.PostcardAlbum || postcardPage < 0 ||
            postcards[postcardPage] is not { Stamped: false } card) return;
        if (answer == Postcards[postcardPage].Correct)
        {
            postcards[postcardPage] = card with { Stamped = true };
            postcardFeedback = "Správně! Pohlednice má razítko.";
            if (postcards.Count(progress => progress?.Stamped == true) >= PostcardRewardStamps && OwnedRewards.Add(PostcardReward))
            {
                vehicle.Appearance.ToggleAccessory(PostcardReward.Id);
                postcardFeedback = "V garáži máš samolepku Mapka údolí!";
            }
            SaveProgress();
        }
        else rejectedPostcardPictures |= 1 << answer;
        ShowPostcards();
    }
}

public sealed record PostcardProgress(byte[] Jpeg, bool Stamped);
