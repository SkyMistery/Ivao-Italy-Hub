# La testa della pagina di un evento: due righe sotto il titolo, gli slot subito dopo (E8b)

**Data:** 11 ottobre 2026 — fase E8b di M4, chiesta da dalberone dopo il banco di E7 (#246)
**Stato:** **Proposta** — una domanda a Carmine con [un commento sulla #249][q] (§4). E8b scrive la pagina come la descrive questa
nota; una risposta diversa è una correzione sul branch della fase.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: solo la pagina del modulo e le sue parole — nessun meccanismo, niente nel
nucleo, nessun endpoint, nessuna migrazione. Cambia però due cose già decise: l'ordine della pagina nel design M4 §7.1 («banner,
titolo, date in UTC e nell'ora della divisione, tipo, organizzatore, scali, rotte, descrizione. Poi: slot pubblici…, slot
privati…») e la riga «Prenotazioni» decisa con E6b (#240, piano 1.32). Per questo è una domanda, e non una lettura.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6108168944

## 1. Che cosa ha chiesto dalberone

Il suo disegno, guardando la pagina sul banco di E7:

```
RFO serale a Fiumicino
Slot pubblici e privati.

16 ott · 17:00–21:00Z (19:00–23:00 LT)
[Prenotazioni aperte]  · LIRF

── Slot ──────────────
 Partenze | Arrivi …
── Slot privati ──────
── Descrizione · Rotte ─
```

1. Sotto titolo e riassunto, una riga di sintesi: la data e gli orari in UTC e nell'ora della divisione; le prenotazioni come badge
   («Prenotazioni aperte», oppure quando aprono, con il conto alla rovescia di E6b, oppure chiuse); gli scali come codici. Le righe
   separate «Quando», «Prenotazioni», «Scali» se ne vanno.
2. «Organizzato da» solo quando non è la divisione, nella stessa riga.
3. «Ogni slot si prenota fino al suo off block» va accanto agli slot.
4. Gli slot subito dopo la testa, poi gli slot privati, in fondo la descrizione e le rotte.
5. Su un telefono la riga va a capo.

Il perché: chi apre la pagina di un evento ci viene per gli slot. I fatti in righe con l'etichetta (la `dl` di E4, allungata da
E6b) e, sotto, le rotte li spingevano fuori dal primo schermo di un telefono.

## 2. Le letture (raccomandate)

1. **Due righe corte, come nel disegno**: la prima dice quando; la seconda le prenotazioni, gli scali e chi organizza. Ognuna va a
   capo da sola su un telefono, mai dentro «(18:00–22:00 LT)» né dentro il conto; il puntino fra due fatti resta con il fatto che
   lo segue, così una riga non finisce mai su un puntino. Misurate a 375 px, in italiano e in inglese, con uno spec usa e getta
   (fuori dal branch): niente della pagina esce dai 343 px utili.
2. **Quando**: il giorno una volta e poi i due orari, «21 nov 2099 · 17:00–21:00Z»; nell'ora della divisione **solo gli orari**
   quando cadono in quello stesso giorno, «(18:00–22:00 LT)», altrimenti anche il giorno; un evento a cavallo della mezzanotte
   scrive le sue due date, come faceva `spanText`. Il giorno è quello di `useMoment` del nucleo, **con l'anno** (il disegno scrive
   «16 ott»): toglierlo vorrebbe un'opzione nuova nel formattatore del nucleo, e a un evento di qui a un mese l'anno non toglie
   niente.
3. **Le prenotazioni, un badge**: «Prenotazioni aperte» (blu, il colore che aveva lo stato «prenotazioni aperte»); prima
   dell'apertura «Prenotazioni dal 14 nov 2099, 18:00Z (19:00 LT)» con accanto il conto alla rovescia di E6b, che a zero rilegge la
   pagina come prima; «Prenotazioni chiuse» per un evento annullato o concluso. **Nessun badge** per un evento che non prende
   prenotazioni (senza apertura, come un online day).
4. **Il badge dello stato sopra il titolo se ne va; quello del tipo resta** dov'era, come sulle schede. «Prenotazioni aperte» è
   già la parola dello stato `BookingOpen`, e sarebbe scritta due volte; «annullato» lo dice l'avviso subito sotto la testa, con la
   sua nota; bozza, non ancora visibile e concluso, che vede solo lo staff, li dice l'avviso dello staff; «in corso» lo dicono gli
   orari e, sugli scali, la striscia di chi è online; «annunciato» non dice niente che il badge delle prenotazioni non dica meglio.
   Le schede di `/events` tengono stato e tipo: in una lista è lo stato che distingue un evento dall'altro.
5. **Gli scali come codici**, il nome al passaggio del mouse (`abbr`) quando l'hub lo conosce; «Tutti gli aeroporti della
   divisione» per un evento di tutta la divisione. I nomi restano dove servono: nelle sezioni degli slot (una per scalo, quando
   sono più d'uno), negli slot privati e nelle rotte.
6. **Chi organizza**, solo se non è la divisione: «Organizzato dalla rete, con la divisione» o «Organizzato da un'altra
   divisione», e accanto «La loro pagina» ↗. La pagina esterna di un evento **della divisione**, se qualcuno la scrive, non compare
   più: il campo è per gli altri (il suo suggerimento lo dice, «Per un evento della rete o di un'altra divisione»), e «La loro
   pagina» senza un «loro» non vorrebbe dire niente.
7. **«Ogni slot si prenota fino al suo off block»** sotto il titolo «Slot», accanto al giorno degli slot, finché l'evento prende
   prenotazioni e non è annullato. Non sopra gli slot privati, che hanno la loro regola (l'off block che il pilota scrive, E7).
8. **L'ordine**: il banner, la testa, la striscia di chi è online nel giorno dell'evento (E6b: «sotto il titolo»), l'avviso di un
   evento annullato; poi gli slot pubblici, gli slot privati, il link «Le tue prenotazioni»; in fondo la descrizione e le rotte.

## 3. Le alternative

- **(A, raccomandata)** la testa e l'ordine del §2.
- **(B)** le due righe, ma l'ordine di prima: rotte e descrizione sopra gli slot.
- **(C)** com'era: i fatti in righe con l'etichetta, e il badge dello stato sopra il titolo.

## 4. La domanda a Carmine

Posta in inglese con [un commento sulla #249][q]; in italiano dice:

> La pagina di un evento con la testa disegnata da dalberone: sotto titolo e riassunto due righe corte — quando, in UTC e nell'ora
> della divisione; le prenotazioni come badge (aperte; dal momento, con il conto; chiuse), gli scali come codici, chi organizza solo
> se non è la divisione —, senza il badge dello stato sopra il titolo e con quello del tipo; poi gli slot, gli slot privati, e in
> fondo la descrizione e le rotte. (A, **raccomandata**) così; (B) le due righe con l'ordine di prima; (C) com'era?

## 5. Che cosa si tocca

Solo la pagina del modulo e le sue parole: `EventHead.tsx` (nuovo), `BookingOpening.tsx` (il badge al posto della riga),
`public.tsx` (la testa e l'ordine), `EventSlots.tsx` (la frase dell'off block), `cards.ts` e `bookingTimes.ts` (gli orari della
testa e lo stato delle prenotazioni, come dati), le parole (nuove `public.organizedBy.*` e `public.booking.opensOn`, `open`,
`closed`; tolte `public.when` e `public.booking.title`, `opensAt`, `openSince`, `closedCancelled`, che nessuno legge più). I test:
Vitest della riga (`EventHead.test.tsx`), dell'ordine (`public.test.tsx`), degli orari e dello stato; le spec che leggevano le righe
vecchie — la smoke `events-public.spec.ts` (E4, E6b) e il giro `full/events-bookings.spec.ts` (E6b) — leggono la testa nuova.

## Da portare nel piano

- Design M4 §7.1, `/events/{slug}`: la testa (due righe; il badge delle prenotazioni al posto della riga «Prenotazioni» decisa con
  E6b; chi organizza solo se non è la divisione; gli scali come codici; il tipo sì, lo stato no) e l'ordine (gli slot, gli slot
  privati, poi la descrizione e le rotte); «ogni slot fino al suo off block» accanto agli slot.
- Piano 1.32, la riga «Prenotazioni» di E6b: diventa il badge della testa.
