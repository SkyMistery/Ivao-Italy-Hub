# Gli slot sulla pagina dell'evento: il tipo principale, partenze e arrivi, il dettaglio (E5)

**Data:** 7 ottobre 2026 — fase E5 di M4 (gli slot pubblici e l'esportazione), PR #228, dopo la prova sul banco
**Stato:** **decisa** (Carmine, 7 ottobre 2026: [le sue risposte][a228b], pubblicate dal master sulla #228 su sua istruzione). **Sì ai
sette punti** come scritti — il punto 5 sostituisce, sapendolo, la lettura 8 della nota del 6 ottobre — e **un ottavo** sulla domanda
che il revisore gli ha girato ([la seconda lettura][v228b], «For the maintainer» n.7): `Hint` resta un pezzo di questa schermata (§12).
I sette erano sei comportamenti della pagina pubblica degli slot e del tipo di aereo che il design non dice, scelti da dalberone come chi
tiene il modulo guardando E5 sul banco di prova (la build di E5 sulla 5090: un evento su LIRF e LIMC, quattro slot incollati con una
rotazione, pubblicato), più un campo dell'esportazione che dalberone ha scelto su una domanda di Claude (§7); la domanda era andata a
Carmine con [un commento sulla #228][q228].
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: nessun meccanismo nuovo. Il tipo principale è il primo della colonna
`aircraft_types` di E5, senza migrazione; la lista pubblica viaggia nella lettura della pagina che c'è (`PublicEventDto.Slots`),
senza campi nuovi; il tooltip e il dialog sono di Atmosphere, l'icona di lucide. **Cambia la lettura 8** della nota
`2026-10-06-il-foglio-degli-slot-e-l-esportazione`, decisa: le rotazioni non sono più raggruppate in una tabella sola (§5).

[q228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6039543785
[a228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040717010
[v228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040474527

## 1. Un tipo principale

- Uno slot ha **un tipo principale**, e gli altri tipi ammessi si aggiungono quando lo slot si scrive. **Nessuna migrazione: il primo
  di `aircraft_types` è il principale.**
- La tabella incollata tiene `A320/A20N`: A320 principale, A20N ammesso anche lui. La pagina del caricamento lo dice fra i formati.
- Il form di uno slot ha **due campi, «Tipo principale» e «Altri tipi»** (gli altri scritti come nella tabella, `A20N/A321`), salvati
  come `[principale, ...altri]`: il principale scritto di nuovo fra gli altri resta il principale, una volta sola (`SlotValues.MainFirst`).
  Ogni rifiuto sta sul suo campo: obbligatorio e formato sul principale, formato e «troppi» sugli altri, e un tipo che il nucleo non
  conosce sul campo dove è scritto. `EventSlotWriteDto` cambia forma (`MainAircraftType` e `OtherAircraftTypes` al posto di
  `AircraftTypes`): lo scrive solo il form del back office, e la PR non è unita.
- L'esportazione tiene l'ordine: §7.

## 2. La colonna del tipo

- La tabella mostra **solo il tipo principale**, con un «+1» quando ce ne sono altri. Gli altri compaiono **al passaggio del mouse, al
  focus da tastiera e al tocco**, nel tooltip di Atmosphere («Ammessi anche: A20N»).
- Il tocco, perché un telefono non ha il passaggio del mouse: il tooltip di Atmosphere è quello di Radix, che si apre solo con un mouse
  che si muove e al focus. Il bottone che lo porta lo apre e lo chiude a ogni pressione, e guarda che cosa mostrava **quando la
  pressione è cominciata**: tra la pressione e il click Radix lo chiude e lo riapre, quindi lo stato al click non dice niente. Con un
  semplice «al click si rovescia» il secondo tocco non lo chiudeva: provato in Chromium col touch (§9).

## 3. Partenze e arrivi

- **Due tabelle, «Partenze» e «Arrivi», rispetto allo scalo dell'evento** (`is_arrival`). Ognuna va per l'orario allo scalo
  dell'evento — l'off block di una partenza, l'on block di un arrivo, lo stesso orario dell'ordine dell'esportazione — e mostra l'altro
  aeroporto («Per», «Da») e quell'orario soltanto. L'altro orario sta nel dettaglio (§6).

## 4. Una sezione per scalo

- **Con più scali dell'evento, una sezione per scalo**: un RFO su Fiumicino e Ciampino ha una sezione per Fiumicino e una per
  Ciampino, ognuna con le sue partenze e i suoi arrivi. Le sezioni vanno nell'ordine degli scali dell'evento; uno scalo che l'evento non
  elenca più va dopo, per codice.
- Uno slot sta nella sezione del suo scalo dell'evento (`event_airport_icao` più `is_arrival`). La pagina lo ricava da `isArrival` e dai
  due aeroporti, gli stessi da cui il server l'ha letto quando lo slot è stato scritto.
- **Il volo fra due scali dell'evento resta una partenza del primo**, come dice la lettura 3 della nota decisa: sta fra le partenze del
  suo scalo di partenza, e non fra gli arrivi dell'altro.

## 5. Le rotazioni

- **Ogni tratta resta nella sua tabella**: l'andata fra le partenze, il ritorno fra gli arrivi.
- Le tratte di una rotazione sono **segnate con un'icona** di lucide, `Repeat`. Al passaggio del mouse, al focus e al tocco l'icona dice
  **«Questo volo fa parte di una rotazione, apri per più dettagli»**, con l'inglese nel file `en` del modulo.
- Cambia la lettura 8 della nota decisa: le rotazioni raggruppate in una tabella sola, ognuna dove cade la sua prima tratta e con una
  riga che la nomina, non ci sono più.

## 6. Il dettaglio di uno slot

- **Una riga apre lo slot in sola lettura**, in un dialog di Atmosphere. Il dialog mostra:
  - **tutti i tipi ammessi**, il principale detto;
  - la partenza e l'arrivo con i loro orari;
  - lo stand;
  - libero o preso;
  - per una rotazione, **le sue tratte**, con questa segnata.
- E6b ci mette «Prenota»: il dialog mostra già tutti i tipi ammessi, quelli fra cui il pilota sceglierà.
- **Nessun endpoint nuovo e nessun campo nuovo**: tutto è già in `PublicEventDto.Slots`.

## 7. L'esportazione porta i tipi ammessi

- La richiesta diceva «l'esportazione tiene la lista in ordine», ma l'esportazione non portava la lista: solo `aircraft_icao`, il tipo
  che il pilota sceglie prenotando, vuoto finché lo slot è libero.
- Chiesto a dalberone (7 ottobre 2026): **sì a un campo nuovo, `aircraft_types`**. Sono i tipi ammessi con il principale per primo;
  sono vuoti su uno slot privato, il cui pilota dice il tipo (E7).
- È **un'aggiunta alla versione 1** del contratto: dentro una versione l'hub solo aggiunge (nota di E5 sul contratto, punto 9).
- Serve al Gate Manager per pianificare gli stand prima delle prenotazioni. Lo dice `docs/events-bookings-export.md`.

## 8. Non una regola

- «Un RFO sta su uno scalo solo» è un'abitudine di dalberone, non una regola: l'hub resta libero, e niente lo controlla.

## 9. Le scelte di Claude, e come sono provate

- Le colonne di una tabella: il nominativo, con il numero di volo sotto; l'aereo; l'altro aeroporto; l'orario allo scalo dell'evento;
  lo stand; lo stato.
- **Una tabella vuota non si mostra**: uno scalo con soli arrivi non ha una tabella «Partenze» vuota. Con uno scalo solo, le due tabelle
  stanno senza sezione.
- **Si apre l'intera riga con un click**, come chiesto e deciso (§6): su un telefono è il bersaglio che il pollice trova. Il nominativo
  è il bottone che la tastiera e chi legge lo schermo raggiungono, e **quando il dialog si chiude il focus torna a quel nominativo**: il
  dialog non ha un suo bottone che lo apre, e senza questo la tastiera finirebbe in cima a una pagina di centinaia di righe (la seconda
  lettura del revisore, punto 1). I bottoni dei tooltip non aprono la riga, e nemmeno **un click che chiude una selezione di testo** —
  un numero di volo da copiare (punto 5).
- **I test:**
  - vitest: `slotList.test.ts` (le sezioni, l'ordine, le tratte) ed `EventSlots.test.tsx` (le sezioni e le tabelle, il tipo con il suo
    tooltip al focus e al tocco, l'icona con il suo testo, il dialog);
  - la smoke `events-public.spec.ts`: due scali, il passaggio del mouse, il dialog, e **il tocco in Chromium con `hasTouch`** — il
    primo tocco apre, il secondo chiude, nessuno apre la riga;
  - il giro `full/events-slots.spec.ts` sul server vero;
  - l'integrazione dei tipi del form e del campo dell'esportazione.

## 10. Che cosa si è toccato

Solo il modulo, nessuna migrazione:
- il server: `Staff/SlotRules.cs` (`MainFirst`), `Staff/EventSlotEndpoints.cs` (il DTO, il validatore, il mapper e i rifiuti dei tipi in
  `SlotSaving`), `Export/BookingsExport.cs` (`aircraft_types`) e `Public/PublicEvents.cs` (un commento);
- il browser: `screens/EventSlots.tsx`, nuovo, `screens/public.tsx`, `screens/slotList.ts`, `schemas.ts`, `api.ts` e le parole;
- i test e `docs/events-bookings-export.md`.

## 11. La domanda a Carmine

Posta in inglese sulla #228 ([il commento][q228]); in italiano dice:

> Sette comportamenti della pagina pubblica degli slot e del tipo di aereo, che dalberone ha scelto guardando E5 sul banco di prova e
> che E5 ha scritto come raccomanda questa nota:
> 1. uno slot ha un tipo principale, il primo di `aircraft_types`: la tabella tiene `A320/A20N`, il form ha «Tipo principale» e «Altri
>    tipi»;
> 2. la colonna del tipo mostra il principale, e gli altri al passaggio del mouse, al focus e al tocco;
> 3. partenze e arrivi in due tabelle, rispetto allo scalo dell'evento;
> 4. con più scali una sezione per scalo, e il volo fra due scali dell'evento resta una partenza del primo;
> 5. ogni tratta di una rotazione nella sua tabella, segnata da un'icona che dice che fa parte di una rotazione;
> 6. una riga apre lo slot in sola lettura, con tutti i tipi ammessi e le tratte della sua rotazione, dove E6b metterà «Prenota»;
> 7. l'esportazione porta `aircraft_types`, i tipi ammessi con il principale per primo, come aggiunta alla versione 1.
>
> «Un RFO su uno scalo solo» resta un'abitudine, non una regola. Confermi?

## 12. La risposta di Carmine

7 ottobre 2026, [le sue risposte sulla #228][a228b] (autore `SkyMistery`, pubblicate dal master su sua istruzione):
- **sì ai sette punti**, come scritti; il punto 5 **sostituisce, sapendolo, la lettura 8** della nota del 6 ottobre (le rotazioni
  raggruppate in una tabella sola);
- **un ottavo**, sulla domanda del revisore se `Hint` sia un componente del modulo da dichiarare o un pezzo di questa schermata:
  **`Hint` resta un pezzo di questa schermata** finché niente altro ne ha bisogno. Il giorno in cui una seconda schermata vorrà un
  tooltip che si apre al tocco, è la decisione da portargli, **non una seconda copia**.

Il punto 8 della seconda lettura — un volo fra due scali dell'evento sta fra le partenze del primo e manca dagli arrivi del secondo — è
la sua risposta 4: niente da cambiare.

## Da portare nel piano

- design M4 §1.5: il primo tipo di aereo di uno slot è il principale; il form di uno slot ha due campi;
- design M4 §3.1: la cella `A320/A20N` dice il principale per primo;
- design M4 §7.1: la lista pubblica è per scalo, con partenze e arrivi, le tratte di una rotazione segnate e il dettaglio di uno slot
  (cambia la lettura 8 della nota decisa di E5);
- design M4 §7.4: `aircraft_types` nell'esportazione, aggiunta alla versione 1;
- il piano, dove dice che cosa è un componente (§8.3, la lista chiusa): un tooltip che si apre al tocco è oggi un pezzo della pagina
  degli slot (`Hint`), e una seconda schermata che lo volesse porta una decisione, non una copia.
