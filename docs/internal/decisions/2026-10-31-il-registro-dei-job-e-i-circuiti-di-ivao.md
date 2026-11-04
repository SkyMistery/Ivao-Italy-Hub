# Il registro dei job tiene un mese, e un circuito per ogni famiglia di chiamate a IVAO

**Data:** 31 ottobre 2026, fase di test.
**Stato:** **decisa da Carmine in chat al master, 31 ottobre 2026**: le due domande della PR #422 (correzioni minori della
revisione del codice, lotto 4, issue #325, elementi 5 e 6), con la raccomandazione di ciascuna — trenta giorni e l'ultimo giro finito
di ogni job sempre tenuto, con il job `jobs-log-retention` alle 04:30; un interruttore per ogni famiglia di chiamate a IVAO (accesso,
rete, tracker, prenotazioni, dati di riferimento), con le stesse soglie. Proposte da una sessione di lavoro di Carmine.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il registro dei job e il gestore standard di resilienza esistono già; si usano come
sono. Nessun meccanismo nuovo.

## 1. Il registro dei job tiene un mese (elemento 5)

`hub_jobs_log` cresceva senza fine: la coda delle mail scrive una riga al minuto, «skipped» compreso, e i job dei moduli una ogni quarto
d'ora — mille, duemila righe al giorno sull'hosting condiviso, e niente le cancellava. Delle righe vecchie se ne leggono due per job: il
guardiano dei job legge l'ultimo giro **finito** di ciascuno (nota `2026-10-09-i-job-che-recuperano`), e i job di rilascio di eventi e tour
(`EventReleaseJob`, `TourReleaseJob`) l'ultimo **riuscito**, da cui comincia la loro finestra; il banco e2e legge le righe del giro chiesto
adesso. Il resto non lo legge nessuno.

- Un job del nucleo, **`jobs-log-retention`** (`Core/Jobs/JobsLogRetentionJob.cs`), alle **04:30** nel fuso della divisione, cancella le
  righe di più di **30 giorni**, mille alla volta, con `SaveChanges` come ogni scrittura dell'hub.
- **L'ultimo giro finito di ogni job resta**, per vecchio che sia: è quello che dice quando il job è dovuto, e un job mensile o annuale
  senza la sua riga sembrerebbe mai girato e ripartirebbe al primo avvio.
- **E resta l'ultimo giro riuscito** (revisione della #422, punto 1): i job di rilascio cominciano da lì la loro finestra, e un job che fallisse
  per più di un mese, senza quella riga, ripartirebbe da `DateTime.MinValue` e proietterebbe di nuovo ogni evento e ogni tour pubblicato.
- Trenta giorni: abbastanza per rileggere che cosa è successo la settimana prima di una segnalazione, poco perché la tabella resti
  piccola. Se un giorno serve di più, è una costante (`JobsLogRetentionJob.Kept`).

## 2. Un interruttore per ogni famiglia di chiamate a IVAO (elemento 6)

Il client IVAO aveva **un** circuit breaker per tutte le chiamate: una raffica di ricerche del tracker che falliscono (i controlli dei
rapporti degli eventi ne fanno molte, #322) lo apriva anche per `/v2/users/me` dell'accesso, e per la pausa dell'interruttore nessuno
entrava. Ora il gestore standard tiene **una pipeline per famiglia** (`SelectPipelineBy`, `IvaoServiceCollectionExtensions.CircuitOf`):
**sign-in** (`/v2/users/`), **network** (whazzup), **tracker**, **bookings** (prenotazioni ATC), **reference** (tutto il resto). Le
opzioni sono le stesse per tutte: 20 secondi per tentativo, 30 in tutto, campionamento su 40.

## 3. Le alternative

- **Registro**: non scrivere le righe «skipped» della coda senza SMTP. Toglie la metà delle righe di un'installazione senza posta, non
  quelle dei moduli; e il registro direbbe meno. Scartata. Una conservazione diversa per ogni job: più codice per
  nessuna domanda che oggi qualcuno faccia. Scartata.
- **Interruttore**: un client tipizzato per famiglia. Più registrazioni e più codice per lo stesso effetto. Scartata.

## 4. Due limiti noti della sincronizzazione (elemento 3, revisione della #422)

Non decisioni, ma da sapere, scritti qui perché l'elemento 3 non ha una nota sua:

- **Una chiave ripetuta con la seconda copia troppo lunga** perde anche la riga che la prima copia aveva aggiunto: `FitsOrLeftOut`
  stacca la riga, non la copia. È raro (IVAO dovrebbe ripetere la chiave e cambiarne il testo nella stessa risposta), e costa solo quella
  riga fino alla sincronizzazione dopo.
- **Se un giorno `/v2/centers` risponde a pagine** per una divisione grande, la guardia sulle pagine (`IvaoApiClient.Items`) prende la
  prima pagina per nessuna risposta e la sincronizzazione dei centri non fa più niente: un'installazione nuova resterebbe senza FIR, dove
  prima aveva almeno la prima pagina. Il rimedio, quel giorno, è leggere le pagine come fa `GetFrasAsync`. Che `/v2/centers` non pagini
  non è verificato contro IVAO.

## Da portare nel piano

- **§5.2** («Job»): la riga sul registro — `hub_jobs_log` tiene 30 giorni, l'ultimo giro finito e l'ultimo riuscito di ogni job, con il job del nucleo
  `jobs-log-retention` alle 04:30.
- **§10** (il paragrafo su `IvaoApiClient`, «circuit breaker»): **dalla correzione del lotto 4** l'interruttore è uno per famiglia di
  chiamate (accesso, rete, tracker, prenotazioni, dati di riferimento), con le stesse opzioni.
