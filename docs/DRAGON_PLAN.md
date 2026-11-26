# Návrh: drak pod horami

Další obsah pro velkou mapu, po poště a majáku (`docs/POST_AND_LIGHTHOUSE_PLAN.md`). Jména, příběh a odměny jsou
návrh a čekají na potvrzení.

## Nápad v jedné větě

Nad salaší pastýře Bohouše leží na skalní římse obrovský **drak**, který nastydl. Místo ohně kýchá jen kroužky kouře a
jeho vejce v hnízdě stydne. Dítě mu přiveze, co potřebuje na teplé hnízdo, v **dračí hádance** mu pomůže pořádně
kýchnout, oheň vzplane a z vejce se vylíhne **dráček**. Uzdravený drak pak dítě **sveze nad celým údolím**.

Proč právě takhle:

- **Je vidět z dálky.** Drak je velký asi jako dům. Z vesnice jsou nad horami vidět jeho kroužky kouře a v noci žhne
  jeho oheň. Dítě nikdo nemusí posílat, samo bude chtít vědět, co tam je.
- **Využije velkou mapu.** Cesta vede přes okruh až k horám. Odměnou je let nad celým údolím: všechny čtyři hranice,
  maják, řeka i vesnice z výšky.
- **Staví na tom, co ve hře funguje.** Jízdy s vozíkem jsou `ITripTarget` jako u majáku, hádanky jsou běžné otázky a
  dráček je další mazlíček.

## Postavy

- **Drak Kvído** (drak): zelený s oranžovým břichem, hodný a trochu nešikovný, mluví pomalu a mírně.
- **Dráček Jiskřička** (mládě): nový `CollectorPetKind.Dragonling`, létá za autem a občas vyfoukne jiskřičku.
- **Zajíc pastýř Bohouš** (už ve hře, pastýř): o drakovi ví: „Nahoře bydlí drak Kvído, nějak smutně kýchá.“

Jiná jména: Drak Šupináč, dračice Žofka, Dráček Uhlík. Pokud má být drakem dračice, vejce dává ještě větší smysl.

## Co drak chce: teplé hnízdo pro vejce

Vejce leží ve skalním hnízdě před drakem. Draci, stejně jako želvy, zahrabávají vejce do teplého písku, a to se dá dětem
pěkně vyprávět. Hnízdo se postaví ze **šesti nákladů**. Všechny jsou suroviny, a tak je dovoz krátký:

| náklad | odkud | co přibude |
|---|---|---|
| 1–2 písek | **horská stráň** u silnice k horám | písečné hnízdo, vejce se do něj zaboří |
| 3–4 kamení | horská stráň | kruh z kamenů kolem hnízda, který drží teplo |
| 5–6 klády | horská stráň | hranice dřeva v ohništi vedle hnízda |

**Horská stráň** je místo k naložení u silnice k horám, podobné pobřežnímu lomu: vyvrácené stromy, suť pod skalou a
písčitý břeh potoka. Otázku na naložení klade pastýř Bohouš (`ITripTarget.SourceFor`, jako majákář u lomu). Cesta ze
stráně ke drakovi má mít méně než 500 m, takže ani jedna jízda s vozíkem nevede přes celé údolí. Z vesnice se k drakovi
jede jen jednou, bez vozíku.

Na rozdíl od majáku nepotřebuje drak zboží ze závodů. Postaví se proto v každém údolí, kde je silnice k horám, i v údolí
bez brodu a bez závodů.

## Dračí hádanka: oheň vzplane

Po šestém nákladu je hranice připravená, ale drak ji kvůli rýmě nezapálí. Řekne: „Pomůžeš mi kýchnout? Když vyřešíš mou
hádanku, kýchnu pořádně!“

- Hádanka jsou **tři otázky za sebou** z běžného střídání zapnutých druhů otázek. Je to první souboj s „bossem“, ale
  hodný: špatná odpověď nic nezkazí, jen přijde nová otázka (jak to hra dělá od posledního commitu).
- Po každé správné odpovědi drak natáhne vzduch víc, jeho hruď se nafoukne a z nosu jde víc kouře. Po třetí odpovědi
  přijde **velké HAPČÍ**, plamen zapálí hranici, vejce zazáře, praskne a vylíhne se dráček.
- Odměna: **40 mincí** a **Dráček Jiskřička**. Kamera ukáže chvíli líhnutí (`FollowCamera.ShowMoment`), jako u
  hotového majáku.
- Stav hádanky (0 až 3 vyřešené otázky) se ukládá. Rozehraná hádanka se po načtení vrátí na stejnou otázku.

## Let na drakovi

Uzdravený drak nabízí v panelu tlačítko s křídly: „Proletíme se?“

1. Auto zůstane zaparkované u hnízda. Kamera si sedne drakovi za hlavu (nový režim `FollowCamera`, podobný
   `ShowPanorama` na rozhledně).
2. Drak roztáhne křídla, vzlétne a proletí **velký okruh nad údolím** ve výšce asi 120 m po pevné dráze: hory, hraniční
   řeka, moře s majákem, les a zpátky. Let trvá asi 40 s a dá se kdykoli ukončit tlačítkem zpět.
3. Cestou se rozsvítí stejné ukazatele jako na rozhledně (`HiddenFriends.Ping`, `Character.Ping`): strážci hranic a
   kamarádi, kteří se ještě schovávají. Při hledání pokladu se na chvíli ukáže místo, kde je zakopaný.
4. Za první let dostane hráč 10 mincí, další lety jsou zadarmo a bez odměny.
5. **Bez nového uloženého stavu**, kromě toho, jestli už první let proběhl. Let se neukládá: po načtení stojí drak na
   římse.

Za soumraku a v noci hoří ohniště jako maják, takže je vidět z celého údolí: jedno `OmniLight3D` a aditivní plamen bez
stínu. `benchmark.ps1 -Off dragon` změří, kolik to stojí.

## Model draka

Drak je z koulí, válců a kuželů jako ostatní postavy, jen mnohem větší. Je dlouhý asi 14 m a hlavu má ve výšce 5 až 6 m.

- **Tělo:** krk a ocas jsou řetězy koulí po hladké křivce, které se zužují, po hřbetě vedou trny z kuželů, břicho je
  světlé a pruhované, má čtyři tlapy s drápy, rohy a velké přátelské oči s víčky.
- **Křídla:** tenká plachta z trojúhelníků (`ArrayMesh`) na kostech z válců. Na římse jsou složená, při letu se
  roztáhnou.
- **Pohyb:** hruď dýchá, ocas se vlní vlnou, která běží řetězem, oči mrkají a hlava se otáčí za autem, když je blíž
  než 60 m. Z nosu jdou kroužky kouře z malé zásoby jako pára vláčku. Kýchnutí je krátká animace s pohybem hlavy a
  zvukem.
- **Kde leží:** hlava a přední tlapy jsou na okraji údolí u místa, kam se vysype náklad. Tělo a ocas leží za okrajem
  (`ValleyBounds`) na úbočí hor, kam auto nedojede. Drak je `Character` s vlastním modelem (`Character.Dragon.cs`) a
  místo, kde se s ním mluví, je u jeho brady, do 8 m (`InteractionRange`) od plochy, po které jezdí auto.
- **Výkon:** na dálku se kreslí zjednodušeně (`DistantDetails`), obrys má jen zblízka. Model se nesmí sloučit do
  `StaticMeshes`, protože se hýbe.

## Kód (podle `CLAUDE.md`)

- **ID draka:** `ActorIds.Dragon` = 79, registrovaný po pošťákovi, mimo zastávky pohlednic a jízdní kurzy.
- **Role:** `DragonRole` v `ActorRoles.cs`, s vlastní ikonou (`Art.RoleTaskIcon`).
- **Stav panelu:** `GameState.DragonStatus` = 33, registrovaný v `Game.Panels.cs`.
- **Jízdy:** `TripKinds.Dragon` = `"dragon"`, `Departure.Dragon`, `DragonLair : ITripTarget`
  (`Buildings/DragonLair.Trips.cs`).
- **Plán:** `DragonPlans`: šest nákladů, díly do bubliny, mince.
- **Místo:** `World.Dragon.cs`, `PlanDragon` po `PlanBorderfolk`, bez náhodných čísel, z konce silnice k horám;
  `DragonSite` (římsa, hnízdo, místo pro výsyp) a `MountainSlopeSite` (horská stráň).
- **Zóna:** `InBorderfolkZone` se nejdřív ptá `InDragonZone`, takže rostliny pásu se tam jen nepostaví a
  `OutlandTreePositions` se nezmění.
- **Tok hry:** `Game.Dragon.cs`: panel, jízdy, hádanka, líhnutí a let.
- **Scéna:** `Buildings/DragonLair.cs` (římsa, jeskyně, hnízdo, kamenný kruh, hranice, vejce, oheň),
  `Buildings/MountainSlope.cs` (stráň), `Characters/Dragon.cs` (model a pohyb), `Characters/Character.Dragon.cs`
  (postava pod bradou), `Valley/DragonFlight.cs` (dráha letu).
- **UI:** `GameUi.Dragon.cs`, `UiIntent.DragonTrip`, `UiIntent.DragonRiddle`, `UiIntent.DragonFlight`; texty v
  `Texts.cs`, jméno a pozdrav v `CharacterCatalog`, obrázky `dragon` a `egg` v `Art.Picture`.
- **Uložení:** `SaveData.Dragon` (`DragonSave`: `Stage` 0 až 6, `Riddle` 0 až 3, `Flown`), pravidlo `HasInvalidDragon`
  (hádanka jen po šestém nákladu, let jen po vylíhnutí), záznam v `LegacySaves.AddedAfterVersion5`.
- **Mazlíček:** `CollectorPetKind.Dragonling` připojený na konec výčtu a `WorldPetCatalog.Dragonling`.
- **Otisk:** nový `Tests/Worlds/seed-N.dragon.txt` s polohou římsy a stráně a počtem vynechaných stromů, vytvořený
  jednou nástrojem `write-world-fingerprint`; otisky jádra, pásu a pobřeží se nemění.

## Fáze

1. **Drak na římse.** Místo, model, pohyb, kroužky kouře, rozhovor a zmínka u pastýře. Bez uloženého stavu. Hotovo,
   když drak na 42 seedech leží u hor, auto k němu dojede, otevře se rozhovor a otisky se nemění.
2. **Hnízdo.** Horská stráň, šest jízd, díly hnízda, sekce `Dragon` (jen `Stage`).
3. **Hádanka a dráček.** Tři otázky, kýchnutí, oheň, líhnutí, mazlíček, mince a ukládání `Riddle` a `Hatched`.
4. **Let a noční oheň.** Dráha, kamera, ukazatele, `Flown` a měření `benchmark.ps1`.

Každá fáze je jeden commit a po každé prochází `./build.ps1 -Test`.

**Kontroly** jsou nová sada `dragon-checks` na seedech 1, 42 a 7421 a jednorázově na 1 až 40:

- drak leží u hranice hor, brada je do 8 m od plochy pro auto a tělo za okrajem údolí;
- trasa ze stráně k hnízdu je kratší než 500 m a z vesnice k drakovi kratší než 1 900 m;
- všech šest nákladů postaví svůj díl a pevné části zastaví auto až po postavení;
- hádanka: tři správné odpovědi zapálí oheň, uložení a načtení po první a druhé odpovědi pokračuje správně;
- líhnutí dá mince a dráčka, let začne i skončí a vrátí auto do jízdy;
- stará hra bez sekce `Dragon` má hnízdo prázdné a poškozená sekce neprojde přes `SaveRules`.

## Jak to dopadlo

Všechny čtyři fáze jsou hotové najednou, v jednom kroku místo čtyř commitů. Proti návrhu se změnilo tohle:

- **Drak leží uvnitř údolí,** ne tělem za okrajem. Na hranici je jen 42 m místa za koncem silnice, a tělo i jeskyně tak
  mohou být pevné a stát na zemi. Nejdřív se zkouší místo vedle statku, odkud drak hledí zpátky do údolí. Kde silnice
  končí v zářezu, auto by muselo vyjet po 2 až 3 m vysokém svahu. Drak pak leží na svahu nad posledním úsekem silnice a
  dívá se dolů na ni; místo pro výsyp a místo pod jeho bradou jsou přímo na silnici. Na seedech 1 až 40 má každé údolí
  draka.
- **Bez stráně není drak.** Všechno do hnízda se nakládá na stráni a otázku tam klade sám drak (jako strážce moře u lomu
  na pobřeží), takže údolí bez místa pro stráň draka nemá.
- **Sekce `Dragon` nemá `Hatched`:** vejce je vylíhnuté, když `Riddle` je 3. Každá odpověď hádanky se uloží hned.
- **Model** je jeden uzel `Dragon` s tělem, krkem, hlavou, ocasem a křídly; pevné části a římsa patří `DragonLair`,
  takže drak může odletět. Mluví se s postavou bez těla, která stojí pod jeho bradou.
- **Let** je uzavřená křivka kolem středu údolí, nejméně 35 m nad zemí (nad kopci se křivka zvedne). Trvá 55 s a jízdou
  se dá ukončit dřív. Uložení během letu vrátí draka na římsu. Za první let je 10 mincí.
- **Noční oheň** u hnízda svítí podle tmy (`Weather.Darkness`), měření v `benchmark.ps1` zatím nemá vlastní přepínač.

- **Velikost:** model je kreslený 1,3krát větší, než jsou rozměřené jeho díly (`Dragon.Size`), takže je vyšší než
  srub pastýře.

`dragon-checks` prochází na seedech 1, 42 a 7421 a jednorázově na 1 až 40. Obrázky ukáže
`./run.ps1 -Capture -View dragon` (a `dragon-nest`, `dragon-flight`).

## Rizika

- **Úbočí hor za okrajem** je na některých seedech příkré nebo nízké (`FootShift` posouvá úpatí o −30 až +80 m). Model
  draka se proto položí na terén po kouscích, každá tlapa a ocas na vlastní výšku, a kontrola na 42 seedech ověří, že
  nic nevisí ve vzduchu ani se nezaboří.
- **Let nad údolím** ukáže zblízka i vzdálené části pásu a hranic, které se dnes stavějí jen kolem auta (rostliny pásu
  po 64m blocích). Kamera proto letí dost vysoko, aby stačily vzdálené tvary stromů (`BuildDistantTrees`). Benchmark
  dostane scénu `let-na-drakovi`.
- **Velký model** a jeho obrys stojí výkon. Měří se před fází 1 a po ní.

## Další nápady pro velkou mapu

Kratší návrhy pro další strážce, podle vzoru majáku:

- **Ovečky pastýře Bohouše:** stádo se rozuteče po pásu a dítě ho autem zažene zpátky do ohrádky. Ovečky utíkají před
  autem jako zvířata u lovce.
- **Krmelec hajné Boženy:** dovoz obilí a sena do krmelce v lese. V noci pak ke krmelci chodí jeleni a srnky.
  Božena dostala jinou činnost, stopy šesti zvířátek napříč pásem: `docs/TRACKING_PLAN.md`.
- **Pramice převoznice Venduly:** postaví se přívoz přes hraniční řeku a auto se po ní převeze na ostrůvek s
  ptačí pozorovatelnou.
- **Rallye po okruhu:** razítkovací stanoviště u strážců a jízda na čas po celém okruhu.
- **Horkovzdušný balon** u vesnice: krátký vyhlídkový let jako s drakem, pro údolí, kde ještě není drak uzdravený.
  Hotovo jinak, daleko od vesnice a s chytáním hvězdiček: `docs/BALLOON_PLAN.md`.
