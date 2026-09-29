# Il roster ATC: proposto dal sistema, deciso dallo staff, e il registro di affidabilità

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.1 n.9, n.10 e n.16 per la penalità, §17.2 n.3 per la lettura delle prenotazioni di IVAO, §17.2 n.6 per i predefiniti, §17.3
n.3).
**Regola applicata:** design M2 §6.3 («la macchina propone, una persona decide»); `CLAUDE.md` §3 (nessun rating nel codice del
modulo: il vocabolario del nucleo) e §5, caso **(c)** per le funzioni del modulo, **(b)** per quello che chiede al nucleo (le
estensioni n.3, n.4 e n.6 del design, ognuna con la sua nota nella sua fase). Design `09-design-m4.md` §1.7, §4, §9.1, §9.3, §17.1
n.9, n.10 e n.16, §17.2 n.3, §17.3 n.3.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

- **Come nasce il roster** (c1, c2): un roster a turni (predefinito un'ora); il controllore dà la sua **disponibilità oraria**; il
  sistema gli assegna i turni **secondo il rating e le postazioni che apre più spesso**; **AS3 minimo** per un evento, **TWR e GND
  meglio ADC+, APP meglio APC+, ACC meglio ACC+**, e se non c'è nessuno si scende al minimo della postazione; **x turni di pausa
  dopo y di fila**; lo staff corregge, anche con chi non si è candidato.
- **Quando**: candidature chiuse x+y giorni prima, roster pubblicato esattamente x giorni prima, e dopo modifiche notificate.
- **I no-show** (c1, c3): penalizzano i turni successivi, **contano per sempre**, pesati sugli eventi fatti, li vedono il
  controllore e lo staff, si tolgono solo a mano; un turno ceduto per un buon motivo non è un no-show.
- **Le prenotazioni delle postazioni su IVAO** (§17.2 n.3) e il **cedere un turno** (§17.3 n.3).

## 2. Le decisioni

1. **Il roster lo propone il sistema** (§17.1 n.9), alla chiusura delle candidature, con codice **deterministico** del modulo che
   dice **perché** ha dato ogni turno: i turni sono le finestre delle postazioni a pezzi di `shift_minutes`; **può** chi è
   disponibile per tutto il turno, ha almeno `minimumAtcRating` e il minimo della postazione, non ha un altro turno a quell'ora e
   non supera `maxConsecutiveShifts` senza `breakShifts` di pausa; **prima** chi ha il **rating preferito** per il tipo di
   postazione, poi chi apre più spesso quella postazione negli ultimi `experienceMonths`, poi la **penalità** più bassa, poi chi ha
   meno turni, poi chi si è candidato prima; si riempiono prima i turni con meno candidati. Un turno senza nessuno resta
   **scoperto**, in evidenza.
2. **Le regole dei rating stanno nel vocabolario del nucleo** (design §1.13): «AS3 minimo» è un'impostazione scelta dal
   vocabolario; il rating preferito per un tipo di postazione e il minimo di una postazione sono regole di IVAO e le dice il
   vocabolario. Il modulo non scrive numeri né nomi di rating.
3. **L'esperienza** viene dalle sessioni condivise (`IAtcActivitySource`, piano 1.18), facoltative: senza la sorgente il criterio
   non c'è e gli altri valgono lo stesso.
4. **Lo staff corregge** fra la chiusura e la pubblicazione (`EventAtc.Edit`): sposta, toglie, **aggiunge anche chi non si è
   candidato**, purché sia entrato nell'hub almeno una volta; un'aggiunta fuori dalle regole è un **avviso**, non un rifiuto.
5. **La pubblicazione è una data** (§17.1 n.9): a inizio − `rosterPublishDays` (predefinito **2**) il roster è pubblicato, senza un
   pulsante; le candidature chiudono a inizio − `applicationsCloseDays` (predefinito **3**), così lo staff ha un giorno per
   correggere (§17.2 n.6). Ogni controllore riceve `atcShiftAssigned`, trova il turno in `/me` e in `/events/mine`, e c'è la pagina
   `/events/{slug}/roster` con tutti i turni (i nomi solo a chi ha fatto il login). **Dopo** ogni modifica avvisa subito
   (`atcShiftAssigned`, `atcShiftChanged`, `atcShiftRemoved`).
6. **Cedere un turno** (§17.3 n.3): il controllore chiede di cederlo a un membro che nomina, o a nessuno; la richiesta è una riga
   sua (`evt_atc_shift_transfers`); **il turno resta suo finché lo staff non decide**, mai sulla propria richiesta. Un turno ceduto e
   approvato **non è un no-show** di chi l'ha ceduto: la presenza si verifica sul nuovo titolare.
7. **No-show** (§17.1 n.10): dopo l'evento il sistema confronta ogni turno con le sessioni — almeno `attendanceMinimumMinutes`
   (predefinito **30**) → presente, meno → **no-show proposto**, con chi ha coperto la postazione se qualcuno l'ha fatto —; **lo staff
   conferma o giustifica** (ED, AOD o staff del FIR, con `EventAtc.Edit`, mai sul proprio turno). Senza la sorgente delle sessioni
   lo staff segna a mano.
8. **Il registro di affidabilità** (§17.1 n.10): turni fatti, no-show, giustificati, per evento; **lo vedono il controllore e lo
   staff**; **un no-show lo toglie solo lo staff, a mano**, con una nota, anche mesi dopo; **conta per sempre**. **La penalità** è
   `noShowWeight × no-show ÷ (turni fatti + no-show + 2)` (§17.1 n.16): un no-show su dieci eventi pesa 0,08, su due 0,25; il «+ 2»
   evita che il primo no-show di un nuovo controllore pesi il massimo.
9. **Le prenotazioni delle postazioni su IVAO si leggono accanto al roster** (§17.2 n.3), alla richiesta, per vedere chi ha anche
   prenotato su IVAO; **in fondo a M4b**. Scriverle per il controllore è fuori da M4 (nota
   `2026-09-29-i-tre-blocchi-e-che-cosa-resta-fuori-da-m4`).

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il roster tutto a mano, come oggi | c1: lo propone il sistema; lo staff lo corregge |
| Il roster assegnato dal sistema senza correzione | la macchina propone, una persona decide (design M2 §6.3) |
| Un pulsante «pubblica il roster» | c2: esattamente x giorni prima; una data non si dimentica |
| I no-show che scadono | c3: contano per sempre, pesati; si tolgono solo a mano |
| La penalità come numero assoluto di no-show | un no-show su dieci eventi non pesa come uno su due (c3) |
| Cambiare subito il titolare di un turno ceduto | cambiare il titolare non è una modifica della propria riga: decide lo staff |

## 4. Che cosa si tocca, e dove

**E10b** (nucleo) le sessioni condivise per VID; **E10c** (nucleo) il vocabolario dei rating e le postazioni della divisione;
**E11a** postazioni e disponibilità; **E11b** la proposta e la correzione; **E12** la pubblicazione per data, le mail, la pagina del
roster, i turni in `/me`, la cessione, `events.atcCoverage`; **E13b** i no-show confermati e il registro; **E15a** (nucleo) e
**E15c** le prenotazioni di IVAO accanto al roster.

## Da portare nel piano

**Già nel piano 1.24**: §9.2 riga Events (roster proposto dal sistema e corretto dallo staff), §9.7 «Collaborazioni tra moduli»
(ATC↔Events: le postazioni si scrivono sull'evento, decise da AOD e staff dei FIR) e «Privacy dei membri» (il registro di
affidabilità per sempre, visibile all'interessato e allo staff), §10 (le prenotazioni ATC di IVAO alla richiesta). **Nient'altro.**
