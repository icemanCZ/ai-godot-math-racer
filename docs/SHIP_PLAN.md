# Návrh: loď Albatros a Želví ostrov

**Stav:** hotovo (28. 11. 2026): všechny čtyři kroky postupu jsou postavené v jedné větvi; co dopadlo jinak, říká
oddíl „Jak to dopadlo“ na konci.

Další obsah pro velkou mapu, po velké trati přes údolí (`docs/MAINLINE_PLAN.md`). Moře na jihu má dnes maják na mysu,
zátoku mušlí a ptačí ostrůvek v deltě, ale po moři samém se nejezdí: pramice přívozu pluje po hraniční řece a noční
loďka u majáku je jen na pohled. Teď na pobřeží vyroste **velká plachetnice** a dítě ji pak **samo kormidluje** po
otevřeném moři k ostrovu za obzorem.

## Nápad v jedné větě

Na pláži v zátoce stojí stará **loděnice** se skluzem, na kterém leží jen kýl. U něj čeká **mrož kapitán Vojta** a sní
o lodi, která doplují „až tam, kde se moře dotýká nebe“. Dítě mu přívěsem naváží klády, prkna, sklo, látku na plachty
a **kotvu ukutou v kovárně velké trati** a ze **čtrnácti dodávek** vyroste loď **Albatros** se dvěma stěžni. Po
poslední dodávce se o příď rozbije láhev a loď **sjede po skluzu do moře**. Pak se po jedné otázce (lodní lístek) auto
postaví na palubu, dítě vezme **kormidlo** a pluje podle bójí kolem pobřeží na **Želví ostrov**. Tam želvy hlídají na
pláži **vzácnosti** do kapitánovy kajuty. Za plnou kajutu si na příď lodi sedne **lodní kocourek Kormidlík**.

Proč právě takhle:

- **Je to největší věc na pobřeží.** Loď je dlouhá asi 30 m a stěžně měří kolem 20 m. Je vidět z útesů, od majáku, z
  hradu i z vlaku, který jede k moři. Den po dni je vidět, jak roste: kýl, žebra, trup, paluba, kajuta, stěžně a
  plachty.
- **Nová činnost: kormidlování.** Vlak, pramice i balón vezou dítě po pevné cestě. Loď je první dopravní prostředek,
  který po velké mapě řídí samo dítě: otáčí kormidlem a pedálem nafukuje plachty. Kdo kormidlovat nechce nebo
  nedokáže, toho loď doveze sama, jen pomaleji.
- **Využije to, co už stojí.** Prkna a klády jsou z pily a ze skladu dřeva, sklo ze sklárny, plachty jsou z látky
  farmáře Tondy (dnes ji chce jen mlýn) a kotvu ukuje kovárna u horské stráně, která dosud dělala jen koleje. Loděnice
  stojí co nejblíž koncové stanici trati u moře, takže se k ní dá dojet vlakem.
- **Vrací se ráda.** Každá plavba přiveze z ostrova jiné vzácnosti a bóje dají pár mincí, jako branky rallye.

## Postavy

- **Mrož kapitán Vojta** (`CaptainRole`, `ActorIds.Captain` 86, `CharacterCatalog`): má modrou čepici s kotvou a
  dýmku, ze které stoupají bublinky místo kouře. Stojí u skluzu, po spuštění lodi na molu loděnice, na plavbě vedle
  kormidla a na ostrově na jeho molu. Žlutou značku má, dokud je co stavět (jen když přívěs zrovna není na cestě), a pak
  dokud kajuta není plná. Z dálky mu značka stoupne nad stěžně (`Character.RaiseFarMarker`, jako balonářce).
- **Ježek tesař Hoblík** (jen na pohled, jako veverka Šlapka u jeřábu hradu): s každou dodávkou vyleze na lešení a
  zatluče kus trupu. Po spuštění lodi sedí na konci mola a chytá ryby.
- **Želvy ze Želvího ostrova** (jen na pohled, bez ID): tři velké mořské želvy, každá sedí v pískovém hnízdě na pláži
  ostrova. Když k ní auto dojede, kapitán položí otázku z dálky, jako kněžna u kamene. Po správné odpovědi želva odleze
  a pod ní se zatřpytí vzácnost.
- **Lodní kocourek Kormidlík**: mourovatý, s malou kapitánskou čepicí. Za autem nejezdí (rozhodnutí uživatele): po plné
  kajutě sedí na přídi lodi, na kotvišti i na plavbě, a v noci mu svítí oči jako dvě lucerničky. Není to
  `CollectorPetKind`, jen díl modelu lodi, takže se mazlíčci ani jejich uložení nemění.

## Místo

`World.PlanShip` běží po velké trati, bez náhodných čísel. Místa drží nový otisk `Tests/Worlds/seed-N.ship.txt`, takže
se nic, co v údolí už bylo, nepohne. Vzdálenosti jsou pojmenované ve `WorldTuning`.

- **Loděnice** (`ShipSite.Yard`): na pláži jedné ze zátok pobřeží (`MAP_EXPANSION_PLAN.md`, moře). Hledá se po pobřeží
  po 8 m. Místo se hodí (`ShipyardFits`), když je pás pláže asi 50 × 24 m kolmý k vodě mírně skloněný, když je hloubka
  vody alespoň 3 m při odlivu (`Tide`) nejvýš 45 m od konce skluzu a když je místo aspoň 130 m od zátoky mušlí, 150 m od
  majáku, 220 m od ústí hraniční řeky a 80 m od koncové stanice trati u moře. Z vyhovujících míst se vezme to nejblíž
  koncové stanici, a když trať stanici u moře nemá, to nejblíž silnici ke statku strážce moře.
- **Skluz a kozlíky** (`Slipway`): dřevěný skluz od suché pláže do vody, loď na něm stojí na kozlících až do spuštění.
  Vedle skluzu stojí kůlna loďaře s lešením, hromadou prken a sudy.
- **Kotviště a molo** (`Anchorage`, `Pier`): spuštěná loď kotví 30 až 40 m od břehu a stoupá a klesá s přílivem
  (`BorderWater.SetTide`). Molo vede na kůlech z pláže ke kotvišti, jako molo přívozu. Je jen na pohled, auto po něm
  nejezdí; na palubu se auto postaví po lodním lístku (`Vehicle.Carry`).
- **Přístup:** auto sjede k loděnici po pláži jako k mušlím (`Vehicle.SeaDepth`, `World.SeaDepth`). Polní cesta vede
  ze silnice k okraji údolí nad loděnicí (`FindSpur` jako u zoo), kde je kruh pro vyložení nákladu, aby přívěs nemusel
  na písek.
- **Želví ostrov** (`TurtleIsland`): v moři 350 až 500 m za pobřežím, aspoň 400 m od ptačího ostrůvku, aby byl vidět z
  útesů jako tečka s palmami na obzoru. Ovál zhruba 120 × 80 m nad hladinou i při přílivu, s pláží, třemi hnízdy želv,
  hájkem palem (`MultiMesh`) a molem pro loď. Má vlastní model a kolize jako ptačí ostrůvek; terén za hranicí se
  nemění.
- **Plavba** (`ShipRoute`): křivka od kotviště podél pobřeží alespoň 60 m od břehu a pak ven k ostrovu, se **šesti až
  osmi bójemi** po 80 až 110 m. Cesta je naplánovaná tak, aby plavba trvala 60 až 90 s, když dítě jede plnými plachtami.
- **Údolí bez místa** loď nemá, podobně jako údolí bez horské stráně nemá draka. `ship-checks` na 40 seedech ověří, že
  místo najde většina z nich, a seedy otisků (1, 42, 7421) ho mít musí.

Zóna loděnice, mola a cesty k ní (`InShipZone`, ptá se jí `InBorderfolkZone`) drží stranou rostliny, trávu, věci u
cesty a divoká zvířata. Mušle (`World.ShellsOfTide`) se zátoce mušlí drží dál, protože loděnice je od ní daleko.

## Fáze 1: stavba lodi

Panel kapitána (`GameState.ShipStatus` 42, `GameUi.CaptainPanel`) ukáže obrázek lodi, na kterém jsou postavené díly
barevné a ty, co chybí, bledé, náklad další dodávky a zelenou fajfku, která pošle přívěs (`TripKinds.Ship`,
`Departure.Ship`, `UiIntent.ShipTrip`). Panel je pro děti, které neumějí číst: náklad je nakreslený a co dělat, se říká
nahlas.

| dodávky | náklad | odkud | co přibude |
|---|---|---|---|
| 1–3 | klády | sklad dřeva | kýl, žebra přídě, žebra zádě |
| 4–7 | prkna | pila | trup po čtvrtinách, od kýlu nahoru |
| 8 | prkna | pila | paluba se zábradlím |
| 9 | prkna | pila | záďová kajuta a kormidlo |
| 10 | sklo | sklárna | okna kajuty a lucerny na zádi |
| 11 | kotva | kovárna | kotva na řetězu a kování |
| 12 | klády | sklad dřeva | dva stěžně a ráhna |
| 13–14 | látka | farmář Tonda | přední a hlavní plachty |

- **Zboží** se bere ze závodů ve vesnici (`Works.Take`). Když je závod prázdný, hra pošle dítě nejdřív pro surovinu
  (`BeginMissingSupply`), stejně jako u hradu a trati.
- **Látka** se nakládá u farmáře Tondy po jeho otázce, jako látka na plachty mlýna (`HaulLoad.Fabric`,
  `World.Works`). Dnes ji Tonda dává jen mlýnu; ve fázi 2 se ověří, že ji umí dát i lodi a že mlýn tím nic neztratí.
- **Kotva** je nový náklad (`HaulLoad.Anchor` 16, obrázek v panelu, kotva na přívěsu v `Trailer.cs`). Ukuje ji
  **kovárna velké trati** (`LineForge`) z jednoho nákladu kamene: když kotva není ukutá, zelená fajfka pošle přívěs s
  kamenem z lomu do kovárny (`TripKinds.ShipForge`, jako `LineForgeTrip`). Kovárna pak kotvu drží na kovadlině, až si
  pro ni přívěs přijede; otázku na naložení klade kapitán z dálky. Kotva se ukládá v `SaveData.Ship`, ne v zásobě
  kolejí `SaveData.MainLine.Rails`, aby se pravidla trati neměnila. Kovárna stojí od začátku, ať je trať postavená,
  nebo ne.
- **Spuštění lodi:** čtrnáctá dodávka zaplatí 50 mincí (`ShipPlans.Coins`). Lešení zmizí, Hoblík rozbije láhev o příď,
  loď sjede po skluzu do vody, zhoupne se a dopluje ke kotvišti. Kamera to chvíli ukáže (`FollowCamera.ShowMoment`).
- **Údolí bez pily nebo skladu dřeva** má loď hotovou od začátku a začíná rovnou plavbou. Údolí bez kovárny (bez horské
  stráně) má kotvu v loděnici od začátku a jedenáctá dodávka se vynechá. Údolí bez farmáře má plachty od začátku a
  dodávky 13 a 14 se vynechají.

## Fáze 2: plavba s kormidlem

Hotová loď: panel kapitána ukáže kajutu se vzácnostmi, kocourka a zelenou fajfku s obrázkem lodi.

1. **Lodní lístek.** Jedna otázka (`Departure.ShipSail`), pak auto stojí na palubě, kapitán stojí u kormidla a kamera
   je za zádí, výš než za autem, aby byly vidět bóje (`FollowCamera.Ride` s novým pohledem `Helm`).
2. **Kormidlování.** Volant (šipky, páka, myš) otáčí kormidlem a loď se pomalu stáčí. Plyn nafoukne plachty a loď jede
   rychleji, brzda plachty svine a loď zpomalí, ale nikdy nezastaví. Vlny loď houpají, za zádí se táhne brázda a
   kormidlo se na palubě viditelně otáčí.
3. **Bóje.** Červené bóje se zvonečkem ukazují cestu. Bója, kolem které loď propluje blíž než 15 m
   (`ShipPlans.BuoyReach`), cinkne a dá 1 minci. Šipka nad přídí ukazuje k další bóji, takže to zvládne i dítě, které
   neumí číst. Minutá bója nevadí: jakmile ji loď mine, šipka ukáže na další.
4. **Bez rukou.** Když se kormidla 3 s nikdo nedotkne, loď se sama pomalu stočí k další bóji a pluje polovičními
   plachtami (`ShipHelm.Autopilot`). Na ostrov tak dopluje i dítě, které jen sedí a kouká.
5. **Mělčina.** Loď nesmí do vody mělčí než 2,5 m (`World.SeaDepth`) ani za okraj moře, které hra kreslí. Když se k
   mělčině blíží, kapitán zavolá „Pozor, mělčina!“, kormidlo zadrhne a příď se sama stočí zpátky na volnou vodu.
   Ztroskotat ani uvíznout nejde.
6. **Delfíni.** Dva delfíni plavou vedle přídě a vyskakují, když loď pluje plnými plachtami. Jsou jen na pohled.
7. **Přistání.** Blíž než 40 m od mola ostrova se loď k molu dotočí sama a auto sjede na pláž (`Vehicle.Carry` skončí).

Pohyb lodi počítá třída bez scény (`ShipHelm`: poloha, směr, rychlost, vstup a hloubka vody, a z toho další krok), takže
`ship-checks` může plavbu projet bez okna a s pevným vstupem (bez rukou, plné plachty, stále doleva do mělčiny).

## Fáze 3: Želví ostrov a kapitánova kajuta

- **Na ostrově** smí auto jezdit jen po ostrově (`World.SeaDepth`, jako na ptačím ostrůvku; `World.IslandOpen` se
  rozšíří na dva ostrovy). Kapitán čeká na molu.
- **Dvě želvy každé plavby** sedí ve dvou ze tří hnízd. Když k želvě auto dojede na 4 m, kapitán se zeptá z dálky
  (`Departure.ShipTurtle`). Po správné odpovědi želva odleze k vodě a pod ní se zatřpytí vzácnost: 2 mince, za první
  svého druhu 5 navíc. Vzácnost se objeví v kajutě.
- **Osm vzácností** (`ShipPlans.Souvenirs`): lahev se vzkazem, kompas, lodní zvon, dalekohled, kokos, ananas, korál a
  truhlička s dukáty. Kokos a ananas jsou časté, truhlička vzácná (`ShipPlans.Weights`). Mušle jsou sbírka zátoky,
  proto tu žádné nejsou.
- **Losování:** vzácnosti plavby se spočítají z údolí, čísla plavby a druhů, které dítě už má, takže se nemusí ukládat.
  První z obou je vždy druh, který v kajutě ještě chybí, takže plná kajuta je nejpozději po čtyřech plavbách.
- **Kajuta** je obrázek police v panelu kapitána (`SouvenirPicture`): osm míst, bledá ta, která ještě chybí, a s počtem
  ta ostatní. Na lodi je to i doopravdy: ve světlých oknech kajuty jsou vzácnosti vidět na polici.
- **Domů.** Panel kapitána má na ostrově dvě tlačítka: želvy (šipka k nejbližší, která ještě sedí) a loďku domů
  (`UiIntent.ShipHome`), bez otázky. Domů se pluje rovnou ke kotvišti, bez bójí. Kormidlovat se smí, bez rukou pluje
  loď sama a pedály po 4 s plavbu zkrátí. U kotviště auto sjede na pláž vedle loděnice.
- **Plná kajuta** přinese 30 mincí (`ShipPlans.CabinCoins`) a kocourka Kormidlíka.

## Model lodi a ostrova

- **Loď** (`Buildings/Ship.cs`) je z kvádrů, válců a z trupu, který vznikne protažením obrysu žeber po délce lodi
  (prkna jako `MultiMesh`). Dva stěžně s ráhny, plachty jako mírně vyduté plochy, které se na plavbě nafouknou, kormidlo
  s loukotěmi, kotva na řetězu, lucerny na zádi a vlajka na stěžni, která vlaje ve větru. Podle stupně (`SetStage`) se
  díly ukazují a skrývají, jako u `MainLine`. Pevné díly blokují auto, až když jsou postavené, jako u `SitedProject`.
  Drobnosti menší než 0,3 m nedostanou obrys (`World.OutlineScenery`).
- **V noci** svítí okna kajuty a lucerny (emisivní plamen, žádné stínové světlo), jako pochodně hradu. Celá loď a
  ostrov stojí v uzlu `ShipLights`, takže `benchmark.ps1 -Off ship` změří, kolik stojí.
- **Z dálky** se loď kreslí jednoduše (bez lan, zábradlí a luceren za 300 m) a ostrov jako trup s několika palmami.
- **Noční loďka majáku** (`NightBoat`) pluje dál, jen se její cesta drží aspoň 120 m od kotviště, pokud by k němu
  zajela. Když to nejde bez změny staršího otisku, zůstane, jak je.

## Uložení

`SaveData.Ship`: `Stage` (0 až 14 dodávek), `Anchor` (kotva je ukutá a čeká v kovárně), `Voyage` (číslo další
plavby) a `Kinds` (kolik vzácností každého z osmi druhů dítě má). Pravidlo `HasInvalidShip` v `SaveRules.All`
(`OutlandRules`): žádná ukutá kotva po jedenácté dodávce, žádná plavba před spuštěním lodi, žádné vzácnosti bez
plavby. Odjezdy `Departure.Ship`, `ShipSail` a `ShipTurtle` mají svá pravidla v `SaveRules` jako přívoz a trať. Cesty
přívěsu mají druhy `ship` a `ship-forge`. Plavba ani pobyt na ostrově se neukládají: uložená hra z lodi nebo z
ostrova se nahraje s autem na pláži u loděnice (`Game.CarPoseToSave`), vzácnosti nalezené na ostrově už v ní jsou.

Uložená hra z doby před lodí se nahraje bez ničeho z toho; nová pole jsou v `LegacySaves.AddedAfterVersion5`.
`SaveStore` ani `SaveDataV4` se nemění a verze uložené hry zůstává 5, protože nová sekce má výchozí hodnoty. Nové
hodnoty `HaulLoad.Anchor` 16, `GameState.ShipStatus` 42 a `ActorIds.Captain` 86 jsou další čísla v řadě a nikdy se
nepřečíslují.

## Kód

- `Buildings`: `Ship.cs` (model a stupně), `Ship.Trips.cs` (`ITripTarget` a `ShipForgeTrip`), `ShipPlans.cs`
  (dodávky, mince, vzácnosti, váhy), `Shipyard.cs` (skluz, kůlna, lešení, molo), `TurtleIsland.cs`.
- `Valley`: `ShipSites.cs` (`ShipSite`, `ShipyardFits`), `ShipRoute.cs` (cesta a bóje), `World.Ship.cs` (plánování,
  `InShipZone`, vzácnosti plavby).
- `Vehicles` nebo `Tasks`: `ShipHelm.cs` (pohyb lodi bez scény, autopilot, mělčina) a `ShipVoyage.cs` (průběh plavby:
  bóje, přistání, cesta domů).
- `Characters`: `CaptainRole` v `ActorRoles.cs`, `ActorIds.Captain`, `Character.Captain.cs`; kocourek je díl modelu
  lodi.
- `App`: `Game.Ship.cs` (tenký: panel, dodávky, kovárna, plavba a ostrov volají do tříd výše), systém `ship` v
  `Game.Systems.cs`, panel v `Game.Panels.cs`, tlačítka `ShipTrip`, `ShipSail`, `ShipHome` v `UiIntent.cs` a
  `Game.Intents.cs`, texty v `OutlandTexts.cs`. `Ui`: `GameUi.Ship.cs`, `SouvenirPicture`, šipka k bóji v `ShipHud`.
- `Diagnostics`: `ship-checks` v `DiagnosticSuites.cs` na seedech otisků a `ShipLines` pro `seed-N.ship.txt` ve
  `WorldContractChecks.cs`.

## Kontroly

`ship-checks` projde loď vstupy samotné hry: rozhovor s kapitánem, čtrnáct dodávek včetně kamene do kovárny a kotvy,
látku od Tondy, spuštění lodi, uložení a nahrání uprostřed stavby, lodní lístek, plavbu bez rukou až k ostrovu, plavbu
s volantem stále doleva (loď nesmí do mělčiny), bóje a jejich mince, obě želvy a jejich otázky, uložení na ostrově
(auto se nahraje u loděnice) a plnou kajutu s kocourkem. Na 40 seedech ověří, že místo najde většina údolí.
`world-fingerprint-checks` hlídá nový otisk `seed-N.ship.txt` a to, že se žádný starší nezměnil.

## Výkon

Loď má kolem dvou set dílů, prkna trupu a palmy jsou `MultiMesh`, bóje jsou jeden `MultiMesh`. Ostrov leží daleko za
pobřežím a kreslí se jednoduše. `./benchmark.ps1` se změří před a po, na návsi, u loděnice a na plavbě, a `-Off ship`
ukáže, kolik loď stojí.

## Snímky

`run.ps1 -Capture -View ship-0` (kýl na skluzu), `ship-4`, `ship-8`, `ship-12`, `ship` (loď na kotvišti), `ship-launch`,
`ship-night`, `ship-far` (z útesu), `ship-panel`, `ship-panel-9`, `ship-sail` (za zádí s bójí), `ship-island`,
`ship-turtle` a `ship-cabin`.

## Postup

Jedna fáze je jedna větev a jeden PR. Po každém commitu prochází `./build.ps1 -Test`. Fixtury v `Tests/Saves` a staré
otisky v `Tests/Worlds` se nemění.

1. **Místo a model.** `PlanShip`, `InShipZone`, loděnice, loď ve všech čtrnácti stupních, Želví ostrov a cesta s
   bójemi, nový otisk `seed-N.ship.txt` a první `ship-checks`. Výkon před a po.
2. **Stavba lodi.** Kapitán, panel, čtrnáct dodávek, kotva z kovárny, látka od Tondy, spuštění lodi, mince a uložení
   `Stage` a `Anchor`.
3. **Plavba.** `ShipHelm`, lodní lístek, kormidlo, bóje, autopilot, mělčina, delfíni, přistání a cesta domů.
4. **Ostrov a kajuta.** Želvy a jejich otázky, vzácnosti, kajuta v panelu i v oknech lodi, kocourek Kormidlík a
   uložení `Voyage` a `Kinds`.

## Rizika

- **Kam až moře sahá.** Ostrov 350 až 500 m za pobřežím musí ležet uvnitř vodní plochy a terénu, které hra kreslí
  (`BorderWater`). Pokud je okraj blíž, ostrov se přiblíží, ne naopak.
- **Ovládání na moři.** Loď se točí pomaleji než auto a dítě to může zmást. Proto má kormidlo velký úhel, šipka k bóji
  je vždy vidět a autopilot převezme loď po 3 s. Přesná čísla (`ShipPlans.TurnRate`, `Speed`) se ladí na hraní.
- **Látka od Tondy** dnes slouží jen mlýnu. Když by druhé použití měnilo uložené hry mlýna, plachty se udělají jako
  nový náklad s vlastním číslem a stejným obrázkem.
- **Délka cesty.** Loděnice je na pobřeží, asi 100 s jízdy z vesnice. Proto co nejblíž stanici trati u moře, a dodávky
  se vykládají na okraji údolí nad pláží, ne až dole.

## Rozhodnutí uživatele

1. Pedály plachty nafukují a svinují, jak je navrženo výše.
2. Dvě vzácnosti za plavbu stačí.
3. Kocourek Kormidlík za autem nejezdí, sedí na přídi lodi.

## Jak to dopadlo

Všechny čtyři kroky postupu jsou v jedné větvi a `./build.ps1 -Test` prochází; žádná fixtura ani starý otisk se
nezměnily, přibyly jen otisky `seed-1.ship.txt`, `seed-42.ship.txt` a `seed-7421.ship.txt`. Proti plánu:

- **Ostrov je blíž:** 250 až 400 m od pobřeží místo 350 až 500 m, aby celý ležel ve vodě, kterou hra kreslí. Čtyři
  bóje stojí rovnoměrně na cestě a každá je posunutá dál od břehu, až leží v hluboké vodě i při odlivu.
- **Loděnice** stojí 160 až 470 m po pobřeží od usedlosti u moře, co nejblíž stanici trati u moře. Okraj, na který se
  vykládá, leží 16 m uvnitř údolí a celý dvůr loděnice musí být v jeho hranicích (`ValleyBounds`).
- **Mělčina:** kormidlo samo otočí příď od vody mělčí než `ShipPlans.LeastDepth`, takže loď nenajede na břeh a z moře
  neodpluje. Kontroly ověřují, že nejmělčí voda pod lodí zůstane kladná.
- **Spuštění lodi** ukáže kamera (`FollowCamera.ShowMoment`), loď sjede po skluzu a dopluje ke kotvišti. Láhev o příď
  a delfíni z plánu postavení nejsou; zůstávají jako nápad na později.
- **Želví otázka** se klade ve stavu `Exploring`, když auto zastaví u želvy; nezodpovězená otázka se znovu nabídne, až
  auto odjede a vrátí se.
- **Plavba a pobyt na ostrově se neukládají:** po načtení stojí auto zase v loděnici a hnízda na ostrově se znovu
  vylosují ze semínka, čísla plavby a nalezených druhů.

