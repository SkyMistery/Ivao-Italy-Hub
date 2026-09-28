# Il trainer sulla regola delle righe affidate: A7 resta com'è, e dopo A3b arriva una fase A7b

**Data:** 27 settembre 2026, alla revisione di A7 (#146); scritta il 28 settembre 2026 con le correzioni della revisione
**Stato:** **decisa** (Carmine, 27 settembre 2026, [commento sulla #146][d]). È la nota che la [risposta 2 sulla #135][a2] chiedeva ad
A7: sposta il momento, non il punto d'arrivo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**. Il trainer userà un meccanismo che c'è già, la regola delle righe affidate di A3b
(nota `2026-09-26-le-righe-affidate-a-chi-scrive`); fino ad allora A7 usa quello che c'era quando è stata scritta, il grant con scope
della n.1 (nota `2026-09-25-chi-conduce-e-chi-scrive-un-training` §2.1).

[d]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855560982
[a2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250425
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855673527
[m]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5869116757
[r8]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/147#issuecomment-5855683074

## 1. Che cosa è successo

- **La n.1 del design** (`07-design-m3.md` §3.3, §12 n.1): il trainer conduce con un grant `Training.Conduct` con lo scope del
  training, scritto all'assegnazione e tolto da un job notturno a training chiuso. Il costo accettato era un rientro del trainer a ogni
  grant scritto o tolto.
- **La risposta 2 sulla #135** (Carmine, 26 settembre 2026, [commento][a2]) ha deciso l'opposto per il trainer:
  - il training dichiara il suo trainer con `IHasAssignee`;
  - `Training.Conduct` è segnato `OnlyForAssignee`, e si tiene per posizione;
  - vanno via il grant per assegnazione e il suo job.

  Chiedeva ad A7 di scriverlo in una nota sua e in `08`, e di cambiare n.1 in `07` nella stessa PR.
- **A7 (#146) ha fatto il contrario**: scrive il grant a ogni assegnazione e aggiunge il job `training-expiry` che lo toglie. Il design
  sul branch diceva ancora n.1, e gli scostamenti in `08` non nominavano la risposta. **A3b (#135) non era unita**, quindi A7 non poteva
  usare la regola senza aspettarla; e la coda sopra A7 (A8a–A10b) era già costruita sul grant.

## 2. La decisione

- **A7 resta com'è**, e la coda da A7 ad A10a va avanti.
- **Dopo l'unione di #135** (A3b, unita il 27 settembre 2026) **una fase A7b porta il trainer sulla regola della risposta 2**:
  - il training dichiara il suo trainer con `IHasAssignee`;
  - `Training.Conduct` è segnato `OnlyForAssignee`, e lo tengono per posizione i TA e i trainer;
  - vanno via il grant con scope scritto all'assegnazione e la metà di `training-expiry` che lo toglie;
  - si adattano i test di A7–A10a che seminano il grant;
  - la modifica a `07` (n.1) sta nella PR di A7b.
- **Il punto d'arrivo è la risposta 2: cambia solo il momento.**

## 3. Che cosa si tocca, e quando

- **In A7, con le correzioni della revisione** (#146):
  - questa nota;
  - lo scostamento in `08`, A7, «Com'è andata», con il link al commento di Carmine;
  - la fase A7b fra le fasi di `08`, dopo A7.

  Per la decisione il codice di A7 non cambia. Le correzioni della [revisione][r] valgono fino ad A7b: il 409 dell'assegnazione che
  toglieva il grant al trainer nominato dal training, il giro sul banco, la pagina dopo un 409.
- **In A7b**, su un branch suo, dalla cima della coda quando comincia, perché cambia i test di A7–A10a:
  - la lista della fase in `08`, A7b. Raccoglie anche quello che hanno aggiunto la revisione di A8a ([«For A7b»][r8]: il ritorno di
    `TrainingExpiryJob.RunAsync`, i `GiveConductAsync` dei test, l'`[AlsoWrittenWith(Conduct)]` che rispetta la regola
    dell'assegnatario, il commento in cima a `TrainingDates.cs`) e il revisore dopo l'unione di A3b ([i tre punti][m]:
    `[PermissionArea]`, che cosa fa DELETE, `DeniedToStakeholder` insieme a `OnlyForAssignee`);
  - la modifica a `07`, §3.3 e §12 n.1.
- **Il nucleo**: niente. La regola è di A3b. Se A7b trovasse che le manca qualcosa, è una fase del nucleo a sé, con la sua nota.

## Da portare nel piano

- **§9.2, riga Training**, e il punto «Nel training» della 1.19, che dice «lo porta A7»: il trainer passa sulla regola delle righe
  affidate con **A7b**, dopo A3b. Fino ad A7b conduce con il grant con scope che A7 scrive all'assegnazione e che `training-expiry`
  toglie.
- **§13, riga M3**, se nomina le fasi del modulo: A7b dopo A7, quando #135 è unita.
