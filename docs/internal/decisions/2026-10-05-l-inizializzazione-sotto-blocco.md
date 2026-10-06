# L'inizializzazione sotto blocco

**Data:** 5 ottobre 2026
**Stato:** **decisa**: codice di una decisione già presa da Carmine, versione **0.6.1**; la domanda nuova (§1) ha **la risposta di
Carmine** (6 ottobre 2026, in chat al master, che l'ha pubblicata sulla #218 su sua istruzione: [risposta][ok218]): **va avanti**.
- La decisione: issue [#203](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/203) di `dalberone`; Carmine, 5 ottobre
  2026: **l'inizializzazione si serializza con un blocco del database** attorno a `InitialisationMarker.RunAsync`
  (<https://github.com/SkyMistery/Ivao-Italy-Hub/issues/203#issuecomment-5996021026>). Scartata la strada «ogni seeder
  legge la chiave duplicata come già seminato». La stessa risposta lascia a questa nota i dettagli: il nome del blocco, il
  tempo d'attesa, che cosa fa un avvio che non lo ottiene, la connessione che lo tiene.

Versione **0.6.1**, PATCH: una correzione, nessuna migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il marcatore d'inizializzazione
(`2026-09-28-il-marcatore-d-inizializzazione.md`, `2026-09-29-il-marcatore-che-non-si-scrive.md`). Nessun meccanismo
nuovo, nessuna tabella. Cambio del nucleo, nella sua PR.

## 1. La domanda a Carmine

**Un avvio che non ottiene il blocco entro il tempo d'attesa: va avanti lo stesso, o si ferma?**

- **Raccomandato, ed è il codice di questa PR: va avanti**, inizializza senza blocco come faceva ogni avvio fino alla
  0.6.0, con un avviso nel log e la frase `without the initialisation lock (…)` nella riga `START` di `starts.txt`.
  È la regola già decisa per il marcatore («riprovare e avvisare», 29 set 2026): un'ottimizzazione o una protezione non
  deve mai essere la cosa che ferma il sito. Il caso peggiore è quello di oggi: due processi che inizializzano insieme, e
  uno dei due può fermarsi sulla chiave duplicata.
- **L'alternativa: fermarsi** con un errore che lo dice. Toglie del tutto la gara, ma un processo rimasto appeso con il
  blocco in mano (o un database che non risponde a `GET_LOCK`) terrebbe giù il sito finché qualcuno non lo spegne: un
  guasto raro diventa un sito fermo. Non la raccomando.
- **La risposta** (Carmine, 6 ottobre 2026, [sulla #218][ok218]): **va avanti**, come raccomandato; l'attesa resta di 30 secondi. Una
  protezione non è mai la cosa che ferma il sito, come per il marcatore.

[ok218]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/218#issuecomment-6012204871
- **Il tempo d'attesa: 30 secondi** (§4). Se la risposta è «fermarsi», o un altro tempo, è una costante e tre righe.

## 2. Il problema

Due processi dell'hub che partono nello stesso secondo su un database senza un segno valido leggono tutti e due «nessun
segno» e inizializzano tutti e due. La nota del marcatore lo accettava («ogni passo è applicato una volta sola, rifarlo
costa solo tempo»), ma «una volta sola» è vero per un processo **dopo** l'altro, non per due **insieme**:
`ContentSeeder.SeedCalendarKindsAsync` legge in tutti e due che il tipo manca, lo aggiunge in tutti e due, e il secondo si
ferma all'avvio su `Duplicate entry 'rfe' for key 'ix_cms_calendar_kinds_key'` (la issue, banco di `dalberone`, 30 set
2026). Lo stesso vale per ogni seeder che inserisce righe con una chiave unica: modelli, pagine, cruscotti, permessi delle
posizioni, e i superadmin al primo avvio (§6). In produzione può capitare al primo avvio dopo una consegna che semina
qualcosa di nuovo, perché Passenger a volte tiene due processi (#165).

## 3. Il meccanismo

`src/IvaoHub.Core/Services/InitialisationLock.cs` (nuovo), usato da `InitialisationMarker.RunAsync`.

- **`GET_LOCK` / `RELEASE_LOCK` di MariaDB**, il blocco con un nome. Non tocca tabelle, non entra nelle transazioni, e chi
  aspetta aspetta sul server: nessun giro di tentativi.
- **Solo un avvio che ha da inizializzare lo chiede.** `RunAsync` legge il segno come prima; se combacia, l'avvio salta
  tutto e **non apre niente**: il risveglio dopo una pausa, che è quello che il visitatore paga, costa come prima.
- **Il segno si rilegge dopo aver preso il blocco.** Quello letto prima è vecchio: mentre l'avvio aspettava, un altro può
  aver fatto tutto. Se ora combacia, l'avvio salta tutto come un avvio successivo (`Skipped`, con il tempo d'attesa); se
  no, inizializza, scrive il segno e solo allora rilascia. Chi aspetta dietro trova quindi il segno.
- **La connessione.** `GET_LOCK` è della **connessione**: serve una connessione tenuta aperta per tutta
  l'inizializzazione, che non sia una di quelle che migrazioni e seeder prendono e rendono al pool. È una
  `MySqlConnection` sua, aperta con la stessa stringa del contesto ma **fuori dal pool** (`Pooling=false`):
  - chiuderla **chiude la sessione**, e il server rilascia il blocco qualunque cosa sia successa. Una connessione del
    pool, resa, resterebbe viva con il blocco in mano se `RELEASE_LOCK` fallisse: MySqlConnector la azzera solo quando
    qualcuno la riprende;
  - un processo che muore si porta via la connessione: **il blocco non sopravvive a chi lo tiene**. Niente righe da
    ripulire, niente scadenze da indovinare (è la ragione per non farlo con una riga di `hub_division_settings`);
  - **il tetto del pool (≤ 15, `CLAUDE.md` §6)**: è una connessione in più oltre il pool, **solo** durante
    un'inizializzazione completa, quando il processo non serve ancora richieste e il pool ne usa una o due. Il tetto dei
    15 è per processo, e due processi insieme ne avevano già due: questa non cambia l'ordine di grandezza.
- **Il nome: `hub-init:<nome del database>`**, per esempio `hub-init:ivaohub`. Il nome di un blocco è **del server**, e il
  server non isola i database (`CLAUDE.md` §6): senza il database nel nome, due installazioni sullo stesso server (quella
  vera e quella di prova, o l'hub di un'altra divisione) si aspetterebbero a vicenda. Il nome del database lo dice la
  connessione aperta (`connection.Database`), non una configurazione; niente codice di divisione, niente dominio.
  - MariaDB 11.4.10 accetta nomi fino a **192 byte** e oltre risponde con un errore (provato: 192 sì, 193 `ERROR 1059`).
    Il nome è tenuto entro **64 caratteri**, che ci stanno comunque; un database con un nome più lungo di 55 caratteri
    prende al suo posto un pezzo dell'hash SHA-256 del nome. Test unitario.
  - I nomi dei blocchi **non distinguono maiuscole e minuscole**: due database `Hub` e `hub` sullo stesso server
    condividerebbero il blocco. Costa un'attesa in più, mai un errore.

## 4. Il tempo d'attesa, e l'avvio che non ottiene il blocco

- **30 secondi** (`InitialisationLock.DefaultWait`). Un'inizializzazione completa sul server è dell'ordine di 1–2 s (nota
  del marcatore, §5); 30 s sono molte volte tanto, e stanno dentro il tempo che Passenger dà a un processo per partire
  (90 s di default — ⚠️ il valore del server non l'ho letto). Il comando ha un timeout suo di 15 s più lungo dell'attesa,
  perché non si arrenda prima che il database abbia risposto (il default di MySqlConnector è proprio 30 s).
- **Un avvio che non ottiene il blocco va avanti senza** (la domanda del §1). «Non ottenuto» è una di due cose, e in
  nessuna delle due l'avvio fallisce:
  - **l'attesa è finita** (`GET_LOCK` risponde 0): `not free after 30000 ms`;
  - **il blocco non si è potuto chiedere**: la connessione non si apre, `GET_LOCK` risponde `NULL` o un errore:
    `could not be asked for`. Il motivo vero è nel log (un avviso con l'eccezione), **non** in `starts.txt`, che non deve
    contenere niente che venga da un messaggio del database.
- **La cancellazione dell'avvio si propaga** come prima: un avvio fermato mentre aspetta non inizializza.
- Senza blocco restano vive le difese di prima: la scrittura del segno che riprova su chiave duplicata e deadlock (nota
  del 29 set). Non sono state tolte.

## 5. Se la connessione del blocco cade a metà

Il server rilascia il blocco quando la sessione finisce. Chi lo teneva **non se ne accorge** (non usa quella connessione
mentre inizializza) e finisce il suo lavoro; chi aspettava lo prende, rilegge il segno, non lo trova ancora e inizializza
anche lui: per quel caso si torna a oggi, due insieme. Alla fine `RELEASE_LOCK` non risponde 1 (o lancia), e il primo
scrive un avviso: «was no longer held when the initialisation ended». Non fallisce l'avvio.
- **Non ho aggiunto un controllo periodico** (un ping, `IS_USED_LOCK` prima di ogni passo): servirebbe a fermare
  un'inizializzazione a metà, che è peggio di due insieme, e una connessione ferma per un paio di secondi non cade (il
  `wait_timeout` di MariaDB è 28800 s di default; ⚠️ quello del server non l'ho letto, ma dovrebbe essere sotto i 2 s per
  contare).
- **Un'inizializzazione che fallisce** (un passo lancia): il blocco si rilascia, il segno non è scritto, e chi aspettava
  inizializza lui. Test.

## 6. I superadmin: dentro il blocco quando si inizializza, come prima quando si salta

`SuperadminService.BootstrapAsync` gira a ogni avvio (decisione di Carmine sulla #175). Al **primo avvio di
un'installazione** scrive in `hub_users` i superadmin di `division.json` e la riga con l'hash dell'insieme: due processi
insieme li scriverebbero due volte, la stessa gara della issue su un'altra chiave. Quindi in `HubPipeline.InitializeAsync`:

- **un avvio che inizializza** lo esegue **come ultimo passo dell'inizializzazione**: dentro il blocco e **prima** del
  segno. Chi aspetta dietro, e chi arriva dopo e trova il segno, trova i superadmin già scritti;
- **un avvio che salta** (segno valido, o trovato dopo l'attesa) lo esegue **dopo**, fuori dal blocco, come oggi. Gira
  quindi sempre, una volta per avvio.
- **Che cosa cambia**: in `starts.txt`, su un avvio completo, il passo `superadmins` viene prima di `marker written`; e un
  controllo dei superadmin che fallisce su un avvio completo lascia il segno non scritto (l'avvio falliva già: ora quello
  dopo rifà anche l'inizializzazione, che costa solo tempo).
- **Che cosa resta scoperto**: due risvegli insieme (segno valido, nessun blocco) su un database a cui qualcuno ha tolto
  **a mano** tutti i superadmin **e** le loro righe di `hub_users`. Per coprirlo ogni avvio dovrebbe prendere il blocco,
  cioè aprire una connessione in più a ogni risveglio: no.

## 7. Che cosa dice `starts.txt`

`startup.txt` non cambia: descrive l'installazione, non l'avvio. In `starts.txt`, nella riga `START`:

```
… initialisation full: another build (the marker is of 0.6.0+78df526)  …  steps ms: …, marker 280, initialisation lock 20, marker read again 3, migrations 26, …, superadmins 20, marker written 83, …
… initialisation skipped (marker of 0.6.1+abc1234, 2026-10-05 16:40:02Z): migrations, module migrations, position grants, content; waited 1840 ms for the initialisation lock  …  steps ms: …, marker 280, initialisation lock 1840, marker read again 3, superadmins 60, …
… initialisation full: another build (the marker is of 0.6.0+78df526); without the initialisation lock (not free after 30000 ms)  …
```

- **Un avvio che ha preso il blocco e ha inizializzato** non dice niente di più nelle parole: il tempo è il passo
  `initialisation lock` fra gli `steps ms` (la connessione, più l'attesa se c'era qualcuno davanti), seguito da
  `marker read again`.
- **Un avvio che ha aspettato e poi saltato** lo dice: `; waited N ms for the initialisation lock`. È la riga che spiega
  un avvio lento senza lavoro.
- **Un avvio senza blocco** lo dice e dice quale dei due casi del §4.
- Un risveglio uguale (segno valido alla prima lettura) non ha né il passo né la frase: non ha chiesto niente.

## 8. Le alternative scartate

- **Ogni seeder legge la chiave duplicata come «già seminato»**: scartata da Carmine. Un posto per seeder, da ricordare a
  ogni seeder nuovo, e le migrazioni resterebbero fuori.
- **Una riga di blocco in `hub_division_settings`** (con chi la tiene e fino a quando): sopravvive a un processo morto, e
  allora serve una scadenza, cioè un tempo da indovinare. E al primo avvio la tabella non c'è.
- **`SELECT … FOR UPDATE` su una riga**: è una transazione aperta per tutta l'inizializzazione, sulla tabella che i
  seeder scrivono; e al primo avvio la tabella non c'è.
- **Il blocco su una connessione del pool, o su quella del contesto**: §3.
- **Prendere il blocco a ogni avvio**: una connessione in più su ogni risveglio, per coprire il caso del §6.

## 9. Verificato

- **`InitialisationMarkerTests`** (MariaDB 11.4.10 vero, Testcontainers):
  - `OfTwoProcessesStartingTogetherOneSeedsAndTheOtherWaitsAndSkips`: la gara della issue. Tolto il tipo `exam` e il
    segno, due inizializzazioni concorrenti con il `ContentSeeder` vero. **Senza il blocco** (mutazione: `SELECT 1` al
    posto di `GET_LOCK`) cade con `Duplicate entry 'exam' for key 'ix_cms_calendar_kinds_key'`, l'errore della issue;
    **con il blocco** una semina, l'altra aspetta, trova il segno e salta, e la riga è una.
  - `AStartThatWaitedBehindAFailedInitialisationDoesItItself`: il primo fallisce con il secondo in attesa; il secondo
    inizializza.
  - `AStartThatCannotTakeTheLockInitialisesWithoutItAndSaysSo`: il blocco tenuto da un'altra connessione, attesa di
    300 ms; tutti e due inizializzano, tutti e due scrivono il segno, e lo dicono. È il test di prima
    (`TwoProcessesStartingTogetherBothInitialiseAndBothWriteTheMark`), che ora descrive solo questo caso: il
    comportamento che provava è cambiato per decisione.
  - `ALockThatCannotBeAskedForIsNotHeldAndDoesNotThrow`: un indirizzo a cui non risponde nessuno.
- **Test unitari**: il nome del blocco; le parole di `starts.txt` nei tre casi.
- **A mano**, su un contenitore `mariadb:11.4.10`: il limite di 192 byte del nome, l'attesa frazionaria (`0.3`).

## 10. Che cosa non è verificato

- **Due processi veri sul server** (Passenger dopo una consegna): provato con due inizializzazioni concorrenti nello
  stesso processo di test, ognuna con il suo contesto e la sua connessione di blocco; il banco a due processi di
  `dalberone` no.
- **Il costo sul server** del passo `initialisation lock` (una connessione nuova e una query, solo sugli avvii completi):
  non misurato da solo. Nel test, aspettare dietro un'inizializzazione vuota è costato 22 ms, connessione compresa; sul
  server lo dirà `starts.txt`.
- **`wait_timeout` e il tempo d'avvio di Passenger sul server**: non letti (§4, §5).
- **L'utente del database sul server può chiamare `GET_LOCK`**: non serve nessun privilegio, ma non l'ho provato lì. Se
  non potesse, l'avvio va avanti senza blocco e `starts.txt` lo dice.
- **Una connessione del blocco che cade a metà**: ragionato sul comportamento di MariaDB (§5), non riprodotto.

## Da portare nel piano

- **§11.3 punto 5** (il marcatore d'inizializzazione): un avvio che deve inizializzare prende prima il blocco
  `hub-init:<database>` (`GET_LOCK`, su una connessione sua fuori dal pool), **rilegge il segno** e solo allora
  inizializza; di due processi che partono insieme uno inizializza e l'altro aspetta e salta. Un risveglio uguale non
  chiede il blocco. Attesa di 30 s; **un blocco non ottenuto non ferma l'avvio** (la domanda del §1, con la risposta di
  Carmine). Corregge la frase «due processi insieme inizializzano tutti e due» della nota del marcatore (§2): ora vale
  solo senza blocco.
- **§11.3 punto 5**: i superadmin si controllano sempre a ogni avvio, ma un avvio che inizializza lo fa **dentro
  l'inizializzazione**, prima del segno (§6).
- **§11.3 punto 2**: in `starts.txt` i passi `initialisation lock` e `marker read again`, la frase
  `; waited N ms for the initialisation lock` su un avvio che ha aspettato e saltato, e
  `; without the initialisation lock (…)` su uno che ha inizializzato senza.
- **§2.5**, la riga di Passenger: con due processi insieme dopo una consegna ne inizializza uno solo (#203).
- **La domanda del §1**, con la risposta di Carmine.
- `docs/DEPLOYING.md` è aggiornato in questa PR.
