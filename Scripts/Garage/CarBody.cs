using System.Collections.Generic;
using System.Linq;

namespace MathRacer.Garage;

/// <summary>
/// A coachwork of the garage. <paramref name="OwnWheels"/> is for a model that carries wheels of its own, so the four
/// tyres of the car are hidden; <paramref name="TrackX"/> and <paramref name="TrackZ"/> are where the tyre marks and the
/// stamp trails start (the rear wheel on the right, mirrored on the left).
/// </summary>
public sealed record CarBody(
    string Id,
    string Name,
    bool OwnWheels = false,
    float TrackX = CarBody.CarTrackX,
    float TrackZ = CarBody.CarTrackZ
)
{
    public const float CarTrackX = .9875f;
    public const float CarTrackZ = .96f;

    /// <summary>The first four are cars; the others are toys of their own (<c>VehicleToys</c>). Ids are saved, never renamed.</summary>
    public static IReadOnlyList<CarBody> All { get; } =
        System.Array.AsReadOnly(
            new[]
            {
                new CarBody("classic", "Retro autíčko"),
                new CarBody("buggy", "Plážová bugina"),
                new CarBody("pickup", "Veselý pickup"),
                new CarBody("wagon", "Výletní kombík"),
                new CarBody("plane", "Vrtulové letadýlko", OwnWheels: true, TrackX: .62f, TrackZ: -.35f),
                new CarBody("tank", "Mírumilovný tank", OwnWheels: true, TrackX: .76f, TrackZ: 1.15f),
                new CarBody("train", "Parní mašinka", OwnWheels: true, TrackX: .88f, TrackZ: .75f),
                new CarBody("rocket", "Hvězdná raketa"),
                new CarBody("ufo", "Létající talíř", OwnWheels: true, TrackX: .62f, TrackZ: .75f),
            }
        );

    public static CarBody? Find(string id) => All.FirstOrDefault(body => body.Id == id);
}
