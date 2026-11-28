# Stopy hajné Boženy

**Stav:** hotovo (`45a1b2b`, 26. 11. 2026).

Další obsah pro velkou mapu, po balonu (`docs/BALLOON_PLAN.md`). Hajná Božena ze statku u lesní hranice byla v nápadech
drakova plánu s krmelcem. Tady dostala jinou činnost, která vede autem napříč celým pásem za starým údolím.

## Nápad v jedné větě

**Medvědice hajná Božena** má **knihu stop** šesti zvířátek. Když jí pošta přinese první dopis, pošle dítě po první
stopě: řada otisků tlapek vede od jejího statku přes pás až k **doupěti** zvířátka. U doupěte otázka zvířátko vyláká
ven, dítě dostane mince, zvířátko zůstane sedět před doupětem a od něj začíná stopa dalšího. Šesté zvířátko knihu
zaplní a přivede **veverku Oříšku**.

Proč právě takhle:

- **Projede se celá velká mapa.** Šest stop měří dohromady asi 1,8 až 2,3 km. Vedou od lesa kolem dokola údolí a doupata
  leží nejméně 200 m od sebe.
- **Nová činnost, ne další dovoz.** Dítě nehledá šipky, ale stopu: vidí jen kousek před autem a musí se po ní dívat.
  Každé zvířátko má jiný otisk (zajíc skáče, srnka má kopýtka, jezevec drápy) a v knize se to dítě dozví.
- **Staví na tom, co ve hře funguje.** Otázka u doupěte je běžný kvíz, šipka zpátky ke stopě je jako u dovážek a
  odměna je mazlíček jako u ostatních sběratelů. Kniha se otevře až po dopise, takže navazuje na poštu jako maják.

## Postavy

- **Medvědice hajná Božena** (strážkyně lesní hranice, `BorderKeeperRole` s `Border.Forest`): po prvním dopise ukazuje v
  panelu místo své vizitky knihu stop.
- **Šest zvířátek** (`TrackingAnimal`, jména v `TrackingPlans.Name`): Zajíc Ušák, Liška Ryška, Srnka Lenka, Jezevec
  Pruhoun, Prasátko Chrochtík a Rys Ostrovid. Mluví za ně jeden „hlas“ bez těla (`CharacterCatalog.TrackingAnimal`,
  `ActorIds.TrackingAnimal` 81), který vždy stojí před doupětem právě sledované stopy. Zvířátka sama kreslí
  `TrackingDens`.
- **Veverka Oříška** (`CollectorPetKind.Squirrel`): čeká u Boženy a po šesti zvířátkách jezdí za autem. V tlapkách drží
  oříšek.

## Stopy a doupata

- **Plán** (`World.PlanTracking`, `TrackingTrail`): spočítá se jednou, až když jsou naplánované všechny rostliny pásu.
  Má vlastní náhodná čísla (`Settings.Seed + 8123`), takže se nic, co v údolí už bylo, nepohne. Doupata se z rostlin,
  trávy a věcí u cest vynechají přes `InTrackingZone` uvnitř `InBorderfolkZone`. Stopy drží
  `Tests/Worlds/seed-N.tracking.txt`.
- **Začátek** (`TrailStarts`): vedle konce cesty k Boženinu statku, kde je volno kolem dokola (`StartOpen`). Když se
  odtud nedají najít všechna doupata, zkusí se další místo.
- **Doupě** (`DenFits`): leží v pásu mezi okruhem a hranicemi, na rovném a suchém místě. Je mimo silnice, řeky, písek a
  mosty, nejméně 200 m od ostatních doupat a 90 m od statků, majáku, draka a balonu. Na celém doupěti nesmí stát žádný
  naplánovaný strom, takže se kvůli doupěti žádný strom nevynechá. Každé další doupě se hledá dál kolem údolí,
  zhruba 270 až 440 m od předchozího. Když se žádné nevejde, zkusí se blíž a jinými směry. Když se nevejde ani tak,
  vybere se jinde to předchozí.
- **Cesta** (`TrackingPath`): A* po mřížce 4 m přes místa, kudy auto projede. Hledá se mimo řeky, moře, písek a mosty, s
  volným místem 3,2 m od každého kmene a se sklonem nejvýš 0,3. Silnice a les jsou dražší, takže stopa silnici
  přetne, ale nejde po ní. Cesta se pak narovná, kde to jde, a zakulatí v rozích. Každý metr se znovu prověří, a když
  někde neprojde, doupě se zkusí jinde. Hledá se jen v elipse, kterou může vést cesta nejvýš 1,6× delší než přímka, a
  tak i hledání bez výsledku skončí rychle. Celý plán trvá asi 0,2 až 0,6 s.
- **Otisky** (`TrackingPrints`): leží po délce cesty v krocích zvířátka, střídavě vlevo a vpravo, a jsou nakreslené
  3,2× větší než ve skutečnosti. Ukáže se jich jen 40 před nejdále dosaženým otiskem a 8 za ním a ke konci blednou.
  Další čtyři zlatě pulzují a nad dalším úsekem se vznášejí zlaté jiskřičky, které jsou vidět i nad vysokou trávou a v
  noci.
- **Modely doupat** (`TrackingDens`): zajíc má keř, liška noru v náspu, srnka břízu s kapradím, jezevec noru pod pařezem
  s hromádkou písku, prasátko kaliště s rákosím a kládou a rys skály s padlým kmenem. Zvířátko nejdřív vykukuje a po
  vylákání sedí před doupětem a hýbe ušima.

## Hra

1. Pošťák donese Boženě první dopis (`Post.Delivered[Forest] > 0`). Od té doby nad ní visí žlutá značka a její panel
   (`GameState.TrackingStatus` 35, `GameUi.TrackingPanel`) ukazuje knihu stop: otisky všech zvířátek, jména nalezených a
   otazníky u ostatních. Zelená fajfka (`UiIntent.TrackingStart`) pošle dítě po první stopě a jak se stopuje, řekne
   Božena nahlas.
2. Rohový panel (`GameUi.TrackingHud`) ukazuje otisk, čí je to stopa a kolik z ní už auto ujelo. Když auto od stopy
   odjede dál než 24 m, objeví se v panelu šipka zpátky ke stopě (`TrackingHunt.Lead`).
3. Otisk se počítá za dosažený, když se k němu auto přiblíží na 6 m (`TrackingHunt.Step`). Úsek, který je vidět, se
   posouvá s autem.
4. U doupěte tlačítko mluvení nabídne „Vylákat zvířátko“. Otázka (`BeginQuiz`) ho vyláká ven. Zvířátko pak sedí před
   doupětem, dostane stránku v knize a hlas se přesune k doupěti další stopy.
5. Po šestém zvířátku je kniha plná. Božena pak v panelu ukazuje celou knihu a za autem jezdí veverka.

## Odměny

| za co | odměna |
|---|---|
| každé vylákané zvířátko | 6 mincí (`TrackingPlans.CoinsPerAnimal`) |
| plná kniha | 20 mincí navíc (`BookCoins`) |
| šest zvířátek | veverka Oříška |

## Kód (podle `CLAUDE.md`)

- **Plán a stav** (`Valley`): `TrackingTrail.cs` (zvířátka, otisky, stopa), `TrackingPath.cs` (A*), `World.Tracking.cs`
  (doupata, cesty, stavba a hlas), `TrackingPrints.cs` (otisky a jiskřičky), `TrackingHunt.cs` (postup bez uzlů scény:
  poslal Boženu, kolik zvířátek je nalezeno, nejdále dosažený otisk).
- **Modely** (`Buildings`): `TrackingDens`, `TrackingPlans` (jména, mince, zajímavosti o stopách).
- **Hra** (`App`): `Game.Tracking.cs`, systém `tracking` v `Game.Systems.cs`, panel v `Game.Panels.cs`,
  `UiIntent.TrackingStart`, `Texts`.
- **Postavy** (`Characters`): `TrackingAnimalRole`, `Character.Tracking.cs` (hlas bez těla a `Away`, které ho vyřadí
  z postav u auta, když se po žádné stopě nejde).
- **Rozhraní** (`Ui`): `GameUi.Tracking.cs`, tedy panel s knihou, rohový panel stopy se šipkou a obrázky otisků
  (`TrackPicture`, ze stejných oválů jako otisky ve hře).
- **Uložení**: sekce `SaveData.Tracking` (`Started`, `Found`) s pravidlem `HasInvalidTracking`. Pravidlo odmítne
  například nalezená zvířátka bez začátku, stopu před prvním Boženiným dopisem a otázku u doupěte, když se po žádné
  stopě nejde. Nejdále dosažený otisk se neukládá, po načtení se najde podle toho, kde auto stojí. Hry uložené před
  stopami se načtou s nulami.
- **Kontroly**: `tracking-checks` kontroluje:
  - každou stopu po metrech: kmeny, voda, sklon, otisky na zemi, doupata od sebe a mimo silnice, žádný vynechaný strom;
  - pravidla uložení a starou hru;
  - Boženin panel před dopisem a po něm, jízdu po otiscích, šipku po sjetí ze stopy a otázku u doupěte;
  - další stopu, poslední zvířátko, veverku a plnou knihu.

  Dále stopy hlídají `world-fingerprint-checks`, `content-checks` a `contract-checks`. Obrázky:
  `./run.ps1 -Capture -View tracking-trail -Seed 42` (a `tracking`, `tracking-panel`, `tracking-night`, `tracking-lost`,
  `tracking-den`, `tracking-dens`, `tracking-dens-night`).

## Jak to dopadlo

- Na seedech 1 až 96 má každé údolí všech šest stop a `tracking-checks` projdou. Jen několik údolí potřebuje vybrat
  některé doupě znovu.
- Hledání cesty nejdřív u některých údolí trvalo až 35 s, protože A* bez výsledku prošel 90 000 buněk. Elipsa podle
  nejdelší přípustné cesty to srazila pod 1 s.
- Doupě nejdřív hlídalo stromy jen do 5 m od středu, ale vynechává se kruh 7 m, takže se občas vynechal strom na
  okraji. Teď se volno kontroluje po celém doupěti.
- Hustý les u Boženy občas nedovolil začít hned u statku. Proto se zkouší víc začátků, dál od dvora i na cestě.
- Otisky v trávě jsou zblízka hezké, ale z auta je tráva zakryje. Proto nad dalším úsekem svítí zlaté jiskřičky.

## Další nápady

- Otisky, které po dešti (`Weather`) chvíli zůstanou jasnější.
- Noční stopa: sova nebo ježek, jejichž doupě je vidět jen v noci.
- Odlévání otisku do sádry u doupěte jako sbírka v muzeu.
