# La mappa che aspettava la mappa di base

**Data:** 11 novembre 2026, fase 6 (le pagine dei tour), trovato misurando la #484.
**Stato:** **deciso da Carmine** in chat al master l'11 novembre 2026, «(a) nel nucleo», riportato sulla #482:
https://github.com/SkyMistery/Ivao-Italy-Hub/pull/482#issuecomment-6797452647.
**Classificazione** (`CLAUDE.md` §5): (b), un difetto di un meccanismo del nucleo (`RouteMap`, `shared/ui/`, elenco chiuso), corretto
nel nucleo con una PR sua, prima del modulo che ne ha bisogno (§0, regola 6).

## Il difetto

`RouteMap` aggiungeva le leg e i marcatori degli aeroporti sull'evento `load` di MapLibre. `load` aspetta **tutte** le sorgenti, anche la
mappa di base (`/tiles/basemap.pmtiles`). Quando l'archivio manca — in CI, sul banco, in un'installazione che non l'ha ancora caricato —
la sua sorgente fallisce («Bad response code»), e MapLibre manda `load` solo al ridisegno successivo. Durante il caricamento di una pagina
un ridisegno c'è sempre, e la mappa di `main` si è sempre disegnata. Una mappa creata più tardi, su una pagina ferma, non lo riceve mai:
è la scheda «Percorso» della pagina di un tour (#482), aperta con un clic. Lì la mappa restava senza leg e senza aeroporti, e la #484 è
andata rossa su `e2e/full/tours-public.spec.ts`.

Il commento del componente dice già che le leg non hanno bisogno della mappa di base: «a map without countries, never an error and never
an empty box». Il codice aspettava proprio quella.

## La misura

Bundle costruito, API simulata, nessun archivio dietro, Chromium, sei prove ciascuna, fino al marcatore `LIMC` sullo schermo (mediane):

| Caso | CPU ×1 | CPU ×4 |
|---|---|---|
| `main`, caricamento della pagina | 356 ms | 763 ms |
| #482 con `load`, clic su «Percorso» | mai (60 s, 6 su 6) | mai |
| #482 con `style.load`, clic su «Percorso» | 101 ms | 228 ms |
| #482 con `style.load`, caricamento con `?tab=route` | 208 ms | 582 ms |

Con gli eventi annotati in una build locale: sul percorso del clic arrivano `style.load`, `styledata` e l'errore della sorgente, `load` no.
Non è il download di MapLibre (#400, aperta): al clic si scaricano solo il worker e l'archivio.

## La scelta

- **(a) nel nucleo — scelta da Carmine.** `RouteMap` aggiunge leg e marcatori su `style.load` (`once`): lo stile c'è, le sorgenti delle
  leg si possono aggiungere, la mappa di base arriva quando arriva o non arriva. Il test `shared/ui/RouteMap.test.tsx` lo tiene con una
  mappa finta che manda solo `style.load`; con `load` cadono entrambi i suoi casi (mutazione provata l'11 novembre 2026).
- (b) solo nel modulo: tenere montata la scheda «Percorso» dal caricamento della pagina. Scartata: lasciava il componente fragile per
  ogni altro uso (la pagina di validazione, qualunque mappa aperta dopo).

L'attesa di 30 s aggiunta a `tours-public.spec.ts` sulla #482 si toglie: la spec torna com'era.

## Da portare nel piano

- **§8.3** (l'elenco chiuso dei componenti): `RouteMap` disegna leg e marcatori quando lo stile è pronto (`style.load`), senza aspettare
  la mappa di base; un'installazione senza archivio ha la mappa delle leg anche aperta dopo il caricamento della pagina.
- **Design M2 §8.6** (la mappa): la stessa precisazione, accanto a «la base non porta i nomi dei luoghi».
