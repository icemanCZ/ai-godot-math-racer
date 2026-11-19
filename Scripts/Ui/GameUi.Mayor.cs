using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Settlement;

namespace MathRacer.Ui;

/// <summary>The mayor's shop: a card with a picture, a price and a buy button for each decoration of the town.</summary>
public partial class GameUi
{
    private float mayorScroll;
    private string mayorFocus = "";

    internal void MayorHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? Texts.AStarostka : Texts.EStarostka;
    }

    internal void MayorShop(Character mayor, string[] owned, int coins)
    {
        Clear();
        Text(mayor.DisplayName, 30);
        var money = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        money.AddThemeConstantOverride("separation", 8);
        content.AddChild(money);
        money.AddChild(
            new Label { Text = $"{Texts.DekoraceDoMesta}   {owned.Length} / {TownDecorations.All.Count}   ·" }
        );
        money.AddChild(new DeliveryPicture { Icon = "coin", CustomMinimumSize = new(40, 36) });
        money.AddChild(new Label { Name = "MayorCoins", Text = coins.ToString() });
        var scroll = new ScrollContainer
        {
            Name = "MayorScroll",
            CustomMinimumSize = new(680, 380),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        content.AddChild(scroll);
        scroll.GetVScrollBar().ValueChanged += value => mayorScroll = (float)value;
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(grid);
        Button? first = null,
            selected = null;
        foreach (var decoration in TownDecorations.All)
        {
            bool have = owned.Contains(decoration.Id);
            bool affordable = coins >= decoration.Price;
            var card = new VBoxContainer();
            grid.AddChild(card);
            card.AddChild(new DecorationPreview { Decoration = decoration });
            var button = Button(
                have ? Texts.Koupeno : decoration.Name,
                () =>
                {
                    mayorScroll = scroll.ScrollVertical;
                    mayorFocus = "Decoration_" + decoration.Id;
                    Send(new UiIntent.BuyDecoration(decoration.Id));
                },
                card
            );
            button.Name = "Decoration_" + decoration.Id;
            button.CustomMinimumSize = new(205, 48);
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Disabled = have || !affordable;
            button.AccessibilityName = have
                ? $"{decoration.Name} · {Texts.Koupeno}"
                : Texts.BuyNamed(decoration.Name, decoration.Price);
            button.TooltipText =
                have ? decoration.Name
                : affordable ? Texts.BuyFor(decoration.Price)
                : Texts.PriceHint(decoration.Price);
            var tag = new HBoxContainer
            {
                Name = "Price_" + decoration.Id,
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            tag.AddThemeConstantOverride("separation", 4);
            card.AddChild(tag);
            card.MoveChild(tag, button.GetIndex());
            if (have)
            {
                tag.AddChild(new Label { Text = decoration.Name, MouseFilter = Control.MouseFilterEnum.Ignore });
                button.AddThemeStyleboxOverride("disabled", Style("cde6cb", 16));
                button.AddThemeColorOverride("font_disabled_color", new Color("316754"));
            }
            else
            {
                tag.AddChild(
                    new DeliveryPicture
                    {
                        Icon = "coin",
                        CustomMinimumSize = new(26, 26),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    }
                );
                var price = new Label
                {
                    Text = decoration.Price.ToString(),
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                price.AddThemeFontSizeOverride("font_size", 17);
                price.AddThemeColorOverride("font_color", new Color(affordable ? "b07812" : "8a8a8a"));
                tag.AddChild(price);
                if (affordable)
                {
                    button.AddThemeStyleboxOverride("normal", Style("d9a02a", 16));
                    button.AddThemeStyleboxOverride("hover", Style("e8b23a", 16));
                    button.AddThemeStyleboxOverride("pressed", Style("c08a1e", 16));
                }
                else
                {
                    button.AddThemeStyleboxOverride("disabled", Style("d5d5d5", 16));
                    button.AddThemeColorOverride("font_disabled_color", new Color("777777"));
                }
            }
            if (!button.Disabled)
            {
                first ??= button;
                if (mayorFocus == button.Name)
                    selected = button;
            }
        }
        var back = BackButton();
        (selected ?? first ?? back).GrabFocus();
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, (int)mayorScroll);
    }
}
