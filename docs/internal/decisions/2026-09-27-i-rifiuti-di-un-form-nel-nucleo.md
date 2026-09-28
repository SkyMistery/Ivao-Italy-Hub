# I rifiuti di un form nel nucleo: `Refusals` accanto a `CrudProblems`

**Data:** 27 settembre 2026 — PR del nucleo, dal lato del maintainer (sessione di lavoro), dopo #143 (A6a di M3)
**Stato:** **Decisa** da Carmine il 27 settembre 2026 ([il suo commento su #143][d], sul [rilievo 1 del revisore][r]): la copia di
A6a entra così com'è; una PR del nucleo, con la sua nota, mette `Refusals` nel nucleo accanto a `CrudProblems` e i tour lo usano;
poi il training toglie la sua copia nella prima fase che il collaboratore apre dopo il merge.
**Regola applicata:** `CLAUDE.md` §2 (un pezzo usato in due posti si scrive una volta) e §5, caso **(b)**: il meccanismo c'è — i
`ProblemDetails` che i form generati leggono, una o più chiavi i18n per campo, scritti da **`CrudProblems`** (design M0 §3.9 e §7.5,
piano §16 punto 6) — e non copre un caso: chi raccoglie i rifiuti a mano, fuori da un validatore. Si estende il meccanismo invece
di riscriverlo in ogni modulo; è una PR del nucleo, prima del codice di modulo che la usa (`CLAUDE.md` §0, regola 6).

[d]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/143#issuecomment-5855666298
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/143#issuecomment-5855666152

## 1. Che cosa è successo

- `CrudProblems.Validation` ha **due porte**: un `ValidationResult` di FluentValidation, oppure due dizionari già fatti — le chiavi
  per campo e le lingue mancanti per campo — per chi non è un validatore (la pubblicazione, M1, per prima). La seconda porta lascia
  a chi la usa il lavoro di **costruirsi** i dizionari.
- I tour quel lavoro l'hanno scritto **due volte**:
  - `Refusals`, classe privata in fondo a `FlightOps/Pireps/PirepSubmission.cs` (il report del pilota, T11): le chiavi per campo,
    ognuna una volta;
  - `TourProblems`, in `FlightOps/Tours/TourRules.cs` (che cosa manca a un tour per essere «pronto», T6a): le stesse chiavi per
    campo, **senza** togliere i doppioni, e in più le lingue mancanti accanto a un campo tradotto.
- A6a (#143) ha copiato la prima **carattere per carattere** in `src/IvaoHub.Modules.Training/Refusals.cs`: la terza scrittura dello
  stesso pezzo, e il collaboratore non poteva fare altro, perché i tour non sono suoi e un modulo non ne riferisce un altro.

## 2. La decisione

Un solo tipo nel nucleo, **`IvaoHub.Core.Data.Crud.Refusals`**, accanto a `CrudProblems` (`Core/Data/Crud/Refusals.cs`):

- `Add(field, key)` — rifiuta il campo con quella chiave i18n; restituisce se stesso, così un rifiuto solo sta in una riga
  (`new Refusals().Add("status", "…").Errors`).
- `Missing(field, locales)` — un campo tradotto non scritto in ogni lingua della divisione: la chiave del validatore
  (`errors.localized.missing`, `LocalizedRules.MissingMessageKey`) e le lingue accanto, perché il form possa dire quale manca
  (design M0 §3.1).
- `IsEmpty`; `Errors`, le chiavi di ogni campo **ognuna una volta**, nell'ordine in cui sono arrivate, campi confrontati in modo
  ordinale; `MissingLocales`, le lingue di ogni campo rifiutato con `Missing`, ognuna una volta. Tutti e due sono copie: ciò che si è
  letto non cambia se dopo si aggiunge.
- **Una terza porta di `CrudProblems`**: `Validation(Refusals, catalog, locale)`, che passa a quella dei due dizionari. È la stessa
  risposta 400 delle altre due, estensione `localized` compresa solo quando qualche lingua manca; un test la confronta con quella di
  un validatore che rifiuta gli stessi campi. Le due porte che c'erano non cambiano.

## 3. Che cosa si tocca

- **Nucleo**: `Core/Data/Crud/Refusals.cs` (nuovo); `Core/Data/Crud/CrudProblems.cs` (la terza porta);
  `tests/IvaoHub.UnitTests/RefusalsTests.cs` (nuovo, sei test).
- **Tour**: `PirepSubmission.cs` perde la classe privata; `TourRules.cs` perde `TourProblems`, e `TourReadiness.ProblemsAsync`
  restituisce un `Refusals`; `TourEndpoints.cs` e `LegEndpoints.cs` passano quel `Refusals` a `CrudProblems`, e il DTO di «che cosa
  manca per pronto» (`TourReadyProblemsDto`, il suo campo `Localized` non cambia nome) prende `MissingLocales`. **Nessun test dei
  tour toccato.**
- **Training**: niente. La copia `Training/Refusals.cs` la toglie il collaboratore, come dice [il commento di Carmine][d]. Finché c'è,
  compila accanto a quella del nucleo: dentro il namespace del training il nome trova prima la sua.

## 4. Perché le risposte non cambiano

- **Report del pilota**: `Refusals` del nucleo fa ciò che faceva la copia privata — stesse chiavi, stesso ordine, stessi confronti.
- **`TourProblems`** si comportava in modo diverso in due casi soli:
  1. la **stessa chiave due volte sullo stesso campo** restava doppia (il nucleo la tiene una volta);
  2. **`Missing` due volte sullo stesso campo** teneva le lingue dell'ultima chiamata (il nucleo le unisce).

  Nei tour **nessuno dei due può accadere**: ogni regola di `TourSaving.PrepareAsync` e di `TourReadiness.ProblemsAsync` rifiuta un
  campo con una chiave diversa dalle altre; i campi per riga portano una chiave che le distingue — `legs.{numero}` (numerati da 1
  senza buchi a ogni scrittura, `LegBook.Renumber`), `hubs.{icao}` (indice unico `tour_id, icao`),
  `rotations.{icao}.{posizione}`, `openGoalParameters.{campo}` (un problema per campo, `OpenCatalog`, `OpenParameterCheck`); e
  `Missing` è chiamata una volta per `title`, una per `summary` e una per ogni percorso del briefing, che `BlockDocumentWalker`
  restituisce ciascuno una volta. Se un giorno accadesse, il form mostrerebbe il messaggio una volta invece di due, come fa già
  `CrudProblems` con un validatore.
- L'ordine dei campi e delle chiavi, `IsEmpty`, e l'estensione `localized` (assente quando nessuna lingua manca) restano quelli di
  prima.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lasciare le copie | tre scritture dello stesso pezzo, contro `CLAUDE.md` §2; e la prossima differenza fra loro sarebbe un form che risponde in due modi |
| Il tipo in un modulo, usato dall'altro | un modulo riferisce solo il nucleo (`CLAUDE.md` §2, regole strutturali) |
| Solo la copia del report del pilota, `TourProblems` lasciato ai tour | è lo stesso pezzo con le lingue in più: resterebbe una seconda scrittura, e con un comportamento diverso sui doppioni |
| I verbi dei tour e del training restituiscono `Refusals` invece di un dizionario | cambia firme usate dagli endpoint e dai test dei tour senza cambiare nessuna risposta; la porta nuova di `CrudProblems` basta, e un verbo può passare a `Refusals` quando lo si tocca per altro |
| Anche i rifiuti di una riga sola scritti a mano (`TourChildren.Refusal`, `new Dictionary<…> { [campo] = [chiave] }`) dentro `Refusals` | non sono una copia di questo pezzo ma una riga; fuori da questa decisione |

## Da portare nel piano

- **§16 punto 6** (un solo motore di back-office; regola **«valida il server, il client mostra i `ProblemDetails` campo per
  campo»**): la risposta a un rifiuto la scrive sempre `CrudProblems`, che ha tre porte — un validatore, i due dizionari, e dal 27 set
  2026 **`Refusals`** (`Core/Data/Crud/`), il solo modo in cui un verbo raccoglie a mano i suoi rifiuti campo per campo, lingue
  mancanti comprese. Nessun modulo si scrive la sua classe dei rifiuti (nota `2026-09-27-i-rifiuti-di-un-form-nel-nucleo`).
- **`CLAUDE.md` §2**, tabella, riga **Validation**: un verbo che raccoglie i rifiuti a mano usa `Refusals` e risponde con
  `CrudProblems.Validation`.
- **`HANDOFF-M3.md`** (il master, dopo il merge): la prossima fase del collaboratore toglie `Training/Refusals.cs` e usa quello del
  nucleo, e lo scrive nel suo «Com'è andata» con il link al [commento di Carmine][d].
