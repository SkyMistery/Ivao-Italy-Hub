# La proposta del roster ATC e la correzione dello staff (E11b)

**Data:** 13 ottobre 2026 — fase E11b di M4 (M4b), PR in coda dopo la #256 di E11a
**Stato:** **decisa**. Carmine, in chat al master, che ha pubblicato le risposte sull'issue #259 su sua istruzione: la **domanda 1**,
«`atcData` di IT», (A), il 13 ottobre 2026 ([risposta 1][a]); la **domanda 2**, le dieci letture, il 14 ottobre 2026, dopo la revisione
della #262 ([risposta 2][a2]): **sì alle letture 1, 2, 4, 7, 9 e 10**, sì alla **3** com'è, e la **5**, la **6** e l'**8** con tre
condizioni, ora nel codice (§7). Le domande sono in §5, pubblicate nell'**issue #259** ([domande][q]); i rilievi del revisore sulla #262
([rilievi][r]) stanno a parte, in §6.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259#issuecomment-6146395689
[a2]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259#issuecomment-6164366206
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/262#issuecomment-6164353974
[e]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6164279151
**Regola applicata:** `CLAUDE.md` §5. La tabella dei turni, le regole della proposta, la correzione, i predefiniti e il permesso negato
all'interessato sono **decisi** (design `09-design-m4.md` §1.7, §1.12, §4.3, §4.4, §6.1, §8.4; note `2026-09-29-il-roster-atc`,
`2026-09-29-chi-lavora-sugli-eventi`, `2026-09-29-le-impostazioni-degli-eventi`). Questa nota registra **le letture** che la fase ha
dovuto fare dove il design non dice: casi **(a)** e **(b)**, con **una lettura scritta a mano** che è uno scostamento dal design §7.2
(«nessuna eccezione dichiarata al motore CRUD»), come le cinque di E11a. Niente del nucleo: si usano `IHasFir`, `IHasStakeholder` con
`DeniedToStakeholder`, il vocabolario dei rating (`PreferredFor`, `IsAtLeast`), i minimi delle postazioni (`AtcPositionMinimum.Over`),
la directory delle postazioni, `IAtcActivitySource.SessionsOfAsync` (E10b), `MemberNames` (E10m) e `[ErasedWith]` (E10n) così come sono.

## 1. Che cosa serviva decidere

Il design dice le colonne del turno (§1.7), le regole della proposta in quattro punti (§4.3), che lo staff corregge fra la chiusura e la
pubblicazione anche con chi non si è candidato e che un'aggiunta fuori dalle regole è un avviso (§4.4), che il job propone una volta da
`roster_proposed_at` (§8.4), e che la correzione è una lista generata per postazione e ora con il perché e gli avvisi (§7.2). Non dice:
come sta un turno scoperto; dove sta il perché; che cosa vuol dire «di fila» e «pausa» in un roster che si riempie fuori dall'ordine delle
ore; come si misura l'esperienza; che cosa decide quando le regole non bastano; quando si propone un evento che era una bozza alla
chiusura; da quando a quando lo staff corregge e che cosa è un rifiuto e che cosa un avviso; che cosa succede a una postazione con dei
turni; dove sta il roster sulle pagine; e che cosa ne fa la cancellazione dei dati di una persona.

## 2. Le letture

1. **La riga del turno** (`evt_atc_shifts`, `EventAtcShift`): le colonne del piano — `position_id`, `controller_vid`, `from_utc`,
   `to_utc`, `origin` (`Proposed`, `Staff`), `notified_at`, `attendance` (predefinito `Unknown`), `attendance_by`, `attendance_at`,
   `attendance_note` — e in più quelle che le regole di tutte le righe dell'evento chiedono: `event_id` (la riga è dell'evento,
   `IEventChild`, con la sua cura e il suo scope), **`fir` copiato dalla postazione** in `BeforeAuthorize` (`IHasFir`: l'unico handler, il
   guardiano e la lista lo leggono sulla riga; mai dalla directory, come dice l'handoff di E11a), `reason` (lettura 4) e i campi di
   `IAuditable`. Lo stakeholder è il controllore: **`EventAtc.Edit` è negato all'interessato** (catalogo, §6.1), quindi nessuno dello staff
   aggiunge, sposta o toglie un turno suo, il superadmin compreso.
2. **Un turno scoperto non è una riga**: è un pezzo di `shift_minutes` di una postazione che nessun turno tocca, **calcolato alla
   lettura**. Così una postazione aperta, spostata o tolta dopo la proposta, una durata del turno cambiata, un turno tolto dallo staff
   dicono sempre i loro scoperti, senza una regola che tenga in pari delle righe vuote. `events.atcCoverage` (E12) conterà nello stesso
   modo, con la stessa funzione.
3. **Il proponente** è una funzione pura e deterministica (`Atc/RosterProposer.cs`), con le regole del §4.3 lette così:
   - **i turni**: ogni postazione dalla sua apertura, a pezzi di `shift_minutes` (dell'evento, o dell'impostazione); **l'ultimo pezzo è
     più corto** quando la postazione chiude prima;
   - **chi può**: disponibile per tutto il turno (`AtcRules.Cover`), con un rating almeno `minimumAtcRating` e almeno **il minimo della
     postazione per quelle ore** (le FRA di IVAO, `AtcPositionMinimum.Over`), non in un altro turno che lo tocca, e non oltre
     `maxConsecutiveShifts` di fila. **Due turni sono «di fila»** quando li separano meno di `breakShifts × shift_minutes`: una pausa più
     corta non conta come pausa, e con `breakShifts` a 0 non c'è limite;
   - **chi prima**, nell'ordine del design: chi ha almeno **il rating preferito** del tipo della postazione (`PreferredFor`; un tipo senza
     preferito non mette nessuno prima); **l'esperienza** — quante connessioni ha aperto **su quel nominativo** negli ultimi
     `experienceMonths`, e a parità **su quel tipo** (il tipo di ogni nominativo lo dà la directory, non l'archivio, come dice l'handoff di
     E10b): «più spesso» sono le volte, non i minuti —; **la penalità** più bassa; **meno turni** in questo roster; **chi si è candidato
     prima**; e per ultimo **il VID più basso**, perché due disponibilità date nello stesso istante non lascino l'ordine al database;
   - **l'ordine dei turni**: prima quelli con meno candidati che **possono** (le regole che non dipendono dagli altri turni: la
     disponibilità e i rating), poi il più presto, poi il nominativo, poi la postazione: lo stesso ordine a ogni giro;
   - **l'esperienza conta solo se l'archivio ha risposto per ogni candidato**: altrimenti (`atcData: none`, un archivio che non si legge)
     il criterio non c'è per tutto il roster, e il perché lo dice. Mai mezzo roster con l'esperienza e mezzo senza;
   - **la penalità** è quella del §4.5, `peso × no-show ÷ (fatti + no-show + 2)`, dai turni degli **altri** eventi (`Attended` e
     `NoShow`; i giustificati e i proposti non contano); il peso è 1, il predefinito di `noShowWeight`, finché E13b non porta
     l'impostazione. Fino a E13b nessuno ha no-show: zero per tutti.
4. **Il perché sta sul turno**, scritto alla proposta (`reason`, JSON): quanti potevano prendere quel turno, **che cosa ha deciso** —
   l'unico che poteva, il rating preferito, l'esperienza, la penalità, meno turni, chi si è candidato prima, il VID — e i numeri del
   controllore scelto (il preferito sì o no, le connessioni sul nominativo e sul tipo, la penalità, i turni che aveva già). **Dati, non
   parole**: la pagina li scrive nella lingua sullo schermo (Carmine sulla #255). Rifatto alla lettura sarebbe un altro perché: i rating e
   le sessioni cambiano dopo la proposta. Un turno scritto dallo staff non ne ha (`Staff`); **un turno proposto che lo staff cambia diventa
   `Staff` e perde il perché**, che resta nell'audit.
5. **Quando si propone**: `events-roster` ogni quarto d'ora (ai minuti 3, 18, 33 e 48, in UTC, e il trigger lo dice), per un evento
   **pubblicato**, con un roster, non annullato, non finito, la cui chiusura è passata (inizio − `applicationsCloseDays`) e che non ha
   ancora `roster_proposed_at`: **una volta**. Ogni evento in una transazione sua, sotto il blocco della sua riga (`FOR UPDATE`), riletta
   prima di scrivere: due processi, o due giri, non lo propongono due volte. `roster_proposed_at` si scrive **con i turni, nella stessa
   transazione**, senza toccare la versione della riga dell'evento (un editor aperto sull'evento non riceve un 409) e senza una riga
   dell'audit dell'evento: è il segno del job. **Una bozza alla chiusura si propone quando la si pubblica** (nessuno ha potuto darle la
   disponibilità prima). Un evento senza postazioni alla chiusura si propone vuoto: le postazioni aperte dopo sono scoperte, e lo staff
   le riempie a mano. Il giro scrive la sua riga di `hub_jobs_log` con `FinishedAt` a ogni uscita (E10j). **E `events-release` dice anche
   lui UTC** nel suo trigger, come l'handoff di E10j chiedeva alla prima fase del modulo.
6. **La correzione** (`EventAtc.Edit`) è **una risorsa del motore CRUD**, `/api/events/atc/shifts`, invece dei verbi scritti a mano che il
   §7.2 permette: «aggiungi» crea, «sposta» cambia — il controllore, le ore, la postazione dello stesso evento —, «togli» elimina. Meno
   codice, e l'unico handler, il guardiano e l'audit come per le postazioni. **Da quando**: dalla proposta (`roster_proposed_at`), **fino
   alla fine** dell'evento, mai su un evento annullato; dopo la pubblicazione (E12) uguale, con le mail. **I rifiuti**: chi non è mai
   entrato nell'hub (`hub_users`), le ore fuori dalla postazione o al contrario, un turno che tocca un altro turno della stessa postazione
   (una postazione ha un controllore alla volta), una postazione di un altro evento, il roster non ancora proposto. **Tutto il resto del
   §4.3 è un avviso**, letto sul roster: non disponibile per tutto il turno (o mai candidato), sotto il rating minimo delle impostazioni,
   sotto il minimo della postazione (con le parole del nucleo: togliere la FRA su IVAO, `atcPositions.belowMinimum`), sotto il preferito, un
   altro turno nelle stesse ore, oltre i turni di fila. Gli avvisi valgono anche per i turni proposti: una postazione la cui FRA è cambiata
   dopo la proposta lo dice, e **un turno dato sotto il preferito perché non c'era nessuno** porta «sotto il preferito» (il proponente ha
   ripiegato, e lo staff lo vede).
7. **Il roster si legge per postazione e ora** con **una lettura scritta a mano**, `GET /api/events/atc/events/{id}/roster`: i turni e i
   pezzi scoperti insieme, per nominativo e ora, con il controllore (`col.person`), il suo rating, l'origine, il perché, gli avvisi e, per
   uno scoperto, quanti potrebbero prenderlo. Gli scoperti non sono righe (lettura 2), quindi la lista del motore non li dà; e l'unico
   handler è chiesto **su ogni postazione**, con il suo FIR, come per i candidati: lo staff di un FIR legge il roster delle sue
   postazioni. È la **sesta lettura scritta a mano** dell'ATC, uno scostamento dal §7.2 come le cinque di E11a; i verbi restano del motore.
8. **Le postazioni con dei turni**: il nominativo non cambia (i turni ne hanno copiato il FIR) e le ore non si stringono lasciando un turno
   fuori (`events:errors.positionHasShifts`); **eliminare una postazione porta via i suoi turni**, ognuno con la sua riga dell'audit,
   **fino all'inizio dell'evento**; dall'inizio una postazione con turni non si elimina: i turni sono il registro di affidabilità (c3).
   **L'interruttore del roster** non si spegne con dei turni (si aggiunge ai controlli di E11a) e **un evento con turni non si elimina**
   (una riga di un membro lo nomina): si annulla.
9. **La cancellazione dei dati di una persona**, nella fase che crea le colonne: i turni di un evento **non ancora finito si cancellano**
   (il pezzo torna scoperto); quelli di un evento **finito restano** sotto lo pseudonimo, senza `attendance_note` e senza `reason` (parole
   dello staff sulla persona e numeri suoi), segnati `[ErasedWith(nameof(ControllerVid))]`, così un turno tolto prima della cancellazione
   non li lascia nell'audit. Le quattro colonne di persona (`controller_vid`, `attendance_by`, `created_by`, `updated_by`) entrano in
   `ErasureTests`. **«Duplica» non copia i turni** (righe di membri): la copia ha il suo roster alla sua chiusura.
10. **Sulle pagine**: il roster sta nel pannello dell'ATC (la scheda «ATC» dell'evento e la pagina dell'ATC di un evento), sotto le
    postazioni: prima della proposta una frase dice quando sarà proposto; dopo, la lista generata con «Aggiungi» su uno scoperto e «Sposta»
    e «Togli» su un turno, a chi scrive l'ATC dell'evento. Il turno ha il suo form generato — la postazione, le ore, il VID del controllore
    — e sotto i candidati della postazione (la lista di E11a). **Il membro non vede ancora niente**: la pubblicazione, la pagina del roster
    e i turni in `/events/mine` sono di E12.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Gli scoperti come righe senza controllore | una postazione nuova, spostata o tolta dopo la proposta, o una durata del turno cambiata, li lascerebbe sbagliati senza una regola in più che li rifaccia |
| Il perché rifatto alla lettura | sarebbe il perché di oggi, non quello della proposta: i rating e le sessioni cambiano |
| Il perché come testo scritto dal server | parole fatte in C#: Carmine le ha scartate per le liste sulla #255 |
| L'esperienza a minuti | «chi apre più spesso» (c1) sono le volte; i minuti premierebbero una sola sessione lunga |
| L'esperienza dal tracker di IVAO con `atcData: none` | il design §9.3 dice che senza l'archivio il criterio non c'è; dodici mesi di sessioni per candidato sono molte chiamate per un criterio di ordine |
| I verbi della correzione scritti a mano | il motore li dà con l'handler, il guardiano e l'audit; il §7.2 li permetteva, non li chiedeva |
| Due liste, i turni del motore e gli scoperti a parte | il roster si legge per postazione e ora, e uno scoperto in fondo a un'altra lista non è «in evidenza» |
| La correzione anche prima della proposta | le disponibilità cambiano fino alla chiusura, e il proponente dovrebbe rispettare turni scritti a mano prima di lui |
| Proporre anche una bozza alla chiusura | nessuno ha potuto darle la disponibilità: tutti scoperti; pubblicata, si propone al giro dopo |
| I turni di una postazione eliminata lasciati orfani | un turno senza la sua postazione non dice né il nominativo né il FIR |

## 4. Che cosa si tocca

Solo il modulo: `EventAtcShift.cs`, `Atc/` (il proponente, il job `events-roster`, i turni con il motore, il roster con gli avvisi), la
migrazione `AddAtcShifts` (dopo `AddAtcPositionsAndAvailability` di E11a), `AtcPositionEndpoints` (le postazioni con turni),
`EventSaving` (l'interruttore e l'eliminazione), `EventsPersonalData`, `EventsModule` (il job, e UTC sul trigger di `events-release`); le
pagine in `web/src/modules/events/` (i candidati di una postazione in un file loro, `screens/candidates.tsx`, per mostrarli anche sotto il
form di un turno); le parole del modulo; `docs/FORKING.md`. Nessun file del nucleo, se non `schema.d.ts` generato e **le quattro colonne di
persona in `ErasureTests`** (il caso che le regole di tutte le fasi prevedono).

**L'e2e** (`full/events-roster.spec.ts`) non sposta un turno: il banco non lancia un job a comando (un endpoint del banco è del nucleo, come
per il promemoria di E6b), quindi prova quello che non dipende dall'orologio — la sezione del roster prima della proposta, un turno rifiutato
prima della proposta, i candidati sotto il form —, e «lo staff sposta un turno» lo provano `EventsRosterTests` e il banco della fase.

## 5. Le domande a Carmine

Pubblicate nell'issue #259 ([domande][q]), con l'assenso di dalberone.

1. **`atcData` di IT** (la domanda in apertura di E11b, `10`, E0, «Trovato», punto 5). Oggi `config/division.json` non ha `atcData`: IT
   gira con `none`, e **la proposta del roster non ha il criterio dell'esperienza** (gli altri valgono uguali, e il perché di ogni turno lo
   dice). Con `"atcData": { "source": "vipi" }` e `ConnectionStrings:AtcData` nei segreti dell'installazione l'esperienza c'è, **senza
   cambiare codice**: l'archivio è già nel nucleo (M2, E10b). La scelta è di configurazione, ed è tua: (A) **IT passa a `vipi` prima del
   primo roster vero**, quando l'utente di sola lettura della vista su Plesk è pronto (la nota `2026-09-25-m2-review`, punto 1, lo aspettava
   per i tour); (B) IT resta a `none`, e i roster si propongono senza esperienza finché non decidi altrimenti. **Raccomandata la (A)**: il
   criterio è uno dei cinque che Carmine ha chiesto (c1, «le postazioni che apre più spesso»), e la vista serve anche ai tour (gli ATC
   contattati) e a E13a (la presenza nei turni, che senza l'archivio ricade sul tracker). Il codice di E11b funziona con tutte e due.
2. **Sì alle dieci letture di §2?** In particolare:
   - **(2)** gli scoperti calcolati alla lettura, non righe;
   - **(3)** «di fila» come meno di `breakShifts` turni di pausa fra due turni, l'esperienza a connessioni (sul nominativo, poi sul tipo) e
     solo se l'archivio risponde per tutti, il VID come ultimo criterio;
   - **(4)** il perché come dati sul turno, perso quando lo staff lo cambia;
   - **(5)** una bozza alla chiusura proposta quando la si pubblica;
   - **(6)** la correzione con il motore CRUD, dalla proposta alla fine dell'evento, con i rifiuti e gli avvisi di quella lettura;
   - **(7)** **una lettura scritta a mano**, il roster, la sesta dell'ATC;
   - **(8)** una postazione con turni che li porta via fino all'inizio dell'evento, e dopo non si elimina.

   **Raccomandato: sì a tutte.**

**Risposta di Carmine alla domanda 1** (13 ottobre 2026, [sull'issue #259][a]): **(A)**, come raccomandato. La divisione passa a `vipi`
prima del primo roster vero, **quando sull'host esiste l'utente di sola lettura della vista**: lo si crea su Plesk, che il maintainer non
raggiunge da sé, e finché non c'è la divisione resta a `none` e i roster si propongono senza il criterio dell'esperienza. Il codice deve
funzionare nei due modi, come fa; è configurazione e segreti dell'installazione, niente da cambiare in E11b.

**Risposta di Carmine alla domanda 2** (14 ottobre 2026, [sull'issue #259][a2], dopo la revisione della #262, con le quattro cose che il
revisore gli ha portato):

- **Letture 1, 2, 4, 7, 9 e 10: sì**, come scritte.
- **Lettura 3, con una condizione — l'archivio che non risponde alla chiusura**: accettata com'è. Una lettura dell'archivio che fallisce —
  «nessun archivio» e «non si legge» sono lo stesso `null` di `SessionsOfAsync` — lascia tutto il roster proposto senza il criterio
  dell'esperienza, ed è proposto una volta: lo staff corregge a mano. Nessun cambio al contratto dell'archivio del nucleo, ora.
- **Lettura 5, con una condizione — l'evento spostato dopo la proposta**: una volta proposto il roster, **le disponibilità restano chiuse
  anche se l'inizio dell'evento si sposta più tardi** (la chiusura non segue più l'inizio), e niente propone di nuovo: lo staff corregge a
  mano. Nessuna proposta nuova sopra le correzioni dello staff.
- **Letture 6 e 8, con due condizioni**: **dall'inizio dell'evento un turno non si toglie più**, come una postazione con turni non si
  elimina più: i turni sono il registro di affidabilità, e il turno di un no-show deve esserci quando E13 lo conta; **un controllore su due
  postazioni alle stesse ore è un rifiuto della correzione**, non un avviso: non può succedere. I turni del controllore in **altri** eventi
  alle stesse ore restano un avviso, e la proposta non deve guardarli.

## 6. Dopo la revisione

I rilievi del revisore sulla #262 ([rilievi][r]), che stanno a parte dalla risposta, e dove sono entrati:

1. **Due correzioni insieme dello stesso pezzo** potevano mettere due controllori sulla stessa postazione alle stesse ore (il controllo di
   `shiftTaken` era una lettura senza blocco) → **sotto il blocco della riga dell'evento**, `FOR UPDATE`, come le prenotazioni dei piloti,
   gli slot privati e la proposta: `Atc/ShiftLock.cs` apre una transazione `READ COMMITTED` nei controlli del salvataggio (`BeforeSave`),
   il salvataggio del motore scrive il turno e il suo audit dentro di lei, e `AfterSave` la chiude; un rifiuto non scrive niente (la
   transazione se ne va con il contesto della richiesta). Un test con il blocco tenuto dal test (il modello delle prenotazioni).
2. **Un evento che fallisce fermava il giro** → il suo fallimento si registra nel log, il giro lo nomina nel suo messaggio e lo lascia al
   giro dopo (la sua transazione è tornata indietro, quindi è ancora dovuto), e gli altri si propongono.
3. **La frase di uno scoperto** («potrebbero prenderlo, ma sono in altri turni») poteva dire occupato chi era libero → la riga porta anche
   **quanti sono liberi** di prenderlo (`Free`: possono, e nessun loro turno lo tocca né supera i turni di fila), e la frase dice i liberi,
   o gli occupati, o nessuno.
4. **La penalità** era scritta con il punto anche in italiano → con il formato dei numeri della lingua sullo schermo (`Intl.NumberFormat`).
5. **La pagina offriva quello che il server rifiuta** → **ogni riga del roster dice se il lettore può scriverla** (`Editable`, l'unico
   handler chiesto sul turno, o su un turno di quel pezzo per uno scoperto: lo staff di un FIR le sue, nessuno il proprio turno; niente su un
   roster chiuso), e la lista delle postazioni lo dice per postazione: **il form di un turno** offre le postazioni scrivibili, niente
   «Salva» su un evento annullato o finito né prima della proposta (con la frase che dice quando arriva), e niente «Togli» dall'inizio.
6. **I test che mancavano**: lo staff di un FIR che sposta un turno su una postazione di un altro FIR, `positionOfAnotherEvent`,
   `positionUnknownForShift` e la corsa del punto 1.

## 7. Le condizioni di Carmine nel codice

- **Le disponibilità chiuse per sempre dopo la proposta** (lettura 5): `AtcRules.IsClosedForGood` — passata la chiusura, o proposto il
  roster —, letto da `IsOpen` e `IsWithdrawable`, quindi dalla pagina, dal dare, dal cambiare e dal ritirare.
- **Dall'inizio dell'evento** (letture 6 e 8): un turno non si toglie (`events:errors.shiftAfterStart`), e — **lettura dello «sposta»,
  raccomandata e chiesta, vedi sotto** — non passa a un altro controllore; le ore e la postazione si cambiano ancora. La pagina non offre
  «Togli» dall'inizio.
- **Un controllore su due postazioni dello stesso evento alle stesse ore** è rifiutato (`events:errors.controllerBusy`, sotto lo stesso
  blocco); **i suoi turni in altri eventi alle stesse ore** sono l'avviso «un altro suo turno nelle stesse ore», che il roster ora legge
  anche fuori dall'evento; i turni di fila si contano nell'evento.
- **Lo «sposta» dall'inizio — la scelta di dalberone, la (B)**: la regola segue la risposta di Carmine com'è scritta, dall'inizio si rifiuta solo di **togliere** un turno; **spostarlo resta libero** (il controllore, le ore, la postazione). Uno spostamento dopo l'inizio che cambia il controllore toglie la riga del primo da quello che E13 legge, anche se l'audit la tiene; dalberone ha scelto la (B) invece di rifiutare anche quello (la (A)). Il revisore e Carmine possono chiedere di più sulla PR.

## 8. I resti della terza lettura di E11a

Due punti piccoli della terza lettura della #256 ([rilievi][e]), che con E11a unita toccano a questa fase:

1. **`events:errors.availabilityChanged` non arrivava mai sullo schermo**: un 409 la pagina lo dice con la frase del nucleo
   (`errors.conflict.title`, in `useProblemDetails`) prima di leggere il titolo del server. **La chiave del modulo è tolta**, e il 409 di una
   disponibilità porta la frase del nucleo, che è quella che il membro legge; far preferire alla pagina il titolo del server sarebbe un
   cambio del nucleo.
2. **Una bozza la cui fine è passata** diceva `Draft` come stato, e la pagina offriva «Salva» che il server rifiuta: **`isClosed` chiede
   anche la fine**.

## Da portare nel piano

- **§10 / §15** (le integrazioni facoltative e i punti aperti dell'installazione): la divisione passa a `atcData: vipi` prima del primo
  roster vero, quando l'utente di sola lettura della vista esiste sull'host; fino ad allora `none`, e il roster si propone senza
  l'esperienza (risposta 1 di Carmine sull'issue #259).

- **§9.2, riga Events** e design **§7.2** (che non si tocca): il roster si legge per postazione e ora con **una lettura scritta a mano**
  (la sesta dell'ATC), accettata come scostamento accanto alle cinque di E11a; la correzione è una risorsa del motore.
- **§9.7, collaborazioni ATC↔Events**: il turno copia il FIR dalla sua postazione; lo staff corregge dalla proposta alla fine dell'evento,
  mai un turno suo, sotto il blocco della riga dell'evento; un'aggiunta fuori dalle regole è un avviso, ma un controllore su due postazioni
  dello stesso evento alle stesse ore è un rifiuto (e in altri eventi un avviso); dall'inizio un turno non si toglie né passa a un altro
  controllore; proposto il roster, le disponibilità restano chiuse anche se l'evento si sposta, e niente propone di nuovo; un archivio che
  non risponde alla chiusura lascia il roster senza esperienza; gli scoperti sono i pezzi delle postazioni che nessun turno tocca.
- **§11, «Privacy dei membri»**: i turni di un evento non ancora finito si cancellano con i dati della persona; quelli di un evento finito
  restano senza la nota della presenza e senza il perché.
- **§16 (job)**: `events-roster` propone una volta, sotto il blocco della riga dell'evento, e il suo segno non cambia la versione della
  riga.
