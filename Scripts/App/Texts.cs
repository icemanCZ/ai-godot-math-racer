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

    public static string HiddenFoundSpeech(string name, int count, int total) =>
        $"Pšt! Tady je {name}! Už jich máš {count} z {total}.";

    public static string HiddenAllOfKind(HiddenKind kind, int total) =>
        kind switch
        {
            HiddenKind.Ladybug => $"Našel jsi všech {total} berušek!",
            HiddenKind.Hedgehog => $"Našel jsi všech {total} ježků!",
            _ => $"Našel jsi všech {total} žabek!",
        };

    public static string HiddenNotebook(int ladybugs, int hedgehogs, int frogs, int total) =>
        $"Berušky {ladybugs}/{total} · Ježci {hedgehogs}/{total} · Žabky {frogs}/{total}";

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

    public static string LampsBag(int carried, int max) => $"Ve vaku: {carried} / {max}";

    public static string LampsOut(int placed, int max) => $"Venku: {placed} / {max}";

    public static string LampsHud(int carried, string next, bool controller) =>
        $"Lampy ve vaku: {carried} · další: {next}\n{(controller ? "A" : "E")} · postavit";

    public static string LampInBag(string name) => $"{name} je ve vaku.";

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
    public const string VsechnaZviratkaNavstivena = "Všechna zvířátka navštívena!";
    public const string StiskniAProUkolAOdmenu = "Stiskni A pro úkol a odměnu";
    public const string StiskniENeboMezernikProUkol = "Stiskni E nebo mezerník pro úkol a odměnu";
    public const string TotoZviratkoUzJsiNavstivilSleduj = "Toto zvířátko už jsi navštívil. Sleduj další otazníky!";
    public const string Zpet = "Zpět";

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

    public const string HouseNeedsBreadButNone =
        "Chystám snídani, ale v pekárně ještě žádný chléb nemají. Pošlu tě na pole pro obilí!";

    public const string JedSVozikemProChlebDo = "Jeď s vozíkem pro čerstvý chléb do pekárny.";

    public const string JedSVozikemProObiliNa = "Jeď s vozíkem na pole pro obilí. Pak ho odvezeš do pekárny.";

    public static string BreadParty(int coins) => $"Snídaně u domečku!  +{coins}  ·  Chléb";

    public static string NewUpgrade(string upgrade) => $"Nové vylepšení: {upgrade}";

    public static string CoinsTotal(int total) => $"Penízky: {total}";

    public static string CoinsGained(int coins) => $"Získáváš {coins} penízků";

    public static string ValleySeed(int seed) => $"Seed údolí: {seed}";

    public static string AnimalsLeft(int remaining, int total) => $"Zbývá zvířátek: {remaining} / {total}";

    public static string Switch(string label, bool on) => $"{label} · {(on ? "zapnuto" : "vypnuto")}";

    public static string BuyNamed(string name, int cost) => $"Koupit {name} za {cost} penízků";

    public static string BuyFor(int cost) => $"Koupit za {cost} penízků";

    public static string PriceHint(int cost) => $"Stojí {cost} penízků. Sbírej penízky ve světě a plň úkoly.";
}
