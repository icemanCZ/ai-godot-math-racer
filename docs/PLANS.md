# Plány a otevřené dluhy

Každá větší změna má plán v `docs/<NÁZEV>_PLAN.md`. Plán je česky, protože se o něm rozhoduje s uživatelem, a pod
nadpisem má řádek `**Stav:** …`, který začíná jedním z těchto slov:

- `návrh` – čeká na rozhodnutí uživatele, nic se nestaví;
- `rozpracováno` – část je hotová; řádek říká která;
- `hotovo` – hra odpovídá plánu; řádek uvádí commit a datum, oddíl „Jak to dopadlo“ (nebo „Jak je to postavené“) na
  konci říká, v čem se hotová hra od návrhu liší;
- `odloženo` – uživatel rozhodl, že se zatím nedělá.

Co zůstalo otevřené, se píše do oddílu „Otevřené dluhy“ níže, ne jen do plánu, aby se to dalo najít na jednom místě.
`repo-checks` selže, když plán nemá řádek se stavem nebo tu chybí v seznamu.

## Plány

- [`REFACTORING_PLAN.md`](REFACTORING_PLAN.md) – architektonické úpravy, fáze 0 až 5: hotovo.
- [`MAP_EXPANSION_PLAN.md`](MAP_EXPANSION_PLAN.md) – šestkrát větší údolí s přírodními hranicemi: hotovo, fáze 5
  odložená.
- [`POST_AND_LIGHTHOUSE_PLAN.md`](POST_AND_LIGHTHOUSE_PLAN.md) – pošta do čtyř koutů a maják na mysu: hotovo.
- [`DRAGON_PLAN.md`](DRAGON_PLAN.md) – drak pod horami: hotovo.
- [`BALLOON_PLAN.md`](BALLOON_PLAN.md) – balónové stanoviště: hotovo.
- [`TRACKING_PLAN.md`](TRACKING_PLAN.md) – stopy hajné Boženy: hotovo.
- [`SHEEP_PLAN.md`](SHEEP_PLAN.md) – ztracené ovečky pastýře Bohouše: hotovo.
- [`RALLY_PLAN.md`](RALLY_PLAN.md) – rallye převoznice Venduly: hotovo.
- [`EXPLORER_PLAN.md`](EXPLORER_PLAN.md) – mapa sovy kartografky: hotovo.
- [`SHELLS_PLAN.md`](SHELLS_PLAN.md) – mušle a příliv: hotovo.
- [`FERRY_PLAN.md`](FERRY_PLAN.md) – přívoz a ptačí ostrůvek: hotovo.
- [`CASTLE_PLAN.md`](CASTLE_PLAN.md) – hrad na kopci: hotovo.
- [`MAINLINE_PLAN.md`](MAINLINE_PLAN.md) – velká trať přes údolí: hotovo.

`PRODUCTION_CHAINS.md`, `ALGORITHMS.md` a `VALIDATION.md` nejsou plány, ale popis hry, jak je.

## Otevřené dluhy

- **Řádky `Game`.** Cíl refaktoringu (všechny partialy `Game.*.cs` dohromady nejvýš 2 500 řádků) splněný není: na konci
  refaktoringu jich bylo 3 295 (`REFACTORING_PLAN.md`, Metriky úspěchu), s funkcemi za starým údolím 10 921
  (2026-11-28). Jeden soubor má nejvýš 1 000 řádků (`repo-checks`), ale třída `Game` jako celek dál roste; další krok
  je brát z ní celé funkce do menších tříd; další partial jejich velikost jen rozloží do víc souborů.
- **Jmenné prostory závisí jeden na druhém oběma směry.** Hlavně proto, že `Settings`, `GameState` a konstanty `Game`
  leží v `App`. Prvním krokem by byl listový jmenný prostor (`Foundation`) a po něm kontrola směru závislostí v
  `contract-checks` (`ARCHITECTURE.md`, `REFACTORING_PLAN.md`).
- **Knihovna `MathRacer.Logic` bez Godotu** s testy xUnit se nestavěla: `SaveData` a otázky dnes potřebují `Vector3`,
  `ProjectSettings` a `GD` (`REFACTORING_PLAN.md`, Ostatní položky).
- **Pohlednice majáku** z plánu pošty a majáku chybí (`POST_AND_LIGHTHOUSE_PLAN.md`).
- **Noční oheň draka** nemá v `benchmark.ps1` vlastní přepínač `-Off` (`DRAGON_PLAN.md`).
- **Bloky rostlin za starým údolím se nikdy neuvolní**: cesta k hranicím přidá asi 17 000 uzlů a 660 MB paměti
  (`LESSONS.md`, Frame rate).
- **Kontroly nikde nespouští CI ani git hook** před commitem nebo pushem; spoléhá se na `./build.ps1 -Test`
  (`CLAUDE.md`).
