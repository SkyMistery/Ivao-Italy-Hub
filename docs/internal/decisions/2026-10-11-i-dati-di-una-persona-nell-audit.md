# I dati di una persona nell'audit di una riga che non c'è più (E10n)

**Data:** 11 ottobre 2026 — fase E10n di M4, PR del nucleo (#251)
**Stato:** **decisa** (Carmine, 11 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [la risposta 2 sulla
#249][a249]): il buco dell'audit che il revisore ha trovato rileggendo E8b ([il punto 7][rv249]) lo chiude dalberone, **come fase del
nucleo di M4**: la sua PR, una nota nuova, **un meccanismo del nucleo per ogni modulo, non un caso degli eventi**, i test nei test della
cancellazione del nucleo, **unita prima che le prenotazioni si aprano su un'installazione vera**, accanto a E8b. **La forma nel codice è
di questa nota** (§3), come per ogni fase del nucleo (`10`, «Regole di tutte le fasi»). Chi la applica alle prenotazioni l'ha scelto
dalberone (§4). **Dopo [la revisione][rv251]** (12 ottobre 2026) le due correzioni e i tre punti bassi sono fatti sulla stessa PR (§7).
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
  pilota. `PersonColumns.ErasedWith(entity)` legge i segni, raggruppati per persona, e **rifiuta** (`InvalidOperationException`) un
  segno che la cancellazione non potrebbe onorare — che svuoterebbe niente senza dire niente —: su una proprietà che il modello non
  mappa, su un'entità che non è `[Audited]` o la cui chiave non è una colonna sola, o che nomina una colonna che non è una persona.
  **L'avvio li controlla tutti** (`PersonColumns.VerifyErasedWith`, in `HubPipeline.InitializeAsync` accanto a `VerifyAlternatives` e
  `VerifyWithdrawals`, dopo la revisione, §7): un segno sbagliato ferma l'avvio, mai una cancellazione a metà dei moduli.
- **Sulla colonna, non nell'eraser**: il segno sta dove sta la colonna, come `[NotAudited]`, e il nucleo lo legge dal modello come le
  colonne di persona — una tabella nuova è coperta il giorno in cui nasce, senza un elenco da tenere a mano altrove.
- **Il limite, detto**: copre un dato che sta sulla riga della persona. Un dato su una riga figlia, la cui persona è nominata solo dalla
  riga sopra — le note di una sessione, il cui trainee lo dice il training — non è coperto. Oggi nessuna riga tolta prima della
  cancellazione è così (§6); se un giorno servisse, è un'altra nota.

### 3.2 Il passo della cancellazione: `AuditRedaction.EmptyGoneAsync`

In ogni contesto, **dopo l'eraser del modulo** (ciò che ha tolto non c'è più, e `EmptyAsync` lo svuota per intero subito dopo) **e
prima di `ReplaceAsync`**, che alla fine di tutto cambia il VID dell'audit nello pseudonimo — ed è il VID nell'audit che trova la
persona qui —, per ogni tabella che il contesto riscrive (`PersonColumns.RewrittenBy`) e che ha dei segni:

1. **Le righe che nominavano la persona**: le righe d'audit della tabella il cui JSON contiene il VID (un `LIKE`, a pagine di 500, come
   `ReplaceAsync`), lette, e tenute quelle dove la colonna di persona del segno **vale proprio il VID** — le copie intere, `created` e
   `deleted`, e un `updated` che cambia la persona. Le loro chiavi.
2. **Meno quelle che ci sono ancora, di chiunque siano** (dopo la revisione, §7): le chiavi che una riga della tabella ha ancora,
   lette con la sola chiave (`CrudSource.BackOffice`, come `PersonColumnRewrite`) e scritte come le scrive l'audit
   (`HubSaveChangesInterceptor.FormatKey`, lo stesso pezzo di `ReadKey`). Una riga che c'è è del suo eraser: anche una che è passata a
   un'altra persona, e una rimasta da un primo giro fallito a metà con lo pseudonimo già scritto.
3. **Di ogni chiave rimasta, le copie scritte mentre la riga era della persona** (`AuditRedaction.EmptyWhileHeld`, dopo la revisione):
   le righe d'audit della chiave in ordine, e per ogni lato — `before`, `after` — chi la riga indicava allora: un lato che ha la colonna
   di persona lo dice, uno che non ce l'ha (un `updated` di un'altra colonna) è della persona del lato prima, e dopo un `deleted` non è
   di nessuno; prima della prima copia che la nomina — una storia cominciata prima che la tabella fosse audita — è di chi quella copia
   dice com'era prima. Sui lati della persona le proprietà segnate diventano `null` nel JSON (`AuditRedaction.Empty`). Il resto resta:
   l'azione, chi l'ha fatta, quando, le altre colonne, e i lati di un'altra persona; la persona prende poi lo pseudonimo da
   `ReplaceAsync`, come oggi.

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
  una colonna già vuota resta vuota, e una riga rimasta dal primo giro, con lo pseudonimo già dentro, c'è ancora: resta del suo eraser
  (la prima forma la prendeva per tolta, §7).

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

- **`ErasedAuditTests`** (integrazione, nuova, 3, **6 dopo la revisione**; il nucleo e il modulo di prova sull'host vero, sulla MariaDB vera), con `SampleItem`
  che ora segna `Title` come dato del membro di cui parla (`[ErasedWith(nameof(StakeholderVid))]`, in `SampleItems.cs`; nessuna
  migrazione: il segno non è nel modello di EF). Persone: **761100**, nuovo (la persona cancellata, mai usata da un altro test, scelta di
  dalberone); **780096** e **780097** di `ErasureTests` (l'altro membro, solo nominato su una riga; il superadmin, seminato com'è lì).
  1. **una riga tolta prima della cancellazione perde il titolo nelle sue righe d'audit** — `created`, `updated` (i due lati), `deleted` —,
     con lo pseudonimo in `StakeholderVid`, le altre colonne e chi l'ha scritta; **la riga di un altro membro**, tolta allo stesso modo,
     tiene il titolo; **le righe che restano** sono del loro eraser come prima: quella che l'eraser toglie ha le copie vuote e la sua riga
     `erased`, quella che il modulo tiene ha il titolo nella sua storia;
  2. **una seconda cancellazione** della stessa persona non cambia niente delle righe d'audit;
  3. **ogni segno dei contesti dell'host** — del nucleo e di ogni modulo, il modulo di prova compreso — nomina una colonna di persona
     della sua entità: un segno sbagliato farebbe cadere ogni cancellazione, e qui si vede il giorno in cui si scrive;
  4. (dopo la revisione) **una riga passata di mano**: una che c'è ancora, ora di un altro membro, tiene tutte le sue copie; una tolta
     dopo essere passata perde il titolo solo nelle copie di quando era della persona — il `created` e il lato `before` del cambio —, e
     tiene quello dell'altro membro nel lato `after` e nel `deleted`;
  5. (dopo la revisione) **un secondo giro dopo un primo fallito a metà**: una riga rimasta con lo pseudonimo già scritto (lo stato del
     primo giro, scritto com'è lui: solo la colonna di persona, senza riga d'audit) tiene la sua storia;
  6. (dopo la revisione) **ciò che la persona ha fatto da staff**: una riga di un altro membro che ha creato e tolto prima della
     cancellazione, con il suo VID in `CreatedBy` e `UpdatedBy` (che prendono lo pseudonimo) **e come titolo**, tiene i suoi dati: solo
     la colonna di persona del segno dice di chi sono.
- **`ErasedColumnsTests`** (unità, nuova, 4, **10 dopo la revisione**): `AuditRedaction.Empty` svuota le proprietà segnate e nient'altro,
  e rende lo stesso testo quando non ne ha (un `updated` di un'altra colonna, una colonna già vuota, i nomi di un `autosaved`, nessun
  dato); `AuditRedaction.EmptyWhileHeld` su una riga passata di mano, su una storia cominciata prima dell'audit e su una riga di un altro
  con il VID in altre proprietà; `PersonColumns.ErasedWith` legge i segni con la loro persona e rifiuta i quattro segni sbagliati, e
  `VerifyErasedWith` con loro.
- **La prova al contrario**: tolte le due chiamate a `EmptyGoneAsync` da `PersonalDataErasure` (modifica locale, mai nel ramo), cadono i
  due test 1 e 2 (il titolo resta in tutte e tre le righe d'audit della riga tolta); rimesse, 3 su 3. **Dopo la revisione**: con il
  nucleo del primo push rimesso (`86b2008`, i test nuovi tenuti), cadono i test 4 e 5 (`Assert.Equal() Failure: Strings differ`: la riga
  passata di mano e quella rimasta dal primo giro perdono il titolo) e passano gli altri quattro; rimessi i file della correzione,
  toccati e ricompilati, 6 su 6. Il 6 passa anche sulla forma di prima: è la metà «non toccato» che mancava ai test, non un difetto.

## 7. Dopo la revisione (12 ottobre 2026)

[I rilievi del revisore][rv251] sulla #251: «la forma è giusta ed è ciò che il maintainer ha chiesto»; da correggere prima
dell'unione i punti 1 e 2, e tre bassi. Fatti tutti sulla stessa PR:

1. **«Tolta» voleva dire «nessuna riga nomina oggi la persona in quella colonna», non «nessuna riga ha quella chiave».** Due casi
   sbagliavano: **una riga passata di mano** (prima di A, ora di B: cancellare A svuotava ogni copia di una riga di B, le sue comprese) e
   **un secondo giro dopo un primo fallito a metà** (i moduli fanno il commit prima del nucleo, quindi una riga rimasta ha già lo
   pseudonimo mentre l'audit ha ancora il VID: il secondo giro la prendeva per tolta, contro §3.3). Il revisore offriva due vie, e
   **le ho prese tutte e due**, perché ciascuna chiude un caso che l'altra lascia: si tolgono le chiavi che **esistono del tutto** (§3.2
   punto 2: chiude il secondo giro, e la riga passata di mano che c'è ancora), e di una riga tolta si svuotano **solo le copie scritte
   mentre era della persona** (§3.2 punto 3: chiude la riga passata di mano e poi tolta, di cui la sola prima via svuoterebbe anche i
   lati dell'altra persona). Un test per caso (§6, 4 e 5), che con la forma di prima cade.
2. **La metà «non toccato» dei test**: la persona che fa da staff su una riga di un altro, tolta prima della cancellazione, con il suo
   VID in `CreatedBy`, `UpdatedBy` e come titolo (§6, 6), e in unità `EmptyWhileHeld` con il VID in altre proprietà. Il filtro della
   tabella resta in tutte e due le query dell'audit.
3. **(basso) Un segno sbagliato all'avvio**: `PersonColumns.VerifyErasedWith`, accanto a `VerifyAlternatives` e `VerifyWithdrawals`; e
   ora rifiutati anche il segno su un'entità non `[Audited]`, su una proprietà non mappata e su una chiave di più colonne (§3.1).
4. **(basso) La sola chiave**: `LiveKeysAsync` caricava le righe intere per leggerne la chiave; `ExistingKeysAsync` legge solo la chiave
   (§3.2 punto 2), e `ReadKey` è tornato privato, con `FormatKey` come pezzo comune.
5. **(basso) Il commento sull'ordine**, in `PersonalDataErasure` e in `EmptyGoneAsync`: l'audit tiene il VID fino a `ReplaceAsync`, alla
   fine di tutto; ciò che vuole il passo dopo l'eraser è che ciò che l'eraser toglie sia già tolto.

**Per il maintainer** i punti 6–8 (nessuna entità vera ha ancora il segno: la #249 viene dopo questa, con il commit di E8b che segna le
cinque colonne; la prenotazione ritirata tiene aereo, slot e orari sotto lo pseudonimo; le cancellazioni già fatte non si rifanno).

[rv251]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/251#issuecomment-6119839771

## Da portare nel piano

- **§9.7 / §16** (la cancellazione dei dati di una persona, la voce di `IPersonalDataEraser` e `PersonColumns`): **dall'11 ott 2026** (M4,
  E10n, decisa da Carmine sulla #249, risposta 2) una colonna che è un dato della persona nominata da un'altra colonna della stessa riga
  porta `[ErasedWith(nameof(…Vid))]`; alla cancellazione il nucleo svuota quelle colonne nelle righe d'audit delle righe che nominavano la
  persona e **non ci sono più** — nessuna riga con quella chiave: tolte prima, da lei o dallo staff —, e di quelle solo nelle copie
  scritte mentre la riga era sua, dopo l'eraser e prima che l'audit prenda lo pseudonimo (`AuditRedaction.EmptyGoneAsync`). Una riga che
  c'è resta del suo eraser, di chiunque sia ora. Copre un dato sulla riga della persona, non su una riga figlia. Un segno che la
  cancellazione non potrebbe onorare (non una persona, un'entità non `[Audited]`, una proprietà non mappata, una chiave di più colonne)
  ferma l'avvio (`PersonColumns.VerifyErasedWith`, dopo la revisione della #251).
- **`CLAUDE.md` §2**, la riga «Erasing a person's data (GDPR)»: una frase in più, «a column of the person's data on a row that may be
  gone before the erasure is marked `[ErasedWith(nameof(…Vid))]`, and the core empties it in that row's audit».
- **`09-design-m4.md` §11.1** (la cancellazione negli eventi): le cinque colonne della prenotazione segnate da E8b dopo questa fase.
- **`10-piano-implementazione-m4.md`**: E10n, scritta in questa PR.
