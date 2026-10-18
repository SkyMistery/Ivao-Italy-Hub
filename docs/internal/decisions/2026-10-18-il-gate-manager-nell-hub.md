# Il Gate Manager nell'hub, ed E9 senza il vecchio Gate Manager

**Data:** 18 ottobre 2026
**Stato:** **decisa da Carmine in chat al master**, 18 ottobre 2026 (§2). Nessun codice: è una nota del master, portata nel piano
1.38. Il Gate Manager avrà il suo documento di design, da approvare prima del codice (`CLAUDE.md` §0 regola 5, §5 caso (c)).
**Regola applicata:** cambia la fase E9 del piano di M4 (`10-piano-implementazione-m4.md`, «E9 — Fuori dal repository: il Gate
Manager, il primo evento, `ivao-booking` spento») e il design M4 §7.4, §17.2 n.7.

## 1. Che cosa diceva il piano

E9 voleva che il **Gate Manager** della divisione — l'applicazione `RFO Gate manager`, fuori da questo repository — leggesse le
prenotazioni dall'hub con un token personale `events.bookings` (l'esportazione di E5, `docs/events-bookings-export.md`), provato
sull'evento `prova-ponte-rfo`; poi il primo evento vero sull'hub e `ivao-booking` spento con un 301 verso `/events`.

## 2. Che cosa ha deciso Carmine

1. **Il Gate Manager attuale è provvisorio** («quello è un temporaneo fatto per apparare»): **non lo si collega all'hub**.
2. **Lo si rifà da zero dentro l'hub** («lo facciamo noi da 0 basandoci sul nostro sistema»), **nel modulo Eventi** (strada A, non un
   modulo a parte): gli slot e le prenotazioni sono già lì.
3. **Chi lo usa**: lo staff eventi (ED) e **chi gestisce i gate**, per esempio i capi dei FIR (CH).
4. **Si parte da zero**, non da ciò che l'app provvisoria fa oggi («No, partiamo da 0»).
5. **Quando**: **dopo la fine di tutte le altre fasi di M4**, **in parallelo ai test** sull'installazione di prova («lo rimanderei a
   dopo la fine di tutte le altre fasi e lo terrei in parallelo al testing»). Il primo passo è il suo documento di design.
6. **L'ordine**: prima si chiudono gli eventi (le fasi aperte e E10r), poi i test sulla prova — la prima prenotazione di prova di
   Carmine, il giro completo —, poi si decide quando andare in produzione («Facciamo prima E9, chiudiamo gli eventi, facciamo i test e
   poi vediamo quando metterlo in produzione»).

## 3. Che cosa cambia

- **E9 perde il pezzo «il Gate Manager legge l'hub»**. Restano: i test sulla prova (una parte che si può fare subito); in produzione
  (`it.ivao.aero`, nota `2026-10-18-la-produzione`) il primo evento vero e `ivao-booking` spento con il 301.
- **L'esportazione delle prenotazioni** (E5, `ContractVersion`, `docs/events-bookings-export.md`) resta com'è: non ha più il suo primo
  cliente, ma è un contratto pubblico e chi forka può usarla. Che cosa ne fa il Gate Manager nuovo lo dirà il suo design.
- **Il Gate Manager** diventa una funzione nuova del modulo Eventi, con il suo design (permessi, chi gestisce quali gate, che cosa vede
  il pilota): una fase nuova di M4, dopo E15b, da numerare quando il design è approvato.

## Da portare nel piano

- **§13 riga M4** e **§15 punto 12** (gli eventi dopo M4): E9 senza il vecchio Gate Manager; il Gate Manager nell'hub, nel modulo
  Eventi, dopo le altre fasi, in parallelo ai test, dal suo design.
- **Design M4** §7.4 e §17.2 n.7: il Gate Manager non è più un'applicazione esterna che legge l'hub.
- **`10-piano-implementazione-m4.md`** (è del collaboratore: lo aggiorna `dalberone` con E15b o dopo, citando questa nota) — E9 e la
  fase nuova del Gate Manager.
