namespace MathRacer.App;

/// <summary>
/// The fixed Czech texts of the game: menus, hints, what the characters say, the names of the postcards. A text lives
/// here instead of in the code that shows it, so the language can be proofread in one file and the code says what
/// happens, not what is written. The names are made from the text itself (without diacritics), which keeps them unique;
/// a text that is edited may keep its name. Texts with numbers or names inside (<c>$"..."</c>) are still where they are
/// used, and so are the texts of the data classes (the catalogue of characters, the courses, the village).
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

    // Game.Plots
    public const string MocDekujuZaDomecekBydliSe = "Moc děkuju za domeček! Bydlí se mi tu skvěle.";
    public const string PrivezesMiDalsiMaterial = "Přivezeš mi další materiál?";
    public const string DekujuAzBudesChtitPrivezDalsi = "Děkuju! Až budeš chtít, přivez další materiál.";
    public const string HuraDomecekJeHotovyTedTu = "Hurá, domeček je hotový! Teď tu budu bydlet. Moc děkuju!";

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

    // GameUi.Postcards
    public const string PohledniceZKouzelnehoUdoli = "Pohlednice z Kouzelného údolí";

    // GameUi.Weather
    public const string ACarodejnice = "A · Čarodějnice";
    public const string ECarodejnice = "E · Čarodějnice";
    public const string Dest = "Déšť";
    public const string Vitr = "Vítr";
    public const string Bourka = "Bouřka";

    // GameUi.Works
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
}
