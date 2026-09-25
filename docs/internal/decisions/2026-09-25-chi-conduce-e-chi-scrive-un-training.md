# Chi conduce e chi scrive un training: il grant del trainer, più permessi alternativi, i capi FIR, gli esami

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121, [risposte alle domande di §12][r1], n.1, n.2, n.3 e n.10, tutte
come raccomandato). La **forma nel codice** delle due estensioni del nucleo non è qui: la scrive la nota nuova della loro fase
(A3, A11), che la PR del nucleo porta con sé (`CLAUDE.md` §0 regola 6; `core-guard` la chiede).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il trainer usa un meccanismo che c'è (il grant con scope, nota
`2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`); `[AlsoWrittenWith]` e i grant a una posizione si **estendono**, non si
aggirano nel modulo con una tabella per ruolo. Design `07-design-m3.md` §2.4, §2.8, §3, §8 n.2 e n.7, §12 n.1, n.2, n.3, n.10.

[r0]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832673576
[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Che cosa serviva

La stessa riga `trn_trainings` la scrivono persone diverse, e quasi nessuna ha `Training.Edit` (che è di TC e TAC):

- chi **approva** (TA1–9, `Training.Approve`), chi **assegna** (TC, TAC e, per il loro FIR, i capi FIR, `Training.Assign`), chi
  **conduce** (il trainer assegnato, `Training.Conduct`);
- il **trainer** conduce **i suoi** training e non quelli degli altri, pur vedendoli tutti (R.1, d2);
- un **esame** nel calendario lo inserisce chi l'ha assegnato su IVAO (R.7, d4): anche un TA o un trainer.

Due fatti del nucleo, letti nel codice durante il design e confermati da Carmine nella [prima revisione][r0]:

- il guardiano dell'interceptor (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`) lascia scrivere con `{Area}.Edit` oppure con
  **un** permesso alternativo, e solo in **modifica**: `AlsoWrittenWithAttribute` non si ripete (`AttributeUsage` senza
  `AllowMultiple`), il guardiano legge la prima alternativa e basta, e creare una riga dello staff chiede sempre `Edit`;
- una **posizione FIR non porta permessi**: un grant a una posizione si scrive per dipartimento (`division.json →
  positionGrants`), e `firStaffScope` vale per tutta la divisione.

## 2. Le decisioni

1. **Il trainer conduce con un grant con scope** (n.1). All'assegnazione il modulo scrive con `ModuleGrants` un grant
   `Training.Conduct` al trainer, con lo scope del training (`training:training:{id}`, `IHasResourceScope`), come «aggiungi
   validatore» in M2; riassegnare toglie il grant al trainer di prima. Un **job notturno** del modulo toglie i grant dei training
   chiusi (`Completed`, `NoShow`, `Closed`), perché i grant con scope viaggiano nel cookie e non si lasciano accumulare.
   **Il costo è accettato**: scrivere un grant cambia il security stamp del titolare, quindi il trainer rifà il login alla
   richiesta successiva a ogni assegnazione, e di nuovo — di notte — quando il job glielo toglie.
2. **`[AlsoWrittenWith]` si ripete, e vale anche alla creazione** per l'entità che lo dichiara (n.2). Ogni alternativa si chiede
   come oggi: con lo scope della riga (alla creazione la riga non ne ha ancora uno), mai all'interessato, senza spostare la riga
   fra dipartimenti. È **un cambio del nucleo**: una PR a sé, **con una nota nuova e i test della spina dorsale**, prima del codice
   del modulo che lo usa — la fase **A3**. Le righe figlie del training (disponibilità, sessioni, voti) non ne hanno bisogno: si
   scrivono con il training.
3. **I capi FIR sì, come fase A11** (n.3), dopo che il modulo funziona con TC e TAC: CH e ACH assegnano, e vedono, i training
   **del loro FIR** (`IHasFir` sul training: il FIR della postazione; vuoto per i piloti). L'estensione del nucleo — un permesso di
   un modulo dato a una posizione FIR e contato solo sulle righe di quel FIR, nell'handler e nel guardiano — ha la sua nota nuova e
   i suoi test della spina dorsale nella PR del nucleo di A11. Fino ad allora assegnano TC e TAC, e i training dei piloti restano
   comunque loro.
4. **Gli esami li inserisce chi ha l'esame assegnato** (n.10). L'hub non sa chi è l'esaminatore, quindi `Training.ManageExams` va
   a tutto lo staff del training — TC, TAC, TA e trainer —, la riga `trn_exams` registra chi l'ha scritta (`examiner_vid`), e la
   **creazione** passa il guardiano grazie alla n.2.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| «L'assegnatario della riga», un meccanismo nuovo nell'unico handler | nessun grant e nessun rientro, ma un pezzo nuovo nella spina dorsale per un caso che il grant con scope copre già |
| `Training.Conduct` a tutti i trainer, senza scope | nessun costo, ma ogni trainer potrebbe scrivere il report di un altro |
| `Training.Edit` ai TA, ai capi FIR e ai trainer | molto più largo del bisogno: `Edit` è «tutto su ogni training» |
| I capi FIR dopo M3, e intanto assegnano TC e TAC | il TD li chiede (d1, d2); in fondo a M3 non bloccano niente |
| Gli esami inseriti solo da TC e TAC, per conto di chi esamina | serviva solo se la n.2 alla creazione non fosse passata |

## 4. Che cosa si tocca, e dove

- **A3** (nucleo): `AlsoWrittenWithAttribute` ripetibile e «anche alla creazione», il guardiano di `HubSaveChangesInterceptor`, il
  modulo di prova dei test, i test della spina dorsale. Con la sua nota nuova.
- **A4**: i permessi nel catalogo del modulo e i `positionGrants` del TD (design §3.1, §3.2) in `config/division.json` e in
  `division.example.json`.
- **A7**: l'assegnazione che scrive e toglie il grant, e il job notturno che toglie quelli dei training chiusi.
- **A10**: gli esami, con `Training.ManageExams`.
- **A11** (nucleo, poi modulo): i capi FIR. Con la sua nota nuova.

## Da portare nel piano

- **§16 punto 2** (l'unico handler) e **`CLAUDE.md` §2**: le due estensioni — `[AlsoWrittenWith]` ripetibile e anche alla
  creazione (A3), i permessi di un modulo a una posizione FIR sul suo FIR (A11) —, ciascuna quando la nota della sua fase è decisa.
- **§9.2, riga Training**: il trainer conduce solo i training che gli sono assegnati, con un grant con scope scritto
  all'assegnazione e tolto a training chiuso; i capi FIR assegnano nel loro FIR (A11).
