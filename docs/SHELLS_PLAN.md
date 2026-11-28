# Mušle a příliv

**Stav:** hotovo (`2151d5a`, 27. 11. 2026).

Další obsah pro velkou mapu, spolu s mapou sovy (`docs/EXPLORER_PLAN.md`). Moře na jihu bylo dosud jen za okrajem údolí.
Teď se k němu dá sjet a pláž žije: dvakrát denně moře ustoupí a nechá na mokrém písku mušle.

## Nápad v jedné větě

Pod statkem strážkyně moře je **zátoka mušlí** s cedulí „MUŠLE“. Auto smí sjet za okraj údolí dolů na pláž, až k vodě.
**Při odlivu** moře odhalí mokrý písek a na něm leží **mušle osmi druhů**, každá s třpytkou nad sebou. Stačí k mušli
dojet. **Každý příliv** přinese mušle nové. Kdo najde všech osm druhů, dostane **kraba Klepetka**.

Proč právě takhle:

- **Velká mapa dostane pobřeží.** Pláž byla dosud jen kulisa; teď je to místo, kam se jezdí.
- **Den má rytmus.** Příliv a odliv jdou podle hodin dne (`Tide`): moře je vysoko ráno a v poledne, o čtvrt dne později
  je o 1,5 m níž. Mušle při přílivu zmizí pod vodou, a tak je dobré pospíšit si, a pak zase přijdou nové.
- **Sbírání bez čtení.** Rohový panel ukazuje šipku k nejbližší mušli a osm obrázků druhů, bledé ty, které dítě ještě
  nemá, s počtem u ostatních.

## Postavy

- **Krab Klepetko** (`CollectorPetKind.Crab`): čeká u strážkyně moře a po osmém druhu mušle jezdí za autem.

## Mušle

`ShellPlans`: hřebenatka, srdcovka, slávka, ulita, zavinutec, hvězdice, ježovka a perlorodka. Moře je nevyplavuje stejně
často (`ShellPlans.Weights`): slávek a srdcovek je hodně, perlorodka je vzácná.

- **Zátoka** (`World.PlanShells`): pod statkem strážkyně moře (bez statku tam, kde je pobřeží nejblíž), 130 m na každou
  stranu. Cedule stojí na okraji údolí nad ní. Plán je až po mapě sovy, takže se nic nepohne; místo drží
  `Tests/Worlds/seed-N.shells.txt`.
- **Mušle přílivu** (`World.ShellsOfTide`): každý příliv má své číslo a z něj a z údolí se spočítá, kde leží jeho deset
  mušlí (`ShellPlans.PerTide`): na mokrém písku, který odkryje jen odliv, mimo ústí řeky, maják a 6 m od sebe. Proto se
  nemusí ukládat.

## Pláž

Okraj údolí (`ValleyBounds`) leží nad pláží. Auto smí jen u moře pokračovat dál dolů po pláži (`Vehicle.SeaDepth`,
`World.SeaDepth`), dokud voda není hlubší než 0,3 m. Když ho dostihne příliv, smí z vody vyjet, ale ne do ní dál. U
ostatních okrajů se zastaví jako dřív. Moře (`BorderWater.SetTide`) i noční loďka jdou s přílivem nahoru a dolů,
pohraniční řeka si drží svou hladinu.

## Hra

1. Když se auto přiblíží k zátoce a na písku leží mušle, zazní „Odliv! Moře ustoupilo a nechalo na písku mušle…“ a
   rohový panel (`GameUi.ShellsHud`) ukáže šipku k nejbližší mušli a osm druhů. Při přílivu panel napíše „Počkej na
   odliv“.
2. Mušli sebere auto, které k ní dojede na 3,6 m (`PickReach`): 1 mince, za první mušli svého druhu 4 navíc
   (`NewKindCoins`).
3. Každý příliv (přechod přes půlnoc a poledne dne) přinese další mušle; nesebrané odplavou.
4. Osmý druh přinese 25 mincí (`AllKindsCoins`) a kraba Klepetka.

## Uložení

`SaveData.Shells`: `Tide` (číslo přílivu, jehož mušle leží na písku), `Picked` (které z nich už jsou sebrané) a `Kinds`
(kolik mušlí každého druhu dítě má). Pravidlo `HasInvalidShells`. Uložená hra z doby před mušlemi se nahraje s prvním
přílivem a bez mušlí. Kontroly: `shell-checks` a `outland-checks` (auto sjede po pláži a zastaví u vody); snímky
`run.ps1 -Capture -View shells`, `shells-close` a `shells-high`.
