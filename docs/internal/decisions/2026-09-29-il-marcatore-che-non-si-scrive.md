# Il marcatore che non si scrive non ferma l'avvio

**Data:** 29 settembre 2026
**Stato:** **Proposta**, in attesa della risposta di Carmine (§6). Versione **0.3.1**, PATCH: solo una correzione, nessuna
migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si corregge il marcatore d'inizializzazione
(`2026-09-28-il-marcatore-d-inizializzazione.md`), nessun meccanismo nuovo. Cambio del nucleo, nella sua PR.

## 1. Il problema

dalberone, sulla #146 (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5887857247>): il test
`InitialisationMarkerTests.TwoProcessesStartingTogetherBothInitialiseAndBothWriteTheMark` è fallito una volta con
«Deadlock found when trying to get lock» in `InitialisationMarker.WriteAsync`, mentre altri due processi di test caricavano
la macchina; da solo passa.

Il test era il sintomo. Il difetto è nel codice: **un deadlock nella scrittura del marcatore faceva fallire l'avvio**.
`WriteAsync` rileggeva e riscriveva una volta sola su `DbUpdateException`, e un deadlock non arriva così: EF lo riporta come
`InvalidOperationException` («likely due to a transient failure»), con il `MySqlException` due livelli sotto. Il `catch` non
lo prendeva neanche al primo tentativo; l'eccezione usciva da `RunAsync`, e `HubPipeline` non la ferma: avvio fallito, per
una riga che è solo un'ottimizzazione.

## 2. Riprodotto, e perché succede

- **Riprodotto** con un test di stress temporaneo (non committato): due, tre e quattro scritture concorrenti del marcatore,
  ognuna dopo aver cancellato la riga. Senza correzione: **7–10 fallimenti su 300 giri** con due scrittori, 9–22 su 200 con
  tre o quattro, tutti deadlock. Con la correzione: **0 su 700 giri, tre volte**.
- **Il meccanismo**, da `SHOW ENGINE INNODB STATUS`: le due transazioni aspettano un lock X sul record `startup.initialised`
  e ognuna tiene un lock S sullo stesso record, che è **marcato come cancellato e non ancora eliminato dal purge**
  (`info bits 32`). Chi inserisce una chiave uguale a un record così ne prende prima un S (il controllo del duplicato) e poi
  chiede l'X per riscriverlo: due che lo fanno insieme si bloccano a vicenda, e MariaDB ne annulla uno. Sotto carico il
  purge è più lento, e il record cancellato resta lì più a lungo: per questo il test cadeva solo con la macchina carica.
- **Nel test** la riga la cancella `ForgetAsync`, subito prima. **Sul server** una riga appena cancellata c'è quando qualcuno
  segue il rimedio della nota del marcatore (§3: «cancellare anche la riga `startup.initialised`») e poi partono due processi
  insieme, come Passenger fa dopo un caricamento. Raro; ma il punto è che **qualunque** errore nella scrittura del marcatore
  non deve fermare l'avvio.

## 3. La correzione

`src/IvaoHub.Core/Services/InitialisationMarker.cs`, `WriteAsync`:

- **Un altro scrittore si riprova**: chiave duplicata (1062) o deadlock (1213), cercati lungo tutta la catena delle
  eccezioni, come fa già `ReviewEndpoints.IsDeadlock` nei Tour. Fino a **5 tentativi**, con il tracker svuotato (il database
  ha già annullato la transazione) e una pausa breve e casuale (10–50 ms × tentativo), perché due perdenti non si
  reincontrino. Ogni conflitto vuol dire che un altro ha vinto: con due processi basta un secondo tentativo.
- **Ogni altro errore del database non si riprova e non ferma l'avvio**: un avviso nel log («the initialisation succeeded,
  and the next start does it again») e il passo `marker not written` in `starts.txt` al posto di `marker written`. Non
  riprovo un timeout d'attesa del lock (ha già aspettato `innodb_lock_wait_timeout`, 50 s) né una connessione caduta
  (l'avvio dopo riprova comunque).
- **La cancellazione dell'avvio si propaga** come prima: un avvio fermato non scrive e non avvisa.
- **Resta la regola** «il marcatore si scrive solo dopo che tutti i passi sono riusciti»: un passo che fallisce fa ancora
  fallire l'avvio, e il marcatore resta quello di prima (il test `TheMarkIsWrittenOnlyAfterTheInitialisationSucceeded` non è
  cambiato).

## 4. Le alternative scartate

- **Un solo `INSERT … ON DUPLICATE KEY UPDATE`**: toglierebbe la corsa alla radice (prende subito l'X, niente S prima) ed è
  una riga. Ma è SQL scritto a mano, e passa **fuori dall'interceptor**: `DivisionSetting` è `[Audited]`, e la riga d'audit
  andrebbe persa o scritta a mano, cioè un secondo posto che conosce la forma dell'audit (il commento di
  `ModuleRegistry.SetMaintenanceAsync` lo dice per le altre impostazioni). La correzione più piccola che toglie il danno è
  riprovare, non cambiare il modo di scrivere.
- **`EnableRetryOnFailure` su `UseMySql`**: riproverebbe da sé ogni errore transitorio, in tutto l'hub. Cambia il
  comportamento di ogni contesto e obbliga a chiudere in una strategia d'esecuzione ogni transazione aperta a mano: un
  cambio grande per una riga. No.
- **Lasciare l'errore e correggere solo il test**: il test era giusto, era il codice a non reggere.

## 5. Verificato

- **Due test nuovi** in `InitialisationMarkerTests`, tutti e due rossi senza la correzione (tolta e rimessa, tre volte):
  - `ADeadlockWhileWritingTheMarkIsTriedAgain`: il deadlock fatto apposta, **deterministico**, nella forma del §2: uno
    snapshot aperto tiene la riga cancellata, un'altra transazione (resa più pesante con 20 righe sue, perché MariaDB annulla
    la più leggera) la legge con `LOCK IN SHARE MODE`; il marcatore inserisce e aspetta; l'altra inserisce e il ciclo si
    chiude. Controlla che `Innodb_deadlocks` sia salito di uno e che il marcatore, al secondo tentativo, sia scritto. Senza
    correzione: l'`InvalidOperationException` del rapporto di dalberone.
  - `AMarkThatCannotBeWrittenDoesNotFailTheStart`: un host vero con un interceptor che rifiuta ogni scrittura del marcatore.
    L'avvio finisce («initialisation full: no marker», passo `marker not written`), la scrittura è tentata una volta sola
    (non è un altro scrittore), il marcatore non c'è, l'avvio dopo rifà tutto.
- Il test vecchio `TwoProcessesStarting…`, che senza correzione è caduto anche in locale in uno dei giri: verde in tutti i
  giri con la correzione.
- ⚠️ **Una trappola pagata** (in `CONTRIBUTING.md`): `information_schema.INNODB_TRX` si rinnova solo se nessuno l'ha letto
  negli ultimi 100 ms. Interrogato ogni 50 ms, il test non vedeva mai l'attesa che c'era.

## 6. La domanda a Carmine

**Va bene riprovare e poi avvisare, o preferisci l'upsert?** Raccomandato: **riprovare e avvisare** (§3), perché tiene la
riga d'audit e cambia solo `WriteAsync`. L'upsert (§4) toglierebbe la corsa invece di assorbirla, al prezzo di SQL a mano
fuori dall'interceptor.

## 7. Che cosa non è verificato

- **Due processi veri sul server** (Passenger dopo un caricamento): provato solo con scritture concorrenti nello stesso
  processo di test, su connessioni diverse, e con il deadlock fatto apposta.
- Che sul server il deadlock sia mai successo: `starts.txt` non l'ha mai mostrato. Da qui in poi lo direbbe il passo
  `marker not written`, o nessun segno se il secondo tentativo riesce.

## Da portare nel piano

- **§11.3 punto 5** (il marcatore d'inizializzazione): la scrittura del marcatore **non fa mai fallire l'avvio**. Un altro
  scrittore (chiave duplicata, deadlock) si riprova fino a cinque volte; ogni altro errore è un avviso, e l'avvio dopo rifà
  tutto. Corregge la frase «l'inserimento doppio dà un errore di chiave duplicata al secondo, che rilegge e aggiorna» della
  nota del marcatore (§2): può essere anche un deadlock, e da solo non bastava.
- **§11.3 punto 2**: in `starts.txt` il passo `marker not written` accanto a `marker written`.
- **La domanda del §6**, con la risposta di Carmine.
- `docs/DEPLOYING.md` e `CONTRIBUTING.md` sono aggiornati in questa PR.
