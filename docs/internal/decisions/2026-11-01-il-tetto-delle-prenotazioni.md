# Il tetto delle prenotazioni di un membro in un evento (#314)

**Data:** 1 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/events-booking-cap`.
**Stato:** **decisa** per i punti 1 e 2 — **Carmine in chat al master, 19 ottobre 2026**, pubblicato su sua istruzione sull'issue
([la risposta][a314]) —; **i punti 3–6 sono letture** che la correzione ha dovuto fare dove la risposta non dice, scritte qui perché il
revisore le controlli; **i punti 7 e 8 sono decisi** — **Carmine in chat al master, 1 novembre 2026** («Tutte come consigli»),
pubblicato su sua istruzione sull'issue ([la risposta][b314]) —: la (A) raccomandata di tutti e due, come la [domanda][q314] la poneva
e come il codice già fa.

[q314]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/314#issuecomment-6550988239
[b314]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/314#issuecomment-6555196512
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** e **(b)**: un'impostazione in più del modulo (dato), e il controllo nei tre verbi del
pilota che già leggono i limiti di chi non vola sotto il blocco del pilota (E6a, E7, E13b). Nessuna colonna, nessuna migrazione, nessun
file del nucleo.

[a314]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/314#issuecomment-6269204922

## 1. Il problema

Il design M4 §3.5 (c1–c2) dice delle prenotazioni di un pilota: compatibili a `bookingGapMinutes`, **«nessun limite di numero»**. Un
membro poteva prenotare quanti slot voleva in un evento; solo chi ha i limiti di chi prenota e non vola (§3.7, E13b) aveva un tetto
(`restrictedMaxPerEvent`, 2, e l'evento il suo). Carmine vuole un tetto per tutti.

## 2. La decisione di Carmine

1. **Un massimo di slot che un membro prenota durante un evento, impostato per tutta la divisione, predefinito 5** («si può impostare un
   limite massimo di divisione agli slot prenotabili durante un evento (di default 5)»).
2. **Una rotazione è una prenotazione sola**, qualunque sia il numero delle sue tratte («Una rotazione è una prenotazione sola, tanto la
   rotazione ha orari fissi stimati da noi»).

## 3. Le letture della correzione

3. **Un'impostazione del modulo Events**, `maxBookingsPerEvent`, **5**, da **1 a 200** (zero chiuderebbe le prenotazioni a tutti; 200 è
   il tetto di `restrictedMaxPerEvent`), nel form generato di `/staff/events/settings` subito dopo `bookingGapMinutes`, con la sua riga in
   `docs/FORKING.md`. **Di divisione e basta**: l'evento non ha un suo numero, a differenza dei limiti di chi non vola — Carmine ha detto
   «di divisione». È la lettura del master sull'issue.
4. **La controllano i tre verbi del pilota** — uno slot pubblico (`POST /api/events/mine/bookings`), la rotazione intera (`…/rotation`),
   uno slot privato con la sua partenza collegata (`…/private`) — **sotto lo stesso blocco del pilota** (`SELECT … FOR UPDATE` sulla sua
   prima prenotazione dell'evento, o sull'evento), **dopo la compatibilità e i limiti di chi non vola**, con le prenotazioni già salvate e
   quelle della stessa richiesta (le tratte appena prenotate, l'arrivo di una partenza collegata). Due richieste dello stesso pilota nello
   stesso istante non passano il tetto insieme: la seconda aspetta il blocco e legge la prima. Lo staff non prenota per un pilota: non c'è
   un altro verbo. È la lettura del master.
5. **Come si conta** (`Bookings/BookingCap.cs`, pura): le tratte di una stessa rotazione contano **una**, **comunque siano prenotate** —
   intera o una tratta alla volta; una rotazione si riconosce dal suo codice come lo tiene il database, maiuscole comprese (E5: R1 e r1
   sono due). Ogni altro slot, pubblico o privato, conta uno.
6. **Si rifiuta solo ciò che fa crescere il conto oltre il tetto**: una tratta di una rotazione già sua passa sempre; un tetto abbassato
   dopo non toglie niente a chi ne ha di più, che non ne aggiunge. Il rifiuto è la chiave `events:errors.bookingMaxPerEvent` sul campo
   dello slot (`slotId`; `departure.slotId` per la partenza collegata, e allora non nasce nessuna delle due, come per i limiti); per una
   rotazione è il motivo di ogni tratta non prenotata. La frase dice perché, e che una rotazione conta una; **il numero non lo dice**: le
   pagine del pilota non leggono le impostazioni, e i rifiuti del nucleo portano solo chiavi (`Refusals`).

## 4. Le due scelte decise il 1 novembre

7. **Chi ha i limiti di chi prenota e non vola ha tutti e due i tetti.** La domanda: (A) **valgono tutti e due**, quindi decide il più
   basso — i limiti di chi non vola contano ogni tratta di una rotazione come da E13b, il tetto della divisione conta la rotazione una;
   (B) anche i limiti di chi non vola contano la rotazione una. **Raccomandata la (A)**: è la lettura del master sull'issue («resta com'è
   e, più basso, vince per loro»), non cambia E13b, e un membro con i limiti resta il più stretto. Se l'evento alza i suoi limiti sopra il
   tetto della divisione, decide il tetto.
8. **Un arrivo privato e la sua partenza collegata** (E7). La domanda: (A) **sono due prenotazioni**, come per i limiti di chi non vola
   (nota `2026-10-16-i-no-show-i-registri-e-i-limiti`, lettura 7); (B) sono una, come una rotazione. **Raccomandata la (A)**: sono due
   voli con due callsign e due slot, che il pilota sceglie a uno a uno; la rotazione è una perché ha gli orari fissi dello staff.

**Carmine ha scelto la (A) di tutti e due** ([la risposta][b314]): chi ha i limiti ha tutti e due i tetti e decide il più basso; un
arrivo privato e la sua partenza collegata sono due prenotazioni. Il codice era già scritto così: la risposta non cambia una riga.

## Da portare nel piano

- **Design M4 §3.5** (c1–c2): al posto di «Nessun limite di numero», il tetto `maxBookingsPerEvent` della divisione, una rotazione una,
  sotto lo stesso blocco del pilota (punti 1–6); un arrivo privato e la sua partenza collegata contano due (punto 8).
- **Design M4 §1.12** (le impostazioni): `maxBookingsPerEvent`, 5, da 1 a 200.
- **Design M4 §3.7**: chi ha i limiti ha anche il tetto della divisione, e decide il più basso (punto 7).
- **Design M4 §17.1** (le decisioni del 29 settembre), punto 5: «Prenotazioni: nessun limite» corretto dalla risposta del 19 ottobre.
