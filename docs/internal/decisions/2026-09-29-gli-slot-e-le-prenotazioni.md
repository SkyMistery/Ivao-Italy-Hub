# Gli slot e le prenotazioni dei piloti

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.1 n.4, n.5, n.6, n.7 e n.16 per la rotazione intera, §17.2 n.6 per la distanza fra due prenotazioni, §17.3 n.1 e n.2).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: funzioni del modulo, dentro meccanismi che ci sono (`MapCrud`, lista e form
generati, `Refusals`, `ISubmittedByMembers`, le impostazioni dei moduli, le notifiche). Design `09-design-m4.md` §1.5, §1.6, §1.12,
§3, §10.1, §17.1 n.4–n.7 e n.16, §17.3 n.1 e n.2.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

- **Che cos'è uno slot pubblico e una rotazione** (c1, c2), come si caricano, **come si generano gli slot privati**, **quanti** slot
  prende un pilota e **fino a quando**.
- **La sera dell'apertura** (design §10.1): molti piloti nello stesso minuto, a volte due processi, un pool di 15 connessioni.
- **Chi prenota molto e vola poco**, con le prenotazioni illimitate (§17.3 n.1), e **il promemoria** (§17.3 n.2).

## 2. Le decisioni

1. **Lo slot pubblico** (§17.1 n.4) pubblica callsign, numero di volo, tipi di aereo ammessi, origine, EOBT, destinazione, EIBT e
   gate. Chi lo prenota vola a quell'ora **con quel callsign**; se sulla rete è occupato, ne usa un altro, e la verifica dopo
   l'evento non guarda il callsign.
2. **Una rotazione** è una catena di tratte: l'arrivo di una è la partenza della successiva, e ogni due tratte si tocca uno scalo
   dell'evento. È un suggerimento per chi vuole fare real ops: si prenota **tutta**, **a pezzi** (anche non attaccati) o **una
   tratta sola** (§17.1 n.4). **«Prenota tutta la rotazione»** prende in una transazione le tratte libere e compatibili e dice quali
   erano già prese (§17.1 n.16).
3. **Il caricamento** (§17.1 n.7): una **tabella incollata** da un foglio di calcolo o un **CSV**, con una riga d'intestazione;
   **niente `.xlsx`**. Tutto o niente, i rifiuti per riga con `Refusals`; le catene controllate (ordine, aeroporto che coincide, uno
   scalo ogni due tratte, la distanza minima fra una tratta e l'altra); aggiunge, o sostituisce gli slot pubblici **liberi**.
4. **Gli slot privati** (§17.1 n.6): un aeroporto è per forza dell'evento, il pilota sceglie l'altro e il suo orario. Il sistema li
   **genera** dagli slot pubblici e dalla **capacità** di ogni scalo — movimenti per ora, oppure arrivi e partenze per ora,
   **configurabile per evento** —, a intervalli regolari nei buchi; rigenerare sostituisce i privati liberi. Un arrivo può chiedere
   **la partenza collegata**, così il Gate Manager dà lo stesso gate. Nessuna conferma dello staff.
5. **Quanti slot** (§17.1 n.5): **nessun limite**, purché **compatibili**: fra la fine di uno (EIBT) e l'inizio dell'altro (EOBT)
   almeno **`bookingGapMinutes`**, in un senso o nell'altro, **predefinito 10** (§17.2 n.6). Non serve che il secondo parta da dove è
   arrivato il primo.
6. **Fino a quando** (§17.1 n.5): si prenota da `booking_opens_at_utc` **finché l'EOBT dello slot è futuro**, anche a evento in
   corso; si **ritira** fino allo stesso momento. Ritirare cancella la riga; la storia è l'audit del nucleo.
7. **La prenotazione è una riga a sé** (`evt_bookings`, **indice univoco su `slot_id`**): l'unicità la tiene il database; la
   compatibilità dello stesso pilota si serializza con un blocco sulla riga della sua prima prenotazione dell'evento, o su quella
   dell'evento. Nessun lock in memoria, nessuna cache della disponibilità.
8. **Il registro del pilota** (§17.3 n.1, M4b): le prenotazioni verificate come **non volate** negli ultimi `pilotRetentionMonths`,
   meno quelle tolte a mano dallo staff con una nota. Da **`unflownThreshold`** (3) in su il pilota prenota **al massimo
   `restrictedMaxPerWindow` slot (1) in ogni fascia di `restrictedWindowHours` ore (2)** e **al massimo `restrictedMaxPerEvent` (2)**
   nell'evento; tutti e tre si configurano, e l'evento può cambiarli per sé. Lo vedono il pilota e lo staff. Vale da **E13b**: prima
   nessun pilota ha un registro.
9. **Il promemoria del giorno prima** (§17.3 n.2): `reminderLeadHours` (24) prima dell'EOBT, **`bookingReminder`** con callsign,
   aereo, partenza e arrivo con gli orari, il gate, **la rotta del FOD** se c'è per quella coppia di aeroporti, e la nota sul callsign
   occupato; più prenotazioni vicine dello stesso evento in una mail sola; una volta per prenotazione (`reminded_at`).

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lo slot libero come `booked_by` nullo sulla riga dello slot (come `ivao-booking`) | lo slot è una riga dello staff, che il membro non scrive (guardiano); una riga a sé tiene l'unicità nel database |
| Un limite fisso di slot per pilota | c1: quanti ne vuole, se compatibili; il limite vale solo per chi prenota e non vola (§17.3 n.1) |
| Il caricamento di un `.xlsx` | c3: non serve; incollare dal foglio basta |
| La direzione dello slot da un campo `type_of_flight` | si deduce dagli ICAO rispetto agli scali dell'evento |
| Una coda o un lock in memoria per l'apertura | due processi possono servire la stessa sera; l'unicità sta nel database |

## 4. Che cosa si tocca, e dove

**E5** gli slot pubblici e il caricamento; **E6a** i verbi, la compatibilità e la rotazione intera sul server; **E6b** le pagine,
`/events/mine`, `events.myEvents` e il promemoria; **E7** il generatore, lo slot privato e la partenza collegata; **E13b** il registro
del pilota e i limiti.

## Da portare nel piano

**Già nel piano 1.24**: §9.2 riga Events (slot pubblici con le rotazioni da CSV o tabella incollata, privati generati dalla
capacità, il pilota prenota quanti slot vuole se compatibili fino all'EOBT), §9.7 «Privacy dei membri» (il registro dei piloti che
prenotano e non volano; un visitatore vede di uno slot solo se è libero o preso). **Nient'altro.**
