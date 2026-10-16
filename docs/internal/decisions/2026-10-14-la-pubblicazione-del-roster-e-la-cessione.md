# La pubblicazione del roster, le mail dei turni e la cessione di un turno (E12)

**Data:** 14 ottobre 2026 — fase E12 di M4 (M4b), PR in coda dopo la #262 di E11b
**Stato:** **Proposta**. La domanda è in §5, pubblicata come primo commento della PR #268 ([domanda][q]), scelta di dalberone. Il codice
segue le risposte raccomandate, come hanno fatto E11a ed E11b.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/268#issuecomment-6213434853
**Regola applicata:** `CLAUDE.md` §5. La pubblicazione per data, le quattro mail, la pagina del roster con i nomi solo a chi ha fatto il
login, i turni in `/me` e in `/events/mine`, la riga della cessione con chi cede come stakeholder, lo staff che decide e mai la propria
richiesta, `events.atcCoverage` e chi riceve l'annullamento sono **decisi** (design `09-design-m4.md` §4.4, §4.4-bis, §7.1, §7.3, §8.3,
§8.4; note `2026-09-29-il-roster-atc`, punti 5 e 6, e `2026-10-13-la-proposta-del-roster` con le [risposte di Carmine sull'issue
#259][a259]). Questa nota registra **le letture** che la fase ha dovuto fare dove il design non dice: casi **(a)** e **(b)**, con **una
lettura scritta a mano** in più (la pagina pubblica del roster), uno scostamento dal design §7.2 come le sei dell'ATC di E11a ed E11b.
Niente del nucleo: si usano `ISubmittedByMembers`, `IHasStakeholder` con `DeniedToStakeholder`, `IHasFir`, il servizio delle notifiche,
`MemberNames`, `[ErasedWith]` e i blocchi Data così come sono.

[a259]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259#issuecomment-6164366206

## 1. Che cosa serviva decidere

Il design dice che a inizio − `rosterPublishDays` il roster è pubblicato senza un pulsante e `events-roster` manda `atcShiftAssigned` a
ognuno (`notified_at`); che dopo ogni turno aggiunto, spostato o tolto avvisa subito; che la pagina `/events/{slug}/roster` mostra tutti i
turni con i nomi solo a chi ha fatto il login; che il controllore trova i suoi turni in `/me` e in `/events/mine`; che la cessione è una riga
del membro (turno, chi cede, a chi — anche nessuno —, nota, stato) che lo staff approva o rifiuta, mai la propria; che il blocco
`events.atcCoverage` mostra gli eventi dei prossimi giorni con turni scoperti o candidature da chiudere; che annullare e spostare un evento
avvisano anche chi ha un turno. Non dice: dove sta il segno della pubblicazione e che cosa succede se l'inizio si sposta dopo; una mail per
turno o per persona; che cosa riceve chi ha un turno cambiato di ore, di postazione o di controllore; fino a quando si chiede e si decide una
cessione, come si ritira, a chi va una cessione «a nessuno»; che cosa conta il blocco; e che cosa ne fa la cancellazione dei dati di una
persona.

## 2. Le letture

1. **Il segno della pubblicazione**: una colonna dell'evento, **`roster_published_at`**, scritta da `events-roster` come
   `roster_proposed_at` — sotto il blocco della riga (`FOR UPDATE`), senza muovere la versione della riga e senza una riga dell'audit
   dell'evento. Si pubblica una volta un evento **pubblicato, con un roster, non annullato, non finito, già proposto**, il cui momento è
   passato (inizio − `rosterPublishDays`). **Una volta pubblicato resta pubblicato**, anche se l'inizio si sposta dopo o l'impostazione
   cambia: la pagina pubblica, `/events/mine`, il blocco e le mail della correzione chiedono «è pubblicato?», e la risposta non deve
   cambiare sotto di loro (lo stesso principio della risposta di Carmine sulla lettura 5 di E11b: la chiusura non segue più l'inizio). Un
   roster proposto dopo il suo momento (una bozza pubblicata tardi, `applicationsCloseDays` uguale a `rosterPublishDays`) si pubblica nello
   stesso giro, subito dopo la proposta.
2. **Le mail della pubblicazione**: `atcShiftAssigned` una volta per turno (`notified_at`), **una mail per controllore per evento** con
   tutti i suoi turni di quell'evento non ancora detti (nominativo e ore in UTC) e il link al roster e a
   `/events/mine` — come «una mail per persona» delle prenotazioni (E6a) e il promemoria (E6b). **Il segno prima della mail**, come il
   promemoria: una mail che non entra in coda è persa, mai mandata due volte. Ogni giro dice **ogni turno di un roster pubblicato con
   `notified_at` vuoto**, così un turno che la correzione non è riuscita a dire lo dice il giro dopo.
3. **Dopo la pubblicazione** la correzione avvisa **subito**, dopo che il salvataggio è fatto, e **solo chi sapeva del turno**
   (`notified_at` scritto):
   - un turno **aggiunto** → `atcShiftAssigned` al controllore, e il segno;
   - un turno con **altre ore o un'altra postazione**, stesso controllore → `atcShiftChanged`, con com'era e com'è; se il controllore non lo
     sapeva ancora, `atcShiftAssigned` con il turno com'è ora;
   - un turno dato **a un altro controllore** → `atcShiftRemoved` a chi lo aveva (se lo sapeva), `atcShiftAssigned` al nuovo; il segno si
     svuota e si riscrive per il nuovo;
   - un turno **tolto** — anche con la sua postazione eliminata — → `atcShiftRemoved`, se lo sapeva.
   Prima della pubblicazione nessuno riceve niente: i controllori conoscono i loro turni alla pubblicazione. Le mail si scrivono con un
   segno per turno, non con una regola sull'ora, quindi «chi sapeva» è sempre la riga.
4. **Annullare e spostare l'evento** avvisano anche **ogni controllore con un turno**, una mail per persona insieme a chi ha prenotato o si
   è iscritto (`EventSaving.PeopleToTellAsync`). Prima della pubblicazione il controllore non sa ancora quale turno ha, ma ha dato la sua
   disponibilità: sapere che l'evento è annullato o spostato gli serve lo stesso. Chi ha dato la disponibilità senza un turno no: il design
   non lo nomina.
5. **La pagina del roster** `/events/{slug}/roster`, con **una lettura scritta a mano**, `GET /api/events/public/{slug}/roster`
   (anonima, come quella della pagina dell'evento): per un evento che il lettore vede (la stessa regola della pagina, lo staff degli eventi
   compreso). **Prima della pubblicazione** nessuna riga, e il momento in cui sarà pubblicato; **dopo**, ogni postazione con i suoi turni **e
   i pezzi scoperti**, per nominativo e ora — gli scoperti con la stessa funzione del roster dello staff (`StaffRoster.Gaps` su
   `RosterProposer.Pieces`) —; **il nome e il rating ATC del controllore solo a chi ha fatto il login** (un visitatore legge «assegnato» o
   «scoperto»), una persona cancellata con `personName`; né il perché né gli avvisi, che sono dello staff. La pagina dell'evento porta il
   link quando il roster è pubblicato (`PublicEventAtcDto` sa il momento e se è pubblicato). È la **settima lettura scritta a mano** dell'ATC:
   uno scoperto non è una riga (lettura 2 di E11b), e la lista del motore non lo dà.
6. **I turni del membro**: `/events/mine` ha **«I miei turni»** — quelli dei roster pubblicati, anche passati, sotto il loro evento, con
   «Cedi» fino all'inizio e lo stato di una richiesta — e **«Le mie disponibilità»** (la lettura 8 di E11a: `MemberAvailability.MineAsync`),
   con «Cambia» sulla pagina dell'evento e «Ritira» fino alla chiusura. `GET /api/events/mine/shifts`, un verbo del flusso del membro come
   `/mine/bookings`. **Il blocco `events.myEvents`** aggiunge i turni ancora da fare (fine dopo adesso), nella stessa forma. **Un turno di un
   roster non ancora pubblicato non si mostra al suo controllore.**
7. **La cessione**, `evt_atc_shift_transfers` (`EventAtcTransfer`): il turno (`shift_id`, con una chiave verso il turno: un turno tolto porta
   via le sue richieste, ognuna con la sua riga dell'audit), l'evento, **chi cede** (`controller_vid`, lo stakeholder), **a chi**
   (`to_vid`, vuoto = «cerco un sostituto»), la nota di chi cede, lo stato (`Pending`, `Approved`, `Refused`, `Withdrawn`), chi ha deciso, quando
   e la sua nota; il FIR **copiato dal turno** (`IHasFir`: lo staff di un FIR decide le richieste delle sue postazioni), la cura e lo scope
   dell'evento. `ISubmittedByMembers`, `IHasStakeholder`, `IVisible` ai membri, `[Audited]`. **Non** `[WithdrawnByStakeholder]`: la riga
   porta la decisione dello staff (il nucleo lo vieta proprio per «la cessione di un turno»); il membro ritira una richiesta **cambiandone lo
   stato** in `Withdrawn`, la regola del nucleo di una riga che un membro ha mandato (T11).
   - **Chiedere** (`POST /api/events/mine/shifts/{id}/transfer`, a chi e una nota): il controllore del turno, su un roster pubblicato, prima
     dell'inizio dell'evento, non annullato; **una richiesta in attesa per turno**; chi riceve deve essere entrato nell'hub, e non è chi cede.
   - **Ritirare** (`DELETE` sullo stesso indirizzo): una richiesta in attesa diventa `Withdrawn`.
   - **Decidere** (`POST /api/events/atc/transfers/{id}/approve` e `/refuse`, verbi che il design nomina): `EventAtc.Edit` chiesto all'unico
     handler **sulla richiesta** — negato a chi cede, superadmin compreso — e, per approvare, anche **sul turno com'è dopo**: negato al suo
     nuovo controllore, così nessuno dello staff si dà un turno da solo (come nella correzione). **Approvare**: fino all'inizio dell'evento
     (dall'inizio un turno non va più a un altro controllore, la risposta di Carmine sull'issue #259, e dopo l'inizio se ne taglia solo la
     fine, la sua [risposta sulla #262](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/262#issuecomment-6173401612): il taglio è un
     «cambiato» per le mail della lettura 3); il turno deve essere ancora di chi l'ha
     chiesto; il nuovo titolare è **quello nominato, o un altro che lo staff sceglie** (obbligatorio per una richiesta «a nessuno»), entrato
     nell'hub, **non su un'altra postazione dello stesso evento in quelle ore** (un rifiuto, la stessa regola della correzione); il resto del
     §4.3 sul nuovo titolare — rating, turni di fila, disponibilità — sono **avvisi** sulla lista delle richieste. Il turno passa al nuovo
     (`Origin = Staff`, il perché tolto, l'audit tiene chi l'aveva); partono `atcShiftTransferDecided` a chi ha ceduto e `atcShiftAssigned`
     al nuovo. **Rifiutare**: con una nota, **anche dopo l'inizio** (così una richiesta rimasta in attesa si chiude), e
     `atcShiftTransferDecided` a chi l'ha chiesta.
   - **Un turno cambiato sotto una richiesta**: se lo staff dà il turno a un altro o lo toglie, una richiesta in attesa non si approva più (il
     turno non è più di chi l'ha chiesta, o non c'è) e la lista lo dice; lo staff la rifiuta. Un turno tolto porta via le sue richieste.
   - **La lista dello staff**: una risorsa del motore in sola lettura (`/api/events/atc/transfers`, `EventAtc.View`, per evento e stato), con
     chi cede e chi riceve (`col.person`), il turno, lo stato e gli avvisi sul nuovo titolare; nel pannello dell'ATC sotto il roster, con
     «Approva» e «Rifiuta».
8. **`events.atcCoverage`**, nelle due metà, con una proprietà, **`days`** (quanti giorni avanti, 14 predefinito, al più 60): gli eventi con
   un roster, pubblicati, non annullati, non finiti, che cominciano entro `days`, e che hanno **pezzi scoperti**, **disponibilità ancora
   aperte** o **richieste di cessione da decidere**; per ognuno il titolo e il link al suo ATC, l'inizio, il punto del roster (disponibilità
   aperte fino a…, da proporre, proposto e pubblicato il…, pubblicato), quante disponibilità, quanti pezzi scoperti su quanti, quante
   richieste in attesa. Conta **sulle postazioni che il lettore legge** (l'unico handler su ognuna, come il roster) e le richieste che può
   decidere; a chi non legge l'ATC di nessun evento, nessun evento; a un visitatore `signedIn: false`. **Dati, non parole**: la pagina le
   scrive.
9. **La cancellazione dei dati di una persona**, nella fase che crea le colonne: le richieste di chi cede di un evento **non ancora finito
   si cancellano**; quelle di un evento finito restano sotto lo pseudonimo, **senza le due note** (`[ErasedWith(nameof(ControllerVid))]`, così
   una richiesta ritirata prima non le lascia nell'audit). Una richiesta che nomina la persona come chi riceve prende lo pseudonimo dal
   nucleo in `to_vid`, e non si approva più verso di lei. Le colonne di persona (`controller_vid`, `to_vid`, `decided_by`) entrano in
   `ErasureTests`.
10. **Le pagine e gli endpoint**: il roster pubblico è una pagina del sito sotto `_public`; «I miei turni», «Le mie disponibilità» e «Cedi»
    stanno in `/events/mine`; le richieste nel pannello dell'ATC dello staff. Gli endpoint scritti a mano: tre verbi del membro (i suoi
    turni, chiedere, ritirare), due dello staff (approvare, rifiutare) e la lettura pubblica del roster (lettura 5). **L'e2e** non aspetta
    `events-roster` (il banco non lancia un job a comando, come per E11b): prova quello che non dipende dall'orologio, e la pubblicazione con
    la mail la provano i test d'integrazione e il banco della fase al giro del quarto d'ora.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| «Pubblicato» calcolato dalla data a ogni lettura | un inizio spostato dopo, o l'impostazione cambiata, ritirerebbe un roster che i controllori hanno già letto |
| Una mail per turno | tre turni della stessa sera sarebbero tre mail; le prenotazioni ne mandano una per persona (E6a) |
| Le mail della correzione anche prima della pubblicazione | lo staff corregge più volte fra la chiusura e la pubblicazione: il controllore saprebbe di turni che poi cambiano |
| La cessione approvata che cambia subito il titolare | cambiare il titolare non è una modifica della propria riga (design §4.4-bis): decide lo staff |
| `[WithdrawnByStakeholder]` sulla richiesta | cancellarla cancellerebbe la decisione dello staff: il nucleo lo vieta per questa riga |
| Una richiesta «a nessuno» che libera il turno quando è approvata | il turno resterebbe scoperto in silenzio; lo staff nomina il sostituto, o rifiuta e corregge il roster |
| Gli scoperti nascosti al pubblico | il roster pubblico dice dove manca qualcuno, e lo dice con la stessa funzione dello staff |
| Il blocco con un solo numero per evento | lo staff deve sapere se mancano persone, se le candidature sono ancora aperte o se una cessione aspetta |

## 4. Che cosa si tocca

Solo il modulo: `EventAtcTransfer.cs`, `Event.RosterPublishedAt`, `Atc/` (la pubblicazione nel giro di `events-roster`, le mail dei turni,
le cessioni del membro e dello staff, il roster pubblico, i turni del membro), `Blocks`/`EventsModule` (`events.atcCoverage`, le quattro
notifiche), la migrazione (dopo `AddAtcShifts` di E11b), `EventSaving` (chi ha un turno fra chi si avvisa), `ShiftEndpoints` e
`AtcPositionEndpoints` (le mail della correzione), `EventsPersonalData`, `MyEventsProvider`; le pagine in `web/src/modules/events/`; le
parole del modulo; `docs/FORKING.md`. Nessun file del nucleo, se non `schema.d.ts` generato e **le colonne di persona in `ErasureTests`**; i
conteggi dei blocchi in `uiKit.test.ts` e `DataBlockEndToEndTests` crescono di uno, come il design §7.3 dice.

## 5. La domanda a Carmine

Pubblicata come primo commento della PR #268 ([domanda][q]).

**Sì alle dieci letture di §2?** In particolare:

- **(1)** il segno `roster_published_at` sull'evento, che resta anche se l'inizio si sposta dopo;
- **(2)** una mail per controllore per evento alla pubblicazione, il segno prima della mail;
- **(3)** dopo la pubblicazione le mail della correzione solo a chi sapeva del turno, e un cambio di controllore come «tolto» al vecchio e
  «assegnato» al nuovo;
- **(4)** annullare e spostare l'evento avvisano ogni controllore con un turno, anche prima della pubblicazione;
- **(5)** la pagina pubblica del roster **con i pezzi scoperti**, nomi e rating solo a chi ha fatto il login, con **una lettura scritta a
  mano** (la settima dell'ATC);
- **(7)** la cessione: ritirata cambiando lo stato; chiesta e approvata **fino all'inizio**, rifiutata anche dopo; una richiesta «a nessuno»
  approvata **con il sostituto che lo staff nomina**, e lo staff che può approvare verso un altro membro da quello nominato;
- **(8)** `events.atcCoverage` con la proprietà `days` e i tre motivi (scoperti, disponibilità aperte, cessioni da decidere).

**Raccomandato: sì a tutte.**

## Da portare nel piano

- **§9.2, riga Events** e design **§7.2** (che non si tocca): la pagina pubblica del roster si legge con **una lettura scritta a mano**
  (la settima dell'ATC), accettata come scostamento accanto alle sei di E11a ed E11b; la cessione si decide con due verbi dello staff.
- **§9.7, collaborazioni ATC↔Events**: il roster si pubblica una volta per data e resta pubblicato; dopo, ogni correzione avvisa chi sapeva
  del turno; la cessione è una richiesta del controllore che lo staff decide fino all'inizio, mai la propria, con il sostituto nominato.
- **§11, «Privacy dei membri»**: le richieste di cessione di un evento non ancora finito si cancellano con i dati della persona; quelle di
  un evento finito restano senza le due note.
- **§16 (job)**: `events-roster` pubblica una volta, sotto il blocco della riga dell'evento, e il suo segno non cambia la versione della
  riga; le mail dei turni hanno un segno per turno, scritto prima della mail.
