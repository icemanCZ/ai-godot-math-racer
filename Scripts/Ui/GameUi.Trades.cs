using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    public void TradeHint(Character owner)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text =
            (controller ? "A · " : "E · ") + (owner.Role is TraderRole shop ? Village.TradeSigns[shop.Index] : "");
    }

    /// <summary>
    /// The baker of the village shop, whose bakery has too little bread for his next order: the loaves he needs, then the
    /// trip of the bread chain under way, or the play button that sends the player for what is missing (flour from the
    /// mill, or wheat for the mill), or nothing while the mill is not finished or another trip is under way.
    /// </summary>
    internal void BakerNeedsBread(Character baker, int loaves, HaulTrip? trip, HaulLoad? missing)
    {
        Clear();
        Text(baker.DisplayName, 30);
        Picture(content, "bread", null, loaves);
        Button? action = null;
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (missing is { } raw)
        {
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            content.AddChild(need);
            Picture(need, LoadIcon(raw))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, raw == HaulLoad.Wheat ? "flour" : "bread")!.CustomMinimumSize = new(100, 84);
            action = IconButton("check", () => Send(new UiIntent.MissingSupply()), "StartMissingSupply");
            action.AccessibilityName = Texts.ZaprahnoutVozikProSurovinu;
        }
        else
            Text("…", 30);
        var back = BackButton();
        (action ?? back).GrabFocus();
    }

    public void TradeStatus(Character owner, Expedition? expedition)
    {
        Clear();
        Text(owner.DisplayName, 30);
        if (expedition?.Sender == owner)
        {
            Picture(content, expedition.Cargo, null, expedition.Amount);
            Picture(content, "", expedition.Recipient);
        }
        else
            Text("…", 30);
        BackButton().GrabFocus();
    }
}
