# Il calendario libero e occupato nel selettore della data

**Data:** 18 novembre 2026 — fase di test, fase 5 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.E, punto c), parte
del nucleo.
**Stato:** **Decisa** da Carmine il 18 novembre 2026, tutte e quattro le domande del §6 come raccomandato (§7, [risposta sulla
PR #536][r1]). Il codice del nucleo segue in una pull request sua.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/536#issuecomment-6980596519
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un meccanismo nuovo del nucleo (una lettura del calendario per il selettore, un
«blocca» che oggi non esiste, un campo del form che non è più quello del browser). Poi, in fasi dei moduli, l'uso in Training ed Eventi.

## 1. Che cosa serve

Chi sceglie una data — il trainer che propone le sessioni, lo staff che fissa un evento — oggi la sceglie alla cieca e scopre dopo
che cosa incontra. Carmine chiede (nota del 22 ottobre, §4.E c):

- nel selettore della data, **i giorni liberi in verde e quelli occupati col colore del tipo**, con una legenda;
- nella **finestra dell'ora**, le ore occupate segnate;
- un impegno che **blocca** quell'ora **non si può scegliere**.

La nota del formato della data (`2026-10-31-il-formato-della-data` §3.2) ha lasciato a questa fase anche **la data scritta a mano**: il
nuovo campo si scrive e si legge nell'ordine della divisione (`dd/mm/yyyy`), e va detto come si rilegge.

## 2. Perché quello che c'è non basta

- **Il campo** istante del form generato (`SchemaForm.tsx`, `kind: 'instant'`) è `<input type="datetime-local">`: lo disegna il
  browser, nella sua lingua e nel suo ordine, e una pagina non può colorarne i giorni né spegnerne le ore.
- **Il calendario unico** (`cms_calendar_entries`, `IProjectable`, plan §9.5) sa che cosa c'è e quando, ma non ha un'idea di «blocca»;
  e le sue due letture non servono a un selettore: l'elenco CRUD `/api/calendar` chiede `Calendar.View` (solo staff) e dà pagine di
  righe intere; il blocco dati `calendar` legge da adesso in avanti, con le `props` di una pagina.
- **L'unica domanda «che cosa incontra questa data»** è di Training (`Dates/DateConflicts.cs`, design M3 §2.5): nel modulo, per
  giorno, con i tipi di `conflictKinds` e la politica `Warn | Block | None`, e arriva **dopo** che la data è scritta. Rifarla in
  Eventi sarebbe la stessa regola scritta due volte (`CLAUDE.md` §2).

## 3. Il meccanismo proposto

- **Chi dice «blocca»: il tipo** (⚖️ 1). `CalendarKind` prende una colonna `Blocks` (migrazione additiva, `false` di default), una
  casella nella schermata dei tipi (`/staff/admin/calendar-kinds`, `Calendar.ManageKinds`), e `/api/me` la porta accanto a etichetta e
  colore. Una voce di un tipo che blocca toglie le sue ore a chi sceglie una data col selettore, in ogni modulo.
- **La lettura**: `GET /api/calendar/busy?fromUtc=&toUtc=&exceptModule=&exceptId=`, accanto al CRUD in `CalendarEndpoints.cs`
  (endpoint scritto a mano della famiglia del calendario, §16.6). Per chi ha fatto l'accesso; finestra al massimo di 62 giorni (un mese
  col margine delle settimane a cavallo). Risponde gli intervalli: `startsAtUtc`, `endsAtUtc`, `allDay`, `kind`, `blocks`, `title`.
  La riga che si sta modificando **non occupa sé stessa** (`except…`: il modulo dice al campo qual è la sua riga, modulo e
  `source_id`).
- **Chi vede che cosa** (⚖️ 2): la lettura passa dal **filtro globale** del calendario, lo stesso di ogni altra lettura: ognuno vede
  occupato solo ciò che può leggere. Un membro non vede mai gli impegni dello staff, nemmeno come «occupato» senza nome; un ufficiale
  vede le voci `Department` solo del suo dipartimento. Nessuna regola nuova di visibilità.
- **Il controllo sul server**: `CalendarBusy` (`Core/Content/`) con la stessa domanda della lettura — `BlockingAsync(from, to,
  except)` — che il validatore di un modulo chiama sul campo della data, e che risponde col rifiuto del nucleo `errors.calendar.blocked`
  (`Refusals`, `CrudProblems.Validation`). Legge **come chi scrive**, come fa oggi `DateConflicts` di Training: il browser spegne le
  ore, il server rifiuta le stesse; una regola sola.
- **Le regole del tempo**, scelte di questa nota: una voce **senza fine** occupa l'ora in cui comincia (oggi le sessioni di Training:
  con la fine dalla durata, punto (b) di `dalberone`, occuperanno la loro durata da sole); una voce **di tutto il giorno** occupa il
  giorno; una **bozza** non occupa niente, perché non proietta (`ProjectionSnapshot.Unpublished`): una bozza non è un impegno; un
  evento **annullato** esce già dal calendario.
- **Il fuso** (⚖️ 3): giorni e ore del selettore in **UTC**, come il campo di oggi tiene e scrive la data e come la rete vive, con
  l'ora della divisione accanto (la riga `localTime` che c'è già).
- **Il campo**: nel form generato, solo dove lo schema lo chiede: `meta({ datetime: true, calendar: true })` — non «ogni data»,
  perché un'apertura delle prenotazioni o un off-block non occupano né sono occupati. Lo disegna il `DatePicker`/`Calendar` di
  **Atmosphere** (react-day-picker, già nel §8.3 del piano: «Calendar/DatePicker (eventi, disponibilità trainer)»), coi `modifiers`
  per i giorni — libero in verde (il token di successo), occupato col colore del tipo, un pallino per tipo fino a tre, un giorno tutto
  bloccato spento — e la legenda dai tipi di `/api/me` presenti nel mese; sotto, la finestra delle 24 ore del giorno scelto, le
  occupate segnate col tipo e il titolo, le bloccate non sceglibili. **Nessun componente nuovo in `catalog.ts`**: è il campo istante del
  motore dei form, come il campo file usa il `MediaPicker`. Il campo **si scrive anche a mano**, `31/10/2026 14:05` nell'ordine della
  divisione: `parseMoment` accanto a `formatMoment` (`useMoment.ts`) rilegge giorno, mese e anno nell'ordine di `dateFormat`, e un
  testo che non è una data è un errore del campo con le parole del nucleo (`zodWords.ts`). Il valore resta ISO UTC.

## 4. Che cosa si tocca (la PR del nucleo, dopo il sì)

- **Server**: `CalendarKind.Blocks` e la migrazione del contesto del nucleo; DTO, validatore e schermata dei tipi; `BootstrapCalendarKind`
  in `MeEndpoints.cs`; `Content/CalendarBusy.cs` (nuovo); l'endpoint in `CalendarEndpoints.cs`; la chiave `errors.calendar.blocked`.
- **Web**: `shared/forms/schema.ts` (`calendar`), il campo istante in `SchemaForm.tsx`, `useMoment.ts` (`parseMoment`), la schermata dei
  tipi, i file di lingua del nucleo, il campione della galleria (`/staff/admin/ui-kit`), `schema.d.ts` rigenerato.
- **Test**: unità per le regole del tempo e per `except`; integrazione dell'endpoint con un membro, un ufficiale di un altro
  dipartimento e lo staff (che cosa vede ciascuno); web per i giorni colorati, le ore spente e la data scritta nei tre ordini; e2e.
- **Non si tocca**: i moduli. **L'uso** è una fase di Training ed Eventi, di `dalberone`: mettere `calendar: true` sulle date
  (sessioni proposte e fissate, esame; inizio e fine dell'evento) e chiamare `BlockingAsync` nei validatori. Lì si decide anche che
  cosa resta di `conflictKinds` e di `ConflictPolicy.Block` di Training, che con il «blocca» del tipo diventano in parte la stessa cosa
  detta da chi legge. Non si tocca nemmeno il punto (b), la fine dalla durata.

## 5. Alternative

| Alternativa | Perché no |
|---|---|
| Il «blocca» sulla **singola voce** (una casella su ogni evento e voce) | Ogni modulo dovrebbe portarla nel suo form e nella sua proiezione; chi scrive decide per tutti, voce per voce. Il tipo è della divisione, e la sua schermata c'è |
| Il «blocca» deciso da **chi sceglie** (la politica di Training estesa a ogni modulo) | La stessa riunione bloccherebbe un training e non un evento; ogni modulo una sua impostazione per la stessa domanda |
| Chi non può leggere una voce la vede **«occupato» senza nome** | Dice a un membro che lo staff ha un impegno a quell'ora: è già dire qualcosa, e una regola di visibilità nuova accanto al filtro |
| Il controllo «blocca» che legge **tutto il calendario**, anche ciò che chi scrive non vede | Un rifiuto per un motivo che non si può vedere; un impegno che deve fermare tutti si scrive con la visibilità `Staff` |
| Usare l'elenco CRUD o il blocco `calendar` per il selettore | Il primo chiede `Calendar.View` e pagina righe intere; il secondo è di una pagina, da adesso in avanti, con le sue `props` |
| Ogni modulo la sua domanda (come `DateConflicts`) | La stessa regola due volte, ed è quella che il calendario unico esiste per evitare |
| Un componente nuovo `BusyCalendar` nell'elenco chiuso | Il calendario di Atmosphere fa già i giorni marcati; serve un campo del form, non un componente da mettere nelle schermate |
| Il campo nativo con l'elenco degli impegni sotto | Non è quello che lo staff ha chiesto (i giorni colorati), e la data resta nell'ordine del browser |

## 6. Le domande per Carmine ⚖️

1. **Chi dice «blocca»?** (a) il **tipo** del calendario, una casella nella schermata dei tipi — **raccomandata**; (b) ogni singola
   voce; (c) il modulo che sceglie la data, come oggi Training.
2. **Chi vede che cosa?** (a) ognuno vede occupato solo ciò che può già leggere nel calendario, e il «blocca» sul server legge come
   chi scrive — **raccomandata**; (b) chi non può leggere una voce la vede come «occupato» senza nome né tipo.
3. **Il fuso del selettore.** (a) giorni e ore in **UTC**, con l'ora della divisione accanto, come il campo di oggi —
   **raccomandata**; (b) nel fuso della divisione (una sessione alle 00:30 italiane cade nel giorno italiano, ma il valore scritto è UTC
   e un evento IVAO si legge in Zulu).
4. **Il resto del §3** (le regole del tempo, il campo solo dove lo schema lo chiede, il calendario di Atmosphere senza componenti
   nuovi, la data scritta a mano nell'ordine della divisione): va bene come proposto?

## 7. La risposta di Carmine

Il 18 novembre 2026, in chat al master e poi riportata sulla PR ([commento][r1]): **«Sì, tutte e quattro»**, e alla sessione di
lavoro «tutte come raccomandi, procedi col codice».

1. **Il tipo** del calendario dice «blocca»: una casella nella schermata dei tipi.
2. Ognuno vede occupato **solo ciò che può già leggere**; il controllo sul server legge come chi scrive.
3. Giorni e ore in **UTC**, con l'ora della divisione accanto.
4. **Il resto del §3 come proposto**: una voce senza fine occupa la sua ora, una di tutto il giorno il giorno, una bozza non occupa
   niente; il calendario di Atmosphere senza componenti nuovi; la data scritta a mano nell'ordine della divisione.

## Da portare nel piano

- **§16** (meccanismi generici): il calendario libero/occupato — `GET /api/calendar/busy` dietro il filtro globale, `CalendarBusy`
  per il controllo sul server, il campo istante con `calendar: true`; una regola sola per il browser e il server.
- **§9.5** (calendario): la colonna `blocks` dei tipi e chi la cambia; una bozza non occupa; una voce senza fine occupa la sua ora.
- **§8.3**: il `DatePicker`/`Calendar` di Atmosphere entra nel campo istante del form generato, senza un componente nuovo nell'elenco.
- **§9.7** o l'elenco del bootstrap: `calendarKinds[].blocks` in `/api/me`.
- La nota del 22 ottobre, §4.E: il punto (c) è disegnato; l'uso in Training ed Eventi è una fase dei moduli. La nota del 31 ottobre,
  §3.2: la data scritta a mano si rilegge con `parseMoment` nell'ordine della divisione.
