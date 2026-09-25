# La cancellazione dei dati di una persona (T20b)

**Data:** 25 settembre 2026 — fase T20b di M2
**Stato:** **decisa** (Carmine, 25 settembre 2026, in chat: le quattro raccomandazioni di §5; il resto è scelta tecnica dichiarata qui
sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un meccanismo nuovo del nucleo. Il **che cosa** per il modulo dei tour è nel design
(`05-design-m2.md` §10.0: «PIREP e dati personali del modulo via, registro disciplinare anonimizzato»); il **dove** l'ha deciso Carmine il
25 settembre (nota `2026-09-25-la-conservazione-dei-tour` §2: «adesso, nel nucleo»: un'interfaccia che ogni modulo implementa, un'azione del
superadmin sull'utente, il nucleo che ripulisce fili, notifiche, preferenze, token e audit).

## 1. Che cosa serve

Una persona chiede che l'hub cancelli i suoi dati (GDPR, art. 17). Il piano (§9.7, «Privacy dei membri») promette che «una richiesta di
cancellazione ha un percorso noto», ma oggi **il percorso non c'è**: nessun codice cancella o anonimizza un utente, e chi provasse a mano
troverebbe tre ostacoli.

1. **L'audit ricopia ciò che si cancella.** `hub_users` è `[Audited]`: cancellarne la riga scrive la riga intera (nomi, email, discord) in
   `hub_audit_log.before_json`, che nessun codice svuota mai.
2. **L'interceptor non lascia riscrivere l'autore.** `Stamp` blocca `created_by` a ogni modifica (`HubSaveChangesInterceptor.cs:344`), e
   `created_by` è il mittente di un filo e il pilota di una segnalazione sulle leg. In più timbra `updated_by` con chi salva, quindi ogni riga
   anonimizzata direbbe «modificata dal superadmin oggi».
3. **Niente scritture in blocco**: `ArchitectureTests.NothingBypassesTheInterceptorWithABulkOperation` vieta `ExecuteDelete`/`ExecuteUpdate`,
   e giustamente: le proiezioni e l'audit passano dall'interceptor.

Nessun meccanismo esistente copre il caso: la conservazione di T20a è del modulo e guarda un tour, non una persona.

## 2. Il censimento, rifatto il 25 settembre

Rifatto in apertura di T20b sul codice di `main` dopo la #118 (quello di T20a §4 era giusto; qui con che cosa succede a ogni riga).
**A** = `[Audited]`.

| Dove | Che cosa nomina la persona | Proposta |
|---|---|---|
| `hub_users` (**A**) e in cascata `hub_user_staff_positions`, `hub_user_grants` (**A**), `hub_user_tokens`, `hub_personal_tokens` (**A**) | tutto | **cancellate** |
| `hub_notifications` | `vid`, `address`, `data_json` (oggetto e testo del filo, nota al pilota, motivo del ban) | **cancellate**, anche quelle già inviate |
| `hub_notification_preferences`, `hub_user_preferences` | `vid` | **cancellate** |
| `hub_award_assignments` (**A**), `cms_award_signals` | `vid`, `reason` | **cancellate** (un award è della persona) |
| `cms_contact_messages` (**A**) aperti dalla persona, con `cms_contact_replies` e `cms_contact_references` | mittente, oggetto, testo, partecipanti | **domanda 3** |
| `cms_contact_replies` scritte dalla persona in fili di altri, `participants_json` | `author_vid` | lo pseudonimo (§3) |
| `cms_contents`, `cms_content_versions`, `cms_media`, `cms_links`, categorie, menu, tipi del calendario, `hub_awards`, `hub_division_settings`, e ogni riga dei moduli con `created_by`/`updated_by`/`*_by`/`*_vid` | lavoro fatto **come staff** | lo pseudonimo (§3, **domanda 4**) |
| `hub_audit_log` | `vid` di chi agisce, `ip`, righe intere in `before_json`/`after_json` | §4 |
| `fo_pireps` **decisi** (accettati, rifiutati) | `vid`, note e testi, contestazione, ATC, snapshot | **il registro resta**: `vid` → pseudonimo; via note, `dispute_text`, ATC ed esenzioni; via voli (callsign, sessione, piani), tracce ed esiti dei controlli; restano stato, decisione, errori confermati, storia (senza la nota libera di una riapertura) e lo snapshot snellito come in T20a |
| `fo_pireps` in coda, in revisione, da modificare, ritirati | tutto | **cancellati** (non sono registro; un PIREP aperto non si decide senza pilota) |
| `fo_enrolments` | `vid` | **cancellate** (nessun conteggio le legge: verificato) |
| `fo_leg_issues` della persona | `created_by`, `body` | **cancellate** |
| `fo_bans` | `vid`, `reason` | **domanda 2** |

Due cose **non** sono dell'hub e la nota le dichiara: i **backup** del database (Plesk) tengono i dati finché ruotano; e **IVAO**, che
ridà nome, email e posizioni a ogni login: se la persona entra di nuovo, `UserSyncService` crea un utente nuovo, senza legami con lo
pseudonimo. È il comportamento giusto (un login nuovo è un consenso nuovo), e la pagina di conferma lo dice.

## 3. Il meccanismo proposto

**Nel nucleo, `Core/Privacy/`:**

- **`IPersonalDataEraser`**, registrato in DI da ogni modulo che tiene dati di persone (come `IContactReferenceResolver`): `PreviewAsync(vid)`
  risponde con righe da mostrare («12 PIREP decisi resteranno anonimi, 2 in coda cancellati, 1 ban in vigore…») e con eventuali
  **impedimenti**; `EraseAsync(ErasureContext)` cancella o svuota ciò che il modulo sa essere **sulla** persona (per i tour: la tabella qui
  sopra). Il modulo non tocca le colonne di VID: lo fa il nucleo, per tutti.
- **La sostituzione del VID la fa il nucleo, per convenzione sui nomi**: in ogni contesto (nucleo e moduli), ogni colonna intera che si chiama
  `vid`, `*_vid` o `*_by` e contiene quel VID diventa lo **pseudonimo**. Si legge dai metadati di EF, quindi Training e Events lo hanno gratis
  se seguono la convenzione, che è già quella di tutto il codice. Un test di architettura elenca le colonne trovate, così una colonna di
  persona con un nome diverso salta all'occhio nella revisione. Le liste di VID in JSON (`participants_json`) le tratta chi le possiede.
- **Lo pseudonimo** è un numero **negativo**, nuovo a ogni cancellazione, mai riusato, e **senza nessuna tabella che lo leghi al VID**
  (domanda 1). Il nucleo lo mostra com'è (un `-3` nell'audit, spiegato dalla riga `erasure`); dove un modulo mostra un VID (pagina del
  pilota, validazione), un numero negativo diventa «persona cancellata» senza link, e lo fa il modulo nelle sue pagine: oggi è l'unico
  posto che ne ha bisogno, e se Training ne avrà bisogno anche lui, l'helper passerà nel nucleo (scelta tecnica, presa scrivendo il
  codice).
- **Una modalità «cancellazione» dell'interceptor**, accesa solo dal servizio: non timbra `updated_by`/`updated_at`, lascia cambiare
  `created_by`, e per le righe auditate scrive una riga d'audit **senza JSON** (azione `erased`). Tutto il resto resta com'è: proiezioni
  (una riga cancellata toglie le sue voci di ricerca, calendario, segnalazioni), niente scritture in blocco.
- **`PersonalDataErasure`**, il servizio: anteprima (nucleo + ogni modulo), poi **una transazione per contesto** (un contesto per modulo, come
  tutto l'hub: se un modulo fallisce, la cancellazione si può rilanciare, perché ogni passo è idempotente). Alla fine **una riga d'audit**
  `erasure` con i conteggi per modulo, il superadmin che l'ha fatta e lo pseudonimo, **non il VID**.
- **Chi e dove**: **solo il superadmin**, da un pannello nella pagina dei permessi, sotto quello dei superadmin (lo stesso precedente:
  un'azione riservata al superadmin sta lì, senza una voce di menu né una rotta nuove): campo VID → anteprima → conferma con
  `ConfirmDialog`. Endpoint `GET /api/admin/erasure/{vid}` (anteprima) e `POST /api/admin/erasure/{vid}`. **Niente pulsante per il
  membro**: è irreversibile, e la richiesta arriva come una qualsiasi richiesta alla divisione. Un superadmin non si cancella: prima gli si
  toglie il ruolo.

## 4. L'audit

`hub_audit_log` oggi è «di sola lettura» per l'API (`AuditEndpoints`, test `TheAuditLogIsReadableAndNotWritable`), non per il codice. La
cancellazione è **l'unico** scrittore che lo modifica, e fa tre cose:

1. le righe il cui **oggetto** è della persona (la sua riga di `hub_users`, i suoi grant, token, award, fili, ban anonimizzati, e ogni riga
   che un modulo dichiara di aver cancellato o svuotato) perdono `before_json` e `after_json`: resta che **qualcosa** è successo, e quando;
2. le righe in cui la persona **ha agito** prendono lo pseudonimo in `vid` e perdono `ip`;
3. nel JSON delle righe rimaste, le proprietà `*Vid`/`*By` con quel VID prendono lo pseudonimo (le righe si trovano con un `LIKE` sul numero,
   poi un walker del JSON, come quello che estrae il testo per la ricerca).

L'API resta di sola lettura: nessuno può scrivere nell'audit da fuori.

## 5. Le domande

**Carmine ha preso le quattro raccomandazioni** (25 settembre 2026).

| # | Domanda | Risposta | Scartate |
|---|---|---|---|
| 1 | Con che cosa si sostituisce il VID nel registro? | **Un numero negativo per persona**, senza tabella di corrispondenza: i conteggi aggregati (piloti distinti di un tour, decisioni di un validatore) restano uguali, come chiede il piano (T20 «l'anonimizzazione lascia i conteggi aggregati uguali») | `0` per tutti (i conteggi dei distinti calano); `null` (le colonne diventano nullable, e ogni lettura deve saperlo) |
| 2 | Un **ban in vigore** (anche a vita)? | **Resta con il VID e il motivo** (interesse legittimo: anonimo non protegge niente); tutto il resto si cancella; quando il ban scade, rilanciare la cancellazione lo anonimizza (è idempotente, l'anteprima lo dice). Un ban scaduto si anonimizza subito | rifiutare la cancellazione finché c'è un ban (un ban a vita la blocca per sempre); anonimizzarlo comunque |
| 3 | I **fili aperti dalla persona** (contatti, contestazioni, chiarimenti)? | **Cancellati interi**, con le risposte dello staff (che citano la persona). L'esito di una contestazione resta sul PIREP. «I fili non si cancellano mai» era contro chi li nasconde; questa è un'altra azione, del superadmin e registrata | tenerli con oggetto e testi svuotati e il mittente pseudonimo |
| 4 | Il lavoro fatto **come staff** (decisioni sui PIREP di altri, contenuti, audit)? | **Pseudonimo anche lì**, una regola sola per ogni colonna: il lavoro resta, il nome no. I testi scritti dalla persona come staff **restano**, perché parlano di altri (una nota a un pilota, il motivo di un ban) | tenere il VID dove la persona ha agito come staff (responsabilità della divisione), pseudonimo solo dove i dati sono **sulla** persona |

## 6. Che cosa si tocca

- **PR del nucleo** (prima): `Core/Privacy/` (interfaccia, servizio, endpoint), la modalità dell'interceptor, il walker del JSON dell'audit, il
  pannello nella pagina dei permessi, le chiavi i18n, test d'integrazione (un utente con grant, notifiche, preferenze, un filo suo e una
  risposta nel filo di un altro, una segnalazione d'award, righe del modulo di prova su di lui e create da lui, righe d'audit; dopo: niente
  del VID in nessuna tabella del nucleo né nell'audit) e il test che elenca le colonne di persona (`ErasureTests`, dai modelli veri: un
  test di integrazione e non di architettura, perché i modelli di EF servono costruiti). **Nessuna migrazione.**
- **PR del modulo** (dopo): `FlightOpsPersonalData : IPersonalDataEraser`, i test (i conteggi del registro uguali prima e dopo, il ban in
  vigore resta, un PIREP aperto sparisce, la pagina del pilota mostra «persona cancellata»).

## 7. Trovato scrivendo il codice

- **Le notifiche su una persona, non solo a lei.** Quelle alla casella di un dipartimento su un filo che la persona ha aperto portano nel
  `data_json` il suo VID (come stringa), l'oggetto e il testo (`ContactThreads`), e così una segnalazione sulle leg. Vanno via come quelle
  indirizzate a lei: il nucleo le trova con un `LIKE` e le conferma con lo stesso walker dell'audit (`AuditRedaction.Mentions`).
- **Il modello di runtime di EF non sa quali tabelle un contesto esclude dalle migrazioni** (`IsTableExcludedFromMigrations` lancia a
  runtime). La regola è quindi sul tipo: un contesto di modulo riscrive i tipi del suo modulo, quello del nucleo i tipi del nucleo
  (`PersonColumns.RewrittenBy`), e le tabelle del nucleo che un modulo mappa si riscrivono una volta sola.

## Da portare nel piano

- `00-piano-di-progettazione.md` §9.7 («Privacy dei membri»): il percorso di una richiesta di cancellazione esiste, e com'è fatto; §16: il
  meccanismo, accanto agli altri; versione e changelog.
- `05-design-m2.md` §10.0: che cosa fa il modulo dei tour.
- `06-piano-implementazione-m2.md` parte C, T20b: le due PR.
- `CLAUDE.md` §2, tabella dei meccanismi: una riga «dati di una persona → `IPersonalDataEraser` + la convenzione `*_vid`/`*_by`».

Portati nella stessa PR del nucleo (piano 1.08), perché la scrive la sessione di Carmine.
