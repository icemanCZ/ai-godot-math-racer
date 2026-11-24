# Implementační plán: šestkrát větší údolí s přírodními hranicemi

Údolí je dnes kruh o poloměru 525 m (`Settings.PlayableRadius`) uprostřed čtverce terénu ±555 m
(`Settings.WorldRadius`). Cílem je hratelná plocha asi šestkrát větší, čtverec zhruba ±1100 m, a k tomu kulisy až do
±1400 m. Údolí má být ze čtyř stran uzavřené přírodou: na jedné straně horami, na druhé mořem, na třetí lesem a na
čtvrté řekou. Nic z toho zatím není provedeno.

Údolí už se jednou zvětšovalo, a to desetkrát, z poloměru 166 m na 525 m (`SmokeTest`: „world playable area increased
tenfold“). Tentokrát je ale jedna podmínka navíc: rozehrané hry si ponechají své údolí.

## Pravidla pro všechny fáze

- **Jedna fáze = jedna větev = jeden PR.** Po každém commitu prochází `./build.ps1 -Test`.
- **Staré údolí se nehne ani o kousek.** Všechno, co dnes leží uvnitř čtverce ±555 m, vzniká dál stejným kódem, ve
  stejném pořadí a ze stejných náhodných čísel. Otisky v `Tests/Worlds` hlídají polohy postav, staveniště, silnice,
  výšky terénu v mřížce po 25 m a všechny stromy. Nikdy se neupravují ani nepřegenerovávají.
- **Nová krajina se přidává až za staré údolí**, stejně jako sklárna (`seed-N.glassworks.txt`), tedy s vlastními
  náhodnými generátory a s vlastním otiskem `Tests/Worlds/seed-N.outland.txt`. Stromy, které musí ustoupit nové silnici
  uvnitř starého údolí, zmizí přes `Vegetation.ClearArea`. Ta je skryje, ale `TreeCount` ani `TreePositions` nemění.
- **Rozšíření dostanou i staré hry.** Svět se celý odvozuje ze seedu a nový pás je deterministický, takže fáze 1 až 4
  nepotřebují novou verzi uložené hry ani migraci. Nová verze přijde až s obsahem, který má stav (fáze 5).
- Výkon se měří pomocí `./benchmark.ps1` před každou fází a po ní, na stejném místě. Čísla se zapisují do PR.

## Rozmístění stran

Strany jsou navržené tak, aby dávaly smysl i jako zeměpis. Dnešní řeka teče k +z (`River.Level` klesá s `z`), proto:

| strana | co tam je | proč tam |
|---|---|---|
| sever (−z) | **hory**, hřbet vysoký 120 až 200 m | pramení tu dnešní řeka i hraniční řeka |
| jih (+z) | **moře**, pláž, voda až k obzoru | dnešní řeka do něj ústí, už teď teče tímto směrem |
| strana za dnešní řekou (`Side`) | **hraniční řeka**, široká 40 až 60 m, za ní louky jako kulisa | mezi oběma řekami vznikne „meziříčí“ |
| opačná strana (`−Side`) | **les**, pás hustého lesa asi 150 m | les tak nestojí u vody |

Řeka je podle seedu jednou vlevo a jednou vpravo (`WorldPlan.Side`), takže se s ní zrcadlí i les a hraniční řeka.

Rohy vzniknou samy: delta, kde hraniční řeka ústí do moře; lesnaté útesy nad mořem; prameniště obou řek pod horami;
lesnaté podhůří.

## Odhad velikosti

| | dnes | po rozšíření |
|---|---|---|
| hratelná plocha | 0,87 km² (kruh r = 525 m) | asi 5,1 km² (čtverec ±1130 m) |
| políčka terénu po 2,5 m | 197 tis. | 774 tis. (jádro 197 tis. + pás 577 tis.) |
| kulisy hranic po 10 m | — | asi 30 tis. |
| jízda ze středu k nejvzdálenějšímu rohu | ~35 s | ~100 s (15 m/s) |

Terén v jádře se dnes skládá z neindexovaných trojúhelníků (asi 240 B na políčko). Nový pás bude indexovaný, a tím pádem
asi čtyřikrát menší. Paměť by tedy neměla být problém. Riziko je spíš doba načítání, viz fáze 2.

## Fáze 0 – Síť před změnou (hotovo)

1. Nová sada `outland-checks` v `DiagnosticSuites.cs`, na seedech otisků (1, 42, 7421). Zatím hlídá, že jádro má
   pořád ±555 a 525 m, že všechny postavy, sběratelská místa, silnice a stromy leží uvnitř `ValleyBounds` a že auto za
   okrajem se vrátí na start, zatímco auto těsně uvnitř jede dál. S dalšími fázemi se rozroste.
2. Na hlavní větvi se změří a do PR zapíše výchozí stav: doba od spuštění po první snímek, `./benchmark.ps1` na návsi a
   při jízdě a hash stromů podle postupu „vegetation hash vs baseline worktree“.

### Výchozí měření (23. 11. 2026, před fází 2)

GTX 1060 6GB, seed 42, `./benchmark.ps1 -Resolution 1920x1080`. Okno mělo ve skutečnosti 1772 × 967 px.

| scéna | FPS | 1 % low | GPU ms | CPU ms | draw calls | trojúhelníky | VRAM MB |
|---|---|---|---|---|---|---|---|
| jízda ve dne | 112,5 | 85,7 | 7,86 | 5,39 | 7012 | 9,4 mil. | 244 |
| jízda v bouřce | 123,0 | 96,0 | 8,02 | 4,05 | 4342 | 14,0 mil. | 245 |
| náves ve dne, postavená | 99,6 | 96,0 | 9,75 | 6,79 | 7864 | 12,3 mil. | 243 |
| jízda v noci | 98,6 | 76,4 | 8,69 | 6,25 | 8244 | 10,0 mil. | 244 |
| náves v noci, postavená | 96,3 | 90,0 | 10,27 | 7,57 | 7936 | 12,9 mil. | 243 |

Paměť procesu je asi 1,6 GB. Celý běh `world-fingerprint-checks` (spuštění Godotu, vygenerování světa a porovnání s
otiskem), kdy běžel jen jeden proces, trval 4,7 s u seedu 42 a 5,7 s u seedu 1. Proti těmto číslům se poměřuje doba
načítání ve fázi 2.

## Fáze 1 – Rozměry jako pojmy, bez změny chování (hotovo)

Dnes se `PlayableRadius` a `WorldRadius` používají na 45 místech ve 30 souborech pro dvě různé věci. Fáze je oddělí:

1. **Generování starého údolí** dostane pevné pojmenované konstanty ve `WorldTuning`: `CoreRadius = 555` a
   `CorePlayableRadius = 525`. Patří sem `Terrain` (mřížka jádra), `RiverCourse`, `River.Generate`, `Vegetation`,
   `Vegetation.Nature`, `Wayside`, rozmístění zvířat ve `Wildlife` (`SafeToPlace`), `World.*` (Ford, Builds, Golem,
   Hidden, Treasure, Collections, ohrada lovce `PenFits`), `World.cs:269` a `Boat` (loďka jezdí u mola). Tento kód se už
   nikdy nesmí řídit novou velikostí.
2. **Hranice při hraní** se přesune do nové třídy `ValleyBounds` ve složce `Valley`, s metodou
   `Contains(x, z, margin)`. Patří sem `Vehicle` (návrat na silnici), `Grass` (které dlaždice se kreslí), pohyb zvířat
   ve `Wildlife` (`Safe`), `HuntField` (kam doletí síť), louky lovu (`World.IsMeadow`), místa pro světlušky
   (`World.IsFireflySpotFree`), `LampField` a `SaveRules` (kontrola polohy lampy). V této fázi `ValleyBounds` vrací
   pořád starý kruh.
3. `Settings.WorldRadius` a `Settings.PlayableRadius` zmizí, aby je nikdo nepoužil omylem. `contract-checks` ohlídá, že
   se na `ValleyBounds` ptá jen kód z bodu 2. Nové použití jinde je pak vědomé rozhodnutí a musí se do seznamu dopsat.

Hotovo, když všechny otisky a sady projdou beze změny.

## Fáze 2 – Terén za starým údolím (hotovo)

1. **Výška:** `WorldLayout.NaturalHeight` zůstal beze změny. Za starým údolím už dnes vrací náhorní plošinu
   (24 m ± vlnky). Hranice přidá až fáze 3.
2. **Terén v pásu** ([Terrain.Outland.cs](../Scripts/Valley/Terrain.Outland.cs)): jádro si nechává své pole `heights`.
   Pás má jedno pole po 2,5 m až do `WorldTuning.OutlandRadius` = ±1115 m. To je přesně sedm dlaždic po 32 políčkách za
   jádrem, takže mřížky na sebe sedí. Pole pásu se spočítá jednou v `BuildMeshes`, až je jádro hotové, a `Generate()`
   ho při každém přepočtu jádra zahodí. `Height()` se za jádrem zeptá pásu. Dokud pás není spočítaný, vrací tam stejně
   jako dřív `RawHeight`.
3. **Kreslení:** 615 dlaždic 80 × 80 m se kreslí v plném detailu do 260 m (`VisibilityRangeEnd`). Dál je 40 bloků
   320 × 320 m s bodem každých 10 m. Blok se ukazuje už od 80 m od svého středu a leží o 0,4 m níž, takže ho jemná
   zem tam, kde se kreslí obě, překryje. Za ±1115 m jsou kulisy po 10 m (kousky 640 m) až do
   `WorldTuning.SceneryRadius` = ±1415 m. Hrubé kusy mají na okrajích „sukni“ hlubokou 4 m, aby na švech nebyly vidět
   mezery. Původní dlaždice 80 m po jedné kreslily asi o 150 až 200 draw calls víc, sloučené bloky jsou proti výchozímu
   stavu jen asi o 30 navíc.
4. **Kolize** se v pásu staví jen kolem auta (`StreamColliders`, systém „outland colliders“ v `Game.Systems.cs`):
   dlaždice blíž než 140 m kolizi dostanou, dál než 200 m o ni přijdou. Jádro má kolizi všude jako dřív.
   `HeightMapShape3D` se nepoužil.
5. **Řeka:** `RiverCourse` počítá jádro jako dřív. Za ±555 m se tok během 450 m klidně stočí na pomalý oblouk kolem
   průměrné polohy řeky v jádru, bez náhody a bez hledání. Voda (`River.Generate`) sahá do ±1415 m. Za jádrem bere
   hloubku pro barvu z prostého profilu koryta (`ChannelBed`), protože terén tam v tu chvíli ještě není spočítaný.
6. **Dohled:** kamera `Far` se zvedla z 1200 na 3000 m, i u kamery pohlednic. Mlha se nezměnila.
7. `ValleyBounds` je stále starý kruh, hráč se do pásu nedostane.

**Ověřeno:**
- Prošla celá sada (`./build.ps1 -Test`) a otisky všech tří seedů.
- Dočasná sonda spočítala, kolikrát se generování starého údolí zeptá na terén nebo řeku za jeho okrajem. Kromě vody
  řeky a okrajových vrcholů sítě jádra, kde má soused váhu přesně 0, to bylo 0krát na všech třech seedech. Staré údolí
  se tedy nezměnilo ani v částech, které otisky nehlídají. Sonda je odstraněná.
- `outland-checks` nově hlídá: výšku až do kulis; švy jádro–pás a pás–kulisy (schod nejvýš asi 0,008 m); řeku bez
  skoku a s mírným ohybem (sklon nejvýš asi 0,32); kolizi kolem auta, jejíž trojúhelníky leží přesně na `Height()`, a
  to, že kolize zase zmizí.

**Měření** (GTX 1060, seed 42, 1920 × 1080, okno 1772 × 967):
- Pás se postaví za 0,49 s. Celý běh `world-fingerprint-checks` se prodloužil z 4,7 na 5,3 s (seed 42) a z 5,7 na
  6,3 s (seed 1), tedy asi o 12 %, pod limitem 30 %.
- Ranní výchozí čísla (tabulka výše) se odpoledne nepotvrdila: stejný nezměněný commit dal ve dne 104,8 FPS místo
  112,5. Rozdíl dělal stav stroje, ne kód. Srovnávací běhy proto jdou hned po sobě:

| scéna | výchozí stav (odpoledne) | po fázi 2 |
|---|---|---|
| jízda ve dne | 104,8 FPS (1 % low 83,5) | 105,0 (80,0) |
| jízda v bouřce | 115,5 (90,0) | 115,0 (90,0) |
| náves ve dne | 96,1 (90,0) | 97,8 (90,0) |
| jízda v noci | 93,3 (75,0) | 89,8 (70,0) |
| náves v noci | 91,8 (85,7) | 89,6 (84,0) |

VRAM vzrostla asi o 32 MB (244 → 276). Pokusy bez sítí pásu a s kamerou zpátky na 1200 m vyšly v rámci rozptylu
stejně. Pás samotný ani delší dohled tedy skoro nic nestojí. Osvětlení ve vrcholech pro vzdálenou zem nepomohlo a
bylo vráceno.

## Fáze 3 – Čtyři hranice (hotovo)

Všechny hranice počítá `ValleyBorders` (vzniká v `WorldPlan` jako poslední, s vlastními náhodnými čísly). Každá maska
je přesně nula, dokud je |x| i |z| pod 700 m (`ValleyBorders.Untouched`). Otisky všech tří seedů jsou beze změny.

Žádná hranice není rovná čára. Každá má vlastní vlnění složené ze sinusovek s omezenou výchylkou, takže se nikdy
nedostane do hratelné plochy.

1. **Hory (sever):** hřbetový šum se zvedá od 930 m (podle místa o 30 m blíž až o 80 m dál) a nahoře dosahuje asi 110
   až 205 m. Výš se terén barví do skály a od 150 m do sněhu, strmé svahy jsou skalnaté i na jiných stranách. Daleko
   do stran, k obzoru, hřbet klesá do kopců, aby nekončil kolmou stěnou. Obě řeky pramení v `SpringZ` (−1260 m): od tam
   se jejich koryta během 140 m zavřou do svahu (`RiverBedWeight`), takže nikde nekončí useknutě.
2. **Moře (jih):** pobřeží má zátoky a mysy kolem 1210 m (±61 m), hladina je −10 m. Pláž je v zátokách široká a pod mysy
   úzká (24 až 66 m), její okraj proti trávě se vlní, dno klesá na −19 m. Voda (`BorderWater`) používá shader řeky s
   pomalým prouděním: u břehu jemná síť s hloubkou pro pěnu, dál velké plochy až k obzoru (3,6 km). Kamera teď dohlédne
   na 3000 m (dřív 1200).
3. **Hraniční řeka (strana `Side`):** meandruje kolem 1215 m od středu (±56 m), je široká 48 m a má stejnou hladinu
   jako dnešní řeka. Koryto se jen vyhloubí, takže u moře nevzniknou hráze. Za řekou jsou kopce s řídkými háji.
4. **Les (strana `−Side`):** zvlněné kopce a hustý les (`BorderForest`), jehož okraj se vlní kolem 1175 m (±37 m),
   houstne během 45 m a před ním stojí osamělé stromy. Je to asi 25 tisíc jednoduchých stromů (jehličnany výš, listnáče
   dole) ve 115 dávkách MultiMesh po 320 m, bez kolize. Dávky dál než 300 m od kamery se kreslí jednoduše: koruna o pár
   stěnách, bez kmene a bez stínu. Žádný strom nestojí blíž než 6 m od okraje hry. Řidší háje rostou i v podhůří a za
   hraniční řekou, nikdy na pláži, ve vodě ani vysoko v horách.
5. **Hranice hry:** `ValleyBounds` je čtverec ±1130 m, těsně před úpatím hor, pláží, břehem hraniční řeky a lesem. Auto
   na jeho okraji zastaví (vrátí se na předchozí polohu). Návrat na start zůstal jen jako pojistka, když se auto dostane
   30 m za okraj nebo propadne. Tráva na pláži neroste. Jemný terén pásu sahá do 1275 m (`OutlandRadius`), kulisy po
   10 m do 1675 m (`SceneryRadius`) a hrubý prstenec po 25 m až k obzoru do 3000 m.
6. Okraje hrubých kusů terénu mají „sukně“. Materiál louky je kreslí z obou stran, a ze zadní strany byly tmavé, takže
   ve švech byly vidět čáry. `MeadowGround.gdshader` teď zadní strany osvětluje jako přední.

Kontroly v `outland-checks` (seedy 1, 42, 7421):
- hory jsou vyšší než 110 m a u okraje hry nižší než 90 m;
- moře je hluboké a pláž u okraje suchá a písečná;
- hraniční řeka má koryto pod hladinou a suchý břeh od −1000 m až k moři;
- les má přes 8000 stromů, všechny za okrajem hry a nad mořem, většinu na své straně;
- obě řeky jsou nahoře v horách uzavřené;
- vše platí na 42 seedech (1 až 40, 42, 7421), nejen na třech z běžné sady;
- auto, které jede plným plynem proti každé ze čtyř stran, se zastaví uvnitř údolí, a auto 40 m za okrajem se vrátí
  na start.

Pro kontrolu očima jsou tu pohledy `border-overview`, `-mountains`, `-sea`, `-river`, `-forest` a `-corner`
(`./run.ps1 -Capture -View border-sea -Seed 42`; `-Seed` je nový).

Odchylky od původního plánu: vodopády, jezírka a řada obrysů stromů na okraji lesu nejsou. Stromy lesa kolizi nemají,
protože auto k nim nedojede. Společná otázka `ValleyBounds.IsWater` nevznikla, protože auto zastaví okraj hry dřív, než
dojede k moři nebo hraniční řece.

**Měření** (GTX 1060, seed 42, 1920 × 1080; běhy hned po sobě; `./benchmark.ps1` nově umí vypnout `forest`, `sea` a
`outland`):

| scéna | bez hraničního lesa | hotová fáze 3 | les bez vzdálené podoby |
|---|---|---|---|
| jízda ve dne | 104,1 FPS (1 % low 80,0) | 101,5 (80,0) | 97,5 (77,1) |
| jízda v bouřce | 113,5 (90,0) | 109,6 (85,7) | 102,5 (84,0) |
| náves ve dne | 97,1 (90,8) | 93,4 (90,0) | 87,9 (85,7) |
| jízda v noci | 89,5 (71,7) | 87,3 (68,6) | 84,5 (70,0) |
| náves v noci | 88,9 (84,0) | 86,8 (77,6) | 81,4 (75,0) |

Bez lesa jsou čísla stejná jako po fázi 2. Moře, hraniční řeka ani větší terén skoro nic nestojí (vypnutí `sea` a
`outland` k vypnutí lesa nic nepřidalo). Les stál nejdřív 8 až 10 %, se vzdálenou podobou stromů stojí 2,5 až 4 %.
Pás se postaví asi za 1 s (dřív 0,5 s), protože jemný terén sahá dál a přibyl prstenec k obzoru. VRAM je 293 MB.

## Fáze 4 – Krajina mezi středem a hranicemi (hotovo)

Všechno v pásu vzniká až po jádru, s vlastními generátory náhodných čísel a vlastními počty. Otisky jádra
(`seed-N.txt` a `seed-N.glassworks.txt`) se nezměnily.

1. **Silnice** (`World.Outland.cs`, `OutlandLayout`, `OutlandRoadbed`) mají vlastní síť `World.OutlandRoutes` vedle
   `World.Routes`.
   - **Okruh** obepíná jádro ve vzdálenosti asi 790 m. Má 160 bodů po zhruba 33 m.
   - **Spojky:** až čtyři, vedou od krajních bodů starých silnic. Spojka smí vést jen místy, kde nic z jádra nestojí a
     není voda.
   - **Silnice k hranicím:** jedna ke každé hranici. Končí 42 m před okrajem hry: u úpatí hor, na pláži, u břehu
     hraniční řeky a u lesa.
   - **Body na okruhu:** kde se silnice napojí na okruh, vloží se nový bod a body okruhu bližší než 15 m se vynechají.
     Křivka přes dva blízké body mezi vzdálenými by se jinak zauzlila.
   - **Výška silnic:** `OutlandRoadbed` ji vyhladí a omezí stoupání v obou směrech. Konce silnic jsou přišpendlené na
     výšku staré silnice, ke které se připojují. Silnice nikde nevede pod nivou řeky.
   - **Terén pod silnicemi** se vyrovná jen za hranicí jádra, takže body otiskové mřížky ani postavy se nehnou.
2. **Přechody:** okruh přejde dnešní řeku na severu i na jihu po dřevěném mostě (`OutlandLayout.Bridges`), který
   vypadá jako starý. Břehy se k mostu zvednou v `River.ShapeTerrain`.
3. **Rostliny** (`Vegetation.Outland.cs`):
   - **Hustota** je stejná jako v jádru, tedy asi 3,9× víc kusů. Na seedu 42 je v pásu 15 405 stromů a 8 740 keřů,
     v jádru 3 823 stromů a 2 044 keřů.
   - **Stavba po blocích:** při načtení se jen určí, kde která rostlina stojí. To potřebuje tráva, zvířata, věci u
     cest i kontroly. Rostliny samotné, s kmeny, kameny a šlapáním, se stavějí po blocích 64 m (`StreamOutland`,
     systém „outland plants“ v `Game.Systems.cs`):
     - blok, jehož nejbližší bod je do 308 m od kamery, hned;
     - blok do 400 m napřed, nejvýš 3 ms za snímek.

     Postavený blok už zůstane. Části rostlin berou náhodná čísla z bloku, takže strom vypadá stejně, ať se postaví
     kdykoli. Na přesném tvaru rostlin pásu ale nezáleží: uživatel potvrdil, že stromy, květiny a motýli se mezi
     načteními smějí lišit.
   - **Kde rostou:** háje, louky, stromy a keře u silnic a květiny. Vrby rostou u řeky, jehličnany pod horami.
   - **Kde nerostou:** na pláži, ve vodě, na silnicích ani na mostech.
   - **Další vrstvy:** tráva roste až k okraji hry, `Wayside` má druhý průchod podél nových silnic a v pásu je asi 1 090
     divokých zvířat. Zvířata dál než 145 m se skryjí.
   - **Tři úrovně detailu**, v blocích po 64 m jako v jádru:
     - do 130 m plné tvary;
     - do 300 m zjednodušené tvary bez obrysu;
     - dál jeden kužel nebo jedna koruna na strom, bez stínu, keřů a kamenů (`BuildDistantTrees`).
   - **Plnění MultiMeshů:** instance se teď předávají jedním polem (`InstanceBuffer`), nejen v pásu, ale i v jádru.
     Obraz se tím nemění.
   - **Zatřesení stromem:** strom si místo záznamu o každé části pamatuje jen úseky svých částí v dávkách
     (`TreeRun`). Klidové polohy částí se čtou z dávek, až když do stromu auto narazí, jako u šlapání. Dřív to
     bylo asi 250 MB.
4. **Otisk:** `Tests/Worlds/seed-N.outland.txt` obsahuje:
   - poloměr okruhu a mosty;
   - každou silnici;
   - hash bodů silnic;
   - výšky pásu po 25 m;
   - počty stromů a keřů a hash poloh stromů;
   - hash zvířat.

   `world-fingerprint-checks` porovná všechny tři soubory.
5. **Kontroly** (`outland-checks` a `wildlife-checks`):
   - silnice:
     - zůstanou uvnitř údolí;
     - nevedou vodou ani stromem;
     - mají mírné stoupání a povrch v celé délce;
   - po obou mostech projede auto;
   - rostliny:
     - v pásu je přes trojnásobek stromů i keřů jádra;
     - žádný strom nestojí na špatném místě;
     - kolem bodu okruhu se postaví a plné tvary se kreslí jen blízko kamery;
   - zvířata: v jádru jich je pořád 240 a v pásu přes 600.

   Všechno prošlo na 42 seedech (1 až 40, 42, 7421).
6. **Výkon:** benchmark má dvě nové scény: `jizda-pas` (jízda po okruhu) a `jizda-k-hranicim` (silnice k hranicím).
   `-Off outland-plants` vypne rostliny pásu.

**Měření** (GTX 1060, seed 42, 1920 × 1080, FPS a v závorce 1 % low):

| scéna | hotová fáze 3 | celý pás postavený najednou | pás po blocích | po blocích, bez rostlin pásu |
|---|---|---|---|---|
| jízda ve dne | 101,5 (80,0) | 96,6 (75,0) | 100,6 (78,3) | 104,6 (80,0) |
| jízda v bouřce | 109,6 (85,7) | 107,2 (85,7) | 108,1 (81,8) | 110,6 (88,8) |
| náves ve dne | 93,4 (90,0) | 87,1 (75,0) | 90,7 (85,7) | 93,8 (90,0) |
| jízda v noci | 87,3 (68,6) | 82,2 (65,5) | 85,8 (67,0) | 88,7 (68,6) |
| náves v noci | 86,8 (77,6) | 81,1 (75,0) | 85,4 (80,0) | 84,4 (53,1)\* |
| jízda pásem | – | 130,2 (102,9) | 118,8 (101,0) | 208,6 (157,8) |
| jízda k hranicím | – | 149,3 (110,0) | 141,5 (120,0) | 210,8 (180,0) |

\* Zaškobrtnutí ovladače grafiky (snímek 210 ms na GPU), stalo se i dřív. S rostlinami pásu nesouvisí.

Splněno: 1 % low v pásu (101,0) i u hranic (120,0) je lepší než při jízdě v jádře (78,3). Při jízdě pásem, kdy se
bloky stavějí za jízdy, trval nejdelší snímek 14,8 ms. Rostliny pásu stojí v jádru nejvýš 4 % průměrných FPS.

**Paměť a načítání:**

| | celý pás najednou | pás po blocích |
|---|---|---|
| načtení hry (okno) | 10,9 s | 7,5 s |
| paměť hry po načtení (okno) | 3,7 GB | 1,8 GB |
| paměť po jízdě pásem a k hranicím | 4,3 GB | 2,5 GB |
| jedna kontrola (bez okna, nejvíc) | 1,9 GB | 1,15 GB |
| 16 kontrol najednou | 29 GB | 16,7 GB |
| celá sada kontrol (103 případů) | 152 s | 109 s |

Hráč, který projede celý pás, se dostane zhruba na paměť staré verze, kdy se pás stavěl celý najednou; ta běžela
také plynule.

Když byl pás postavený celý najednou, šestnáct kontrol najednou zabralo 29 GB. Na počítači s 32 GB RAM pak některá
občas spadla při ukončení, až po `…_PASSED` (asi 1 ze 60 běhů). Po přechodu na bloky prošla celá sada se 16 kontrolami
najednou i 80 dalších běhů `outland-checks` a `playground-checks` bez pádu.

**Sítě až po startu:** výšky, polohy a kolize se dál počítají při načtení. Sítě, které jen kreslí, se stavějí až po
startu po kouskách (`DeferredBuild`, `World.FarParts`, systém „far parts“ v `Game.Systems.cs`):
- terén pásu: nejdřív vzdálené bloky, kulisy a obzor, pak jemné dlaždice od středu;
- okraje silnic jádra, polních cest a pásu.

Na úvodní obrazovce a v pauze na to připadá nejvýš 25 ms za snímek, při jízdě 5 ms. Kontroly bez okna tyto sítě vůbec
nestavějí, `smoke-test` ověřuje, že se za běhu dostaví. Snímky (`run.ps1 -Capture`) a `benchmark.ps1` je postaví hned.
`build.ps1` už po sestavení nespouští celou hru, jen ověří, že se projekt načte; dřív se tak při `run.ps1` svět
načítal dvakrát.

| seed 42 | vše při načtení | sítě po startu |
|---|---|---|
| okno hry se ukáže | 7,6 s | 6,3 s |
| sítě dostavěné | – | za dalších 2,3 s (50 snímků) |
| součet časů všech 103 kontrol | 2 703 s | 2 506 s |

Odchylky od plánu:
- Brody přes řeku nejsou, jen dva mosty.
- Stromy ve vzdálených blocích nejsou zjednodušené kopie dávek, ale samostatné jednoduché tvary.
- Rostliny pásu se stavějí po blocích kolem kamery. Jejich polohy jsou v otisku, jejich tvary ne.

## Fáze 5 – Obsah nového území (odloženo)

Uživatel rozhodl, že obsah se zatím přidávat nebude. Nové území je zatím krajina k projížďce. Návrhy zůstávají pro
pozdější práci.

První vybraný obsah, pošta do čtyř koutů a maják na mysu, má vlastní plán v `docs/POST_AND_LIGHTHOUSE_PLAN.md`.

Bez cílů bude nové území jen hezká a prázdná krajina. Návrhy, každý jako samostatný PR podle pravidel v `CLAUDE.md`
(role v `ActorRoles`, ID v `ActorIds`, cesta s přívěsem jako `ITripTarget`, texty v `Texts.cs`, nová sekce v uložené hře
s pravidlem v `SaveRules`):

- **Moře:** maják, který se staví z dovezeného kamene, přístav s lodí a pláž s mušlemi do sbírky.
- **Hory:** lom nebo důl (druhý zdroj kamene) a horské jezero s vyhlídkou.
- **Les:** hájovna, pila (dřevo z lesa místo kmenů z údolí) a hledání zvířecích stop.
- **Meziříčí:** převozník u hraniční řeky (jen vyhlídka, za řeku se nejezdí) a pastviny.
- Pohlednice a pohledy z rozhledny na všechny čtyři hranice.

Cesty s přívěsem by měly začínat i končit hlavně v jádru nebo blízko něj, aby jízda tam a zpět netrvala dítěti přes
dvě minuty.

## Rizika

- **Doba načítání.** Jádro se během plánování přepočítává několikrát. Pás proto musí mít vlastní pole výšek a nesmí se
  přepočítávat s jádrem. Pokud ani tak nebude načítání rychlé, mohou se dlaždice pásu dopočítávat až během hry
  (podobně jako dnes tráva v `Grass`).
- **Výkon rostlin.** Rostlin bude asi 4,5× víc. Podle posledních měření hru brzdí geometrie a počet objektů na
  obrazovce, ne rozlišení, takže tady leží největší práce s úrovněmi detailu.
- **Nechtěná změna jádra.** Hranice se dá snadno prolomit, například malou chybou ve výpočtu masky nebo novou
  silnicí, která vyrovná terén u otiskové mřížky. Proto se otisky kontrolují po každém commitu, ne až na konci.
- **Délka jízd.** Šestinásobná plocha znamená až dvě minuty jízdy jedním směrem. Případně se dá zvážit rychlejší auto
  na nových dlouhých silnicích. To ale mění pocit z jízdy, a proto o tom rozhoduje uživatel.

## Rozhodnutí, která čekají na uživatele

1. Sedí rozmístění stran (hory na severu, moře na jihu, hraniční řeka za dnešní řekou, les na opačné straně)?
2. Co má být na novém území? To se týká až fáze 5, která je zatím odložená.
