# Il primo giro dei job dopo la pagina di chi ha svegliato l'hub

**Data:** 14 novembre 2026
**Stato:** **Proposta** della sessione `fix/jobs-catch-up-start` (issue #399). Il codice della PR è la raccomandazione del §2;
la domanda del §4 è per Carmine.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende un meccanismo che c'è, il recupero dei job (`JobCatchUp`, nota
`2026-10-09-i-job-che-recuperano` §1.4, che dopo il merge non si tocca), perché non fa quello che la sua nota gli chiede («cinque
secondi dopo che l'hub è partito (**dopo la pagina di chi l'ha svegliato**)»). Il contatore delle richieste in corso c'è già
(`RequestsInFlight`, nota `2026-11-14-le-richieste-in-corso-nella-diagnostica`): diventa uno solo per processo. Nessun
meccanismo nuovo, nessuna configurazione nuova, nessun modulo tocca niente.

## 1. Che cosa serve

Dall'analisi della velocità del sito di prova (`docs/internal/test/velocita-del-sito.md` §2.7 e §3.5, dati 18–26 ottobre):

- il primo controllo del recupero parte **5 s dopo «Application started»**; la richiesta che ha svegliato l'hub arriva al codice
  **circa 2 s dopo** (7,7 s dalla nascita del processo contro 5,8 s di avvio, mediane), e la pagina manda subito le sue richieste
  iniziali, che costano dieci volte le calde perché la CPU compila. Il giro cade **in mezzo alla prima pagina**;
- lo fa in **348 avvii su 395**, quasi sempre con `flightops-flight-checks`, che un processo nuovo trova sempre dovuto (§4).

## 2. Che cosa cambia, e le scelte

**Il primo controllo** viene **non prima di 5 s** dall'avvio (`JobCatchUp.FirstCheck`, come oggi), **e poi solo quando nessuna
richiesta è in corso da 3 s** (`JobCatchUp.Quiet`), **e comunque non dopo 30 s** dall'avvio (`JobCatchUp.Latest`). I controlli
successivi restano uno al minuto, come prima. La regola è una funzione pura (`JobCatchUp.UntilFirstCheck`), provata da sola.

- *Scelta*: **la quiete, non un ritardo fisso più lungo** (la seconda strada della issue, 15–20 s). Un ritardo fisso non sa dove
  cade la pagina: a 20 s può cadere sulla seconda pagina dello stesso visitatore, e un processo che vive 10–20 s (un quinto, §2.1
  dell'analisi: le vite sono piatte fra 10 e 60 s) non recupera mai. La quiete aspetta il buco fra due pagine, che c'è quasi sempre
  (il silenzio più lungo dentro un processo è 6 s in mediana).
- *Scelta*: **3 s di quiete**. Più della pausa di un browser fra la risposta di `index.html` e le chiamate del suo script (il
  caricamento e la lettura dello script, sotto il secondo su un computer, un secondo o due su un telefono); meno del silenzio dopo il
  quale Passenger ferma l'hub (8 s in mediana dall'ultima risposta). Con 5 s o più il giro si perderebbe spesso, fermato insieme al
  processo.
- *Scelta*: **non prima di 5 s**, il valore di oggi: un hub che nessuno ha chiamato (il ping di `/health`, che non conta, o un
  avvio senza visite) fa il suo giro a 5 s come prima, e le prove d'integrazione del recupero, che non mandano richieste, non
  cambiano tempi.
- *Scelta*: **non dopo 30 s**: un hub con richieste continue recupera lo stesso, prima della fine di quasi ogni processo (pochissimi
  passano il minuto).
- *Scelta*: **un solo contatore per processo**, registrato con i job (`AddHubJobs`) e alimentato da un middleware **sempre
  acceso** (`HubPipeline.UseRequestsInFlight`), subito sotto quello di `starts.txt`. Il recupero aspetta la quiete anche quando la
  diagnostica delle richieste è spenta. La riga `STOP` di `starts.txt` e la colonna `others_n` del file delle richieste leggono
  questo stesso contatore, invece dei due che avevano (`StartsWatch` e `RequestTimingsMiddleware` ne creavano uno ciascuno). La
  riga di una richiesta conta le altre prima che la richiesta finisca nel contatore (`RequestsInFlight.Others`): lo stesso numero
  che dava `End`, con lo stesso significato della nota del 14 novembre.
- Un controllo di `/health` **non è una richiesta** neanche qui, come nei due file: un ping che tiene sveglio l'hub non ritarda il
  giro.

## 3. Alternative scartate

- **Un ritardo fisso più lungo** (15–20 s): vedi sopra.
- **Dopo la prima richiesta e basta**: la prima richiesta è `index.html`; le chiamate dello script arrivano dopo, ed è lì che
  la CPU serve.
- **Le metriche di ASP.NET Core** (`current-requests` dell'`EventSource` di hosting) al posto del contatore: un ascoltatore di
  eventi nel processo per leggere un numero che il contatore della diagnostica ha già, e che conterebbe anche `/health`.
- **Ogni controllo, non solo il primo, dopo la quiete**: i controlli del minuto cadono quasi sempre in un processo che non c'è più,
  e uno al minuto su un hub vivo non è la prima pagina. Non serve.

## 4. La domanda per Carmine: `FlightCheckJob` in ogni processo

Con il primo giro dopo la pagina, `flightops-flight-checks` **non cade più sulla prima pagina**, ma gira ancora **in ogni processo
nuovo**: non scrive una riga in `hub_jobs_log` quando non trova niente («six empty lines an hour would hide the others»,
`FlightCheckJob.cs`), quindi un processo che non l'ha fatto girare lui lo trova sempre dovuto (nota `2026-10-09` §1.1). Costa il
blocco del database su una connessione nuova, due query e il modello EF dei tour; con processi da 30–50 s, ogni 30–50 s.

**Raccomandato**: che `FlightCheckJob` scriva la sua riga anche quando non trova niente («nothing waiting»). Allora gira una
volta per occorrenza del suo cron (ogni dieci minuti) qualunque processo sia vivo, come tutti gli altri job; sono **al più sei
righe l'ora**, contro le sessanta della coda delle mail, che scrive la sua riga ogni minuto anche quando non manda niente
(`NotificationDispatchJob`), e nessuna schermata legge il registro (solo il banco e2e e il job della conservazione, che lo tiene
un mese). È una modifica del modulo dei tour, non del nucleo: una **PR a parte**, piccola, dopo questa (`CLAUDE.md` §0 regola 6:
nucleo e modulo non si mescolano). L'alternativa — lasciarlo com'è — costa poco ora che non cade sulla prima pagina, e di più se
la #398 rende pigri i modelli dei moduli, perché il giro costruirebbe quello dei tour in ogni processo.

## 5. Che cosa tocca

`src/IvaoHub.Core/Jobs/JobCatchUp.cs` (la regola), `JobServiceCollectionExtensions.cs` (il contatore unico),
`src/IvaoHub.Core/Services/RequestsInFlight.cs` (la quiete, `Others`, `Begun`), `src/IvaoHub.Web/HubPipeline.cs` (il middleware),
`Program.cs` (dove sta), `StartsWatch.cs` e `RequestTimingsMiddleware.cs` (leggono il contatore unico), i test unitari e
d'integrazione dei job. Nessuna migrazione, nessuna pagina, nessun modulo, nessuna chiave di configurazione.

## Da portare nel piano

- **§5.2** (Job): «il recupero parte cinque secondi dopo l'avvio» diventa «il primo controllo del recupero viene non prima di 5 s
  dall'avvio, quando nessuna richiesta è in corso da 3 s, e non dopo 30 s; poi ogni minuto» (#399).
- **§11.3 punto 2** (diagnostica): le richieste in corso sono un solo contatore per processo, che leggono la riga `STOP`, la colonna
  `others_n` e il primo controllo dei job.
- `docs/internal/test/velocita-del-sito.md` §4: la voce **C5** è fatta per il giro sulla prima pagina; la seconda metà dipende
  dalla risposta del §4.
