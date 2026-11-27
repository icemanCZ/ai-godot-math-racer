# Rallye převoznice Venduly

Další obsah pro velkou mapu, po ovečkách pastýře Bohouše (`docs/SHEEP_PLAN.md`). Okruh kolem starého údolí měří přes
pět a půl kilometru. Auto po něm zatím jen projíždělo k něčemu jinému. Rallye z okruhu dělá závodní trať a ptá se při
plné jízdě.

## Nápad v jedné větě

**Liška převoznice Vendula** pořádá **rallye kolem celého kraje**. Okruh se dělí na **čtyři etapy**, vždy od jedné
silnice k hranici k další („Od řeky k moři“, „Od moře k lesu“…). Na každé etapě stojí **tři branky se třemi pruhy**.
Před brankou se objeví otázka a dítě **projede pruhem se správnou odpovědí**. Čas se měří a za chybu se přičte pět
vteřin. Podle času dostane etapa **bronz, stříbro nebo zlato**. Zlato ve všech čtyřech etapách přivede **vydru
Šplouchalku**.

Proč právě takhle:

- **Projede se celá velká mapa.** Čtyři etapy dají dohromady celý okruh, na semínku 42 je to 5580 m (etapy 1644, 1182,
  1160 a 1594 m). Po každé etapě se dá rovnou jet dál a kdo objede všechny čtyři najednou, dostane bonus za celý kraj.
- **Nová činnost: otázka v plné jízdě.** Jinde se odpovídá v panelu, kde auto stojí. Tady jsou odpovědi napsané nebo
  nakreslené na tabulích nad silnicí a odpovídá se volantem: dítě musí přečíst otázku, najít odpověď a včas najet do
  správného pruhu. Rychlost a přesnost tu poprvé jdou proti sobě.
- **Opakuje se ráda.** Medaile a nejlepší časy zůstávají v panelu. Bronz má každý, kdo dojede, a zlato chce čistou jízdu
  po silnici bez chyb. Dítě se k etapě vrací, aby si zlepšilo čas.
- **Staví na tom, co ve hře funguje.** Otázky jsou běžné otázky hry (`QuizSession.NextGate`) bez slabik, protože ty se
  skládají a nedají se projet pruhem. Obrázky odpovědí kreslí `WordPicture`, stejně jako v panelu. Odměna je mazlíček.

## Postavy

- **Liška převoznice Vendula** (strážkyně říční hranice, `BorderKeeperRole` s `Border.River`): po prvním dopisu, který
  jí pošťák doveze (`Post.Delivered[River] > 0`), ukazuje v panelu místo vizitky závodní kartu. Na ní jsou čtyři etapy
  s medailí, obrázky hranic, mezi kterými etapa vede, nejlepším časem a zelenou fajfkou startu.
- **Vydra Šplouchalka** (`CollectorPetKind.Otter`): sedí u Vendulina domu a po čtvrtém zlatu jezdí za autem.

## Trať

- **Plán** (`World.PlanRally`): spočítá se jednou, po ovečkách, bez náhodných čísel. Okruh se projde po 2 m
  (`RallyCourse`). Trať nezabírá žádnou půdu: nic se kvůli ní nevynechá, ani rostlina, ani tráva, a branky nemají
  kolize. Proto se nic, co v údolí už bylo, nepohne. Trať drží `Tests/Worlds/seed-N.rally.txt` a plán trvá asi 15 ms.
- **Startovní oblouky** (`ArchFits`): čtyři, každý asi 30 m za odbočkou k hranici, po směru jízdy. Nesmí stát na mostě
  ani blíž než 25 m od něj, musí být nejméně 22 m od každé křižovatky a jejich sloupky musí stát v suchu mimo kmeny.
  První etapa začíná u řeky, tedy u Venduly.
- **Branky** (`PlanGates`, `GateFits`): tři na etapu, kolem čtvrtin její délky, nejméně 190 m za startem, 150 m od sebe
  a 90 m před cílem. Stojí na rovném úseku, 40 m od mostů a křižovatek. Jsou daleko od oveček, doupat, balonu, lomu a
  majáku, aby se nepletly do jejich obrázků. Všechny tři pruhy (13 m napříč) musí být suché, bez kmenů a jen málo nad
  silnicí nebo pod ní, aby se dalo projet i krajním pruhem.

## Hra

1. Vendula má po prvním dopisu žlutou značku, dokud nemá žádný čas. Její panel (`GameState.RallyStatus` 37,
   `GameUi.RallyPanel`) se dá pochopit bez čtení: u každé etapy medaile, obrázek hranice odkud a kam (řeka → moře),
   nejlepší čas a zelená fajfka (`UiIntent.RallyStart`), která etapu přečte nahlas i s medailí a pošle dítě na start.
   Pod etapami jsou zlaté medaile se šipkou k vydře a známky v albu. Jak se jede, řekne Vendula nahlas.
2. Čas začne běžet, když auto projede startovním obloukem po směru okruhu. Rohový panel (`GameUi.RallyHud`) vlevo nahoře
   ukazuje obrázky hranic etapy, čas a pruh etapy (`RallyStrip`): silnici od praporku startu k praporku cíle, tři
   kroužky branek (bílý před námi, zelený s fajfkou za správně, červený s křížkem za chybu) a červenou tečku auta.
3. Když je branka blíž než 170 m (`Game.RallyQuestionAhead`), objeví se na jejích tabulích tři odpovědi a v rohovém
   panelu otázka se třemi barevnými rámečky ve stejném pořadí jako pruhy (červený vlevo, žlutý, modrý vpravo). Otázku
   hra jednou přečte nahlas.
4. Pruh se pozná podle toho, kde auto branku projede (`RallyCourse.LaneAt`). Správná odpověď dá minci a zelenou tabuli.
   Chyba nebo projetí mimo branku přičte 5 vteřin (`RallyPlans.PenaltySeconds`) a ukáže, co bylo správně.
5. V cíli, u dalšího oblouku, dostane etapa medaili podle průměrné rychlosti i s trestnými vteřinami (`MedalFor`):
   zlato za 11,5 m/s, stříbro za 8,5 m/s, jinak bronz. Auto jede nejvýš 15 m/s, takže zlato je čistá jízda po silnici.
   Pak se hned jede další etapa a čas se měří znovu.
6. Kdo sjede z trati víc než 45 m na 4 vteřiny nebo se vrátí 30 m za start, etapu vzdá a může ji jet znovu.

## Odměny

| za co | odměna |
|---|---|
| správná odpověď v brance | 1 mince (`RallyPlans.CoinsPerGate`) |
| dojetá etapa | 3 mince za bronz, 5 za stříbro, 8 za zlato (`StageCoins`) |
| lepší medaile, než etapa kdy měla | 10 mincí navíc (`NewMedalCoins`) |
| všechny čtyři etapy za sebou | 15 mincí navíc (`LapCoins`) |
| zlato ve všech čtyřech etapách | Vydra Šplouchalka |

## Kód (podle `CLAUDE.md`)

- **Plán a stav** (`Valley`): `World.Rally.cs` (oblouky, branky, stavba), `RallyCourse.cs` (okruh po 2 m, poloha na
  trati a pruh) a `RallyRun.cs` (jízda jedné etapy bez uzlů scény: start, branky, cíl, vzdání a pokračování).
- **Modely** (`Buildings`): `RallyGates` (oblouky s praporky a nápisem START · CÍL, branky s tabulemi, které kreslí
  odpovědi), `RallyPlans` (počty, mince, medaile, časy a jména etap).
- **Hra** (`App`): `Game.Rally.cs`, systém `rally` v `Game.Systems.cs`, panel v `Game.Panels.cs`,
  `UiIntent.RallyStart`, `Texts`.
- **Rozhraní** (`Ui`): `GameUi.Rally.cs`, tedy závodní karta s medailemi (`MedalPicture`) a rohový panel s otázkou.
- **Uložení**: sekce `SaveData.Rally` (`Best`, nejlepší čas každé etapy v desetinách vteřiny, 0 je nejeto). Pravidlo
  `HasInvalidRally` odmítne například špatný počet etap, čas delší než hodina, čas bez Vendulina dopisu nebo její panel
  u jiné postavy. Medaile se neukládají, počítají se z času, a rozjetá etapa se neukládá: po načtení se jede znovu od
  startu. Hry uložené před rallye se načtou bez časů.
- **Kontroly**: `rally-checks` kontroluje:
  - trať: čtyři etapy kolem celého okruhu, oblouky a branky na suchu, mimo kmeny a mosty, odstupy;
  - pravidla uložení a starou hru;
  - Vendulin panel a značku až po dopisu, start, jízdu přes branky správným i špatným pruhem, trestné vteřiny, zlato,
    mince, pokračování do další etapy, vzdání, načtení během jízdy a vydru.

  Dále rallye hlídají `world-fingerprint-checks`, `content-checks` a `contract-checks`. Obrázky:
  `./run.ps1 -Capture -View rally-gate -Seed 42` (a `rally`, `rally-panel`).

## Další nápady

- Duch nejlepšího času: průhledné autíčko, které jede po trati tak, jak dítě jelo minule.
- Noční rallye s rozsvícenými tabulemi a světlušky jako body na trati.
- Soupeři: zvířátka z vesnice ve svých autíčkách, která jedou stejnou etapu.
