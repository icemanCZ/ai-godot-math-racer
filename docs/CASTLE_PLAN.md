# Návrh: hrad na kopci

**Stav:** hotovo, všechny tři fáze (`a480c90`, 27. 11. 2026).

Další obsah pro velkou mapu, po přívozu a ptačím ostrůvku (`docs/FERRY_PLAN.md`). Je to první **velká stavba** za
starým údolím: zoo, mlýn, maják i molo přívozu stojí každé na jednom místě a vozí se k nim z kousku. Hrad se staví z
kamene od moře a z hor, ze dřeva lesa a ze zboží závodů ve vesnici. Na konci posílá dítě do všech čtyř koutů mapy.
Všechny tři fáze jsou hotové; oddíl „Jak je to postavené“ na konci říká, v čem se hotová hra od návrhu liší.

## Nápad v jedné větě

Na kopci za okruhem stojí jen zbytky starých zdí a **lvice kněžna Libuše**, která tam „vidí hrad, jehož sláva bude až
k hvězdám“. Dítě jí přívěsem naváží kámen, dřevo a zboží a z **šestnácti dodávek** vyroste hrad se čtyřmi věžemi,
padacím mostem a palácem. Pak dítě na **čtyřech hranicích** postaví hlásky, jejichž ohně v noci svítí až na hrad, a
nakonec **sveze hosty z celého údolí na hostinu**. Za plnou hostinu přiletí **strašidýlko Bubáček**, které v noci
svítí v oknech hradu.

Proč právě takhle:

- **Je vidět z celého údolí.** Hrad stojí vysoko a věže s praporky měří kolem 18 m. Z vesnice je vidět, jak roste: první
  den jen lešení a jeřáb, potom hradby, věže a nakonec vlajky. V noci svítí okna a pochodně na hradbách.
- **Využije celou mapu.** Kámen se vozí od moře (pobřežní lom) a z hor (horská stráň), zboží z vesnice, hlásky stojí na
  všech čtyřech hranicích a hosté bydlí ve všech koutech. Žádná jiná stavba takhle nespojuje všechna místa.
- **Staví na tom, co ve hře funguje.** Dodávky jsou `ITripTarget` jako u zoo a majáku, kámen z lomu nakládá
  `ITripTarget.SourceFor` jako u majáku, hlásky stojí u statků strážců hranic a hosté jsou postavy, které ve hře už
  jsou.
- **Dá se rozdělit na tři fáze**, z nichž každá je hratelná sama o sobě: hrad, hlásky, hostina.

## Postavy

- **Lvice kněžna Libuše** (`CastleRole`, `ActorIds.Castellan` 84): stojí u brány, po dostavbě na nádvoří. Žlutou
  značku má, dokud je co stavět (jen když přívěs zrovna není na cestě), a pak dokud není hostina plná. Z dálky jí značka
  stoupne nad nejvyšší věž (`Character.RaiseFarMarker`, jako balonářce).
- **Veverka jeřábnice Šlapka** (jen na pohled, jako zvířátka na hřišti): běží v kole **šlapacího jeřábu** na
  staveništi. S každou dodávkou zvedne jeřáb kus nákladu na lešení. Po poslední dodávce jeřáb zmizí a Šlapka sedí na
  hradbě.
- **Strašidýlko Bubáček** (`CollectorPetKind.CastleGhost`): bílé, průsvitné, hodné. Za plnou hostinu jezdí za autem, v
  noci jemně svítí a ve tmě občas vykoukne z okna hradu.

Jiná jména: kněžna Lvíčka, kastelán Jezevec Bořek, rytíř Medvěd Bruno. Libuše odkazuje na pověst o kněžně, která
věštila slavný hrad, a dětem se dá vyprávět.

## Místo

`World.PlanCastle` běží po přívozu, bez náhodných čísel. Místo drží nový otisk `Tests/Worlds/seed-N.castle.txt`, takže
se nic, co v údolí už bylo, nepohne. Hrad je z plánů nejvyšší a nejširší, proto:

- **Hledání jako u balónu** (`World.PlanBalloon`): obě strany každé silnice za starým údolím po 8 m. Hrad se vejde
  (`CastleFits`), když je celý půdorys 40 × 40 m mimo silnice, řeky, písek, mosty a místa jiných plánů, alespoň 480 m
  od vesnice a 160 m od statků, majáku, draka, balónu, přívozu, ohrady ovcí a doupat. Rozdíl výšek terénu v půdorysu smí
  být nejvýš 3 m, aby nádvoří bylo sjízdné. Z vyhovujících míst se bere to nejvýš položené, které je alespoň 300 m od
  balónu. Dva nejvyšší body krajiny pak nestojí vedle sebe.
- **Přístup:** polní cesta ze silnice k bráně (`FindSpur` jako u zoo), nejvýš se sklonem `PathMaxGrade`. Kruh pro
  vyložení nákladu leží před bránou, mimo most.
- **Terén se nemění.** Hradby a věže stojí na kamenném soklu, který vyrovná svah dolů k zemi, jako molo přívozu na
  kůlech. Nádvoří je terén, po kterém auto jezdí. Rostliny, tráva, věci u cesty a divoká zvířata půdorys vynechají přes
  `InCastleZone`, kterou se ptá `InBorderfolkZone`. Stromy se dál plánují, takže `OutlandTreePositions` i otisk krajiny
  zůstanou stejné.
- **Údolí bez místa** hrad nemá, podobně jako údolí bez horské stráně nemá draka. `castle-checks` na 40 seedech ověří,
  že místo najde aspoň většina z nich, a seedy otisků (1, 42, 7421) ho mít musí.

## Fáze 1: stavba hradu

Panel kněžny (`GameState.CastleStatus` 40, `GameUi.CastlePanel`) ukáže obrázek hradu, na kterém jsou postavené díly
barevné a ty, co chybí, bledé, a zelenou fajfku, která pošle přívěs pro další náklad (`TripKinds.Castle`,
`Departure.Castle`, `UiIntent.CastleTrip`). Panel je pro děti, které neumějí číst: náklad je nakreslený a co dělat, se
říká nahlas.

| díl | dodávky | odkud | co přibude |
|---|---|---|---|
| základy | 1 štěrk, 2 kámen | štěrkovna, lom | lešení, jeřáb a kamenný sokl kolem celého půdorysu |
| hradby | 3–5 kámen | lom | hradby s cimbuřím, rostou do výšky po třetinách |
| věže | 6–9 cihly, prkna, cihly, prkna | cihelna, pila | čtyři rohové věže se stříškami, jedna za dodávku |
| brána | 10 klády, 11 prkna | sklad dřeva, pila | brána s padacím mostem na řetězech |
| palác | 12 cihly, 13 cihly, 14 sklo | cihelna, sklárna | palác s okny, střecha z tašek |
| praporky | 15 prkna, 16 sklo | pila, sklárna | stožáry, praporky, pochodně ve skleněných lucernách |

- **Kámen** (dodávky 2–5) se nakládá v **pobřežním lomu** nebo na **horské stráni**, podle toho, ke kterému auto
  dojede dřív (`ITripTarget.SourceFor` a `HaulTrip.Source` jako nejbližší místo). Otázku na naložení klade kněžna z
  dálky, jako drak u stráně. Takhle se jezdí jednou k moři a jednou do hor a cesta s vozíkem není zbytečně dlouhá.
- **Zboží** se bere ze závodů ve vesnici (`Works.Take`). Když je závod prázdný, hra pošle dítě nejdřív pro surovinu
  (`BeginMissingSupply`), stejně jako u parcel a zoo.
- **Šestnáctá dodávka** zaplatí 60 mincí (`CastlePlans.Coins`), lešení a jeřáb zmizí, padací most spadne a kamera
  chvíli ukáže hotový hrad (`FollowCamera.ShowMoment`). Do garáže přibude **praporek na auto**
  (`CarAccessory.Pennant`).
- **Údolí bez závodů** má hrad hotový od začátku a začíná rovnou fází 2.

Po dostavbě se padací most spustí, když auto přijede blíž než 25 m, a zvedne se, když odjede. Auto může vjet na
nádvoří, kde stojí studna, stůl na hostinu a kněžna.

## Fáze 2: hlásky na hranicích

Hotový hrad je slepý, nevidí do kraje. Kněžna pošle dítě postavit **hlásku** u statku každého ze čtyř strážců hranic.

- Hláska je dřevěná strážní věžička s košem na oheň, asi 9 m vysoká, vedle dvora statku (`BorderHome`). Místo se
  najde bez náhodných čísel, stejně jako místo majáku vedle statku strážce moře.
- Každá hláska je **jedna dodávka klád** ze skladu dřeva nebo z horské stráně, podle toho, co je nejblíž, a pak
  **jedna otázka u strážce**, která zapálí oheň. Je to krátká práce na každé hranici, ale za všemi čtyřmi se musí
  dojet.
- Pořadí si dítě vybírá samo. Šipky na silnici vedou k nejbližší hlásce, která ještě nehoří.
- Oheň hlásky hoří v noci a je vidět z dálky: jeden aditivní plamen a světlo bez stínu, jako ohniště draka. Když hoří
  všechny čtyři, rozsvítí se o první noci **velký oheň na nejvyšší věži hradu** a od hlásek ke hradu na chvíli přeletí
  čtyři jiskry (signál). Za to je 30 mincí (`CastlePlans.BeaconCoins`).
- Údolí bez silnice k některé hranici má tolik hlásek, kolik je silnic. Fáze skončí, když hoří všechny, které jsou.

## Fáze 3: hostina na hradě

Kněžna chce slavit a pozve celou krajinu za starým údolím. Na stole na nádvoří je **sedm židlí** a každá patří jednomu
hostu:

| host | kde bydlí |
|---|---|
| medvěd majákář Kryštof | statek u moře |
| zajíc pastýř Bohouš | salaš pod horami |
| liška převoznice Vendula | statek u hraniční řeky |
| medvědice hajná Božena | statek u lesa |
| liška balonářka Amálka | balónová stanice |
| sova kartografka Mapka | stánek u cesty ze starého údolí |
| volavka pozorovatelka Klárka | přístaviště přívozu |

Strážci se převezmou z `CharacterCatalog.BorderKeepers` a `ActorIds`. Host, jehož místo v údolí není, se z hostiny
vynechá a hostina má o židli méně. Když u stolu sedí všichni a drakovi se už vylíhl dráček, přiletí na hostinu sám
**drak Kvído** a lehne si na hradbu. Do přívěsu se nevejde.

- **Nová věc: pasažér v přívěsu.** Kněžna dá dítěti pozvánku (`UiIntent.CastleInvite`) pro hosta, který ještě nesedí.
  Dítě pojede s prázdným přívěsem k hostovi, krátká otázka u něj („Pojedeš na hostinu?“) ho posadí do přívěsu a cestou
  zpátky host sedí, mává a občas něco řekne. Na nádvoří vystoupí a sedne si ke stolu. Je to nový druh cesty
  (`GuestTrip`), ne `HaulTrip`, protože se nic nevykládá a host jede místo nákladu.
- Šipky vedou k pozvanému hostu a pak zpátky na hrad (`RouteGuide`, jako `LetterTrip` u pošty).
- Každý host zaplatí 8 mincí. Hosté, kteří sedí u stolu, jedí, ťukají si pohárky a smějí se, když přijede auto.
- **Plná hostina:** po posledním hostu je 40 mincí (`CastlePlans.FeastCoins`), další noc nad hradem bouchne
  **ohňostroj** a přijde strašidýlko Bubáček. Hosté pak zůstanou jen o hostině: po načtení stojí zase doma, u stolu
  sedí jejich obrázky na praporcích nad židlemi.
- Host, který sedí v přívěsu, stojí doma, dokud ho auto nenaloží. Když se uložená hra nahraje s hostem v přívěsu, host
  v přívěsu sedí dál (ukládá se `Carrying`).

## Model hradu

Hrad je z kvádrů, válců a kuželů jako ostatní stavby, s fasádou `Art.Facade` (kámen) a střechami `Art.RoofTiles`.
Hradby jsou silné 2 m a vysoké 6 m, s ochozem a cimbuřím. Věže jsou kulaté, mají průměr 6 m, jsou vysoké 14 m a mají
kuželovou střechu s praporkem, který vlaje ve větru. Palác stojí u zadní hradby a je dvoupatrový,
s věžičkou. Brána má padací most, dvě pochodně a mříž. Pevné díly blokují auto, až když jsou postavené, jako u
`SitedProject`. Drobnosti menší než 0,3 m nedostanou obrys (`World.OutlineScenery`).

- **Staveniště** (`marks`): lešení, šlapací jeřáb s veverkou, hromady kamene, míchačka malty, páska a cedule s
  počtem dodávek. Po poslední dodávce zmizí, jako staveniště zoo.
- **V noci** svítí okna paláce jako okna ve vesnici (`Village.Nightfall`), pochodně blikají (emisivní plamen, žádné
  stínové světlo) a na věži hoří velký oheň, jakmile svítí všechny hlásky. Celý hrad stojí v jednom uzlu
  `CastleLights`, takže `benchmark.ps1 -Off castle` změří, kolik stojí.
- **Z dálky** se hrad kreslí jednoduše (bez cimbuří, praporků a pochodní za 300 m), jako vzdálené stromy.

## Uložení

`SaveData.Castle`: `Stage` (0 až 16 dodávek), `Beacons` (které ze čtyř hlásek mají věžičku a které hoří, dvě pole po
čtyřech), `Guests` (kdo už sedí u stolu), `Carrying` (host v přívěsu, nebo nikdo) a `Feast` (jestli už bouchl
ohňostroj). Pravidlo `HasInvalidCastle` v `SaveRules.All` (žádná hláska před dostavbou, žádný host před všemi
hláskami, `Carrying` jen host, který ještě nesedí). Odjezdy `Departure.Castle`, `CastleBeacon` a `CastleGuest` mají
svá pravidla v `SaveRules` jako přívoz. Uložení a obnova jsou v `Game.Persistence.cs` a stará hra se nahraje bez
ničeho z toho (`LegacySaves.AddedAfterVersion5`). `SaveStore` ani `SaveDataV4` se nemění a verze uložené hry zůstává
5, protože nová sekce má výchozí hodnoty.

## Kód

- `Buildings/Castle.cs` (model a díly), `Castle.Trips.cs` (`ITripTarget`), `CastlePlans.cs` (dodávky, mince, hosté),
  `BeaconTower.cs` (hláska).
- `Valley/CastleSites.cs` (hledání místa), `World.Castle.cs` (plánování, `InCastleZone`, hlásky), `Tasks/GuestTrip.cs`.
- `Characters`: `CastleRole` v `ActorRoles.cs`, ID v `ActorIds.cs`, strašidýlko v `CollectorPet.cs`.
- `App`: `Game.Castle.cs` (panel, dodávky, hlásky, hosté), systém `castle` v `Game.Systems.cs` (most, pochodně,
  ohňostroj, hosté u stolu), panel v `Game.Panels.cs`, tlačítka v `UiIntent.cs` a `Game.Intents.cs`, texty v
  `Texts.cs`.
- `Diagnostics`: `castle-checks` v `DiagnosticSuites.cs` na seedech otisků a `CastleFingerprint` pro
  `seed-N.castle.txt`.

## Postup

Jedna fáze je jedna větev a jeden PR. Po každém commitu prochází `./build.ps1 -Test`. Fixtury v `Tests/Saves` a staré
otisky v `Tests/Worlds` se nemění.

1. **Místo a model.** `PlanCastle`, `InCastleZone`, model ve všech šestnácti stupních (snímky `run.ps1 -Capture -View
   castle-0`, `castle-8`, `castle`, `castle-night`, `castle-far`), nový otisk `seed-N.castle.txt` a `castle-checks`,
   které hlídají, že se žádný starší otisk nezměnil. Výkon `./benchmark.ps1` před a po, na návsi i na okruhu.
2. **Stavba hradu.** Kněžna, panel, šestnáct dodávek, kámen ze dvou zdrojů, odměny a uložení `Stage`.
3. **Hlásky.** Čtyři věžičky u statků, ohně, signál na hrad a uložení `Beacons`.
4. **Hostina.** `GuestTrip`, host v přívěsu, stůl na nádvoří, ohňostroj, strašidýlko a uložení `Guests`, `Carrying` a
   `Feast`.

## Jak je to postavené

Všechny fáze jsou v jedné větvi. Od návrhu výše se hotová hra liší takhle:

- **Místo:** hledání je jako u balónu, ale rozdíl výšek pod hradbami smí být 3 m, a když se nic nenajde, postupně 4,5,
  6 a 8 m. Cesta k bráně je rovná čára ze silnice (stoupání nejvýš 25°), ne `FindSpur`. Věci u cesty (pampelišky a
  kvítí) se rozmisťují dřív než hrad, takže na nádvoří mohou zůstat.
- **Dodávky:** štěrk, čtyřikrát kámen (první a třetí od moře, druhý a čtvrtý z hor, chybí-li jedno, vozí se z druhého),
  cihly, prkna, cihly, prkna, klády, prkna, cihly, cihly, sklo, prkna, sklo. Každá dodávka přidá jeden díl
  (`CastlePlans.Parts`). Za hotový hrad je 60 mincí. **Praporek na auto není**: nový doplněk by potřeboval model a místo
  na každém z devíti vozidel.
- **Hlásky:** jedna dodávka klád ze skladu dřeva hlásku postaví a rovnou zapálí, bez otázky u strážce. Šipky vedou
  přívěs k hlásce jako u každé dodávky a pořadí je dané hranicemi (moře, hory, řeka, les). Každá hláska dá 30 mincí.
  Když hoří všechny, rozhoří se oheň na věži. Jiskry od hlásek ke hradu nejsou.
- **Hostina:** host jede jako náklad `HaulLoad.Guest` obyčejnou cestou přívěsu (`CastleGuestTrip`, žádný nový druh
  úkolu). Otázku klade host doma. Na lavičce přívěsu a pak u stolu sedí jeho **zmenšená kopie** (`Character.Figure`),
  takže skutečná postava stojí doma dál a její úkoly fungují jako dřív. Hosté jezdí v pořadí židlí, každý dá 8 mincí a
  plný stůl 40 mincí, ohňostroj nad hradem každou noc a strašidýlko Bubáček. Drak na hostinu
  nepřiletí.
- **Uložení:** `SaveData.Castle` má `Stage`, `Beacons` (hranice, kde hláska hoří), `Guests` (obsazené židle) a `Feast`.
  Host v přívěsu se neukládá zvlášť, plyne z uložené cesty přívěsu. Je jen jeden odjezd, `Departure.Castle`.
  `SaveRules.AsksAsGuest` dovolí uložit hru uprostřed otázky hosta u strážce, sovy, balonářky nebo volavky.
- **Kód:** `Buildings/Castle.cs`, `Castle.Trips.cs` (i `CastleGuestTrip`), `BeaconTower.cs`, `CastlePlans.cs`;
  `Valley/CastleSites.cs`, `World.Castle.cs`; `Characters/Character.Castellan.cs`, `CastleRole`, `ActorIds.Castellan`;
  `App/Game.Castle.cs`; `Ui/GameUi.Castle.cs`; `Diagnostics/CastleChecks.cs` a otisk `seed-N.castle.txt` ve
  `WorldContractChecks.cs`.
