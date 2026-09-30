# Il controllo all'avvio delle alternative si rinforza

**Data:** 30 settembre 2026
**Stato:** **decisa** — è la voce 4 della «coda del codice» del piano (Carmine, 28 set 2026, in chat; piano 1.22, §«La coda del
codice»), annunciata dal revisore sulla #146
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5869116757>). Qui non si decide niente di nuovo: si scrive
come è fatta. Versione **0.4.3**, PATCH (§5).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende un controllo che c'è già
(`PermissionCatalog.VerifyAlternatives`, nota `2026-09-26-le-righe-affidate-a-chi-scrive` §3.5-bis), nessun meccanismo nuovo.
Cambio del nucleo, nella sua PR.

## 1. Il problema

Un permesso **segnato** (`OnlyForAssignee`, nota `le-righe-affidate-a-chi-scrive`) viene chiesto in due posti, e i due devono
dire la stessa cosa: l'handler unico (all'endpoint) e il guardiano dell'interceptor (al salvataggio). Quattro dichiarazioni li
fanno rispondere in modo diverso sulla stessa riga — «sì» all'endpoint, 403 al salvataggio, o il contrario — senza che niente lo
dica prima:

1. **l'`Edit` del permesso non è quello dell'area dell'entità.** Su una riga non affidata a chi scrive l'handler ripiega
   sull'`Edit` del prefisso del permesso (`PermissionCatalog.EditOf`), il guardiano sull'`Edit` dell'area dell'entità
   (`[PermissionArea]`, altrimenti il nome del `DbSet`: `HubSaveChangesInterceptor.PermissionAreaOf`). Il caso della #146:
   un `Exam` senza `[PermissionArea("Training")]` avrebbe chiesto `Exams.Edit`, che nessuno ha;
2. **`OnlyForAssignee` sull'`Edit` di un'area.** Il ripiego del permesso sarebbe il permesso stesso: chi scrive tutta l'area
   scriverebbe solo le sue righe per l'handler e tutte per il guardiano;
3. **un'alternativa segnata su un'entità che non è `IOwnedByDepartment`.** Il guardiano chiede l'alternativa sui dipartimenti
   della riga, e una riga senza dipartimento non ne ha;
4. **un'entità con chi ha interesse (`IHasStakeholder`) il cui permesso segnato non è `DeniedToStakeholder`.** Il guardiano
   esclude l'interessato da ogni alternativa, l'handler solo dai permessi segnati così (nota §3.6).

## 2. Oggi nessuna dichiarazione le viola

Controllate a mano, una per una, prima di scrivere il controllo (e poi dal controllo stesso, a ogni avvio dei test
d'integrazione):

| Entità | Alternative | Segnate | Area → `EditOf` | Dipartimento | Interessato |
|---|---|---|---|---|---|
| `ContactMessage` (nucleo) | `Contacts.View` | no | — | — | — |
| `Pirep` (FlightOps) | `Tours.Validate` | no | — | (`ITourChild`) | — |
| `Exam` (Training) | `Training.ManageExams` | sì | `Training` → `Training.Edit` ✓ | ✓ | non ne ha |
| `Training` (Training) | `Approve`, `Assign`, `Conduct` | `Conduct` | `Training` → `Training.Edit` ✓ | ✓ | `Conduct` è `DeniedToStakeholder` ✓ |
| `SampleRecord` (modulo di prova) | `Decide`, `Record`, `Manage` | `Manage` | `Sample` → `Sample.Edit` ✓ | ✓ | `Manage` è `DeniedToStakeholder` ✓ |

Nessun `Edit` è segnato nei cataloghi. Non si tocca codice di modulo.

## 3. La correzione

`src/IvaoHub.Core/Auth/Permissions/PermissionCatalog.cs`:

- **la regola 2 sta nel costruttore del catalogo**, accanto a quella gemella («la `View` di un'area non è mai segnata»): è una
  proprietà del solo catalogo, che si vede senza nessuna entità, e il catalogo si costruisce all'avvio, prima delle migrazioni
  (`HubPipeline.InitializeAsync` lo chiede al contenitore). Così l'avvio si ferma anche se nessuna entità usa quel permesso;
- **le regole 1, 3 e 4 in `VerifyAlternatives`**, solo per le alternative segnate: un'alternativa non segnata non chiede niente
  all'entità (il PIREP non ha un dipartimento suo e resta com'è). Tutti gli errori di tutte le entità escono insieme, in un
  solo messaggio, come prima;
- **l'area** la dà chi chiama: `VerifyAlternatives(entities, areaOf)`. `HubPipeline` passa
  `HubSaveChangesInterceptor.PermissionAreaOf(contextType, entity)`, la stessa funzione del guardiano, così l'area controllata è
  quella che il guardiano chiederà. Senza `areaOf` (i test d'unità che la chiamano già) si controlla solo un'area dichiarata con
  `[PermissionArea]`: senza contesto il nome del `DbSet` non si conosce, e indovinarlo darebbe rifiuti falsi.

Test: `tests/IvaoHub.UnitTests/VerifyAlternativesTests.cs`, uno per regola più «ciò che le rispetta passa»; scritti prima, rossi
(5 su 6) prima della correzione. I test di A3b e A10 (`AssigneePermissionTests`, `TrainingExamRulesTests`) non sono cambiati e
restano verdi.

## 4. Le alternative scartate

- **Tutte e quattro in `VerifyAlternatives`**, come dice alla lettera la voce della coda: la regola 2 lì scatterebbe solo se
  un'entità dichiara quel permesso come alternativa, mentre il difetto è nel catalogo e c'è anche senza. Il costruttore la
  prende sempre, e prima.
- **Un test di architettura** al posto del controllo all'avvio: `ArchitectureTests.cs` è del maintainer, e il controllo
  all'avvio vale anche per un fork che aggiunge un modulo senza far girare i test (la stessa ragione della nota di A3b, §3.5-bis).
- **La regola 3 su ogni alternativa**, segnata o no: rifiuterebbe il PIREP, che è scritto dal validatore del tour per scope e
  non per dipartimento, e funziona.

## 5. La versione

**0.4.3, PATCH**: solo una correzione (un controllo all'avvio più severo), nessuna migrazione, nessuna pagina, nessun
comportamento nuovo per un'installazione le cui dichiarazioni sono giuste — cioè tutte quelle di oggi. Se il master alza la
prossima consegna a 0.5.0 (le fasi di M3 con le migrazioni del modulo), questa correzione ci entra dentro senza cambiare niente.

## Da portare nel piano

- §«La coda del codice», voce 4: ✅ fatta, con il link a questa nota e alla PR; la regola 2 sta nel costruttore del catalogo.
- §9.7 (o dove il piano descrive `OnlyForAssignee` e le alternative): le sei cose che l'avvio rifiuta, in una riga.
- Changelog: la riga della 0.4.3.
