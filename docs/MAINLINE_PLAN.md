# Návrh: velká trať přes údolí

Další obsah pro velkou mapu, po hradu na kopci (`docs/CASTLE_PLAN.md`). Cesta do nejvzdálenějšího rohu trvá asi 100 s
jízdy. Tahle stavba velkou mapu využije a zároveň ji zpřístupní: je to první stavba, která roste **krajinou**, ne na
jednom místě. Oddíl „Jak je to postavené“ na konci říká, v čem se hotová hra od návrhu liší.

## Nápad v jedné větě

U velkého nádraží na okraji starého údolí čeká **jezevec strojvůdce Kuba** a chce postavit **velkou trať ke třem
koncovým stanicím**: k horám, k moři a k hraniční řece. Dítě mu přívěsem vozí štěrk, prkna, cihly a sklo a **koleje,
které ukuje nová kovárna z kamení**. Každý náklad se vyklopí **na konec rozestavěné trati**, takže dítě vidí, jak trať
roste krajinou. Hotovým ramenem pak vlak sveze auto na plošinovém voze: jízdenkou je jedna otázka.

Proč právě takhle:

- **Velká mapa dostane páteř.** Trať vede podél silnic ven z údolí až k hranicím a ke každé hranici se dá dojet vlakem.
- **Staveniště se posouvá.** Dodávka se nevyklopí vždy na stejné místo: jeden úsek trati se položí tam, kde končí ten
  předchozí. Kolejiště je napřed vyznačené kolíky, takže je vidět, kudy se pojede příště, a na konci hotové trati stojí
  oranžová vlajka tam, kam se veze další náklad.
- **Nový výrobní řetězec.** Kámen se vozí do kovárny u horské stráně a koleje se z ní vozí na trať (viz
  `docs/PRODUCTION_CHAINS.md`).
- **Staví na tom, co ve hře funguje.** Dodávky jsou `ITripTarget`, zboží se bere ze závodů, kámen z lomů, otázku na
  naložení kolejí klade Kuba z dálky jako kněžna u kamene.

## Postavy

- **Jezevec strojvůdce Kuba** (`EngineerRole`, `ActorIds.Engineer` 85): stojí na nástupišti velkého nádraží, při jízdě
  sedí v kabině a na konečné stojí na nástupišti terminálu. Žlutou značku má, dokud je co stavět (jen když přívěs zrovna
  není na cestě).
- **Krteček Pražec** (`CollectorPetKind.Mole`): čeká na nástupišti a po dokončení celé trati jezdí za autem; kdysi prý
  „pomáhal kopat tunel“.

## Místo

`World.PlanMainLine` běží po hradu, bez náhodných čísel. Místa drží nový otisk `Tests/Worlds/seed-N.railway2.txt`, takže
se nic, co v údolí už bylo, nepohne. Malá železnice u hřiště zůstává, jak je.

- **Velké nádraží:** vedle první silnice, která vede ze starých silnic ven na okruh, tak daleko od ní, aby bylo mimo
  staré údolí, na rovném místě bez cest, dvorů a stromů (`LineStationFits`). Nástupiště je na straně k silnici, budova
  nádraží naproti přes koleje.
- **Koncové stanice:** vedle silnice k hranici, kus před statkem na jejím konci, nástupištěm k silnici.
- **Kovárna:** vedle horské stráně, kde se nakládá kámen pro draka.
- **Trať** vede z nádraží ven a od každého ramene se odpojí další. Hledá se nejlevnější cesta po terénu (A* po buňkách
  10 m), která se vyhne všemu naplánovanému i postavenému, vodě, strmým svahům a stromům starého údolí, drží se krajnic
  silnic a přes údolní řeku jde po pilířích. Cesta se pak narovná a zaobluje. První rameno vede od nádraží k horám,
  další dvě odbočují z trati, která už je naplánovaná.
- **Terén se nemění.** Trať jde po náspu a v hlubších prohlubních a přes řeku po pilířích, bez kolize, takže auto přes
  ni jezdí. Rostliny, tráva, věci u cesty a divoká zvířata půdorys vynechají přes `InMainLineZone`, kterou se ptá
  `InBorderfolkZone`; květiny a kameny starého údolí se skryjí.
- **Údolí bez místa** trať nemá. `mainline-checks` na 40 seedech ověří, že místo najde skoro každé z nich, a seedy
  otisků (1, 42, 7421) ho mít musí.

## Hra: sedmnáct dodávek

Panel Kuby (`GameState.MainLineStatus`, `GameUi.EngineerPanel`) ukáže vlak, sedmnáct nákladů (dovezené jasně), kovárnu s
kolejemi, které drží, a zelenou fajfku, která pošle přívěs pro další náklad (`TripKinds.MainLine`,
`Departure.MainLine`). Panel je pro děti, které neumějí číst: náklad je nakreslený a co dělat, se říká nahlas.

| díl | dodávky | co přibude |
|---|---|---|
| nádraží | štěrk, cihly | násep s nástupištěm, budova s přístřeškem |
| rameno (3×) | štěrk, prkna, **koleje** | tři úseky trati, každý od konce předchozího |
| | cihly | koncová stanice |
| | sklo | lampy a semafory, rameno je otevřené |

- **Koleje** vyrábí kovárna. Zboží se při naložení odebere z kovárny jako ze skladu závodu. Když kovárna koleje nemá,
  zelená fajfka pošle přívěs s **kamenem** (z lomů jako každý kámen) **do kovárny**. Jeden náklad kamene dá dvě koleje
  (`MainLinePlans.RailsPerStone`), kovárna drží nejvýš čtyři. Koleje se nakládají u kovárny, otázku klade Kuba z dálky.
- **Zboží** se bere ze závodů ve vesnici. Když je závod prázdný, hra pošle dítě nejdřív pro surovinu
  (`BeginMissingSupply`), stejně jako u hradu a zoo.
- **Odměna:** 10 mincí za nádraží, 20 za každé rameno a 40 za celou trať, krteček Pražec.
- **Údolí bez závodů, skládky klád nebo sklárny** má trať hotovou od začátku.

## Jízda vlakem

Hotové rameno (`MainLinePlans.ArmDone`) má v panelu tlačítko s obrázkem své hranice. **Jízdenka je jedna otázka**
(`Departure.MainLineRide`), pak auto stojí na plošinovém voze, Kuba sedí v kabině a kamera pluje za vlakem po trati
(`FollowCamera.Ride`, `Vehicle.Carry`, jako na pramici). Vlak kouří, houká a mele koly. Jízda trvá nejvýš 42 s a pedály
ji po 4 s zkrátí. Na konečné se auto postaví na nádvoří před stanicí a Kuba vystoupí na nástupiště; jeho panel tu má
jedno tlačítko, cestu zpátky na velké nádraží, bez otázky. Když auto od konečné odjede, vlak i Kuba se vrátí na nádraží
sami.

## Uložení

`SaveData.MainLine`: `Stage` (0 až 17 dodávek) a `Rails` (koleje v kovárně, 0 až 4). Pravidlo `HasInvalidMainLine` v
`SaveRules.All` (žádné koleje, než je někdo chce, otázky Kuby jen před cestou, před zbožím, které chybí, a otázka na
naložení kolejí). Cesty přívěsu mají druhy `mainline` a `mainline-forge`. Jízda ani pobyt na konečné se neukládají:
uložená hra z vlaku se nahraje s autem na nádvoří velkého nádraží a Kubou na nástupišti. Uložená hra z doby před tratí
se nahraje bez ničeho z toho (`LegacySaves.AddedAfterVersion5`). `SaveStore` ani `SaveDataV4` se nemění a verze uložené
hry zůstává 5, protože nová sekce má výchozí hodnoty.

## Kód

- `Buildings`: `MainLine.cs` (trať, nádraží, lampy, kolíky a vlajka), `MainLine.Trips.cs` (`ITripTarget` a
  `LineForgeTrip`), `MainLinePlans.cs` (dodávky, mince), `LineForge.cs`, `LineTrain.cs`.
- `Valley`: `MainLineSites.cs` (`LineStationSite`, `LineForgeSite`, `LineRoute`, `LineSite`), `World.MainLine.cs`
  (plánování, `InMainLineZone`).
- `Characters`: `EngineerRole` v `ActorRoles.cs`, ID v `ActorIds.cs`, `Character.Engineer.cs`, krteček v
  `CollectorPet.World.cs`.
- `App`: `Game.MainLine.cs` (panel, dodávky, kovárna, jízda), systém `mainline` v `Game.Systems.cs`, panel v
  `Game.Panels.cs`, tlačítka v `UiIntent.cs` a `Game.Intents.cs`, texty v `Texts.cs`. `Ui`: `GameUi.MainLine.cs`.
- `Diagnostics`: `mainline-checks` v `DiagnosticSuites.cs` na seedech otisků a `Railway2Lines` pro
  `seed-N.railway2.txt`.
- Snímky: `run.ps1 -Capture -View line` (hotové nádraží), `line-0`, `line-2`, `line-5`, `line-9`, `line-14` (konec trati
  po tolika dodávkách), `line-far`, `line-night`, `line-forge`, `line-panel`, `line-panel-4`, `line-panel-9` a
  `line-ride`.

## Jak je to postavené

Od návrhu výše se hotová hra liší takhle:

- **Nádraží stojí na okraji starého údolí, ne uprostřed vesnice.** Silnice ven z vesnice vedou ke spojkám na okruh a
  nádraží stojí u první z nich, za posledními domky. Staré údolí je plné věcí, mezi které by se velká trať nevešla.
- **Dodávek je sedmnáct, trať roste po třetinách ramene.** Třetí úsek každého ramene vyžaduje koleje z kovárny. Zboží na
  koncovou stanici a lampy se vyklopí před stanicí.
- **Kovárna není `Works`.** Je to vlastní budova se zásobou kolejí (`LineForge`), protože `Works` sdílí uložení skladů,
  mazlíčky a panel závodů a nová položka by změnila uložené hry. Řetězec je ale stejný: surovina se vozí, zboží se bere
  při naložení a když chybí, hra pošle pro surovinu.
- **Rameno k řece vede po pilířích přes údolní řeku**, kde ji najde nejlevnější cesta. Nikdy nejde přes hraniční řeku,
  moře ani mosty.
- **Semafory a lampy** jsou jen na pohled: lampy svítí v noci jako okna, semafory nemění barvu.
- **Vlak jezdí jen s autem.** Sám po trati nejezdí.
