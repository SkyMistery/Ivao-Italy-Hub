# Il primo allineamento dei dati di riferimento sotto il blocco dell'inizializzazione

- **Data**: 18 novembre 2026
- **Stato**: la forma del blocco (§2) è scelta di una sessione di lavoro del maintainer (`fix/first-ref-sync-lock`), dentro il caso
  (b), e il revisore la legge come tale. La domanda del §4, che cambia **quando** i dati di riferimento ci sono la prima volta, è
  **decisa da Carmine**: «B, solo i centri», in chat al master il 18 novembre 2026, registrata sulla issue:
  <https://github.com/SkyMistery/Ivao-Italy-Hub/issues/325#issuecomment-6980597038>.
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Si estende un meccanismo che c'è, il blocco dell'inizializzazione
  (`InitialisationLock`, nota `2026-10-05-l-inizializzazione-sotto-blocco`), al passo dell'avvio che ne era rimasto fuori. Nessun
  blocco nuovo, nessuna configurazione nuova, nessun modulo tocca niente.
- **Da dove viene**: issue #325, punto 7 (lotto 4 della revisione del codice, nota `2026-10-20-la-revisione-del-codice`). La #422 l'ha
  lasciato apposta: prendere il blocco **del job** e saltare il download nel secondo processo lo farebbe partire senza FIR, e
  `FirDirectory` terrebbe l'insieme vuoto per sei ore.
- **Legata a**: `2026-09-28-l-avvio-a-freddo` (§4.3: il controllo dei dati di riferimento vuoti gira a ogni avvio, e se la prima
  sincronizzazione è fallita deve riprovare) e `2026-09-28-i-job-quando-passenger-spegne-l-hub` (Passenger spegne e riaccende spesso,
  a volte due processi insieme).

## 1. Che cosa c'era

Dopo l'inizializzazione, a ogni avvio, `HubPipeline.InitializeAsync` chiede se `ref_ivao_centers` ha un centro del paese della
divisione e, se no, lancia `RefDataSyncJob` dentro l'avvio. Fuori da ogni blocco: su un'installazione nuova due processi che partono
insieme (Passenger lo fa) scaricano **tutti e due** l'intera fotografia, circa 26 MB (aeroporti del mondo ~14 MB, posizioni ATC,
aerei, centri, FRA), e scrivono le stesse righe uno sopra l'altro.

## 2. Che cosa c'è ora

Una classe del nucleo, `FirstReferenceSync` (`Core/Ivao/`), con la regola dell'inizializzazione:

1. il controllo **prima**, una query come oggi: un avvio che trova i centri non chiede il blocco (è il caso di ogni avvio tranne il
   primo, e non paga niente di più);
2. se mancano, prende **il blocco dell'inizializzazione** (`hub-init:<database>`, `InitialisationLock`) con un'attesa sua di **65 s**
   (`FirstReferenceSync.DefaultLockWait`, sotto) e **rilegge** con il blocco in mano;
3. se un altro avvio li ha scritti mentre questo aspettava, va avanti senza scaricare; altrimenti scarica, tenendo il blocco fino alla
   fine perché chi aspetta trovi i centri;
4. **un blocco non preso** (l'attesa finita, il database che non risponde al blocco) **scarica come prima**: mai un avvio senza i
   suoi dati per colpa del blocco. È la stessa regola dell'inizializzazione.

- *Scelta*: **il blocco dell'inizializzazione, non quello del job** (`hub-job:<database>:ref-data-sync`). Il blocco del job non
  aspetta (un'esecuzione tenuta da un altro processo si salta), ed è proprio il salto che la #422 ha scartato. Quello
  dell'inizializzazione aspetta e rilegge: il secondo processo ha i centri esattamente quando li avrebbe avuti il primo.
- *Scelta*: **un download fallito non si eredita**. `RefDataSyncJob` non lancia mai: un'esecuzione fallita lascia la tabella vuota e
  la sua riga «failed» in `hub_jobs_log`. L'avvio che aspettava, con il blocco in mano, rilegge, non trova i centri e scarica lui,
  come farebbe l'avvio dopo.
- *Scelta*: **un download fallito non si dice riuscito** (rilievo 1 del master sulla PR). `RunCentresAsync` risponde quanti centri ha
  scritto, zero quando è fallito o IVAO non ne ha dati; `FirstReferenceSync` lo legge (`synchronise` risponde se ci sono centri) e
  in quel caso l'esito è **`NotSynced`**, non `Synced`. Prima l'avvio scriveva «synchronised» anche con IVAO spento, ed era proprio il
  caso in cui il file doveva dirlo.
- *Scelta*: **il passo di `starts.txt` dice chi ha pagato**, con i nomi che il codice scrive (`HubPipeline`): `reference data`
  quando i centri c'erano, `reference centres synchronised`, `reference centres synchronised without the initialisation lock`,
  `reference centres synchronised by another start`, `reference centres not synchronised`. Il punto 7 era «da provare»: sul server
  lo dice il file, con i millisecondi del passo. Gli avvisi del blocco nel log dicono che cosa l'avvio fa senza («this start runs the
  first reference synchronisation without it»), non più «initialises» (rilievo 3).
- *Scelta*: **l'attesa copre le due chiamate di chi scarica** (rilievo 2). Chi tiene il blocco prende prima il token, sul suo client
  resiliente, poi i centri: ognuna fino ai 30 s del totale del gestore standard di resilienza, quindi fino a circa **60 s**, non i 30
  s di una chiamata che la nota diceva. Con i 30 s dell'inizializzazione, un IVAO lento ma vivo (35 s, per dire) faceva arrendere chi
  aspettava, che scaricava una seconda volta: il doppio download che il blocco toglie. L'attesa è quindi **due totali più 5 s per le
  scritture, 65 s**, calcolata dal totale del gestore (`HttpStandardResilienceOptions`), non scritta a mano; un test controlla che
  copra le due chiamate e stia sotto i 90 s.
  - *Il conto con Passenger* (90 s di default): l'avvio a freddo misurato sul server è di 8–10 s (nota
    `2026-09-28-i-job-quando-passenger-spegne-l-hub`), quindi chi scarica arriva al massimo a circa 70 s. Chi aspetta finisce
    d'aspettare quando chi scarica ha finito: se ha trovato i centri, è dentro come lui.
  - *Il caso che non sta nei 90 s*, con l'attesa lunga come con quella corta: IVAO **appeso** per tutti e due. Chi aspetta, dietro un
    download fallito, scarica lui e si appende a sua volta (65 + 60 s; con 30 s era 30 + 60): Passenger lo ferma, la tabella resta
    vuota, e l'avvio dopo riprova, come prima di questa PR. L'attesa lunga non lo peggiora e toglie il doppio download quando IVAO
    risponde piano.
  - *Scartata*: chi ha aspettato dietro un download fallito non riprova e lascia i centri al job. Starebbe nei 90 s, ma `FirDirectory`
    terrebbe l'insieme vuoto per sei ore nel processo che non fa girare il job: è il salto che la #422 ha scartato.
- ⚠️ **Il nome del blocco è condiviso con l'inizializzazione** (rilievo 7). Un avvio che ha letto un segno non valido appena prima
  che un altro lo scrivesse può trovarsi ad aspettare `hub-init:<database>` dietro il download di quell'altro, fino a 65 s invece dei
  secondi di un'inizializzazione. La finestra è stretta (il segno si legge prima del blocco, e chi l'ha scritto ha appena lasciato il
  blocco) e i seeder sono idempotenti: un'attesa in più, mai un errore. Un nome a parte non serve: due blocchi diversi lascerebbero
  scaricare un avvio mentre l'altro inizializza ancora lo stesso database.

## 3. Le prove

`FirstReferenceSyncTests`, sul MariaDB vero e con il blocco vero; che cosa si controlla e che cosa si scarica sono del test (i centri
del database condiviso sono di tutte le classi della suite):

- due avvii insieme su una tabella vuota: un download solo, un avvio `Synced` e l'altro `AnotherStartSynced`;
- un avvio che trova i centri non chiede il blocco (tenuto da un altro, non aspetta) e non scarica;
- un avvio che ha aspettato dietro a un download fallito scarica lui, e quello fallito risponde `NotSynced`;
- **attraverso `FirstReferenceSync`** (la classe che l'avvio chiama, con il job vero sulle fixture), uno spazio aereo che il database
  non ha ancora fa scaricare i centri e **non** gli aeroporti: una riga `ref-data-centres` in più, nessuna `ref-data-sync`, gli
  aeroporti non toccati (rilievo 5: rimettere l'esecuzione intera in `FirstReferenceSync` fa diventare rosso questo test);
- l'attesa del blocco copre le due chiamate di chi scarica e sta sotto i 90 s;
- il download dell'avvio, con il job vero sulle fixture, scrive i centri e una riga `ref-data-centres`, e **nessuna** riga di
  `ref-data-sync`: gli aeroporti non si toccano e il job resta dovuto;
- con il blocco tenuto da un processo appeso, l'avvio aspetta, poi scarica senza (`SyncedWithoutTheLock`);
- l'avvio vero dell'hub, dopo il primo, trova lo spazio aereo e non fa niente (`Present`).

## 4. La domanda aperta: quanto può durare il primo allineamento dentro l'avvio

Il blocco toglie il doppio download, non l'attesa. Con IVAO che **non risponde** (non che rifiuta: un rifiuto torna subito), il primo
allineamento fa sette chiamate (token, centri, aeroporti, aerei, vocabolari del piano di volo, posizioni ATC, FRA), ognuna fino ai
30 s del totale di Polly: **fino a tre-quattro minuti**, oltre i 90 s di `passenger_start_timeout` predefinito. Passenger uccide il
processo, la tabella resta vuota, l'avvio dopo riprova: l'installazione nuova non parte finché IVAO resta appeso. Strade:

- **A.** Solo il blocco (questo codice). Il quando non cambia.
- **B.** Dentro l'avvio **solo i centri** (le FIR: sono il motivo per cui l'allineamento sta nell'avvio, riconoscere un posto di staff
  FIR al primo accesso), una chiamata dei dati dopo quella del token, al massimo 30 s ciascuna (§2); il resto lo fa il job `ref-data-sync`, che non ha mai girato e quindi è dovuto
  al primo giro dopo la pagina (nota `2026-11-14-il-primo-giro-dei-job-dopo-la-pagina`). Cambia il quando: aeroporti e posizioni
  arrivano qualche secondo dopo l'avvio invece che prima della prima richiesta.
- **C.** Tutto nell'avvio, con una scadenza (per esempio 45 s): tagliato, la riga diventa «failed» e l'avvio va avanti; ogni avvio
  dopo ripaga la scadenza finché IVAO non torna.

**Raccomandazione**: **B**, con il blocco di questa nota. **Decisa: B** (Carmine, [commento sulla #325][b]).

[b]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/325#issuecomment-6980597038

## 5. Che cosa fa B nel codice

- `RefDataSyncJob.RunCentresAsync`: **solo i centri** del paese della divisione, con la stessa lettura, gli stessi filtri e la stessa
  potatura dell'esecuzione intera; una riga in `hub_jobs_log` sotto un nome suo, **`ref-data-centres`**, con l'esito e il messaggio
  («N centre(s) for XX; the rest of the snapshot is the job's»), o «failed». La riga di un'esecuzione, la scrittura dell'esito, la
  cancellazione dell'insieme delle FIR in memoria e il fallimento sono **una funzione sola** (`RecordedAsync`) per le due esecuzioni,
  non una copia.
- *Scelta*: **un nome suo, non `ref-data-sync`**. La guardia dei job legge l'ultima esecuzione di un job dalle sue righe
  (`ScheduledJobs`): con il nome del job, l'avvio farebbe credere che l'allineamento intero ci sia stato, e aeroporti, aerei,
  posizioni e FRA aspetterebbero la notte. Con un nome suo il job **non ha mai girato, quindi è dovuto** (`JobSchedule`), e il primo
  giro dopo la pagina di chi ha svegliato l'hub (fra 5 e 30 s dall'avvio, nota `2026-11-14-il-primo-giro-dei-job-dopo-la-pagina`)
  scarica il resto, sotto il blocco del job come ogni esecuzione.
- `FirstReferenceSync` chiama `RunCentresAsync` e legge quanti centri ha scritto (§2, `NotSynced`); il resto del §2 non cambia.
- ⚠️ **Che cosa cambia per un'installazione nuova vera** (richiesta del master sulla PR, da scrivere e non da scoprire):
  - **fino alla fine del primo giro del job `ref-data-sync`**, un tour o un evento che nomina un aeroporto è rifiutato con
    `airportUnknown` (`events:errors.airportUnknown`, e il rifiuto equivalente dei tour), il briefing ATC non trova le posizioni,
    l'aereo di un volo non trova il suo tipo. I centri, e quindi le FIR e i posti di staff FIR all'accesso, ci sono dall'avvio;
  - **il giro parte da solo, anche senza visite**, ma solo **mentre il processo vive**: `JobCatchUp` fa il primo controllo non prima
    di 5 s dall'avvio, appena nessuna richiesta è in corso da 3 s e comunque entro 30 s (nota
    `2026-11-14-il-primo-giro-dei-job-dopo-la-pagina`). Un processo nasce solo da una richiesta (Passenger), quindi «senza visite» vuol
    dire: dopo la richiesta che lo ha svegliato, anche se non ne arrivano altre. Su un'installazione nuova il giro trova dovuti
    **tutti** i job (nessuno ha mai girato) e li fa **uno dopo l'altro**: `ref-data-sync` può non essere il primo, e scarica circa
    14 MB di aeroporti;
  - se Passenger ferma il processo prima che il job finisca, la sua riga resta «running», che la guardia considera un'esecuzione mai
    finita: al risveglio dopo il job è ancora dovuto e il giro lo riprende. Se invece il job **finisce male** (IVAO appeso: «failed»;
    i centri sì e gli aeroporti no: «partial»), è un'esecuzione finita e aspetta la sua prossima occorrenza, la notte, come ogni job:
    fino ad allora gli aeroporti mancano. Né un riavvio né l'indirizzo dei job pianificati (`/api/jobs/run`, che fa girare solo i job
    dovuti) lo anticipano: è il comportamento che ogni job ha già (nota `2026-10-09-i-job-che-recuperano`), e qui lo si scrive perché
    un'installazione nuova lo paga senza fotografia di ieri da tenere;
  - è la scelta di Carmine («B, solo i centri»): il prezzo sono questi secondi, o questa notte, su un'installazione che nessuno sta
    ancora usando, contro un avvio che con IVAO appeso non arrivava mai alla fine.
- **Da dire nelle note di rilascio** (le scrive il master): «On a fresh installation the start downloads only the FIR centres; airports,
  aircraft types, ATC positions and FRAs arrive with the first round of the `ref-data-sync` job, a few seconds after the first page.
  Until then a tour or an event naming an airport is refused as an unknown airport; if that first run fails (IVAO not answering), they
  arrive with the job's next nightly run. Installations that already have the reference data see no change.»
- ⚠️ **Il banco end to end lo ha pagato per primo.** In CI parte su un database vuoto e le sue prove scrivono subito eventi e tour
  fra Roma e Milano: senza gli aeroporti, 36 prove su 86 rifiutate con `events:errors.airportUnknown` (prima esecuzione della CI di
  questa PR). Il banco ora chiede il job prima della prima prova, come una prova chiede qualunque job (`runJob`, nota
  `2026-10-18-il-banco-fa-girare-un-job`): `web/e2e/full/global-setup.ts`, nel `globalSetup` di `playwright.full.config.ts`. Nessuna
  eccezione nell'avvio per il banco: il banco fa quello che farebbe il primo giro dei job, prima.
- ⚠️ Un caso limite, non coperto: se il download dei centri di un avvio fallisce e un altro processo, nello stesso momento, ha già
  cominciato il job intero, i due scrivono gli stessi centri. Il secondo che salva trova la chiave duplicata e registra «failed»; il
  prossimo avvio o la prossima occorrenza rimette tutto in ordine. Per arrivarci servono un'installazione nuova, IVAO che risponde a
  uno e non all'altro e due processi: non vale un meccanismo.

## Da portare nel piano

- §11.3 punto 5, dove dice che «il controllo dei dati di riferimento» gira a ogni avvio e del blocco `hub-init:<database>`: il primo
  allineamento gira **sotto lo stesso blocco**, con la stessa regola (controllo, blocco, controllo di nuovo; un blocco non preso scarica
  come prima, mai un avvio senza i suoi dati) e un'attesa sua di 65 s, le due chiamate di chi scarica; il passo di `starts.txt` dice
  quale avvio ha scaricato, o che il download non ha portato centri.
- §11.3 punto 5, sullo stesso passo: **dentro l'avvio solo i centri** (`ref-data-centres` in `hub_jobs_log`); il resto della
  fotografia lo prende il job `ref-data-sync` al primo giro dopo l'avvio (Carmine, «B, solo i centri»). Dove la nota
  `2026-09-28-l-avvio-a-freddo` §4.3 dice che il controllo dei dati vuoti «se la prima sincronizzazione è fallita deve riprovare»: vale
  per i centri; il resto riprova con il job.
- Issue #325, punto 7: chiuso (il doppio download con il blocco, l'attesa con IVAO appeso con B).
