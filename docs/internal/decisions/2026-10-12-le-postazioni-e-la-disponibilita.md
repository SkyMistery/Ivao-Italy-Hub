# Le postazioni di un evento e la disponibilità dei controllori (E11a)

**Data:** 12 ottobre 2026 — fase E11a di M4 (la prima di M4b), PR #256
**Stato:** **decisa** (Carmine, 12 ottobre 2026, in chat al master, che ha pubblicato la risposta sulla #256 su sua istruzione:
[risposta][a]): **sì alle dieci letture**, come raccomandate. La domanda era il primo commento della PR ([domanda][q]). Le correzioni della
revisione ([rilievi][r], [seconda lettura][r2]), che stanno a parte dalla risposta, sono in §6; il punto 2 era **una seconda domanda**
([domanda 2][q2]), **decisa** da Carmine il 13 ottobre 2026, in chat al master che l'ha pubblicata su sua istruzione ([risposta 2][a2]):
**la (B)**.
**Regola applicata:** `CLAUDE.md` §5. Le tabelle, i permessi, le regole del roster e i predefiniti sono **decisi** (design `09-design-m4.md`
§1.7, §1.12, §4.1, §4.2, §6; note `2026-09-29-il-roster-atc`, `2026-09-29-chi-lavora-sugli-eventi`, `2026-09-29-le-impostazioni-degli-eventi`);
la forma per lo staff di un FIR è quella scritta in E2 («Le due verifiche del design §6.3», `10`, E2). Questa nota registra **le letture**
che la fase ha dovuto fare dove il design non dice: casi **(a)** e **(b)**, con cinque letture scritte a mano che sono uno scostamento dal
design §7.2 («nessuna eccezione dichiarata al motore CRUD»), come la lettura dei preset di E3a. Niente del nucleo: si usano `IHasFir`, i
grant al team di un FIR, `[WithdrawnByStakeholder]`, `IAtcPositionDirectory` e il vocabolario dei rating così come sono.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6129863697
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6131047046
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6130536153
[q2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6132606484
[a2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6146395160
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/256#issuecomment-6146431124

## 1. Che cosa serviva decidere

Il design dice le due tabelle (§1.7), chi le scrive (§6.2: AOD e staff dei FIR, `EventAtc.*`), fino a quando un membro dà la sua
disponibilità e con quale rating (§4.2), e — dalla verifica di E2 — che lo staff di un FIR legge l'intestazione di un evento da un endpoint
dell'area e le disponibilità **per postazione**. Non dice: dove lavora chi tiene l'ATC e non gli eventi; da dove il form sceglie una
postazione e le impostazioni un rating; su quali eventi si aprono le postazioni e come stanno nella finestra dell'evento; che cosa è una
finestra valida; che cosa legge lo staff di una disponibilità; che cosa ne vede la pagina pubblica e `/events/mine`; quando le righe nuove
entrano nella cancellazione dei dati di una persona e in «Duplica».

## 2. Le letture

1. **Dove lavora chi tiene l'ATC senza gli eventi** (lo staff di un FIR; un grant a un VID su `EventAtc`). Una sezione del back office,
   **«ATC degli eventi»** (`/staff/events/atc`, voce di menu con `EventAtc.View`), con gli eventi che hanno un roster e non sono finiti
   (bozze comprese: le postazioni si aprono prima della pubblicazione), e la **pagina dell'ATC di un evento** (`/staff/events/atc/{id}`),
   con le sue postazioni. Le intestazioni vengono da **due letture dell'area** (`GET /api/events/atc/events` e `/{id}`), con `EventAtc.View`
   chiesto senza riga: la forma di E2, che diceva anche che cosa comporta — chi tiene l'ATC da qualche parte legge l'intestazione (titolo,
   stato, date, chiusura, durata del turno) di **ogni evento con un roster**, mai una riga dei membri. Per chi legge gli eventi la stessa
   lista è la scheda **«ATC»** della pagina dell'evento, e la sezione lo porta lì.
2. **Le due liste da cui si sceglie**: le postazioni della divisione (`GET /api/events/atc/division-positions`, `EventAtc.Edit`), per il
   nominativo di una postazione, e i rating ATC del vocabolario (`GET /api/events/atc/ratings`, `Events.ManageSettings`), per
   `minimumAtcRating`. Come le due del training (`/api/training/ratings`, `/positions`): un modulo non importa un altro, e il nucleo non ha
   un endpoint dei rating. Con la lettura dei candidati (punto 7) sono **cinque letture scritte a mano**, oltre ai tre verbi del membro.
3. **Le postazioni solo su un evento con un roster** (`has_roster`): altrimenti `events:errors.eventHasNoRoster`. L'interruttore del roster
   **non si spegne** mentre l'evento ha postazioni o disponibilità (`events:errors.hasRosterRows`), come gli slot privati (E7) e l'evento in
   presenza (E16); **un evento con disponibilità non si elimina** (si annulla), e le sue postazioni se ne vanno con lui, ognuna con la sua
   riga dell'audit.
4. **Una postazione nell'evento**: senza ore, **la finestra dell'evento** (design §1.7); con le ore, **dentro** la finestra; lo stesso
   nominativo due volte solo in ore che **non si sovrappongono** (due che si toccano sì: la torre fino alle 19 e di nuovo dalle 19). Il
   nominativo è una postazione della divisione (`IAtcPositionDirectory.FindAsync`), scritto come lo scrive il nucleo; **il FIR lo copia il
   server dalla directory prima di chiedere il permesso** (mai il client: l'unico handler e il guardiano lo chiedono su quel FIR); una
   postazione il cui nominativo non cambia tiene il FIR con cui è stata scritta (anche se la directory nel frattempo lo ha spostato o non
   lo elenca più), così si corregge o si elimina sullo stesso FIR (§6, punto 7). **La nota è dello staff**,
   in una lingua, e non va sulla pagina pubblica. ⚠️ Se l'evento cambia orari le postazioni **non si spostano**: lo staff le corregge (il
   form non rifiuta il cambio dell'evento).
5. **La disponibilità** (§4.2): la dà un membro che ha fatto il login, su un evento che vede, con un roster, non annullato, con almeno una
   postazione (senza, non c'è niente per cui essere disponibili), **fino a inizio − `applicationsCloseDays`**, con un rating ATC almeno
   `minimumAtcRating` (nessun minimo: ogni membro, anche senza rating ATC). **Le finestre**: almeno una, al più 12, ognuna che finisce dopo
   che comincia, dentro l'evento, nessuna sovrapposta a un'altra (che si toccano sì); ogni rifiuto sul campo della sua finestra. **La nota**:
   del membro, in una lingua, al più 500 caratteri. **La cambia** fino alla chiusura di un evento non annullato, **la ritira** fino alla
   chiusura, anche di un evento annullato; **dopo la chiusura niente**: il roster è stato proposto da lì (§4.3).
6. **Ritirare cancella la riga** (`[WithdrawnByStakeholder]`), con le sue finestre. **La regola di Carmine sulla #232** — il segno solo su
   una riga che non porta decisioni dello staff sul membro — è rispettata: **la decisione del roster è il turno** (`evt_atc_shifts`, E11b),
   una riga sua; sulla disponibilità niente si approva, si rifiuta o si conferma, lo staff la legge. E dopo la chiusura nessuno la ritira,
   così quello che il proponente ha letto resta. **Le finestre sono righe figlie** (design §1.7), del membro come la disponibilità: portano
   il controllore e l'evento, e hanno gli stessi segni, così il guardiano le legge sulla riga.
7. **Lo staff legge le disponibilità per postazione** (la forma di E2): sulla pagina di una postazione, chi ha **una finestra che si
   sovrappone** alle sue ore — il controllore con `col.person`, il suo rating ATC con la sigla del vocabolario, le sue finestre in UTC (tutte,
   perché la serata si legga intera), se copre la postazione **per tutto il tempo**, la nota, quando l'ha data —, il primo arrivato primo; la
   lista delle postazioni dice **quanti** sono disponibili su ognuna. L'unico handler è chiesto con `EventAtc.View` **sulla postazione**,
   con il suo FIR: lo staff di un FIR legge i candidati delle sue. La lista intera delle disponibilità di un evento non c'è ancora: con il
   roster di E11b si legge per postazione e ora.
8. **La pagina dell'evento** (§7.1): **le postazioni a tutti** — nominativo, nome, ore —, fino a quando si dà la disponibilità, e il form;
   sotto il minimo, la frase dice **quale rating** serve (dal vocabolario); a un visitatore, «accedi». **`/events/mine` non mostra ancora le
   disponibilità**: le porta E12 con i turni (§7.1 la vuole lì; E11a la mette sulla pagina dell'evento, dove si dà).
9. **Le righe nuove nella cancellazione e in «Duplica»**, nella fase che le crea (non in E15b): la disponibilità di un evento **non ancora
   finito si cancella** con le finestre; quella di un evento **finito resta** sotto lo pseudonimo (nella disponibilità e nelle finestre),
   **senza la nota**, che sono parole sue — la regola delle iscrizioni (E16) —; e la nota porta `[ErasedWith(nameof(ControllerVid))]` (E10n),
   così una disponibilità ritirata prima della cancellazione non la lascia nell'audit. «Duplica» copia **le postazioni**, con le ore spostate, e
   chiede `EventAtc.Edit` per scriverle; mai una disponibilità.
10. **I limiti delle impostazioni** (§1.12 dà i predefiniti, non i limiti): `shiftMinutes` 15–720; `minimumAtcRating` un rating ATC del
    vocabolario, o nessuno; `maxConsecutiveShifts` 1–96, `breakShifts` 0–96; `applicationsCloseDays` e `rosterPublishDays` 0–60, e
    **`rosterPublishDays` non più di `applicationsCloseDays`** (il roster si pubblica dopo la chiusura, «lo staff ha un giorno per
    correggere», nota `il-roster-atc` §2.5); `experienceMonths` 1–60. `shift_minutes` dell'evento, quando c'è, nello stesso intervallo.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lo staff dei FIR nella pagina dell'evento, aprendo `Events.View` | darebbe a ogni capo FIR il testo, gli scali e le liste di ogni evento: il design §6.2 gli dà solo l'ATC |
| Le intestazioni dalla lista generata degli eventi | con `own` il team di un FIR non ha un dipartimento, e l'evento non dice un FIR: la lista gli risponde 403 (E2) |
| Le finestre in una colonna JSON della disponibilità | meno codice, ma il design le vuole righe figlie, e il roster di E11b le legge per ora |
| La lista intera delle disponibilità con il motore CRUD | con `own` è chiusa allo staff di un FIR (la disponibilità non dice un FIR): per postazione, come ha deciso E2 |
| Una postazione anche su un evento senza roster | non servirebbe a niente: le postazioni esistono per il roster e la disponibilità |
| Le postazioni che si spostano con l'evento quando ne cambiano gli orari | una postazione può coprire solo una parte della serata, e spostarla è una scelta dello staff; «Duplica» invece sposta tutto |
| Il nominativo scritto a mano, controllato al salvataggio | il form sceglie fra le postazioni della divisione, come il training sceglie le sue |
| Ritirare anche dopo la chiusura | il roster è già proposto: la riga sparirebbe sotto un turno; dopo la chiusura un controllore che non può più chiede di cedere il turno (E12) |
| La cancellazione delle disponibilità in E15b, come diceva l'handoff | una colonna di persona che nasce qui entra in `ErasureTests` qui, e la sua regola con lei |

## 4. Che cosa si tocca

Solo il modulo: `EventAtcPosition.cs`, `EventAtcAvailability.cs` (la disponibilità e le finestre), `Atc/` (le regole, le postazioni con
le cinque letture, la disponibilità dello staff e del membro), la migrazione `AddAtcPositionsAndAvailability`, `EventsSettings`,
`EventDtos` e `EventSaving` (il roster nel form dell'evento), `PublicEvents` (l'ATC sulla pagina), `EventDuplication`, `EventsPersonalData`,
`EventsPeople` (il rating ATC accanto al nome), `EventsModule`; le pagine in `web/src/modules/events/`; le parole del modulo. Nessun file
del nucleo, se non `schema.d.ts` generato, `docs/FORKING.md` e **le quattro colonne di persona in `ErasureTests`** (il caso che le regole
di tutte le fasi prevedono).

## 5. La domanda a Carmine

**Sì alle dieci letture di §2?** In particolare:

- **(1) e (2), cinque letture scritte a mano** — le intestazioni degli eventi con un roster (due), i candidati di una postazione, e le
  due liste da cui si sceglie —, uno scostamento dal design §7.2 come la lettura dei preset di E3a; e la sezione **«ATC degli eventi»** per
  chi tiene l'ATC senza gli eventi, che legge l'intestazione di ogni evento con un roster;
- **(4)** le postazioni **non si spostano** quando l'evento cambia orari;
- **(8)** le disponibilità in `/events/mine` arrivano **con i turni, in E12**;
- **(9)** la cancellazione delle disponibilità **ora**, e non in E15b.

**Raccomandato: sì a tutte.**

**Risposta di Carmine** (12 ottobre 2026, [sulla #256][a]): **sì alle dieci letture**, e in particolare sì alle cinque letture scritte a
mano e alla sezione «ATC degli eventi», sì alla lettura 4, sì alla lettura 8 (le disponibilità in `/events/mine` con E12), sì alla lettura 9
(la cancellazione qui).

## 6. Dopo la revisione

I rilievi del revisore sulla #256 ([rilievi][r]), che stanno a parte dalla risposta, e dove sono entrati:

1. **«Duplica» e le postazioni senza un test** → un test d'integrazione: la copia ha le postazioni con le ore spostate, chi scrive gli eventi
   senza `EventAtc.Edit` è rifiutato e niente si scrive, ed eliminare un evento ne elimina le postazioni, ognuna con la sua riga dell'audit.
2. **Le finestre dei candidati scritte come testo in C#** → **Proposta, una seconda domanda a Carmine** ([domanda 2][q2]): la lista generata
   non ha una colonna per più istanti (`col.date` ne disegna uno, `col.list` parole), e un candidato ha da 1 a 12 finestre. **(A)** una
   colonna nuova nella lista del nucleo (`col.ranges`), con una fase del nucleo e la sua nota prima; **(B)** solo nel modulo: per ogni
   candidato gli istanti di quello che copre **dentro la postazione** (`availableFromUtc`, `availableToUtc`, due `col.date`), con «per tutto
   il tempo» e il numero delle finestre — i buchi fra una finestra e l'altra non si vedono più, solo «non per tutto il tempo»; **(C)** il
   testo tenuto, scritto dal server in UTC e nell'ora della divisione — parole fatte in C#, la forma che Carmine ha già scartato per le liste
   sulla #255. **Raccomandata la (B)**: ogni orario di quella pagina lo disegna la lista, negli stessi due orologi; la (A) se un giorno lo
   staff vorrà i buchi (il roster di E11b, per postazione e ora, mostra comunque la copertura). **Decisa la (B)** ([risposta 2][a2]):
   ogni candidato manda `availableFromUtc` e `availableToUtc` — la prima delle sue finestre dentro la postazione tagliata alla sua
   apertura, l'ultima tagliata alla sua chiusura —, `wholePosition` e il numero delle finestre che la toccano; due colonne `col.date`, nessun
   testo fatto in C#. Niente colonna nuova nel nucleo ora; la (A) solo se un giorno lo staff vorrà i buchi; non la (C).
3. **Le letture del membro attraverso il filtro dell'evento** → la disponibilità **propria** si legge e si ritira con l'evento letto oltre la
   visibilità (un evento che il membro non vede più, perché il suo «visibile dal» è stato spostato, lascia ritirare fino alla chiusura); darla
   chiede ancora un evento che il membro vede.
4. **Due cambi insieme dello stesso membro** → il secondo riceve **409** («è cambiata in un'altra finestra: ricarica la pagina per vederla
   com'è ora», `events:errors.availabilityChanged` — la pagina non la rilegge da sé, e la frase lo dice), non più 404; provato da un test
   deterministico, con una transazione del test che tiene la finestra che la richiesta sta per sostituire (come le corse di E6a).
5. **Lo stesso nominativo in ore sovrapposte, due creazioni insieme**: il controllo è una lettura, e due salvataggi nello stesso istante
   passano tutti e due. **Detto e lasciato**: nessun vincolo della MariaDB esprime «intervalli che non si sovrappongono» su una stessa
   chiave senza un blocco sulla riga dell'evento, e per due postazioni uguali aperte nello stesso secondo dallo staff dello stesso FIR non vale
   il prezzo; lo staff ne elimina una, e il roster di E11b le tratta come due postazioni.
6. **Una postazione su un evento annullato o finito** → rifiutata, con parole della postazione (`events:errors.positionOnCancelledEvent`,
   `events:errors.positionOnEventOver`: la seconda lettura, punto 2); **la pagina non offre «Salva», «Elimina» né «Nuova postazione»** su
   un evento annullato o finito e dice perché (`events:atc.closedEvent`), perché il rifiuto del server sta sul campo nascosto dell'evento e
   la pagina non lo disegnerebbe (la seconda lettura, punto 1; guardato sullo schermo del banco). Eliminarla dall'API resta possibile.
7. **Su `DELETE` il FIR riscritto dalla directory** → una postazione il cui nominativo non cambia **tiene il FIR con cui è stata scritta**,
   in modifica e in eliminazione: l'handler e il guardiano lo chiedono sullo stesso FIR. Il FIR si copia quando si scrive il nominativo
   (lettura 4). ⚠️ **Che cosa ne segue** (la seconda lettura, punto 4): «Duplica» copia il FIR com'è, quindi una postazione duplicata anno
   dopo anno **tiene il FIR della sua prima scrittura** anche se la directory ha poi spostato il nominativo in un altro FIR, finché qualcuno
   non riscrive il nominativo; fino ad allora la vede e la scrive il team del FIR di prima.
8. **Al capo di un FIR offerte tutte le postazioni** → la lista da cui si sceglie (`/api/events/atc/division-positions?eventId=`) chiede
   l'unico handler su ogni postazione come sarebbe sull'evento, con il suo FIR: al capo di un FIR quelle del suo FIR, allo staff di un
   dipartimento tutte.

## Da portare nel piano

- **§9.2, riga Events** e design **§7.2** (che non si tocca): lo staff dell'ATC lavora anche da una sezione sua, «ATC degli eventi», con le
  intestazioni degli eventi con un roster; **cinque letture scritte a mano** dell'ATC accettate come scostamenti, accanto alla lettura dei
  preset (E3a) e alla pagina pubblica (E4).
- **§9.7, collaborazioni ATC↔Events**: una postazione è una postazione della divisione, con il FIR della directory scritto dal server; la
  disponibilità si dà dalla pagina dell'evento fino a inizio − `applicationsCloseDays`, con il rating minimo, si cambia e si ritira fino alla
  chiusura, e lo staff la legge per postazione.
- **Design §7.2, la scheda «ATC»** (che non si tocca): su una postazione, chi è disponibile con **da quando a quando dentro la postazione**
  (due istanti che la lista disegna), «per tutto il tempo» e il numero delle finestre — non le finestre una per una (risposta 2, la (B)).
- **§11, «Privacy dei membri»**: la disponibilità di un evento non ancora finito si cancella con i dati della persona; quella di un evento
  finito resta senza la nota.
