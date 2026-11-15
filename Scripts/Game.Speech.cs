using Godot;
using System.Linq;

namespace MathRacer;

public partial class Game
{
    private string? czechVoice;

    private void Speak(string text)
    {
        if (!DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech)) return;
        czechVoice ??= DisplayServer.TtsGetVoicesForLanguage("cs").FirstOrDefault() ?? "";
        if (czechVoice.Length > 0) DisplayServer.TtsSpeak(text, czechVoice, rate: 0.9f, interrupt: true);
    }

    private void StopSpeech()
    {
        if (czechVoice is { Length: > 0 }) DisplayServer.TtsStop();
    }

    private static string QuizInstruction(Animal actor, IChallenge question)
    {
        string instruction = question switch
        {
            SyllableChallenge => "nejprve vybrala slabiky",
            LetterChallenge => "nejprve vybrala obrázek podle prvního písmene",
            ReadingChallenge => "nejprve přečetla slovo a vybrala obrázek",
            DeliveryChallenge => "spočítala náklad",
            _ => "nejprve vyřešila příklad"
        };
        return $"{actor.DisplayName} chce, abys {instruction}.";
    }

    private static string ExpeditionInstruction(Expedition expedition)
    {
        string task = expedition switch
        {
            { Golem: true } => "přivez šest dílků golema",
            { Farm: true } => $"přivez {expedition.FarmName}",
            { Garden: true } => $"přivez {expedition.FlowerName}",
            { Gems: true } or { Toys: true } => $"přivez {expedition.CollectionName}",
            { Collection: true } => "přivez tři jablka",
            _ => $"rozvez {DeliveryCargoInstruction(expedition.Cargo, expedition.Amount)}"
        };
        return $"Teď prosím {task}.";
    }

    private static string DeliveryCargoInstruction(string cargo, int amount)
    {
        var (one, two, three) = cargo switch
        {
            "bread" => ("jeden kus pečiva", "tyto dva kusy pečiva", "tyto tři kusy pečiva"),
            "parcel" => ("jeden balíček", "tyto dva balíčky", "tyto tři balíčky"),
            "cake" => ("jeden koláček", "tyto dva koláčky", "tyto tři koláčky"),
            "carrot" => ("jednu mrkev", "tyto dvě mrkve", "tyto tři mrkve"),
            "apple" => ("jedno jablko", "tato dvě jablka", "tato tři jablka"),
            _ => ("jednu sklenici medu", "tyto dvě sklenice medu", "tyto tři sklenice medu")
        };
        return amount switch { 1 => one, 2 => two, _ => three };
    }
}
