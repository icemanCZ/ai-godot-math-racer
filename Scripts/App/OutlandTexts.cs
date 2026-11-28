using MathRacer.Buildings;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The fixed Czech texts of what lies beyond the old valley: the post and the lighthouse, the dragon, the balloon,
/// the tracks, the sheep, the rally, the map, the shells, the ferry, the castle and the great railway. The rules of
/// <see cref="Texts"/> hold here too.
/// </summary>
internal static class OutlandTexts
{
    // Signs of the post office, the lighthouse, the ferry and the coast quarry
    public const string SignPost = "POŠTA";
    public const string SignLighthouse = "MAJÁK";
    public const string SignFerry = "PŘÍVOZ";
    public const string SignCastle = "HRAD";
    public const string SignQuarry = "LOM";
    public const string SignSlope = "STRÁŇ";

    // GameUi.Post and Game.Post
    public const string PostmanHint = "Pošťák";
    public const string KeeperHint = "Strážce hranic";
    public const string PostTake = "Vzít dopis";
    public const string PostAnotherLetter = "Mám tu další dopis! Odvezeš ho?";
    public const string ReplyDelivered = "Psaníčko od strážce hranic! Moc děkuju, že jsi ho přivezl.";

    public static string PostStillCarrying(string name) => $"Dopis už vezeš! Je pro: {name}.";

    public static string PostLetterFor(string name) => $"Tenhle dopis je pro: {name}. Sleduj šipky na silnici!";

    public static string LetterFor(string name) => $"Dopis pro: {name}";

    public static string PostStamps(int stamps, int all) => $"Známky v albu: {stamps} / {all}";

    public static string KeeperAbout(Border border) =>
        border switch
        {
            Border.Sea => "Tady končí údolí a začíná moře. Pošťák mi občas pošle psaníčko.",
            Border.Mountains => "Až sem nahoru do hor chodí jen ovečky a pošta. A vedle na skále leží drak Kvído!",
            Border.River => "Za řekou už je jiná země. Pramicí převážím jen poštu.",
            _ => "V lese je ticho. Těším se na každé psaníčko z vesnice.",
        };

    public static string LetterDelivered(bool stamp, int albumCoins, string? replyTo) =>
        (
            albumCoins > 0 ? $"Album známek je plné!  +{albumCoins}"
            : stamp ? "Nová známka do alba!"
            : "Děkuju za dopis!"
        ) + (replyTo == null ? "" : $"\nOdpověď vezeš pro: {replyTo}");

    // GameUi.Lighthouse and Game.Lighthouse
    public const string LighthouseWanted =
        "Na mysu by se hodil maják! Přivez mi kámen z lomu u silnice a postavíme ho spolu.";
    public const string LighthouseUnfinished = "Maják ještě není hotový. Každý náklad přidá kus.";
    public const string LighthouseShining = "Maják svítí! V noci se podívej, jak jeho světlo obchází celé údolí.";
    public const string LighthouseDoneSpeech = "Maják je hotový! Až se setmí, rozsvítím lucernu.";
    public const string LighthouseStart = "Poslat vozík";

    public static string LighthousePart(string part, int stage, int trips) => $"Maják má {part}!  {stage} / {trips}";

    public static string LighthouseDone(int coins) => $"Maják na mysu je hotový!  +{coins}";

    // GameUi.Dragon and Game.Dragon
    public const string DragonHint = "Drak Kvído";
    public const string DragonNestUnfinished = "Vejce v hnízdě stydne. Každý náklad ze stráně ho trochu zahřeje.";
    public const string DragonNestWanted =
        "Přivez mi písek, kameny a dřevo ze stráně u silnice. Postavíme vejci teplé hnízdo!";
    public const string DragonNext = "Hap… hapčí! Přivezeš mi další náklad ze stráně?";
    public const string DragonThanks = "Děkuju! Vejce je zase o kousek teplejší. Až budeš chtít, přivez další.";
    public const string DragonRiddleWanted =
        "Hnízdo je hotové! Ale bez ohně vejce nezahřeju. Když mi pomůžeš s hádankou, nadechnu se a kýchnu pořádně!";
    public const string DragonRiddleStart = "Hádanka";
    public const string DragonFlightWanted = "Jsem zdravý! Sedni si mi na záda a proletíme se nad celým údolím!";
    public const string DragonFlightStart = "Letět";
    public const string DragonFlightAgain = "Chceš se zase proletět? Drž se pevně!";
    public const string DragonFlightEnd = "Hotovo, přistáváme. Bylo to krásné, viď?";
    public const string DragonFlightSpeech =
        "Hurá, letíme! Podívej se dolů, svítí tam schovaní kamarádi i mince. Jízdou letíme zpátky.";
    public const string DragonHatchedSpeech = "Hapčí! Oheň je zpátky! A podívej, Jiskřička se vylíhla!";

    public static string DragonPart(string part, int stage, int trips) => $"Hnízdo má {part}!  {stage} / {trips}";

    public static string DragonRiddle(int answered, int all) =>
        (all - answered) switch
        {
            _ when answered == 0 => $"Hádanka má {all} otázky. Za každou správnou odpověď se nadechnu víc!",
            1 => "Nááádech! Ještě jedna otázka a kýchnu!",
            int left and < 5 => $"Nádech! Ještě {(left == 2 ? "dvě" : left.ToString())} otázky a kýchnu.",
            int left => $"Nádech! Ještě {left} otázek a kýchnu.",
        };

    public static string DragonHatched(int coins) => $"Drak má zase oheň a vejce se vylíhlo!  +{coins}";

    public static string DragonFirstFlight(int coins) => $"První let na drakovi!  +{coins}";

    // GameUi.Balloon and Game.Balloon
    public const string SignBalloons = "BALÓNY";
    public const string BalloonHint = "Balonářka Amálka";
    public const string BalloonFirst =
        "Chceš se proletět? Plynem zapneš hořák a balón stoupá, couváním pustíš vzduch a balón klesá."
        + " Chytej zlaté hvězdičky! Zelená fajfka spustí let.";
    public const string BalloonAgain = "Vítr fouká přesně jak má. Poletíme zase?";
    public const string BalloonFact =
        "Víš, že první cestující v horkovzdušném balónu byli beran, kachna a kohout? Bylo to ve Francii v roce 1783.";
    public const string BalloonFlightStart = "Letět";
    public const string BalloonQuiz = "Vyřeš příklad a já zatím nahřeju vzduch v balónu!";
    public const string BalloonTakeOff = "Pouštím lana! Letíš! Já ti budu mávat ze země.";
    public const string BalloonLand = "Přistát";
    public const string BalloonRooster = "Kikirikí! Kohoutek Kokrháček poletí s tebou, je to přece letec!";
    public const string BalloonControls = "Hořák nahoru, ventil dolů.";

    public static string BalloonMoreStars(int left) =>
        left switch
        {
            1 => "Ještě jedna hvězdička a přiletí kohoutek Kokrháček!",
            < 5 => $"Ještě {left} hvězdičky a přiletí kohoutek Kokrháček. Poletíme?",
            _ => $"Ještě {left} hvězdiček a přiletí kohoutek Kokrháček. Poletíme?",
        };

    public static string BalloonCaught(int caught, int stars) => $"Hvězdičky: {caught} z {stars}";

    public static string BalloonStar(int caught, int stars) => $"Hvězdička!  {caught} / {stars}  +1";

    public static string BalloonAllStars(int stars, int coins) => $"Všech {stars} hvězdiček!  +{coins}";

    public static string BalloonFirstFlight(int coins) => $"První let balónem!  +{coins}";

    public static string BalloonLanded(int caught, int stars) =>
        caught == stars ? "Přistáli jsme! Máš úplně všechny hvězdičky, to byl let jako pro opravdové balonáře!"
        : caught == 0 ? "Přistáli jsme! Příště zkus vyletět nebo klesnout přímo ke hvězdičce."
        : $"Přistáli jsme! Máš {caught} z {stars} hvězdiček. Krásný let!";

    // GameUi.Tracking and Game.Tracking
    public const string TrackingWanted =
        "Naučím tě číst stopy zvířátek. Stopa vede přes celý kraj až k doupěti, a kde jedno zvířátko najdeš, tam začíná stopa dalšího."
        + " Jeď po stopách, zlatě svítí ty, kam jet dál. Když stopu ztratíš, šipka nahoře ti ukáže, kudy vede."
        + " Půjdeš stopovat? Zelená fajfka tě pošle na cestu.";
    public const string TrackingStart = "Jdu stopovat";
    public const string TrackingBookFull =
        "Moje kniha stop je plná! Všechna zvířátka sedí u svých doupat. Teď umíš číst stopy jako opravdoví hajní.";
    public const string TrackingLast = "To byla poslední stopa z knihy! Hajná Božena bude mít radost.";
    public const string TrackingDenHint = "Vylákat zvířátko";
    public const string TrackingHidden = "?";

    public static string TrackingFirst(TrackingAnimal animal) =>
        $"Hned tady za plotem začíná stopa {TrackingPlans.Whose(animal)}. Sleduj ji, schovává se {TrackingPlans.Hideout(animal)}!";

    public static string TrackingFollow(TrackingAnimal animal) =>
        $"Pořád sleduješ stopu {TrackingPlans.Whose(animal)}. Schovává se {TrackingPlans.Hideout(animal)}.";

    public static string TrackingCoax(TrackingAnimal animal) =>
        $"Pšt! Tady {TrackingPlans.Hideout(animal)} někdo je. Vyřeš příklad a zvířátko vyleze!";

    public static string TrackingFound(TrackingAnimal animal, int found, int all) =>
        $"{TrackingPlans.Name(animal)}!  {found} / {all}";

    public static string TrackingBookDone(int coins) => $"Kniha stop je plná!  +{coins}";

    public static string TrackingNext(TrackingAnimal found, TrackingAnimal next) =>
        $"Ahoj, já jsem {TrackingPlans.Name(found)}! Máš mě, moje stopy tě dovedly až sem. Podívej, tady u mého doupěte začíná stopa {TrackingPlans.Whose(next)}!";

    public static string TrackingTrail(TrackingAnimal animal) => $"Stopa {TrackingPlans.Whose(animal)}";

    public static string TrackingBookCount(int found, int all) => $"Vystopováno {found} z {all}";

    // GameUi.Sheep and Game.Sheep
    public const string SheepWanted =
        "V noci byla bouřka a osm mých oveček se vyplašilo a uteklo do všech koutů kraje. Doma mám jen tři. Pomůžeš mi je najít?"
        + " Ovečky mají zvonečky. Dojeď k ovečce a půjde za autem, jen jeď pomalu. Zelená fajfka tě pošle hledat.";
    public const string SheepStart = "Jdu hledat ovečky";
    public const string SheepPenButton = "Zahnat ovečky do ohrady";
    public const string SheepGoLook = "Děkuju! Šipka nahoře ti ukáže nejbližší ovečku. Poslouchej, jak zvoní zvoneček!";
    public const string SheepPenQuestion = "Vyřeš příklad a ovečky skočí přes ohradu!";
    public const string SheepAllHome =
        "Celé moje stádo je doma! Všechny ovečky jsou v ohradě a pejsek Alík teď půjde s tebou.";
    public const string SheepLeftBehind = "Bé! Jedna ovečka nestačila a zůstala pozadu. Vrať se pro ni!";
    public const string SheepSlowDown = "Pomalu, ovečky nestačí!";
    public const string SheepTakeHome = "Odveď ovečky k Bohoušovi.";
    public const string SheepFindMore = "Najdi další, nebo je odveď k Bohoušovi.";
    public const string SheepListen = "Ovečka se pase tímhle směrem.";

    /// <summary>"1 ovečka", "3 ovečky", "5 oveček".</summary>
    public static string SheepCount(int count) =>
        count switch
        {
            1 => "1 ovečka",
            >= 2 and <= 4 => $"{count} ovečky",
            _ => $"{count} oveček",
        };

    public static string SheepBrought(int following) =>
        following == 1
            ? "Jé, ovečka se vrátila! Zaženeme ji do ohrady? Zmáčkni zelenou fajfku."
            : $"Jé, ovečky se vrátily! Za autem: {SheepCount(following)}. Zaženeme je do ohrady? Zmáčkni zelenou fajfku.";

    public static string SheepStillLost(int lost) => $"Venku jsou ještě ovečky: {lost}. Poslouchej zvonečky!";

    public static string SheepThanks(int lost) =>
        lost == 0 ? "Děkuju! Už jsou skoro všechny doma." : $"Děkuju! Venku jsou ještě ovečky: {lost}.";

    public static string SheepJoined(string name, int following) =>
        following == 1 ? $"Bé! {name} jde za tebou." : $"Bé! {name} jde za tebou. Za autem: {SheepCount(following)}";

    public static string SheepGoBack(string name) => $"Ovečka {name} čeká pozadu. Vrať se pro ni!";

    public static string SheepPenned(int penned, int home, int all) => $"Hop, hop, přes ohradu! Doma {home} / {all}";

    public static string SheepFlockDone(int coins) => $"Celé stádo je doma!  +{coins}";

    public static string SheepHome(int home, int all) => $"Doma {home} / {all}";

    public static string SheepBehind(int following) => following == 0 ? "" : $"Za autem {following}";

    // GameUi.Rally and Game.Rally
    public const string RallyWanted =
        "Děkuju za dopis! Pořádám rallye kolem celého kraje. Má čtyři etapy, od jedné cesty k hranici k další."
        + " Na trati jsou branky se třemi odpověďmi. Projeď tou správnou!";
    public const string RallyAgain =
        "Ahoj, závodníku! Kterou etapu pojedeš? Zelená fajfka u etapy tě pošle na start, červený křížek okno zavře.";
    public const string RallyAllGold = "Samé zlato! Jsi nejlepší řidič kraje. Šplouchalka tě bude ráda doprovázet.";
    public const string RallyGo = "Start! Jeď po okruhu a dávej pozor na branky.";
    public const string RallyAbandoned = "Sjel jsi z trati. Etapu můžeš jet znovu, Vendula na tebe počká.";
    public const string RallyDriveToStart = "Jeď na start a projeď bránou.";
    public const string RallyPickLane = "Projeď pruhem se správnou odpovědí!";
    public const string RallyToGate = "Jeď k další brance.";
    public const string RallyToFinish = "Ještě kousek do cíle!";
    public const string RallyRight = "Správně!  +1";
    public const string RallyBestTime = "Nový nejlepší čas!";
    public const string RallyOtter = "Samé zlato! Vydra Šplouchalka teď pojede s tebou.";
    public const string RallyCompare = "Porovnej čísla.";

    public static string RallyToStart(string stage) =>
        $"Hurá do závodu! Jeď na start etapy {stage}. Šipka ti ukáže cestu.";

    public static string RallyQuestion(string instruction) => $"Otázka! {instruction}";

    public static string RallyLetter(string letter) => $"Co začíná na písmeno {letter}?";

    public static string RallyReading(string word) => $"Přečti slovo {word}.";

    public static string RallyWrong(string answer, int seconds) => $"Škoda, správně bylo {answer}.  +{seconds} s";

    public static string RallyMedalName(RallyMedal medal) =>
        medal switch
        {
            Buildings.RallyMedal.Gold => "zlato",
            Buildings.RallyMedal.Silver => "stříbro",
            Buildings.RallyMedal.Bronze => "bronz",
            _ => "",
        };

    public static string RallyFinished(string stage, string time, RallyMedal medal, int right, int gates) =>
        $"Cíl! {stage}: {time}  ·  {RallyMedalName(medal)}  ·  správně {right} / {gates}";

    public static string RallyMedalSpoken(RallyMedal medal) =>
        medal switch
        {
            Buildings.RallyMedal.Gold => "Cíl! Výborně, máš zlatou medaili!",
            Buildings.RallyMedal.Silver => "Cíl! Máš stříbrnou medaili. Zkusíš příště zlatou?",
            _ => "Cíl! Máš bronzovou medaili. Příště to půjde rychleji!",
        };

    public static string RallyBetterMedal(int coins) => $"Nová medaile!  +{coins}";

    public static string RallyLap(int coins) => $"Objel jsi celý kraj!  +{coins}";

    public static string RallyStage(int number, string name) => $"{number}. {name}";

    /// <summary>A stage's green check mark read aloud: which stage it is and the medal it has, if any.</summary>
    public static string RallyStageSpoken(int number, string name, RallyMedal medal) =>
        $"Etapa {number}, {name.ToLowerInvariant()}. "
        + medal switch
        {
            Buildings.RallyMedal.Gold => "Máš zlatou medaili.",
            Buildings.RallyMedal.Silver => "Máš stříbrnou medaili.",
            Buildings.RallyMedal.Bronze => "Máš bronzovou medaili.",
            _ => "Tuhle etapu jsi ještě nejel.",
        };

    // GameUi.Explorer and Game.Explorer
    public const string SignMaps = "MAPY";
    public const string SignShells = "MUŠLE";
    public const string SignBirds = "PTÁCI";
    public const string CartographerHint = "Sova kartografka Mapka";
    public const string ExplorerWanted =
        "Za starým údolím je osm divů krajiny. Projeď kolem nich a pak se vrať, já je zakreslím do mapy. Zmáčkni zelenou fajfku a já ti ukážu cestu k nejbližšímu.";
    public const string ExplorerMapFull = "Hú hú! Moje mapa je celá. Jsi ten nejlepší průzkumník!";
    public const string ExplorerRecordQuestion = "Vyřeš příklad a já zatím namočím pero do inkoustu!";
    public const string ExplorerRecordButton = "Zakreslit divy do mapy";
    public const string ExplorerGuideButton = "Ukaž mi cestu k nejbližšímu divu";
    public const string ExplorerMap = "Mapa údolí";

    public static string ExplorerSeen(int seen) =>
        seen == 1
            ? "Hú! Viděl jsi nový div krajiny! Zakreslíme ho do mapy? Zmáčkni zelenou fajfku."
            : $"Hú! Viděl jsi nové divy krajiny: {seen}. Zakreslíme je do mapy? Zmáčkni zelenou fajfku.";

    public static string ExplorerStillHidden(int hidden) =>
        $"Divů, které jsi ještě neviděl: {hidden}. Zmáčkni zelenou fajfku a já ti ukážu cestu.";

    public static string ExplorerGo(string name) => $"{name}! Šipky na silnici tě k němu dovedou. Hú hú!";

    public static string ExplorerSpotted(string name) => $"Div krajiny: {name}! Řekni to sově Mapce.";

    public static string ExplorerRecorded(string[] names, int recorded, int all) =>
        $"V mapě: {string.Join(", ", names)}  {recorded} / {all}";

    public static string ExplorerMapDone(int coins) => $"Mapa je celá!  +{coins}";

    public static string ExplorerThanks(int left) => $"Děkuju! Ještě chybí divy: {left}. Najdeš je?";

    public static string ExplorerOnMap(int recorded, int all) => $"V mapě {recorded} / {all}";

    // GameUi.Shells and Game.Shells
    public const string ShellsLowTide = "Odliv! Moře ustoupilo a nechalo na písku mušle. Sbírej je, než se voda vrátí!";
    public const string ShellsWait = "Počkej na odliv";
    public const string ShellsCrab = "Máš všech osm druhů! Krab Klepetko z pláže teď půjde s tebou.";
    public const string ShellsHud = "Mušle";

    public static string ShellPicked(string name, int count) => $"{name}  {count}×  +1";

    public static string ShellNewKind(string name, int found, int all, int coins) =>
        $"Nový druh: {name}!  {found} / {all}  +{coins}";

    public static string ShellNewKindSpoken(string name) => $"Nový druh mušle! {name}.";

    public static string ShellsAllKinds(int coins) => $"Všechny druhy mušlí!  +{coins}";

    public static string ShellsKinds(int found, int all) => $"Druhy mušlí: {found} z {all}";

    // GameUi.Ferry and Game.Ferry
    public const string BirdwatcherHint = "Volavka pozorovatelka Klárka";
    public const string FerryWanted =
        "Převoznice Vendula mi půjčí svou starou pramici, ale je celá rozbitá. Přivezeš mi klády a prkna? Postavím molo a novou pramici.";
    public const string FerryBuiltSpeech =
        "Pramice je hotová! Až budeš chtít, poplujeme po řece až do delty na ptačí ostrůvek.";
    public const string FerryReady = "Pramice je připravená. Poplujeme na ptačí ostrůvek? Zmáčkni zelenou fajfku.";
    public const string FerrySailQuestion = "Než odrazím, vyřeš úlohu!";
    public const string FerrySailSpeech = "Držte se! Plujeme po proudu až k moři.";
    public const string FerryHomeSpeech = "Tak domů! Pluju proti proudu k přístavišti.";
    public const string FerryHomeEnd = "Jsme doma. Přijeď zase, ptáci se pořád střídají!";
    public const string FerryIslandHello =
        "Jsme na ostrůvku! V rákosí kolem se schovávají ptáci. Vylez do pozorovatelny a najdi je dalekohledem.";
    public const string FerryIslandAllSeen = "Všechny ptáky z dnešní plavby už máš v atlasu. Poplujeme domů?";
    public const string FerryTelescopeHow =
        "Otáčej dalekohledem volantem nebo myší. Když je pták uprostřed kroužku, chvilku počkej. Pedál dalekohled zavře.";
    public const string FerryAtlasFull = "Atlas je plný! Ale ptáci se rádi vracejí, poplujeme se zase podívat?";
    public const string FerryTelescopeButton = "Dalekohled";
    public const string FerryHomeButton = "Plout domů";
    public const string FerrySailButton = "Plout na ostrůvek";

    public static string FerrySailAgain(int missing) =>
        $"V atlasu ještě chybí {missing} {(missing == 1 ? "druh" : missing < 5 ? "druhy" : "druhů")} ptáků. Poplujeme na ostrůvek?";

    public static string FerryPart(string part, int stage, int all) => $"Hotovo: {part}!  {stage} / {all}";

    public static string FerryBuilt(int coins) => $"Pramice je hotová!  +{coins}";

    public static string FerryNewBird(string name, int found, int all) =>
        $"{name}! Nový pták v atlasu: {found} z {all}";

    public static string FerryBirdAgain(string name) => $"{name}! Zase jeden do atlasu.";

    public static string FerryAtlasDone(int coins) => $"Atlas je plný! Ledňáček Modroušek teď půjde s tebou.  +{coins}";

    public static string FerryTelescopeSeen(int seen, int all) => $"Dalekohled. Ptáci: {seen} z {all}";

    // GameUi.Castle and Game.Castle
    public const string CastellanHint = "Lvice kněžna Libuše";
    public const string CastleWanted =
        "Na hrad potřebuju kamení od moře i z hor, štěrk, cihly, prkna, klády a sklo. Přivezeš mi je? Šestnáct nákladů a hrad bude stát.";
    public const string CastleBuiltSpeech =
        "Hrad stojí! Teď ještě hlídky na hranicích. Postavíme u každé hranice strážní věž s ohněm?";
    public const string CastleBeaconsWanted =
        "U každé hranice má stát strážní věž s ohněm. Přivez k ní klády a uvidíme ten oheň až z hradu!";
    public const string CastleBeaconsLit =
        "Všechny ohně hoří! Teď uspořádáme hostinu. Přivezeš mi hosty z celého údolí? Jezdí se pro ně s vozíkem.";
    public const string CastleGuestsWanted = "Ke stolu se ještě nevešli všichni hosté. Pojedeš pro dalšího?";
    public const string CastleFeastSpeech =
        "Všichni hosté sedí u stolu! Ať žije hostina! Dnes v noci bude nad hradem ohňostroj.";
    public const string CastleFeastOver = "Děkuju za krásnou hostinu! Přijeď se v noci podívat na ohňostroj.";
    public const string CastleBringLoad = "Teď odvez náklad před bránu hradu, do žlutého kruhu.";
    public const string CastleBringLogs = "Teď odvez klády ke strážní věži, do žlutého kruhu.";
    public const string CastleBringGuest =
        "Host sedí na lavičce! Opatrně ho odvez na hrad, do žlutého kruhu před bránou.";
    public const string CastleGuestHello = "Jedu na hostinu! Ale nejdřív vyřeš úlohu.";

    public static string CastleFetchGuest(string name) => $"Jeď pro hosta: {name}. Čeká u svého domu.";

    public static string CastlePart(string part, int stage, int all) => $"Hotovo: {part}!  {stage} / {all}";

    public static string CastleBuilt(int coins) => $"Hrad je hotový!  +{coins}";

    public static string BeaconLit(int lit, int all, int coins) =>
        $"Oheň na strážní věži hoří!  {lit} / {all}  +{coins}";

    public static string GuestSeated(string name, int seated, int all, int coins) =>
        $"{name} sedí u stolu!  {seated} / {all}  +{coins}";

    public static string CastleFeast(int coins) => $"Hostina! Strašidýlko Bubáček teď půjde s tebou.  +{coins}";

    // GameUi.MainLine and Game.MainLine
    public const string EngineerHint = "Jezevec strojvůdce Kuba";
    public const string LineWanted =
        "Postavíme velkou trať přes celé údolí! Nádraží, a pak koleje k horám, k moři i k řece. Potřebuju štěrk, prkna, cihly a sklo, a koleje ukuje kovárna z kamení.";
    public const string LineBringLoad = "Teď odvez náklad na konec trati, k oranžové vlajce.";
    public const string LineBringStation = "Teď odvez náklad před nádraží, do žlutého kruhu.";
    public const string LineBringStone = "Teď odvez kamení do kovárny, do žlutého kruhu.";
    public const string LineFetchRails = "Jeď do kovárny pro koleje. Čekají tam na hromadě.";
    public const string LineRailsMissing = "Koleje ještě nejsou hotové. Kovárna je ukuje z kamení, tak ho tam zavez!";
    public const string LineForgedSpeech =
        "Kovárna kuje! Za chvilku budou koleje hotové. Pro koleje pojedeš zase s vozíkem.";
    public const string LineHubReady = "Nádraží stojí! Teď pojedeme k horám, pak k moři a nakonec k řece.";
    public const string LineAllDone = "Celá trať je hotová! Svezu tě, kam budeš chtít.";
    public const string LineRideAsk = "Jízdenka je úloha. Vyřeš ji a svezu tě.";
    public const string LineHomeSpeech = "Jedeme zpátky na velké nádraží! Držte se.";
    public const string LineHomeEnd = "Jsme na velkém nádraží. Svezeme se zase někdy?";
    public const string LineRideButton = "Svézt se vlakem";
    public const string LineHomeButton = "Zpátky na nádraží";
    public const string LineForgeButton = "Kamení do kovárny";

    public static string LineRideSpeech(int arm) =>
        $"Nastupovat! Jedeme {MathRacer.Buildings.MainLinePlans.ArmName(arm)}. Píííp!";

    public static string LineRideEnd(int arm) =>
        $"Jsme na konečné {MathRacer.Buildings.MainLinePlans.ArmName(arm)}. Odtud se dá jet autem, kam chceš, a zpátky se svezeš ode mě.";

    public static string LinePart(string part, int stage, int all) => $"Hotovo: {part}!  {stage} / {all}";

    public static string LineForged(int rails) => $"Kovárna ukovala koleje!  {rails}";

    public static string LineHubDone(int coins) => $"Velké nádraží stojí!  +{coins}";

    public static string LineArmDone(int arm, int coins) =>
        $"Trať {MathRacer.Buildings.MainLinePlans.ArmName(arm)} je hotová, vlak může jet!  +{coins}";

    public static string LineDone(int coins) =>
        $"Celá velká trať je hotová! Krteček Pražec teď půjde s tebou.  +{coins}";

    // GameUi.Ship and Game.Ship
    public const string CaptainHint = "Mrož kapitán Vojta";
    public const string ShipWanted =
        "Postavíme loď Albatros! Kýl a žebra z klád, trup a palubu z prken, okna ze skla, stěžně z klád a plachty z látky. A kotvu ukuje kovárna u velké trati z kamení.";
    public const string ShipBringLoad = "Teď odvez náklad do loděnice, do žlutého kruhu.";
    public const string ShipBringStone = "Teď odvez kamení do kovárny, do žlutého kruhu. Ukuje z něj kotvu.";
    public const string ShipFetchAnchor = "Jeď do kovárny pro kotvu. Leží tam a čeká.";
    public const string ShipAnchorMissing = "Kotva ještě není ukovaná. Kovárna ji ukuje z kamení, tak ho tam zavez!";
    public const string ShipAnchorForgedSpeech = "Kovárna kuje kotvu! Až bude hotová, přivez mi ji s vozíkem.";
    public const string ShipAnchorForged = "Kovárna ukovala kotvu!";
    public const string ShipLaunchSpeech = "Albatros je hotový! Pozor, spouštíme ho na vodu!";
    public const string ShipSailWanted =
        "Albatros pluje! Poplujeme na Želví ostrov. Kormidluješ ty: zatáčíš jako s autem, plyn nafoukne plachty a brzda je stáhne.";
    public const string ShipSailAsk = "Než vyplujeme, vyřeš úlohu.";
    public const string ShipVoyageSpeech =
        "Vyplouváme! Jeď kolem bójí, každá zazvoní. Když nebudeš kormidlovat, loď pluje k další bóji sama.";
    public const string ShipIslandHello =
        "Jsme na Želvím ostrově! Na pláži sedí želvy na pokladech. Zastav u želvy a vyřeš její úlohu.";
    public const string ShipIslandSpeech = "Až budeš chtít domů, zmáčkni loď a poplujeme zpátky.";
    public const string ShipHomeSpeech = "Plujeme domů do loděnice!";
    public const string ShipHomeEnd = "Jsme doma v loděnici. Poplujeme zase někdy?";
    public const string ShipTurtleAsk = "Želva ti dá poklad, když vyřešíš úlohu.";
    public const string ShipSailButton = "Plout na Želví ostrov";
    public const string ShipHomeButton = "Plout domů";
    public const string ShipForgeButton = "Kamení do kovárny";

    public static string ShipPart(string part, int stage, int all) => $"Hotovo: {part}!  {stage} / {all}";

    public static string ShipDone(int coins) => $"Loď Albatros je hotová a pluje!  +{coins}";

    public static string ShipTreasure(string name, bool first, int coins) =>
        first ? $"Nový poklad do kajuty: {name}!  +{coins}" : $"Poklad do kajuty: {name}!  +{coins}";

    public static string ShipCabinFull(int coins) =>
        $"Kajuta je plná pokladů! Kocour Kormidlík teď sedí na přídi.  +{coins}";
}
