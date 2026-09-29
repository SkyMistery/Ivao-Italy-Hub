# I dati dei membri negli eventi: che cosa si vede, quanto si tiene, che cosa si cancella

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.1 n.10 per il registro dei controllori e n.15, §17.3 n.1 per il registro dei piloti, e le risposte del secondo giro approvate da
Carmine, design §R.3 c3). Lo spostamento della cancellazione in M4a (§4), proposto in E0, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184).
**Regola applicata:** piano §9.7, «Privacy dei membri» (di un membro il minimo necessario; ogni modulo scrive nel suo design che
cosa conserva e per quanto); `CLAUDE.md` §2 (la cancellazione dei dati di una persona con `IPersonalDataEraser`, le colonne che
nominano una persona chiamate `Vid`, `…Vid` o `…By`; nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`) e §5, caso
**(b)**. Design `09-design-m4.md` §1.1, §7.1, §11, §17.1 n.10, n.14, n.15.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

- **Chi vede che cosa**: il pubblico vede gli slot di un evento; lo staff vede chi li ha presi; il Gate Manager li legge.
- **Quanto si tiene** (c3): lo staff dell'ED vuole che le prenotazioni e i voli dei piloti, dopo un periodo, si cancellino e
  **restino solo le statistiche**; i turni e le presenze ATC invece restano, perché **il no-show conta per sempre**.
- **La cancellazione dei dati di una persona**, che il nucleo fa da T20b, e che ogni modulo completa per le sue righe.

## 2. Le decisioni

1. **Un visitatore vede di uno slot solo se è libero o preso, mai chi l'ha preso**; il roster pubblicato mostra i nomi solo a chi
   ha fatto il login. Le righe dei membri **non sono `IHasParticipants`**: il membro legge le sue dai suoi endpoint, e le liste con i
   VID sono dello staff (`EventBookings.View`, `EventAtc.View`, `EventReports.View`). Il Gate Manager legge con un token personale
   di chi ha `EventBookings.View`.
2. **Chi ha volato senza prenotare** è solo un numero nelle statistiche: nessun VID si salva.
3. **Quanto si tiene** (§17.1 n.15, c3):

   | Che cosa | Quanto |
   |---|---|
   | L'evento, gli slot, le rotte, le regole, le statistiche | per sempre, allo staff |
   | Prenotazioni e PIREP di supporto **dei piloti** | `pilotRetentionMonths` (**24**) dopo la fine, poi cancellati |
   | Disponibilità ATC | cancellate a roster pubblicato e a evento finito |
   | **Turni ATC e loro esiti** (il registro di affidabilità), PIREP di supporto ATC, richieste di cessione | **per sempre** |
   | Iscrizioni a un evento in presenza e posti delle attività | `inPersonRetentionMonths` (**3**) dopo la fine; le somme restano |
   | Chi ha volato senza prenotare | mai salvato per VID |

   Il job **`events-retention`**, una volta al mese, decide dalle date delle righe.
4. **I due registri**, dei controllori (per sempre) e dei piloti che prenotano e non volano (sui dati tenuti 24 mesi), **li vedono
   l'interessato e lo staff**, nessun altro (§17.1 n.10; §17.3 n.1).
5. **La cancellazione dei dati di una persona** (`EventsPersonalData : IPersonalDataEraser`): prenotazioni, disponibilità, turni,
   iscrizioni e posti di **eventi non conclusi** si **cancellano** (lo slot torna libero, il turno si scopre e lo staff lo vede);
   quelli di eventi conclusi e i PIREP **restano con lo pseudonimo**, senza i dati del volo privato e senza note; **il registro di
   affidabilità diventa di nessuno** — i turni restano con lo pseudonimo e le statistiche non cambiano —; quello che la persona ha
   fatto **come staff** resta con lo pseudonimo. **Iscrizioni e posti di un evento in presenza** sono un'estensione di questa nota al
   design §11.1, che li elenca solo in §4-bis e in §11: dicono chi viene di persona e dove, quindi si trattano come le prenotazioni
   (cancellati se l'evento non è concluso, e comunque dopo `inPersonRetentionMonths`). Le colonne che nominano una persona seguono la convenzione del nucleo
   (`booker_vid`, `controller_vid`, `vid`, `decided_by`, `cancelled_by`, `attendance_by`…).

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Tenere le prenotazioni dei piloti per sempre | c3: dopo un periodo restano solo le statistiche |
| Far scadere il registro dei controllori | c3: il no-show conta per sempre, pesato |
| Mostrare al pubblico chi ha preso uno slot | piano §9.7, il minimo necessario; la regola di M3 (nota `2026-09-25-il-training-in-pubblico`) |
| Cancellare i turni di una persona cancellata | le statistiche e il registro dell'evento cambierebbero; lo pseudonimo li tiene senza la persona |

## 4. Che cosa si tocca, e dove

- **Ogni fase** che crea una tabella con una colonna che nomina una persona la chiama secondo la convenzione del nucleo.
- **La cancellazione nasce con M4a**, non in fondo a M4b come il design §16 metteva (E15) — proposta di questa fase, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184): M4a va in produzione da solo, per spegnere `ivao-booking`, e le prenotazioni sono
  dati di una persona dal primo giorno. Il nucleo scrive già lo pseudonimo nelle colonne dei contesti dei moduli
  (`PersonalDataErasure` li scorre tutti), ma **non cancella** le prenotazioni degli eventi non conclusi: senza `EventsPersonalData`
  uno slot resterebbe preso da uno pseudonimo. **E8a** (nucleo) allarga `ErasureTests` al contesto degli eventi, **E8b** scrive
  `EventsPersonalData` per le righe di M4a; **E15b** lo allarga alle righe di M4b, **E16** e **E17** a quelle di M4c.
- **La conservazione** (`events-retention`) nasce in **E15b** per le righe dei piloti e in **E17** per quelle in presenza: la prima
  riga da cancellare arriva 3 mesi dopo il primo evento in presenza, 24 mesi dopo il primo evento a slot.

## Da portare nel piano

**Già nel piano 1.24**: §9.7 «Privacy dei membri» (i due registri, chi ha volato senza prenotare solo come numero, un visitatore vede
di uno slot solo se è libero o preso). Resta:

- **§9.7, «Privacy dei membri»**: che cosa tiene il modulo e per quanto, come la tabella del punto 3 (le prenotazioni e i PIREP dei
  piloti 24 mesi, le iscrizioni in presenza 3 mesi, i turni ATC per sempre).
