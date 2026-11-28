# Přívoz a ptačí ostrůvek

**Stav:** hotovo (`fc8d509`, 27. 11. 2026).

Další obsah pro velkou mapu, po mapě sovy (`docs/EXPLORER_PLAN.md`) a mušlích (`docs/SHELLS_PLAN.md`). Hraniční řeka
byla dosud jen voda za okrajem údolí. Teď se po ní pluje: od statku převoznice Venduly dolů po proudu až do delty, kde
řeka ústí do moře, na ptačí ostrůvek s pozorovatelnou.

## Nápad v jedné větě

U Vendulina statku čeká **volavka pozorovatelka Klárka** a chce na **ptačí ostrůvek** v deltě. Vendulina stará
**pramice** je rozbitá, a tak dítě přiveze přívěsem klády a prkna na nové **molo a pramici**. Pak se jedna otázka
promění v **plavbu po řece** až k ostrůvku. Na něm stojí **pozorovatelna s dalekohledem**: dítě otáčí dalekohledem,
hledá ptáky schované v rákosí a každého, kterého zahlédne, zapíše do **atlasu ptáků**. Za celý atlas přiletí
**ledňáček Modroušek**.

Proč právě takhle:

- **Velká mapa dostane řeku.** Plavba vede kolem celé říční hranice, od statku až k moři, a ostrůvek leží za okrajem
  údolí, kam auto samo nedojede.
- **Nová činnost: hledání dalekohledem.** Kamera se dívá z pozorovatelny přes úzké zorné pole a dítě ji otáčí volantem
  (šipkami, pákou) nebo myší. Pták, který chvíli zůstane uprostřed kruhu, je zapsaný. Šipka na okraji ukáže, kterým
  směrem se ještě někdo schovává, takže to zvládne i dítě, které neumí číst.
- **Vrací se ráda.** Každá plavba přiveze jiné ptáky, vzácný plameňák a pelikán se ukážou jen občas.

## Postavy

- **Volavka pozorovatelka Klárka** (`BirdwatcherRole`, `ActorIds.Birdwatcher` 83): stojí na přístavišti vedle
  Vendulina statku, na jeho straně u okraje údolí. Na plavbě stojí v pramici a odráží se bidlem, na ostrůvku čeká u
  pozorovatelny. Žlutou značku má nad sebou, dokud se staví (jen když přívěs zrovna není na cestě), a pak dokud atlas
  není plný.
- **Ledňáček Modroušek** (`CollectorPetKind.Kingfisher`): čeká na přístavišti a po osmém druhu ptáka jezdí za autem.

## Místo

`World.PlanFerry` běží po mušlích, bez náhodných čísel, kromě ptáků, kteří mají vlastní (`FerrySeed`). Místa drží
`Tests/Worlds/seed-N.ferry.txt`, takže se nic, co v údolí už bylo, nepohne.

- **Přístaviště** (`FerrySite.Landing`): vedle dvora statku hraniční řeky, na suché zemi uvnitř údolí, mimo silnice a
  dvůr. Břeh smí k řece klesat, paluba stojí na kůlech, ale jen tak mírně, aby na místě pro vyložení nákladu (hned před
  přístavištěm) přívěs stál. Kousek dál (`Return`) se auto postaví po návratu z ostrůvku. U přístaviště stojí cedule
  „PTÁCI“ s obrázkem loďky a ptáka a vrak staré pramice.
- **Molo** vede od okraje údolí dolů po břehu k vodě hraniční řeky, kde u něj čeká pramice (`Berth`). Je jen na
  pohled, auto po něm nejezdí.
- **Ostrůvek** (`Island`): v moři před ústím hraniční řeky, ovál 30 × 22 m nad hladinou i při přílivu. Má vlastní
  model a kolize, terén za hranicí se nemění. Na severní straně má molo pro pramici, uprostřed pozorovatelnu na kůlech.
- **Plavba** (`FerryRoute`): od mola u statku středem řeky po proudu až k ústí a k molu ostrůvku. Trvá 20 až 40
  vteřin podle délky řeky, pedály ji zkrátí.
- **Ptačí rákosí** (`World.ReedSpots`): dvanáct trsů rákosí ve vodě a na písku kolem ostrůvku, 45 až 95 m od něj, za
  okrajem údolí a mimo cestu pramice.

## Hra

1. **Stavba.** Panel Klárky (`GameState.FerryStatus` 39, `GameUi.BirdwatcherPanel`) ukazuje pět dílů přístaviště a
   pramice a zelenou fajfku, která pošle přívěs pro další náklad (`TripKinds.Ferry`, `Departure.Ferry`): dvakrát
   klády ze skladu dřeva na kůly, třikrát prkna z pily na lávku, dno a boky. Pátý náklad zaplatí 25 mincí
   (`FerryPlans.Coins`). Údolí bez pily má pramici hotovou od začátku.
2. **Plavba.** Hotová pramice: panel ukáže atlas ptáků (osm obrázků, bledé ty, které dítě ještě nevidělo, s počtem u
   ostatních), ledňáčka a zelenou fajfku. Po správné odpovědi stojí auto v pramici, Klárka se odráží bidlem a kamera
   pluje za nimi dolů po řece. U ostrůvku auto vyjede na břeh.
3. **Ostrůvek.** Auto smí jezdit jen po ostrůvku (`Vehicle.SeaDepth`, `World.SeaDepth`). Klárka čeká u pozorovatelny a
   její panel tu má dvě tlačítka: zelenou fajfku pro dalekohled a loďku pro cestu domů.
4. **Dalekohled.** Kamera stojí v okně pozorovatelny s úzkým zorným polem (`FollowCamera.ShowTelescope`). Volant,
   páka nebo myš jím otáčí. Překryv přes celou obrazovku (`TelescopeOverlay`, `GameUi.TelescopeHud`) kreslí kruh
   dalekohledu s kroužkem uprostřed, který se plní, šipku k nejbližšímu ptákovi, který se ještě schovává, a pod tím
   čtyři ptáky této plavby, bledé s otazníkem, dokud je dítě nenajde. Pták, který zůstane 0,6 s blíž než 4,5° od středu
   (`FerryPlans.SpotAngle`, `SpotSeconds`), vyskočí z rákosí a mávne křídly: 2 mince, za první svého druhu 5 navíc.
   Pedály dalekohled zavřou, po posledním ptákovi se zavře sám.
5. **Každá plavba** přiveze čtyři ptáky (`FerryPlans.PerVisit`) různých druhů na čtyři z dvanácti trsů. Druhy se losují
   podle `FerryPlans.Weights` z čísla plavby, takže se nemusí ukládat. Labuť a čáp jsou častí, pelikán a plameňák
   vzácní.
6. **Osmý druh** přinese 25 mincí (`AtlasCoins`) a ledňáčka Modrouška.
7. **Domů.** Loďka v panelu Klárky (`UiIntent.FerryHome`) odveze auto zpátky k přístavišti, bez otázky.

## Ptáci

`FerryPlans`: labuť, čáp, kormorán, rybák, kulík, potápka, pelikán a plameňák. Labuť a potápka plavou na hladině a
stoupají a klesají s přílivem, ostatní stojí na trsu rákosí. Každý má obrázek (`BirdPicture`) pro panel a dalekohled.

## Uložení

`SaveData.Ferry`: `Stage` (díly přístaviště a pramice, 0 až 5), `Visit` (kolikátá plavba), `Spotted` (kteří ze čtyř
ptáků této plavby už jsou zapsaní) a `Kinds` (kolikrát dítě vidělo každý druh). Pravidlo `HasInvalidFerry`. Plavba ani
pobyt na ostrůvku se neukládají: uložená hra z ostrůvku se nahraje s autem u přístaviště. Uložená hra z doby před
přívozem se nahraje bez ničeho z toho.

Kontroly: `ferry-checks`; snímky `run.ps1 -Capture -View ferry` (hotové přístaviště), `ferry-0` a `ferry-2`
(rozestavěné), `ferry-panel`, `ferry-ride`, `ferry-island` a `ferry-telescope`.
