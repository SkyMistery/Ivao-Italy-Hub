# La persona cancellata nel nucleo, e le colonne del training in `ErasureTests` (A12a)

**Data:** 29 settembre 2026 — fase A12a di M3, PR del nucleo, in coda dopo #182
**Stato:** **Proposta**: due domande a Carmine (§5), ognuna con una raccomandazione, in un commento sulla PR. Il codice aspetta le
risposte.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**, e §0 regole 3 e 6. Tre cose, tutte su meccanismi che ci sono:

- si **porta nel nucleo** un pezzo che la nota di T20b aveva già deciso di portarci quando un secondo modulo ne avesse bisogno
  (`2026-09-25-la-cancellazione-dei-dati-di-una-persona` §3: «se Training ne avrà bisogno anche lui, l'helper passerà nel nucleo»;
  design `07-design-m3.md` §8 n.10). **Che** il training mostri «persona cancellata» l'ha deciso Carmine (§6.1, nota
  `2026-09-25-la-cancellazione-dei-dati-di-un-trainee`); la **forma** nel codice è di questa nota (domanda 1);
- si **estende la lista generata** di un tipo di colonna, come dice il suo file: «a new one is a line here, never a renderer in a
  screen» (`web/src/shared/list/columns.ts`);
- si tocca **un test del maintainer**, `ErasureTests`, come `08` dice da A0. Un test che non si è scritto non si cambia di propria
  iniziativa: come, lo decide lui (domanda 2).

## 1. Che cosa serve

- **Nelle pagine del training** (design §6.1): dove il modulo mostra un VID — il percorso del trainee, **le liste**, la pagina della
  sessione — un VID negativo diventa «persona cancellata», senza link. Lo fa A12b nelle pagine del modulo, insieme al suo eraser; il
  nucleo dà qui il pezzo che usa.
- **Anche negli eventi**: il piano di M4 aspetta lo stesso pezzo per la scheda «Prenotazioni» dello staff, **una lista generata**
  (`10-piano-implementazione-m4.md`, E0 «Trovato» n.14, E6b punto 4; `09-design-m4.md` §1.13 e §13).
- **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` con le colonne del training** (`08`, A0 «Trovato» n.1, e
  A12a). La cancellazione le riscrive già, perché la convenzione è sui nomi; ma l'elenco con cui la revisione confronta una tabella
  nuova (piano §16 punto 16) non le mostra.

## 2. Che cosa c'è, letto nel codice

1. **L'helper c'è solo nei tour**: `memberName(member, t?)` in `web/src/modules/flightops/api.ts`, con la parola
   `flightops:people.erased` («Deleted person», «Persona cancellata»). Un modulo non importa un altro (`CLAUDE.md` §2).
2. **Nelle liste non arriva**, come dice il suo commento: «A list computes its names inside the query, where there is no `t`: there
   the pseudonym stays a number». La coda dei PIREP mostrerebbe `-3`.
3. **Il training** ha `memberLabel(member)` (`web/src/modules/training/api.ts`), senza `t` e senza il caso dello pseudonimo — il suo
   commento rimanda ad A12a —, in 28 punti; le liste `/staff/training` e dei ban hanno colonne di testo (`traineeName`,
   `trainerName`, `givenByName`) calcolate nella query, come nei tour.
4. **Il membro di ogni modulo ha la stessa forma**: `MemberDto(int Vid, string? Name)` nei tour, `TrainingMemberDto(int Vid, string?
   Name)` nel training; in TypeScript tutti e due `{ vid: number; name: null | string }`.
5. **I tipi di colonna della lista sono un insieme chiuso** (`text`, `list`, `localized`, `number`, `boolean`, `date`, `department`,
   `badge`, `media`, `file`), disegnati dalla `Cell` di `DataList`, che ha `t`. Nessuno disegna una persona.
6. **I file di lingua sono uno**, letti dalla SPA e dal back end (`CLAUDE.md` §1): una parola del nucleo in
   `locales/{lang}/common.json` la può leggere anche una mail.
7. **`ErasureTests` legge due contesti scritti nel test**, `HubDbContext` e `FlightOpsDbContext`, e confronta con una lista di 76
   righe. La cancellazione invece scorre i contesti di **ogni modulo abilitato** (`PersonalDataErasure.EraseModuleAsync`,
   `module.DbContextTypes`). Le colonne del training che la regola di `PersonColumns.IsVid` trova, lette nelle entità e nello
   snapshot delle migrazioni, sono **21**: `trn_bans` (`created_by`, `lifted_by`, `updated_by`, `vid`); `trn_exams`
   (`candidate_vid`, `created_by`, `examiner_vid`, `updated_by`); `trn_sessions`, `trn_sheet_items` e `trn_slots` (`created_by`,
   `updated_by`); `trn_trainings` (`assigned_by`, `closed_by`, `created_by`, `decided_by`, `trainee_vid`, `trainer_vid`,
   `updated_by`). `trn_evaluations` non ne ha, nessuna è una chiave, e nessuna colonna JSON porta VID (gli avvisi di una data non ne
   hanno).
8. **Negli eventi**, nel piano di Carmine: la fase del nucleo **E8a** fa leggere al test anche `EventsDbContext` e dice «A12a di M3
   allarga lo stesso test al training: chi arriva secondo unisce `main` e tiene tutte e due le liste»; il design di M4 (§11.1) dice
   «`ErasureTests` non vede da solo i contesti dei moduli: va allargato nella fase che crea le tabelle». È la stessa scoperta di A0
   per il training (`07` §6.1 dava per scontato che il test le vedesse da solo).

## 3. La forma nel codice (domanda 1)

1. **`web/src/shared/ui/people.ts`**, nuovo, esportato da `shared/ui/index.ts` accanto agli altri aiuti delle schermate:
   - `NamedPerson`, `{ readonly vid: number; readonly name: string | null }`: la forma del membro di ogni modulo, che i due DTO hanno
     già. Nessun tipo C# nuovo: TypeScript la riconosce dalla forma.
   - `isErased(vid)`: `vid < 0`, perché lo pseudonimo è negativo (nota di T20b, §5 risposta 1). Una pagina non porta un link a una
     persona cancellata (il percorso di un trainee, la pagina di un pilota) e lo chiede qui.
   - `personName(person, t)`: «Deleted person» per uno pseudonimo; altrimenti `Nome (VID)`, o il VID da solo quando l'hub non ha il
     nome — la regola di `memberName` e di `memberLabel`, che è la stessa. `t` è obbligatorio: nei tour era facoltativo per le liste,
     e le liste hanno il punto 3.
2. **La parola**: `people.erased` in `locales/en/common.json` («Deleted person») e `locales/it/common.json` («Persona cancellata»),
   le parole della copia dei tour. Essendo del nucleo, la legge anche il back end: una mail che un giorno dovesse nominare una
   persona cancellata chiede questa chiave al catalogo. Qui non se ne scrive nessuna.
3. **La lista**: un tipo di colonna **`person`** in `columns.ts` — `col.person('trainee')`, su un campo `NamedPerson | null`,
   `sortable` come gli altri — che `DataList` disegna con `personName`. Una lista mostra «Deleted person» come una pagina, e nessun
   modulo calcola più i nomi nella query. Una riga in `docs/UI-GUIDELINES.md` («Screens are configuration, not markup»), accanto a
   `col.media` e `col.file`.
4. **I test** (Vitest): `personName` e `isErased` (un nome con il VID, il VID da solo con il nome `null` o vuoto, uno pseudonimo); la
   colonna `person` in una lista (un nome, una cella vuota, uno pseudonimo).
5. **Che cosa non cambia**:
   - **la copia dei tour** (`memberName`, `flightops:people.erased`) **non si tocca** (`CLAUDE.md` §0 regola 2): la sostituisce con
     questa una sessione di Carmine, e allora la coda dei PIREP può usare `col.person` e dire «Deleted person» dove oggi scrive il
     numero;
   - **`memberLabel` del training** resta fino ad A12b, che lo sostituisce con `personName` e `col.person` e toglie i link verso uno
     pseudonimo, insieme al suo eraser;
   - **le schermate del nucleo**: l'audit mostra lo pseudonimo come numero apposta («spiegato dalla riga `erasure`», nota di T20b
     §3); nessun'altra schermata del nucleo nomina il membro di un modulo;
   - **niente server, nessuna migrazione, nessun componente nuovo** nell'elenco chiuso: una funzione e un tipo di colonna non sono
     componenti (`UI-GUIDELINES.md` §3), e la galleria non cambia.

**Scartate:**

| Alternativa | Perché no |
|---|---|
| Solo la funzione, come nei tour | le liste restano con il numero; il training (§6.1) e la scheda «Prenotazioni» degli eventi (E6b) le chiedono |
| Un componente `PersonName`: il nome, e il link solo per un VID positivo | un componente nuovo nell'elenco chiuso, con la sua sezione della galleria, per scrivere una stringa; e il link è della pagina, che ha `isErased` |
| Il server scrive la parola nel DTO (`name: "Deleted person"`) | il server manda dati e chiavi, non frasi: la lingua è di chi legge, e una lista che cambia lingua terrebbe la parola vecchia |
| Ogni modulo calcola il nome nella query con l'istanza di i18n | la chiave della query non cambia con la lingua, e il pezzo sarebbe scritto in ogni modulo |
| Un tipo C# del membro nel nucleo, per i due moduli | tocca il modulo dei tour; in TypeScript i due DTO hanno già la stessa forma, e basta |

## 4. `ErasureTests` (domanda 2)

Il test è di Carmine (T20b). Tre modi:

- **(a) Com'è scritto in `08` e in E8a**: `TrainingDbContext` come terza voce di `contexts`, e le 21 righe `trn_` in fondo alla
  lista (97 righe). Solo righe aggiunte. Ma il test resta cieco a un modulo che non vi è nominato: la scoperta del §2 punto 8, fatta
  due volte, resta vera per il prossimo.
- **(b) Un test accanto, nei file del training** (`tests/IvaoHub.IntegrationTests/Training/`): la stessa proiezione su
  `TrainingDbContext`, con le 21 righe. Il test di Carmine non si tocca, e una fase del training che aggiungesse una colonna di
  persona toccherebbe il suo file e non uno del nucleo. Ma le liste diventano due (il piano §16 punto 16 parla dell'elenco con cui
  la revisione confronta), la proiezione è scritta due volte, e il test del nucleo resta cieco al training.
- **(c) Il test legge i contesti di ogni modulo**, come li trova la cancellazione (`ModuleRegistry.Enabled` e i loro
  `DbContextTypes`), tranne il modulo di prova (`SampleModule`, che non è dell'hub); la lista prende le 21 righe `trn_`. Oggi la lista
  è la stessa di (a); la differenza è domani: il contesto di un modulo si vede il giorno in cui nasce, e il test cade finché le sue
  colonne non sono nella lista, che è quello per cui esiste («whoever adds it sees it here»). Oltre alla lista cambia solo come si
  fanno i contesti:

  ```csharp
  var modules = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();
  DbContext[] contexts =
  [
      scope.ServiceProvider.GetRequiredService<HubDbContext>(),
      // Every module of the hub, the way the erasure goes through them; the test module is not one of the hub's.
      .. modules.Enabled
          .Where(module => module.Key != SampleModule.ModuleKey)
          .SelectMany(module => module.DbContextTypes)
          .Select(type => (DbContext)scope.ServiceProvider.GetRequiredService(type)),
  ];
  ```

  ⚠️ **Che cosa cambia per M4**: la prima fase degli eventi che crea una tabella con una colonna `…Vid` o `…By` (E2, lo scheletro, o
  quella dopo) scrive le sue righe nella lista, come dice il design di M4 (§11.1: «nella fase che crea le tabelle»); del punto 1 di
  E8a non resta niente, e il suo controllo («una colonna `…Vid` finta aggiunta e tolta lo fa cadere») resta buono. Chi arriva
  secondo, fra A12a e le fasi degli eventi, tiene tutte e due le liste, come E8a dice già.

Con (a) e con (c), una fase del training che aggiungerà una colonna di persona scriverà la sua riga nella lista di un test condiviso,
con una nota breve, come i conteggi dei blocchi (`2026-09-27-i-conteggi-dei-blocchi-del-training`); il piano di M4 ne fa già una
regola di tutte le fasi.

**Raccomandazione: (c).** È la promessa che il test fa nel suo commento («every column the erasure gives the pseudonym to»), e che due
design hanno dato per mantenuta; costa le righe qui sopra, e per il training oggi dà la stessa lista di (a).

## 5. Le domande

| # | Domanda | Raccomandazione | Le altre |
|---|---|---|---|
| 1 | La forma del pezzo nel nucleo (§3)? | **Sì, com'è scritta**: `personName` e `isErased` in `shared/ui/people.ts`, la parola `people.erased`, la colonna `col.person` della lista | solo la funzione; un componente; la parola dal server; il nome nella query di ogni modulo; un tipo C# comune |
| 2 | Come legge `ErasureTests` le colonne del training (§4)? | **(c)**: i contesti di ogni modulo dal registro, tranne quello di prova, e le 21 righe `trn_` nella lista | (a) `TrainingDbContext` scritto nel test; (b) un test accanto, nei file del training |

## 6. Che cosa si tocca, dopo le risposte

- **Nucleo**: `web/src/shared/ui/people.ts` e `people.test.ts` (nuovi), `web/src/shared/ui/index.ts`; `web/src/shared/list/columns.ts`,
  `DataList.tsx` e un test della colonna; `locales/en/common.json`, `locales/it/common.json`; `docs/UI-GUIDELINES.md`;
  `tests/IvaoHub.IntegrationTests/ErasureTests.cs`, come risponde Carmine alla domanda 2.
- **Documenti del modulo**: `07-design-m3.md` §6.1 (il test che «le vedrà da solo»), `08` («Com'è andata (A12a)»), `HANDOFF-M3.md`.
- **Non si toccano**: il modulo dei tour; il modulo del training (A12b).

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 punto 16: «persona cancellata» è del nucleo — `personName` e `isErased`
  (`web/src/shared/ui/people.ts`), la parola `people.erased`, la colonna `col.person` della lista generata; con la (c), il test delle
  colonne di persona legge i contesti di ogni modulo. Versione e changelog.
- `CLAUDE.md` §2, la riga «Erasing a person's data»: una pagina nomina una persona cancellata con `personName` del nucleo, una lista
  con `col.person`, mai con una copia sua.
- `05-design-m2.md` §10.0: la copia dei tour lascia il posto a quella del nucleo (una sessione di Carmine).
- `09-design-m4.md` §1.13, §11.1 e §13, e `10-piano-implementazione-m4.md` (E0 «Trovato» n.13 e n.14, E6b punto 4, E8a): l'helper c'è
  (A12a); con la (c), il test vede da solo il contesto degli eventi e la fase che crea le tabelle scrive le loro righe.
