# Implementační plán: pošta do čtyř koutů a maják na mysu

První obsah pro nové území kolem jádra (`docs/MAP_EXPANSION_PLAN.md`, fáze 5). Fáze 0 až 4 jsou provedené,
kromě pohlednice majáku; kde se provedení od plánu liší, říká to oddíl **Jak to dopadlo** na konci.

**Pošta** rozmístí po okrajích údolí čtyři nové obyvatele a dá hráči důvod objet celé údolí. **Maják** je první stavba
mimo jádro a v noci svítí přes celé údolí. Oba nápady na sebe navazují: k majákáři hráče poprvé přivede dopis.

## Pravidla pro všechny fáze

- **Jedna fáze = jedna větev = jeden PR.** Po každém commitu prochází `./build.ps1 -Test`.
- **Jádro se nehne.** Otisky `seed-N.txt` a `seed-N.glassworks.txt` zůstávají beze změny. Nic nového nevzniká v
  `World.Generate` před `PlanOutlandRoads`.
- **Ani otisk pásu se nemění.** Soubor `seed-N.outland.txt` hlídá silnice pásu, výšky po 25 m, polohy stromů a hash
  zvířat. Nové stavby proto silnice ani terén pásu nepřesunou. Rostliny ustoupí, ale jinak než v jádru: strom nebo keř
  v zóně stavby se při plánování naplánuje jako dosud, jen ho `BuildOutlandBlock` nepostaví. `OutlandTreePositions` se
  tak nezmění. Nové polohy (postavy, usedlosti, maják, lom) hlídá nový otisk `Tests/Worlds/seed-N.coast.txt`. Ten se
  vytvoří jednou nástrojem `write-world-fingerprint` a pak se už nikdy nepřegeneruje.
- **Uložené hry:** každá nová část je nová sekce v `SaveData` s výchozími hodnotami, pravidlem v `SaveRules.All`,
  uložením a obnovením v `Game.Persistence.cs` a záznamem v `LegacySaves.AddedAfterVersion5`. Takhle vznikla i sekce
  `Hunt`. Verze 5 zůstává, `SaveStore` ani migrace se nemění. Nová čísla `GameState`, ID postav a jména v `TripKinds`
  jsou od té chvíle součástí formátu a už se nikdy nepřečíslují.
- **Délka jízd:** jízda tam a zpět má trvat dítěti nejvýš dvě minuty. Kde to nejde, je dlouhá cesta jen jedním směrem
  a bez vozíku. Kontroly měří délku trasy po silnicích (`RouteGuide.RouteLength`). Auto jede 15 m/s, s naloženým
  vozíkem 12,75 m/s (`LoadedTrailerFactor` 0,85).
- Výkon se měří pomocí `./benchmark.ps1` před každou fází a po ní, na stejném místě. Čísla se zapisují do PR.

## Fáze 0 – Navigace po obou sítích silnic

Dnes hráče nikdo za jádro nepošle. `RouteGuide` staví graf jen ze vzorků `World.Routes` (body blíž než 3,2 m jsou
spojené) a šipky by do pásu nikam nevedly. Proto nejdřív:

1. `RouteGuide` postaví graf ze vzorků obou sítí, `Routes.Samples` a `OutlandRoutes.Samples`. Spojka pásu začíná přesně
   na vzorku staré silnice (`PlanJoins` bere body `Routes.Samples`), takže se sítě spojí samy v bodech se vzdáleností 0.
   Kontrola to potvrdí na 42 seedech. Kdyby se někde nespojily, přidá se hrana mezi začátkem spojky a nejbližším
   vzorkem.
2. `destinationRoadDistance` a `Nearest` hledají v obou sítích. Výšku šipek bere `ArrowRoadHeight` z té sítě, na
   které šipka leží.
3. `Retarget` dnes vyhodí výjimku, když je graf nesouvislý. Pás může mít úsek bez spojky do jádra, třeba když
   `PlanJoins` najde méně než čtyři spojky. Kontrola na 42 seedech proto ověří, že je souvislý celý graf, nejen jádro.
4. **Kontroly** jsou nová sada `route-checks` na seedech 1, 42, 7421 a k tomu 1 až 40 jednorázově:
   - trasa ze středu vesnice vede ke konci každé silnice k hranici;
   - každá šipka leží na povrchu silnice;
   - délka trasy ze vesnice ke každému konci je menší než 1 900 m, tedy asi 2 minuty jízdy.
   Stávající cesty, výpravy a brody musí jezdit stejně: jejich cíle jsou v jádru a nejkratší trasa přes okruh je delší
   než trasa jádrem. `ford-checks`, `works-checks` a ostatní procházejí beze změny.

Hotovo, když všechny sady procházejí a nová kontrola dovede auto po šipkách ke všem čtyřem hranicím.

## Fáze 1 – Čtyři strážci hranic

Na konci každé silnice k hranici (`PlanBorderRoads`, poslední bod silnice, 42 m před okrajem hry) stojí usedlost a
před ní postava. V této fázi strážci jen pozdraví a řeknou něco o svém kraji. Úkoly přidají až fáze 2 a 3.

| hranice | postava (návrh) | usedlost |
|---|---|---|
| moře (jih) | **Tuleň majákář Kryštof** | chaloupka na mysu nad pláží, vedle místo pro maják |
| hory (sever) | **Kamzík pastýř Bohouš** | salaš s ohrádkou a zvonkem |
| hraniční řeka (`Side`) | **Bobr převozník Vašek** | chaloupka s mólem a pramicí u břehu |
| les (`−Side`) | **Sova hajná Božena** | hájovna s parožím nad dveřmi a krmelcem |

Jména a zvířata jsou návrh a čekají na potvrzení. Vybraná jsou tak, aby se nepletla s postavami, které už ve hře jsou.

1. **Umístění** (`World.Borderfolk.cs`, `PlanBorderfolk`): běží hned po `PlanOutlandRoads`, s vlastním generátorem
   (`Settings.Seed + 7801`). U každého konce silnice zkusí několik míst na boku silnice:
   - v rovině (sklon nejvýš asi 0,25);
   - mimo vodu, pláž s mořem a další silnice;
   - uvnitř `ValleyBounds` s okrajem 8 m.
   Na moři stojí usedlost ještě na trávě nad pláží, maják pak na kraji pláže.
2. **Chybějící silnice:** `PlanBorderRoads` silnici vynechá, když paprsek nenajde okruh. Kontrola na 42 seedech ověří,
   že silnice ke všem čtyřem hranicím existují vždy. Kdyby ne, strážce té hranice stojí u bodu okruhu nejblíž ke své
   hranici a hra s ním počítá jako s ostatními.
3. **ID a role:** `ActorIds.FirstBorderKeeper` = 74 až 77, v pořadí moře, hory, řeka, les. `BorderKeeperRole` s polem
   `Border` dostane jednu ikonu za každou hranici. Jména, pozdravy a barvy jsou v `CharacterCatalog.BorderKeepers`.
   Postavy se registrují až za lovcem a `contract-checks` dostane jejich čísla.
4. **Zóny:** `InBorderfolkZone(p)` vrací obdélník usedlosti a příjezd k ní. V zóně se nepostaví rostliny pásu
   (`BuildOutlandBlock` je přeskočí), neroste tam tráva (`Grass.CanGrow`) a nestojí tam věci u cest. Zvířata pásu se v
   ní nezastaví (`Wildlife.Safe`). Zvíře, které v ní začíná, se při startu přesune ven. Hash zvířat v otisku pásu se
   počítá z plánu, takže se nezmění. To je potřeba hned v této fázi ověřit.
5. **Modely** (`Art`, vlastní metoda pro každou usedlost) jsou z kvádrů, válců a koulí jako lovcova chata. Na dálku
   (`DistantDetails`) je vidět jen obrys. Do `StaticMeshes` se usedlosti sloučí stejně jako vesnice.
6. **Otisk:** `Tests/Worlds/seed-N.coast.txt` hlídá polohu a směr každé usedlosti a strážce a také místo pro maják a
   pobřežní lom. Ty se naplánují už teď, i když se postaví až ve fázi 3, aby se otisk později nemusel měnit.
   `world-fingerprint-checks` porovná i tento soubor.
7. **Kontroly** jsou nová sada `borderfolk-checks` na seedech otisků a jednorázově na 42 seedech:
   - čtyři strážci stojí u čtyř různých hranic, uvnitř údolí, na suchu a v rovině;
   - k žádnému nevede trasa delší než 1 900 m;
   - auto k nim dojede a otevře se rozhovor;
   - v zóně usedlosti se nepostaví žádný strom ani keř;
   - otisky jádra a pásu jsou beze změny.

Tato fáze nemá uložený stav. Staré hry dostanou strážce automaticky, protože se odvozují ze seedu.

## Fáze 2 – Pošta

**Hra:**
1. Na návsi stojí **pošťák** s poštovní schránkou. Návrh: Ježek pošťák Pepa, `ActorIds.Postman` = 78.
2. Rozhovor otevře panel s poštovní brašnou a albem známek. Zelená fajfka (`UiIntent.TakeLetter`) dá hráči **jeden
   dopis** pro jednoho strážce. Vozík se nepotřebuje.
3. Na HUD se ukáže obálka s obrázkem adresáta a jeho kraje (maják, hory, řeka, les). Šipky vedou ke strážci (fáze 0).
4. Po předání strážce položí otázku. Bere se z běžného střídání zapnutých druhů otázek, stejně jako u předání nákladu
   kamarádovi u cesty (`Handover`). Po správné odpovědi hráč dostane **známku** do alba a mince.
5. Strážce dá hráči **odpověď**: dopis pro jednoho obyvatele vesnice (`Village.Residents`). Cesta zpátky tak není
   prázdná. Odpověď přinese další mince a poděkování obyvatele.
6. Další dopis dá pošťák, až se hráč vrátí. Pošta se tak nikdy nespustí sama, stejně jako poklady u kapitánky.

**Album:** pro každého strážce 3 známky, celkem 12. Komu je další dopis, určuje seed a počet doručených dopisů. Dopis
dostane nejdřív ten strážce, kterému zatím chybí nejvíc známek, takže hráč objede všechny čtyři hranice dřív, než
některou navštíví podruhé. Odměny:
- za první známku od každého strážce (4 cesty): **poštovní holub**, nový `CollectorPetKind` připojený na konec
  výčtu, který letí za autem;
- za plné album: **poštovní brašna** do garáže, doplněk, a 30 mincí;
- po plném albu chodí dopisy dál, už jen za mince. Kreslí se na nich čtvrté známky bez sbírky.

**Kód** (podle `CLAUDE.md`):
- `Post` (`Tasks`, bez uzlů scény) drží, kolik dopisů kdo dostal, který dopis se právě nese a jestli je to odpověď a
  pro koho. `Game` ho vlastní a předává mu volání jako u `TreasureHunt`.
- `SaveData.Post` (`PostSave`): `Delivered` (int[4]), `Carrying` (−1, nebo index strážce), `Reply` (−1, nebo ID domu)
  a pravidlo `HasInvalidPost`. Pravidlo pozná přeplněné album, neznámého adresáta a odpověď do domu, který neexistuje.
- `GameState.PostStatus` = 30 je panel pošťáka a `GameState.LetterHandover` = 31 je předání u strážce. Oba se
  registrují v `Game.Panels.cs` a obnovují po načtení.
- `PostmanRole`. `BorderKeeperRole` předá dopis, když je hráč nese pro něj, jinak jen pozdraví.
- `UiIntent.TakeLetter` a `UiIntent.CloseAlbum` se obsluhují v `Game.HandleIntent`. Panel pošťáka a obálka na HUD jsou
  v `GameUi.Post.cs`.
- Texty jsou v `Texts.cs`. Jména a věty strážců jsou v `CharacterCatalog`.
- **Kde stojí pošťák:** na návsi už stojí starostka, světlař a kapitánka. Pošťák se postaví stejně jako oni, v rámci
  návsi a bez náhodných čísel, a víc než `InteractionRange` od nich. Když na návsi nebude místo, stojí před obchodem
  (`TownPlace`) uvnitř země, kterou si obchod už rezervuje. Ověří se to jako v `mayor-checks` (`Village.IsReserved`)
  na 42 seedech. Ve fingerprintu jádra se nic nezmění, protože ten ID od 64 výš vynechává.

**Kontroly** jsou nová sada `post-checks`:
- celý oběh: pošťák, cesta ke strážci, otázka, známka, odpověď, obyvatel, mince;
- pořadí adresátů obejde všechny čtyři strážce dřív, než se některý zopakuje;
- holub přijde po první známce od každého strážce a brašna po plném albu;
- uložení a načtení uprostřed cesty tam i zpět obnoví obálku i šipky;
- poškozená sekce `Post` neprojde přes `SaveRules`.

`unit-checks` přečte nové pravidlo a `save-checks` starou hru bez sekce `Post`.

## Fáze 3 – Maják

**Hra:**
1. Po prvním doručeném dopisu majákář řekne, že by na mysu rád postavil maják. Jeho panel pak ukazuje stavbu stejně
   jako lovec chatu.
2. Maják se staví z **osmi nákladů**. Většina z nich je krátká, protože kámen se láme přímo u moře:

| náklad | odkud | co přibude |
|---|---|---|
| 1–2 kamení | **pobřežní lom** na okruhu u silnice k moři | základy na skále |
| 3–5 kamení | pobřežní lom | kulatá věž, třikrát o patro výš |
| 6 prkna | pila v jádru | schody a ochoz |
| 7 cihly | cihelna v jádru | strážní domek u paty |
| 8 skleničky | sklárna v jádru | lucerna nahoře |

3. Kámen jde přímo bez závodu, stejně jako klády na molo a písek na most. Pobřežní lom je místo k naložení bez dělníka,
   stejně jako první pískovna. Naloží se tam jen pro maják. Jízda z lomu k majáku má být kratší než 500 m. Tři dlouhé
   cesty z jádra jsou jen jedním směrem a jsou až na konci stavby. Když závodu dojde zboží, pošle majákář hráče pro
   surovinu (`BeginMissingSupply`), jako ostatní stavitelé.
4. Za hotový maják dostane hráč 30 mincí, nového mazlíčka **Rackáčka** (`CollectorPetKind`) a vyhlídku: když rozhovor
   s majákářem nabídne „podívat se nahoru“, kamera se na chvíli přesune na ochoz a ukáže pobřeží.

**Kód:**
- `Lighthouse`, `LighthousePlans` (8 nákladů a díly „základy“, „věž“, „ochoz“, „domek“ a „lucerna“) a
  `Lighthouse.Trips.cs` jsou v `Buildings`. `ITripTarget` je podle vzoru `HunterPen`.
- `TripKinds.Lighthouse` = `"lighthouse"` a doplní se do `TripKinds.IsKnown`.
- `GameState.LighthouseStatus` = 32 a `LighthouseRole` dostane majákář (ID 74), jakmile je maják odemčený. Do té doby
  má `BorderKeeperRole`. Role se mění podle stavu, jak to dnes dělají stavitelé.
- Sekce `SaveData.Lighthouse` (`LighthouseSave`) má položku `Stage` (0 až 8) a pravidlo `HasInvalidLighthouse`: stavba
  nesmí začít, dokud majákář nedostal dopis.
- Místo pro maják i lom naplánuje `PlanBorderfolk` už ve fázi 1, včetně zón bez rostlin. Jejich polohy jsou tak v
  `seed-N.coast.txt` od začátku. Fáze 3 už jen staví na místech, která jsou pevně daná, a žádný otisk nemění.
- Pobřežní lom stojí na okruhu, kde se na něj napojuje silnice k moři, v zóně `InBorderfolkZone`.

**Kontroly** jsou nová sada `lighthouse-checks`:
- všech osm nákladů, každý postaví svůj díl;
- pevné části zastaví auto až po postavení;
- trasa z lomu k majáku je kratší než 500 m;
- uložení a načtení v každém stupni stavby;
- chybějící zboží pošle hráče pro surovinu;
- staré hry bez sekce mají maják nepostavený.

## Fáze 4 – Světlo majáku

1. Za soumraku a v noci (`Weather.TimeOfDay`) se lucerna rozsvítí a otáčí se z ní **paprsek**. Je to průsvitný kužel
   s aditivním shaderem, který slábne do dálky, a k tomu malé světlo u lucerny. Paprsek je vidět z celého údolí,
   vesnice i hor: nedostane visibility range z `DistantDetails` a nevrhá stín.
2. Jednou za noc pluje po moři **loďka s rozsvícenými okny**. Jede po pevné dráze daleko za pláží a nemá kolizi.
3. Maják se přidá jako nový cíl k pohlednicím, s vlastním obrázkem a slovem „ma-ják“.
4. **Výkon:** `benchmark.ps1` dostane scénu „náves v noci s majákem“ a přepínač `-Off lighthouse`. Paprsek smí stát
   nejvýš 1 % FPS.

Tato fáze nemá nový uložený stav. Pohlednice se ukládají podle indexu, proto se nová pohlednice připojí na konec seznamu
`Postcards`.

## Pozdější rozšíření (není součástí plánu)

- **Otázka na adresu:** na obálce je slovo (například MAJÁK, LES) nebo jméno rozdělené na slabiky a dítě vybere
  obrázek adresáta. Je to nový druh `IChallenge`, takže i nový typ v uloženém `Quiz.Challenge`. Proto samostatná fáze
  s vlastní kontrolou v `unit-checks`.
- Úkoly ostatních strážců podle vzoru majáku: pastýř a ovečky, převozník a pramice, hajná a krmelec.
- Rallye po okruhu s razítky, se stanovišti u strážců.

## Rizika

- **Zvířata a věci u cest v zónách usedlostí.** Jejich polohy jsou v otisku pásu. Odsunout se smějí jen za běhu, nikdy
  při plánování. Ověřit se to musí hned ve fázi 1.
- **Navigace po dvou sítích** mění kód, na kterém stojí každá cesta ve hře. Stávající sady proto musí projít ve fázi
  0, ještě než přibude jakýkoli obsah.
- **Délka jízd.** Konec silnice k moři může být na některém seedu daleko od vesnice. Kontroly s limitem 1 900 m na 42
  seedech to odhalí dřív, než se to stane hráči. Pomoc by pak byla posunout usedlost blíž, na půl cesty.
- **Plná náves.** Když se pošťák nevejde na náves, postaví se před obchod. Rozhodne o tom kontrola na 42 seedech.

## Jak to dopadlo

Rozhodnutí uživatele: jména vymyslí Claude, odměnou jsou mazlíčci (ne věci do garáže), na obálce není otázka na čtení,
tři dlouhé cesty z jádra k majáku jsou v pořádku. Proti plánu se změnilo:

- **Jména.** Strážci jsou Medvěd majákář Kryštof (moře, ID 74), Zajíc pastýř Bohouš (hory, 75), Liška převoznice
  Vendula (řeka, 76) a Medvědice hajná Božena (les, 77). Pošťák je Zajíc pošťák Pepa (78).
- **Pošta má vlastní budovu.** Žlutý domek s modrým pruhem, schránkou a lavicí na balíky stojí u silnice na kraji
  vesnice, pošťák před ním (`World.PostOffice.cs`, `Buildings/PostOffice.cs`). Místo se hledá bez náhodných čísel po
  lovcově ohradě a před rostlinami, nejblíž návsi, a celé leží do 90 m od nejbližšího domu: skrytí kamarádi, poklady,
  světlušky i stáda drží 100 m od každého domu (`Village.TaskAnimalClearance`), takže se nic z nich nepohne. Rostliny,
  tráva, věci u cest a divoká zvířata místo vynechají až po svém losování. Na všech třech seedech kontrol stojí pošta
  17 m od středu návsi. Kdyby se nikde nevešla, stojí pošťák na návsi se schránkou (`Village.Postman.cs`).
- **Stavy hry:** `KeeperStatus` = 30 (panel strážce), `PostStatus` = 31 (album u pošťáka), `LighthouseStatus` = 32.
  Předání dopisu je běžná otázka u strážce, ne vlastní stav. `UiIntent.CloseAlbum` není potřeba, panel zavírá tlačítko
  zpět.
- **Odměny:** holub (`CollectorPetKind.Pigeon`) za známku od každého strážce, plné album 30 mincí, každý dopis 6 a
  každá odpověď 4 mince. Za hotový maják 30 mincí a racek (`CollectorPetKind.Gull`). Brašna do garáže ani vyhlídka z
  ochozu nejsou; dokončení majáku ukáže kamera.
- **Majákář zůstává `BorderKeeperRole`.** Jakmile dostal dopis, jeho panel ukazuje stavbu. U lomu klade otázku na
  naložení kamene on (`ITripTarget.SourceFor`), proto ji jeho role rozliší podle toho, zda auto stojí u lomu.
- **Jízda z lomu k majáku** měří 221 až 256 m (kontrola hlídá méně než 1 200 m). Údolí bez sklárny, pily nebo cihelny má
  maják postavený od začátku a nic se k němu nevozí.
- **Pohlednice majáku (fáze 4, bod 3) není udělaná.** Pravidlo `HasInvalidWorldProgress` přijímá jen dva počty
  pohlednic a hledání vyhlídky zná jen silnice jádra; nová pohlednice je proto samostatný krok.
- Kontroly jsou sady `borderfolk-checks`, `post-checks` a `lighthouse-checks` (seedy 1, 42 a 7421).
