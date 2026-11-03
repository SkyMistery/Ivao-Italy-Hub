# Il formato della data, in un posto solo

**Data:** 31 ottobre 2026 — fase di test, fase 5 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.E, punto a), PR del
nucleo.
**Stato:** **decisa**. Il formato e i tre ordini li ha decisi Carmine approvando la nota del 22 ottobre (§4.E, §7.6): «`dd/mm/yyyy` di
default, con la divisione che sceglie `mm/dd/yyyy` o `yyyy/mm/dd` in `division.json`». Le cinque scelte nuove del §3 le ha prese
Carmine il 31 ottobre 2026, in chat alla sessione di lavoro, tutte come raccomandato, e le ha confermate sulla PR:
https://github.com/SkyMistery/Ivao-Italy-Hub/pull/441#issuecomment-6600998155.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: i meccanismi ci sono già — `formatMoment` sul web (nota `2026-10-13-le-ore-in-24`)
e `DivisionOptions` sul server — e si estendono; nessun meccanismo nuovo.

## 1. Che cosa serviva

Lo staff, provando l'hub, ha letto le date nell'ordine della **lingua** e non della divisione: chi tiene l'hub in inglese leggeva
«Oct 31, 2026» e «10/31/26», e un 03/11 si legge in due modi. Il piano del 22 ottobre chiede un ordine solo, scelto dalla divisione.

Che cosa c'era, cercato in tutto il codice:

- **Il web** ha già **un** formattatore, `formatMoment` (`web/src/shared/i18n/useMoment.ts`), con `useMoment` per i componenti: circa
  150 chiamate nel nucleo e nei moduli, tutte con `Intl.DateTimeFormat` della lingua (`dateStyle` `medium`, `short` o `long`). Fuori da
  lì due soli `Intl.DateTimeFormat` scrivono una data a un lettore: la data di una news (`PublicEntryScreen.tsx`) e i passi del blocco
  timeline (`blocks.tsx`), tutte e due `long`. Gli altri `Intl` (`CalendarView`, il mese e i giorni della settimana in parole;
  `bookingTimes.dayIn`, una chiave interna) non scrivono una data in numeri.
- **Il campo data del form generato** è il selettore nativo del browser (`<input type="date">`, `datetime-local`): lo disegna il
  browser nella **sua** lingua, e la pagina non può cambiarlo.
- **Il server** scrive le date delle mail a mano, `yyyy-MM-dd`, in sette posti: gli helper `Moment` di `EventsMail` e `TrainingMail`
  (e `Span`, `Shift`, `SeatLines` sopra di loro), il riesame dei documenti (`DocumentReviewJob`), e nei Tour la sospensione (`Bans`),
  l'esito di un report (`PirepReview`), il riassunto della coda (`ReviewDigestJob`) e l'etichetta di un report in un filo
  (`FlightOpsReferences`). Il riassunto dei report di Eventi (`ReportsDigestJob`) scrive `yyyy-MM-dd` anche lui.

## 2. Il meccanismo

- **`division.json → dateFormat`**: `"dd/mm/yyyy"` (il default, se manca), `"mm/dd/yyyy"` o `"yyyy/mm/dd"`. Il validatore all'avvio
  rifiuta qualsiasi altra cosa, come fa con `awardDigestTime`: un errore di battitura non fermerebbe niente dopo, scriverebbe
  tutto nel default senza dirlo.
- **Il server**: `HubDates` (`Core/Division/HubDates.cs`) con `Day` (`31/10/2026`) e `Moment` (`31/10/2026 14:05`, sempre in 24 ore);
  `DivisionOptions.ResolveDateOrder()` dà l'ordine. **L'ordine si passa sempre**, mai un default nascosto: chi lo dimenticasse
  scriverebbe il default in una divisione che ha scelto altro. Gli helper dei moduli prendono l'ordine come parametro
  (`EventsMail.Moment(utc, order)`, `TrainingMail.Span(…, order)`), e la loro istanza ha `Dates`, l'ordine delle opzioni.
- **Il bootstrap**: `/api/me → division.dateFormat`.
- **Il web**: `formatMoment` scrive il giorno in numeri nell'ordine della divisione, poi l'ora (`31/10/2026 14:05`). L'ordine lo prende
  **una volta**, nel `beforeLoad` della route radice, appena arriva il bootstrap (`setDateFormat`), come i18next tiene la lingua: un
  chiamante non lo passa mai, e nessuna delle 150 chiamate cambia. `medium` e `short` scrivono uguale; `long` resta in parole.
  La data di una news e la timeline passano anche loro da `formatMoment` (`dateStyle: 'long'`).
- **Una data che legge un programma** — la sitemap, un indirizzo, un nome di file, l'API — resta ISO, `yyyy-MM-dd`.
- **I test**: `tests/IvaoHub.UnitTests/DateFormatTests.cs` (i tre ordini, il default, il rifiuto) e `web/src/shared/i18n/dateFormat.test.ts`
  (i tre ordini, il giorno nel fuso chiesto, la data lunga nella lingua, il default per un formato sconosciuto).

## 3. Le scelte di Carmine (31 ottobre 2026, in chat)

1. **Quali date seguono l'ordine.** Scelta: **«Numeri, tranne le lunghe»** — media e corta diventano `31/10/2026 14:05` ovunque
   (back office, moduli, liste, mail); la data lunga di una news e della timeline resta in parole della lingua («31 ottobre 2026»,
   «31 October 2026»), perché lì il mese è scritto e non c'è ambiguità. L'alternativa scartata: tutte in numeri.
2. **Il campo data del form.** Scelta: **«Dopo, con la fase (c)»** — il selettore lo riscrive comunque il calendario libero/occupato
   (§4.E punto c, un selettore del nucleo), e lì si scrive e si legge nel formato della divisione. Questa PR non tocca `SchemaForm.tsx`
   (che è anche nella #413); la riga sotto un campo istante (`01/06/2026 14:00 Europe/Rome`) segue già il formato, perché chiama
   `formatMoment`. **Come si legge all'indietro una data scritta** è quindi una domanda della fase (c), non di questa. L'alternativa
   scartata: un campo di testo adesso, da riscrivere una seconda volta nella fase (c).
3. **I test di `dalberone` sulle date.** Scelta: **«Solo i valori attesi»**, come il 13 ottobre per le ore in 24 (#261): in questa PR
   cambiano solo le stringhe attese delle date nei suoi test di Training ed Eventi (`Sep 27, 2026` → `27/09/2026`), niente altro.
4. **Le mail di Eventi e Training.** Scelta: **«Sì, solo l'helper»** — `Moment()` di `EventsMail` e `TrainingMail` chiama il
   formattatore del nucleo; più le date delle mail dei Tour e del nucleo. Niente altro dei loro moduli.
5. **Gli helper ricevono l'ordine** (domanda nata scrivendo il punto 4: gli helper sono statici e le opzioni no). Scelta: **«Cambio la
   chiamata»** — l'ordine è un parametro obbligatorio, e nei dieci punti dei test di `dalberone` in cui la chiamata a questi helper
   calcola il valore atteso si passa l'ordine del default, `DateOrder.DayMonthYear`, che è quello della divisione di prova. Scartato: un
   parametro facoltativo col default, che lascia i test intatti ma scrive il default in silenzio a chi lo dimentica.
   Due spec e2e di Training (`training-dates.spec.ts`, `training-report.spec.ts`) calcolavano la data attesa con una **loro copia** di
   `useMoment` (`Intl`, `medium`, in inglese). Su richiesta del master, la copia **non** chiama `formatMoment` (un formattatore sbagliato
   passerebbe il suo stesso test): costruisce `dd/mm/yyyy` e l'ora UTC a mano dal testo ISO, e nient'altro della spec cambia. Nei
   bootstrap di prova si aggiunge `dateFormat` invece di renderlo facoltativo nel tipo (il master: non si indebolisce il contratto).

**L'ora**, che la richiesta lasciava aperta: è già decisa, 24 ore (nota `2026-10-13-le-ore-in-24`); dopo il giorno, separata da uno
spazio e senza virgola (`31/10/2026 14:05`), come la scrivevano già le mail. Il fuso resta quello di prima: UTC, con `Z` o «UTC» o il
fuso della divisione accanto, come lo dice chi chiama.

## 4. Che cosa si tocca

- **Nucleo, server**: `Core/Division/HubDates.cs` (nuovo), `DivisionOptions.DateFormat` e `ResolveDateOrder`, il validatore,
  `BootstrapDivision.DateFormat` in `MeEndpoints.cs`, la data di `DocumentReviewJob`.
- **Nucleo, web**: `useMoment.ts` (`formatMoment`, `setDateFormat`), `routes/__root.tsx`, `PublicEntryScreen.tsx` e il blocco timeline
  in `blocks.tsx` su `formatMoment`, `schema.d.ts` rigenerato, `dateFormat` nei sei bootstrap di prova dei test che lo scrivono per
  intero (tipo richiesto).
- **Moduli**, solo la chiamata: `EventsMail` (`Moment`, `Shift`, `SeatLines`), `MemberSeats`; `TrainingMail` (`Moment`, `Span`),
  `TrainingBans`, `TrainingSessions`, `TrainingDates`, `Staff/StaffTrainings.cs` (la mail del formatore assegnato, arrivata con
  `main`); nei Tour `Bans`, `PirepReview`, `ReviewDigestJob`, `FlightOpsReferences` (nessuno
  dei file delle PR #415, #416, #419).
- **Test**: i valori attesi delle date (web, e2e, server) e le dieci chiamate del punto 5.
- **Documenti pubblici**: `config/division.example.json`, `docs/FORKING.md`, `docs/UI-GUIDELINES.md` («Times»).
- **Non toccato**: il selettore del form (punto 2); `ReportsDigestJob` di Eventi, che scrive ancora `yyyy-MM-dd` nel riassunto dei
  report (fuori da «solo l'helper»: è la issue #442); i nomi dei mesi e dei giorni del calendario, che sono parole.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| L'ordine come argomento di ogni `formatMoment` | 150 chiamate da cambiare, e la prima che lo dimentica scrive un altro ordine; l'ordine è della divisione, non di chi chiama |
| L'ordine letto con un hook dentro `useMoment` | `formatMoment` lo chiamano anche le celle delle liste e la riga sotto un campo, che non sono componenti: due strade per la stessa regola |
| `Intl` con una lingua scelta per l'ordine (`en-GB` per `dd/mm`) | Nessuna lingua scrive `yyyy/mm/dd` con le barre, e l'ordine di una lingua può cambiare con una versione del browser |
| Una stringa di formato libera in `division.json` | Il piano ne ha decise tre; una libera è un errore di battitura che nessuno vede |
| Lasciare le mail come sono (`yyyy-MM-dd`) | Una mail e la pagina che apre direbbero la stessa data in due modi |

## Da portare nel piano

- **§16** (meccanismi generici): il formato della data è `division.json → dateFormat` (tre valori, `dd/mm/yyyy` di default), scritto
  da `formatMoment` sul web (l'ordine dal bootstrap, una volta, nella route radice) e da `HubDates` sul server, con l'ordine sempre
  passato; la data lunga resta della lingua; una data letta da un programma resta ISO. Accanto alla riga delle ore in 24.
- **§4.1** (che cosa configura una divisione): la chiave `dateFormat`.
- **§9.7** o l'elenco del bootstrap: `division.dateFormat` in `/api/me`.
- La nota del 22 ottobre, §4.E: il punto (a) è fatto; la lettura all'indietro di una data scritta passa alla fase (c).
