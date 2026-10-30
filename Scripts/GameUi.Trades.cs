namespace MathRacer;

public partial class GameUi
{
    public void TradeHint(Animal owner)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Village.TradeSigns[owner.TradeIndex];
    }

    public void TradeStatus(Animal owner, Expedition? expedition)
    {
        Clear(); Text(owner.DisplayName, 30);
        if (expedition?.Sender == owner)
        {
            Text("Objednávka už je naložená!", 28);
            Picture(content, expedition.Cargo, null, expedition.Amount);
            Text($"Doruč náklad: {expedition.Recipient.DisplayName}\nZelené šipky tě povedou k zákazníkovi.", 22);
        }
        else Text("Nejdřív dokonči rozjetou výpravu.\nMoje objednávka tu na tebe počká.", 24);
        Text("Za doručení si vybereš doplněk, barvu nebo typ auta.", 22);
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
}
