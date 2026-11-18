namespace MathRacer;

public partial class GameUi
{
    public void TradeHint(Animal owner)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text =
            (controller ? "A · " : "E · ") + (owner.Role is TraderRole shop ? Village.TradeSigns[shop.Index] : "");
    }

    public void TradeStatus(Animal owner, Expedition? expedition)
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
