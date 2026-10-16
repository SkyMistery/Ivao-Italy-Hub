# Le righe cancellate per conservazione lasciano anche l'audit (E10s)

**Data:** 16 ottobre 2026 — fase E10s di M4, PR del nucleo (#269)
**Stato:** **decisa** (Carmine, 16 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [la risposta 3 sulla
#267][a267], la (B)): «una piccola fase del nucleo prima, di dalberone, con la sua nota: un modulo che cancella righe per conservazione
chiede al nucleo di svuotare, nell'audit, le colonne segnate `[ErasedWith]` delle righe che cancella, accanto a
`AuditRedaction.EmptyGoneAsync`. E17 la usa ed è in coda dopo». **La forma nel codice è di questa nota** (§3), come per ogni fase del
nucleo (`10`, «Regole di tutte le fasi»).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende `AuditRedaction` (nota `2026-10-11-i-dati-di-una-persona-nell-audit`,
E10n), che già svuota le colonne segnate nell'audit delle righe tolte prima di una cancellazione; nessun meccanismo nuovo accanto. È una
PR del nucleo, prima del codice del modulo che la usa (`CLAUDE.md` §0 regola 6): nessun uso negli eventi qui, E17 la usa nella sua PR.

[a267]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267#issuecomment-6215521232

## 1. Che cosa serve, e perché nessun pezzo lo copre

- **La conservazione breve** (design M4 §4-bis.2): le iscrizioni a un evento in presenza e i posti delle attività si tengono
  `inPersonRetentionMonths` (3) dopo l'evento, poi si cancellano; le somme restano nelle statistiche. Le risposte si tengono poco
  **proprio perché** dicono chi viene di persona, e dove.
- **Il buco** (la domanda 3 della #267): `events-retention`, che nasce in E17, cancella quelle righe passando dall'interceptor, come
  ogni scrittura. L'audit tiene allora per sempre le loro copie in `hub_audit_log` — il `created` con le risposte, gli `updated`, e il
  `deleted` che la conservazione stessa scrive — e la regola si svuota.
- **Nessun pezzo lo copre**: `EmptyGoneAsync` (E10n) lavora **per una persona**, alla sua cancellazione, e trova le righe dal VID nel
  log; la conservazione cancella **per età**, righe di tante persone, e nessuna è cancellata. `EmptyAsync` svuota per intero le copie di
  ciò che l'interceptor ha visto cancellare **in modalità cancellazione**, che la conservazione non è (e svuotare tutto toglierebbe anche
  chi ha fatto cosa e quando, che non scade). Un modulo non legge né scrive l'audit: è del nucleo.

## 2. Che cosa c'è, letto nel codice (`main` a `e041bb04`)

1. **Il segno** `[ErasedWith(nameof(…Vid))]` (E10n) su una colonna di un'entità `[Audited]` con una chiave di una colonna sola:
   `PersonColumns.ErasedWith(entity)` lo legge dal modello, raggruppato per colonna di persona, e rifiuta un segno sbagliato; l'avvio li
   controlla tutti. Oggi lo portano `EventRegistration.AnswersJson`, cinque colonne di `EventBooking`, `EventAtcAvailability` e, nei
   test, `SampleItem.Title`.
2. **`EmptyGoneAsync`**: per ogni tabella segnata trova le chiavi delle righe d'audit che nominano la persona, toglie quelle che una
   riga della tabella ha ancora (`ExistingKeysAsync`, con la sola chiave, scritta come la scrive l'audit: `FormatKey`), legge le righe
   d'audit di ogni chiave rimasta in ordine e ne svuota le colonne segnate sui lati di quando la riga era della persona
   (`EmptyWhileHeld`, poi `Empty` sul JSON).
3. **L'interceptor**: una riga `[Audited]` tolta scrive `deleted` con tutte le colonne in `before_json`, nel secondo passo dello stesso
   `SaveChanges` (`SavedChangesAsync`), nella transazione del chiamante se c'è, altrimenti in una sua che chiude lì. Dopo il
   `SaveChangesAsync` la riga non c'è più e la sua copia `deleted` c'è.

## 3. La forma nel codice: `AuditRedaction.EmptyDeletedAsync<TEntity, TKey>`

```csharp
await AuditRedaction.EmptyDeletedAsync<EventRegistration, long>(database, deletedIds, cancellationToken);
await database.SaveChangesAsync(cancellationToken);
```

In `src/IvaoHub.Core/Privacy/AuditRedaction.cs`, accanto a `EmptyGoneAsync`. Il modulo la chiama **dopo il salvataggio che ha
cancellato le righe**, con le chiavi che ha cancellato; il nucleo:

1. **Rifiuta ciò che non potrebbe onorare** (`InvalidOperationException`, `ArgumentException` per le chiavi), invece di non svuotare
   niente senza dirlo: un'entità che il contesto non mappa; una **senza colonne segnate**; chiavi di un tipo che non è quello della
   chiave dell'entità; e **una cancellazione dell'entità ancora da salvare** nel contesto (le righe ci sarebbero ancora, e il passo 2 le
   lascerebbe tutte). Il segno sbagliato lo rifiuta già `PersonColumns.ErasedWith`.
2. **Toglie le chiavi che una riga ha ancora**, di chiunque sia: una riga che c'è non è stata cancellata, e la sua storia resta sua.
   È lo stesso passo di `EmptyGoneAsync`.
3. **Svuota le colonne segnate in ogni copia** delle chiavi rimaste — `created`, i due lati di un `updated`, `deleted` — **di tutti i
   segni, chiunque la riga indicasse**: la conservazione va per età, e il tempo per cui il dato si teneva è finito per tutto il dato.
   Resta il resto della copia: l'azione, chi l'ha fatta, quando, le altre colonne, la colonna di persona.
4. **Righe tracciate, salvate dal chiamante**, come gli altri passi di `AuditRedaction`. A pagine di 500 chiavi.

**Una sola strada per i due chiamanti**: il passo 2 e la lettura delle copie sono un pezzo privato, `EmptyGoneCopiesAsync`, che ora usa
anche `EmptyGoneAsync`; il JSON si svuota con lo stesso `Empty`. Cambia solo che cosa si svuota di ogni riga: i lati di quando era della
persona (`EmptyWhileHeld`) per la cancellazione, ogni lato per la conservazione. Nessuna seconda lettura del JSON.

### 3.1 Nella stessa transazione, o subito dopo: tutte e due vanno, la prima è quella giusta

- **Nella stessa transazione** (inizio, cancellazione, `SaveChangesAsync`, `EmptyDeletedAsync`, `SaveChangesAsync`, commit): sicuro. Lo
  stesso collegamento vede le sue cancellazioni (le righe non ci sono, passo 2) e la copia `deleted` che l'interceptor ha appena scritto
  (passo 3, svuotata anche lei). Un errore riporta indietro **insieme** la cancellazione e lo svuotamento: il giro dopo ritrova le righe
  e rifà tutto. È la forma che E17 usa, e il test la prova, anche con un rollback.
- **Subito dopo**, a cancellazione già confermata: funziona allo stesso modo, ma un errore fra le due lascia copie piene di righe che
  nessun giro cercherà più (la conservazione sceglie le righe che ci sono). Per questo la nota la sconsiglia.
- **Prima del salvataggio**: rifiutata (§3, punto 1).
- La chiamata è **idempotente**: un secondo giro sulle stesse chiavi non cambia niente (il test lo prova).

## 4. Chi la usa

- **E17** (`events-retention`): dopo il salvataggio che cancella le iscrizioni e i posti di un evento oltre `inPersonRetentionMonths`,
  nella stessa transazione, una chiamata per entità con le chiavi cancellate. `EventRegistration.AnswersJson` ha già il segno (E16).
  **Un'entità senza segni è rifiutata**: se una riga di un posto è un dato della persona solo come riga (chi, quale attività, quale
  turno), E17 sceglie quali colonne segnare, con la colonna di persona della stessa riga, e lo scrive nella sua nota.
- **E15b** (la conservazione di M4b, se verrà): le stesse regole, per le righe di M4b che porteranno un segno.
- **I tour** sono del maintainer: oggi **non ne hanno bisogno**. `TourRetentionJob` cancella righe `[Audited]` della forma del tour
  (regole, scali, rotazioni, vincoli, regole dei nominativi), che non sono dati di una persona; le righe di una persona che la
  conservazione dei tour tocca — `Pirep`, `PirepFlight`, `PirepError`, `CheckResult`, `PirepTrack`, `Enrolment` — non sono `[Audited]`, quindi non lasciano copie. Se un
  giorno un report diventasse `[Audited]`, la sua conservazione **svuota** colonne di una riga che resta, non la cancella: questa chiamata
  la lascerebbe (passo 2), e servirebbe un'altra nota.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una modalità «conservazione» dell'interceptor (`BeginRetention()`, come `BeginErasure`), che scrive il `deleted` senza le colonne segnate e svuota le copie di prima | cambia l'interceptor, da cui passa ogni scrittura, per un caso di due job; ogni salvataggio nello scope ne sarebbe toccato; `BeginErasure` è interno al nucleo e un modulo non dovrebbe aprirne uno |
| Il `deleted` scritto sempre senza le colonne segnate, per ogni cancellazione | toglierebbe i dati anche dalle cancellazioni dello staff, che non scadono; e lascerebbe il `created` e gli `updated` |
| Svuotare per intero le copie, come `EmptyAsync` | Carmine ha chiesto le colonne segnate; chi ha fatto cosa e quando non scade con il dato |
| Svuotare solo i lati di una persona (`EmptyWhileHeld`) | la conservazione non ha una persona: va per età, e il dato è scaduto per chiunque la riga indicasse |
| La firma senza tipi (`DbContext`, `Type`, `IEnumerable<object>`) | il compilatore non controllerebbe né l'entità né le chiavi; con `<TEntity, TKey>` le controlla, e il nucleo verifica la chiave del modello |
| Uno `UPDATE` in SQL con `JSON_SET` sulle righe d'audit | una seconda lettura del JSON accanto a `Empty`, legata a MariaDB, e fuori dal contesto; le righe sono poche (quelle di un evento) |
| Il modulo che svuota l'audit da sé | l'audit è del nucleo, che solo lui cambia (come in E10n, §5) |

## 6. I test

- **`RetentionAuditTests`** (integrazione, nuova, 3; il modulo di prova sull'host vero, sulla MariaDB vera; le righe scritte e
  cancellate come le scrive un job, senza nessuno connesso; `SampleItem.Title` segnato da E10n; persone **780096** e **780097** di
  `ErasureTests`, solo nominate sulle righe, nessuna cancellata né connessa):
  1. **le righe cancellate e passate alla chiamata perdono il titolo in ogni copia** — `created`, i due lati dell'`updated` (che passa la
     riga a un altro membro), `deleted` —, con la colonna di persona e le altre colonne; **una riga cancellata nello stesso salvataggio
     e non passata tiene il titolo** (la prova al contrario dentro il test); **una chiave passata la cui riga c'è ancora** tiene la sua
     storia; un secondo giro non cambia niente;
  2. **un rollback** riporta indietro la riga e le sue copie (dentro la transazione le copie sono già vuote, dopo il rollback il titolo
     c'è di nuovo e la riga anche);
  3. **i rifiuti**: la cancellazione non salvata, un'entità senza segni (`SampleRecord`), le chiavi di un altro tipo.
- **Nessun test esistente cambia**: `ErasedAuditTests` (6) passa com'era, e prova che `EmptyGoneAsync`, ora sul pezzo comune, fa ciò che
  faceva.
- **La prova al contrario**: con lo svuotamento tolto da `EmptyDeletedAsync` (un `return` prima, modifica locale, mai nel ramo)
  cadono i test 1 e 2 (`Assert.DoesNotContain() Failure: Sub-string found`: il titolo in tutte le copie), il 3 passa; rimesso, 3 su 3.

## Da portare nel piano

- **§9.7 / §16** (la cancellazione dei dati di una persona, la voce di `IPersonalDataEraser` e `PersonColumns`): **dal 16 ott 2026** (M4,
  E10s, decisa da Carmine sulla #267, risposta 3) un modulo che cancella righe **per conservazione** chiama, dopo il salvataggio della
  cancellazione e nella stessa transazione, `AuditRedaction.EmptyDeletedAsync<TEntity, TKey>(context, chiavi)`: il nucleo svuota le
  colonne segnate `[ErasedWith]` in **ogni** copia d'audit di quelle righe, se non ci sono più; un'entità senza segni, o una
  cancellazione non ancora salvata, è rifiutata.
- **`CLAUDE.md` §2**, la riga «Erasing a person's data (GDPR)»: una frase in più, «a module that deletes such rows for retention has
  the core empty the same columns in their audit (`AuditRedaction.EmptyDeletedAsync`)».
- **`09-design-m4.md` §4-bis.2** e la tabella della conservazione (§11): le risposte e i posti lasciano anche l'audit alla
  conservazione, con `EmptyDeletedAsync` (E17).
- **`10-piano-implementazione-m4.md`**: E10s, scritta in questa PR.
