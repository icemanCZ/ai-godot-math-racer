# Mapa sovy kartografky

Další obsah pro velkou mapu, po rallye převoznice Venduly (`docs/RALLY_PLAN.md`). Tentokrát se nejezdí za jedním úkolem,
ale objevuje se: kraj za starým údolím je velký a dítě v něm má co najít.

## Nápad v jedné větě

**Sova kartografka Mapka** má stánek u silnice tam, kde poprvé opouští staré údolí, a kreslí mapu celého údolí. Za
starým údolím je **osm divů krajiny**. Stačí kolem každého projet, auto ho „uvidí“, a u sovy pak jedna otázka zakreslí
do mapy všechny viděné divy. Za každý jsou mince, za celou mapu bonus a **sovička Houkalka**.

Proč právě takhle:

- **Projede se celá velká mapa.** Každý div stojí v jiném z osmi směrů kolem údolí, u silnice, nejméně 220 m od
  ostatních.
- **Objevování bez čtení.** Mapa v panelu je nakreslená: hory nahoře, moře dole, řeky, les, silnice, vesnice, sova, auto
  a divy. Neviděný div je otazník, viděný má obrázek s vykřičníkem a zakreslený sovinu modrou vlaječku.
- **Nic nespěchá.** Div se dá uvidět od začátku hry, ještě než sova mapu ukáže. Kdo neví kudy, zmáčkne u sovy lupu (nebo
  fajfku, když není co zakreslit) a šipky na silnici ho dovedou k nejbližšímu neviděnému divu.

## Postavy

- **Sova kartografka Mapka** (`CartographerRole`, `ActorIds.Cartographer` 82): sedí u stojanu s velkou mapou, má brýle,
  uši z peří a brašnu se srolovanými mapami. Dokud mapu neukázala, a pak vždy, když je co zakreslit, má nad sebou žlutou
  značku.
- **Sovička Houkalka** (`CollectorPetKind.Owlet`): čeká u stánku a po osmém zakresleném divu jezdí za autem.

## Divy krajiny

`ExplorerPlans`: Starý dub (s dvířky v kmeni), Skalní brána, Zřícenina hradu, Obří muchomůrka, Kamenný kruh, Kostra
dinosaura, Studánka a Strašák v poli. Každý má kolizi a vedle sebe sovinu vlaječku, která se ukáže, až je zakreslený.

- **Plán** (`World.PlanExplorer`): po rallye, s vlastními náhodnými čísly (`Settings.Seed + 9203`), takže se nic, co v
  údolí už bylo, nepohne. Místa drží `Tests/Worlds/seed-N.explorer.txt`.
- **Stánek** (`PlanCartographer`): u silnice, která se napojuje na okruh nejblíž vesnici, na prvním kousku za starým
  údolím, kde se vejde.
- **Místo divu** (`LandmarkFits`): u silnice za starým údolím, na rovné a suché půdě mimo silnice, řeky, pláž a mosty,
  140 m od sovy, 220 m od ostatních divů a dál od statků, majáku, draka, balonu, doupat, oveček a bran rallye. Druhy se
  rozdělí do osmi směrů v pořadí, které má každé údolí své; když se div ve svém směru nevejde, hledá se kdekoli.
- Rostliny, tráva a drobnosti u cesty na místech divů a stánku nerostou (`InExplorerZone`).

## Hra

1. Auto uvidí div, když k němu dojede na jeho poloměr a 15 m (`ExplorerPlans.SpotReach`): nad divem se ukáže oslava a
   zazní „Div krajiny: …! Řekni to sově Mapce.“
2. Panel sovy (`GameState.ExplorerStatus` 38, `GameUi.CartographerPanel`) ukazuje mapu, pod ní řadu osmi obrázků divů
   (bledé neviděné, ✓ zakreslené) a sovičku, kterou divy přinesou.
3. Zelená fajfka (`UiIntent.ExplorerRecord`) se zeptá na otázku (`Departure.Explorer`) a správná odpověď zakreslí
   všechny viděné divy: 6 mincí za každý (`CoinsPerLandmark`) a 30 za celou mapu (`MapCoins`).
4. Lupa (`UiIntent.ExplorerGuide`) rozsvítí šipky na silnici k nejbližšímu neviděnému divu (`ExplorerTrip`) a rohový
   panel (`GameUi.ExplorerHud`) ukáže jeho obrázek a vzdálenost. Šipky ustoupí doručení, přívěsu i dopisu.
5. Po osmém divu je mapa celá a sovička Houkalka jde s autem.

## Uložení

`SaveData.Explorer`: `Started`, `Spotted`, `Recorded` a `Guided` (-1 bez šipek). Pravidlo `HasInvalidExplorer`: jen
známé divy, každý nejvýš jednou, zakreslený jen viděný a jen po ukázání mapy, šipky jen k neviděnému, panel jen u sovy a
její jediná otázka. Uložená hra z doby před mapou se nahraje bez ničeho z toho. Kontroly: `explorer-checks`; snímky
`run.ps1 -Capture -View explorer`, `explorer-panel`, `explorer-hud` a `landmark-0` až `landmark-7`.
