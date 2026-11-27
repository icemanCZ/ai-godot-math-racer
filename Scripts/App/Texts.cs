using MathRacer.Buildings;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The fixed Czech texts of the game: menus, hints, what the characters say, the names of the postcards. A text lives
/// here instead of in the code that shows it, so the language can be proofread in one file and the code says what
/// happens, not what is written. The names are made from the text itself (without diacritics), which keeps them unique;
/// a text that is edited may keep its name. Texts with numbers or names inside are methods. The texts of data tables
/// (the catalogue of characters, the works, the village, the word lists of the questions) stay beside the colours and
/// numbers of their entry.
/// </summary>
internal static class Texts
{
    // Game.Bandits
    public const string StujPocestnyVyresTriUkolyNebo = "Stůj, pocestný! Vyřeš tři úkoly, nebo tě dál nepustíme!";

    // Game.Events
    public const string UkoncitJizdu = "Ukončit jízdu";
    public const string ZahajitJizdu = "Zahájit jízdu";

    // Game.Ford
    public const string ZboziJesteNeniVyrobenePosluTe = "Zboží ještě není vyrobené. Pošlu tě pro surovinu!";
    public const string PrivezesMiDalsiNaklad = "Přivezeš mi další náklad?";
    public const string ZboziDosloZkusToZnovuPozdeji = "Zboží došlo. Zkus to znovu později.";
    public const string DekujuAzBudesChtitPrijedZa = "Děkuju! Až budeš chtít, přijeď za mnou pro další náklad.";
    public const string MostJeHotovyVozikJeTvuj = "Most je hotový! Vozík je tvůj.";
    public const string HuraMostJeHotovyTedMuzes =
        "Hurá, most je hotový! Teď můžeš přejet řeku. Vozík si nech, najdeš ho v garáži.";
    public const string HuraJeHotovoMocDekuju = "Hurá, je hotovo! Moc děkuju.";
    public const string TedOdvezNakladNaDruhyBreh = "Teď odvez náklad na druhý břeh řeky. Přejeď po velkém mostě.";
    public const string TedOdvezNakladNaStavbuA = "Teď odvez náklad na stavbu a vysyp ho do žlutého kruhu.";
    public const string TedOdvezNakladPredDomecekA = "Teď odvez náklad před domeček a vysyp ho do žlutého kruhu.";
    public const string TedOdvezNakladNaParceluA = "Teď odvez náklad na parcelu a vysyp ho do žlutého kruhu.";
    public const string TedOdvezNakladDoZlutehoKruhu = "Teď odvez náklad do žlutého kruhu před závodem.";
    public const string JedSVozikemProKameniDo = "Jeď s vozíkem pro kamení do lomu.";
    public const string JedSVozikemProHlinu = "Jeď s vozíkem pro hlínu.";
    public const string JedSVozikemProKladyNa = "Jeď s vozíkem pro klády na skládku.";
    public const string JedSVozikemProPrknaNa = "Jeď s vozíkem pro prkna na pilu.";
    public const string JedSVozikemProCihlyDo = "Jeď s vozíkem pro cihly do cihelny.";
    public const string JedSVozikemProSterkDo = "Jeď s vozíkem pro štěrk do štěrkovny.";
    public const string JedSVozikemProLatkuK = "Jeď s vozíkem pro látku k farmáři Tondovi.";
    public const string JedSVozikemProKamenKe = "Jeď s vozíkem pro kámen ke sběrateli drahokamů.";
    public const string JedSVozikemProVlajeckuK = "Jeď s vozíkem pro vlaječku k hračkáři Pepovi.";
    public const string JedSVozikemProPisek = "Jeď s vozíkem pro písek.";

    // Game.Lookout
    public const string PodivejSeCoVsechnoJeV =
        "Podívej se, co všechno je v údolí! Zlaté sloupy ukazují mince a modré kamarády.";

    public const string RozhlizejSeMysi = "Táhni myší a rozhlížej se, jak dlouho chceš. Až budeš chtít jet, rozjeď se.";

    public const string StopyKol = "Stopy kol";

    // Game.Hidden: the small friends who hide beside the roads
    public static string HiddenName(HiddenKind kind) =>
        kind switch
        {
            HiddenKind.Ladybug => "Beruška",
            HiddenKind.Hedgehog => "Ježek",
            _ => "Žabka",
        };

    public static string Pst(string name) => $"Pšt! {name}!";

    /// <summary>The button under the screen when a friend is close enough to be found.</summary>
    public static string HiddenTake(HiddenKind kind) =>
        kind switch
        {
            HiddenKind.Ladybug => "Vzít berušku",
            HiddenKind.Hedgehog => "Vzít ježka",
            _ => "Vzít žabku",
        };

    public static string HiddenFoundSpeech(string name, int count, int total) =>
        $"Pšt! Tady je {name}! {count}/{total}";

    public static string HiddenAllOfKind(HiddenKind kind, int total) =>
        kind switch
        {
            HiddenKind.Ladybug => $"Našel jsi všech {total} berušek!",
            HiddenKind.Hedgehog => $"Našel jsi všech {total} ježků!",
            _ => $"Našel jsi všech {total} žabek!",
        };

    public static string PodivejSeKdoSeSchovava(int remaining) =>
        $"Podívej se z věže! Oranžové sloupy ukazují, kde se schovávají kamarádi. Ještě jich hledáš {remaining}.";

    public static string Souhvezdi(string name, int stars) =>
        $"Podívej, hvězdy! Souhvězdí {name}.\nSpočítej si je: má {stars} hvězdiček.";

    // Game.Plots
    public const string MocDekujuZaDomecekBydliSe = "Moc děkuju za domeček! Bydlí se mi tu skvěle.";
    public const string PrivezesMiDalsiMaterial = "Přivezeš mi další materiál?";
    public const string DekujuAzBudesChtitPrivezDalsi = "Děkuju! Až budeš chtít, přivez další materiál.";
    public const string HuraDomecekJeHotovyTedTu = "Hurá, domeček je hotový! Teď tu budu bydlet. Moc děkuju!";
    public const string TedOdvezNakladNaHristeA = "Teď odvez náklad na hřiště a vysyp ho do žlutého kruhu.";
    public const string MocDekujuZaHristeDetiSeSmeji = "Moc děkuju za hřiště! Děti si hrají od rána do večera.";
    public const string HuraHristeJeHotove = "Hurá, hřiště je hotové! Děti se můžou vyřádit! Moc děkuju!";
    public const string HristeHint = "Hřiště";
    public const string TedOdvezNakladNaNadraziA = "Teď odvez náklad na nádraží a vysyp ho do žlutého kruhu.";
    public const string VlacekJezdiDekujuZaNadrazi = "Vláček jezdí od rána do večera! Moc děkuju za nádraží.";
    public const string HuraNadraziJeHotove = "Hurá, nádraží je hotové! Vláček veze zvířátka! Moc děkuju!";
    public const string NadraziHint = "Nádraží";
    public const string TedOdvezNakladDoZooA = "Teď odvez náklad do zoo a vysyp ho do žlutého kruhu.";
    public const string ZvirataciJsouSpokojenaDekujuZaZoo = "Zvířátka jsou tu spokojená! Moc děkuju za zoo.";
    public const string HuraZooJeHotova = "Hurá, zoo je hotová! Všechna zvířátka mají svůj domov! Moc děkuju!";
    public const string ZooHint = "Zoo";

    // Game.Postcards
    public const string MostALekniny = "Most a lekníny";
    public const string UPrijezduKMostu = "U příjezdu k mostu";
    public const string DomeckyPodKopcem = "Domečky pod kopcem";
    public const string NaSilniciNadVesnici = "Na silnici nad vesnicí";
    public const string GarazUCesty = "Garáž u cesty";
    public const string PoblizGaraze = "Poblíž garáže";
    public const string ZahradaPlnaKvetin = "Zahrada plná květin";
    public const string UZahradnictvi = "U zahradnictví";
    public const string LoukaULesniCesty = "Louka u lesní cesty";
    public const string ULucnichKvetin = "U lučních květin";
    public const string VyfotimePohlednici = "Vyfotíme pohlednici.";
    public const string PohledniceMaRazitko = "Pohlednice má razítko.";
    public const string VGaraziMasSamolepkuMapkaUdoli = "V garáži máš samolepku Mapka údolí!";

    // Game.Speech
    public const string NejprveVybralaObrazekPodlePrvnihoPismene = "nejprve vybrala obrázek podle prvního písmene";
    public const string NejprvePrecetlaSlovoAVybralaObrazek = "nejprve přečetla slovo a vybrala obrázek";
    public const string SpocitalaNaklad = "spočítala náklad";
    public const string SpocitalaKolikSeTohoJesteVejde = "spočítala, kolik se toho ještě vejde do vozíku";
    public const string NejprveVyresilaPriklad = "nejprve vyřešila příklad";
    public const string NejprvePorovnalaCisla = "nejprve porovnala čísla";
    public const string AjajToNeniSpravneZkusTenhle = "Ajaj, to není správně. Zkus tenhle.";
    public const string AjajToNeniSpravneZkusToZnovu = "Ajaj, to není správně. Zkus to znovu.";
    public const string PrivezSestDilkuGolema = "přivez šest dílků golema";
    public const string PrivezTriJablka = "přivez tři jablka";
    public const string JedenKusPeciva = "jeden kus pečiva";
    public const string TytoDvaKusyPeciva = "tyto dva kusy pečiva";
    public const string TytoTriKusyPeciva = "tyto tři kusy pečiva";
    public const string JedenBalicek = "jeden balíček";
    public const string TytoDvaBalicky = "tyto dva balíčky";
    public const string TytoTriBalicky = "tyto tři balíčky";
    public const string JedenKolacek = "jeden koláček";
    public const string TytoDvaKolacky = "tyto dva koláčky";
    public const string TytoTriKolacky = "tyto tři koláčky";
    public const string TytoDveMrkve = "tyto dvě mrkve";
    public const string TytoTriMrkve = "tyto tři mrkve";
    public const string TatoDveJablka = "tato dvě jablka";
    public const string TatoTriJablka = "tato tři jablka";
    public const string TytoDveSkleniceMedu = "tyto dvě sklenice medu";
    public const string TytoTriSkleniceMedu = "tyto tři sklenice medu";

    // Game.Trades
    public const string ObjednavkaDorucenaDekujeme = "Objednávka doručena! Děkujeme!";

    // Game.Village
    public const string PrivedMiTenNakladKDomecku = "Přiveď mi ten náklad k domečku!";

    // Game.Weather
    public const string TedProsimVyberPocasi = "Teď prosím vyber počasí.";

    // Game.Works
    public const string SkladJePlnyDekuju = "Sklad je plný. Děkuju!";
    public const string SkladZavoduJePlnyZkusTo = "Sklad závodu je plný. Zkus to později.";
    public const string SkladJePlnyDekuju2 = "Sklad je plný, děkuju!";
    public const string DekujuAzBudesChtitPrivezDalsi2 = "Děkuju! Až budeš chtít, přivez další náklad.";

    // Game
    public const string TedSiProsimVyberRozvozNebo = "Teď si prosím vyber rozvoz nebo dovoz.";
    public const string DorucenoDekujemeZaPomoc = "Doručeno! Děkujeme za pomoc!";
    public const string NakladJeNaAuteTedHo = "Náklad je na autě. Teď ho dovez zpátky.";

    // GameUi.Coins
    public const string ZaPenizkySiVGaraziKoupis = "Za penízky si v garáži koupíš nová vylepšení.";

    // GameUi.Events
    public const string EMezernik = "E / mezerník";

    // GameUi.Ford
    public const string Mlynar = "Mlynář";
    public const string Hvezdar = "Hvězdář";
    public const string Rybar = "Rybář";
    public const string ZaprahnoutVozik = "Zapřáhnout vozík";
    public const string ZaprahnoutVozikProSurovinu = "Zapřáhnout vozík pro surovinu";
    public const string Vozik = "Vozík";
    public const string PostavSBobremBodouMostA = "Postav s bobrem Bóďou most a vozík je tvůj.";
    public const string VozikZatimNeziskan = "Vozík · zatím nezískán";
    public const string VozikPripojeny = "✓ Vozík připojený";
    public const string PripojitVozik = "Připojit vozík";

    // GameUi.Garage
    public const string VyjetZGaraze = "Vyjet z garáže";
    public const string Garaz = "Garáž";
    public const string TypyUkoluAlesponJedenZapnuty = "Typy úkolů · alespoň jeden zapnutý";
    public const string Pismenka = "Písmenka";
    public const string Cteni = "Čtení";
    public const string Priklady = "Příklady";
    public const string Porovnavani = "Porovnávání";
    public const string UrovenPrikladu = "Úroveň příkladů";
    public const string N1DveCisla = "1 · Dvě čísla";
    public const string N2TriCisla = "2 · Tři čísla";
    public const string AlesponJedenTypUkoluMusiZustat = "Alespoň jeden typ úkolů musí zůstat zapnutý.";
    public const string TutoOdmenuZiskasZaZvlastniUkol = "Tuto odměnu získáš za zvláštní úkol.";
    public const string Mazlicci = "Mazlíčci";
    public const string ZatimNeziskan = "Zatím nezískán";
    public const string MalyGolem = "Malý golem";
    public const string CernokneznikBaltazar = "Černokněžník Baltazar";
    public const string Doplnky = "Doplňky";

    // GameUi.Golem
    public const string ACernokneznik = "A · Černokněžník";
    public const string ECernokneznik = "E · Černokněžník";
    public const string VyrazitProDilky = "Vyrazit pro dílky";

    // GameUi.Hints
    public const string AGaraz = "A · Garáž";
    public const string EGaraz = "E · Garáž";
    public const string ADomecek = "A · Domeček";
    public const string EDomecek = "E · Domeček";
    public const string AZahradnik = "A · Zahradník";
    public const string EZahradnik = "E · Zahradník";
    public const string Hrackar = "Hračkář";
    public const string SberatelDrahokamu = "Sběratel drahokamů";
    public const string ZahradnikVitek = "Zahradník Vítek";
    public const string FarmarTonda = "Farmář Tonda";

    // GameUi.Houses
    public const string ZaprahnoutVozikProMaterial = "Zapřáhnout vozík pro materiál";

    // GameUi.Input
    public const string WasdEMezernikREscIi = "WASD / ↑↓←→    E / mezerník · ?    R · ↶    Esc · Ⅱ";

    // GameUi.Mayor and Game.Mayor
    public const string AStarostka = "A · Starostka";
    public const string EStarostka = "E · Starostka";
    public const string DekoraceDoMesta = "Dekorace do města";
    public const string Koupeno = "✓ Máme";
    public const string DekujiHnedToOzdobime = "Děkuji! Hned to ozdobíme.";
    public const string CoSiPriVybrat = "Co se ti líbí? Za penízky ozdobíme celé město!";
    public const string MestoJeKrasneDekujeme = "Naše město je krásné! Děkujeme ti!";

    // GameUi.Glassmaker and Game.Lamps
    public const string ASvetlar = "A · Světlař";
    public const string ESvetlar = "E · Světlař";
    public const string SklenenaSvetla = "Skleněná světla";
    public const string Vzit = "Vzít si";
    public const string JakeSvicticeSiVyberes = "Která se ti líbí? Postav ji, kam chceš. Večer se sama rozsvítí!";
    public const string TadyMasPostavJi = "Tady máš! Postavíš ji tam, kde se ti bude líbit.";
    public const string UzMasSpoustuLampSeberNejakou = "Už jich máš v údolí spoustu! Nějakou seber.";
    public const string SemLampuPostavitNejde = "Sem lampu postavit nejde.";
    public const string LampaStojiSeberesJi = "Lampa stojí. Až budeš u ní, zase ji sebereš.";
    public const string VakJePlny = "Vak je plný. Nejdřív nějakou postav.";
    public const string SebratLampu = "Sebrat";
    public const string PostavitLampu = "Postavit lampu";

    // Game.Glassmaker: the glasses the lamp maker blows his lamps from
    public const string SklenickyNaNoveLampy = "Skleničky ze sklárny na nové lampy";
    public const string ZaprahnoutVozikProSklenicky = "Zapřáhnout vozík pro skleničky";
    public const string PrivezesMiSklenickyZeSklarny = "Ze skleniček foukám svíčky a lampy. Přivezeš mi je ze sklárny?";
    public const string SklarnaNemaSklenickyPoslusTePro = "Ve sklárně skleničky došly. Pošlu tě pro písek!";
    public const string TedOdvezSklenickyNaNaves = "Teď odvez skleničky na náves a vysyp je do žlutého kruhu u kašny.";
    public const string ZTvychSklenicekVyfouknuLampy = "Děkuju! Z tvých skleniček vyfouknu nové lampy.";

    public static string GlassmakerPaid(int coins) => $"Skleničky pro světlaře!  +{coins}";

    public static string LampsBag(int carried, int max) => $"Ve vaku: {carried} / {max}";

    public static string LampsOut(int placed, int max) => $"Venku: {placed} / {max}";

    public static string LampsHud(int carried, string next, bool controller) =>
        $"Lampy ve vaku: {carried} · další: {next}\n{(controller ? "A" : "E")} · postavit";

    public static string LampInBag(string name) => $"{name} je ve vaku.";

    // GameUi.Treasure and Game.Treasure
    public const string TreasureHint = "Poklad";
    public const string TreasureDig = "Kopat!";
    public const string TreasureStart = "Vyrazit na poklad";
    public const string TreasureStillLooking = "Poklad už hledáš! Jeď podle detektoru: čím blíž, tím rychleji pípá.";
    public const string TreasureAnotherOne =
        "Skvělé! Mám pro tebe další poklad. Zakopala jsem ho jinde. Půjdeš hledat?";
    public const string TreasureGoodLuck = "Hodně štěstí! Poslouchej pípání a sleduj, jestli je ti teplo.";

    public static string TreasuresFound(int count) => $"Nalezené poklady: {count}";

    public static string TreasureBigOne(int left) =>
        left == 1 ? "Další truhla je velká!" : $"Do velké truhly zbývá {left}.";

    public static string TreasureWarmthWord(TreasureWarmth warmth) =>
        warmth switch
        {
            TreasureWarmth.Freezing => "Studeno",
            TreasureWarmth.Cold => "Chladno",
            TreasureWarmth.Warm => "Teplo",
            TreasureWarmth.Hot => "Horko!",
            TreasureWarmth.Burning => "Pálí!",
            _ => "Kopej!",
        };

    private static readonly string[] Contents =
    {
        "zlatá korunka",
        "zářivý rubín",
        "zlatá kachnička",
        "perlový náhrdelník",
        "starý kompas",
        "kouzelná mušle",
    };

    public static string TreasureContents(int index) => Contents[index % Contents.Length];

    public static string TreasureFound(string contents, int coins) => $"Poklad! V truhle je {contents}.  +{coins}";

    // GameUi.Fireflies and Game.Fireflies
    public const string FireflyStart = "Chytat světlušky";
    public const string FireflyChangeWeather = "Změnit počasí";
    public const string FireflyWelcome =
        "Je tu hvězdná noc! Půjčím ti lucernu. Jeď za blikajícími světluškami a chytej je.";
    public const string FireflyNeedNight = "Světlušky se ukážou jen v noci. Až budu čarovat hvězdnou noc, vrať se.";
    public const string FireflyStillOn = "Lucerna už svítí! Jeď za světluškami. Čím víc jich chytíš, tím víc září.";
    public const string FireflyGoodLuck = "Hezky pomalu a klidně. Světlušky blikají, jeď za nimi!";
    public const string FireflyMorning = "Je ráno, světlušky šly spát. Lucerna je zpátky u čarodějnice.";
    public const string FireflyComplete =
        "Všechny hvězdy svítí! Máš celou sbírku souhvězdí. Děkuju, že jsi mi pomáhal.";
    public const string FireflyCollectionComplete = "Všechna souhvězdí už svítí na obloze!";
    public const string FireflyLantern = "Lucerna";

    public static string FireflyStars(int lit, int total) => $"Hvězdy ve sbírce: {lit} / {total}";

    public static string FireflyCounter(int caught, int perStar) => $"{caught} / {perStar}";

    public static string FireflyNewStar(int lit, int total) =>
        $"Na obloze se rozsvítila nová hvězda! Máš jich {lit} z {total}.";

    public static string FireflyPictureDone(string name) => $"Souhvězdí {name} je hotové! Podívej se na oblohu.";

    // GameUi.Hunter and Game.Hunter
    public const string HunterHint = "Lovec";
    public const string HuntStart = "Vyrazit na lov";
    public const string HuntThrow = "Hodit síť";
    public const string HuntPickUp = "Sebrat síť";
    public const string HunterGoodLuck =
        "Tady máš síť! Zvířátka běhají daleko v přírodě, šipka ti ukáže kam. Přibliž se pomalu, ať se nesplaší, a mezerníkem hoď síť.";
    public const string HunterStillHunting = "Lov už běží! Chyť zvířátko do sítě, nalož ho na auto a přivez mi ho.";
    public const string HunterNewNet = "Ztratila se ti síť? Nevadí, tady máš novou!";
    public const string HunterAnotherOne = "Ohrada se plní! Pojedeš chytit další zvířátko?";
    public const string HunterPetEarned = "Osm zvířátek v ohradě! Koloušek Puntík teď bude jezdit s tebou.";
    public const string HuntMissed = "Vedle! Síť leží v trávě. Dojeď si pro ni a zkus to znovu.";
    public const string HuntNetLost = "Síť spadla do vody! Jeď za lovcem, dá ti novou.";
    public const string HuntNetBack = "Síť je zpátky na autě. Zkus to znovu!";
    public const string HunterHutGreeting =
        "Ahoj, jsem lovec Hubert! Ohradu už mám, ale chybí mi chata. Pomůžeš mi ji postavit? Pak spolu vyrazíme na lov.";
    public const string HunterHutDone = "Hurá, chata stojí a z komína se kouří! Teď můžeme vyrazit na lov.";
    public const string HutUnfinished = "Nejdřív postavíme chatu, potom vyrazíme na lov.";

    public static string HutPart(string part, int stage, int trips) => $"Chata má {part}!  {stage} / {trips}";

    public static string HutDone(int coins) => $"Chata lovce je hotová!  +{coins}";

    public const string HuntPetOwned = "Koloušek Puntík už je tvůj. Ohrada se ale ráda zaplní dál!";

    /// <summary>The animal of the hunt, as the subject of a sentence.</summary>
    public static string PreyName(WildKind kind) =>
        kind switch
        {
            WildKind.Fox => "liška",
            WildKind.Deer => "srnka",
            _ => "zajíček",
        };

    /// <summary>The animal of the hunt, as the object of a sentence.</summary>
    public static string PreyObject(WildKind kind) =>
        kind switch
        {
            WildKind.Fox => "lišku",
            WildKind.Deer => "srnku",
            _ => "zajíčka",
        };

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

    public static string HuntCaught(WildKind kind) =>
        $"Hurá, {PreyName(kind)} je v síti! Dojeď k síti a stiskni mezerník.";

    public static string HuntLoad(WildKind kind) => $"Naložit {PreyObject(kind)}";

    public static string HuntLoaded(WildKind kind) => $"{Capital(PreyName(kind))} je na autě. Teď jeď za lovcem!";

    public static string Animals(int count) =>
        count == 1 ? "1 zvířátko"
        : count is >= 2 and <= 4 ? $"{count} zvířátka"
        : $"{count} zvířátek";

    public static string HuntDelivered(WildKind kind, int count) =>
        $"{Capital(PreyName(kind))} běží do ohrady!  V ohradě je {Animals(count)}.";

    public static string HunterThanks(WildKind kind, int count) =>
        $"Děkuju! {Capital(PreyName(kind))} má v ohradě nový domov. Teď tu mám {Animals(count)}. Až budeš chtít lovit dál, přijeď za mnou.";

    public static string HuntPenned(int count) => $"Zvířátka v ohradě: {count}";

    public static string HuntUntilPet(int left) =>
        left == 1 ? "Ještě jedno zvířátko a dostaneš mazlíčka!"
        : left <= 4 ? $"Do mazlíčka zbývají {Animals(left)}."
        : $"Do mazlíčka zbývá {Animals(left)}.";

    // GameUi.Postcards
    public const string PohledniceZKouzelnehoUdoli = "Pohlednice z Kouzelného údolí";

    // GameUi.Weather
    public const string ACarodejnice = "A · Čarodějnice";
    public const string ECarodejnice = "E · Čarodějnice";
    public const string Dest = "Déšť";
    public const string Vitr = "Vítr";
    public const string Bourka = "Bouřka";
    public const string Vecer = "Večer";
    public const string Noc = "Hvězdná noc";

    // GameUi.Works
    public const string Pole = "Pole";
    public const string Hliniste = "Hliniště";
    public const string Piskovna = "Pískovna";
    public const string Klady = "Klády";

    // GameUi
    public const string MathracerKouzelneUdoli = "MATHRACER  /  Kouzelné údolí";
    public const string KouzelneUdoli = "Kouzelné údolí";
    public const string Pokracovat = "Pokračovat";
    public const string Hrat = "Hrát";
    public const string NovaHra = "Nová hra";
    public const string NovaHraNahradiUlozenyPostup = "Nová hra nahradí uložený postup.";
    public const string StiskniAProUkolAOdmenu = "Stiskni A pro úkol a odměnu";
    public const string StiskniENeboMezernikProUkol = "Stiskni E nebo mezerník pro úkol a odměnu";
    public const string TotoZviratkoUzJsiNavstivilSleduj = "Toto zvířátko už jsi navštívil. Sleduj další otazníky!";
    public const string Zpet = "Zpět";

    // Spoken descriptions of panels that open without a character speaking. The green check mark accepts what the
    // panel offers and the red X declines it, so the offers say so in the same words everywhere.
    public const string Potvrdit = "Potvrdit";
    public const string GarazVyberSiAutoDruhyUkolu =
        "Garáž. Vyber si auto, druhy úkolů a mazlíčky. Červený křížek tě vrátí na cestu.";
    public const string OvoceNabidka = "Chci tři jablka. Zelená fajfka tě pro ně pošle, červený křížek okno zavře.";
    public const string GolemNabidka =
        "Přiveď mi šest dílků golema. Zelená fajfka tě pro ně pošle, červený křížek okno zavře.";
    public const string GolemJeHotovy = "Golem je hotový! Najdeš ho v garáži.";
    public const string ObjednavkaJeNaCesteDorucJi = "Tvoje objednávka je na cestě. Doruč ji zvířátku na obrázku.";
    public const string AlbumVyberObrazekAOdpovez =
        "Album pohlednic. Vyber obrázek a odpověz na otázku, ať pohlednice dostane razítko.";
    public const string VyberSlabikyPodleObrazku = "Vyber slabiky podle obrázku a potvrď je zelenou fajfkou.";
    public const string VyberObrazekPodlePrvnihoPismena = "Vyber obrázek podle prvního písmene.";
    public const string PrectiSlovoAVyberObrazek = "Přečti slovo a vyber obrázek.";
    public const string VyresPriklad = "Vyřeš příklad.";
    public const string PorovnejCisla = "Porovnej čísla.";

    // CompareChallenge: how the signs are read aloud.
    public const string JeMensiNez = "je menší než";
    public const string SeRovna = "se rovná";
    public const string JeVetsiNez = "je větší než";
    public const string RozvozPopis = "Rozvoz. Odvezeš kamarádovi zboží z obrázku.";
    public const string DovozPopis = "Dovoz. Přivezeš kamarádovi, co chybí do sbírky.";
    public const string Pauza = "Pauza";

    public static string HouseProgress(int level, int total) => $"Domeček má {level} z {total} vylepšení.";

    public static string CollectedProgress(int collected, int total) => $"Už máš {collected} z {total}. Jeď pro další.";

    public static string CollectorProgress(int count, int total) =>
        count >= total ? $"Máme všech {total}! Děkujeme!" : $"Už máme {count} z {total}.";

    public static string GolemProgress(int collected) => $"Už máš {collected} z 6 dílků golema. Jeď pro další.";

    // Words and phrases without diacritics
    public const string FarmaMeziLoukami = "Farma mezi loukami";
    public const string NaSilniciUFarmy = "Na silnici u farmy";
    public const string AlbumPohlednic = "Album pohlednic";
    public const string NejprveVybralaSlabiky = "nejprve vybrala slabiky";
    public const string JednuMrkev = "jednu mrkev";
    public const string JednoJablko = "jedno jablko";
    public const string JednuSkleniciMedu = "jednu sklenici medu";
    public const string Prkna = "prkna";
    public const string Cihly = "cihly";
    public const string AFarma = "A · Farma";
    public const string EFarma = "E · Farma";
    public const string VyrazitProOvoce = "Vyrazit pro ovoce";
    public const string KonecHry = "Konec hry";

    // Texts with values inside. A value that has to be worked out first (a time, a title) is passed in as the text.
    /// <summary>The count of everything a works has made, which earns its pet at the goal.</summary>
    public static string MadeInAll(int made, int goal, string pet) =>
        made >= goal ? $"✓  Vyrobeno celkem {made}  ·  {pet}" : $"Vyrobeno celkem {made} / {goal}  ·  {pet}";

    public static string NewPet(string name) => $"Nový mazlíček: {name}!";

    public static string CourseBeat(string course, string time) => $"{course} — porazil jsi ducha! Čas {time} s";

    public static string CourseFinished(string course, string time, string ghostTime) =>
        $"{course} — krásná jízda! Čas {time} s, duch měl {ghostTime} s";

    public static string CourseHud(int gate, int gates, float distance, string time) =>
        $"Brána {gate} / {gates} · {distance:0} m\nČas {time} s";

    public static string AheadOfGhost(string gap) => $"\nPřed duchem o {gap} s";

    public static string BehindGhost(string gap) => $"\nZa duchem o {gap} s";

    public static string CourseGhostTime(string course, string ghostTime) => $"{course}\nPoraz ducha: {ghostTime} s";

    public static string BuilderThanks(string name, int stage, int trips) => $"{name} děkuje!  {stage} / {trips}";

    public static string BuildDone(int coins, string item) => $"Stavba je hotová!  +{coins}  ·  {item}";

    public static string PlotDone(string name, int coins) => $"Domeček je hotový! {name} se nastěhoval.  +{coins}";

    public static string PlaygroundToy(string toy, int stage, int trips) => $"Máme {toy}!  {stage} / {trips}";

    public static string RailwayPart(string part, int stage, int trips) => $"Máme {part}!  {stage} / {trips}";

    public static string RailwayDone(int coins) => $"Nádraží je hotové! Vláček jezdí.  +{coins}";

    public static string ZooPart(string part, int stage, int trips) => $"Máme {part}!  {stage} / {trips}";

    public static string ZooDone(int coins) => $"Zoo je hotová! Zvířátka mávají.  +{coins}";

    public static string PlaygroundDone(int coins, string item) =>
        $"Hřiště je hotové! Děti si hrají.  +{coins}  ·  {item}";

    public static string Bring(string what) => $"přivez {what}";

    public static string Deliver(string what) => $"rozvez {what}";

    public static string PleaseNow(string task) => $"Teď prosím {task}.";

    public static string HouseNeeds(string wanted) => $"Na vylepšení domečku potřebuju {wanted}. Přivezeš mi je?";

    public static string HouseNeedsButNone(string wanted, string works) =>
        $"Na vylepšení domečku potřebuju {wanted}, ale {works} jich nemá. Pošlu tě pro surovinu!";

    public const string HouseNeedsGlasses = "Chystám oslavu domečku! Přivezeš mi skleničky ze sklárny?";

    public const string HouseNeedsGlassesButNone =
        "Chystám oslavu domečku, ale ve sklárně ještě žádné skleničky nemají. Pošlu tě pro písek!";

    public const string JedSVozikemProSklenickyDo = "Jeď s vozíkem pro skleničky do sklárny.";

    public static string GlassesParty(int coins) => $"Oslava domečku!  +{coins}  ·  Skleničky";

    public const string HouseNeedsBread = "Chystám snídani pro celou rodinu! Přivezeš mi voňavý chléb z pekárny?";

    /// <summary>Kept for a valley without the mill, whose bakery still takes the farmer's wheat itself.</summary>
    public const string HouseNeedsBreadButNone =
        "Chystám snídani, ale v pekárně ještě žádný chléb nemají. Pošlu tě na pole pro obilí!";

    public const string HouseNeedsBreadButNoFlour =
        "Chystám snídani, ale v pekárně ještě žádný chléb nemají. Pošlu tě do mlýna pro mouku!";

    public const string HouseNeedsBreadButNoWheat =
        "Chystám snídani, ale v pekárně nemají chléb a ve mlýně mouku. Pošlu tě na pole pro obilí!";

    public const string HouseNeedsBreadButNoMill =
        "Chystám snídani, ale v pekárně nemají mouku na chléb. Nejdřív pomoz mlynáři dostavět mlýn!";

    public const string JedSVozikemProChlebDo = "Jeď s vozíkem pro čerstvý chléb do pekárny.";

    /// <summary>For a valley without the mill, whose bakery still takes the farmer's wheat itself.</summary>
    public const string JedSVozikemProObiliNa = "Jeď s vozíkem na pole pro obilí. Pak ho odvezeš do pekárny.";

    public const string JedSVozikemProObiliDoMlyna = "Jeď s vozíkem na pole pro obilí. Pak ho odvezeš do mlýna.";

    public const string JedSVozikemProMoukuDoMlyna = "Jeď s vozíkem do mlýna pro mouku. Pak ji odvezeš do pekárny.";

    public const string Mouka = "Mouka";

    public const string MlynJesteNemele =
        "Mlýn se ještě staví, a tak nemá kdo semlít obilí. Pomoz mlynáři mlýn dostavět!";

    public const string MlynJePlnyMouky = "Mlýn je plný mouky. Odvez ji nejdřív do pekárny.";

    public const string SemeluZObiliMouku = "Děkuju! Semelu z obilí mouku. Odvez ji pak do pekárny.";

    public const string MlynJePlnyDekuju = "Mlýn je plný mouky. Děkuju!";

    public const string NalozimTiMoukuProPekarnu = "Naložím ti mouku pro pekárnu.";

    public const string NemamMoukuPrivezObili = "Nemám žádnou mouku. Přivez mi obilí od sedláka a já ho semelu.";

    public const string PekarnaMaPlnoChleba = "Pekárna má plno chleba. Mouku odvezeme, až ho trochu ubude.";

    public const string FrantisekNoBreadFlour =
        "V pekárně došel chléb! Pošlu tě do mlýna pro mouku, ať pekař upeče nový.";

    public const string FrantisekNoBreadWheat = "V pekárně došel chléb a ve mlýně mouka! Pošlu tě na pole pro obilí.";

    public const string FrantisekNoBreadWait = "V pekárně došel chléb. Až dovezeš svůj náklad, přijeď zas.";

    public static string BreadParty(int coins) => $"Snídaně u domečku!  +{coins}  ·  Chléb";

    public static string NewUpgrade(string upgrade) => $"Nové vylepšení: {upgrade}";

    public static string CoinsTotal(int total) => $"Penízky: {total}";

    public static string CoinsGained(int coins) => $"Získáváš {coins} penízků";

    public static string ValleySeed(int seed) => $"Seed údolí: {seed}";

    public static string Switch(string label, bool on) => $"{label} · {(on ? "zapnuto" : "vypnuto")}";

    public static string BuyNamed(string name, int cost) => $"Koupit {name} za {cost} penízků";

    public static string BuyFor(int cost) => $"Koupit za {cost} penízků";

    public static string PriceHint(int cost) => $"Stojí {cost} penízků. Sbírej penízky ve světě a plň úkoly.";

    // Signs of the post office, the lighthouse, the ferry and the coast quarry
    public const string SignPost = "POŠTA";
    public const string SignLighthouse = "MAJÁK";
    public const string SignFerry = "PŘÍVOZ";
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
}
