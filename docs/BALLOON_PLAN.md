# Balónové stanoviště

Další obsah pro velkou mapu, po drakovi (`docs/DRAGON_PLAN.md`), kde byl horkovzdušný balon mezi dalšími nápady. Proti
tamnímu nápadu stojí stanoviště daleko od vesnice, a let tak má vlastní činnost: chytání hvězdiček.

## Nápad v jedné větě

Daleko za vesnicí, na nejvyšším místě u silnic pásu, se na laně vznáší velký **duhový balon** liščí balonářky
**Amálky**. Dítě za ní dojede, vyřeší otázku, „ohřeje vzduch v balonu“ a vzlétne. Vítr nese balon po pevné trase nad
údolím a zpátky a dítě jen hořákem stoupá a ventilem klesá, aby košem chytilo **zlaté hvězdičky**, které visí v různých
výškách.

Proč právě takhle:

- **Je vidět z dálky.** Balon je i s košem vysoký asi 25 m a na laně visí 9 m nad polem, takže přečnívá stromy. Stojí
  na nejvyšším místě, kterým vedou silnice pásu, nejméně 480 m od vesnice. V noci svítí jako lampion ve svých barvách a
  z dálky nad ním visí ikona balonu.
- **Nová činnost, ne další dovoz.** Let je jednoduchá hra pro jednu ruku: plyn je hořák, couvání ventil. Dítě musí
  odhadnout, kdy začít stoupat, protože balon reaguje pomalu.
- **Staví na tom, co ve hře funguje.** Otázka před letem je běžný kvíz, kamera jede s balonem jako na drakovi a odměna
  je mazlíček jako u ostatních sběratelů.

## Postavy

- **Liška balonářka Amálka** (`CharacterCatalog.Balloonist`, `ActorIds.Balloonist` 80): kožená letecká čepice s
  brýlemi, kožená bunda, červená šála a mosazný dalekohled.
- **Kohoutek Kokrháček** (`CollectorPetKind.Rooster`): čeká u Amálky a po dvaceti hvězdičkách jezdí za autem. Na zádech
  má malinký balonek. Je to vzpomínka na první let balonem z roku 1783, kdy letěli beran, kachna a kohout; tuhle
  zajímavost Amálka vypráví v panelu.

## Stanoviště

- **Místo** (`World.PlanBalloon`, `BalloonSite`): podél každé silnice pásu se po 8 m zkouší obě strany. Ze všech míst,
  kam se stanoviště vejde (`BalloonFits`), vyhraje to, jehož pole leží nejvýš. Vejde se tam, kde je pole skoro rovné,
  leží nejméně 480 m od vesnice, mimo silnice, řeky, písek a mosty a nejméně 160 m od statků hranic, majáku a draka.
  Auto musí z cesty vyjet na pole. Nejsou při tom žádná náhodná čísla, takže se nic, co v údolí už bylo, nepohne:
  rostliny pásu se plánují jako dřív a jen se nepostaví (`InBalloonZone` uvnitř `InBorderfolkZone`). Místo drží
  `Tests/Worlds/seed-N.balloon.txt`.
- **Model** (`BalloonStation`, `HotAirBalloon`): přistávací terč z červených a bílých kruhů, dřevěná bouda s
  pruhovanou markýzou a nápisem BALÓNY, větrný rukáv, lahve s plynem, pytle s pískem a cedule u silnice. Balon má
  dvanáct dílů v barvách duhy, proutěný koš a hořák. Na poli visí na čtyřech lanech 9 m nad terčem. Občas zahučí hořák
  a v noci obal svítí.

## Let

1. Amálčin panel (`GameState.BalloonStatus` 34) ukáže, kolik hvězdiček dítě chytilo a kolik chybí do kohoutka, a
   zelenou šipkou (`UiIntent.BalloonFlight`) nabídne let.
2. Nejdřív přijde otázka (`BeginQuiz`). Správná odpověď pustí lana a balon stoupá.
3. Vítr nese balon po pevné trase (`BalloonFlight`): uzavřená křivka od pole směrem ke středu údolí a zpátky, dlouhá 500
   až 1200 m. Letí rychlostí 10,5 m/s a let trvá zhruba minutu a půl. Spodní hranice trasy je nejméně 14 m nad
   nejvyšší zemí v okolí.
4. **Plyn** (W, šipka nahoru, páčka dopředu) zapálí hořák a balon zrychluje vzhůru, **couvání** (S, šipka dolů, páčka
   dozadu) otevře ventil a balon klesá rychleji. Když dítě nedělá nic, vzduch chladne a balon pomalu klesá. Mezerník a E
   jsou tlačítko mluvení a balon jimi hned přistane, ale až po jedné a půl vteřině letu, aby let neukončilo stisknutí,
   které ho spustilo.
5. Podél trasy visí **osm hvězdiček** (`BalloonStars`) ve výškách 12 až 48 m. Každý let má jiné pořadí výšek. Hvězdička,
   které se koš dotkne, se rozprskne. Ta, kterou balon mine, zhasne.
6. Po poslední hvězdičce vítr balon sám snese na terč. Balon pak pomalu vystoupá na lanech zpátky.

## Odměny

| za co | odměna |
|---|---|
| každá hvězdička | 1 mince (`BalloonPlans.CoinsPerStar`) |
| všech osm hvězdiček v jednom letu | 5 mincí navíc (`AllStarsCoins`) |
| první let | 10 mincí (`FirstFlightCoins`) |
| 20 hvězdiček celkem | kohoutek Kokrháček (`RoosterStars`) |

Při letu se navíc rozsvítí mince, zvířata a schovaní kamarádi dole v údolí, stejně jako při letu na drakovi. Z výšky je
tak vidět, kam ještě dojet.

## Kód (podle `CLAUDE.md`)

- **Místo a let** (`Valley`): `BalloonSites.cs`, `BalloonFlight.cs` (trasa a hvězdičky), `BalloonRide.cs` (jeden let
  bez uzlů scény: vzdálenost, výška, stoupání, chycené hvězdičky) a `World.Balloon.cs`.
- **Modely** (`Buildings`): `BalloonStation`, `HotAirBalloon` (obal je `ArrayMesh` s barvami ve vrcholech a malým
  shaderem, který v noci svítí v barvách dílů), `BalloonStars`, `BalloonPlans`.
- **Hra** (`App`): `Game.Balloon.cs`, systém `balloon` v `Game.Systems.cs`, panel v `Game.Panels.cs`, `Texts`.
- **Rozhraní** (`Ui`): `GameUi.Balloon.cs`, tedy panel, nápověda a rohový panel letu s počtem hvězdiček a ovládáním.
- **Uložení**: sekce `SaveData.Balloon` (`Flights`, `Stars`) s pravidlem `HasInvalidBalloon`. Let sám se neukládá:
  načtení hry vrátí balon na lana. Hry uložené před stanovištěm se načtou s nulami.
- **Kontroly**: `balloon-checks` (místo, pravidla uložení, první let, hořák a ventil, chycení a minutí hvězdičky,
  uložení během letu, předčasné přistání, kohoutek, let se všemi hvězdičkami a přistání), dále
  `world-fingerprint-checks` a `content-checks`. Obrázky: `./run.ps1 -Capture -View balloon` (a `balloon-night`, `balloon-far`,
  `balloon-far-night`, `balloon-panel`, `balloon-flight`).

## Jak to dopadlo

- Na seedech 1 až 40 má každé údolí stanoviště a `balloon-checks` projdou.
- Balon nejdřív stál na poli. Z vesnice ho ale schovávaly stromy a překrývala ho ikona balonářky. Teď visí na laně nad
  polem, je větší a ikona se z dálky posune nad něj.
- Ikona balonářky (`Character.RaiseFarMarker`) sjede nad její hlavu, až když je auto blíž než asi 30 m.

## Další nápady

- Ukazatel výšky na rohovém panelu (šipka ke další hvězdičce nahoru nebo dolů) pro menší děti.
- Vítr, který se každý den mění, a s ním trasa letu.
- Noční let k souhvězdí (`Constellation`): hvězdičky by visely mezi skutečnými hvězdami.
