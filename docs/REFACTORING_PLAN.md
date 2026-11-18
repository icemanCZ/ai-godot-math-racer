# Implementační plán: architektonické úpravy

Plán vychází z architektonického review (osm nálezů, z toho pět doporučených oprav). Nic z něj zatím není provedeno.

## Pravidla pro všechny fáze

- **Jedna fáze = jedna větev = jeden PR.** Každý krok uvnitř fáze je samostatný commit, po kterém prochází `./build.ps1 -Test`.
- **Chování se nemění.** Fáze 1–5 jsou čistě strukturální. Jediná změna viditelná hráči je případná oprava chyby z kapitoly „K ověření“.
- **Staré uložené hry se v žádném případě nesmí rozbít.** Je to nepřekročitelná podmínka celého plánu, viz kapitola níže. Platí před jakýmkoli jiným cílem, včetně úspory času a čistoty návrhu.
- Číselné hodnoty `GameState`, ID postav a pořadí v `CharacterCatalog` zůstávají. Formát JSON se mění jen přes migrace (fáze 2 a 4).
- **Nejdřív sít, potom změna.** Fáze 0 musí být hotová dřív než cokoli, co sahá na `Game`, `SaveData` nebo trasy.
- **Mechanické přesuny a změny logiky se nemíchají** v jednom commitu (přejmenování zvlášť, přesun kódu zvlášť, změna chování zvlášť).

## Nepřekročitelná podmínka: staré uložené hry musí fungovat

Každá hra uložená jakoukoli dřívější verzí (včetně verzí 1, 2 a 3 formátu a hry uložené uprostřed aktivity) se musí po aktualizaci načíst a pokračovat stejně, jako by se nic nestalo. Žádná fáze se nesmí sloučit, dokud tohle neplatí.

### Brány, které musí splnit každý PR

1. **Fixtury jsou nedotknutelné.** Soubory v `Tests/Saves/` se po vytvoření ve fázi 0 nikdy neupravují ani nepřegenerovávají novým kódem. Nová verze formátu dostane **vlastní** složku (`v4`, `v5`), stará zůstává. Změna fixtury ve starém adresáři v PR znamená automatické zamítnutí.
2. **Každá fixtura se načte a hra z ní pokračuje.** `save-checks` pro každou fixturu: načíst, validovat, obnovit, znovu zachytit a ještě simulovat několik vteřin hry (aktivní trasa se dokončí, panel se zavře, kvíz se vyřeší). Nestačí, že načtení nevyhodí výjimku.
3. **Migrace se nikdy nemažou.** Kroky `V1→V2`, `V2→V3` a každý další zůstávají v řetězu natrvalo. Úklid „nepoužívaných migrací“ je zakázaný.
4. **Validace se nezpřísňuje.** Nové invarianty smí odmítat jen data, která dnešní kód sám nikdy nezapíše. Kdo zpřísní kontrolu, musí doložit, že žádná fixtura ani žádný výstup `CaptureProgress` z dnešní verze ji neporuší. Chybějící pole se doplňují výchozí hodnotou, nikdy se neodmítají.
5. **Zachovává se, co hráč nasbíral.** Test porovná po načtení: mince, vlastněné položky garáže, společníci, stupně staveb a parcel, sklad dílen, pohlednice, rekordy duchů, výchozí kvízová nastavení a seed světa. Hodnota, která se při migraci ztratí, shodí test.
6. **Seed a svět zůstanou stejné.** Rozmístění světa ze seedu se při refaktoringu nesmí změnit (generátory dál používají stejné posuny `Seed + N` ve stejném pořadí). Test z fáze 0 porovná otisk světa (pozice postav, staveb a pickupů) pro seedy 1, 42 a 7421 před a po každé fázi.

### Ochrana skutečných uložených her hráče

- **Záloha před prvním přepsáním novým formátem.** Když hra načte soubor starší verze, zkopíruje ho před prvním zápisem do `progress.v<N>.bak` (N je původní verze) a nikdy ho nepřepíše. Starší `.bak` se zachovají.
- **Soubor, který se nepodaří načíst, se nesmí ztratit.** Při selhání migrace nebo validace se původní `progress.json` přesune do `progress.nenacteno-<čas>.json`, než ho nová hra přepíše. Ověřit, jak se to chová dnes (viz „K ověření“), a když ne, doplnit to ve fázi 0, tedy před jakoukoli změnou formátu.
- **Ruční test na skutečných datech.** Před sloučením fáze 2 a fáze 4 se uloží kopie hráčova skutečného `progress.json` (a `.bak`), načte se v nové verzi a prohlédne se, že hra pokračuje: stejná poloha, mince, rozestavěné stavby a otevřený úkol.
- **Jen jeden směr kompatibility.** Nové verze hry musí číst všechny staré uložené hry. Naopak **nové uložené hry se ve starší verzi hry načítat nemusí**. Starší verze takový soubor může odmítnout, a tím se nezabýváme: nepřidává se zpětně zapisovaný formát, pole „pro starší verze“ ani přepínač verze zápisu.
- **Návrat ke staré verzi hry** je možný jen ručně přes zálohu `progress.v<N>.bak` (hráč přijde o postup od aktualizace). Nová verze formátu se proto vydává až po úspěšném ručním testu.

## Přehled a pořadí

| Fáze | Co | Řeší nález | Odhad | Riziko |
| --- | --- | --- | --- | --- |
| 0 | Záchytná síť (fixtury uložených her, kontraktní testy) | podmínka všeho | 1–2 dny | nízké |
| 1 | Rychlé zisky: `Is…` příznaky, registr ID postav, registr diagnostik | 3, 4, 7 | 2–3 dny | nízké |
| 2 | Sjednocení trailerových tras (`HaulTrip`) + uložení `Trip` (verze 4) | 2 | 4–6 dnů | střední |
| 3 | Vytažení stavu z `Game` do malých objektů | 1, 3 | 5–8 dnů | střední až vyšší |
| 4 | Sekce v `SaveData`, řetěz migrací (verze 5) | 5 | 3–4 dny | střední |
| 5 | Úklid: přejmenování `Animal`, texty, magická čísla, čisté testy | 4, 6, 8 | 3–5 dnů | nízké |

Pořadí je dané závislostmi: fáze 2 potřebuje fixtury (0) a čisté role (1), fáze 3 potřebuje sjednocené trasy (2), fáze 4 staví na tom, že každý podsystém umí sám říct, co ukládá (3).

---

## Fáze 0: záchytná síť

Cíl: odhalit regresi dřív, než ji pozná hráč.

**Stav: provedeno.** Co přesně vzniklo, kde je to a co zůstalo mimo:

| Krok | Výsledek |
| --- | --- |
| 1 Základní běh | všech 53 scénářů prošlo (`./build.ps1 -Test`, výstup 0) |
| 2–3 Fixtury a jejich test | 33 fixtur v `Tests/Saves/v3/`, test `save-fixture-checks` (viz `Tests/README.md`) |
| 4 Kontraktní testy | `contract-checks` pro seedy 1, 42, 7421 |
| 5 Otisk světa | `Tests/Worlds/seed-{1,42,7421}.txt`, test `world-fingerprint-checks` |
| 6 Nenačitatelný soubor | potvrzeno, že se přepisoval bez zálohy; doplněna ochrana v `SaveStore.Preserve`, testovaná v `save-checks` |

Co fixtury **nepokrývají** (vědomě, aby fáze 0 zůstala hotová): rozdělanou pohlednici a výhled z věže. Uložení uprostřed kvízu před odjezdem je pokryté šesti fixturami `quiz-before-…-trip` až po opravě chyby (viz „K ověření“, bod 1). Fixtury verzí 1 a 2 jsou dál jen syntetické, v `save-checks`; v repozitáři není žádný soubor z opravdu staré verze hry. Doplnit je lze sestavením staršího commitu a uložením hry v něm; nový kód starý zápis nevyrobí.

1. **Základní běh.** Spustit `./build.ps1 -Test` na čisté větvi a uložit výsledek jako referenci (doba běhu, počet scénářů). Co už teď padá, se řeší zvlášť a před fází 1.
2. **Fixtury uložených her.** Přidat diagnostický režim `--save-fixtures`, který pro seed 42 vygeneruje a uloží JSON do `Tests/Saves/v3/`:
   - jezdění bez aktivity,
   - rozdělený kvíz (každý typ),
   - aktivní rozvoz a sběr jablek,
   - rozjetá trasa každého druhu (stavba, zásobování dílny, dům, parcela), a to naložená i nenaložená,
   - otevřený panel (garáž, stavitel, parcela, dílna),
   - bandité, závodní běh s duchem, rozdělaná pohlednice.
3. **Test načtení.** `save-checks` rozšířit: každá fixtura se načte, prověří `SaveStore.Validate`, obnoví do `Game` a znovu zachytí přes `CaptureProgress`. Výsledek se musí rovnat vstupu (po normalizaci polí, která se mají změnit migrací). Tento test je hlavní pojistka fází 2, 4.
4. **Kontraktní testy** (nový `contract-checks`, běží bez světa, pár sekund):
   - hodnoty `GameState` jsou zmrazené (tabulka jméno → číslo v testu),
   - každá hodnota `GameState`, která je interakcí, má panel v `ShowPanel` (zatím přes reflexi nad enumem a zkušební volání),
   - ID postav jsou jednoznačná a rozsahy v `World` na sebe navazují,
   - pořadí v `CharacterCatalog` odpovídá zmrazeným ID.
5. **Otisk světa.** Pro seedy 1, 42 a 7421 uložit do `Tests/Worlds/` otisk (pozice postav, staveb, parcel, pickupů, bodů silnice) z neupraveného kódu. Test `world-fingerprint-checks` ho porovnává při každém běhu.
6. **Chování při nenačitatelném souboru.** Ověřit, co dnes hra udělá s poškozeným nebo nevalidním `progress.json`, a doplnit ochranu z kapitoly „Ochrana skutečných uložených her“ (přesun do `progress.nenacteno-<čas>.json`, `progress.v<N>.bak` před prvním přepisem), pokud chybí.
7. **Hotovo, když:** `contract-checks`, rozšířené `save-checks` a `world-fingerprint-checks` běží v `build.ps1 -Test` a všechny fixtury projdou na současném kódu.

## Fáze 1: rychlé zisky

**Stav: provedeno** (1a, 1b i 1c). Odchylky od plánu: `Animal` ztratila všech 8 příznaků i oba indexy (`IsCollectionOwner` nikdo nepoužíval); `World.FarmerId` a ostatní názvy zůstaly jako aliasy `ActorIds`, aby se nemuselo přepisovat 89 míst; seznam diagnostik drží i hodnoty `NeedsStart`, `ContinuesGame` a `FixedFps`, které byly dřív rozeseté po `Game.Diagnostics.cs` a `build.ps1`.

### 1a. Role místo `Is…` příznaků (nález 4)

Použití: `Game.Village.cs` (8), `Game.cs` (7), `Expedition.cs` (6), `World.cs` (2), `Game.Trades.cs` (2), po jednom v `GameUi*.cs`, `Game.Works/Rewards/Postcards/Events.cs`, `DrivingEvents.cs`; v diagnostice `FarmChecks`, `GardenChecks`, `TradeChecks`.

1. U každého použití rozhodnout: vyhovuje porovnání vzoru (`actor.Role is TraderRole t`), nebo patří chování do role jako virtuální člen (např. `ActorRole.StartsExpedition`, `ActorRole.Celebrates…`)?
2. Přepisovat po souborech, po každém souboru build + `delivery-checks`, `village-checks`, `trade-checks`, `farm-checks`, `garden-checks`, `collection-checks`.
3. Až nejsou použití, smazat `IsFarmer`…`IsCollectionOwner`, `TradeIndex`, `HouseIndex` a `IsTrader` z `Animal`. Kde potřebujeme index, bere se z role.
4. **Hotovo, když:** v `Animal` nezůstane žádná vlastnost, která jen odráží typ role.

### 1b. Jednotný registr ID postav (nález 3)

1. Nová statická třída `ActorIds` (soubor `Scripts/ActorIds.cs`) s pojmenovanými rozsahy: roadside 0–9, rezidenti 30–33, sběratelé 34–37, obchodníci 38–40, čarodějnice 41, kouzelník 42, bandita 43, stavitelé 44 a 48–50, dílny 45–47, zdroje 51–57, parcely 58–63. Konstanty z `World.cs` se na ni přesměrují.
2. `World.RegisterActors` registruje přes `ActorRegistry.Add(id, actor)`, které při kolizi vyhodí výjimku s oběma jmény postav.
3. Test z fáze 0 začne číst rozsahy z `ActorIds`, takže nové ID bez zapsaného rozsahu test shodí.
4. **Hotovo, když:** žádné číselné ID postavy v kódu nezůstane mimo `ActorIds`, kromě uložených dat ve fixturách.

### 1c. Registr diagnostik (nález 7)

1. Vytvořit `DiagnosticSuite` (název, příznak `--x-checks`, továrna `Func<Game, World, Vehicle, Node>`, `NeedsStart`, seedy, váha pro řazení). Seznam všech sad je jedno pole v `Scripts/Diagnostics/Suites.cs`.
2. `Game.Diagnostics.cs` projde registr místo řetězu `if (args.Contains(...))`.
3. Nový režim `--list-checks` vypíše registr jako JSON. `build.ps1` ho načte místo ručního seznamu `New-GameTestCase` (road-audit a road-driving zůstanou zvlášť, protože mají jiný start).
4. Test v `contract-checks`: každý soubor `Scripts/Diagnostics/*Checks.cs` je v registru.
5. **Hotovo, když:** nová sada se přidává na jednom místě a `build.ps1` ji sám zahrne.

## Fáze 2: sjednocení trailerových tras (nález 2)

**Stav: provedeno.** Odchylky od návrhu níže:

- `HaulTrip` je jedna třída; `ITripTarget` mají `BuildProject`, `HousePlot`, `Works` a nový `HouseTripTarget`. `HaulTrip` má typované přístupy `Project`, `Plot`, `Works`, `House`, `Step`, `FarBank`, kterými se pozná, co trasa obsluhuje.
- `FinishDump` je na cíli (`ITripTarget.FinishDump`) a jen volá `Game.FinishBuildDump` a spol. Kód odměn a oslav zůstal v `Game.*.cs`.
- Panely `OpenBuilder`, `OpenPlot`, `OpenHouse` a `OpenWorks` se **nesjednotily**: liší se kontrolami i texty a jejich sloučení by změnilo chování. Společné je jen zahájení trasy (`Game.Trips.cs`: `StartTrip`) a návrat po kvízu (`StartDeparture`, tabulka `PanelTrips`).
- `Departure` zůstal výčtem, protože se ukládá (`SaveData.Departure`); `PendingDeparture` s uzávěrem by se po načtení nedal obnovit.
- Uložení: `SaveData.Trip` (verze 4). Staré páry polí zůstaly jen pro čtení jako `int?`/`bool?` a `MigrateTrips` je převádí. Fixtury verze 4 jsou v `Tests/Saves/v4/`, složka `v3` se nezměnila a načítá se přes migraci.
- **Ruční zkouška na skutečném souboru** (kopie `progress.json`, verze 3, bez trasy, postup ve stavbách i v zásobách dílen): načetla se, přemigrovala na verzi 4, obnova a znovu zachycení daly stejné uložení a hra běžela dál. Originál zůstal beze změny. Provedl to nástroj `--load-save-checks --seed=N --save-file=PATH`, který načítá jen kopii. Při načtení projektu v `build.ps1` se navíc sám vytvořil `progress.v3.bak`, tedy ochrana z fáze 0 funguje na skutečném souboru.
- Po refaktoringu zbývají místa, která o druzích tras vědí: `Game.TripTarget` (obnova ze save), `SaveStore.HasInvalidTrip` a `ActiveTripAsker`, `Game.PanelTrips`, `Game.TripInstruction`. `CaptureProgress`, `HaulTrip`, `FinishDump` a `SaveData` se pro nový druh nemění.

Dnes: `BuildTrip`, `HouseTrip`, `PlotTrip`, `SupplyTrip`; čtyři trojice `Open…` / `Begin…Trip(bool)` / `Finish…Dump`; ruční `switch` v `StartDeparture` a `FinishDump`; ruční větve v `RestoreBuilds`; v `SaveData` osm polí; čtyři validátory a `ActiveTripAsker` v `SaveStore`.

### Cílový návrh

```csharp
// Co o trase ví cíl (budova, parcela, dům, dílna).
internal interface ITripTarget
{
    string TripKind { get; }          // "build" | "plot" | "house" | "supply", ukládá se
    int Index { get; }                // index cíle v daném druhu
    Animal Contractor { get; }        // kdo trasu zadává a děkuje
    bool Complete { get; }
    HaulLoad? NextLoad { get; }       // náklad pro další jízdu, null když je hotovo
    int Step { get; }
    Vector3 Drop { get; }
    Vector3 DropRoad { get; }
    string Counter { get; }           // "3 / 10"
    void FinishDump(Game game, HaulTrip trip);   // zvýší stupeň, odměna, oslava
}

// Jedna třída místo čtyř.
public sealed partial class HaulTrip : Node3D { ITripTarget Target { get; } ... }
```

Panel, zahájení a obnova jsou **jeden tok** v `Game.Trips.cs` místo čtyř kopií: `OpenContractor(actor)`, `BeginTrip(target, quizzed)`, `RestoreTrip(SavedTrip)`. Texty a velikost panelu zůstávají v cíli (`TripTexts`), protože se liší.

### Kroky

1. **Rozhraní `ITripTarget`** přidat a implementovat ho na `BuildProject`, `HousePlot`, nový `HouseTripTarget` (dům) a `Works`. Čtyři podtřídy `HaulTrip` zatím zůstanou a čtou z cíle. Build + testy.
2. **Společný tok.** `Game.Trips.cs`: sjednotit `OpenBuilder` / `OpenPlot` / `OpenHouse` (materiálová větev) / `OpenWorks` a `Begin…Trip(bool)`. Starý kód se maže po jednom druhu: nejdřív parcely (nejnovější, nejméně specifické), pak stavby, domy, dílny. Po každém druhu `plot-checks`, `build-checks`, `ford-checks`, `works-checks`, `village-checks`.
3. **`Departure` bez enumu.** Místo `enum Departure` a `switch` v `StartDeparture` držet `PendingDeparture { Action Start; GameState ReturnState; bool CloseWhenRefused }`. Golem a jablka mají vlastní lambdu. Zmizí třetí místo, kde je potřeba znát všechny druhy tras.
4. **Sloučení podtříd** `BuildTrip`, `HouseTrip`, `PlotTrip`, `SupplyTrip` do jednoho `HaulTrip`. `FinishDump` se volá přes `Target.FinishDump`. Zmizí všech ~20 testů `Haul is XTrip`.
5. **Uložení trasy, verze 4.** `SaveData.Trip` (`{ Kind, Index, Loaded }`, nebo `null`) nahradí osm polí `BuildTrip`…`PlotTripLoaded`. Staré fixtury (v3) projdou novou migrací `MigrateTrips`, stejně jako `MigrateBuilds` dnes. Validace: jedna obecná `HasInvalidTrip` (index v rozsahu, trasa neběží zároveň s `Sender >= 0`, `Loaded` jen s trasou) a kontrola cíle přes `ITripTarget` (parcela není hotová…). `ActiveTripAsker` se ptá cíle.
6. **Aktualizovat** `docs/ARCHITECTURE.md` (řádky o `HaulTrip`, `Works`, `HousePlot`, uložení) a fixtury: přidat v4 sadu vedle v3.

**Podmínka sloučení:** všechny brány z kapitoly „Nepřekročitelná podmínka“, zvlášť pro každou v3 fixturu s rozjetou trasou (naloženou i nenaloženou) a pro uložení uprostřed kvízu před odjezdem.

**Hotovo, když:** nový druh trasy znamená jednu implementaci `ITripTarget`, jeden řádek v registru druhů, nový `GameState` a text panelu; ve `SaveData` ani v `SaveStore` se nic nemění. Ověřit na zkoušce: dočasně přidat „studnu“ jako pátý druh a spočítat dotčené soubory (cíl ≤ 5, dnes 21).

## Fáze 3: stav z `Game` do malých objektů (nálezy 1, 3)

**Stav: provedeno** v menším rozsahu, než plán čekal. Co vzniklo: `Economy`, `QuizSession`, `TaskSlot`, `Interaction` (258 řádků, žádné uzly scény), seznam systémů `Game.Systems.cs` a registr panelů `Game.Panels.cs`. `Game` si nechala vlastnosti, které používaly ostatní části a testy (`State`, `Delivery`, `Haul`, `CurrentChallenge`, `Coins`), a ty teď předávají.

Odchylky a čísla, pro úplnost:

- **Velikost `Game` se skoro nezměnila:** všechny soubory `Game.*.cs` mají 3 295 řádků (před fází 3 jich bylo 3 328), `Game.cs` 628 (dřív 719). Cíl ≤ 2 500 se nesplnil a **s tímto postupem splnit nejde**: stav se přesouvá, ale velké části (pohlednice, bandité, závody, společníci) zůstaly v `Game`, protože plán je přesouvat nechtěl. Číslo 5 388 z review počítalo i `GameUi*` a `GameControls`, skutečná třída `Game` měla 3 328 řádků.
- `ShowPanel` už není `switch`, ale slovník `GameState → panel`. Kontrola v `contract-checks` se tím změnila z čtení zdrojového kódu na dotaz do registru (`Game.HasPanel`).
- `Interaction` je jen stav a přechody pauzy. `EnterInteraction`, `Resume` a `TogglePause` zůstaly v `Game`, protože zastavují auto a ukazují panel.
- `Economy` a `Interaction` jsou malé; větší užitek mají `QuizSession` (generátory a rotace bez scény) a `TaskSlot` (vkládání a odebírání úkolu ze scény na jednom místě).
- **Nalezeno cestou, není z refaktoringu, vyřešeno:** `track-checks` selhával na některých náhodných seedech (42, 1706514621) už na původním commitu `c742645`. Příčina byla v testu, ne ve stopách kol: při plném zatočení doprava auto narazí do kmene stromu asi 10 m od startu, zaklíní se a po pěti vteřinách ho `Vehicle` vrátí na start; ten skok o 9 m test vyhodnotil jako mezeru ve stopách. Test teď skoky způsobené `PoseReset` přeskakuje a v registru má pevné seedy 1, 42 a 7421. Prošel i na seedech 1–64.

Princip: **přesun, ne přepis.** Každý objekt vezme pole a metody, které dnes leží v `Game`, a `Game` na něj jen deleguje. Pořadí od nejmenšího rizika:

| Krok | Objekt | Co převezme | Dnes v |
| --- | --- | --- | --- |
| 3.1 | `Economy` | `Coins`, `rewardCoins`, `CarRewards`, `PayReward` | `Game.Coins.cs`, `Game.Rewards.cs`, `Game.cs` |
| 3.2 | `TaskSlot` | `Delivery`, `Haul`, `CancelActiveTask`, `StartHaul`, `EndHaul`, `EndExpedition`, `TaskActive`, přípojka vozíku | `Game.cs`, `Game.Ford.cs` |
| 3.3 | `QuizSession` | `CurrentChallenge`, `rejectedAnswers`, `syllableSelection`, `handoverQuiz`, `PendingDeparture`, `enabledQuizzes`, `threeTermMath`, `nextQuiz`, generátory | `Game.Quizzes.cs`, `Game.cs` |
| 3.4 | `Interaction` | `State`, `beforePause`, `nearby`, `EnterInteraction`, `Resume`, `TogglePause` | `Game.cs` |
| 3.5 | `IGameSystem` | `Tick…` metody jako seznam systémů (`Companion`, `Golem`, `CollectorPets`, `Events`, `Postcards`, `Bandits`, `Coins`, `Mill`, `Lookout`) | `_Process` v `Game.cs` |
| 3.6 | `PanelRegistry` | `ShowPanel` jako slovník `GameState → Func<bool>`; jeden zápis při registraci podsystému | `Game.cs` |

Poznámky:

- **3.6** zároveň splní kontraktní test z fáze 0 (každý stav interakce má panel) a odstraní ruční komentář u `GameState`.
- Podsystémy (`Bandits`, `Postcards`, `Events`, `Golem`…) se **nepřepisují**. Dostanou jen rozhraní `IGameSystem` a vlastní pole zůstanou, kde jsou. Pole používaná více partialy se přesunou do objektů 3.1–3.4.
- Po každém kroku: celý `./build.ps1 -Test`. Rozdíl doby běhu oproti základu z fáze 0 musí zůstat v toleranci.
- `Game` smí po fázi mít **řádově 800–1 200 řádků** (kompozice, vstup, životní cyklus). Pokud je víc, přesun nebyl dost důkladný.

**Hotovo, když:** `Game` nemá žádné pole se stavem, které mění víc než jedna feature, a stavy a panely se přidávají registrací.

## Fáze 4: struktura `SaveData` (nález 5)

**Stav: provedeno** (verze 5). Odchylky od návrhu níže:

- **Sekce jsou datové třídy**, ne rozhraní `ISaveSection`: `Vehicle`, `Economy`, `Quiz`, `Task` (výprava nebo trasa), `Roadside`, `Village`, `Buildings`, `Companions`, `Postcards`, `Events`, `Bandits`. Pravidla jsou seznam `SaveRules.All`, jedno pravidlo na část; pravidlům nevadí, že se odkazují na jiné části uložení (postava, panel, trasa), protože vidí celé `SaveData`.
- **Starý plochý tvar je `SaveDataV4`** (jen čtení, nikdy se nemění) a `SaveMigrations.ReadLegacy` + `ToCurrent` ho převedou. Soubor verze 5 se čte přímo. Opravy po verzích (`Repair`, `AfterValidation`) zůstaly ve stejném pořadí jako dřív, takže pořadí validace a oprav se nezměnilo.
- **Řetěz migrací** zůstal tvaru „kroky 1→2→3→4 na ploché třídě, pak jeden převod do sekcí“, ne nový řetěz na každou verzi: kroky 1–4 jsou beze změny a pro ně nikdo nechce další převod. Nový tvar dostane vlastní krok, až bude verze 6.
- **Chyba z fáze 2 opravena při přesunu pravidel:** pravidlo pro obchody (`TradeStatus`) po fázi 2 porovnávalo staré `int?` pole tras s nulou, takže se vždy vyhodnotilo jako nepravdivé a uložení „stav obchodu bez úkolu“ se přijímalo. Teď kontroluje `Task.Sender` a `Task.Trip` jako dřív.
- **Testy:** všech 117 fixtur (v3, v4, nové v5) se načítá a hra z nich pokračuje; u v4 se navíc ověřuje, že se žádné pole při převodu neztratilo ani nezměnilo (zploštění výsledku je původní soubor). `LegacySaves` (diagnostika) vyrábí uložení ve starém tvaru z aktuálního, a testy migrací starších verzí (v1, v2, v3) ho používají. Kopie skutečného uložení hráče (verze 3) se načetla jako verze 5.

1. **`ISaveSection`** pro každý podsystém: `string Name`, `void Capture(SaveBuilder)`, `void Restore(SaveReader)`, `bool Validate(SaveReader)`. Sekce vznikají přirozeně z fáze 3: `Economy`, `TaskSlot`, `QuizSession`, `Vehicle`, `Village`, `Buildings`, `Postcards`, `Events`, `Bandits`, `Companions`.
2. **Verze 5** ukládá `{ Version, Seed, State, Nearby, Sections: { ... } }`. Každá sekce má vlastní třídu a vlastní výchozí hodnoty.
3. **Řetěz migrací** podle verze: `Migrate(data, from)` volá `V1→V2`, `V2→V3`, `V3→V4`, `V4→V5` postupně. Současné migrace podle tvaru dat (`MigrateBuilds`, `MigrateEventRun`…) se převedou na kroky řetězu; jedna migrace = jedna čistá funkce a jedna fixtura.
4. **Starý formát** se čte přes `SaveDataV4` (plochá třída, jen pro čtení) a převádí na novou strukturu. Pole určená jen pro migraci (`FordStage`, `Math`, `Letters`…) se z živé `SaveData` odstraní.
5. **Testy:** každá fixtura z fáze 0 (v3 i v4) se načte, převede na verzi 5 a znovu zachytí. Navíc **zpětný test**: zapsaná v5 se načte a zapíše beze změny.
6. **`SaveStore.Validate`** se rozdělí do sekcí (každá zná své invarianty), `SaveStore` jen volá sekce a kontroluje vazby mezi nimi (`Sender` vs. trasa).
7. Aktualizovat `docs/ARCHITECTURE.md` a `docs/VALIDATION.md`.

**Podmínka sloučení:** všechny brány z kapitoly „Nepřekročitelná podmínka“ pro fixtury v1, v2, v3 i v4. Staré třídy pro čtení (`SaveDataV4` a starší) zůstávají v kódu natrvalo.

**Hotovo, když:** přidat uložená pole znamená vytvořit sekci s výchozími hodnotami a validací, bez zásahu do `SaveStore.Validate`.

## Fáze 5: úklid (nálezy 4, 6, 8)

Dělá se podle potřeby, každá položka samostatně.

- **Přejmenovat `Animal` na `Character`** (`Actor`). Čistě mechanicky, vlastním commitem, až po fázi 3, aby diff nezakryl logiku.
- **Texty na jedno místo.** Postupně vynést české texty z `Game*.cs` a `GameUi*.cs` do `Texts.cs` (statické členy podle funkce). Cílem není překlad, ale oddělení textu od logiky a jednodušší kontrola jazyka.
- **Rozdělit `GameUi.cs`** (1 121 řádků) podle panelů: každý panel jedna třída s `Show(...)`; společný základ jen pro rozložení a ovládání menu. Události `GameUi` se sloučí na `UiIntent` (jedna událost s typovanými záměry), čímž zmizí dalších 24 řádků kabeláže v `SetupUi`.
- **Magická čísla** z `Game`, `World` a vegetace (vzdálenosti vyhrazených míst, poloměry) do `WorldTuning` v `Settings.cs`.
- **Čisté testy.** Dvě možnosti, rozhodnout se po fázi 4:
  - (a) režim `--unit-checks` v enginu pro `ChallengeGenerator`, `SaveStore.Validate` a migrace, bez generování světa, trvá vteřiny;
  - (b) knihovna `MathRacer.Core` bez závislosti na Godotu (`Mathematics`, `Letters`, `SaveData`, validace, katalogy) a testovací projekt xUnit. Kvalitnější, ale musí se oddělit typy závislé na `Godot` (`Vector3` v datech, `ProjectSettings` v `SaveStore.SavePath`).
- **`World.GenerateVillage`** (dvojí generování vesnice): zdokumentovat jako záměr a přidat test, že rozmístění je shodné s verzí před refaktoringem; samotnou logiku neměnit, pokud neprokáže problém s výkonem.

---

## K ověření (možné chyby zjištěné při review)

Nepotvrzeno během review, nic se nespouštělo. Každou věc potvrdit nebo vyvrátit testem ve fázi 0, ať se nezamíchá do refaktoringu.

1. **Uložení uprostřed kvízu před odjezdem je potvrzená chyba (nalezena ve fázi 0).** Hra takový stav ukládá, ale při načtení ho sama odmítne: `SaveStore.HasInvalidAsker` pro stav `Challenge` u stavitele nebo nováčka na parcele čeká aktivní trasu, a ta ještě nezačala. Navíc `departure` není v `SaveData`, takže i kdyby soubor prošel, trasa po kvízu nezačne.
   - **Dopad (odvozeno ze čtení kódu, nezkoušeno na hře):** hráč, který u stavitele při kvízu před jízdou odpoví špatně, přepíše i zálohu `.bak` neplatným souborem, protože špatná odpověď změní uložený stav a hra ho do půl vteřiny zapíše znovu. Při dalším spuštění se nenačte ani jeden a hra začne nové údolí. Nově se postup zachrání do `progress.nenacteno-HASH.json` (viz `SaveStore.Preserve`), ale hra ho sama neobnoví.
   - **Stav: opraveno** samostatnou malou změnou po fázi 0. `SaveData` má nová volitelná pole `Departure` a `DepartureState`; `Game.Persistence` je zapisuje a obnovuje; `SaveStore` přijme kvíz u stavitele nebo nováčka, když čeká odjezd, a ověří, že odjezd sedí k postavě a panelu. Staré soubory tato pole nemají a načtou se jako dřív, protože se zmírnila jen pravidla pro data s `Departure != 0`. Fix se týká všech šesti druhů (stavba, parcela, dům, zásobování, golem, jablka); u domu, golema a jablek se podle čtení kódu (nezkoušeno před opravou) kvíz po načtení bez odjezdu mohl dokončit jako obyčejný úkol domu, tedy bez dovezeného zboží. Ve fázi 2 se `Departure` přesune do `PendingDeparture` a `SaveData.Trip`.
2. **`Speak` určuje hlas indexem v `InteractionActors`** (`TakeWhile`). Hlas postavy se mění, když se přidá postava s nižším ID. Je to stabilní jen díky pevnému pořadí; zafixovat v `ActorIds`.
3. **Co dnes hra udělá s nenačitatelným `progress.json`.** `SaveStore.Read` vrátí `null` a hra začne novou; jestli se původní soubor při prvním autosave přepíše bez zálohy, je potřeba zjistit (viz fáze 0, krok 6).
4. **`RestoreBuilds` ošetřuje trasy řetězem `else if`.** Pokud ve fixturě naběhnou dvě trasy zároveň, tiše se použije jen první. Validace by to měla odmítnout; po fázi 2 to zajistí jedna `Trip`.

## Metriky úspěchu

| Metrika | Dnes | Cíl |
| --- | --- | --- |
| Řádky `Game` (všechny partialy `Game.*.cs`) | 3 328 (5 388 s `GameUi`) | ≤ 2 500 — **nesplněno**, 3 295; `Game.cs` 719 → 628 |
| Soubory dotčené novým druhem trasy | 21 | ≤ 5 |
| Pole tras ve `SaveData` | 8 | 1 (`Trip`) — splněno |
| Místa s ručním výčtem druhů tras | `Departure`, `FinishDump`, `RestoreBuilds`, `CaptureProgress`, `SaveStore` | 4 zbývají (viz fáze 2), `FinishDump`, `CaptureProgress` a `RestoreBuilds` už ne |
| `Is…` příznaky a odvozené indexy v `Animal` | 8 + 2 (`TradeIndex`, `HouseIndex`) | 0 |
| Místa, kde se registruje diagnostika | 2 (`Game.Diagnostics.cs`, `build.ps1`) | 1 |
| Doba `./build.ps1 -Test` | základ z fáze 0 | ≤ +10 % |

## Rizika a jejich ošetření

| Riziko | Ošetření |
| --- | --- |
| Rozbité staré uložené hry (nepřijatelné) | brány z kapitoly „Nepřekročitelná podmínka“: nedotknutelné fixtury všech verzí, test pokračování ve hře, otisk světa, zálohy před přepsáním, ruční test na skutečném souboru, migrace se nemažou |
| Regrese chování při přesunu stavu z `Game` | po každém kroku celá sada testů, přesun bez změn logiky |
| Rozjetý rozsah fáze 3 | každý krok 3.1–3.6 je samostatný PR; po kroku 3.4 lze skončit a zbytek odložit |
| Konflikty s paralelní funkční prací | fáze 2 a 3 dělat v době, kdy se nepřidávají nové aktivity; nové věci dělat až nad hotovým `ITripTarget` |
| Nové uložené hry ve starší verzi hry | přijato, nic se neřeší; starší sestavení může nový soubor odmítnout. Při vydání o tom stačí vědět, před návratem ke staré verzi je potřeba záloha `progress.v<N>.bak` |
