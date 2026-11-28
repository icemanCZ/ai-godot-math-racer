# Výrobní řetězce

Přehled všeho, co se ve hře vozí na vozíku: odkud surovina pochází, kde se zpracuje, kdo zboží spotřebuje a kolik ho za
celou hru potřebuje. Stav po přidání vození skleniček k světlaři Ignácovi (na `65b2c8f`).

## Celkový obraz

```
SUROVINA (místo, kdo nakládá)        ZÁVOD (dělník)                ZBOŽÍ        SPOTŘEBITEL
───────────────────────────────────────────────────────────────────────────────────────────────────────────────
klády   (skládka: Drvoš, Dušan)  ──► Pila (Medvěd Pilař)        ──► prkna    ──► stavby, parcely, hřiště, železnice,
                                                                                zoo, domky obyvatel
hlína   (hliniště: Hanka, Honza) ──► Cihelna (Liška Cihlářka)   ──► cihly    ──► stavby, parcely, hřiště, železnice,
                                                                                zoo, domky obyvatel
kamení  (lom: Kvido, Zdeněk)     ──► Štěrkovna (Zajíc Štěrkař)  ──► štěrk    ──► stavby, parcely, hřiště, železnice, zoo
písek   (pískovna: –, Pepík)     ──► Sklárna (Králice Sklenářka)──► skleničky──► dokončené domky (jednou každý)
                                                                                ──► světlař Ignác (bez omezení)
obilí   (pole: sedlák Klásek)    ──► Mlýn (Mlynář Bruno)        ──► mouka ──► Pekárna (Rohlík) ──► chléb
                                                                                ──► dokončené domky (jednou každý)
                                                                                ──► Pekař František (objednávky)

kamení  (lom: Kvido, Zdeněk)     ──► Kovárna u horské stráně (Kuba) ──► koleje ──► velká trať přes údolí
                                                                  └──► kotva  ──► loď Albatros (jednou)

přímo bez závodu:  písek ──► most (poslední náklad)      klády ──► molo (piloty)
od sběratelů:      látka (Farmář Tonda) ──► mlýn, plachty lodi   kámen (sběratel drahokamů) ──► rozhledna
                   vlajka (hračkář) ──► molo
```

## Společná pravidla závodů

Platí pro pilu, cihelnu, štěrkovnu, sklárnu, pekárnu i mlýn (`Works`, `FlourMill`):

| Pravidlo | Hodnota |
| --- | --- |
| Kapacita skladu | 12 kusů (`Works.Capacity`) |
| Jeden náklad suroviny dá | 2 kusy (`Works.PerLoad`) |
| Jedna cesta se zbožím odveze | 1 kus (`Works.PerTrip`) |
| Plný sklad | surovinu nepřijme, dělník u zdroje řekne, že je plno |

- Surovinu vozí hráč od **dělníka u zdroje**: ten otevře panel, hned položí otázku na naložení (`StartLoadingTrip`) a
  vozík jede k závodu (`FinishSupplyDump` přidá 2 kusy).
- Zboží se ze skladu odebere ve chvíli **naložení** u závodu (`CompleteHaulLoading`); když mezitím došlo, cesta se
  zruší.
- Každá surovina má dvě místa (druhý lom, hliniště, pískovna, skládka); vozík naloží u kteréhokoli, kam dojede první.
- **Chybějící zboží:** když stavitel, nový obyvatel, správce hřiště, výpravčí, ošetřovatel zoo nebo obyvatel domku chce
  zboží, které závod nemá, pošle hráče rovnou pro surovinu (`BeginMissingSupply`). U chleba posílá po řetězci: mouka ze
  mlýna, jinak obilí z pole (`BreadSupply`).

## Řetězce jeden po druhém

### 1. Dřevo: klády → pila → prkna
- Zdroj: skládka klád (`LogYard`, Medvěd dřevorubec Drvoš) a druhá skládka (Králík dřevorubec Dušan).
- Závod: Pila, Medvěd Pilař. Mazlíčci: Kládička (10 nákladů klád), Prkýnko (20 prken).
- Klády jdou i **přímo** na molo (2 náklady pilot), bez pily.

### 2. Hlína: hlína → cihelna → cihly
- Zdroj: hliniště (Liška hlinařka Hanka) a druhé hliniště (Liška hlinař Honza).
- Závod: Cihelna, Liška Cihlářka. Mazlíčci: Hliněnka (10 nákladů hlíny), Cihlička (20 cihel).

### 3. Kámen: kamení → štěrkovna → štěrk
- Zdroj: lom (Králíček lomař Kvido) a druhý lom (Zajíc lomař Zdeněk).
- Závod: Štěrkovna, Zajíc Štěrkař. Mazlíčci: Kamínek (10 nákladů kamení), Štěrkáček (20 kusů štěrku).

### 4. Sklo: písek → sklárna → skleničky
- Zdroj: první pískovna **nemá dělníka** (písek tam naloží ten, kdo cestu zadal: bobr pro most, Sklenářka pro sklárnu),
  druhá pískovna má Medvěda písečníka Pepíka, který posílá písek do sklárny.
- Závod: Sklárna, Králice Sklenářka. Mazlíčci: Pískulka (10 nákladů písku), Sklenička (20 skleniček).
- Písek jde i **přímo** na most (desátý, poslední náklad).
- Spotřeba:
  - Každý ze čtyř domků obyvatel si po desátém vylepšení **jednou** objedná skleničky na kolaudaci (5 mincí).
  - Medvěd světlař Ignác na návsi z nich fouká lampy a bere je **bez omezení**: v jeho panelu je řádek se skleničkami,
    vozík naloží 1 kus ve sklárně a vysype ho do kruhu na návsi mezi Ignácem a kašnou (5 mincí). Když sklárna nemá
    skleničky, pošle hráče pro písek (`Game.Glassmaker.cs`, `GlassmakerTripTarget`).

### 5. Chléb: obilí → mlýn → mouka → pekárna → chléb
- Zdroj: pšeničné pole za vesnicí, Králíček sedlák Klásek (`SourceCatalog.Field`).
- Mezičlánek: **dostavěný** větrný mlýn (`FlourMill`), Mlynář Bruno. Dokud mlýn není hotový, sedlák obilí neposílá
  („mlýn ještě nemele“). Jeden náklad obilí = 2 pytle mouky, kapacita 12 pytlů.
- Mlynář nakládá mouku pro pekárnu (1 pytel na cestu), jen když má mouku a pekárna má místo.
- Závod: Pekárna, Králíček pekař Rohlík. 1 pytel mouky = 2 chleby. **Jeden náklad obilí tedy dá 4 chleby.**
- Spotřeba:
  - Pekař František v obchodě: jakmile mlýn mele, bere na každou objednávku 1–3 chleby z pekárny
    (`Expedition.OrderAmount`); když chybí, pošle hráče po řetězci. Před dostavbou mlýna peče sám jako dřív.
  - Každý zasklený domek si potom **jednou** objedná chléb (5 mincí).
- Mazlíčci: Zrníčko (10 nákladů obilí), Housánek (20 chlebů).
- Údolí bez mlýna (žádné ze zkontrolovaných) by vozilo obilí rovnou do pekárny (`Works.Input = Wheat`).

### 6. Koleje: kamení → kovárna → koleje
- Zdroj: stejné dva lomy jako u štěrku (Králíček lomař Kvido, Zajíc lomař Zdeněk).
- Závod: **kovárna u horské stráně** (`LineForge`, `docs/MAINLINE_PLAN.md`). Není `Works`: je to vlastní budova se
  zásobou, která se ukládá jako `SaveData.MainLine.Rails`. Jeden náklad kamení dá 2 koleje, zásoba drží nejvýš 4.
- Koleje se nakládají u kovárny po vyřešené otázce, kterou klade jezevec strojvůdce Kuba z dálky (kovárna nemá dělníka).
  Odebírají se při naložení stejně jako zboží ze skladu závodu.
- Když kovárna koleje nemá a trať je chce, panel Kuby pošle přívěs rovnou s kamením do kovárny. Koleje jsou potřeba
  třikrát (třetí úsek každého ze tří ramen trati), takže celá trať chce aspoň 2 náklady kamení.
- Spotřeba: velká trať přes údolí, 17 nákladů (štěrk 4, cihly 4, prkna 3, koleje 3, sklo 3) a nejméně 2 náklady kamení
  pro kovárnu.
- **Kotva lodi Albatros** (`docs/SHIP_PLAN.md`): jedenáctá dodávka lodi. Když kotva není ukutá, panel mrože kapitána
  Vojty pošle přívěs s kamením do kovárny (`TripKinds.ShipForge`) a kovárna z jednoho nákladu ukuje kotvu, která leží
  vedle ní, dokud ji přívěs neodveze. Kotva se ukládá v `SaveData.Ship.Anchor`, ne v zásobě kolejí, takže trať o nic
  nepřijde. Otázku na naložení klade kapitán z dálky. Údolí bez kovárny kotvu vynechá.

### 7. Zvláštní náklady od sběratelů
Nejsou to výrobky, ale poslední (desátý) náklad tří staveb; nakládá se po vyřešené otázce u sběratele:

| Náklad | Kdo ho dá | Stavba |
| --- | --- | --- |
| látka na plachty | Farmář Tonda | mlýn |
| látka na plachty (2×) | Farmář Tonda | loď Albatros |
| kámen na špičku | sběratel drahokamů | rozhledna |
| vlajka | hračkář | molo |

## Kdo co spotřebuje

Pořadí nákladů je pevné (`BuildPlans`, `PlotPlans`, `PlaygroundPlans`, `RailwayPlans`, `ZooPlans`,
`Village.CreateTasks`). Š = štěrk, C = cihly, P = prkna.

| Spotřebitel | Nákladů | Pořadí | Odměna |
| --- | --- | --- | --- |
| Most (Bobr Bóďa) | 10 | Š Š Š C C(druhý břeh) C P P P písek | vozík |
| Mlýn (Mlynář Bruno) | 10 | Š Š C C C P P P P látka | 30 mincí + odměna; pak mele |
| Rozhledna (Hvězdář Kryštof) | 10 | Š Š Š C C C C P P kámen | 30 mincí + odměna |
| Molo (Rybář Vilda) | 10 | Š Š klády klády P P P P P vlajka | 30 mincí + odměna |
| Parcela (6×, nový obyvatel) | 6 každá | Š C C P P P | 10 mincí za domek |
| Hřiště (Medvědice Madla) | 6 | Š P P C P C | 20 mincí |
| Železnice (Králíček výpravčí Špendlík) | 6 | Š P C P C P | 25 mincí |
| Zoo (Medvěd ošetřovatel Bruno) | 12 | Š C P Š C P P C Š C P Š | 60 mincí |
| Velká trať (Jezevec strojvůdce Kuba) | 17 | Š C, pak 3× (Š P koleje C sklo) | 10 + 3×20 + 40 mincí, krteček |
| Loď Albatros (Mrož kapitán Vojta) | 14 | klády ×3, P ×6, sklo, kotva, klády, látka ×2 | 50 mincí, pak plavby |
| Domky obyvatel (4×) | 2 P + 2 C každý, náhodně mezi 10 úkoly | – | 5 mincí za úkol |
| Dokončený domek (4×) | 1 skleničky, pak 1 chléb | – | 5 mincí za každé |
| Světlař Ignác | 1 skleničky na cestu, bez konce | – | 5 mincí za cestu |
| Pekař František | 1–3 chleby na objednávku, bez konce | – | odměna obchodu |

### Celková potřeba za hru

| Zboží | Kusů za hru | Nákladů suroviny | Rozpis |
| --- | --- | --- | --- |
| Štěrk | 26 | 13 nákladů kamení | stavby 10, parcely 6, hřiště 1, železnice 1, zoo 4, trať 4 |
| Cihly | 42 | 21 nákladů hlíny | stavby 10, parcely 12, hřiště 2, železnice 2, zoo 4, domky 8, trať 4 |
| Prkna | 53 | 27 nákladů klád | stavby 14, parcely 18, hřiště 3, železnice 3, zoo 4, domky 8, trať 3 |
| Skleničky | 7 + Ignác | 4 náklady písku | domky 4, trať 3, Ignác bez omezení |
| Chléb | 4 + objednávky | 1 obilí na 4 chleby | domky 4, pekař bez omezení |
| Přímo | – | 2 klády, 1 písek | molo, most |

## Výsledek kontroly

Prošel jsem kód všech řetězců a spustil kontroly (`./test.ps1`):

- `content-checks` na světech 1–256: každé údolí má všech pět závodů, obě místa každé suroviny s dělníky, pole se
  sedlákem, mlýn (a tedy celý chlebový řetězec), hřiště, železnici a zoo.
- Kontroly chování řetězců (`works`, `bakery`, `glass`, `build`, `plot`, `playground`, `railway`, `zoo`, `trade`,
  `world-pet`, `farm`) na jejich výchozích světech: 31 běhů, všechny prošly.

Řetězce na sebe navazují správně: suroviny jdou do správných závodů, chybějící zboží vždy pošle hráče pro správnou
surovinu (u chleba po řetězci obilí → mouka), plný sklad zastaví dovoz a sklad se odečítá až při naložení.

### Nalezené problémy a co se s nimi stalo

1. **Opraveno: mazlíčci Sklenička a Pískulka byli prakticky nedosažitelní.** Skleničky dřív spotřebovaly jen čtyři
   domky, každý jednou, takže sklárna za celou hru přijala nejvýš 10 nákladů písku, a to jen při dokonalém pořadí cest;
   při běžném hraní se zastavila na plném skladu po 8 nákladech nebo dřív. Sklenička chce 20 skleniček (= 10 nákladů),
   Pískulka 10 nákladů písku. Teď skleničky bere bez omezení světlař Ignác, takže oba mazlíčci jsou dosažitelní.
2. *(jen teoretické)* V údolí, které by mělo pekárnu, ale ne sklárnu, by domky chléb nikdy nechtěly: objednávka chleba
   čeká na zasklení (`Game.HouseMaterial`). Žádný ze 256 zkontrolovaných světů takový není.
3. *(jen teoretické)* V údolí bez mlýna by sedlák neměl nad hlavou značku úkolu, protože `UpdateHaulGuides` ho navádí
   jen tam, kde je `FlourMill`. Obilí by přesto šlo vozit do pekárny přes jeho panel.
4. *(jen teoretické)* Údolí s brodem, ale bez závodů (`INDUSTRY_MISSING`), by most nemohlo dostavět: první náklad je
   štěrk a `GoodsWorks` vrátí null. Kontrola obsahu to na 256 světech vylučuje.
5. **Opraveno:** zastaralé komentáře, které nezmiňovaly mlýn, pekárnu nebo nové cesty (`HaulTally`, `Works`,
   `WorksCatalog`, `World.WorksFor`, `World.AllWorks`, `Game.Works`, `HaulTrip`, `ITripTarget`, `HouseTripTarget`,
   `SavedTrip`, `SaveRules.HasInvalidTrip`).
