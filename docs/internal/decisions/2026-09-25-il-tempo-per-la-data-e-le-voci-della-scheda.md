# Che cosa configurano TC e TAC: il tempo per scegliere la data e le voci della scheda

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121, [risposte alle domande di §12][r1], n.9 e n.11, tutte e due come
raccomandato). Le due domande non avevano alternative scritte oltre alla raccomandazione.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** dentro meccanismi che esistono: le impostazioni dei moduli (`IModule.Settings`,
nota `2026-09-16-impostazioni-dei-moduli`) e `Localized<T>` (piano §16 punto 1). Design `07-design-m3.md` §1.4, §1.6, §2.5, §5.3,
§12 n.9 e n.11.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Il tempo per scegliere la data (n.9)

- **Il caso.** Il trainer propone le sue disponibilità e il trainee non sceglie. Il TD non vuole un tempo massimo di default, ma
  configurabile; dopo `responseReminderDays` giorni (3) il training compare in evidenza nel blocco del trainer; e se il trainee non
  risponde mai, lo staff chiude la richiesta, che resta in memoria (R.3, d2, d4).
- **La decisione.** Con **`maxResponseDays`** impostato, un job chiude **da solo** i training rimasti senza scelta oltre quel tempo
  (`Closed`, con il motivo; mail `trainingClosed`, R.7). **Di default l'impostazione non c'è**, e allora nessuno chiude da solo. Lo
  staff chiude a mano quando vuole (`Training.Approve`), con un motivo.
- **Il job** è `training-expiry`, notturno: lo stesso che toglie i grant con scope dei training chiusi (design §5.3, nota
  `2026-09-25-chi-conduce-e-chi-scrive-un-training`).

## 2. Le voci della scheda tradotte (n.11)

- **Il caso.** Le voci della scheda di valutazione (`trn_sheet_items`) le configurano TC e TAC per percorso e rating (R.5, d1, d3),
  e il trainee le legge nel suo report.
- **La decisione.** Il titolo di una voce è **tradotto**: `Localized<string>` con **tutte le lingue della divisione**, come ogni
  testo che un utente legge, e il validatore del nucleo le chiede tutte. La scheda compilata (`trn_evaluations`) fotografa titolo e
  tipo di voto della voce, così un report vecchio non cambia quando la voce cambia; una voce usata da un report non si elimina, si
  disattiva.

## 3. Che cosa si tocca

Solo il modulo: `TrainingSettings` con `maxResponseDays` vuoto e gli altri predefiniti del design §1.6 (**A4**); `trn_sheet_items`
con il titolo `Localized`, lista e form generati dietro `Training.ManageSheets` (**A5**); la chiusura per tempo nel job
`training-expiry` (**A8**); la fotografia della voce nella scheda compilata (**A9**).

## Da portare nel piano

- Niente: sono un'impostazione e un dato del modulo, dentro meccanismi decisi (design §1.4, §1.6).
