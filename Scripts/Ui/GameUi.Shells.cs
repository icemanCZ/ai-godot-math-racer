using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>): the corner panel by the cove, with the arrow to the nearest shell
/// the low tide uncovers and how far it is (or that the low tide is awaited), and the eight kinds of shell in pictures,
/// faint until one is found, each with how many the player has.
/// </summary>
public partial class GameUi
{
    private PanelContainer shellsHud = null!;
    private DeliveryPicture shellsArrow = null!;
    private Label shellsDistance = null!;
    private readonly ShellPicture[] shellPictures = new ShellPicture[ShellPlans.Kinds];
    private readonly Label[] shellCounts = new Label[ShellPlans.Kinds];

    private void SetupShells()
    {
        shellsHud = new PanelContainer
        {
            Name = "ShellsHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("2f6f8f", .9f);
        shellsHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(shellsHud);
        shellsHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        shellsHud.OffsetLeft = -290;
        shellsHud.OffsetRight = 290;
        shellsHud.OffsetTop = 96;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        shellsHud.AddChild(row);
        var pointer = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(pointer);
        shellsArrow = new DeliveryPicture
        {
            Name = "ShellsArrow",
            Icon = "arrow",
            CustomMinimumSize = new(56, 56),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        pointer.AddChild(shellsArrow);
        shellsDistance = HudLabel("ShellsDistance", 18);
        shellsDistance.AddThemeColorOverride("font_color", new Color("fff3c4"));
        shellsDistance.HorizontalAlignment = HorizontalAlignment.Center;
        pointer.AddChild(shellsDistance);
        for (int kind = 0; kind < ShellPlans.Kinds; kind++)
        {
            var cell = new VBoxContainer { Name = $"ShellKind{kind}", MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(cell);
            shellPictures[kind] = new ShellPicture
            {
                Kind = kind,
                Faint = true,
                CustomMinimumSize = new(48, 48),
            };
            cell.AddChild(shellPictures[kind]);
            shellCounts[kind] = HudLabel($"ShellCount{kind}", 18);
            shellCounts[kind].HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(shellCounts[kind]);
        }
    }

    /// <summary>
    /// The corner panel by the cove: the arrow turned to <paramref name="target"/> as seen from the camera, the
    /// <paramref name="distance"/> to it (or a word that the low tide is awaited), and how many shells of each of the
    /// <paramref name="kinds"/> the player has. It sits under the other corner panels that show.
    /// </summary>
    internal void ShellsHud(bool visible, Vehicle car, Vector3 target, int[] kinds, string distance)
    {
        shellsHud.Visible = visible && !modal.Visible;
        if (!shellsHud.Visible)
            return;
        float top = 96;
        foreach (var above in new Control[] { trackingHud, sheepHud, explorerHud })
            if (above.Visible)
                top = Mathf.Max(top, above.OffsetTop + above.Size.Y + 10);
        shellsHud.OffsetTop = top;
        int found = 0;
        for (int kind = 0; kind < ShellPlans.Kinds; kind++)
        {
            int count = kind < kinds.Length ? kinds[kind] : 0;
            if (count > 0)
                found++;
            if (shellPictures[kind].Faint != (count == 0))
            {
                shellPictures[kind].Faint = count == 0;
                shellPictures[kind].QueueRedraw();
            }
            shellCounts[kind].Text = count > 0 ? count.ToString() : "";
        }
        bool pointing = target != Vector3.Zero;
        shellsArrow.Modulate = pointing ? Colors.White : new Color(1, 1, 1, .25f);
        shellsDistance.Text = distance;
        shellsHud.AccessibilityName = $"{Texts.ShellsHud}. {distance}. {Texts.ShellsKinds(found, ShellPlans.Kinds)}";
        var camera = GetViewport().GetCamera3D();
        if (camera == null || !pointing)
            return;
        var delta = target - car.Position;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        shellsArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        shellsArrow.QueueRedraw();
    }

    /// <summary>Whether the corner panel of the shells shows, for the checks.</summary>
    internal bool ShellsHudShown => shellsHud.Visible;
}
