# La conservazione di M4b, le prenotazioni della rete accanto al roster, il giro completo (E15b)

**Data:** 17 ottobre 2026 — fase E15b di M4 (M4b, l'ultima), PR #301, in coda dopo la #293 di E14b e la #300 di E10w
**Stato:** **decisa**. Le **letture 1–8 del §2 sono accettate come sono scritte** da Carmine il 19 ottobre 2026, in chat al master, che [l'ha
pubblicato sulla #301][a301] su sua istruzione («sì, per tutte») — la 6 compresa: le prenotazioni ATC della rete accanto al roster,
l'ottava lettura scritta a mano dell'ATC, accettata come le sette prima di lei. **Le due domande del §4 sono decise** da Carmine il 18
ottobre 2026, in chat al master, che le ha pubblicate sull'issue #294 su sua istruzione: la 2 **(B)** ([la risposta][a294]) — prima una
piccola fase del nucleo per il banco, **E10w** (`m4/e10w-bench-runs-a-job`), con `/e2e/jobs/{name}` solo sul banco e una fixture di sessioni
di un personaggio del banco spostata sul giorno chiesto; poi il giro di E15b è intero, segnale compreso —; la 1 **(A)** ([la
risposta][a294b]) — le disponibilità se ne vanno con l'evento finito, pubblicato o no il roster (`EventRetention.AvailabilitiesDue`), com'è
nel codice.

[q294]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/294
[q294a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/294#issuecomment-6237599717
[a294]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/294#issuecomment-6246961879
[a294b]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/294#issuecomment-6253529817
[a301]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/301#issuecomment-6256206322
**Regola applicata:** `CLAUDE.md` §5. Che prenotazioni e PIREP dei piloti si cancellino dopo `pilotRetentionMonths`, le disponibilità «a
roster pubblicato e a evento finito», e turni, esiti, PIREP ATC e cessioni restino per sempre; che lo faccia `events-retention` una volta al
mese, decidendo dalle date; che le prenotazioni ATC della rete si leggano **accanto al roster, alla richiesta**, senza job e senza cache, al
più sette giorni per richiesta: sono **decisi** (design `09-design-m4.md` §4.4, §9.1, §11, §17.2 n.3; note
`2026-09-29-i-dati-dei-membri-negli-eventi` §2.3 e §4, `2026-09-29-il-roster-atc` §2.9, `2026-09-30-le-prenotazioni-atc-della-rete` e la
risposta di Carmine sulla #207). Questa nota registra le **letture** dove il design non dice — casi **(a)** e **(b)**: nessuna tabella e
nessuna migrazione, un endpoint di lettura scritto a mano, un passo in più di un job che c'è (E17) — e **due domande**.

## 1. Che cosa serviva decidere

Il design dice che cosa si cancella e quando, in una riga per tipo (§11), e che le prenotazioni della rete stanno accanto al roster. Non
dice: che cosa vuol dire «a roster pubblicato» per un evento il cui roster non è mai stato pubblicato; quale confine tiene il registro dei
piloti quando le righe se ne vanno; che cosa restano le statistiche dopo, e che cosa fa «Ricalcola»; che cosa succede ai segnali di award
dei PIREP cancellati; come la pagina mostra le prenotazioni della rete, e quanto aspetta. E il giro completo di M4b (§15) chiede al banco
due cose che il banco non sa fare.

## 2. Le letture

1. **Le disponibilità se ne vanno con l'evento finito** (`EventRetention.AvailabilitiesDue`: `EventState.IsOver`), il primo giro del mese
   dopo la fine. Un evento finito ha il roster pubblicato, o non lo avrà più: `events-roster` non pubblica un evento finito (E12), e dopo la
   fine niente le legge — i turni sono il roster, e le statistiche tengono quante erano. **È la domanda 1** (§4): letto alla lettera, un evento
   annullato prima della pubblicazione le terrebbe per sempre.
2. **Prenotazioni e PIREP dei piloti se ne vanno quando l'evento è finito da più di `pilotRetentionMonths`**, con **lo stesso confine** che il
   registro dei piloti conta (`EventRetention.PilotsKeptSince`, scritto una volta e usato da tutti e due: il registro conta gli eventi finiti
   da allora, la conservazione cancella quelli finiti prima). Così una prenotazione esce dal registro quando esce dal database, e il registro
   conta uguale prima e dopo un giro (E13b, «Che cosa deve sapere la fase dopo»). Prima il registro scriveva `now.AddMonths(-months)` da sé.
3. **Le statistiche si scrivono prima della cancellazione**, dalle righe, con `EventStatistics.WriteAsync` sotto il blocco della riga
   dell'evento, nella stessa transazione (la regola di E13a: chi scrive le statistiche dopo di lei passa da lì); dopo, **«Ricalcola» tiene i
   numeri** che la conservazione ha scritto quando le righe non ci sono più: oltre `pilotRetentionMonths` e senza prenotazioni né PIREP pilota,
   i prenotati, volati e non volati, **i PIREP mandati e accettati e i segnali** restano com'erano; oltre la fine e senza disponibilità, il loro
   numero. ⚠️ **Il limite**: i PIREP mandati e accettati sono un numero solo, piloti e controllori insieme; dopo la conservazione un PIREP ATC
   deciso di nuovo — dopo due anni — non sposta più i due numeri. Contarli divisi vorrebbe due colonne nuove di `evt_event_stats`, nata intera
   in E16: non vale una migrazione.
4. **I PIREP pilota se ne vanno con le loro voci e con le righe delle regole soddisfatte** (`evt_report_awards`), cancellate dal modulo così
   che ognuna lasci la sua riga nell'audit (le chiavi in cascata le porterebbero via senza). Il nucleo toglie dalla coda un segnale **ancora in
   attesa** della riga che se ne va; uno scartato o diventato award resta (E14b). Un segnale in attesa da due anni è una coda dimenticata.
5. **Una transazione per evento**, come E17: le statistiche, poi le righe dovute di ognuna delle tre conservazioni — in presenza, piloti,
   disponibilità — attraverso l'interceptor, poi **le colonne segnate `[ErasedWith]` svuotate nell'audit** con `AuditRedaction.EmptyDeletedAsync`
   (E10s) per iscrizioni, prenotazioni (le cinque di E8b), PIREP (la nota del validatore), voci (nominativo e sessione) e disponibilità (la
   nota); non per posti, finestre e segnali, che non hanno colonne segnate. La parte in presenza ora scrive le sue statistiche con la stessa
   funzione (`InPersonAsync` di E13a dava già gli stessi numeri), non più a mano nel job. Un evento che fallisce si lascia al giro dopo.
6. **Le prenotazioni della rete accanto al roster**: una **lettura scritta a mano**, `GET /api/events/atc/events/{id}/network-bookings`
   (`EventAtc.View`; l'unico handler chiesto su ogni postazione con il suo FIR, come il roster), **l'ottava dell'ATC**, accanto a quella del
   roster (E11b, lettura 7): niente di quello che dice è una riga dell'hub. Una domanda sola a `IAtcBookingSource` per la finestra
   dell'evento, tenute le prenotazioni sui nominativi delle postazioni dell'evento **nelle ore di una postazione con quel nominativo**; per
   ognuna chi ha prenotato (il nome dal lettore del nucleo se l'hub lo conosce, il numero altrimenti), per che cosa (controllo, training,
   esame) e **se ha quel turno nel roster** (un suo turno sulla stessa postazione che si sovrappone). La risposta è una pagina con
   `available`: falso quando la rete non ha risposto o l'evento tocca più di sette giorni di UTC — **mai una lista vuota** al suo posto.
7. **Sulla pagina**: la sezione «Prenotazioni sulla rete» sotto il roster, nel pannello dell'ATC (la scheda dell'evento e la pagina dell'ATC
   dell'evento), una lista generata in sola lettura; **una query sua**, così con la rete lenta aspetta la sezione e il roster no (la
   risposta di Carmine sulla #207); nessun secondo tentativo dal browser (il nucleo ha già i suoi); `available` falso è un errore della query
   tutto suo (`NetworkBookingsUnavailable`), e la sezione dice «non disponibili»; un errore dell'hub — un 403 dopo un cambio del «a cura di»,
   un 500 — lo dice con le parole del nucleo (`describeProblem`), mai come la rete giù (il punto 3 della revisione della #301). Sotto la
   chiave dell'ATC: una correzione del roster la rilegge, e con lei «nel roster». **Non su un evento annullato**: la pagina non mostra la
   sezione, e la lettura risponde 404 senza chiedere niente alla rete, come per un evento senza roster (il punto 2).
8. **«Duplica» per l'ATC** c'era già: le postazioni con le loro ore spostate (E11a) e le regole di award (E14b); questa fase aggiunge solo il
   test che il piano chiede (postazioni, regole, e niente dei membri né dei segni dei job). **`EventsPersonalData`** copriva già ogni riga di
   M4b (E11a, E11b, E12, E14a): nessuna colonna nuova, nessuna riga nuova in `ErasureTests`; il test chiesto — il registro contato uguale — è
   nuovo.

## 3. Che cosa si tocca

Solo il modulo: `EventRetention.cs` (le date), `EventsRetentionJob.cs` (i due passi in più), `After/EventStatistics.cs` (i numeri che
restano), `Registers/PilotRegister.cs` (il confine), `Atc/NetworkBookings.cs` e `EventsModule.cs`; nel browser `screens/networkBookings.tsx`,
`screens/atc.tsx`, `api.ts`, le parole; `schema.d.ts` generato; `docs/FORKING.md`. Il test `EventsRetentionTests` (VID 761300–761308). Nessuna
migrazione.

## 4. Le domande

**Domanda 1 — le disponibilità di un evento il cui roster non è mai stato pubblicato** ([sull'issue #294][q294a]). Il design (§11) dice «cancellate a roster
pubblicato + la fine dell'evento». Un evento annullato prima della pubblicazione, o finito senza che il roster sia stato proposto, non ha mai
il roster pubblicato.

- **(A), raccomandata**: le disponibilità se ne vanno **con l'evento finito**, pubblicato o no il roster: dopo la fine nessuno le legge, e un
  roster non pubblicato a quel punto non lo sarà più. È il codice.
- (B): alla lettera, solo con il roster pubblicato e l'evento finito; quelle di un evento annullato prima restano per sempre — contro il
  minimo necessario (piano §9.7) — o finché la persona non chiede la cancellazione.

**Decisa: (A)** ([la risposta di Carmine][a294b]), com'è il codice.

**Domanda 2 — il giro completo di M4b sul banco.** Il piano (`10`, E15b punto 5) chiede «disponibilità, roster proposto, corretto,
pubblicato; PIREP di supporto, validato, segnale» in `e2e:full`, e il «fatta quando» che passi in locale e in CI. Il banco non sa farne due
pezzi: **non lancia un job** (`/e2e/signin` è il suo solo endpoint; `/api/jobs/run` fa i giri già dovuti), e la proposta e la pubblicazione
arrivano solo da `events-roster` ai minuti 3/18/33/48; e **non ha sessioni della rete dei suoi personaggi** (`FixtureIvaoApiClient` legge
`tracker-sessions-{vid}.json` senza spostarne le date), quindi un PIREP del banco non ha voci verificate e nessuna regola dà un segnale.
E14a ed E14b l'hanno scritto nel loro handoff.

- **(A)**: la spec mette `applicationsCloseDays` e `rosterPublishDays` a 0 per il suo giro e li rimette dopo, crea un evento di un quarto
  d'ora che comincia un minuto prima del prossimo giro di `events-roster`, e **aspetta** la proposta e la pubblicazione (la mail
  `atcShiftAssigned`); poi taglia la fine del turno (dopo l'inizio è l'unica correzione), con la mail `atcShiftChanged`; a evento finito il
  controllore manda il PIREP ATC e lo valida chi ha `EventReports.Edit`. **Il segnale resta provato in integrazione**
  (`EventsAwardRulesTests`). La spec dura 15–30 minuti, in locale e in CI.
- **(B)**: prima una piccola **fase del nucleo per il banco**: un `/e2e/jobs/{name}` che fa girare un job adesso, solo sul banco come
  `/e2e/signin`, e una fixture di sessioni di un personaggio del banco spostata sul giorno chiesto, come `whazzup.json` e le prenotazioni di
  E15a. Poi il giro è intero, segnale compreso, in pochi minuti. Una PR del nucleo con la sua nota, prima di E15b (regola 6).
- **(C)**: il giro come test d'integrazione (`EventsM4bRoundTests`: i job chiamati nel processo, la sera delle fixture spostata), più una spec
  `e2e:full` delle sole schermate, senza job.

**Raccomandata la (B)** (dalberone, che ha portato la domanda a Carmine sull'[issue #294][q294]) se il «fatta quando» vuole il giro intero
sul banco, in CI; altrimenti la (A), che regge con la (B) (cambia solo come si aspetta il job).

**Decisa: (B)** ([la risposta di Carmine][a294]). La fase del nucleo del banco, E10w, viene prima; E15b ne unisce il branch, e la spec del
giro fa girare `events-roster` subito invece di aspettarlo, con il segnale di award sulle sessioni del trainer (E10w).

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un `DELETE` in blocco per la conservazione | l'audit non saprebbe che le righe se ne sono andate, e il nucleo non ne svuoterebbe le copie (E17, la stessa scelta) |
| Il registro dei piloti con il suo confine, la conservazione con il suo | `AddMonths` avanti e indietro non si invertono a fine mese: una prenotazione uscirebbe dal database un giorno prima che dal registro |
| Due colonne nuove per i PIREP dei piloti e dei controllori nelle statistiche | una migrazione per un numero che cambia solo se un PIREP ATC è deciso di nuovo dopo due anni |
| Le prenotazioni della rete dentro la risposta del roster | con la rete lenta aspetterebbe tutto il roster (la risposta di Carmine sulla #207) |
| Un 503 quando la rete non risponde | la rete giù non è un errore dell'hub: la lettura risponde, e dice che cosa sa, come `IAtcBookingSource` risponde `null` e non lancia |
| La lettura per postazione, una domanda per nominativo | una domanda per giorno basta per tutte le postazioni (nota di E15a, §4) |

## Da portare nel piano

- **Design §11**, la riga delle disponibilità: «cancellate con l'evento finito», pubblicato o no il roster (domanda 1, decisa (A)); la conservazione dei
  piloti con lo stesso confine del registro (lettura 2); le statistiche tengono i numeri dopo, e il limite della lettura 3; i segnali in
  attesa dei PIREP cancellati se ne vanno (lettura 4).
- **Design §4.4**: le prenotazioni della rete accanto al roster, l'ottava lettura scritta a mano dell'ATC, `available` (letture 6 e 7).
- **Design §15** e **piano `10`, E15b**: il giro completo di M4b com'è stato fatto (domanda 2).
- **Piano §9.7**, «Privacy dei membri»: la conservazione di M4b è nel codice (`events-retention`).
