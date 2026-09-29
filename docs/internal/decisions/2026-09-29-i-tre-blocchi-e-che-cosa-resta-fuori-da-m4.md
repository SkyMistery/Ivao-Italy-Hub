# I tre blocchi di M4, e che cosa resta fuori

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.1 n.8 e n.17, §17.2 n.3 per la parte fuori da M4, n.5 e n.7, §17.3 n.5 e n.6).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: che cosa entra nel modulo, in che ordine, e che cosa no. Design
`09-design-m4.md` §0.1, §0.2, §7.4, §16, §17.1 n.8 e n.17, §17.2 n.3, n.5 e n.7, §17.3 n.5 e n.6.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

- **Quanto è grande M4.** I requisiti dell'ED (design §R.3) sono tre lavori diversi: gli eventi con le prenotazioni dei piloti,
  che bastano a spegnere `ivao-booking`; l'ATC e il dopo evento; e, dal quarto giro di risposte, gli eventi in presenza. Farli
  in una milestone sola tiene `ivao-booking` acceso finché non è finito anche il roster.
- **Il Gate Manager e gli stand.** Il Gate Manager della divisione legge oggi le prenotazioni da `booking.it.ivao.aero` e gestisce
  gli stand con i dati che ha solo lui. Spegnere `ivao-booking` senza dargli un'altra fonte lo lascia senza dati.
- **La prenotazione su IVAO.** Carmine vorrebbe che l'hub prenotasse la postazione su IVAO per il controllore del roster.
- **Le mail di massa** per un evento nuovo, e **quando spegnere `ivao-booking`**.

## 2. Le decisioni

1. **Tre blocchi, un calendario solo** (§17.1 n.8, §17.2 n.6):
   - **M4a — gli eventi e le prenotazioni**: basta a spegnere `ivao-booking`, **con l'esportazione per il Gate Manager** con un
     token personale (§7.4; §17.3 n.6: senza, il Gate Manager di oggi resterebbe senza dati);
   - **M4b — l'ATC e il dopo evento**: postazioni, disponibilità, roster, no-show, PIREP di supporto, regole di award;
   - **M4c — gli eventi in presenza**: domande, iscrizione, attività parallele. **Non dipende da M4b**: può venire prima, se il
     primo evento in presenza arriva prima.
2. **`ivao-booking` si spegne dopo il primo evento vero fatto sull'hub**, con il Gate Manager già passato a leggere l'hub
   (provato su `prova-ponte-rfo`), poi un **301 verso `/events`** (§17.2 n.7). È la fase E9, fuori dal repository, e lo spegnimento
   lo fa Carmine.
3. **Fuori da M4**:
   - **scrivere la prenotazione della postazione su IVAO** per il controllore (§17.2 n.3): chiede il token IVAO del membro con
     `bookings:write`, conservato dall'hub per suo conto — un **meccanismo nuovo** (caso (c)) con una nota sua, dopo M4, con la
     sicurezza del token davanti. In M4 l'hub le prenotazioni di IVAO le **legge** accanto al roster (nota
     `2026-09-29-il-roster-atc`);
   - **la gestione degli stand**, anche semi-automatica, con i controlli (stand chiusi, due slot sullo stesso stand) (§17.1 n.17,
     §17.3 n.5), **e il Gate Manager dentro il sito** (§17.3 n.6): insieme, dopo che il modulo eventi è completo. Fino ad allora lo
     stand di uno slot pubblico è un campo scritto dallo staff, quello di uno slot privato resta vuoto;
   - **l'esportazione di un evento in bozza** per il Gate Manager (§17.3 n.6): no, ora;
   - **una mail a tutti i membri per un evento nuovo** (§17.2 n.5): no. Il feed iCal resta di M6 (piano §15.9);
   - **lo storico di `ivao-booking`**: nessun import (piano §12 punto 3, `CLAUDE.md` §7);
   - **gli eventi di HQ che non coinvolgono la divisione**, e qualunque evento letto da un'API: l'API di IVAO non ne ha (design
     §9.2).
4. **La porta resta aperta** al Gate Manager dentro il sito: lo slot ha un'identità stabile (`slot_id` nell'esportazione), lo stand
   è un campo suo, e l'esportazione del design §7.4 è il contratto da cui partire.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una milestone sola | `ivao-booking` resterebbe acceso fino alla fine del roster e del dopo evento, che non servono a spegnerlo |
| L'esportazione per il Gate Manager dopo M4 | spegnendo `ivao-booking` il Gate Manager di oggi resterebbe senza dati (§17.3 n.6) |
| Prenotare su IVAO per il controllore già in M4 | un token del membro con uno scope di scrittura conservato dall'hub è un meccanismo nuovo, e la sua sicurezza va studiata a parte |
| Controllare gli stand all'import | servono i dati degli stand di ogni aeroporto, che oggi ha solo il Gate Manager: è il lavoro degli stand, dopo |
| Una mail a tutti per ogni evento | è rumore; l'evento sta nel calendario, nella home e nel blocco `events.eventList` |

## 4. Che cosa si tocca, e dove

Solo l'ordine delle fasi in `10-piano-implementazione-m4.md`: **M4a** E1–E9, **M4b** E10–E15, **M4c** E16–E17. L'esportazione con il
token è in **E5**; lo spegnimento di `ivao-booking` in **E9**, fuori dal repository.

## Da portare nel piano

**Già nel piano 1.24**, portato dal master con il design: §9.2 riga Events (i tre blocchi, fuori da M4), §12 punto 3 (lo spegnimento
dopo il primo evento vero, poi il 301), §13 riga M4, §15 punto 12 (la prenotazione su IVAO, gli stand e il Gate Manager dentro il
sito). Resta:

- **§13, riga M4**: le fasi sono quelle di `10-piano-implementazione-m4.md`, che divide alcune fasi del design (E3, E6, E10, E11,
  E13, E14, E15) in PR più piccole e aggiunge tre fasi del nucleo (E8a, E10c allargata, E10e); i blocchi restano M4a E1–E9, M4b
  E10–E15, M4c E16–E17. **La fase E0 scrive le note delle decisioni di §17, non quelle delle estensioni del nucleo**: ognuna la
  porta la sua fase del nucleo, con la forma nel codice (come A0 di M3). Oggi la riga dice «le note di §17 e delle estensioni del
  nucleo».
