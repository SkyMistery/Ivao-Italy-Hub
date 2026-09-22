# Il pubblico dei tour: una mappa senza nomi, i riquadri come blocco, e `tours` che nessuna pagina può prendere

**Data:** 22 settembre 2026 — fase **T10** di M2 (`/tours`, `/tours/{slug}`, `RouteMap`, `flightops.tourCards`)
**Stato:** **decisa**. Tre risposte di Carmine in apertura, più una cosa trovata scrivendo il codice.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)** per due punti su tre — si estendono meccanismi esistenti, non se ne aprono —
e **(c)** per la mappa, già decisa il 15 settembre (`2026-09-15-la-mappa.md`): qui si correggono due dettagli di quella nota.

## 1. La mappa di base non porta i nomi dei luoghi

La nota del 15 settembre prevedeva, sotto `/tiles`, anche i **caratteri** (Noto Sans, OFL) e gli **sprite** dei simboli di
Protomaps: servono per disegnare le etichette dell'archivio — città, stati, mari.

**Deciso: niente etichette.** La base disegna terra, acqua e confini fra stati; le uniche parole sulla mappa sono i **codici degli
aeroporti**, che sono marcatori HTML del componente e non testo della mappa.

- Le etichette dell'archivio sono scritte in ogni alfabeto del mondo: per disegnarle servirebbero i glifi di ogni scrittura, che
  sono una cartella di file da ospitare e da caricare via FTP per ogni fork.
- Una mappa di un tour parla di aeroporti, non di toponimi: il nome di una città a zoom 4 non aiuta nessuno a capire una tratta.
- **Chi forka carica un file solo** (`tiles/basemap.pmtiles`) e non una cartella.

Misura ripetuta il 22 settembre sulla build del giorno: il mondo fino allo zoom 7 pesa **179,4 MB** (21 845 tessere), esattamente
la misura della nota. ⚠️ **Le build di Protomaps durano circa una settimana**: `20260915.pmtiles`, la build misurata allora, oggi
risponde 404. Per questo lo script `tools/basemap.mjs` prende la data come argomento e non ne scrive una fissa.

## 2. Il blocco `flightops.tourCards`: sempre vivo, con due proprietà

**Deciso**: il blocco è **sempre `live`** e ha due proprietà — quali stati mostrare (aperti, in chiusura, in arrivo; niente di
scelto = tutti) e un numero massimo facoltativo.

- Congelare alla pubblicazione un elenco di tour vorrebbe dire che la pagina invecchia da sola: un tour che apre stamattina non
  c'è, uno chiuso a giugno sì.
- Le proprietà sono quelle che una redattrice usa davvero: «i tour in arrivo, tre al massimo» su una pagina, «tutti» su `/tours`.
- **Un avanzamento nei riquadri non c'è**, e non perché sia difficile: è di chi guarda, e arriva con i PIREP (`flightops.myTours`,
  T15). Un riquadro è uguale per un visitatore e per un pilota.

**L'estensione del meccanismo** (caso b): il form delle proprietà di un blocco legge le etichette da `blocks.<tipo>` nel namespace
del **nucleo**, e il nucleo non sa che cosa sia un tour. `BlockRegistration` guadagna quindi un campo, `propertyLabels`, che un
blocco di un modulo riempie con il suo namespace (`flightops:blocks.tourCards`). Una riga nel tipo, una riga in
`BlockProperties.tsx`. T9 l'aveva previsto quando il primo blocco di un modulo è nato senza proprietà.

## 3. ⚠️ Trovato scrivendo il codice: una pagina poteva chiamarsi `tours`

`ContentAddresses.ReservedSegments` è l'elenco dei primi segmenti che nessuna pagina può prendere — `news`, `calendar`, `staff`,
`media`… È scritto nel **nucleo**, e il nucleo non conosce gli indirizzi pubblici di un modulo. T10 apre `/tours` e
`/tours/{slug}`: senza fare niente, la prima pagina che qualcuno avesse chiamato «tours» sarebbe stata salvata, pubblicata e
**irraggiungibile per sempre**, perché la rotta del modulo vince.

**Deciso** (caso b, si estende il meccanismo): `IModule` guadagna `ReservedSegments`, `ModuleRegistry` li compone come già compone
`SpaFallbackExclusions`, e `ContentAddresses` chiede i due insiemi insieme. `FlightOpsModule` dichiara `["tours"]`. `tiles` entra
invece nell'elenco del nucleo, perché è il nucleo a servire l'archivio.

L'alternativa — scrivere «tours» nell'elenco del nucleo — è la stessa riga di codice e la regola sbagliata: il nucleo non nomina
mai un modulo (piano §16.9), e il secondo modulo con una pagina pubblica l'avrebbe rifatta.

## 4. Che cosa si è toccato

- **Modulo**: `Tours/PublicTours.cs` (un servizio, letto dai due endpoint e dal blocco), `PublicTourDtos.cs`,
  `TourCardsProvider.cs`, due verbi anonimi sul gruppo dei tour (`/public`, `/public/{slug}`), `ReservedSegments`.
- **Nucleo**: `Content/TileEndpoints.cs` (`/tiles/basemap.pmtiles`, `Range` e `ETag` forte, niente compressione), `HubPaths.Tiles`,
  `IModule.ReservedSegments` + `ModuleRegistry` + `ContentAddresses`, `img-src` guadagna `blob:` in `config/security.json`.
- **SPA**: `shared/ui/RouteMap.tsx` e `shared/ui/greatCircle.ts` (elenco chiuso dei componenti: il ventiduesimo),
  `modules/flightops/screens/public.tsx` e `TourCards.tsx`, il blocco, `backendPaths.ts`, `BlockRegistration.propertyLabels`.
- **Documenti**: `docs/UI-GUIDELINES.md` §3, `docs/FORKING.md` (il file della mappa), `tools/basemap.mjs`, piano 0.90 (§8.3, §14).

## 5. Che cosa resta aperto

- **Passenger e `Range`** (verifica 1 della nota del 15 settembre): qui risponde Kestrel, e risponde 206 con un `ETag` forte
  (`PublicTourTests`). In produzione lo dirà lo staging, che non c'è ancora.
- **`blob:` in `img-src`** (verifica 2): la suite del giro completo gira sotto la CSP vera e la mappa disegna, ma non è stato
  provato a **togliere** `blob:` per vedere se qualcosa si rompe. La direttiva resta, misurata dalla documentazione di MapLibre.
- **I colori degli stati sulle tratte**: il componente li ha tutti e quattro, la pagina ne usa due (da fare, non ancora rilasciata).
  Verde e arancione arrivano con i PIREP (T11).
