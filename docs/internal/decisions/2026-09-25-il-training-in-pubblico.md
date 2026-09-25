# Il training in pubblico: niente VID ai visitatori

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121, [risposte alle domande di §12][r1], n.4, come raccomandato).
**Regola applicata:** piano §9.7, «Privacy dei membri» (di un membro si mostra il minimo necessario); `CLAUDE.md` §5, caso **(a)**:
che cosa mostrano una proiezione e una pagina del modulo. Design `07-design-m3.md` §0.4, §0.5, §4.1, §4.3, §5.1, §12 n.4.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Che cosa serviva decidere

- **Lo staff TD vuole pubblici**, di un training programmato, **VID, postazione, data e ora**, e i nomi solo a chi ha fatto il
  login (R.6-bis, d3).
- **Il piano** (§9.7) chiede di mostrare di un membro il minimo necessario; l'hub non ha profili pubblici, e un VID identifica la
  persona (è la chiave del suo profilo IVAO).
- **L'elenco pubblico di PATS** oggi il VID non lo mostra: la vista `trainingslist` porta postazione, rating, data e ora, senza
  nomi (design §0.5).

## 2. La decisione

**Niente VID ai visitatori.** A chi non ha fatto il login un training mostra **postazione, rating, data e ora**; **VID e nomi** li
vede solo chi ha fatto il login, sulla **pagina della sessione** (`/training/sessions/{id}`).

- **La voce del calendario unico** (tipo `training`, pubblica) ha un titolo **senza nomi e senza VID** — rating e postazione — e
  punta alla pagina della sessione. Un training non va nella ricerca (design §5.1).
- **La pagina della sessione** risponde per chi guarda: con il login anche VID e nomi.
- **Il blocco `training.upcomingSessions`** (la pagina `/training` e le pagine del CMS) segue la stessa regola: senza nomi per chi
  non ha fatto il login.
- **Gli esami** sono voci dello stesso calendario pubblico (tipo `exam`) e seguono la stessa regola: niente VID né nomi del
  candidato ai visitatori.

## 3. Alternativa scartata

| Alternativa | Perché no |
|---|---|
| VID pubblico con postazione, data e ora, come chiede lo staff TD | il piano chiede il minimo necessario, e l'elenco pubblico di PATS già non lo mostra; chi ha fatto il login lo vede comunque |

## 4. Che cosa si tocca

Solo il modulo: la proiezione della sessione nel calendario, con il titolo senza nomi (**A8**); la pagina pubblica della sessione,
il blocco `training.upcomingSessions` e gli esami nel calendario (**A10**).

## Da portare nel piano

- **§9.7, «Privacy dei membri»**: nel calendario pubblico nessun VID ai visitatori; VID e nomi di un training solo a chi ha fatto
  il login, sulla pagina della sessione.
- **§8.2, sitemap** — non è una domanda di §12 ma uno scostamento dichiarato nel design (§0.4, §0.6), approvato con il design nella
  PR #121; è qui perché riguarda le stesse pagine: quelle del membro stanno sotto `/training` (`/training/request`,
  `/training/mine`), e `/me` riceve il blocco `training.myTraining` invece di una pagina `/me/training`.
