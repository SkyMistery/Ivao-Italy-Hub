# Gli slot privati: le letture del design (E7)

**Data:** 11 ottobre 2026 — fase E7 di M4 (gli slot privati), PR #246
**Stato:** **Proposta** — le domande sono a Carmine con un commento sulla #246 (§4). E7 scrive le letture come le raccomanda questa nota;
una risposta diversa è una correzione sul branch della fase. Il ritiro di uno dei due voli collegati è un'altra nota, già **decisa**
(nota `2026-10-10-il-ritiro-di-un-volo-collegato`: la strada A,
[la risposta di Carmine sull'issue #245](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/245#issuecomment-6102449241)).
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — `IAirportDirectory`, `IAircraftTypeDirectory`,
`Refusals`, l'unico handler, il guardiano dell'interceptor, il blocco del pilota e `READ COMMITTED` di E6a, il servizio delle notifiche,
`SchemaForm`, la lettura della pagina che c'è (`PublicEventDto`), il `ContractVersion` dell'esportazione —; sono letture del design M4 §1.3,
§1.5, §1.6, §3.2, §3.4, §3.5, §7.1, §7.2, §7.4 e della nota `2026-09-29-gli-slot-e-le-prenotazioni` §2.4. **Nessuna migrazione**: le colonne
ci sono da E5 (`evt_slots`) ed E6a (`evt_bookings`).

## 1. Il generatore (§3.2)

1. **Le ore** sono quelle dell'evento: dall'inizio, un'ora ciascuna, e l'ultima tagliata alla fine con la sua parte della capacità,
   arrotondata per difetto (trenta l'ora per mezz'ora sono quindici). «Ogni ora della finestra di prenotazione» del design è la finestra
   dell'evento: i privati stanno nelle sue ore, mai nel margine di sei ore che E5 lascia all'orario di uno slot pubblico (`SlotWindow`), e
   un pubblico in quel margine non toglie posto a nessuna ora.
2. **Gli intervalli regolari, lontano dai pubblici**: un'ora che prende N slot è tagliata in N passi uguali dal suo inizio, al minuto
   intero (trenta l'ora è uno slot ogni due minuti: :00, :02, :04…). Ogni orario che lo scalo tiene già prende il passo libero più vicino
   — a parità, il primo —, e i privati prendono i passi rimasti: il posto libero è la capacità meno quello che c'è, e nessun privato sta
   al passo di un pubblico.
3. **Per verso o in movimenti**: con arrivi e partenze ogni verso ha i suoi passi e i suoi orari tenuti; **in movimenti** i due versi
   dividono i passi, e i privati vanno ad arrivi e partenze così che l'ora, con quello che tiene, resti pari quanto può — un arrivo per
   primo quando sono pari. Un verso senza numero non prende privati; uno scalo senza capacità nessuno; un evento in cui nessuno scalo ne
   ha una è rifiutato (`events:errors.noCapacity`).
4. **Che cosa tiene un'ora**: ogni volo che resta, in ogni scalo dell'evento che tocca e nel suo verso — un pubblico com'è scritto, un
   privato prenotato com'è scritto dal suo pilota (`BookedFlight`). **Un volo fra due scali dell'evento conta in tutti e due**: è una
   partenza dal primo e un arrivo al secondo, anche se la pagina e l'esportazione lo mettono fra le partenze del primo (E5, lettura 3).
5. **Rigenerare** sostituisce i privati liberi che il generatore ha fatto; quelli prenotati restano, con la loro riga e la loro
   prenotazione, e contano come un pubblico. Un salvataggio solo, ogni slot con la sua riga d'audit; un pilota che prenota nello stesso
   istante uno slot che se ne va, o un'altra scrittura che li ha presi prima, è un **409** («leggi di nuovo»), come per «sostituisci» di E5.
6. **Un MSE ha tutta la capacità privata** perché non ha pubblici: il codice non conosce il tipo, legge gli interruttori. Un evento senza
   slot privati è rifiutato (`events:errors.noPrivateSlots`); si genera su una bozza come su un evento pubblicato. **Cambiare la capacità o
   le date non rigenera da sé**: lo staff preme di nuovo «Genera».
7. **Al più 5.000 privati** per generazione (`events:errors.privateSlotsTooMany`): l'evento più grande con qualche scalo e qualche ora ne
   fa qualche migliaio; di più è una capacità o una finestra scritte male (999 l'ora per giorni), rifiutate prima di scrivere decine di
   migliaia di righe.
8. **L'interruttore degli slot privati si spegne anche con dei privati**: non c'è la regola gemella di `hasPublicSlots` (E5). La prova
   l'ha scritta E7 e l'ha tolta: un test di E5 (`EventsSlotsTests`, l'esportazione) scrive un privato nel database di un evento senza
   l'interruttore, come «lo farà il generatore di E7», e cadeva. I privati restano finché «elimina i liberi» non li toglie. La domanda 2.

## 2. Prenotare un privato (§3.4, §3.5)

9. **Il volo**: lo slot privato fra quelli liberi, il **nominativo**, l'**aereo** (un tipo che il nucleo conosce), **l'altro aeroporto** —
   uno che il nucleo conosce, non lo scalo dello slot (`events:errors.otherIsTheSlots`), anche un altro scalo dell'evento — e **il suo
   orario**. Un arrivo lascia l'altro aeroporto a quell'orario e arriva allo slot; una partenza lascia lo slot e arriva là a quell'orario.
   L'orario all'altro aeroporto è libero, come per un pubblico (E5, punto 10 di Carmine), e va solo in ordine
   (`events:errors.onBlockBeforeOffBlock`). Il volo sta sulla prenotazione (`callsign`, `other_icao`, `other_time_utc`, le colonne di E6a),
   e lo legge un posto solo: **`BookedFlight`** — la lista del pilota, quella dello staff, le mail, il promemoria, l'esportazione.
10. **Fino a quando** (§3.3, §3.6): si prenota e si ritira **fino all'off block del volo** — per una partenza il suo orario allo scalo, per
    un arrivo l'orario in cui il pilota lascia l'altro aeroporto (`events:errors.offBlockPassed` su quell'orario, quando è passato). Un
    privato libero si offre finché il suo orario allo scalo è futuro.
11. **La compatibilità** (§3.5) usa l'intervallo del volo — dall'off block all'on block — contro ogni prenotazione del pilota nell'evento,
    pubblica o privata, sotto il suo blocco, come uno slot pubblico (E6a).
12. **La partenza collegata**: una partenza privata **dallo stesso scalo**, libera, dello stesso evento (`events:errors.pairedNotADeparture`),
    che lascia il gate **almeno `bookingGapMinutes` dopo l'on block dell'arrivo** (`events:errors.pairedTooSoon`); solo un arrivo ne porta
    una (`events:errors.pairedOnlyForArrival`). **La vola lo stesso aereo** — atterra e riparte dallo stesso gate —, con un nominativo suo,
    che la pagina propone uguale a quello dell'arrivo. Le due nascono in una transazione: prima la partenza, poi l'arrivo che la nomina
    (`paired_booking_id`, sull'arrivo come l'ha scritto E6a), **o nessuna** — anche quando l'indice univoco risponde nello stesso istante.
13. **Le risposte**: ogni rifiuto sul suo campo — `slotId`, `aircraftIcao`, `callsign`, `otherIcao`, `otherTimeUtc`, e quelli della partenza
    sotto `departure.…` —; uno slot pubblico mandato al verbo dei privati è rifiutato (`events:errors.slotNotPrivate`), come un privato a
    quello dei pubblici (`bookingPrivateSlot`, E6a); un deadlock è **409** «riprova»; un evento che il pilota non vede, 404. La risposta,
    **201**, dice la prenotazione e la partenza collegata.
14. **Il promemoria** (§3.8) di un privato parte per l'off block del suo volo: per un arrivo, quello scritto dal pilota (E6b lo lasciava a
    E7, «i privati aspettano»).

## 3. L'esportazione e le pagine (§7.1, §7.2, §7.4)

15. **L'esportazione**: un privato prenotato porta il volo del suo pilota — nominativo, aereo, l'altro aeroporto e il suo orario —, il
    **gate vuoto**, e **`paired_slot_id` sull'arrivo e sulla partenza**, ciascuno lo slot dell'altro: lo stesso gate da dare ai due. Un
    privato libero è quello che era (lo scalo e l'orario). Ogni campo c'era già nella versione 1: è un'**aggiunta alla versione 1**, e
    `docs/events-bookings-export.md` lo dice.
16. **La pagina dell'evento**: i privati **per scalo, verso e ora dell'evento** (le ore del generatore), una sezione per scalo quando sono
    più d'uno, le partenze e gli arrivi in due tabelle come per i pubblici; ogni ora dice quanti dei suoi slot sono liberi, «Pieno», o
    «Tuo» quando il lettore ne ha uno. Una riga apre l'ora: il form del volo è **quello generato dal suo schema** (`SchemaForm`), così ogni
    rifiuto del server cade sul suo campo; un interruttore aggiunge la partenza collegata. I filtri di E6b restano dei pubblici. Nessuna
    lettura nuova: i privati viaggiano nella lettura della pagina (`PublicEventDto.PrivateSlots`), mai chi li ha presi.
17. **`/events/mine`** dice il volo a cui una prenotazione è collegata, e il ritiro di una delle due dice che l'altra resta (la nota del
    ritiro, decisa: la strada A). **La scheda «Slot»** dello staff ha «Genera gli slot privati» su un evento con slot privati, chiesto
    una volta di più (blu: niente di prenotato si perde), e la lista dice se uno slot è pubblico o privato e il suo scalo.

**Gli endpoint scritti a mano di E7** (per il conto del piano §16.6): due verbi che il design nomina —
`POST /api/events/events/{id}/slots/generate` («genera gli slot privati», §7.2) e `POST /api/events/mine/bookings/private` (prenotare,
§7.2, nel flusso del membro). Nessuna lettura nuova.

## 4. Le domande a Carmine

Poste in inglese sulla #246; in italiano dicono:

> 1. Le letture dei §1–§3, come scritte? In particolare: (a) le ore sono quelle dell'evento, l'ultima tagliata con la sua parte di
>    capacità, e nessun privato nel margine di sei ore; (b) i passi regolari dell'ora, e ogni orario già tenuto prende il passo più vicino;
>    (c) in movimenti i versi restano pari; (d) un volo fra due scali dell'evento conta in tutti e due; (e) l'altro aeroporto di un privato
>    può essere un altro scalo dell'evento; (f) la partenza collegata la vola lo stesso aereo, almeno `bookingGapMinutes` dopo l'arrivo;
>    (g) al più 5.000 privati per generazione.
> 2. L'interruttore degli slot privati si spegne anche con dei privati (non c'è la regola gemella di `hasPublicSlots`): va bene così
>    (raccomandato: i privati sono del generatore, e «elimina i liberi» li toglie), o vuoi la regola gemella in una fase dopo, con il test di
>    E5 che scrive un privato su un evento senza l'interruttore corretto?

## Da portare nel piano

- Design M4 §3.2: il generatore — le ore dell'evento, i passi regolari e il passo più vicino, i versi in movimenti, che cosa tiene un'ora
  (un volo fra due scali in tutti e due), rigenerare, i rifiuti e il tetto di 5.000; l'interruttore dei privati (la domanda 2).
- Design M4 §3.4 e §3.5: il volo di un privato e fino a quando si prenota; la partenza collegata (stesso scalo, stesso aereo, il gap, tutta o
  niente); il volo di una prenotazione letto in un posto solo (`BookedFlight`).
- Design M4 §3.8: il promemoria di un privato all'off block del suo volo.
- Design M4 §7.1, §7.2: i privati sulla pagina per scalo, verso e ora con il form generato; «Genera gli slot privati» nella scheda; il
  collegamento in `/events/mine`.
- Design M4 §7.4: il volo di un privato e `paired_slot_id` nell'esportazione, aggiunta alla versione 1.
- Piano §16.6: il conto degli endpoint a mano di M4 (due verbi di E7).
