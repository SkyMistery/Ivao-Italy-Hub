# Dopo l'evento: le verifiche, il PIREP di supporto, le regole di award, le mail

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180, [conferma di §17.1 e §17.2][ok]; §17.1 n.11, n.12 e n.13, §17.2
n.6 per i predefiniti).
**Regola applicata:** piano §9.7 (**mai un'assegnazione automatica di award**: il modulo segnala, una persona assegna); `CLAUDE.md`
§2 (il segnale con `IProjectable`; le notifiche dal servizio del nucleo, spegnibili) e §5, caso **(c)** per le funzioni del modulo,
**(b)** per quello che chiede al nucleo (le estensioni n.2 e n.5 del design, ognuna con la sua nota nella sua fase). Design
`09-design-m4.md` §0.6, §1.8–§1.11, §5, §8.2, §8.3, §17.1 n.11–n.13.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987

## 1. Che cosa serviva decidere

- **Le liste del dopo evento** (c1, c2): chi si era dato disponibile e ha controllato e chi no; chi ha prenotato e ha volato e chi
  no; **chi ha volato senza prenotare**.
- **Gli award «pilot support» e «ATC support»** (c1, c2): oggi li chiede chiunque, e l'MD valida e assegna. E gli **award
  specifici di un evento**: una certa rotta, una tratta più lunga di x, x tratte, un certo numero di voli (c3).
- **La partecipazione riportata in automatico** (c3), anche per l'Online Day e gli eventi liberi, dove nessuno prenota.
- **Le mail** (c1).

## 2. Le decisioni

1. **Le verifiche le fa il sistema, dopo la fine** (§17.1 n.11), con il job `events-after` a lotti: per ogni prenotazione la ricerca
   delle sessioni del pilota nella finestra, fra partenza e arrivo (**il callsign non conta**); i turni con le sessioni ATC (nota
   `2026-09-29-il-roster-atc`); **chi ha volato senza prenotare** con la ricerca delle sessioni per aeroporto e finestra **senza
   VID** (estensione n.2), meno chi ha prenotato: **solo il numero**, nessun VID si salva; le **statistiche** in una riga per evento,
   solo numeri, per sempre, ricalcolabili dallo staff.
2. **Il PIREP di supporto** (§17.1 n.11) sostituisce l'iscrizione a un evento: **lo manda chiunque** abbia volato o controllato, da
   `/events/mine`, entro `reportDays` (predefinito **14**, §17.2 n.6) dalla fine — pilota con uno o più voli, ATC con uno o più turni,
   uno per tipo per evento, correggibile finché non è deciso. **Il sistema lo verifica** a ogni invio (`Valid`, `Partial`,
   `Invalid`, o `Unavailable` se la sorgente non risponde) e **lo segnala a chi valida**; **una persona lo valida**
   (`EventReports.Edit`, oggi l'MD), mai il proprio.
3. **La partecipazione automatica** (§17.1 n.12): solo per chi è entrato nell'hub **e ha spuntato** la preferenza «riporta
   automaticamente la mia partecipazione agli eventi» (spenta di predefinito); per lui il sistema crea **un PIREP `Auto`** per pilota
   e uno per ATC, che va ai validatori come gli altri. **Per l'Online Day e gli eventi liberi** si guardano **solo** i VID con la
   preferenza e chi manda a mano: nessuna ricerca per ogni aeroporto della divisione.
4. **Le regole di award dell'evento** (§17.1 n.11): righe dell'evento con un award e un **criterio da un elenco chiuso** —
   `AnyFlight`/`AnyShift` (pilot e ATC support), `MinFlights`, `Route`, `RouteTimes`, `MinLegDistance` (con la distanza fra i due
   aeroporti del nucleo), `MinAtcMinutes` —; un criterio nuovo è una riga dell'elenco nel codice, mai un'espressione libera.
   **Accettare** un PIREP valuta le regole sulle sue voci **verificate** e, per ogni regola soddisfatta, proietta un **segnale di
   award** nella stessa transazione. **Chi ha `Awards.Assign`** (oggi l'MD) lo trova nella coda del nucleo e **assegna a mano**.
5. **Le mail** (§17.1 n.13): a chi è messo in un turno o se il turno cambia; ai validatori, **un riepilogo al giorno** dei PIREP da
   validare (`reportsToValidate`, come il riepilogo dei validatori dei tour); **a chi assegna gli award**, quando entra un segnale
   nuovo in coda — è del nucleo (estensione n.5) e vale anche per i tour. **Tutte disattivabili** dal profilo.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| L'iscrizione a un evento online (`event_participants` del piano) | c2: la partecipazione è il PIREP di supporto |
| Il segnale di award dal calendario («eventi e partecipanti nel periodo», piano §9.7) | l'ED vuole un validatore prima di chi assegna |
| Il sistema che assegna l'award se la verifica è valida | piano §9.7: mai un'assegnazione automatica |
| Salvare i VID di chi ha volato senza prenotare | serve solo il numero; il VID sarebbe un dato personale senza scopo |
| Controllare tutti i membri per l'Online Day | una ricerca per ogni aeroporto della divisione, senza che nessuno l'abbia chiesto (c3) |
| Regole di award scritte come espressioni | un elenco chiuso si prova riga per riga; un'espressione libera no |

## 4. Che cosa si tocca, e dove

**E10a** (nucleo) le sessioni del tracker senza VID; **E10d** (nucleo) la mail a chi assegna; **E10e** (nucleo) la distanza fra
due aeroporti, che oggi sta solo nel modulo dei tour; **E13a** le verifiche e le statistiche;
**E14a** il PIREP di supporto, la verifica, la validazione; **E14b** le regole di award, i segnali, la preferenza, i PIREP automatici e
il riepilogo dei validatori.

## Da portare nel piano

**Già nel piano 1.24**: §9.1 riga Award (la mail a chi assegna, estensione n.5), §9.2 riga Events (PIREP di supporto verificato e
validato), §9.7 «Collaborazioni tra moduli» (il segnale da un PIREP di supporto validato e dalle regole dell'evento) e «Privacy dei
membri» (chi ha volato senza prenotare solo come numero), §10 (il tracker anche senza VID). **Nient'altro.**
