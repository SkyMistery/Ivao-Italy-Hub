# Gli esami li inserisce chi esamina: `Training.ManageExams` senza i trainer (A4)

**Data:** 26 settembre 2026 — fase A4 di M3
**Stato:** **Decisa** da Carmine il 26 settembre 2026, **come raccomandato**: [«yes»][a] alla domanda della [issue #134][q] (§5).
**La decisione vale nella sua nota**, `2026-09-26-gli-esaminatori` (#136, piano 1.14), presa lo stesso giorno sulla #131
([commento][c]), che corregge la n.10; questa registra come A4 ci è arrivata, e che il seme del commit 969c02e resta. Era uno
scostamento da una decisione scritta — la n.10 (nota `2026-09-25-chi-conduce-e-chi-scrive-un-training` §2 punto 4, design
`07-design-m3.md` §3.2 e §12 n.10) —, per un fatto nuovo del TD, e per questo non bastava «Com'è andata» (`08`, «Regole di tutte le
fasi»): lo ha notato la sessione di A3.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** — è configurazione della fase (`positionGrants` del TD) —, ma con la conferma del
maintainer, perché cambia l'elenco che la sua decisione scrive. Sui fatti risponde `dalberone`; le scelte le decide Carmine
(`HANDOFF-M3.md`, «La prima sessione: il design»).

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/134
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/134#issuecomment-5844363804
[c]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5844102750

## 1. Che cosa dice la decisione

- **La regola** (n.10, Carmine, 25 settembre 2026): **gli esami li inserisce chi ha l'esame assegnato**, e la creazione passa il
  guardiano con la n.7 (A3).
- **L'elenco che ne veniva**: `Training.ManageExams` a **tutto lo staff del training — TC, TAC, TA e trainer** —, perché «l'hub non sa
  chi è l'esaminatore» e perché la risposta d4 di `dalberone` diceva che un esame lo ha **«anche un TA o un trainer»** (design §2.8,
  §3.2, §3.4; nota n.10 §1 e §2 punto 4).

## 2. Il fatto nuovo

- **Il 26 settembre 2026 `dalberone` (staff TD) ha precisato**, nella sessione di A4 e in quella di A3: **un esame si assegna solo a
  un esaminatore, e gli esaminatori sono HQ, TC, TAC e i TA (TA1–9), come da regole; mai un trainer (T01–T99).** È coerente con la
  risposta del TD sulla #131 (nota di A3 §3.5: un esame lo tolgono dall'hub HQ, TC, TAC e **il TA a cui è assegnato**), su cui Carmine
  ha scelto la risposta 4.
- HQ, TC e TAC hanno già tutto; di chi esamina, restano da coprire i TA.

## 3. La proposta (raccomandata)

**`Training.ManageExams` a TC, TAC e TA1–9, e non ai trainer**, che tengono `Training.View` e basta; HQ lo ha già, come ogni permesso
di dipartimento. La regola della n.10 non cambia — l'esame lo inserisce chi ce l'ha —, cambia solo l'elenco che ne segue, perché è
cambiato il fatto da cui veniva.

**Perché ora, in A4**: un seme di `positionGrants` si applica **una volta sola** (`PositionGrantSeeder`, per impronta): cambiarlo
dopo il rilascio lascerebbe il grant già scritto in ogni installazione, da togliere a mano dalla schermata dei permessi. A4 non è
ancora in nessuna installazione.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lasciare `ManageExams` anche ai trainer, come scrive la n.10 | più largo del bisogno: ogni trainer potrebbe mettere in calendario qualunque esame, e un esame non lo ha mai |
| Toglierlo dopo, quando A10 fa gli esami | il seme si applica una volta: ogni installazione dovrebbe togliere il grant a mano |

## 5. La domanda per Carmine

**`Training.ManageExams` va a TC, TAC e TA1–9 e non ai trainer, perché un esame si assegna solo a chi esamina — HQ, TC, TAC e i TA?**
**Raccomandata: sì.** Se la risposta è no, i trainer tornano nel seme di A4 com'era nel design (una riga di `division.json` e
dell'esempio, e i tre test che lo controllano).

**Risposta di Carmine, 26 settembre 2026** ([commento sulla issue #134][a]): **sì**. `Training.ManageExams` va a TC, TAC e TA1–9, e i
trainer tengono `Training.View` e basta; è la stessa decisione presa sulla #131 ([commento][c]) e scritta nella nota
`2026-09-26-gli-esaminatori`. Il seme del commit 969c02e resta; la issue è chiusa.

## 6. Che cosa si tocca

- **A4**: il seme di `Training.ManageExams` in `config/division.json` e `division.example.json`; `TrainingArchitectureTests` (i semi
  contro il design), `TrainingSkeletonTests` (il trainer ha solo `View`), `web/e2e/full/training-skeleton.spec.ts` (il trainer del
  banco ha solo `View`).
- **A3b** e **A10**: gli esami affidati all'esaminatore a cui sono assegnati.

## Da portare nel piano

- Niente di più: il design (§2.8, §3.2, §3.4, §10, §12 n.10), `08` (A3b, A4, A10) e il piano §9.2 li ha già allineati la sessione del
  maintainer con #136 e la nota `2026-09-26-gli-esaminatori`.
