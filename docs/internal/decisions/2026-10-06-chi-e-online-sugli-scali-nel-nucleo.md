# Chi è online sugli scali: la forma nel nucleo (E4b)

**Data:** 6 ottobre 2026 — fase E4b di M4, PR del nucleo #226
**Stato:** **scelta tecnica**, per dare forma nel codice a una decisione già presa: E4b è la (b) della nota
`2026-10-06-chi-e-online-sugli-scali-di-un-evento` (fase E4, #223, unita su `main` lo stesso giorno con lo stato «decisa»), che
**Carmine ha scelto** il 6 ottobre 2026 ([risposte sulla
#223, punto 2][a223], pubblicate dal master su sua istruzione): «(b), a small core phase E4b, in its own pull request with its note,
and mounted by the first module phase after it». **Uno scostamento** dalla lettera di quella nota (§3, punto 2), deciso dalla misura
che la nota stessa chiedeva (§2 qui sotto): **un whazzup al minuto per tutti**, non uno per insieme di scali. Nessuna domanda da questa
fase; **due cose le porta a Carmine il revisore** ([rilievi sulla #226][r226], punto 5, «nothing to change»): questo scostamento —
una lettura al minuto per tutti, tenuta in memoria (`IvaoNetworkPicture`), che il revisore trova il disegno migliore — e **le parole del
titolo** della striscia con gli scali («Su questi scali adesso», «At these airports now»), che sono sue da confermare. I punti 2–4 dei
rilievi sono corretti nel codice (§3).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è — il blocco `networkStats` e la `LiveStatusStrip` — e si
estende, nel nucleo, in una PR a sé prima della fase del modulo che lo usa (§0 regola 6).

[a223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039
[r226]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/226#issuecomment-6021367518

## 1. Che cosa serve, e perché il modulo non ne fa a meno

Il design M4 §7.1 vuole sulla pagina di un evento, **il giorno dell'evento**, la `LiveStatusStrip` con chi è online sugli scali
dell'evento; §9.1 dice come: `GetNetworkStatusAsync`, il whazzup con 60 secondi di cache, letto alla richiesta e mai campionato. La
nota di E4 ha misurato nel codice che oggi non si può (§2 di quella nota): il blocco conta solo l'area della divisione, la chiave della
cache di `IvaoAirspace` non distingue due insiemi di scali senza centri, il modulo non nomina la rete
(`EventsArchitectureTests.TheModuleDoesNotNameTheNetwork`, piano §4.2), e la striscia fa una domanda fissa. Ogni strada passa da file
del nucleo: questa fase.

## 2. La misura (6 ottobre 2026)

La nota di E4 lasciava un ⚠️: «da misurare che un whazzup per insieme di scali al minuto regga (oggi uno solo per la divisione)».
Misurato da questa macchina con un programma usa e getta sopra `IvaoWhazzup` (non nel repository), leggendo
`https://api.ivao.aero/v2/tracker/whazzup` come lo legge l'hub — senza token, senza decompressione, `JsonDocument` e `Clone`:

- **alle 16:00 UTC** (un martedì pomeriggio): **793.080 byte**, 59 controllori e 550 piloti (626 connessioni); **alle 16:18 UTC**:
  774.049 byte, 54 e 539. Lo scaricamento **75–513 ms** (il primo di ogni serie il più lento), la lettura e la copia 1–20 ms;
- **ogni risposta viene dalla cache di Cloudflare** davanti a IVAO (`cf-cache-status: HIT`, `Age` da 0 a 9 secondi, `Last-Modified`
  pochi secondi prima), che la rinnova ogni dieci secondi circa: una chiamata in più quasi non arriva a IVAO, e costa all'hub circa
  0,8 MB;
- **una sera di punta non è misurata**: in proporzione (circa 1,25 KB per connessione) tremila connessioni sarebbero circa 4 MB.

**Per l'uso che il design chiede regge**: la striscia della divisione più la pagina di ogni evento del giorno aperta da qualcuno, cioè
una manciata di letture al minuto. **Ma l'insieme lo sceglie chi chiede**: `/api/blocks/data/{type}` è anonimo, e gli scali sono un
parametro della richiesta. Con un whazzup per insieme al minuto ogni insieme inventato — `airports: ["Q001"]`, `["Q002"]`, … — è uno
scaricamento intero: venti richieste al secondo diventano 16 MB al secondo scaricati dall'hub e venti chiamate al secondo dal suo
indirizzo verso l'API di IVAO (i limiti di chiamate di IVAO non sono misurati, design M4 §9.1), su un blocco che fino a oggi costava
una chiamata al minuto qualunque cosa accadesse. Gli insiemi non li limitano gli eventi, e un limite agli insiemi di un minuto
toglierebbe la striscia agli eventi che chiedono dopo chi li ha inventati. **Come regola della cache non regge.**

**Con la forma del §3**, misurata sulla stessa lettura (54 controllori, 539 piloti): la lettura tenuta per un minuto pesa **circa 75
KiB** e si fa in circa 9 ms; contare uno spazio la prima volta costa **0,05 ms** (tre scali) — 1,5 ms la divisione alla prima chiamata,
con la compilazione —, poi la risposta tenuta si legge in un microsecondo o due; **mille insiemi inventati in un minuto: 24 ms in tutto**,
invece di mille scaricamenti (circa 770 MB). Senza tetto la lettura teneva anche le loro mille risposte, circa 246 KiB fino alla sua
scadenza; **dopo la revisione** (rilievi sulla #226, punto 2) una lettura tiene le risposte di al più sedici spazi, e conta ogni volta
tutti gli altri (§3, punto 3).

## 3. La forma nel codice

1. **`NetworkStatsProvider`** (`Core/Content/CoreDataBlockProviders.cs`) legge **`airports`**, un elenco di stringhe, con
   `BlockProps.ReadTexts` (`Core/Content/DataBlocks.cs`, accanto a `ReadInstant`). È una domanda della schermata, come `from` e `to`
   del calendario: **non è nello schema zod** del blocco, che è ciò che un editor salva, e una pagina pubblicata con gli scali di un
   evento invecchierebbe con lui. Con l'elenco, lo spazio è `IvaoAirspace.OfAirports`; senza, quello della divisione
   (`IFirDirectory.GetAirspaceAsync`), come prima. **Un elenco senza nessuno scalo non conta nessuno, mai la divisione** (come
   `TryDepartment`: una domanda sbagliata restringe, non allarga), e al più `DataBlockScope.MaxItems` (50) scali, **contati dopo la
   pulizia** (rilievi sulla #226, punto 3): un blocco non è un'esportazione, e cinquanta voci che non sono scali non ne spingono fuori
   uno vero. Le due cifre `divisionAtc` e `divisionPilots` contano lo spazio chiesto: nessuna cifra nuova, le stesse parole.
2. **`IvaoAirspace`** (`Core/Ivao/IvaoNetworkStatus.cs`): `OfAirports(scali, limite)` fa lo spazio dei soli scali — ripuliti, in
   maiuscolo, una volta ciascuno, e solo quello che può essere uno scalo della fotografia, **da una a quattro lettere o cifre**
   (larghi quanto `ref_ivao_airports.icao`, `IvaoAtcPosition.MaxAirportLength`; rilievi sulla #226, punto 4): altro non è uno scalo,
   allungherebbe soltanto la chiave e potrebbe portarci la virgola che la separa (`A,B` e `C` si leggerebbero come `A` e `B,C`); poi
   **al più il limite**, nell'ordine chiesto —, senza centri: un controllore conta solo su uno di loro, non il settore sopra di loro.
   **La `CacheKey` nomina gli scali quando non ci sono centri** (`0/2/LIRA,LIRF`); con i centri è quella di prima
   (`{centri}/{scali}/{elenco dei centri}`): la divisione tiene la sua chiave.
3. **Un whazzup al minuto per tutti** (`IvaoApiClient.GetNetworkStatusAsync`, `Core/Ivao/IvaoWhazzup.cs`): il whazzup si legge in un
   **`IvaoNetworkPicture`** — i due totali della rete, ogni controllore con nominativo, stazione e frequenza, le due estremità di ogni
   piano di volo; nessun VID, nessun nome, nessuna traccia — che il client tiene **per un minuto sotto una chiave sola**
   (`ivao-api:network-picture`), il fallimento compreso, come prima. Ogni spazio si conta dalla lettura con la sua `CacheKey`; **la
   lettura tiene le risposte dei primi `IvaoNetworkPicture.MaxKeptAnswers` (16) spazi** — la divisione e gli eventi di un giorno, con
   margine — e conta ogni volta tutti gli altri, un ventesimo di millisecondo: gli scali sono un parametro di una richiesta anonima, e chi
   inventa insiemi non deve far crescere quello che una lettura tiene (rilievi sulla #226, punto 2). Le risposte tenute se ne vanno con la
   lettura: nessuna è più vecchia della lettura da cui viene. La regola «in area» resta di `IvaoWhazzup` (`Count`), per il client vero e
   per quello delle fixture (`IvaoWhazzup.Read(root).For(spazio)`).
   **`IIvaoApiClient` non cambia**: i doppi dei test del maintainer lo implementano com'è.
4. **`LiveStatusStrip`** (`web/src/shared/ui/LiveStatusStrip.tsx`): `airports?: readonly string[]`. Con gli scali la domanda li porta e
   il titolo è `liveStatus.airportsTitle` («Su questi scali adesso», «At these airports now»); senza, la domanda e il titolo sono
   quelli della divisione, parola per parola. Un elenco vuoto è un elenco: il titolo degli scali, nessuno contato.
5. **Nessuna migrazione, nessun endpoint, nessun blocco nuovo, nessun componente nuovo** (la striscia è già nell'elenco chiuso), nessun
   ICAO né FIR nel codice: gli scali li dice la schermata. Una riga in `docs/UI-GUIDELINES.md` dice dell'opzione.

## 4. Gli scostamenti

- **Dalla nota di E4, §3 punto 2**: la chiave nomina gli scali, come chiesto; **il whazzup è uno al minuto per tutti, non uno per
  insieme**, perché la misura (§2) dice che uno per insieme non regge quando l'insieme lo sceglie un chiamante anonimo. Le risposte
  sono le stesse; le chiamate a IVAO, in ogni caso, non più di oggi.
- **Che cosa si tiene per un minuto**: prima la risposta della divisione (poche centinaia di byte), ora la lettura di tutti (circa 75
  KiB un martedì pomeriggio; qualche centinaio in una sera di punta, in proporzione) con al più sedici risposte. Il principio del design
  M1 §6.2 — del payload si tiene solo quello che se ne ricava, mai il payload — resta: la lettura non ha VID, nomi né tracce, e gli
  0,8 MB se ne vanno come prima.

## 5. Le alternative

| Alternativa | Pro | Contro |
|---|---|---|
| (a) Un whazzup per insieme al minuto (la lettera della nota di E4) | il codice più corto | scaricamenti senza limite da un chiamante anonimo (§2) |
| **(b) Un whazzup al minuto per tutti, contato per ogni spazio** (scelta) | uno scaricamento al minuto qualunque cosa accada; le stesse risposte | un tipo in più (`IvaoNetworkPicture`), circa 75 KiB tenuti per un minuto |
| (c) La (a) con un tetto agli insiemi di un minuto | limitata | chi inventa insiemi toglie la striscia agli eventi che chiedono dopo |
| (d) Tenere il payload intero per un minuto | nessun tipo nuovo | da 0,8 a 4 MB tenuti con i VID e i nomi: contro il design M1 §6.2 |

## 6. Per la fase del modulo che monta la striscia

La prima fase del modulo dopo il merge di E4b (Carmine, punto 2 sulla #223; «per esempio E6b» diceva la nota di E4):

- **la pagina dell'evento** (`web/src/modules/events/screens/public.tsx`, di E4) monta `<LiveStatusStrip airports={…} />` con gli ICAO
  dei suoi scali **il giorno dell'evento** (design §7.1), e la fase scrive come legge «il giorno» (nell'ora della divisione da
  mezzanotte a mezzanotte, o dall'inizio alla fine); **un evento di tutta la divisione** non ha scali e non monta niente: la striscia
  della divisione è già in cima al sito;
- ⚠️ **dove**: `docs/UI-GUIDELINES.md` vuole la striscia «in the banner slot of `Shell`», con una misura in `web/e2e/live-status.spec.ts`,
  e il banner del layout `_public` ha già quella della divisione. Dentro la pagina è una scelta della fase, da scrivere; al posto di
  quella della divisione nel banner vuole che il layout sappia gli scali della rotta, cioè un'altra modifica del nucleo con la sua nota;
- **i test**: una spec che ferma `networkStats` (`stubTheBlockData`) e cerca il titolo `liveStatus.airportsTitle`, leggendo nella
  richiesta gli scali chiesti; sul banco la fixture `tests/fixtures/ivao/whazzup.json` ha `LIRR_CTR`, `LIMC_APP`, `LIRF_TWR`,
  `EDDF_TWR` e i voli LIRF→LIMC, EDDF→LIMC, EDDF→EGLL: un evento a `LIRF` mostra un controllore e un pilota.

## Da portare nel piano

- **§9.1**, riga «Live status»: dal 6 ottobre 2026 (M4, E4b) anche **gli scali che una schermata chiede** — quelli di un evento, il suo
  giorno —, con **un whazzup al minuto per tutti**, contato per ogni spazio, e la striscia con gli scali facoltativi.
- **§9.3**, blocchi Data: `networkStats` con gli scali che una schermata chiede, fuori dallo schema, come `from` e `to` del calendario.
- **`10-piano-implementazione-m4.md`**: la riga e la sezione di E4b (scritte da questa fase) e la fase del modulo che monta la striscia.
- Il design M4 §7.1 e §9.1 restano com'è: «il chi è online adesso della pagina è `GetNetworkStatusAsync` (whazzup, 60 secondi di
  cache)» resta vero.
