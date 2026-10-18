# Il banco fa girare un job, e dà sessioni di rete a una sua persona (E10w)

**Data:** 18 ottobre 2026 — fase E10w di M4, PR del nucleo (#300)
**Stato:** **decisa** da Carmine sull'issue #294: la **(B)** — «a small core phase for the bench first, with its own note, before E15b:
an `/e2e/jobs/{name}` endpoint that runs a job now, mapped only on the bench like `/e2e/signin`, and a sessions fixture for a bench
person moved to the day asked» (18 ottobre 2026, in chat al master, che l'ha pubblicata sull'issue su sua istruzione: [risposta][a294]).
La forma qui sotto è una scelta tecnica della fase, sopra quella risposta.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono due meccanismi che ci sono — il banco con i suoi due lucchetti
(`/e2e/signin`, design M1 §11.1) e i job una volta per occorrenza (`ScheduledJobs`, nota `2026-10-09-i-job-che-recuperano`), più il client
delle fixture che sposta già le prenotazioni sul giorno chiesto (E15a) — invece di scrivere nello spec di E15b un'attesa di mezz'ora o
una riga di SQL. È una PR del nucleo, prima di E15b che la usa: **nessun uso negli eventi qui**.

[a294]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/294#issuecomment-6246961879

## 1. Che cosa serve, e perché il banco non ce l'ha

Il giro intero di M4b (design M4 §15: «disponibilità, roster proposto, corretto e pubblicato; PIREP di supporto, validato, un segnale»)
in `e2e:full` (E15b, `events-m4b-round.spec.ts`) inciampa in due cose, misurate sul codice (issue #294):

1. **Il banco non fa girare un job quando lo chiede uno spec.** `/e2e/signin` è il suo solo indirizzo, e `/api/jobs/run` (E10j) fa
   girare solo i job **dovuti**. Il roster lo propongono e lo pubblicano solo i giri di `events-roster` ai minuti 3, 18, 33 e 48: lo spec
   aspetta fino a un quarto d'ora, e il giro intero dura 15–30 minuti, in locale e in CI.
2. **Il banco non ha sessioni di rete per le sue persone.** `FixtureIvaoApiClient` legge `tracker-sessions-{vid}.json` così com'è, con
   le date registrate: un PIREP di supporto del banco non ha voci verificate, e nessuna regola di award dà un segnale. E14a ed E14b hanno
   scritto il loro «fatta quando» sul banco segnando la voce verificata in SQL.

## 2. Che cosa c'è, letto nel codice (`main` a `e7b1331c`)

1. **`/e2e/signin`** (`src/IvaoHub.Web/E2E/E2ESignIn.cs`): due lucchetti. Il primo è l'ambiente `E2E`: `Program.cs` registra e mappa
   solo dentro `if (…IsEnvironment(HubEnvironments.E2E))`; il secondo è `E2E:Enabled`, letto da `MapE2ESignIn` che senza ritorna
   senza mappare; e il flag fuori dal suo ambiente ferma l'applicazione (`HubConfiguration.RequireE2EEnvironment`). Il prefisso `/e2e`
   è già fuori dal ripiego della SPA sul banco (`HubPipeline.MapSpaFallback`).
2. **`ScheduledJobs`**: la guardia davanti a ogni giro (il lucchetto del job nel database, poi «è dovuto?» rispetto all'ultimo giro) e
   il passaggio che lancia i dovuti uno dopo l'altro con `IScheduler.TriggerJob` e aspetta che la guardia dica `Ran` o `Skipped`. Un job
   lanciato mentre la sua occorrenza è già fatta è saltato: è ciò che tiene «una volta per occorrenza», ed è ciò che a uno spec non
   serve.
3. **Ogni job scrive la sua riga** in `hub_jobs_log` (`StartedAt`, `FinishedAt`, `Status`, `Message`), da sé; uno che non trova niente
   da fare può non scriverla (i controlli dei tour).
4. **`FixtureIvaoApiClient.GetDailyAtcBookingsAsync`** (E15a) sposta le prenotazioni registrate sul giorno chiesto; **`replay.ts`**
   (T11b) scrive accanto ai file una copia di un volo con gli istanti spostati a ieri. **`tracker-airport-LIRF.json`** (E10a) è una sera
   di Fiumicino senza nessuno dentro, sul giorno sostitutivo 1° gennaio 2001: c'è la torre (`LIRF_TWR`) e i voli.

## 3. La forma

### 3.1 `POST /e2e/jobs/{name}`

```csharp
// ScheduledJobs (Core/Jobs/ScheduledJobs.cs)
public async Task<JobRunState?> RunNowAsync(string job, CancellationToken cancellationToken = default);
```

1. **Lo stesso job, lanciato adesso.** `RunNowAsync` cerca il job per nome fra quelli che Quartz conosce (null se non c'è) e lo lancia
   come il passaggio lancia un dovuto — `TriggerJob`, la stessa attesa fino a `LongestRun` —, con un segno nei dati del trigger
   (`hub.jobs.now`).
2. **La guardia vale, la domanda «è dovuto?» no.** Con il segno, la guardia prende il lucchetto come per ogni giro — un giro in un altro
   processo resta suo, e la risposta è `Skipped` — ma non chiede l'ultimo giro: lo spec l'ha chiesto adesso. Senza il segno niente cambia.
3. **È un giro come gli altri**: il job scrive la sua riga; il suo inizio conta come ultimo giro, quindi l'occorrenza che è già passata
   è fatta, e la prossima viene come sempre.
4. **L'indirizzo** (`src/IvaoHub.Web/E2E/E2EBenchRuns.cs`): `POST /e2e/jobs/{name}`, nessun corpo. Risponde quando il giro è finito:
   `200 { job, outcome: "Ran" | "Skipped" | "Running", run: { startedAt, finishedAt, status, message } | null }` — `run` è la riga che
   il giro ha scritto (la più recente del job iniziata dal secondo della chiamata), `null` per un job che non ne scrive. Un nome che
   l'hub non ha: **404**.

### 3.2 `PUT /e2e/sessions/{vid}` e `DELETE /e2e/sessions/{vid}`

1. **Il modello** `tests/fixtures/ivao/tracker-sessions-bench.json`: due righe di `tracker-airport-LIRF.json`, già senza nessuno e sul
   giorno sostitutivo — la torre `LIRF_TWR` (40 minuti e 54 secondi) e, 50 minuti e 8 secondi dopo la sua apertura, un volo che parte da
   Fiumicino (71 minuti e 52 secondi). Cambiano tre cose: il membro è `0`, un segnaposto; sessioni e piani di volo sono numerati da 1; il
   volo arriva a Milano (`LIMC`) e non ad Ajaccio, perché il banco conosce cinque aeroporti (come fa `replay.ts`). **Nessuna chiave con
   cui l'API di IVAO ritrovi una persona**: niente VID, niente numeri veri, niente giorno vero (il README delle fixture).
2. **`BenchNetworkSessions`** (`Core/Ivao/`, singleton): chi del banco ha ricevuto le sessioni, e da quando. È registrato **solo sul
   banco** (`AddE2EBenchRuns`, accanto a `AddE2ESignIn`); `FixtureIvaoApiClient` lo prende come parametro facoltativo, e dovunque altro
   non c'è: il client legge i suoi file come prima.
3. **Il client delle fixture**, per una ricerca con un VID a cui il banco ha dato le sessioni, aggiunge alle righe dei suoi file quelle
   del modello **spostate** (`MoveBenchTemplate`): ogni istante della riga di uno stesso scarto, così che la prima sessione (la torre)
   cominci **al momento chiesto** e le distanze fra le due restino; il VID della persona al posto dello `0`; il numero della sessione fatto
   del VID e del suo (`999004` e `1` danno `999004001`), così due persone non ne condividono uno. Poi la regola del tracker sceglie fra
   tutte (`IvaoTrackerReader.Answers`), come per le righe registrate.
4. **Gli indirizzi**: `PUT /e2e/sessions/{vid}` con `{ "atUtc": "2026-10-18T14:03:00Z" }` dà le sessioni (una seconda chiamata le
   sposta) e risponde `200 { vid, sessions: [{ id, callsign, connection, startedAt, endedAt, departureIcao, arrivalIcao }] }`, **letto
   attraverso `IIvaoApiClient`**, la strada di una domanda di un modulo; senza `atUtc` è un 400 (`errors.required`). `DELETE` le
   riprende (204). Un VID che non è una delle cinque persone del banco (`E2E:Vid`, `Pilot`, `Assistant`, `Trainer`, `Events`): **404**.
5. **Nel processo, mai in un file.** Un banco che si ferma le dimentica, e uno spec interrotto non lascia un file che cambi i giri dopo
   (a differenza di una copia di `replay.ts` rimasta lì).

### 3.3 Fuori dal banco non ci sono

Tutti e tre gli indirizzi sono mappati da `MapE2EBenchRuns`, chiamato **nello stesso `if` dell'ambiente `E2E`** di `MapE2ESignIn`, e
ritornano senza mappare se `E2E:Enabled` non c'è: **gli stessi due lucchetti**, nessun modo nuovo di essere un banco. Il generatore
dell'OpenAPI gira come `Production`, quindi non li vede.

### 3.4 Come lo chiama uno spec (E15b)

`web/e2e/full/bench.ts`:

```ts
const ran = await runJob(context.request, 'events-roster'); // asserisce 200 e outcome 'Ran'; ran.run è la riga del giro
const sessions = await giveSessions(trainer.request, 999004, starts); // la torre da `starts`, poi il volo
afterwards(() => takeSessions(trainer.request, 999004));
```

Lo spec di E15b non aspetta più il quarto d'ora: dopo la chiusura delle disponibilità fa girare `events-roster` (proposta e
pubblicazione), dopo la fine `events-after`, e dà al formatore la torre dall'inizio del turno, perché il controllo del PIREP ATC trovi i
minuti e la regola di award dia il suo segnale.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **(A) dell'issue: aspettare i giri veri** | 15–30 minuti a ogni build, e il segnale provato solo in integrazione; Carmine ha scelto (B) |
| **`/api/jobs/run` con un nome** | è l'indirizzo dell'installazione, con il suo token: un modo di far girare un job fuori orario anche in produzione, che nessuno ha chiesto |
| **Un job lanciato con `TriggerJob` da solo, senza segno** | la guardia lo salta quando la sua occorrenza è fatta: lo spec avrebbe `Skipped` proprio quando serve |
| **Togliere la guardia per il banco** (lanciare `IJob.Execute` a mano) | non sarebbe più lo stesso giro: niente lucchetto, niente scope di Quartz, niente dati del trigger |
| **Le sessioni sul giorno chiesto, con le ore registrate** (come le prenotazioni) | l'evento di E15b dura un quarto d'ora e comincia fra quattro minuti, a un'ora qualunque: un giorno con le ore fisse non copre il suo turno; il momento sì |
| **Le sessioni scritte in un file dallo spec** (come `replay.ts`) | un file lasciato da uno spec interrotto dà sessioni a ogni giro dopo, e il file della persona sostituisce i suoi; in memoria si dimenticano con il banco |
| **Le sessioni sempre presenti per una persona del banco** | cambierebbero le verifiche degli spec che oggi contano su nessuna sessione (un no-show, un PIREP non verificato) |
| **Una persona nuova del banco** | le persone del banco bastano (il formatore è il controllore di E15b, il pilota pilota); una nuova vorrebbe VID e configurazione |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- `src/IvaoHub.Core/Jobs/ScheduledJobs.cs` (`RunNowAsync`, il segno `hub.jobs.now` nella guardia);
- `src/IvaoHub.Core/Ivao/BenchNetworkSessions.cs` (nuovo) e `src/IvaoHub.Core/Ivao/FixtureIvaoApiClient.cs` (`BenchTemplate`,
  `MoveBenchTemplate`, il parametro facoltativo);
- `src/IvaoHub.Web/E2E/E2EBenchRuns.cs` (nuovo) e `src/IvaoHub.Web/Program.cs` (le due righe nel blocco del banco);
- `tests/fixtures/ivao/tracker-sessions-bench.json` (nuovo) e il `README.md` delle fixture (una sezione);
- `web/e2e/full/bench.ts` (`runJob`, `giveSessions`, `takeSessions`) e `web/e2e/full/README.md` (un punto in «What the bench is»);
- i test, tutti nuovi: `tests/IvaoHub.UnitTests/BenchNetworkSessionsTests.cs`, `tests/IvaoHub.IntegrationTests/E2EBenchRunsTests.cs`,
  `web/e2e/full/bench-runs.spec.ts`. **Nessun test esistente cambiato**, nessuno spec del maintainer.
- **Nessuna riga** di un modulo; nessuna migrazione; `/api/me`, `schema.d.ts` e l'OpenAPI uguali.

## 6. Una riga per Carmine (`CONTRIBUTING.md`, suo)

`CONTRIBUTING.md` è del maintainer: questa fase non lo tocca. Se lo vuole, accanto a `/e2e/signin` nel paragrafo sul banco:

> The bench can also run a job now (`POST /e2e/jobs/{name}`, `runJob` in `web/e2e/full/bench.ts`) and give one of its people sessions
> on the network from a moment (`PUT /e2e/sessions/{vid}`, `giveSessions` / `takeSessions`): a spec never waits for a job's hour.

## Da portare nel piano

- **§16** (i job, nota `2026-10-09-i-job-che-recuperano`): un giro **chiesto adesso** — solo dal banco, `ScheduledJobs.RunNowAsync` —
  prende il lucchetto del job come ogni giro e non chiede se è dovuto; il suo inizio conta come ultimo giro. Dal 18 ott 2026 (M4, E10w).
- **Design M1 §11.1** (il banco, `/e2e/signin`) e `web/e2e/full/README.md`: dietro gli stessi due lucchetti, `POST /e2e/jobs/{name}`
  (un job adesso, con la sua riga) e `PUT`/`DELETE /e2e/sessions/{vid}` (le sessioni del modello spostate al momento chiesto, per una
  delle cinque persone, nel processo).
- **Design M0 §4.6** (le fixture di IVAO): `tracker-sessions-bench.json`, letto solo per una persona a cui il banco l'ha dato.
- **`CONTRIBUTING.md`**: la riga del §6, se Carmine la vuole.
- **`10-piano-implementazione-m4.md`**: E10w, scritta in questa PR; E15b dipende da E10w.
