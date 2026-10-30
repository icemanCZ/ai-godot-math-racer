using Godot;

namespace MathRacer;

// Explicitly ticked by Game so pause also freezes cargo, confetti and the camera.
public partial class Celebration : Node3D
{
    private float elapsed;
    private readonly Node3D[] confetti = new Node3D[32];
    private Node3D? cargo;
    private Vector3 cargoStart;
    private Vector3 destination;
    internal bool Finished => elapsed >= 3;
    internal void Setup(Vector3 center, Node3D? delivered = null, Vector3 target = default)
    {
        Position = center;
        for (int i = 0; i < confetti.Length; i++)
            confetti[i] = Art.Block(this, Vector3.Zero, new(.12f, .22f, .06f),
                new[] { "ffe58a", "ed776b", "68cfee", "70b765", "df92ac" }[i % 5]);
        cargo = delivered;
        if (cargo != null) { cargo.Reparent(this, true); cargoStart = cargo.GlobalPosition; destination = target; }
    }
    internal void Tick(double delta)
    {
        elapsed += (float)delta;
        for (int i = 0; i < confetti.Length; i++)
        {
            float angle = i * 2.4f, radius = elapsed * (1.2f + i % 4 * .25f);
            confetti[i].Position = new(Mathf.Cos(angle) * radius, 1 + elapsed * (4 + i % 3) - elapsed * elapsed * 2, Mathf.Sin(angle) * radius);
            confetti[i].Rotation = new(elapsed * 3, angle, elapsed * 4);
            confetti[i].Visible = confetti[i].Position.Y > 0;
        }
        if (cargo != null)
        {
            float t = Mathf.Clamp(elapsed / 1.4f, 0, 1);
            cargo.GlobalPosition = cargoStart.Lerp(destination, Mathf.SmoothStep(0, 1, t)) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 2;
            cargo.Scale = Vector3.One * Mathf.Lerp(1, .45f, t);
            cargo.Visible = elapsed < 2.2f;
        }
    }
}
