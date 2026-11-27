# Ztracené ovečky pastýře Bohouše

Další obsah pro velkou mapu, po stopách hajné Boženy (`docs/TRACKING_PLAN.md`). Ovečky pastýře Bohouše byly v nápadech
drakova plánu. Tady dostaly činnost, při které se projede celý pás za starým údolím a auto vede stádo zpátky domů.

## Nápad v jedné větě

Bouřka vyplašila **osm oveček** **Zajíce pastýře Bohouše** a ty se rozutekly do všech koutů kraje. Bohouš pošle dítě
hledat hned, bez čekání na dopis. U každé ovečky stačí dojet blízko: zazvoní zvoneček, ovečka zabečí a **jde za autem**.
Dítě může sbírat víc oveček najednou a vést za sebou celé stádo. Ovečky ale nestačí plné rychlosti, a tak se musí jet
pomalu, jinak některá zůstane pozadu. U salaše vyřeší dítě otázku a ovečky jedna po druhé **přeskočí ohradu**, a
počítá se při tom jako před spaním: jedna, dvě, tři… Osm oveček doma přivede **ovčáckého pejska Alíka**.

Proč právě takhle:

- **Projede se celá velká mapa.** Každá ovečka utekla do jiného z osmi směrů kolem údolí, mezi okruh a hranice, nejméně
  160 m od ostatních. Od salaše pod horami až k moři je to přes dva kilometry.
- **Nová činnost, ne další dovoz.** Dítě nehledá šipky k jednomu cíli, ale vede za sebou stádo, které ho poslouchá jen
  při mírné jízdě. Je to jiná dovednost než u stop nebo u lovu: jet opatrně a ohlížet se.
- **Staví na tom, co ve hře funguje.** Ovečky jdou po dráze, kterou jelo auto, jako mazlíčci (`CompanionPath`). Otázka
  je běžná otázka před výjezdem (`Departure.Sheep`) a odměna je mazlíček. Na rozdíl od majáku a stop se na dopis
  nečeká: ovečky se dají hledat od začátku hry.

## Postavy

- **Zajíc pastýř Bohouš** (strážce horské hranice, `BorderKeeperRole` s `Border.Mountains`): od začátku hry ukazuje v
  panelu místo své vizitky ohradu se jmény oveček. V ohradě u salaše už stojí tři ovečky, které neutekly.
- **Osm oveček** (`SheepPlans.Names`): Bělka, Kudrnka, Vločka, Chundelka, Mráček, Pampeliška, Ťapka a Kulička. Nejsou
  to postavy: najdou se tím, že k nim auto dojede, a kreslí je `SheepFlock`.
- **Ovčácký pejsek Alík** (`CollectorPetKind.Sheepdog`): čeká u Bohouše a po osmé ovečce jezdí za autem. Beránek už na farmě je, proto pejsek.

## Kde se ovečky pasou

- **Plán** (`World.PlanSheep`): spočítá se jednou, po stopách, s vlastními náhodnými čísly (`Settings.Seed + 8311`), a
  tak se nic, co v údolí už bylo, nepohne. Ovečky nezabírají žádnou půdu: nic se kvůli nim nevynechá, ani rostlina, ani
  tráva. Místa drží `Tests/Worlds/seed-N.sheep.txt`.
- **Místo** (`SheepSpotFits`): v pásu mezi okruhem a hranicemi, v jednom z osmi směrů kolem údolí (v každém právě jedna
  ovečka). Je na rovné a suché louce, nejméně 4 m od kmene, mimo silnice, řeky, písek a mosty. Leží 150 m od salaše,
  80 m od statků, majáku, draka a balonu, 40 m od doupat a 160 m od ostatních oveček. Když se v jednom směru žádné
  místo nenajde, hledá se kdekoli jinde.
- **Dojet se tam dá** (`TrackingPath`): ke každé ovečce se najde cesta autem od nejbližší silnice za starým údolím,
  stejnou A* jako u stop, nejvýš 1,8× delší než přímka.

## Hra

1. Od začátku hry visí nad Bohoušem žlutá značka (dopis od pošťáka není potřeba) a jeho
   panel (`GameState.ShepherdStatus` 36, `GameUi.ShepherdPanel`) ukazuje ohradu: ovečky doma, ovečky za autem a u
   ztracených obrázky hranic, ke kterým utekly (moře, hory, řeka, les, v rohu kraje dvě). Zelená fajfka
   (`UiIntent.SheepStart`) pošle dítě hledat a ovečky se objeví na loukách. Co dělat, řekne Bohouš nahlas.
2. Ztracená ovečka se pase a zvoní zvonečkem, který je slyšet zdaleka. Rohový panel (`GameUi.SheepHud`) ukazuje šipku a
   vzdálenost k nejbližší ztracené ovečce a kolik oveček jde za autem a kolik je doma.
3. Když auto přijede na 9 m (`SheepHerd.FindRadius`), ovečka zabečí a jde za autem. Ovečky jdou po dráze auta v řadě
   po dvou (`SheepHerd.Slot`) a běží nejvýš 12 m/s (`SheepHerd.Run`), auto jede až 15 m/s. Když jede auto rychleji,
   ovečky zaostávají a panel napíše „Pomalu, ovečky nestačí!“. Ovečka, která zůstane 60 m pozadu (`StrayDistance`),
   se zastaví, bečí a čeká. Šipka pak ukazuje nejdřív k ní a stačí pro ni dojet zpátky.
4. Když za autem jdou aspoň tři ovečky nebo už žádná ztracená nezbývá, šipka ukazuje k salaši a Bohouš má žlutou
   značku. V jeho panelu je tlačítko „Zahnat do ohrady“. Po správné odpovědi na otázku (`Departure.Sheep`) běží ovečky
   jedna po druhé k ohradě, přeskočí ji a nad každou se ukáže, kolikátá je doma.
5. Po osmé ovečce je stádo doma. Bohouš pak v panelu ukazuje plnou ohradu a za autem jezdí pejsek Alík.

## Odměny

| za co | odměna |
|---|---|
| každá ovečka zahnaná do ohrady | 5 mincí (`SheepPlans.CoinsPerSheep`) |
| celé stádo doma | 25 mincí navíc (`FlockCoins`) |
| osm oveček | Ovčácký pejsek Alík |

## Kód (podle `CLAUDE.md`)

- **Plán a stav** (`Valley`): `World.Sheep.cs` (místa oveček, ohrada, stavba stáda), `SheepHerd.cs` (stádo bez uzlů
  scény: kdo je ztracený, kdo jde za autem, kdo čeká a kdo je doma, dráha auta a jak daleko za ním které ovečka jde).
- **Modely** (`Buildings`): `SheepFlock` (osm oveček, jejich pasení, chůze, čekání, skok přes ohradu, zvonečky a
  bečení), `SheepPlans` (jména, mince, místa v ohradě).
- **Hra** (`App`): `Game.Sheep.cs`, systém `sheep` v `Game.Systems.cs`, panel v `Game.Panels.cs`,
  `UiIntent.SheepStart` a `UiIntent.SheepPen`, `Departure.Sheep`, `Texts`.
- **Rozhraní** (`Ui`): `GameUi.Sheep.cs`, tedy panel s ohradou a rohový panel se šipkou. Obrázek ovečky kreslí
  `DeliveryPicture` (`sheep`).
- **Uložení**: sekce `SaveData.Sheep` (`Started`, `Home`, `Following`) s pravidlem `HasInvalidSheep`. Pravidlo odmítne
  například ovečku doma i za autem zároveň, nalezenou ovečku před začátkem hledání nebo otázku u ohrady bez oveček za
  autem. Kde ovečky za autem právě jsou, se neukládá: po načtení stojí hned za autem. Hry uložené před ovečkami se
  načtou s nulami.
- **Kontroly**: `sheep-checks` kontroluje:
  - místa oveček: osm směrů, louky bez kmenů a vody, odstupy, cesta autem od silnice;
  - pravidla uložení a starou hru;
  - Bohoušův panel a značku bez dopisu, nalezení ovečky, chůzi za autem, zaostávání a čekání při rychlé jízdě;
  - otázku u ohrady, skok do ohrady, mince, poslední ovečku, pejska a plnou ohradu.

  Dále ovečky hlídají `world-fingerprint-checks`, `content-checks` a `contract-checks`. Obrázky:
  `./run.ps1 -Capture -View sheep-herd -Seed 42` (a `sheep`, `sheep-panel`, `sheep-lost`, `sheep-fold`).

## Další nápady

- Pes Bohouše, který by pomáhal stádo hlídat a štěkal na ovečku, která zaostává.
- Stříhání vlny po návratu a vlna jako náklad pro novou tkalcovnu ve vesnici.
- Noc: ovečky, které se v noci ztratí, svítí odrazkou na zvonečku.
