# La contestazione aperta di chi si cancella (T20b)

**Data:** 25 settembre 2026 — fase T20b di M2, PR del modulo dei tour
**Stato:** **decisa** (Carmine, 25 settembre 2026, in chat: la raccomandazione)
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** dentro il meccanismo già deciso (nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`):
che cosa fa l'eraser del modulo in un caso che quella nota non aveva visto.

## La domanda

Un pilota contesta un rifiuto e, mentre la contestazione è ancora aperta, chiede che i suoi dati siano cancellati. Il PIREP resta nel
registro (è deciso), ma il filo della contestazione va via con gli altri fili che la persona ha aperto, e con lui il suo testo. Che cosa
ne è della contestazione?

| Opzione | |
|---|---|
| **Chiusa dal modulo, respinta** — scelta | La cancellazione la chiude come `Dismissed`, senza decisore (`dispute_decided_by_vid` vuoto) e con un passo nella storia, fatto dal modulo (`by_vid = 0`), `flightops:events.disputeClosedByErasure`. Il rifiuto resta nel registro, la leg torna a contare come rifiutata, e la contestazione esce dalla coda. |
| Resta aperta | Lo staff si troverebbe da decidere una contestazione senza il suo testo né il filo, per una persona che non c'è più. |
| La cancellazione aspetta | L'anteprima avvertirebbe e la cancellazione verrebbe rifiutata finché lo staff non decide: una richiesta della persona dipenderebbe dai tempi dello staff. |

## Che cosa si tocca

`People/FlightOpsPersonalData.cs` (la chiusura, `DisputeClosedKey`), le parole del passo in `locales/*/flightops.json` del modulo,
`PirepTests.Erasure.cs` (il pilota contesta prima di chiedere la cancellazione).

## Da portare nel piano

- `05-design-m2.md` §10.0: la contestazione aperta si chiude respinta.
- `00-piano-di-progettazione.md`: nel changelog di 1.10.

Portati nella stessa PR, perché la scrive la sessione di Carmine.
