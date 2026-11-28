using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>
/// The post to the four corners of the valley: the postman's panel (the album of stamps, a row of three for every keeper
/// of a border, and a green check mark that gives out the next letter), the keepers' panel, the talk-button hints and the
/// envelope in the corner while a letter or a reply is carried, with a picture of whom it is for.
/// </summary>
public partial class GameUi
{
    private PanelContainer letterHud = null!;
    private HBoxContainer letterPictures = null!;
    private Label letterName = null!;
    private bool letterCarried;

    /// <summary>The picture of each border, in the order of <see cref="Border"/>.</summary>
    internal static string BorderIcon(int border) =>
        border switch
        {
            0 => "sea",
            1 => "mountains",
            2 => "river",
            _ => "forest",
        };

    private void SetupPost()
    {
        letterHud = new PanelContainer
        {
            Name = "LetterHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("1f4e8c", .88f);
        letterHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(letterHud);
        letterHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        letterHud.OffsetLeft = -190;
        letterHud.OffsetRight = 190;
        letterHud.OffsetTop = 16;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        letterHud.AddChild(row);
        letterPictures = new HBoxContainer { Name = "LetterPictures", MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(letterPictures);
        letterName = new Label
        {
            Name = "LetterName",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        letterName.AddThemeFontSizeOverride("font_size", 26);
        row.AddChild(letterName);
    }

    /// <summary>
    /// The envelope in the corner: a letter for a keeper (<paramref name="border"/> 0 to 3) with the picture of the border,
    /// or a reply for a resident (<paramref name="border"/> −1); none when <paramref name="addressee"/> is null.
    /// </summary>
    internal void LetterHud(Character? addressee, int border)
    {
        letterCarried = addressee != null;
        foreach (var child in letterPictures.GetChildren())
            child.QueueFree();
        if (addressee == null)
        {
            letterHud.Visible = false;
            return;
        }
        letterPictures.AddChild(
            new DeliveryPicture
            {
                Icon = "letter",
                CustomMinimumSize = new(64, 54),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        letterPictures.AddChild(
            new DeliveryPicture
            {
                Icon = border >= 0 ? BorderIcon(border) : "person",
                Character = border >= 0 ? null : addressee,
                CustomMinimumSize = new(64, 54),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        letterName.Text = addressee.DisplayName;
        letterHud.AccessibilityName = OutlandTexts.LetterFor(addressee.DisplayName);
        ShowLetterHud(true);
    }

    /// <summary>Shows the envelope while something is carried and nothing covers the screen.</summary>
    internal void ShowLetterHud(bool visible) => letterHud.Visible = visible && letterCarried && !modal.Visible;

    internal void PostmanHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.PostmanHint;
    }

    internal void KeeperHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.KeeperHint;
    }

    /// <summary>The album: for every keeper the picture of the border and three stamps, the ones sent bright.</summary>
    private void StampAlbum(Post post)
    {
        var album = new VBoxContainer { Name = "StampAlbum" };
        album.AddThemeConstantOverride("separation", 2);
        content.AddChild(album);
        for (int keeper = 0; keeper < Post.Keepers; keeper++)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 6);
            album.AddChild(row);
            row.AddChild(
                new DeliveryPicture
                {
                    Icon = BorderIcon(keeper),
                    CustomMinimumSize = new(62, 52),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            for (int stamp = 0; stamp < Post.StampsPerKeeper; stamp++)
                row.AddChild(
                    new DeliveryPicture
                    {
                        Icon = "stamp",
                        CustomMinimumSize = new(56, 48),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        Modulate = new Color(1, 1, 1, stamp < post.Stamps(keeper) ? 1 : .25f),
                    }
                );
        }
        Text(OutlandTexts.PostStamps(post.AlbumStamps, Post.Keepers * Post.StampsPerKeeper), 20);
    }

    /// <summary>
    /// The postman's panel: the album, and then the green check mark that gives out the letter for <paramref name="next"/>,
    /// or, while a letter or a reply is carried, whom it is for and only the way back.
    /// </summary>
    internal void PostPanel(Character postman, Post post, Character? carried, Character? next)
    {
        Clear();
        Text(postman.DisplayName, 30);
        StampAlbum(post);
        if (carried != null || next == null)
        {
            if (carried != null)
                Text(OutlandTexts.PostStillCarrying(carried.DisplayName), 20);
            BackButton().GrabFocus();
            return;
        }
        Text(OutlandTexts.LetterFor(next.DisplayName), 22);
        var take = AcceptButton(() => Send(new UiIntent.TakeLetter()), "TakeLetter", OutlandTexts.PostTake);
        take.GrabFocus();
        BackButton();
    }

    /// <summary>A keeper's panel: the border, the stamps the keeper has sent, and the way back.</summary>
    internal void KeeperPanel(Character keeper, Border border, int stamps)
    {
        Clear();
        Text(keeper.DisplayName, 30);
        StatusPicture(BorderIcon((int)border));
        KeeperStamps(stamps);
        Text(OutlandTexts.KeeperAbout(border), 20);
        BackButton().GrabFocus();
    }

    /// <summary>The stamps a keeper sends the album: three of them, the ones sent bright, the others faint.</summary>
    private void KeeperStamps(int stamps)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "KeeperStamps" };
        row.AddThemeConstantOverride("separation", 6);
        row.TooltipText = OutlandTexts.PostStamps(stamps, Post.StampsPerKeeper);
        content.AddChild(row);
        for (int stamp = 0; stamp < Post.StampsPerKeeper; stamp++)
            row.AddChild(
                new DeliveryPicture
                {
                    Icon = "stamp",
                    CustomMinimumSize = new(78, 64),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = new Color(1, 1, 1, stamp < stamps ? 1 : .25f),
                }
            );
    }
}
