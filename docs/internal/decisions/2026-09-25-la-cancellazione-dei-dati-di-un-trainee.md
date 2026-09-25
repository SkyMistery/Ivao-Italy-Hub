# La cancellazione dei dati di un trainee: il registro dei training resta, senza testi

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121: la n.7 riformulata, nel [secondo giro][r2], come raccomandato).
La prima risposta, nelle [risposte alle domande di §12][r1], era sulla domanda prima della riformulazione e dice lo stesso: il
registro resta, il meccanismo è quello del nucleo di T20b, il modulo non ne scrive uno suo. La regola è quella delle quattro
risposte della nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il modulo si aggancia al meccanismo del nucleo (`IPersonalDataEraser`, piano
§16 punto 16), non ne scrive uno suo. Design `07-design-m3.md` §6, §6.1, §8 n.10, §12 n.7.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832839987

## 1. Che cosa serviva decidere

Il meccanismo c'è (T20b, piano 1.08, 1.09 e 1.10): il superadmin cancella dal pannello dei permessi; il nucleo scrive lo
**pseudonimo negativo** in ogni colonna `vid`, `*_vid` e `*_by` di ogni contesto; ogni modulo che tiene dati di persone registra un
`IPersonalDataEraser` per ciò che è **sulla** persona, e passa a `ErasureRequest.Keep` una riga che deve restare con il VID.
Restava che cosa il modulo del training tiene come **registro**.

## 2. La decisione

`TrainingPersonalData : IPersonalDataEraser`, con questa regola:

- **I training chiusi** del trainee (`Completed`, `NoShow`, `Closed`, `Rejected`, `Cancelled`) sono **il registro**: restano, con
  lo pseudonimo, e perdono **tutti i testi liberi** che parlano di lui — i due della richiesta (disponibilità e note), i commenti
  per il trainee, le note riservate, i commenti del report, gli appunti delle sessioni, il motivo di un rifiuto. Restano stati,
  date, rating, voti e spunte, così i conteggi del TD (training per trainer, per rating) restano uguali.
- **I training aperti** (`Requested` … `Scheduled`) **si cancellano**: non vanno avanti senza la persona, come i PIREP aperti.
- **Gli esami** in cui la persona è candidata **si cancellano**: sono voci di calendario, e l'esame è su IVAO.
- **Un ban in vigore resta** con VID e motivo, con `ErasureRequest.Keep(ban)` (piano 1.09, nota
  `2026-09-25-le-righe-che-restano-con-il-vid`); uno scaduto o tolto si anonimizza (risposta 2 della nota di T20b). Rilanciare la
  cancellazione quando il ban scade lo anonimizza.
- **Ciò che la persona ha fatto come staff o come trainer** — training condotti, decisioni, assegnazioni, esami inseriti, ban dati
  — resta con lo pseudonimo, e i suoi testi restano perché parlano di altri (risposta 4 della nota di T20b).
- **Le colonne seguono la convenzione**: `trainee_vid`, `trainer_vid`, `examiner_vid`, `decided_by`, `assigned_by`, `closed_by`, il
  `vid` e i `*_by` dei ban. Nessuna lista di VID in JSON.
- **«Persona cancellata»**: dove il modulo mostra un VID (percorso del trainee, liste, pagina della sessione), un VID negativo
  diventa «persona cancellata», senza link. L'helper oggi sta nel modulo dei tour (`memberName`); la nota di T20b dice che passa
  nel nucleo quando un secondo modulo ne ha bisogno: è l'estensione n.10, nella PR del nucleo di **A12**.

## 3. Alternativa scartata

| Alternativa | Perché no |
|---|---|
| Cancellare anche i training chiusi | il percorso del trainee non servirebbe più a nessuno, ma i conteggi del TD (training per trainer, per rating) cambierebbero |

## 4. Trovato leggendo il codice, per A12

- ⚠️ **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` non vedrà da solo le colonne del training**, come il
  design §6.1 dava per scontato: il test legge i contesti scritti nel test (`HubDbContext`, `FlightOpsDbContext`) e confronta con
  una lista scritta a mano. La cancellazione le riscrive lo stesso, perché la convenzione è sui nomi; ma la lista con cui la
  revisione confronta una tabella nuova non le mostra. Aggiungere `TrainingDbContext` e le colonne `trn_` vuol dire modificare un
  test condiviso: per `core-guard` è nucleo, quindi va nella PR del nucleo di A12, con la sua nota.
- **Un training aperto cancellato si porta via il grant con scope del suo trainer** (`ModuleGrants`): il job notturno toglie i
  grant dei training chiusi, non quelli di un training che non esiste più.
- ⚠️ **La copia dei tour dell'helper** (`web/src/modules/flightops/`) il collaboratore non la tocca (`CLAUDE.md` §0 regola 2):
  resta dov'è finché una sessione di Carmine non la sostituisce con quella del nucleo, e la PR di A12 lo dice al revisore.

## 5. Che cosa si tocca

- **A12, PR del nucleo**: l'helper «persona cancellata» nel nucleo e `ErasureTests` con `TrainingDbContext`, con la loro nota nuova.
- **A12, PR del modulo**: `TrainingPersonalData` (anteprima ed esecuzione) e i test del design §10 — i conteggi del registro
  uguali prima e dopo, nessun testo libero rimasto, i training aperti e gli esami del candidato spariti, il ban in vigore rimasto,
  «persona cancellata» nelle pagine.

## Da portare nel piano

- Niente di nuovo: §9.7 e §16 punto 16 dicono già il percorso, e la regola del modulo sta nel design §6.1. Lo spostamento
  dell'helper «persona cancellata» nel nucleo lo porta la nota della PR del nucleo di A12.
