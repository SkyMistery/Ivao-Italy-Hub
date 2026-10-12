# «Duplica» e la cancellazione dei dati di una persona negli eventi: le letture del design (E8b)

**Data:** 11 ottobre 2026 — fase E8b di M4 («Duplica», la cancellazione, il giro completo di M4a)
**Stato:** **Decisa** — da Carmine, [le sue risposte sull'issue #248][a] alle due domande dell'[issue][q], aperta all'inizio della fase
perché il codice ne dipende (§4): la (A′) e la (i), come raccomandate e come E8b le scrive (§6).
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — l'unico handler, il guardiano
dell'interceptor, la cura dell'evento sulle sue righe (`IEventChild`), il generatore degli slot privati di E7
(`PrivateSlotGeneration`), `IPersonalDataEraser`, la riscrittura delle colonne di persona del nucleo (`PersonColumns`) e la sua
modalità «cancellazione» —; sono letture del design M4 §2.3-bis e §11.1 e delle note `2026-09-29-la-vita-di-un-evento` §2.5 e
`2026-09-29-i-dati-dei-membri-negli-eventi` §2.5. **Nessuna migrazione**, niente nel nucleo.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/248
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/248#issuecomment-6113460643
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113085816
[a2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113461471

## 1. «Duplica» (design §2.3-bis)

1. **Un verbo, una transazione**: `POST /api/events/events/{id}/duplicate` (`EventsDuplicate`), con la nuova data d'inizio, il nuovo
   indirizzo e la scelta degli slot pubblici. Risponde **201** con l'evento nuovo (`EventDetailDto`, l'indirizzo della risorsa
   come la creazione del motore CRUD), e la pagina apre la copia.
2. **La nuova data d'inizio è un istante**, in UTC come ogni orario degli eventi, non un giorno: il campo è quello del form
   dell'evento, e chi tiene l'ora cambia solo la data. **Ogni orario si sposta della stessa differenza** (il nuovo inizio meno il
   vecchio): l'inizio, la fine, «visibile da» e l'apertura delle prenotazioni quando ci sono, e i due orari di ogni slot pubblico
   copiato.
3. **Che cosa è la copia**: l'evento con tutte le sue impostazioni — il tipo, i cinque interruttori, chi organizza e il suo link,
   titolo, riassunto e descrizione, la visibilità, la durata di un turno, il luogo, i tre limiti di chi non vola — e **nella stessa
   cura** dell'evento di partenza (i suoi dipartimenti; l'ED c'è sempre). **Una bozza**: mai pubblicata, non annullata, senza i
   segni dei job (`roster_proposed_at`, `after_done_at`); chi l'ha creata e quando li scrive l'interceptor.
4. **Il banner si copia finché la libreria lo ha** (`DeletedAt` vuoto): il job del nucleo elimina i file di un evento sette giorni
   dopo la sua fine (§2.4), e il caso per cui «Duplica» esiste — l'RFE dell'anno scorso — ha quasi sempre il banner già eliminato;
   copiato, la bozza mostrerebbe un'immagine rotta. **La descrizione si copia com'è scritta**: è un documento intero, e una sua
   immagine eliminata lo è anche nell'evento di partenza; l'editor la mostra e lo staff la sostituisce.
5. **Gli scali con la capacità e le rotte** si copiano come sono. **Gli slot pubblici, a scelta** (un interruttore della richiesta,
   «copia gli slot pubblici»), con le rotazioni: ogni colonna com'è, gli orari spostati. Le righe copiate **non si ricontrollano**
   con gli aeroporti e i tipi che il nucleo conosce oggi: erano accettate, si spostano insieme, e il form le ricontrolla al primo
   salvataggio. Nessuna riga dei membri: niente prenotazioni (le disponibilità, i turni, le iscrizioni e i PIREP arrivano con le
   loro fasi, che allargano il verbo).
6. **Gli slot privati** — **la domanda 1**. Il design dice «i privati si rigenerano dalla capacità». **Raccomandato**: la copia li
   genera **nella stessa transazione** con il generatore di E7, **quando i suoi slot pubblici ci sono già** — li ha copiati, oppure
   l'evento non ne prende (un MSE) —; **altrimenti li lascia a «Genera»**, che lo staff preme dopo aver caricato la tabella nuova:
   generati prima, i privati occuperebbero i passi e la capacità che la tabella nuova prenderà, e l'ora andrebbe oltre la capacità
   finché nessuno preme di nuovo «Genera». Quando il generatore non genererebbe (nessuno scalo con una capacità, più di 5.000), la
   copia è fatta senza privati, e «Genera» sull'evento nuovo dice perché.
7. **Ogni evento si duplica**, in ogni stato: una bozza, un pubblicato, un concluso (il caso dell'RFE dell'anno scorso), un
   annullato.
8. **Chi duplica**: `Events.Edit` **sull'evento nuovo** — con lo scope di un evento nuovo, `events:event:0`, quindi **un grant su un
   evento solo non duplica** (E10i: mai un evento nuovo) —, e il permesso dell'area di ogni riga che la copia scrive, come per
   eliminare (E4: chi elimina un evento deve avere anche `EventRoutes.Edit`): `EventRoutes.Edit` quando l'evento ha delle rotte,
   `EventBookings.Edit` quando la copia porta degli slot, i pubblici copiati o i privati generati. Si chiedono **all'unico handler
   prima di scrivere**: senza uno di loro 403, e niente scritto (il guardiano dell'interceptor è la rete sotto, non la strada).
9. **L'indirizzo** ha le regole del form — una parola sola per l'evento, `mine` riservato (le stesse regole, scritte una volta per i
   due validatori) — ed è libero: altrimenti `events:errors.slugTaken`, anche quando l'indice univoco risponde nello stesso istante.
   **Il tipo non si sceglie di nuovo**: la copia tiene quello dell'evento di partenza anche se la divisione non lo offre più (la
   regola di E4 lo chiede quando un tipo si sceglie), e lo staff lo cambia nel form.
10. **La pagina**: «Duplica» nella barra di un evento, a chi ha `Events.Edit` sulla riga; una pagina con il form generato — la nuova
    data d'inizio (proposta: quella dell'evento), il nuovo indirizzo, e «copia gli slot pubblici» su un evento che ne prende —; dopo,
    la pagina dell'evento nuovo.

## 2. La cancellazione dei dati di una persona (design §11.1)

`EventsPersonalData : IPersonalDataEraser`, per le righe di M4a: le prenotazioni (`evt_bookings`) del pilota.

11. **«Concluso»** — **la domanda 2**. **Raccomandato: un evento è concluso quando la sua fine è passata, annullato o no** — l'ora
    che decide anche la conservazione (24 mesi «dopo la fine»), e la stessa di `EventState` («dopo la sua fine»). Un evento annullato
    che non è ancora finito perde le prenotazioni della persona come uno che si farà; dopo la sua fine le tiene, con lo pseudonimo.
12. **Le prenotazioni di un evento non concluso si cancellano**, attraverso il contesto del modulo in modalità «cancellazione»: lo
    slot torna libero (la pagina dice «libero», l'esportazione nessuno), l'audit tiene che una riga è stata cancellata e quando, il
    nucleo ne svuota le copie. Nessuna mail: la persona non c'è più, e nessun altro ne è toccato.
13. **Le prenotazioni di un evento concluso restano**, con lo pseudonimo che il nucleo scrive in `booker_vid`, **senza i dati del volo
    privato** (`callsign`, `other_icao`, `other_time_utc`), **senza la nota** dello staff su un volo non fatto
    (`unflown_excused_note`: il design dice «senza note») e **senza la sessione della rete** in cui il volo è stato trovato
    (`flown_session_id`: lega la riga alla persona sul tracker di IVAO, come la sessione di un volo dei tour, che la cancellazione dei
    tour svuota). Restano quello che le statistiche contano: lo slot, l'aereo, se e quando è stato volato e verificato
    (`flown_at`, `flown_checked_at`), il legame di una coppia, quando è stata fatta.
14. **Una coppia** (un arrivo privato con la sua partenza, E7) è di un pilota solo e di un evento solo: le due se ne vanno insieme o
    restano insieme, e la regola del ritiro (`BookingPairs.LetGoAsync`, il legame che si scioglie quando una sola se ne va) qui non
    serve.
15. **Quello che la persona ha fatto come staff resta**, con lo pseudonimo che il nucleo scrive nelle colonne `…By`
    (`cancelled_by` di un evento, `unflown_excused_by` di una prenotazione, `created_by`, `updated_by`), e con le sue parole, che
    parlano di altri: la nota di una scusa scritta per un altro pilota resta (la risposta 4 della nota di T20b).
16. **Le righe dell'anteprima**: `events:erasure.bookingsToCome` (cancellate) e `events:erasure.bookingsOver` (tenute, con lo
    pseudonimo). Una cancellazione rifatta non trova più niente: le righe tenute hanno lo pseudonimo.

## 3. Gli endpoint scritti a mano

**Uno**, per il conto del piano §16.6: «Duplica», il verbo che il design nomina (§2.3-bis, «un solo endpoint»). Nessuna lettura
nuova; la cancellazione non ha endpoint suoi (è quello del nucleo).

## 4. Le domande a Carmine

Poste in inglese con [l'issue #248][q]; in italiano dicono:

> 1. «Duplica» e gli slot privati: (A) la copia li genera sempre, nella sua transazione; (A′, **raccomandata**) li genera quando i suoi
>    slot pubblici ci sono già — li ha copiati, o l'evento non ne prende — e altrimenti li lascia a «Genera», dopo la tabella nuova;
>    (B) non li genera mai, e lo staff preme «Genera» sull'evento nuovo?
> 2. Per la cancellazione dei dati di una persona, quando è «concluso» un evento annullato: (i, **raccomandata**) quando passa la sua
>    fine, come ogni altro; (ii) mai — il suo stato è «annullato» qualunque siano le date, e le prenotazioni della persona si
>    cancellano sempre; (iii) subito — su un evento annullato non succede più niente, e le prenotazioni restano sempre con lo
>    pseudonimo?

## 5. Che cosa si tocca

Solo il modulo e i suoi documenti: `Staff/EventDuplication.cs` (nuovo), `Staff/EventEndpoints.cs` (il verbo), `Staff/EventDtos.cs`
(la richiesta, le regole dell'indirizzo scritte una volta), `EventState.cs` (quando un evento è concluso, letto in un posto solo),
`EventsPersonalData.cs` (nuovo), `EventsModule.cs`, le parole del modulo, le pagine (`web/src/modules/events/`), `docs/FORKING.md`.
Nessuna migrazione: ogni colonna c'è da E2, E5 ed E6a.

## 6. Le risposte di Carmine, e la revisione

11 ottobre 2026, [le sue risposte sull'issue #248][a] (autore `SkyMistery`: date in chat al master e pubblicate da lui su sua
istruzione):
1. **«Duplica» e gli slot privati: la (A′).** La copia genera i suoi slot privati nella sua transazione quando i suoi slot pubblici
   ci sono già — li ha copiati, o l'evento non ne prende —; altrimenti li lascia a «Genera», premuto dopo che la tabella nuova è
   caricata. Mai oltre la capacità.
2. **Un evento annullato è concluso quando passa la sua fine, la (i)**, come ogni altro: prima, le prenotazioni della persona
   cancellata si cancellano; dopo, restano con lo pseudonimo. Un orologio solo, lo stesso da cui conta la conservazione.

**La revisione** ([i rilievi sulla #249][r]: approvabile, niente da correggere) ha cinque punti bassi, presi tutti perché costano
poco: (1) l'eraser lascia fuori una prenotazione il cui evento non c'è più, come fa il join dell'anteprima (`TryGetValue`), così le
due dicono le stesse righe; (2) `docs/events-bookings-export.md` dice che cosa dà l'esportazione dopo una cancellazione; (3) «Duplica»
dice quando la copia non ha potuto generare i suoi slot privati: la risposta porta la chiave del perché (`EventDuplicatedDto`), e la
pagina lo dice con la via alla copia; (4) un nuovo inizio che sposterebbe un orario dell'evento fuori dal calendario — gli anni 1000–
9999 di una colonna DATETIME — è rifiutato sul suo campo (`events:errors.duplicateOutOfCalendar`), non più un 500; (5) il test della
cancellazione guarda anche il callsign della partenza collegata e le righe «erased» dell'audit.

**Fuori da E8b**: il buco dell'audit che il revisore ha trovato (punto 7) — le righe dell'audit di una prenotazione ritirata o tolta
**prima** della cancellazione tengono il callsign, l'altro aeroporto, l'orario e le note — è, per [la risposta 2 di Carmine sulla
#249][a2], **una fase del nucleo di M4**, con la sua PR e una nota nuova, unita prima che le prenotazioni si aprano su
un'installazione vera. Il punto 8 del revisore (la descrizione copiata com'è scritta) non ha avuto risposta: resta la lettura 4.

## Da portare nel piano

- Design M4 §2.3-bis: la nuova data d'inizio è un istante e ogni orario si sposta della stessa differenza; che cosa copia l'evento
  (le impostazioni, la cura; il banner finché la libreria lo ha, la descrizione com'è), gli slot pubblici a scelta e non
  ricontrollati, gli slot privati (la (A′) della domanda 1, decisa; la risposta dice perché non sono stati generati quando il
  generatore li rifiuta), chi duplica (i permessi delle aree che la copia scrive, mai un evento nuovo con un grant su un evento
  solo), l'indirizzo, il tipo che non si sceglie di nuovo, un nuovo inizio che sposterebbe un orario fuori dal calendario rifiutato.
- Design M4 §11.1: quando un evento è concluso (la (i) della domanda 2, decisa); che cosa resta di una prenotazione tenuta (senza il
  volo privato, la nota e la sessione della rete), e che cosa ne dà l'esportazione; una coppia va o resta intera.
- Piano §16.6: il conto degli endpoint a mano di M4 (uno di E8b).
