# I dati di una persona nell'audit di una riga che non c'è più (E10n)

**Data:** 11 ottobre 2026 — fase E10n di M4, PR del nucleo (#251)
**Stato:** **decisa** (Carmine, 11 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [la risposta 2 sulla
#249][a249]): il buco dell'audit che il revisore ha trovato rileggendo E8b ([il punto 7][rv249]) lo chiude dalberone, **come fase del
nucleo di M4**: la sua PR, una nota nuova, **un meccanismo del nucleo per ogni modulo, non un caso degli eventi**, i test nei test della
cancellazione del nucleo, **unita prima che le prenotazioni si aprano su un'installazione vera**, accanto a E8b. **La forma nel codice è
di questa nota** (§3), come per ogni fase del nucleo (`10`, «Regole di tutte le fasi»). Chi la applica alle prenotazioni l'ha scelto
dalberone (§4).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la cancellazione dei dati di una persona (`PersonalDataErasure`,
`AuditRedaction`, nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona` §4), che già svuota l'audit delle righe che cancella e
scrive lo pseudonimo nel resto; nessun meccanismo nuovo accanto. È una PR del nucleo, prima del codice del modulo che la usa (`CLAUDE.md`
§0 regola 6).

[rv249]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113085816
[a249]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113461471

## 1. Che cosa serve, e perché nessun meccanismo lo copre

- **L'eraser degli eventi** (E8b, `EventsPersonalData`, la #249): una prenotazione di un evento ancora da finire va; una di un evento
  finito resta, senza `Callsign`, `OtherIcao`, `OtherTimeUtc`, `UnflownExcusedNote` e `FlownSessionId`. In modalità cancellazione
  l'interceptor scrive per ognuna una riga `erased` senza dati e `AuditRedaction.EmptyAsync` svuota **tutte** le copie che l'audit ne
  aveva.
- **Il buco** (il punto 7 del revisore): una prenotazione che il pilota ha ritirato, o che lo staff ha tolto, **prima** della
  cancellazione non è più una riga, quindi nessun eraser la trova. Le sue righe `created` e `deleted` in `hub_audit_log` tengono il
  nominativo, l'altro aeroporto e l'orario; `AuditRedaction.ReplaceAsync` cambia solo il VID in `BookerVid` con lo pseudonimo. Restano
  proprio i dati che l'eraser toglie dalle righe che restano.
- **Nessun pezzo lo copre**: `EmptyAsync` lavora sulle chiavi che l'interceptor ha visto cancellare o svuotare **durante** la
  cancellazione; `ReplaceAsync` riconosce una persona dal nome di una proprietà (`PersonColumns.IsVidName`), mai i suoi dati; l'eraser di
  un modulo legge le righe che ci sono. E il nucleo non sa quali colonne di un modulo sono dati di chi.
- **Non è solo degli eventi**: ogni modulo che ha righe di una persona tolte prima della cancellazione — un ritiro, una riga dello staff
  tolta — ha lo stesso buco. Per questo Carmine lo vuole nel nucleo.

## 2. Che cosa c'è, letto nel codice (`main` a `bea8c0e`)

1. **L'audit** (`HubSaveChangesInterceptor.CollectAudit`): una riga `[Audited]` aggiunta scrive `created` con tutte le colonne in
   `after_json`; tolta, `deleted` con tutte in `before_json`; cambiata, `updated` con le sole colonne cambiate (o `autosaved` con i soli
   nomi). Il JSON è un oggetto con i **nomi delle proprietà** come chiavi (`"Callsign"`, `"BookerVid"`); `entity` è la tabella,
   `entity_id` la chiave (`ReadKey`, i valori uniti da `:`). C'è un indice su `(entity, entity_id)`.
2. **La cancellazione** (`PersonalDataErasure.EraseAsync`): per ogni modulo, in una transazione per contesto, l'eraser; poi
   `PersonColumnRewrite` (lo pseudonimo nelle colonne di persona) ed `EmptyAsync` (le copie di ciò che è stato cancellato o svuotato).
   Poi il nucleo, allo stesso modo; poi `ReplaceAsync` su tutto l'audit e la riga `erasure`.
3. **I segni che il nucleo legge su un'entità**: `[Audited]`, `[PermissionArea]`, `[AlsoWrittenWith]` sulla classe; `[NotAudited]` su una
   proprietà (una colonna di contabilità, T19a). Le colonne di persona si riconoscono dal nome (`PersonColumns`), letto dal modello del contesto.

## 3. La forma nel codice

### 3.1 Un segno sulla colonna: `[ErasedWith(nameof(BookerVid))]`

`ErasedWithAttribute` (`src/IvaoHub.Core/Privacy/ErasedWithAttribute.cs`), su una proprietà: **questa colonna è un dato della persona che
la colonna nominata indica**. Sulla prenotazione sarà:

```csharp
[ErasedWith(nameof(BookerVid))] public string? Callsign { get; set; }
```

- **La persona è una colonna della stessa riga** che nomina una persona (`PersonColumns.IsVid`: un intero `Vid`, `…Vid`, `…By`). Così il
  dato è di chi la riga dice, non di chiunque la riga nomini: cancellare lo staff che ha scusato un volo non svuota il nominativo del
  pilota. `PersonColumns.ErasedWith(entity)` legge i segni dal modello, raggruppati per persona, e **rifiuta** un segno che nomina una
  colonna che non è una persona (`InvalidOperationException`): un segno sbagliato non svuoterebbe niente senza dire niente.
- **Sulla colonna, non nell'eraser**: il segno sta dove sta la colonna, come `[NotAudited]`, e il nucleo lo legge dal modello come le
  colonne di persona — una tabella nuova è coperta il giorno in cui nasce, senza un elenco da tenere a mano altrove.
- **Il limite, detto**: copre un dato che sta sulla riga della persona. Un dato su una riga figlia, la cui persona è nominata solo dalla
  riga sopra — le note di una sessione, il cui trainee lo dice il training — non è coperto. Oggi nessuna riga tolta prima della
  cancellazione è così (§6); se un giorno servisse, è un'altra nota.

### 3.2 Il passo della cancellazione: `AuditRedaction.EmptyGoneAsync`

In ogni contesto, **dopo l'eraser del modulo e prima dello pseudonimo** (la persona si trova ancora dal suo VID), per ogni tabella
`[Audited]` che il contesto riscrive (`PersonColumns.RewrittenBy`) e che ha dei segni:

1. **Le righe che nominavano la persona**: le righe d'audit della tabella il cui JSON contiene il VID (un `LIKE`, a pagine di 500, come
   `ReplaceAsync`), lette, e tenute quelle dove la colonna di persona del segno **vale proprio il VID** — le copie intere, `created` e
   `deleted`. Le loro chiavi.
2. **Meno quelle che ci sono ancora**: le righe della tabella che oggi nominano la persona in quella colonna (`CrudSource.BackOffice`,
   come `PersonColumnRewrite`), con la chiave scritta come la scrive l'audit (`HubSaveChangesInterceptor.ReadKey`, ora `internal`).
3. **Su ogni riga d'audit di quelle chiavi** — `created`, `updated`, `deleted`: un `updated` porta solo le colonne cambiate e non la
   persona, e si trova dalla chiave — le proprietà segnate diventano `null` nel JSON (`AuditRedaction.Empty`). Il resto resta: l'azione,
   chi l'ha fatta, quando, le altre colonne; la persona prende poi lo pseudonimo da `ReplaceAsync`, come oggi.

Nel flusso: `PersonalDataErasure.EraseModuleAsync`, una riga prima di `PersonColumnRewrite`, e lo stesso per il contesto del nucleo
(oggi nessuna entità del nucleo ha un segno, e il passo non fa query).

### 3.3 Che cosa non cambia

- **Una riga che c'è ancora resta del suo eraser**: svuotata per intero da `EmptyAsync` se l'eraser l'ha tolta o svuotata, così com'è se
  il modulo la tiene (`ErasureRequest.Keep`, un ban in vigore) o non la tocca. È la scelta fra le due che il prompt della fase offriva
  («per le righe che non esistono più, o per ogni riga della persona»): solo le righe che non ci sono, perché per una riga che c'è i dati
  sono ancora sulla riga, e svuotarli nell'audit non toglierebbe niente; ed è esattamente ciò che Carmine ha chiesto («le righe d'audit di
  una prenotazione ritirata o tolta prima della cancellazione»).
- **Le righe di un'altra persona**: non toccate, nemmeno se la persona cancellata le ha scritte.
- **Nessuna forma che un test del maintainer usa**: `IPersonalDataEraser`, `ErasureRequest`, `ErasureLine` e le righe dell'anteprima
  restano come sono; nessuna riga nuova nel risultato (come `EmptyAsync`, il passo non conta); nessuna migrazione, nessun endpoint,
  niente nel browser. `ErasureTests` non cambia. Senza segni, la cancellazione fa ciò che faceva.
- **Il rifacimento**: una cancellazione fallita a metà si rifà; il passo trova la persona dal VID finché `ReplaceAsync` non l'ha scritto,
  e una colonna già vuota resta vuota.

## 4. Chi lo applica

- **Le prenotazioni: E8b** (scelta di dalberone, 11 ottobre 2026, chiesta prima del codice): la #249 è aperta; dopo l'unione di questa,
  la sua sessione unisce `main` e mette il segno sulle cinque colonne che il suo eraser svuota — `Callsign`, `OtherIcao`, `OtherTimeUtc`,
  `UnflownExcusedNote`, `FlownSessionId`, tutte `[ErasedWith(nameof(BookerVid))]` —, con un suo test di una prenotazione ritirata prima
  della cancellazione. Non in questa PR: la regola 6 vuole il nucleo e il modulo in due PR.
- **Il training** (A12b, `TrainingPersonalData`): i suoi testi sono sul training (che resta o va con l'eraser) e sulle righe figlie
  (sessioni, foglio), il caso del limite di §3.1. Nessun segno in questa fase.
- **I tour** sono del maintainer: se un loro dato di una persona sta su una riga che si toglie prima della cancellazione, il segno è una
  riga sua.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Svuotare per intero, come `EmptyAsync`, ogni riga d'audit di una riga tolta che nominava la persona in una colonna qualsiasi | l'autore o lo staff di una riga non sono la persona di cui la riga parla: si perderebbe la storia di righe di altri; e il nucleo non distinguerebbe un dato da un riferimento |
| I segni dichiarati dall'eraser (un membro nuovo di `IPersonalDataEraser`) | un secondo elenco di colonne lontano dalle colonne; cambia l'interfaccia che i tour implementano; il nucleo legge già i segni dal modello |
| Svuotare le colonne segnate in ogni riga d'audit della persona, anche delle righe che ci sono | per una riga che c'è il dato è ancora sulla riga; cambierebbe l'audit delle righe che un modulo tiene apposta (un ban) |
| Sapere se una riga c'è dall'audit stesso (l'ultima azione `deleted`) | una riga tolta dal database senza passare dall'interceptor non lascerebbe `deleted`; il database dice com'è oggi |
| Un caso degli eventi nell'eraser degli eventi, che legge l'audit | contro la risposta di Carmine (un meccanismo del nucleo per ogni modulo); e l'audit è del nucleo, che solo lui cambia |

## 6. I test

- **`ErasedAuditTests`** (integrazione, nuova, 3; il nucleo e il modulo di prova sull'host vero, sulla MariaDB vera), con `SampleItem`
  che ora segna `Title` come dato del membro di cui parla (`[ErasedWith(nameof(StakeholderVid))]`, in `SampleItems.cs`; nessuna
  migrazione: il segno non è nel modello di EF). Persone: **761100**, nuovo (la persona cancellata, mai usata da un altro test, scelta di
  dalberone); **780096** e **780097** di `ErasureTests` (l'altro membro, solo nominato su una riga; il superadmin, seminato com'è lì).
  1. **una riga tolta prima della cancellazione perde il titolo nelle sue righe d'audit** — `created`, `updated` (i due lati), `deleted` —,
     con lo pseudonimo in `StakeholderVid`, le altre colonne e chi l'ha scritta; **la riga di un altro membro**, tolta allo stesso modo,
     tiene il titolo; **le righe che restano** sono del loro eraser come prima: quella che l'eraser toglie ha le copie vuote e la sua riga
     `erased`, quella che il modulo tiene ha il titolo nella sua storia;
  2. **una seconda cancellazione** della stessa persona non cambia niente delle righe d'audit;
  3. **ogni segno dei contesti dell'host** — del nucleo e di ogni modulo, il modulo di prova compreso — nomina una colonna di persona
     della sua entità: un segno sbagliato farebbe cadere ogni cancellazione, e qui si vede il giorno in cui si scrive.
- **`ErasedColumnsTests`** (unità, nuova, 4): `AuditRedaction.Empty` svuota le proprietà segnate e nient'altro, e rende lo stesso testo
  quando non ne ha (un `updated` di un'altra colonna, una colonna già vuota, i nomi di un `autosaved`, nessun dato);
  `PersonColumns.ErasedWith` legge i segni con la loro persona e rifiuta quello che nomina una colonna che non è una persona.
- **La prova al contrario**: tolte le due chiamate a `EmptyGoneAsync` da `PersonalDataErasure` (modifica locale, mai nel ramo), cadono i
  due test 1 e 2 (il titolo resta in tutte e tre le righe d'audit della riga tolta); rimesse, 3 su 3.

## Da portare nel piano

- **§9.7 / §16** (la cancellazione dei dati di una persona, la voce di `IPersonalDataEraser` e `PersonColumns`): **dall'11 ott 2026** (M4,
  E10n, decisa da Carmine sulla #249, risposta 2) una colonna che è un dato della persona nominata da un'altra colonna della stessa riga
  porta `[ErasedWith(nameof(…Vid))]`; alla cancellazione il nucleo svuota quelle colonne nelle righe d'audit delle righe che nominavano la
  persona e **non ci sono più** — tolte prima, da lei o dallo staff —, prima di scrivere lo pseudonimo (`AuditRedaction.EmptyGoneAsync`).
  Una riga che c'è resta del suo eraser. Copre un dato sulla riga della persona, non su una riga figlia. Un segno che non nomina una
  colonna di persona è rifiutato.
- **`CLAUDE.md` §2**, la riga «Erasing a person's data (GDPR)»: una frase in più, «a column of the person's data on a row that may be
  gone before the erasure is marked `[ErasedWith(nameof(…Vid))]`, and the core empties it in that row's audit».
- **`09-design-m4.md` §11.1** (la cancellazione negli eventi): le cinque colonne della prenotazione segnate da E8b dopo questa fase.
- **`10-piano-implementazione-m4.md`**: E10n, scritta in questa PR.
