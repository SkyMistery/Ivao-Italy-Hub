# IVAO Division Hub — Piano di progettazione

**Progetto:** nuovo sito/hub della divisione italiana IVAO (sostituisce `it.ivao.aero`), progettato per essere forkabile da altre divisioni.
**Versione documento:** 1.10 — 25 settembre 2026 (**T20b fatta**: il modulo dei tour cancella i dati di un pilota, il registro disciplinare resta senza di lui e conta lo stesso, un ban in vigore resta con il VID)
**Autore:** Carmine (IT-DIV), con supporto Claude
**Stato:** architettura, catalogo moduli (§9), contratti (§9.7), **meccanismi generici** (§16) e **modello unico dei contenuti** (§9.3) decisi; restano aperte solo le voci di §15 (per lo più informazioni da recuperare). **M0 è chiusa** (F0–F9, tag `v0.1.0-m0`): le fondamenta e la spina dorsale generica di §16 esistono e sono dimostrate end-to-end, come §16.15 chiedeva. **M1 ha design e piano di implementazione** (`03-design-m1.md` e `04-piano-implementazione-m1.md`, 5 set 2026): perimetro, set dei blocchi e convenzioni decisi, tredici fasi G0-G12 più la mezza G11a; **sono chiuse tutte**, e la chiusura è contata in `decisions/2026-09-07-m1-review.md`. Le sezioni marcate ⚠️ richiedono ancora una decisione

**Changelog 1.10** (25 set 2026, T20b, PR del modulo): **T20b è fatta**. `People/FlightOpsPersonalData` è l'eraser del modulo dei tour: i
PIREP decisi restano con lo pseudonimo e perdono ciò che toglie la conservazione (`TourRetentionJob.Empty`, condiviso) più callsign e
sessione dei voli, tracce, esiti dei controlli e la nota libera di una riapertura; gli altri PIREP, le iscrizioni e le segnalazioni sulle leg
vanno via; un ban in vigore resta intero (`ErasureRequest.Keep`), uno scaduto perde il motivo. Le pagine del modulo leggono un VID negativo
come «persona cancellata». Scelte tecniche e scostamenti in `06-piano-implementazione-m2.md` parte C, T20b. Nessuna domanda nuova.

**Changelog 1.09** (25 set 2026, T20b, tra la PR del nucleo e quella del modulo): **le righe che restano con il VID**. Nota
`decisions/2026-09-25-le-righe-che-restano-con-il-vid.md`, caso (b). La risposta 2 della nota di 1.08 (un ban in vigore resta con il VID)
non reggeva nel codice della #119: il nucleo scrive lo pseudonimo dopo l'eraser del modulo, in ogni colonna di ogni tabella. Ora l'eraser
passa le righe che tiene apposta (`ErasureRequest.Keep`) e il nucleo le salta; le loro righe d'audit prendono lo pseudonimo come le
altre. Trovato all'inizio della PR del modulo, prima di scriverlo. Toccati: §16 punto 16.

**Changelog 1.08** (25 set 2026, T20b, PR del nucleo): **la cancellazione dei dati di una persona**. Nota
`decisions/2026-09-25-la-cancellazione-dei-dati-di-una-persona.md`, caso (c). **Quattro risposte di Carmine**, tutte le
raccomandazioni: (1) nel registro il VID diventa **un numero negativo per persona**, senza tabella di corrispondenza, così i conteggi
aggregati restano uguali; (2) un **ban in vigore resta con il VID** (un ban anonimo non protegge), e quando scade si rilancia la
cancellazione; (3) i **fili aperti dalla persona si cancellano interi**, risposte comprese; (4) lo **pseudonimo anche sul lavoro fatto
come staff**, una regola sola per ogni colonna. **Fatto**: `Core/Privacy/` (`IPersonalDataEraser`, `PersonalDataErasure`,
`PersonColumns`, `AuditRedaction`, gli endpoint `/api/admin/erasure/{vid}`), la modalità cancellazione dell'interceptor, il pannello
nella pagina dei permessi, sotto quello dei superadmin. **Trovato**: le notifiche alla casella di un dipartimento su un filo della
persona portano il suo VID, l'oggetto e il testo nel `data_json`, e vanno via come quelle indirizzate a lei; e il modello di runtime di
EF non sa quali tabelle un contesto esclude dalle migrazioni, quindi un contesto di modulo riscrive i tipi del modulo e quello del
nucleo i tipi del nucleo. Toccati: §9.7 («Privacy dei membri»), §16 punto 16, `05-design-m2.md` §10.0, `06-piano-implementazione-m2.md`
parte C (T20b), `CLAUDE.md` §2.

**Changelog 1.07** (25 set 2026, T20a): **la conservazione dei tour**. Nota `decisions/2026-09-25-la-conservazione-dei-tour.md`.
In apertura di T20 il censimento dei dati personali ha trovato che **il nucleo non ha nessun modo di cancellare o anonimizzare un
utente** (fili delle contestazioni, notifiche, award e `hub_audit_log`, che copia intere le righe auditate e non si svuota mai).
**Tre risposte di Carmine**: (1) T20 si divide in **T20a** (conservazione), **T20b** (cancellazione dei dati di un pilota) e **T20c**
(rifiniture, giro completo, chiusura di M2); (2) la cancellazione si fa **adesso, nel nucleo**: T20b apre con una nota di caso (c)
per un meccanismo che ogni modulo implementa, e Training lo trova fatto; (3) tiene **25 mesi** il tour che **dura più di 12 mesi**
(`close_at > release_at + 12 mesi`), non quello che chiude nell'anno dopo. **Fatto in T20a**: `TourRetentionJob`, mensile, archivia un
tour pronto (`fo_tours.purged_at`) e toglie ciò che pesa; il tour esce dal pubblico, non si modifica più e le sue decisioni non si
riaprono. **Trovato**: il registro legge i nomi degli errori confermati dallo **snapshot delle regole**, che quindi si **snellisce**
(restano le regole con un errore confermato, senza testo né parametri) invece di svuotarsi; un tour **aspetta** finché ha un PIREP da
decidere o contestato, o un award in attesa; le etichette di `retentionMonths`/`retentionMonthsLong` dicevano «report» e «registro
disciplinare», corrette. Toccati: `05-design-m2.md` §10, `06-piano-implementazione-m2.md` parte C (T20a/b/c).

**Changelog 1.06** (24 set 2026, tra T19b e T20): **un secondo sviluppatore**. Nota
`decisions/2026-09-24-un-secondo-sviluppatore.md`, che chiude la proposta §3.5 della nota `2026-09-13-ordine-dei-moduli`.
**Quattro risposte di Carmine**: (1) Training (M3) lo scrive **`dalberone`**, già collaboratore con `write`; (2) **`CLAUDE.md`
diventa pubblico, in inglese, versionato**, le istruzioni personali vanno in `CLAUDE.local.md`; (3) **branch nel repository**
con ruleset, non un fork; (4) **T20 va avanti**, il collega parte dal design, che non tocca il codice. **La restrizione chiesta
da Carmine come assoluta**: su `main` unisce solo lui, dopo la revisione del suo Claude. **Tre ruoli** (`CLAUDE.md` §0:
maintainer, revisore, collaboratore); **ruleset** `main` (PR, `build-test` e `core-guard` obbligatori, «Restrict updates» con il
solo bypass admin) e `release tags` (`v*` solo admin: con `write` si poteva far partire `release.yml`); `.github/CODEOWNERS`;
**`core-guard`** (`pull_request_target`, script letto da `main`): su una PR non di Carmine ferma i file del maintainer e chiede una
nota nuova per ogni file del nucleo; `CONTRIBUTING.md` con le trappole che stavano solo nelle memorie locali; la sezione **«For
the reviewer»** del template. **Il piano e `HANDOFF.md` li scrive solo il revisore**, dopo il merge, dalla sezione «Da portare
nel piano» di ogni nota; il collaboratore scrive `07-design-m3.md`, `08-piano-implementazione-m3.md`, `HANDOFF-M3.md`. Test di
M3: VID `790001–790099`, slug `trn-test-`. **Da verificare**: che «Restrict updates» regga su un repository personale (una PR
di prova di `dalberone`). Toccate §13 e §16.E. **25 set** (nota §7): ruleset attivi; le sessioni Claude di Carmine hanno un `deny`
su merge e push su `main` (nessuna sessione unisce più niente); `.claude/settings.json` committato con lo stesso `deny`; il passo
`backbone-ran.sh` verifica che i test di architettura e del fork XX siano stati eseguiti (un `.csproj` può toglierli dalla build).

**Changelog 1.05** (24 set 2026, fase T19b di M2): **il contratto dell'agente del validatore**. Nota
`decisions/2026-09-24-il-contratto-dell-agente.md`, **due risposte di Carmine**: (1) `fo_check_results` **non** diventa `[Audited]`: un
esito dell'agente porta sulla sua riga `by_vid`, `token_id` e `agent_version`, e rimandarlo lo sostituisce; (2) l'agente **non** manda un
controllo che il server esegue (400 `agentCheckKeyServer`): **un controllo, un esecutore**. **Nel codice** (`Agent/` del modulo): l'audience
`flightops.agent` con `Tours.Validate`; `GET /api/flightops/agent/contract` aperto a tutti; la coda (`?pending=true`), il dettaglio (403
se il membro non decide quel PIREP: l'agente legge meno della pagina) e `POST …/checks` dietro la policy del token e un
`Hub-Agent-Contract` accettato; DTO della versione 1 suoi; la scrittura sostituisce per controllo e risuggerisce gli errori di tutti i
controlli falliti, senza toccare il PIREP (nessun 409 al validatore che ha la pagina aperta); la pagina di validazione dice di chi è
l'agente e quale versione; il form dei token mostra la parola del modulo. Migrazione additiva `AddAgentResultColumns`.
`docs/agent-contract.md` in inglese. **Corretto**: il PIREP non è `[Audited]` (è `IAuditable`, la sua storia è `fo_pirep_events`): la
frase «PIREP e tour compresi» del changelog 1.04 valeva per i tour, non per il PIREP. Toccate §9.7 e §16.10; design M2 §6.6 e §15.2 n.14.

**Changelog 1.04** (24 set 2026, fase T19a di M2): **i token personali**. Nota `decisions/2026-09-24-i-token-personali.md`,
**quattro risposte di Carmine**: (1) T19 si divide come T14 e T15 — **T19a il nucleo**, **T19b il modulo**; (2) il dettaglio del PIREP per
l'agente ha **DTO suoi, versionati**, non quelli della pagina di validazione; (3) l'agente scrive esiti solo su un PIREP **in coda o in
revisione** (409 altrimenti); (4) senza l'intestazione `Hub-Agent-Contract` la risposta è **400 con le versioni accettate**. Le ultime tre
valgono per T19b. **Nel codice** (`Core/Auth/PersonalTokens.cs`, `PersonalTokenAuthentication.cs`, `PersonalTokenEndpoints.cs`): il token
`hubpat_` di 256 bit, tenuto come SHA-256 e mostrato una volta; lo schema `HubToken`, mai di default, che ricostruisce l'identità a ogni
richiesta come il login e rifiuta con 401 e un `code` (sconosciuto, revocato, scaduto, ultimo login oltre 30 giorni); la policy
`PersonalTokenPolicy.For(audience)` nell'unico policy provider (solo lo schema del token, il claim dell'audience, il suo permesso); da 1 a 90
giorni, dieci token vivi a persona; `/me/tokens` con la lista e il form generati. **Quattro meccanismi estesi**: `IModule.TokenAudiences`
(il nucleo non ne dichiara), `user.tokenAudiences` nel bootstrap, `[NotAudited]` per una colonna di servizio (`last_used_at`), e
`hub_audit_log.token_id`. La guardia contro le richieste da altri siti lascia passare `Authorization: Bearer`. **Trovato**: nessun contesto
di modulo mappava `hub_audit_log`, quindi **nessuna riga `[Audited]` di un modulo è mai stata auditata** (PIREP e tour compresi), mentre il
commento di `ModuleDbContext` diceva il contrario; ora la mappa, fuori dalle migrazioni del modulo. Aggiornati §6 (i token), il design M2
§11 n.14 e la fase T19, divisa in T19a e T19b.

**Changelog 1.03** (24 set 2026, fase T18 di M2): **i controlli sulle tracce e le tarature**. Nota
`decisions/2026-09-24-i-controlli-sulle-tracce.md`, **quattro risposte di Carmine**: (1) `maxAltitude` ha **un limite per regola di volo**
(`maxFeetI|V|Y|Z`, V 19 500 ft e gli altri 66 000, i numeri del sistema di oggi); (2) un'esenzione `FreeSpeed` ammorbidisce `speed250` solo
se la posizione era `Online` o `Unverifiable`, **non** `NotOnline`; (3) **`thresholdToleranceMeters` resta 150** dopo la taratura sul
corpus (dodici decolli entro 100 m dalla testata IVAO, tre oltre 250: LFPM, LIPB, LIEO); (4) il tempo stimato parte da **5 % e 15 minuti**
(erano 20: sul tempo in volo di 14 voli sovrastimavano di 4 minuti in media). **Nel codice** (`Checks/TrackChecks.cs`,
`Checks/MetarReading.cs`): otto controlli, con il contesto di T17 esteso a posizioni degli aeroporti, piste, METAR tenuti ed esenzioni; il
decollo e l'atterraggio dell'invio (un touch and go lungo la strada non è l'atterraggio); la velocità indicata stimata senza vento con la
fascia dei 1 000 ft sotto FL100 che non fallisce; il sim rate come mediana su cinque minuti; la corsa di decollo dall'ultimo punto sotto i
30 kt riportato indietro, perché a 15 secondi per punto l'aereo non è quasi mai colto fermo in pista; il ceiling del METAR per `vmc`, letto
nel modulo. **Le fixture**: `airports-corpus.json` (`tools/record-ivao-fixtures.mjs --airports`, un modo nuovo) e `metars-corpus.json`, dati
pubblici. Sul corpus i controlli sulle tracce danno gli esiti della nota del 24 settembre §5 (880159 passa). **Trovato**: IVAO dà la
lunghezza delle piste in metri per alcuni aeroporti e in piedi per altri. Aggiornati il design §1.5, §1.11, §6.4 e la fase T18.

**Changelog 1.02** (24 set 2026, fase T17 di M2): **il motore dei controlli e i controlli sul piano**. Nota
`decisions/2026-09-24-il-motore-dei-controlli.md`, **quattro risposte di Carmine**: (1) oltre a W, **anche J1 sopra FL285**; (2) le
lettere della **casella 10b** per regola di volo, come la 10a; (3) **R senza `PBN/`** fa fallire `flightPlanForm`; (4) un **livello
scritto in un piano VFR** (`F085`) fa fallire `flightPlanForm` — la nota del 24 settembre §5 lo dava «tutto passa» ed è corretta. **Le
fixture**: i 15 PIREP della nota registrati dal tracker con VID, data e callsign letti da Chrome (`tools/record-ivao-fixtures.mjs --list`),
sotto il VID 780002. **Nel codice** (`Checks/` del modulo): `IFlightCheck` con `Evaluate` **puro e sincrono** (il design diceva
`EvaluateAsync`), il contesto raccolto una volta, otto controlli sul piano, `FlightChecks` che li esegue **subito dopo l'invio** e
`FlightCheckJob` **ogni dieci minuti** su chi non ha esiti recenti; `fo_check_results` (`ran_by` `Server`/`Agent`) e `fo_pireps.checks_ran_at`;
gli errori dei controlli falliti **suggeriti** in `fo_pirep_errors`, e la decisione tiene se il validatore li ha confermati; la sezione
dei controlli nella pagina di validazione e la parola dei controlli nella coda. Il parametro di `equipment` ha una forma nuova
(`lettersI|V|Y|Z`, `transponderI|V|Y|Z`, `highLevelLetters` con W e J1, `highLevelFl` 285); l'impostazione **`routeProcedurePrefixes`**
parte da `ED`, `LO`. **Una correzione nel nucleo**: `IvaoTrackerReader` legge quando è stata depositata una revisione da `updatedAt`, non
da `createdAt` (uguale per tutte le revisioni di un piano), quindi il piano al decollo non è più sempre l'ultima revisione. Sul corpus i
controlli sul piano danno gli esiti della nota. Aggiornati il design §1.11, §6.1, §6.2, §6.4 e la fase T17.

**Changelog 1.01** (24 set 2026, prima di T17): **i controlli dai PIREP veri**. Nota `decisions/2026-09-24-i-controlli-dai-pirep-veri.md`:
Carmine ha chiesto di guardare, da amministratore e in sola lettura, che cosa trovano oggi i controllori nel sistema dei tour in uso (45
PIREP da 12 tour del 2026, 363 commenti, i commenti predefiniti, il regolamento). **Le sue risposte**: (1) **`flightRules`** nuovo, lettere
ammesse nella regola; (2) **`planAtTakeoff`** nuovo, i controlli sul piano leggono il piano valido al decollo (GR9); (3)
**`flightPlanForm`** nuovo — REG/ solo con un callsign da volo di linea, RMK/, Z con COM/DAT/NAV, VFR senza DCT, SID e STAR nella rotta solo
nei paesi di **un'impostazione del modulo** (parte da `ED` e `LO`; non `division.json`, è un fatto dell'AIP); (4) **`equipment`** con le
**lettere per regola di volo**, e W solo con un livello pianificato sopra FL285; (5) **`alternate`** non passa anche se l'alternato è uguale
alla destinazione, e segnala nell'evidenza se è uguale alla partenza; (6) **`maxAltitude`** sulle tracce in T18; (7) **i livelli volati
contro i pianificati** sull'agente (T21), perché salite e discese sono pianificate sui fix. **Restano fuori** le manovre obbligatorie
della leg (touch and go, pista, VRP): oggi nessuno le valida in automatico, si valutano più avanti. **Già coperti** per costruzione: le
procedure nel form, i dati del report (il volo si sceglie dal tracker), il PIREP respinto rimandato uguale (il volo resta preso; T17
aggiunge il test). Aggiornati il design §6.4 e §6.6 e le fasi T17, T18, T21. Nessun codice, nessuna estensione del nucleo.

**Changelog 1.00** (24 set 2026, fase T16 di M2): **il meteo salvato**. Nota `decisions/2026-09-24-il-meteo-salvato.md`, **due risposte
di Carmine**: (1) il meteo di un volo con PIREP deciso **si cancella** come dice il design (un bollettino resta oltre il tempo solo finché
un PIREP di quel giorno su quell'aeroporto aspetta una decisione); (2) **`weatherRetentionDays` è la finestra di riporto più lunga** dei
tour aperti o in chiusura (il default della divisione se non ce n'è), **non un'impostazione**. **Nel codice**: `WeatherArchive` e
`fo_weather_reports` (indice unico su aeroporto, momento e tipo) nel modulo; `WeatherJob` ai minuti 5 e 35 sugli aeroporti delle leg dei tour
che prendono PIREP; all'invio e al reinvio la storia degli aeroporti del report **senza un METAR nella finestra del volo** (20 secondi al
massimo, mai un invio rifiutato); `WeatherRetentionJob` ogni giorno; `ReviewDto.weather` al posto di `weatherAvailable`, con la deviazione
`Weather` in evidenza. **Correzione** alla nota del 15 settembre: il file di cache dei TAF di NOAA non esiste, i TAF si chiedono a gruppi.
Il test di architettura sulle fonti del meteo guarda ora la cartella del nucleo e non una qualsiasi `Weather`. Nessuna estensione del
nucleo, nessun componente nuovo.

**Changelog 0.99** (24 set 2026, fase T15b di M2): **le pagine delle persone** — T15 è chiusa. Nota
`decisions/2026-09-24-le-pagine-delle-persone.md`, **due risposte di Carmine**, tutte e due come proposte: (1) alla pagina del pilota si
arriva da **una voce «Piloti»** con un campo VID, più i link dalla pagina di validazione e dai ban — nessuna lista degli iscritti;
(2) le statistiche dei validatori hanno **un selettore d'anno** (il corrente, il precedente a un clic, fino a cinque indietro) e non
i due anni sempre insieme. **Nel codice**: `MyTours` (`People/`), una risposta sola a «i tour di questo pilota» — iniziati e ancora
visibili con la misura di `PilotProgress` e la prossima leg, PIREP `ToModify`, fili dei tour `Answered`, riepilogo con i minuti volati —
letta dal blocco **`flightops.myTours`** e da **`GET /api/flightops/my-tours`**, che i riquadri di `/tours` (e del blocco `tourCards`)
chiedono quando qualcuno ha fatto login: **il riquadro resta uguale per tutti** (T10) e la barra ci si disegna sopra. Le pagine
`/staff/tours/validators`, `/staff/tours/pilots`, `/staff/tours/pilots/{vid}`, `/staff/tours/bans` (lista e form generati) e le tre voci
di menu, tutte con `Tours.ViewPilots`. Al server di T15a si aggiungono **i titoli dei tour abilitati** nelle statistiche (un validatore
con il solo grant non legge la lista dei tour) e **il dipartimento dei fili** nella pagina del pilota (il link va nei contatti di quel
dipartimento). Nessuna estensione del nucleo, nessun componente nuovo (la barra è il `Progress` di Atmosphere).

**Changelog 0.98** (23 set 2026, fase T15a di M2): **completamento, validatori, piloti, ban** — il server. Nota
`decisions/2026-09-23-completamento-validatori-piloti-ban.md`, **quattro risposte di Carmine**, tutte come proposte: (1) **T15 divisa** in
T15a (il server) e T15b (le pagine, il blocco `myTours` nelle due metà, l'avanzamento sui riquadri, il giro «completato → award
assegnato»); (2) un validatore si abilita **sul tour di primo livello** — il PIREP di un sottotour prende lo scope del contenitore
(`fo_pireps.scope_tour_id`) — **o su tutti i tour**; (3) «aggiungi validatore» scrive **anche `Tours.ViewPilots`** (risposta 17) se il
membro non ne ha uno suo, e «togli» l'ultimo tour lo toglie solo se l'aveva scritto lui; (4) il riepilogo di `myTours` mostra le **ore
volate** dal tracker sui PIREP accettati, non le ore stimate delle leg. **Nel codice**: `PilotProgress`, l'unica risposta a «dove sta
questo pilota in questo tour» (anche il `Container`, dalle iscrizioni dei sottotour), usata dalla pagina del pilota di T11b;
`TourCompletion` completa l'iscrizione — e il contenitore — **prima del salvataggio che accetta**, e `Enrolment` diventa `IProjectable`
e proietta l'`AwardSignalProjection` con l'award del tour nella stessa transazione; mai tolto. `/api/flightops/validators` (statistiche
per anno e per tour sulla decisione che un PIREP ha adesso, aggiungi, togli), `/api/flightops/pilots/{vid}` (errori per categoria e per
errore con il nome congelato, leg volate, contestazioni, fili, ban, tour con la misura), `/api/flightops/bans` (lista e form generati,
mai cancellati, la mail `flightops.banned` quando il ban si scrive). **Due estensioni piccole del nucleo** (§16.E caso b):
**`ModuleGrants`**, che scrive un grant a una persona per conto di un modulo con le regole della schermata dei permessi (catalogo, mai
globale, solo staff) — la schermata non scrive mai uno scope e `hub_user_grants` la protegge l'endpoint —, e **`CrudOptions.AfterSave`**,
ciò che segue un create o un update salvato (la mail del ban non parte per un ban non scritto). La domanda di T13b («un validatore con il
solo grant non entra in `/staff`») non si pone: un grant a una persona si dà solo a chi è staff. Migrazione `AddValidatorScope` (una
colonna riempita dai tour esistenti, un indice).

**Changelog 0.97** (23 set 2026, fase T14b di M2): **contestazioni, chiarimenti, segnalazioni** — il modulo dei tour usa i fili di
T14a. Nota `decisions/2026-09-23-contestazioni-chiarimenti-segnalazioni.md`, **quattro risposte di Carmine**, tutte come proposte:
(1) una contestazione la **decide solo chi ha `Tours.ReopenDecisions`** sul tour e **non ha deciso quel PIREP** — il validatore che ha
deciso partecipa al filo e risponde, non giudica; (2) il pilota sa l'esito da **una risposta del dipartimento nel filo**, che la
decisione chiede e scrive (`contact.threadReplied`), senza un tipo di notifica nuovo; (3) **una contestazione per PIREP**, la chiave
«una volta sola» della proiezione; (4) il chiarimento ha **una pagina propria**, `/tours/{slug}/ask`, con il form generato e i
riferimenti del tour. **Nel codice**: `fo_pireps` + `dispute_status` (`Open`, `Upheld`, `Dismissed`), `dispute_text`, `disputed_at`,
`dispute_decided_at`, `dispute_decided_by_vid` (`is_disputed` resta, letta da `dispute_status`); `Pirep` diventa `IProjectable` e apre
il filo nella sua transazione (oggetto ed etichetta con il titolo del tour, consegnati alla riga in una proprietà non mappata per il
salvataggio che apre); aperta, la leg non blocca più; **respinta, blocca con la tolleranza da `dispute_decided_at`**; **accolta, il PIREP
torna `Queued`** a chiunque; con una contestazione aperta **«riapri» non c'è** (trovato scrivendo il giro: lascerebbe la contestazione
aperta per sempre). `FlightOpsReferences` risolve `pirep:{id}` (il validatore partecipa), `leg:{id}`, `rule:{tourId}:{ruleId}`;
`PublicTourDto.department` porta la domanda al dipartimento del tour. **`fo_leg_issues`** con `flightops.legIssueReported` alla sola
casella, lista e form generati in `/staff/tours/issues`; il blocco **`flightops.openIssues`** (segnalazioni aperte, contestazioni e
chiarimenti senza risposta). Il banco guadagna una terza persona, `?as=assistant` (FOAC), per il giro «contestato e riaperto».
**Nessun meccanismo nuovo del nucleo.** Migrazione `AddDisputesAndLegIssues` (solo additiva).

**Changelog 0.96** (23 set 2026, fase T14a di M2): **i fili dei contatti nel nucleo** — un messaggio di contatto diventa una
conversazione: `cms_contact_replies` (solo in aggiunta), `cms_contact_references` (gli oggetti citati, con l'etichetta presa
all'apertura), `kind`, `participants_json` e la riga che ha aperto il filo su `cms_contact_messages` (migrazione `AddContactThreads`).
Nota `decisions/2026-09-23-i-fili-dei-contatti.md`, **tre risposte di Carmine**, tutte come proposte: (1) **T14 divisa** in T14a (il
nucleo) e T14b (contestazione, chiarimento dalle pagine dei tour, segnalazioni su una leg, `openIssues`); (2) un **partecipante** legge e
risponde da **`/me/contacts`**, che elenca i fili dove si è mittente o partecipante; (3) quando risponde un partecipante la mail va **al
mittente e agli altri partecipanti**, non alla casella del dipartimento. **Cinque estensioni di meccanismi** (§16.E caso b):
**`IHasParticipants`** nell'unico handler (concede il permesso che legge l'area, che per un filo è anche quello che risponde) e nella
rete dell'interceptor, gemella dell'interessato di T11a; **`[AlsoWrittenWith(Contacts.View)]`** su `ContactMessage`, perché chi legge
la coda risponde e la risposta sposta lo stato; **`ThreadOpeningProjection`** in `ProjectionSnapshot` («una volta sola», chiave
`source_module, source_id, kind` unica), con `ModuleDbContext` che mappa messaggi e riferimenti fuori dalle sue migrazioni;
**`IContactReferenceResolver`** per modulo; **`CrudOptions.Participating`**, la vista personale del motore delle liste (le righe a cui
il lettore partecipa, solo lettura, 404 sulle altre). Un endpoint del filo serve il back office e `/me/contacts`: il mittente legge
«il dipartimento» come autore di ogni risposta dell'altra parte (design M2 §3.5). I tipi di notifica sono `contact.threadOpened` e
`contact.threadReplied` (non `contacts.…`: il prefisso che c'era è `contact.`). **`MessageThread`** entra nell'elenco chiuso, ventitreesimo.
Toccate §8.3 e la parte C di `06-piano-implementazione-m2.md`.

**Changelog 0.95** (23 set 2026, fase T13b di M2): **le pagine della validazione** — `/staff/tours/review` (la coda, unica o per
tour, decisi o in attesa, l'ordine come preferenza del validatore) e `/staff/tours/review/{id}` (la mappa con la traccia volata, tutte
le revisioni del piano con quella al decollo, ciò che il pilota ha dichiarato, il profilo, la tabella degli errori con i conteggi, il
suggerimento chiesto al server a ogni spunta, la decisione, la riapertura, la storia), la voce di menu e il blocco
**`flightops.reviewQueue`**. Il «fatta quando» di T13 è provato dal giro e2e. Nota `decisions/2026-09-23-le-pagine-della-validazione.md`,
**due risposte di Carmine**: (1) **il giro con due persone e Mailpit** — il login del banco accetta `?as=pilot` (un membro senza
posizioni, con un indirizzo), Mailpit è un servizio della CI, e il giro aspetta la mail del rifiuto e controlla che non porti chi ha
deciso; (2) **il blocco `reviewQueue` è per tour** (quanti e da quando il più vecchio, sui tour che chi guarda può validare). **Tre
estensioni di meccanismi** (§16.E caso b): **`RouteMap` disegna una traccia** (`tracks`, in rosso sopra le leg); la prima preferenza
letta dal browser (`preferenceQuery`); il secondo membro del banco. La pagina riceve le revisioni del piano **già lette** dal client
del nucleo (`ReviewPlanDto`) e gli aeroporti con la posizione: il browser non legge il JSON di IVAO. ⚠️ Trovato: un validatore con il
solo grant, senza posizioni staff, non entra in `/staff` — da decidere con «aggiungi validatore» (T15).

**Changelog 0.94** (23 set 2026, fase T13a di M2): **la validazione dei PIREP sul server** — la coda (lista generica, ordinata per
data o per tour, l'ordine come preferenza del validatore), la presa con il lease, la pagina di validazione come API con la tabella
degli errori delle regole congelate e i loro conteggi, il suggerimento chiesto al server, la decisione con gli errori, le mail al
pilota **senza il nome del validatore**, la riapertura, il riepilogo giornaliero ai validatori e le **tracce salvate all'invio**.
Nota `decisions/2026-09-23-la-validazione.md`, **cinque risposte di Carmine**: (1) **T13 si divide** in T13a (il server) e T13b (le
pagine e il blocco `reviewQueue`); (2) **la traccia si salva all'invio**, compressa, in `fo_pirep_tracks` (~10 KB a volo); (3) **si
cancella 90 giorni dopo la decisione**, `trackRetentionDays`; (4) **riaprire una decisione altrui** è il permesso nuovo
**`Tours.ReopenDecisions`** (FOC e FOAC da `positionGrants`), chi ha deciso riapre la sua con `Tours.Validate`; (5) **l'anno di un
errore** conta i PIREP accettati e rifiutati del pilota su tutti i tour, per anno UTC del decollo. **Quattro estensioni di meccanismi**
(§16.E caso b): la rete dell'interceptor accetta una scrittura con il permesso che la riga dichiara (**`[AlsoWrittenWith]`**, con lo
scope della riga e mai per l'interessato); **`IModule.NotificationTypes`** con `NotificationTypeCatalog` (i tipi di notifica dei
moduli, le parole nel file del modulo); **`IPermissionHolders`** (chi tiene un permesso, con il calcolatore del login); la lista
generica che tiene l'ordine di default dentro una colonna ordinata. Migrazione additiva `AddValidation`.

**Changelog 0.93** (23 set 2026, fase T12 di M2): **gli ATC contattati e le esenzioni** — nel nucleo `IAtcActivitySource`
(«quali posizioni erano online in questo intervallo»), con due risposte: la vista `v_share_atc_sessions` di vIPI, accesa da
`division.json → atcData` con `ConnectionStrings:AtcData` nei segreti, o **nessuna** (`Unavailable`, il default e quello di chi
forka); nel modulo la proposta leggera sul server (aeroporti di partenza, arrivo e deviazione nelle loro finestre, FIR attraversati
un punto al minuto), la sezione «ATC contattati» del form, `GET …/reports/atc`. Nota `decisions/2026-09-23-gli-atc-contattati.md`,
**tre risposte di Carmine**: (1) il **perimetro delle esenzioni** — `FreeSpeed` → `speed250`, `LevelChange` → `semicircularLevels`,
`DirectRouting` e `Other` nessun controllo di M2, `Other` con la nota obbligatoria; (2) **tre stati** di un'esenzione (`Online`,
`NotOnline`, `Unverifiable`), mai un rifiuto all'invio; (3) gli **ATC proposti e tolti restano scritti** (`Removed`). **Un'estensione
di meccanismo** (§16.E caso b): `AddSharedViewContext`, il contesto di sola lettura su una vista di un altro sito, accanto agli altri
due metodi che costruiscono un contesto. Una colonna additiva, `fo_pireps.atc_archive_available`. La vista la crea una migrazione
**di vIPI** (una PR nel suo repository); l'utente MariaDB di sola lettura e il privilegio sulle viste restano **da verificare sul
server** (nota del 14 settembre §4), e fino ad allora `division.json` non ha `atcData`.

**Changelog 0.92** (23 set 2026, fase T11b di M2): **il form del PIREP e la pagina del pilota** — `/tours/{slug}/report`
(prima il volo fra le sessioni del tracker, con la deviazione e il secondo volo, poi SID, STAR, IAP e note), la correzione di un
report «da modificare» che ripropone i suoi voli, i colori del pilota sulla mappa e sulle leg, «Invia il report», «I tuoi report»
con «Ritira» e «Correggi». Nota `decisions/2026-09-23-il-form-del-pirep.md`, nessuna domanda: **un'estensione di meccanismo**
(§16.E caso b) — il manifest di un modulo ha una terza area, **`area: 'member'`**, sotto la guardia del login del nucleo
(`_member`, come `/contact`); le procedure obbligatorie le dice il server (il browser non legge il piano); il rifiuto si divide fra
la metà della pagina del volo e il form dei dettagli; il giro e2e **rivola un volo registrato** (una copia ridatata a ieri, sotto
il VID del banco), senza orologi finti nel prodotto. L'avanzamento sui riquadri resta di T15 (`myTours`).

**Changelog 0.91** (23 set 2026, fase T11a di M2): **il PIREP sul server** — le tre domande di ogni tipo di tour (`TourRules`:
quali leg si volano, qual è la prossima, quando è finito, con il rifiuto, la tolleranza e la contestazione che sblocca), i filtri e
le regole di sequenza di un `Open` e il suo obiettivo (`OpenRules`), i limiti giornalieri per giorno UTC del decollo, cinque
tabelle (`fo_pireps`, `fo_pirep_flights`, `fo_pirep_events`, `fo_enrolments`, `fo_bans`), sei verbi del pilota (le sessioni del
tracker, l'invio, il suo stato nel tour, la lettura, la correzione, il ritiro), lo snapshot delle regole e della leg al primo invio,
il job del ritiro automatico e l'`ITourReports` vero. Nota `decisions/2026-09-23-il-pirep.md`, quattro risposte di Carmine in
apertura: **(1) T11 si divide** in T11a (il server) e T11b (il form, i colori della mappa, la pagina del pilota); **(2) l'hub di un
tour `Hub` si sceglie volando** la prima leg, quindi `hub_order_json` non esiste; **(3) la partenza di `SequentialChosenStart`** è la
leg del primo PIREP **non ritirato**, quindi `start_leg_id` non esiste — l'iscrizione ha `vid`, `tour_id`, `started_at`,
`completed_at`; **(4) il form è una pagina sua**, `/tours/{slug}/report` (T11b). **Due estensioni di meccanismi** (§16.E caso b):
⚠️ **la rete di sicurezza dell'interceptor** lascia modificare una riga `ISubmittedByMembers` e `IHasStakeholder` all'interessato
che l'ha inviata, se resta sua e negli stessi dipartimenti — senza, un pilota non poteva ritirare né correggere il proprio PIREP
(la regola del «terzo della famiglia», changelog di M1, valeva per la sola creazione); T13 dovrà estenderla ancora per i
validatori abilitati, che hanno `Tours.Validate` e non `Tours.Edit`. E `IAircraftTypeDirectory.WakeCategoriesAsync`, per il filtro
`AircraftCategory`. Sezioni toccate: §16 (la frase su `ISubmittedByMembers`, sotto).

**Changelog 0.90** (22 set 2026, fase T10 di M2): **il pubblico dei tour e la mappa** — `/tours` (i riquadri dei tour aperti, in
chiusura e in arrivo), `/tours/{slug}` (briefing, date, aerei, regole in vigore con i parametri, errori pubblici, leg con distanza
e tempo stimato, pulsante SimBrief, i vincoli di un `Open`, i sottotour di un `Container`), il componente **`RouteMap`** —
ventiduesimo dell'elenco chiuso (§8.3) — e il blocco **`flightops.tourCards`**. Nota
`decisions/2026-09-22-il-pubblico-dei-tour.md`, tre risposte di Carmine in apertura. **(1) La mappa di base non porta i nomi dei
luoghi**: terra, acqua e confini dall'archivio, i codici degli aeroporti come marcatori HTML — niente glifi né sprite da ospitare,
**un file solo** per chi forka (correzione alla nota `2026-09-15-la-mappa`, che li prevedeva sotto `/tiles`). Il mondo fino allo
zoom 7 rimisurato oggi: **179,4 MB**, la misura del 15 settembre; ⚠️ le build di Protomaps durano una settimana, quindi
`tools/basemap.mjs` prende la data come argomento. **(2) Il blocco è sempre vivo** e ha due proprietà (quali stati, quante carte);
nessun avanzamento nei riquadri, che è di chi guarda e arriva in T15. **(3)** L'archivio si scarica con `pmtiles extract`, sta in
`tiles/` fuori dal repository e si serve da `/tiles/basemap.pmtiles` con `Range` ed `ETag` forte: in sviluppo risponde 206
(`PublicTourTests`), in produzione lo dirà lo staging (verifica 1 della nota resta aperta). `img-src` guadagna `blob:`
(`config/security.json`). **Due estensioni di meccanismi esistenti** (§16.E caso b, nessun meccanismo nuovo): `BlockRegistration`
guadagna `propertyLabels`, perché il form delle proprietà di un blocco legge le etichette dal namespace del nucleo e il nucleo non
sa che cosa sia un tour; e ⚠️ **`IModule.ReservedSegments`** — trovato scrivendo il codice: `/tours` è un indirizzo del sito, il
nucleo tiene l'elenco dei segmenti che nessuna pagina può prendere e non conosce quelli di un modulo, quindi una pagina chiamata
«tours» sarebbe stata salvata, pubblicata e irraggiungibile per sempre. `ModuleRegistry` li compone come già compone
`SpaFallbackExclusions`; `tiles` entra invece nell'elenco del nucleo. Sezioni toccate: §8.3 (il ventiduesimo componente), §14 (un
rischio: l'archivio non caricato).

**Changelog 0.89** (22 set 2026, fase T9 di M2): **regole ed errori** — le regole generali e quelle dei tour (`fo_rules`, con
l'emendamento di una generale), il catalogo degli errori della divisione (`fo_errors`, categoria, massimo annuale dei warning,
pubblico o no), i collegamenti (`fo_rule_errors`), le **regole effettive** di un tour e di un sottotour (un servizio solo), «copia le
regole da un altro tour», i template che copiano le regole, e il primo **blocco di un modulo**, `flightops.errorCatalog`, sempre vivo.
Nota `decisions/2026-09-22-regole-ed-errori.md`, tre risposte di Carmine in apertura. **(1) Un emendamento eredita**: salva solo i
parametri che cambia, gli altri li legge dalla regola generale a ogni lettura (il PIREP congela tutto al primo invio). **(2) La copia
aggiunge**: le regole proprie in vigore dell'altro tour, con parametri ed errori; quello che il tour ha già (stessa generale emendata,
stesso codice) resta, e l'esito dice che cosa è stato saltato. **(3) I valori di partenza dei controlli** senza un numero nel design:
5 NM, 2 + 2 minuti di parcheggio, 10 kt, 10 %, da tarare in T17. Scelte di forma nella nota: la tolleranza del decollo dalla testata
**resta un'impostazione** (risposta 15; la tabella del design §6.4 è corretta), si emenda solo una generale, il controllo di un
emendamento è quello della sua regola, gli errori si sommano, una generale emendata non si elimina, il blocco non ha proprietà. Il
catalogo dei controlli nasce qui, vuoto di logica (`CheckCatalog`, che T17 leggerà). **L'estensione n.9 del design è verificata e non
serve**: il generatore di form già disegna uno schema scelto a runtime (`KindPicker`, T7c) e una selezione multipla, e gli aggregati
della lista sono `ToListPage`. Nessun meccanismo nuovo; due verbi scritti a mano (regole effettive, copia), dichiarati come quelli del
tour. Nessuna sezione del piano toccata oltre a questa riga.

**Changelog 0.88** (22 set 2026, fase T8 di M2): **le leg di un tour si importano da un file** — XLSX, XLS, ODS o CSV letto nel
browser, le differenze calcolate dal server senza scrivere, «fondi» e «sostituisci» applicati in un solo salvataggio e solo come
l'anteprima li ha mostrati (un'impronta delle leg, 409 se sono cambiate). Nota `decisions/2026-09-22-l-import-delle-leg.md`, quattro
risposte di Carmine in apertura. **(1) La libreria è SheetJS 0.20.3** (Apache-2.0), caricata solo all'import, installata dal tarball
del CDN di SheetJS perché su npm c'è solo la 0.18.5 con due CVE in lettura; scartate read-excel-file (worker da `blob:`, contro la
CSP) e un lettore scritto da noi. **(2) La stessa leg è la stessa coppia partenza→arrivo**, abbinata nell'ordine se ripetuta;
**nessuna colonna di numeri**: l'ordine delle righe è l'ordine del tour, e in «fondi» una leg assente resta dopo quella che la
precedeva. **(3) I tour `Hub` non importano** (le rotazioni non si nominano da un file). **(4) Una leg ritirata che il file nomina
torna nel tour**, con il motivo dell'import (ADR-051). **Sul file vero del FOD** (la cartella dei tour 2027, letta foglio per
foglio): **(5)** le righe che non sono leg si rifiutano, il file si pulisce; **(6) una leg ha più callsign e più numeri di volo
suggeriti** (due liste JSON al posto di `real_callsign` e `flight_number`, che non si scrivono più e cadono in una release successiva;
design §1.4); **(7)** una cartella di più fogli si apre sul primo foglio con le leg, e se ne sceglie un altro. Nessun meccanismo nuovo: ogni riga passa per le regole di una leg scritta a
mano, e i due verbi stanno nell'eccezione dichiarata dell'editor delle leg (§16.6, design M2 §8.4). Nessuna sezione del piano toccata
oltre a questa riga.

**Changelog 0.87** (22 set 2026, fase T7c di M2): **il tour `Open` si compone** — l'obiettivo con i suoi parametri
(`open_goal_json`), i filtri per volo e le regole di sequenza (`fo_tour_constraints`, righe figlie del tour come hub e callsign),
i loro controlli di «pronto»; il «fatta quando» di T7c — un `Open` con un obiettivo, due filtri e una regola, pronto — è provato
dal giro e2e. Nota `decisions/2026-09-22-il-tour-open.md`, cinque risposte di Carmine in apertura. **(1) L'obiettivo ha una scheda
sua**, «Obiettivo e vincoli», e si salva con il tour; il pezzo «tipo, poi il form dei suoi parametri» è uno, fatto di due form
generati (il tipo è un form di un campo che si applica mentre lo si sceglie). **(2) Filtri e regole solo sui tour `Open`.**
**(3) Una riga per tipo**, tranne `MinFlightsAt` (una per aeroporto); `TouchesAirport` prende un elenco. **(4) Un `Open` con
vincoli non cambia tipo**; l'obiettivo si svuota fuori da `Open`. **(5) Gli elenchi si scrivono nel tour**, e un template li
porta. Estensioni (caso b): **`IAirportDirectory.KnownCountriesAsync`** e **`IFirLocator.KnownAsync`** — un parametro che nomina
un paese o un FIR si verifica sul server come un aeroporto, e il modulo non legge le tabelle `ref_` da sé. Corretto il design
§2.6.1: **`AircraftTypes` cade** (sono gli aerei consentiti del tour, §1.5); resta `AircraftCategory`. Nessuna sezione del piano
toccata oltre a questa riga.

**Changelog 0.86** (21 set 2026, fase T7b di M2): **i tour hanno la loro forma** — hub e rotazioni (`fo_hubs`, `fo_rotations`), le
leg che dicono la loro rotazione o il collegamento fra hub, i sottotour di un `Container`, i vincoli sul callsign
(`fo_callsign_rules`), e i controlli di «pronto» di ognuno; il «fatta quando» di T7 — un tour `Hub` composto da zero e pronto — è
provato dal giro e2e. Nota `decisions/2026-09-21-la-forma-dei-tour.md`, cinque risposte di Carmine in apertura. **(1) T7 si divide
ancora**: il tour `Open` (obiettivo, filtri, regole di sequenza) è **T7c**, in una chat nuova. **(2) Un sottotour ha date proprie, e
quelle che non ha sono del padre**, ognuna da sola, dentro il periodo del padre; **(3) ha uno slug proprio**; **(4) ricerca e
calendario li porta solo il `Container`**: un sottotour proietta solo i suoi file. **(5) Un vincolo sul callsign è sulla compagnia**
(tre lettere, il resto lo sceglie il pilota: il callsign reale di una leg è solo un suggerimento) o, **solo per vietare**, su un
callsign intero; `Prefix` e `Pattern` del design cadono. **(6) Fra i livelli vince l'`Allow` più vicino** (leg, tour, padre) **e i
`Deny` si sommano**, al posto dell'unione di design §1.6. Estensione (caso b): **`CrudOptions.BeforeAuthorize`** (§16.6), che cosa una
riga prende da un'altra prima che il suo permesso venga chiesto — il motore chiedeva il permesso di una riga figlia sul solo
dipartimento di base, e chi cura un tour con un secondo dipartimento sarebbe stato rifiutato creando un hub. Hub, rotazioni e vincoli
sono **liste e form generati**: l'eccezione di §16.6 resta la sola `LegGrid`. Toccate §16.6.

**Changelog 0.85** (18 set 2026, fase T7a di M2): **i tour hanno le leg** — `fo_legs` con le coordinate congelate e la GCD del
server, il tempo stimato calcolato a ogni lettura, eliminare e rinumerare senza report, ritirare e ripristinare con un motivo quando
un report ci punta, e l'editor a tabella `LegGrid`. Nota `decisions/2026-09-18-le-leg-dei-tour.md`, due risposte di Carmine in
apertura. **(1) T7 si divide** in **T7a** (le leg, gli aerei consentiti nel form, `required_nm`, i controlli di «pronto» dei tipi con
leg) e **T7b** (hub e rotazioni, sottotour, callsign, tour `Open`), in una chat nuova. **(2) Le righe figlie di un tour copiano la sua
maschera dei dipartimenti** a ogni scrittura, e la seguono quando cambia: l'unico handler le legge come il tour. Estensioni (caso b):
**`IAirportDirectory`** e `/api/reference/airports` nel nucleo, come `IAircraftTypeDirectory`; **`ConfirmDialog`** (§8.3) chiede al
server quando si apre (`onOpenChange`) e tiene spenta la conferma finché la risposta non c'è (`confirmDisabled`). ⚠️ L'eccezione
dichiarata al motore lista e form (§16.6) vale anche **lato server**: sei verbi scritti a mano per le leg, contati nella nota, e ogni
scrittura risponde con tutta la griglia rinumerata. `LegGrid` vive nel modulo e non è ancora nella galleria dei componenti: come un
modulo ci porta i suoi lo dice T20. Corretto nel design il tempo stimato a 2000 NM (5 h 00, non 4 h 40). Toccate §8.3, §16.6.

**Changelog 0.84** (16 set 2026, fase T6a di M2): **i tour esistono nel back office** — `fo_tours`, lo stato calcolato dalle date in
un solo posto (`TourState`), «pronto» con i problemi elencati campo per campo, nascondere ed eliminare, template, proiezioni. Nota
`decisions/2026-09-16-i-tour-nel-back-office.md`, tre risposte di Carmine in apertura. **(1) Un job del modulo riproietta i tour
rilasciati** (`TourReleaseJob`, ogni quarto d'ora): un tour pronto senza anteprima è dello staff fino al rilascio e di tutti dopo, e
nessuno lo salva in quel momento. ⚠️ È la **prima eccezione dichiarata** a «niente job di riconciliazione» di §16.4: non pubblica e non
scrive il tour, chiede solo all'interceptor di proiettarlo di nuovo (`ProjectionRefresh`, nel nucleo). **(2) T6 si divide** in **T6a**
(server, lista, impostazioni, azioni) e **T6b** (l'editor del corpo estratto dall'editor dei contenuti, montato come scheda
«briefing»). **(3) Gli aerei consentiti sono tipi più gruppi, senza la spunta «anche le varianti»** del design §1.5: in IVAO le varianti
sono livree e motori dello stesso tipo, e i tipi imparentati si mettono insieme con un gruppo. Tre estensioni di meccanismi (caso b):
`ProjectionContext` porta l'**orologio** dell'host (§9.7); `CrudOptions.DeletePolicy`, una policy che l'eliminazione chiede **in più**
della scrittura (§16.6); `ModuleDbContext` registra le **funzioni SQL** del nucleo (`LocalizedQuery`, `JsonQuery`), senza le quali la
ricerca su un campo tradotto di una lista di modulo rispondeva 500 — trovato dal test dei tour, c'era da T5. «Pronto» è
`PublishStatus.Published`, così la regola della bozza resta dell'interceptor. Toccate §9.7, §16.4, §16.6.

**Changelog 0.83** (16 set 2026, fase T5 di M2): **il primo modulo del build**, `IvaoHub.Modules.FlightOps` (chiave `flightops`), con
contesto, permessi `Tours.*`, profili e gruppi di aerei, impostazioni. Nota `decisions/2026-09-16-impostazioni-dei-moduli.md`, due
risposte di Carmine in apertura. **(1) I grant di `division.json → positionGrants` si ricordano uno per uno**: la riga
`positionGrants.seeded` tiene l'impronta di ogni grant applicato, così un grant portato da un modulo nuovo arriva anche a
un'installazione già avviata, e uno cancellato dalla schermata resta cancellato (prima era «una volta per installazione», e i grant
del FOD non sarebbero mai arrivati). **(2) Le impostazioni di un modulo sono un meccanismo del nucleo**: `IModule.Settings`
(`ModuleSettingsDescriptor`: tipo, valori di partenza, validatore, permesso), salvate in `hub_division_settings` alla chiave
`modules.{key}.settings` e servite a `/api/modules/{key}/settings` dietro il permesso sul dipartimento di base. **(3)** Il manifest
della SPA monta anche **rotte di staff** (`RouteDefinition.area`, `permission`, `validateSearch`), e il nucleo ha
`IAircraftTypeDirectory` con `/api/reference/aircraft-types` per il campo dei tipi. Toccate §9.7 (contratto `IModule`), §16.

**Changelog 0.82** (16 set 2026, fase T4b di M2): **gli award esistono**, e le preferenze dell'utente con loro. Nota
`decisions/2026-09-16-award-e-preferenze.md`, quattro risposte di Carmine in apertura. **(1) Il catalogo è del dipartimento**:
`hub_awards` è `IOwnedByDepartment` con i nuovi `Awards.View` e `Awards.Edit` (coordinator, assistant e advisor sul proprio
dipartimento), letto da ogni dipartimento; **assegnare resta `Awards.Assign`, globale**, e dà la coda e il registro. Chiarisce la
frase di §9.1 «per dipartimento/grant», che non diceva chi scrive il catalogo. **(2) La segnalazione propone l'award**:
`AwardSignalProjection` guadagna `AwardId` facoltativo e `cms_award_signals` la colonna `award_id`; una segnalazione in attesa
segue la riga, una gestita non si riscrive. **(3) Nessuna mail** al membro. **(4) Il membro non vede mai i suoi award nell'hub**:
li vede sul profilo IVAO, e l'hub tiene il registro di chi ha assegnato e perché. Tutto passa dal motore CRUD (§16.6), senza un
endpoint scritto a mano: assegnare da una riga della coda è creare un'assegnazione con `signalId`, che `BeforeSave` segna gestita
nello stesso salvataggio (indice unico su `signal_id`); scartare è un `PUT` dello stato; un award già ricevuto si ritira e non si
elimina. **Le preferenze** (`hub_user_preferences`, `/api/me/preferences/{key}`) hanno le chiavi dichiarate da
**`IModule.Preferences`**, composte come i permessi e chiamate `<modulo>.<nome>`. Toccate §9.1, §9.7, §16.4.

**Changelog 0.81** (16 set 2026, fase T4a di M2): **le righe di un modulo proiettano davvero**, e il buco trovato in T0 è chiuso.
`ModuleDbContext` mappa `cms_search_index`, `cms_calendar_entries`, `cms_award_signals` e `cms_media_uses` con la configurazione del
nucleo, **escluse dalle migrazioni del modulo**, e condivide le convenzioni del nucleo (`HubDbContext.ApplyConventions`); un test sul modulo
di prova l'ha visto fallire prima della correzione. Quattro decisioni prese nella fase, oltre a quanto la nota già diceva. **(1) T4 si
divide** in **T4a** (proiezioni dei moduli, più voci di calendario, file con scadenza) e **T4b** (award e preferenze), come la fase
prevedeva (Carmine, 16 set 2026). **(2) Un'entità che si proietta in un contesto senza le tabelle è un errore**, non più un salto
silenzioso: l'interceptor lancia. **(3) Una riga non pubblicata tiene i suoi usi dei file** e nient'altro (`ProjectionSnapshot.Unpublished`):
la regola «una bozza non proietta» restava nell'interceptor e avrebbe tolto il banner a un tour in preparazione, contro la nota §3.
**(4) Il motore della lista guadagna `CrudOptions.ToListPage`** (§16.6, caso b di `CLAUDE.md` §5: si estende il meccanismo): una pagina
di righe mappata in una volta, perché «sarà eliminato il …» accanto al file è un fatto degli usi dei moduli e non una colonna del file;
costa una query per pagina, mai una per riga. Minori: le voci di calendario di una riga si distinguono per `sequence` (indice unico
`source_module, source_id, sequence`); l'eliminazione a mano di un file che una riga di modulo usa ancora risponde
`errors.media.inUseByModule`, distinta da quella della pagina pubblicata che offre l'archivio. Toccate §9.7 (tabella `calendar_entries`),
§16.4, §16.6.

**Changelog 0.80** (16 set 2026, fase T1 di M2): **i confini dei FIR cambiano fonte**, un giorno dopo averla scelta:
`decisions/2026-09-16-i-confini-dei-fir.md`. Con la chiave vera si è misurato che **OpenAIP ha 108 airspace di tipo «FIR» in tutto il
mondo** — zero per Regno Unito e Spagna, uno per gli Stati Uniti, e fra gli altri anche ATS locali che FIR non sono — mentre la domanda
del nucleo («in quale FIR sta questo punto», §9.1) vale per rotte che vanno ovunque. La fonte diventa il **dataset di VATSpy**
(`Boundaries.geojson`, 1121 confini mondiali, CC BY-SA 4.0): **scaricato da un job e mai committato**, con attribuzione dove il dato
derivato si vede, solo i confini interi e non i settori, e `ref_firs` vuota — quindi proposta degli ATC ridotta agli aeroporti — se il
file non c'è. OpenAIP resta annotato come fonte possibile per le **altre** classi di spazio aereo, dove è ricco. La lezione, scritta
perché è la seconda volta in due giorni: **una fonte esterna si sceglie sui dati, non sulla sua documentazione**. Toccate §9.1, §9.7.

**Changelog 0.79** (16 set 2026, fase T0 di M2): **il modulo dei tour entra nel piano**. Il design `05-design-m2.md`, chiuso il 15
set 2026 dopo quattro giri di revisione con Carmine (PR #80), e le sei note di T0 portano qui le decisioni che il piano non aveva.
**(1) Il modulo** (`flightops`, sezione Tours, §9.2 riga 2): sette tipi di tour (in sequenza, libero, hub, in sequenza con partenza a
scelta, a distanza, `Open` componibile con obiettivo, filtri e regole di sequenza, contenitore con sottotour); lo **stato del tour si
calcola dalle date**, nessun job pubblica o chiude; template con sole impostazioni e regole; un tour con PIREP non si elimina, si
nasconde, e la chiusura non si anticipa sotto due finestre di report; una leg senza PIREP si elimina e le successive si rinumerano, con
PIREP si ritira; tempo stimato facoltativo, calcolato a ogni lettura; regole con **parametri** (le soglie dei controlli stanno nelle regole,
non nelle impostazioni) e regole congelate sul PIREP; limiti giornalieri, callsign, aereo e rating **bloccano l'invio**; validazione con
code unica e per tour, suggerimento dalla soglia, **nessuno valida i propri PIREP, superadmin compreso**; contestazione che **non blocca**
le leg successive; chiarimenti; ban; **un award per tour**, segnalato e mai assegnato da solo; controlli automatici che **suggeriscono
dalla prima stagione**; il registro disciplinare non si cancella mai (tour e leg restano archiviati, tracce e piani vanno via dopo 13 o 25
mesi). **Mai punti né classifiche** (Carmine, 15 set 2026): tolte da §9.2 e §13. **Le Virtual Airlines sono rimandate**, e **lo storico dei
tour non si importa** (§15.2d chiusa: il sistema entra in uso nel 2027). **(2) Scope per risorsa e interessato** (nota
`2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`, §6.3, §16.2): un grant può valere su una riga sola (`resource_scope`), e una risorsa
nega all'interessato i permessi che il catalogo segna, anche al superadmin — l'unica eccezione al «bypassa ogni policy». **(3) I contatti
hanno le risposte** (nota `2026-09-15-contatti-con-risposte`, §9.1): fili con riferimenti a oggetti dei moduli e partecipanti in più; si
risponde dall'hub, niente mail in ingresso; la contestazione si apre con una proiezione nella transazione del PIREP. **(4) La mappa**
(nota `2026-09-15-la-mappa`, §8.3): MapLibre 6 e una mappa di base del mondo **fino allo zoom 7 (179 MB, misurati)** ospitata dall'hub;
`img-src` guadagna `blob:`; componente `RouteMap`. **(5) Due fonti esterne** (nota `2026-09-15-meteo-e-confini-dei-fir`, §9.1, §9.7):
il meteo da NOAA con ripiego su IVAO e VATSIM, e i confini dei FIR da OpenAIP (CC BY-NC 4.0, attribuzione; pagina legale da leggere a
mano). **(6) Token personali e contratto dell'agente del validatore** (nota `2026-09-15-token-personali-e-agente-del-validatore`, §6.3,
§9.7, §16.10): un token vale solo per la sua `audience` e ricostruisce i permessi di adesso; il contratto si versiona in
un'intestazione, non nell'indirizzo; l'app Python la adatta Claude dopo T19 (Carmine, 15 set 2026); la licenza di Navigraph è
verificata solo in parte, e prima di distribuire l'app si scrive a Navigraph. **(7) File della media library con scadenza** (nota
`2026-09-15-file-con-scadenza`, §9.1, §16.4): le righe dei moduli dichiarano gli usi con `IProjectable`, e un job del nucleo elimina i file
con tutti gli usi scaduti; le immagini le collega al tour chi modifica il tour. **(8) ⚠️ Trovato leggendo il codice**: `IProjectable`
**non proiettava le righe di un modulo** (il contesto del modulo non ha le tabelle delle proiezioni e l'interceptor saltava in silenzio);
si corregge in T4 mappando quelle tabelle nei contesti dei moduli, escluse dalle loro migrazioni (§9.7, §16.4). **(9) L'editor delle leg a
tabella** è un'**eccezione dichiarata** al motore lista e form (§16.6), con il componente `LegGrid`; il terzo componente nuovo di M2 è
`MessageThread`. **(10) Le fasi** T1–T21 in `06-piano-implementazione-m2.md` parte C. Toccate §6.3, §8.3, §9.1, §9.2, §9.7, §10, §13,
§14, §15, §16.

**Changelog 0.78** (14 set 2026): **vIPI è l'estensione ATC dell'hub e i due condividono i dati senza
copiarli**: `decisions/2026-09-14-dati-condivisi-con-vipi.md`, decisa con Carmine. **(1) Due database, non
uno**: un database unico esporrebbe i dati di un'app a una falla dell'altra e legherebbe i rilasci di due
app con versioni diverse di EF. **(2) Un padrone per ogni dato**, che è l'unico a scriverlo: l'hub per
persone, permessi, contenuti e moduli; vIPI per aeroporti curati, settori, SOP e archivio delle sessioni
ATC. **(3) Letture attraverso viste `v_share_`** con un utente MariaDB dedicato di sola lettura; mai
scritture incrociate; l'API HTTP resta il ripiego. **(4) Integrazione opzionale del nucleo**, accesa da
`division.json`: nessun modulo nomina vIPI e l'hub funziona senza. **(5)** Il primo consumatore è il
controllo della copertura ATC dei tour, che legge l'archivio delle sessioni ATC di vIPI. Riapre in parte
`2026-09-13-staccarsi-da-vipi.md` (l'hub legge dati di vIPI); M5 resta sospeso. Toccate §2.5, §9.7,
§15 punto 2.

**Changelog 0.77** (13 set 2026, notte): **l'ordine dei moduli cambia**, dopo il confronto di Carmine
con lo staff di IVAO: `decisions/2026-09-13-ordine-dei-moduli.md`. **(1) Tours, Training, Eventi**:
M2 è il modulo dei tour (`flightops`), M3 Training, M4 Eventi; `05-design-m2.md` diventa il design dei
tour; il deploy su staging resta in M2; `ivao-booking` si spegne con M4. Sostituisce l'ordine del 1° set
2026. **(2) Nessun altro modulo per ora**: `specialops` resta segnaposto senza milestone, i
dipartimenti restano come sono. **(3) Coordinator e assistant del dipartimento di base hanno tutte le
funzioni del modulo** (FOD, TD, ED), con i grant a una posizione da `division.json → positionGrants`
come già deciso; che cosa fanno gli advisor lo decide il design di ogni modulo. **(4) La collaborazione
resta, ma chi collabora non cancella**: cancellano coordinator e assistant del dipartimento di base; si
precisa nel design degli eventi. **(5) Due agenti su due PC**: una proposta nella nota §3.5, da decidere
prima che parta il secondo modulo. Toccate §9.2, §13.

**Changelog 0.76** (13 set 2026, notte): **le due dashboard personali**, la nota che il piano 0.59
metteva all'apertura di M2: `decisions/2026-09-13-le-dashboard-a-tutto-schermo.md`, decisa con Carmine.
**(1) Un meccanismo solo**: `/me` e `/staff` sono righe `Dashboard` di blocchi Data, come le dashboard
dei dipartimenti; i blocchi rispondono per chi guarda; **il registro dei widget sparisce** (§9.7: i
moduli registrano blocchi Data e basta). Le compone il web team e valgono per tutti: nessuna scelta per
persona. **(2) Una dashboard occupa tutto lo schermo**, con una barra compatta al posto di titolo,
descrizione e breadcrumb, e vale per tutte, dipartimenti compresi. **(3) Una griglia a tessere libere**:
la larghezza di un blocco (`span`, sei misure su 12 colonne) sta nell'envelope accanto a `column`;
l'editor sposta le tessere e le ridimensiona **con una maniglia e con un selettore**, tutti e due subito.
**(4) Tessere alte uguali per riga**, con un massimo e il contenuto che scorre. **(5) `/staff`** mostra ciò
che aspetta me (pagine da approvare, contatti, documenti da rivedere), le mie bozze, il calendario interno,
i miei dipartimenti e poi i riquadri dei moduli; **`/me`** parte con il solo saluto. Le fasi stanno nella
parte B di `06-piano-implementazione-m2.md`, prima del modulo Events. Toccate §8.1, §8.2, §9.7, §13.

**Changelog 0.75** (13 set 2026, notte): **H2**, la forma in codice di «a cura di» multiplo che la
nota `moduli-non-subordinati-ai-dipartimenti` §3.3 lasciava «da confermare nel design di M2»: l'insieme
dei dipartimenti di una riga di modulo è una **maschera di bit** in una colonna, con il bit di ogni
dipartimento scritto a mano e mai derivato dall'ordine dell'enum; **`IOwnedByDepartment` resta
l'unica interfaccia** (due membri con un default) e l'unico handler chiede «uno dei dipartimenti della
riga»; i **contesti dei moduli derivano da `ModuleDbContext`**, che applica lo stesso filtro globale di
`HubDbContext` (prima i moduli non ne avevano); **`division.json → modules`** passa da acceso/spento a
un oggetto per modulo con `enabled` e `baseDepartment`, e il dipartimento di base lo rimettono sulla
riga il motore CRUD e l'interceptor. Dettagli in `06-piano-implementazione-m2.md`.

**Changelog 0.74** (13 set 2026, sera): **M2 si apre con i prerequisiti** della nota
`moduli-non-subordinati-ai-dipartimenti` (§13), in tre fasi H1–H3 scritte nella parte A di
`06-piano-implementazione-m2.md`; la parte B (il modulo Events) segue la nota sulle due dashboard e
`05-design-m2.md`. **Deciso con Carmine**: il soggetto «posizione» di un grant (§6.3) si indica con
**dipartimento + uno o più livelli** (coordinator, assistant, advisor, membro), non con il codice della
posizione — regge quando IVAO rinumera le posizioni, e chi lascia il ruolo perde il permesso da solo.
Il seed è `division.json → positionGrants`, applicato una volta.

**Changelog 0.73** (13 set 2026, sera): **le decisioni prese scrivendo G19 e G20**, dove il testo
della 0.72 e il codice si sono scostati; i dettagli sono nelle sezioni «Fatta» di
`04-piano-implementazione-m1.md`. **(1) Approvazione (G19)**: nessuna versione candidata separata — una
pagina `Ready` è la **candidata di sé stessa**, perché il server rifiuta ogni scrittura finché aspetta;
la coda è un filtro della lista con una voce «Da approvare» nella barra laterale, e il blocco Data col
conteggio aspetta M2. **(2) Raccolte (G20)**: la raccolta è la vecchia categoria con un **elenco** sul
contenuto (`collections_json`, la colonna `category` resta fino a un contract); **tutte le raccolte sono
lette da tutti i dipartimenti** (`ISharedForReading`, come media e link), perché una pagina TD sceglie
le guide AOD per nome; nei corpi dei blocchi la proprietà **resta `category`**, così nessun corpo
salvato o pubblicato va riscritto, e le schermate la chiamano raccolta. **(3) L'indice derivato**
`cms_content_references` si scrive alla pubblicazione; le pagine pubblicate **prima** di G20 non ci
sono finché qualcuno non le ripubblica (il sito non è online, e la cancellazione di un file continua a
guardare anche le versioni pubblicate). **(4) Un file sostituito** (deciso con Carmine): l'indirizzo è
`/media/{id}/{impronta}/{nome}`, `immutable` per un anno; la risposta pubblica di una pagina porta
l'impronta di ogni file che mostra, così dopo la sostituzione la pagina mostra subito il file nuovo
senza ripubblicarla; l'indirizzo senza impronta, o con un'impronta vecchia, resta valido e si rivalida
(ETag), quindi niente di già stampato si rompe e niente resta vecchio.

**Changelog 0.72** (13 set 2026): **staccarsi da vIPI e centralizzare i contenuti**, due richieste
che Carmine ha portato dopo essersi confrontato con lo staff di IVAO, decise con lui domanda per
domanda. **(1) vIPI** (`decisions/2026-09-13-staccarsi-da-vipi.md`, che sospende la nota del 7
settembre): **M5 esce dalla roadmap** senza data; il sito ha **un link ad `atc.it.ivao.aero`**, che è
una voce di menu; l'hub **non consuma nessuna API** di vIPI; **cade il confine documentale** di §9.4
e un documento dell'hub è di qualsiasi natura. Il modulo **`atc` esce dai moduli obbligatori**, che
diventano tre, e rinascerà **opzionale** quando l'ATC avrà logica sua. **La G14 tiene ciò che è
generico** — archiviato e sostituito, «in vigore dal», data di revisione con l'avviso, piè di pagina
con la stampa, i blocchi Frequency Table e Coordination — e **perde ciò che è ATC**: il tipo SOP/LoA,
le due posizioni, ICAO, FIR e l'AIRAC. La pila #59–#64 si mergia com'è e la rimozione è una fase dopo
il merge, expand/contract. **(2) I contenuti centralizzati**
(`decisions/2026-09-13-contenuti-centralizzati.md`): **una schermata per tipo di oggetto, non per
dipartimento** — `/staff/content` per pagine, news, documenti e template, `/staff/links`,
`/staff/media` — con dipartimento e tipo come filtri, e le voci del menu del dipartimento che ci
portano già filtrate; chi vede tutto sono WD, HQ e **chi riceve un grant su ogni dipartimento**. **Le
pagine si approvano**: un terzo stato `Ready` che fotografa una **versione candidata**, un permesso
nuovo **`Content.Approve`** tenuto da Director e Web e dai grant — **non** dal coordinator del
dipartimento —, chi lo ha pubblica direttamente, e quali `kind` lo richiedono sta in `division.json`.
**News, documenti e template si pubblicano dal dipartimento**. **`/news` e `/documents` restano
indici generali** e le pagine **tirano** i contenuti per **raccolta**, un vocabolario per
dipartimento che allarga la categoria: un documento sta in più raccolte e quindi in più pagine,
senza una tabella di collocazioni che costringerebbe il server a leggere le `props`. **Media e link
dichiarano `ISharedForReading`**: si scelgono da tutti, si gestiscono dal proprietario. Confermato
da Carmine anche l'ultimo punto: un documento AOD entra in una pagina TD **perché la pagina TD elenca
la raccolta AOD**, e non perché l'AOD lo infila (§4 della nota). **Seconda passata**, lo stesso giorno: una pagina
**pronta è in sola lettura** finché non la si ritira dalla revisione (l'editor salva da solo); chi
approva vede un **riepilogo per sezione** e una coda con il conteggio, e la versione ricorda chi
l'ha approvata; **togliere una pagina non si approva**; un **indice derivato alla pubblicazione**
dice quali pagine elencano quale raccolta e quali media usano, così un media usato altrove **si
archivia e non si cancella**; media e link **si aggiornano sul posto** (lo stesso logo con l'SVG
nuovo), con l'indirizzo che cambia insieme al file perché `/media` è `immutable`; **il menu passa
da WD e HQ**, con la posizione proposta dal dipartimento e confermata da chi approva. Le vecchie
rotte `/staff/{dept}/…` si tolgono senza redirect: il sito non è online. **L'indirizzo di una pagina
si compone e non si scrive** (nota §3.7): gerarchia fino a **tre livelli**, «sotto quale pagina» da
un albero più l'ultimo pezzo generato dal titolo nella lingua principale, anteprima con il controllo
di occupato e di parola riservata, un indirizzo per pagina e non per lingua; **il primo livello lo
creano solo WD e HQ**; chi approva **corregge** indirizzo e voce di menu invece di rimandare
indietro; un indirizzo cambiato dopo la pubblicazione lascia un **301 automatico**; news e documenti
hanno l'indirizzo generato. **(3) I moduli non appartengono ai dipartimenti**
(`decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`): eventi, tour e training sono
**sezioni a sé** anche nel back-office (`/staff/events`, `/staff/tours`, `/staff/training`), e
`IModule` non dichiara più un dipartimento. **Chi può fare che cosa** si stabilisce **estendendo i
grant**: il soggetto è un VID **oppure una posizione** (dipartimento + livello), con i valori
iniziali da `division.json` e le modifiche da `/staff/admin/permissions`. Una riga di modulo ha
**«a cura di» obbligatorio e multiplo** — l'ED coordina, un altro dipartimento collabora — e quel
campo **decide i permessi**: il SOD non tocca gli eventi degli altri. Ogni modulo ha un
**dipartimento di base sempre presente** — gli eventi sempre dell'ED, i tour sempre del FOD, i training sempre del TD —, gli
altri si aggiungono in collaborazione, e l'ED gestisce tutti gli eventi con il permesso sul proprio
dipartimento; il dipartimento di base sta in `division.json` (`modules.<key>.baseDepartment`), non
nel codice. Si estende **l'unica** `IOwnedByDepartment` a un insieme,
senza un secondo ramo nel handler. Le **widget delle dashboard di dipartimento sono blocchi Data dei
moduli**, sempre live e filtrati su chi guarda. Eventi e **sessioni di training sono pubblici** nel
calendario. `AtcModule` lascia il posto a un **modulo finto nei soli test**. Nessun codice ancora: le fasi si scrivono
dopo il merge della pila.

**Changelog 0.71** (12 set 2026, sera): **il blocco interattivo usato davvero.** Carmine ha scaricato
le linee guida, le ha date a un altro agente con un suo prompt («una pista 09/27, un pallino con
accanto IIVAO, circuito sinistro, finale a 3 NM con una tacca per miglio») e ha portato indietro il
risultato insieme a quello dello stesso prompt senza istruzioni. Il frammento scritto con le linee
guida **le rispettava tutte**; quello libero era più ricco e **nell'hub non sarebbe entrato** (pagina
intera, font da Google, tavolozza sua, una lingua). Le due cose che non tornavano non venivano dalle
regole — il prompt chiedeva insieme un circuito sinistro e una virata a destra, e la partenza non era
disegnata come fase — ma il confronto ha insegnato abbastanza da decidere cinque cose, tutte con
Carmine e tutte sulla PR #64: **(1)** le linee guida hanno una **sezione di stile** — un mestiere per
ogni variabile di colore e quattro colori al massimo, tratti e testo **in proporzione alla larghezza
del `viewBox`** invece di una tela fissa, i controlli sotto il disegno, 8–15 secondi per un circuito,
le **convenzioni di un disegno d'aeroporto**, una striscia di valori ammessa, e che cosa fare di una
richiesta che si contraddice; **(2)** **le cose vietate si denunciano da sole** — il guscio ascolta
`securitypolicyviolation` e `window.onerror` e li manda alla pagina, che li mostra **solo allo staff**;
scartato il controllo del codice al salvataggio, perché un'euristica che grida al lupo si impara a
ignorare; **(3)** il codice può arrivare **da un file del computer**, letto nel browser e mai caricato
— l'opzione (B) resta scartata — e una **pagina intera è rifiutata da tutte e due le strade**;
**(4)** **un'anteprima locale** generata dallo stesso guscio (`/embed/preview`, un download, mai una
pagina del sito), e le linee guida dicono le due strade per vedere un frammento prima di pubblicarlo
— una bozza, o quel file — e **vietano il `HUB` di ripiego** nel frammento, che nasconderebbe proprio
l'errore di un guscio assente; **(5)** **l'elenco di ciò che manca a `v0.2.0-m1`**, proposto in 0.68,
**è confermato** da Carmine («l'elenco mi torna»); restano da decidere dentro o fuori la
pubblicazione programmata e il blocco interattivo. Trovati per strada e corretti: in sviluppo
**`/embed` tornava `index.html`** perché mancava da `BACKEND_PATHS` — la stessa trappola di `/media`,
trovata allo stesso modo —, **il guscio prometteva un font che non può caricare** (ora `system-ui`), e
un test d'integrazione era **verde per la ragione sbagliata** (un VID che un'altra classe crea come
superadmin, con una posizione inesistente).

**Changelog 0.70** (12 set 2026): **il blocco interattivo**
(`decisions/2026-09-12-il-blocco-interattivo.md`), chiesto da Carmine con il caso d'uso scritto per
intero — creo un documento, scarico le linee guida, le do a Claude, incollo il codice, e chi legge
vede l'animazione — e deciso da lui su tre domande: **(C)** un endpoint che serve il frame e non un
`srcdoc`, la sezione che **si chiude** in stampa («un banner non serve a nulla»), un **permesso
dedicato**. I due casi d'uso che definiscono il perimetro sono «si legge e sotto si vede» e «si vede
che cosa succede secondo le scelte»; il confine è che il widget è interattivo **dentro la sua
scatola** — non cambia il testo intorno, non ricorda niente, non ha un indirizzo che porti a una
scelta. Cinque decisioni: **(1)** il codice sta nell'**envelope** (`source`, accanto a `renderMode` e
`frozen`) e non in `props`, perché con (C) il server deve leggerlo e dentro `props` non guarda mai —
tetti di 64 KB a blocco e 256 KB a pagina, controllati dal walker che non sa che cosa sia; **(2)** un
endpoint solo, `/embed/{contenuto}/{versione}/{blocco}`, immutabile e cacheato per un anno sul
pubblicato e `no-store` sulla bozza, che passa dall'**unico** authorization handler; la risposta
porta i **suoi** header (`default-src 'none'`, `sandbox allow-scripts`, `frame-ancestors 'self'` al
posto del `DENY` della pagina); **(3)** il **guscio è il contratto**, una risorsa compilata
nell'assembly, e le linee guida — `/embed/guidelines`, dietro il permesso — lo **citano dentro di
sé**, così il documento e ciò che descrive non possono divergere; **(4)** le linee guida impongono
due lingue, tastiera, `prefers-reduced-motion`, 360 px, niente rete, e portano l'esempio della
**pista 09/27 con il circuito sinistro**; **(5)** `Content.EmbedCode`, e la barra dei componenti
**non elenca** un blocco che chi compone non può usare — mentre un blocco che il template vieta
resta visibile e disabilitato, perché è un fatto della sezione e non del lettore. Due cose emerse
disegnando: l'editor scrive un campo che non è una proprietà (una `textarea` sotto il form, via lo
stesso `onEnvelope` di `renderMode` e `column`), e il renderer non sa in che pagina sta, quindi
l'indirizzo del frame arriva da un **contesto** che la schermata fornisce, come già fa per il chrome
dell'editor. Il registry passa a **30** blocchi. Branch `m1/interactive-block`.

**Changelog 0.69** (12 set 2026): **gli header di sicurezza**
(`decisions/2026-09-12-gli-header-di-sicurezza.md`), nati dalla scelta di Carmine sul blocco
interattivo — «ti direi C, e prepara un piano per mettere una CSP» — e fatti **prima** del blocco,
perché il motivo per cui quel frame sarà servito da un endpoint è proprio la CSP di questa pagina, e
costruire prima l'endpoint avrebbe voluto dire scoprire dopo se la CSP era possibile. **Il fatto da
cui parte tutto: l'hub non mandava nessun header di sicurezza** — nessuna CSP, nessun `nosniff`,
nessun `Referrer-Policy`, nessun `X-Frame-Options`. Non un difetto introdotto: una cosa mai passata
per nessuna fase, mentre M0 aveva costruito CSRF, proxy fidati e HSTS. Ora **un file solo**,
`config/security.json`, letto da **due** server: ASP.NET in produzione (`SecurityHeaders.cs`, un
`Use` calcolato all'avvio) e la **preview di Vite**, che è ciò contro cui gira la suite smoke — così
tutti e 62 quei test girano sotto la policy vera invece che sotto niente. Due cose **misurate** e non
decise a tavolino: `script-src 'self'` basta, perché l'`index.html` costruito non ha un solo script
inline; e `style-src` ha bisogno di `'unsafe-inline'`, perché con `'self'` il browser blocca tre
applicazioni di stile dai bundle di React e di Atmosphere — `style-src-attr` non aiuta, Chrome
attribuisce a `style-src` anche gli stili scritti via CSSOM, ed è così che React ne scrive uno. Lo
sviluppo ha una policy più larga (Vite inietta un modulo e apre un web socket), scritta accanto a
quella stretta con il perché. Niente nonce, niente hash, niente `report-uri`: al posto dei rapporti
c'è `e2e/security.spec.ts`, che **guarda la console** e fallisce su una violazione — l'unico modo in
cui quel guasto si denuncia, perché un foglio di stile bloccato non fa fallire nessuna asserzione.
⚠️ C'è un interruttore nel file, e la ragione è il piano §2.5: la produzione si raggiunge via FTP e
non c'è una shell, quindi una direttiva che rompe un'installazione vera deve potersi togliere senza
ricompilare. Sette test nuovi (quattro e2e, due di integrazione, un Vitest che tiene insieme
l'allowlist degli `embed` e `frame-src`). Branch `m1/security-headers`.

**Changelog 0.68** (12 set 2026): **il tag `v0.2.0-m1` cambia significato** — Carmine, dopo aver
chiuso la Parte 7 della scheda: «il tag non lo rilasciamo ancora, vorrei che la 0.2 significasse
editor di documenti e news pronto». Non è più «M1 è costruita e la scheda è stata riseguita»: è una
soglia di prodotto, e ciò che la milestone ha costruito resta comunque scritto in §13 e nel piano di
implementazione. **La scheda `tools/demo-m1.md` e la sua gemella italiana sono aggiornate al 12
settembre**: la Parte 7 è riscritta sull'editor di oggi — annulla e ripeti da tastiera, proprietà
applicate mentre si scrive, autosalvataggio, anteprima che dice la verità, trascinamento fra sezioni,
finestra di pubblicazione con changelog e AIRAC, le comodità del 10–12 settembre e l'otto-fondi del
colore — il documento operativo entra nella Parte 2 (dove serve, perché la sua tesi è «nessuna entità
nuova»), e la Parte 3 conta **29** blocchi e otto sfondi. ⚠️ **Che cosa manchi perché l'editor sia
«pronto» è una proposta, non una decisione**, e va confermata da Carmine: (1) la scheda riseguita da
capo; (2) l'interruttore «da rivedere» nella lista dei documenti, che oggi ha il filtro e non il
comando; (3) il **gruppo richiudibile** nel generatore di form (decima estensione), perché lo stato
vuoto di un campo opzionale occupa più spazio del campo — è la cosa che si vede di più aprendo un
form lungo; (4) la stampa guardata su carta e la tipografia delle schermate dense guardata a occhio;
(5) **da decidere se dentro o fuori**: la pubblicazione programmata (nota del 9 settembre, tre
domande aperte) e il blocco di codice interattivo chiesto il 12 settembre. Fuori di sicuro: le righe
della libreria senza impronta.

**Changelog 0.67** (12 set 2026): **il sito ha un colore**
(`decisions/2026-09-12-il-sito-ha-un-colore.md`, quattro idee su otto proposte, decise da Carmine:
«fai 1–4»). Il censimento che le ha fatte nascere: il pacchetto `@ivao/atmosphere-brand` porta
**dieci famiglie di colore** e l'hub ne usava **due** — `atmos` sulla barra, sul piè di pagina e su
due fondi, `fuselage` per tutto il resto. (1) **I titoli tornano del colore del testo**: la regola
base di Atmosphere dipinge h2–h6 in `fuselage-400`, cioè ogni titolo di sezione del sito e ogni
intestazione del back-office a ≈ 3,2 : 1 su bianco — basta per h2 e h3 come testo grande, **non
basta** per h5 e h6. È il **terzo** override di Atmosphere, e le linee guida dicono che un terzo
override è una decisione: una riga (`color: var(--foreground)`), fuori da ogni layer perché una
regola senza layer batte `@layer base`, contro una passata su 72 schermate. (2) **Il fondo `accent`
smette di essere grigio** — `ocean-50` nel chiaro, `ocean-900` nello scuro: era `fuselage-250`, e nel
tema scuro era `fuselage-700`, **lo stesso colore di `muted`**. (3) **`aurora` è l'ottavo fondo di
sezione** (`product-aurora-dark`), costruito come i tre scuri dell'11 settembre — porta `.dark`,
quindi ciò che gli sta sopra legge chiaro per costruzione; `aurora-mid`, più verde, lascerebbe il
testo secondario a 2,3 : 1 e non si prende. **§16.C cambia di nuovo**: i fondi sono otto. (4)
**L'accento sta sui grafici e mai sotto una parola**: un insieme chiuso di quattro famiglie
(`brand`, `ocean`, `aurora`, `artifice`) e una proprietà `accent` su `hero`, `cardGrid`, `iconGrid` e
`timeline`, disegnata sull'icona, su un filetto e su una barretta, con `brand` come valore
predefinito — nessuna pagina già scritta cambia. La regola non è prudenza: un grafico deve stare a
3 : 1, una parola a 4,5 : 1, e l'arancio del brand non ci arriva sul nuovo fondo azzurro. **Nessun
colore libero**, di nuovo, e nessun posto dove scrivere un tricolore. **Trovato sulla strada e
corretto**: il numero di un passo di `timeline`, 12 px nella pastiglia, misurava **4,15 : 1** su un
fondo scuro e 3,96 : 1 su quello azzurro — falliva AA anche prima di oggi, su ogni fondo scuro e su
`muted`; ora è del colore del testo, perché quel numero è il segnaposto del passo e non testo
secondario, e `contrast.spec.ts` porta un `timeline` così che lo dica da sé la prossima volta. Non
fatte, e scritte nella nota: il distintivo che dice qualcosa, pagina e scheda invertite, la famiglia
d'accento in `division.json`, il colore sui dati vivi. Branch `m1/site-colour`, sopra
`m1/media-dedupe`; **375 Vitest, 58 smoke, 306 .NET unit**, le 172 di integrazione alla CI.

**Changelog 0.66** (12 set 2026): **due immagini identiche caricate nella stessa libreria sono un
file solo** (`decisions/2026-09-12-due-immagini-identiche.md`, decisa da Carmine: «procediamo con le
immagini identiche»). Era **(c)**: nessuna colonna diceva che cosa c'è dentro un file. Ora
`cms_media.sha256` — calcolata da `MediaStorage.SaveAsync` nello stesso passaggio della copia —
con un indice per dipartimento; un caricamento che trova nel **suo** dipartimento una riga viva
con la stessa impronta risponde **quella riga, `200` invece di `201`**, e il file appena scritto
viene tolto. La libreria lo dice con un avviso; il selettore dell'editor sceglie il file e tace.
Mai attraverso i dipartimenti (visibilità e `alt` sono loro), mai due righe su un file solo (la
cancellazione conta chi lo nomina). Le righe di prima non hanno impronta e non la ricevono.
Migrazione additiva `AddMediaSha256`; nessun componente e nessun endpoint nuovo. Branch
`m1/media-dedupe`, sopra G14 per non far litigare due snapshot EF.

**Changelog 0.65** (12 set 2026, notte): **G14 costruita**, i cinque passi dell'ordine di lavoro in
cinque commit sullo stesso branch, tutto come la sezione G14 di `04-piano-implementazione-m1.md`
diceva, con quattro cose decise strada facendo e scritte lì: (1) **la pubblicazione chiede** —
`ConfirmDialog` (§8.3, quarto dell'elenco chiuso) **esteso** con campi e con una conferma non
distruttiva invece di una seconda finestra scritta accanto: la riga di changelog che ogni versione
poteva portare da M0 e non aveva mai avuto una casella, e il ciclo AIRAC su un documento; l'elenco
dei componenti non cresce. (2) **I giorni di un documento si leggono come giorni** (`dayOf`): il
server scrive una mezzanotte senza fuso e `new Date()` formattata in UTC era il giorno prima a est
di Greenwich. (3) **Il job scrive attraverso il change tracker** — il test di architettura non
ammette aggiornamenti in blocco oltre l'interceptor — e il prezzo, una versione di riga mossa sotto
chi edita alle tre e mezza di notte, è accettato e scritto. (4) **I due blocchi ATC sono nel cassetto
Data, sottogruppo `atc`, ma di tipo Content**: righe scritte dal redattore, niente risolto dal server.
Trovato e corretto sulla strada: **una riga nuova salvata con «Salva bozza» veniva fermata dalla
guardia** («lasciare la pagina?») perché la navigazione al suo indirizzo avviene dentro il
salvataggio — il giro e2e non l'aveva mai incontrato, parte sempre da un template. Il tag
`v0.2.0-m1` resta in attesa della scheda.

**Changelog 0.64** (12 set 2026): **G14 aperta**, dopo il merge delle PR #57 e #58 su `main`
(«mergia e poi vai di G14»). Il design della prima passata sta nella sezione G14 di
`04-piano-implementazione-m1.md`, scritto prima del codice come la nota del 10 settembre chiedeva.
Due scelte prese lì e non nella nota, da contestare se non convincono: **Archived e Superseded sono
due colonne** (`retired_at`, `superseded_by_id`) e non valori nuovi di `PublishStatus` — un
documento ritirato resta pubblicato e leggibile, con l'avviso in cima e il link al successore, e
niente deve insegnare al query filter o alla ricerca un terzo stato; e **le posizioni sono un campo
suggerito e aperto** (ICAO e FIR invece si scelgono da elenco), perché l'API IVAO non sincronizza le
posizioni e una tabella inventata sarebbe peggio di un campo. In §9.3 e §9.4 il documento
operativo è una **specializzazione** di `Document`, non un `kind`: `NoSecondContentEntity` resta il
test che lo dice. Il tag `v0.2.0-m1` **non è stato messo**: aspetta la scheda riseguita da Carmine.

**Changelog 0.63** (11 set 2026, notte): **sette comodità dell'editor**, proposte guardandolo e
scelte da Carmine (la 6, i pezzi riutilizzabili, no). (1) **La lingua dell'anteprima**: IT / EN
accanto alle larghezze; ogni valore tradotto letto dentro l'editor la segue
(`PreviewLocaleContext` sotto `useLocalized`) e ogni campo tradotto apre su quella scheda — prima
il sito era in inglese, il form apriva sull'italiano e quello che si scriveva non si vedeva. (2)
**Doppio clic** su un blocco o una sezione: scelto, e il cursore nel primo campo del pannello.
(3) **Canc** elimina, **⌘D** duplica, **Esc** lascia: gli stessi comandi della targhetta, fuori
da un campo. (4) L'oggetto scelto è **portato in vista** sulla pagina. (5) **Duplica sezione**,
dalla targhetta e dall'outline: copia subito dopo, identificatori nuovi, senza chiave. (7) **Il
selettore di file carica** nella libreria del dipartimento — dallo stato vuoto e sotto la griglia,
con la stessa chiamata della schermata della libreria, passata al generatore come `uploadMedia`;
quello che si carica è scelto. Non riapre la scelta di G1 «un posto solo da cui un file entra»: la
chiamata è una, offerta da un posto in più. (8) **Bozza | Pubblicato** sull'anteprima: la versione
pubblicata disegnata dallo stesso renderer senza picking, o «non ancora pubblicata».

⚠️ **Verificato, e da decidere: due file identici caricati sono due righe e due file.** Non c'è
hash né controllo sul nome: ogni upload salva sotto un nome nuovo (`Guid`) e crea una riga. Una
deduplica vorrebbe una colonna `sha256` (migrazione additiva), il calcolo all'upload — i primi
byte già si leggono per riconoscere il formato — e una risposta «c'è già, eccolo» che restituisce
la riga esistente. È (c): mezza pagina prima del codice, se Carmine la vuole.

**Changelog 0.62** (11 set 2026, sera): tre richieste di Carmine mentre compone. **Le sezioni si
annidano fino a quattro livelli**, non tre: «una sezione in una sezione in una sezione in una
sezione». `MaxDepth` passa a 4 nel validatore, e l'editor offre «Aggiungi riga» fino al terzo livello
compreso — dall'outline e dalla targhetta — così l'ultimo consentito è il quarto; il 10 settembre la
riga era offerta solo al primo livello, «una riga dentro una riga è rumore», e la pratica ha detto il
contrario. **Il selettore di file porta alla libreria**: dove chiede di caricare un file «nella
libreria media del dipartimento», c'è il link per andarci — anche quando la libreria non è vuota,
sotto la griglia. L'indirizzo viaggia sulla query (`meta.libraryHref`), perché la query è la sola
cosa della libreria che raggiunge il selettore attraverso il generatore di form. **Due cose viste in
uno screenshot**: la targhetta della prima sezione stava a cavallo del bordo dell'anteprima, che
ritaglia, e usciva tagliata a metà — ora sta dentro l'aria della sezione; e fra gli sfondi la
pastiglia «foto» era una riga nera in diagonale che si leggeva come un divieto, «nessuno» un punto
bianco su fondo bianco — ora i due che non sono un colore hanno un glifo: un cerchio barrato e una
foto. **Poi, sempre la sera: anche i blocchi si trascinano sulla pagina, e fra sezioni diverse.**
Un blocco scelto ha il grip sulla targhetta; mentre lo si trascina compaiono gli stessi slot dei
componenti della barra, in ogni sezione non bloccata, e lo si lascia dove si vuole — la sua colonna,
un'altra, un'altra sezione (`moveBlockTo`). Il renderer riceve `BlockDraggable` dal contesto di
picking come riceve `Sortable` per le sezioni; un blocco a cui l'editor non risponde comandi (una
sezione bloccata) non ha grip. **La strada da tastiera** è un selettore «Sezione» nelle proprietà del
blocco, accanto a «Colonna»: lo sposta in fondo alla prima colonna della sezione scelta. In più, un
campo di ricerca in cima alla barra dei componenti, e barre di scorrimento sottili nei due pannelli
laterali. **E un blocco in cui non è scritto niente si disegna come segnaposto**: un titolo appena
aggiunto non disegnava nulla e sembrava perso; ora, finché è vuoto, la pagina mostra al suo posto
un riquadro tratteggiato con l'icona, il nome e «compilalo nel pannello a destra», che sparisce
alla prima cosa scritta. «Vuoto» lo decide lo schema (`isBlank`): i campi che portano contenuto —
parole, un file, una data, una lista — tutti non scritti, qualunque impostazione sia scelta; un
blocco Data non è mai vuoto, perché disegna una risposta o il suo stato vuoto. Il visitatore, che
non legge mai una bozza, non lo vede mai.

**Changelog 0.61** (11 set 2026, sera): **i comandi stanno anche sull'oggetto, nella pagina.**
Chiesto da Carmine con G15 appena costruita: aggiungere e togliere sezioni, e togliere un blocco,
**dalla pagina** e non solo dall'outline; e nell'outline capire, quando una sezione è divisa in
colonne, «dove va cosa». Riapre il punto 2 della nota del 10 settembre
(`2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`), che aveva tenuto i comandi nel pannello perché
il renderer non deve mettere su chrome da editor. **La risposta è la stessa del 9 settembre:** il
chrome esiste solo attraverso il contesto di picking, che sul sito pubblico è `null`. Il contesto
porta `actions` — l'editor risponde con ciò che il template permette, la pagina disegna esattamente
quella lista — e `onAddSection`; la cosa scelta porta una **targhetta** con il nome e i comandi
(sposta su e giù, duplica ed elimina su un blocco; sposta su e giù, aggiungi riga ed elimina su una
sezione — «le sezioni già posizionate le vorrei poter spostare a mano»), e in fondo alla pagina
c'è «Aggiungi una sezione» come una colonna vuota offre un blocco. Le regole del template — niente su
una sezione bloccata, niente eliminazione di una obbligatoria — sono lette in un posto solo e
valgono per outline e pagina insieme. **L'outline elenca una colonna alla volta**, ognuna col suo
nome, una lista ordinabile per colonna; per conseguenza «sposta su/giù» muove un blocco **dentro la
sua colonna** — prima scambiava posti nella lista senza che sulla pagina si muovesse niente — e un
blocco lasciato su uno di un'altra colonna non si muove, come già un drop fra due sezioni. E una
**riga si sposta fra le righe della sua sezione**: prima `moveSection` muoveva solo il primo livello
e le frecce su una riga nell'outline non facevano niente. Costruito lo stesso giorno, sulla PR #58
di G15. **E le sezioni si trascinano sulla pagina** («a mano nel senso di trascinabili»): il
contesto di picking porta due componenti in più, `SortableGroup` intorno ai fratelli — le sezioni
della pagina, o le righe di una sezione — e `Sortable` intorno a una di loro, che restituisce dove
attaccare il nodo, lo stile che lo muove e la presa; la presa è un **grip sulla targhetta** della
sezione scelta, così cliccare l'aria di una sezione la sceglie e basta. Lo stesso `DndContext`
della barra dei componenti sente il rilascio, e distingue le due cose che vi si trascinano con una
collision detection che guarda solo il proprio genere — un componente sopra una sezione non le è
«sopra», né una sezione sopra uno slot. Le frecce restano, e sono la strada da tastiera.

**Changelog 0.60** (11 set 2026): **l'editor che risponde**, fase **G15**, decisa da Carmine con
davanti il page builder di va.ivao.aero e il nostro editor uno accanto all'altro. Nota
`decisions/2026-09-11-l-editor-che-risponde.md`; perimetro e ordine in `04-piano-implementazione-m1.md`,
fase G15. **Viene prima di G14** (il documento operativo), perché è ciò che si sta collaudando adesso.

Cinque cose, quattro delle quali non toccano il server: le **proprietà di un blocco si applicano
mentre si scrive** (settima estensione del generatore di form, `onChange` su valori validi, via il
pulsante «Apply»); **annulla e ripeti** con `Ctrl/⌘+Z` e `Shift+Z`, che non agiscono dentro un campo,
con **coalescenza** per chiave — una frase scritta è un passo, non venti — e cinquanta passi;
**trascinare un componente dalla barra fra due blocchi**, con dnd-kit che c'è già e un `DropZone`
portato dal contesto di picking, così il renderer non importa dnd-kit e il pubblico resta inerte; e
**l'anteprima mobile vera**: misurato, a 390 px una sezione a due colonne ne disegnava ancora due da
167 px, perché `md:grid-cols-2` guarda la finestra. La nostra anteprima era finta come la loro; con
le container query di Tailwind 4 sulla radice del renderer non lo è più, senza iframe.

La quinta, l'**autosalvataggio della bozza**, è l'unica che tocca il DB, e ha tre decisioni dentro:
**dieci secondi** di pausa e all'uscita, mai su una riga nuova, mai se non è cambiato niente; la
versione della riga **esce dal form** dei metadati, che altrimenti verrebbe rimontato mentre
qualcuno scrive; e l'audit dell'autosalvataggio è la **(B)**: una riga `autosaved` con i campi
cambiati e **senza il corpo** (~200 byte invece di due copie del corpo), mentre «Save draft» premuto
a mano e «Publish» restano auditati per intero. Scelta per il DB condiviso con vIPI (§2.5), e perché
della bozza di dieci secondi fa nessuno chiederà la storia. Si chiude così anche il punto 3 della
nota del 10 settembre (`2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`).

**Changelog 0.59** (11 set 2026): **le due dashboard personali hanno una data** — si progettano
**per prime in M2**, prima di `05-design-m2.md`. Decisione di Carmine, presa mentre collaudava
l'editor.

Fino a oggi nessuna milestone le nominava. **`/me`** compone i widget che i moduli registrano, ma il
nucleo ne registra uno solo (`welcome`): il «cosa posso fare oggi» di §8.1 si riempie coi moduli. La
**dashboard personale da staffista** non esiste: `/staff` è una porta verso la dashboard del primo
dipartimento raggiungibile, lasciata così l'11 settembre «finché non si progettano le sue sezioni»
(`decisions/2026-09-11-la-barra-laterale-i-sottomenu-e-il-carattere.md`).

**Perché proprio all'apertura di M2:** Events è il primo modulo che registra widget per `/me` (i
prossimi eventi a cui sono iscritto, le mie prenotazioni). Se la forma della dashboard si decidesse
dopo, quei widget nascerebbero in una forma da rifare. La parte staff è una schermata nuova, quindi
caso (c): **una nota di design** in `decisions/` su tutte e due, poi il piano aggiornato, poi il codice.

Che cosa la nota deve chiudere:

- **`/me`**: quali sezioni, chi ne decide l'ordine (fisso, o scelto dalla persona), e che cosa vede
  chi non ha ancora niente (nessuna iscrizione, nessun training);
- **`/staff`**: quali sezioni personali (per esempio ciò che aspetta me — bozze, contatti arrivati,
  richieste — e i miei dipartimenti), e se sono **widget dello stesso registry**: `WidgetDescriptor`
  porta già un `Department?` che nessuno usa. ⚠️ Una seconda macchina per comporre schermate l'ha già
  scartata la dashboard di dipartimento (§9.3), e lo stesso vale qui;
- **il rapporto con `/staff/{dept}`**: il tasto Staff punta già a `/staff`, quindi quando la pagina
  esiste ci porta senza essere toccato.

Toccate §8.1, §8.2 e §13.

**Changelog 0.58** (11 set 2026): **una sezione può stare su tre fondi scuri**, e mentre si compone
si vede com'è divisa. Nota `decisions/2026-09-11-la-sezione-si-vede-com-e-divisa.md`, chiesta da Carmine
con davanti il page builder di va.ivao.aero.

**Riaperta e cambiata una convenzione di §16.C**, chiusa il 6 settembre: gli sfondi di una sezione
erano **quattro** (`none`, `muted`, `accent`, `image`) e ora sono **sette** — si aggiungono `brand`,
`deep` e `dark`, i tre fondi scuri della tavolozza di va.ivao.aero, presi dai token di Atmosphere
(`atmos-700` è esattamente il loro #0D2C99). Ognuno è disegnato **nel tema scuro**, quindi quello
che un blocco ci scrive sopra si legge per costruzione; misurato, e il blu del marchio ha avuto
bisogno di un grigio secondario più chiaro (3,50 : 1 prima, 4,88 dopo). **Il colore libero no**: è
l'unico pezzo loro non preso, perché un colore scelto a mano non può promettere che il testo si legga
— la ragione per cui gli sfondi erano un insieme chiuso resta intera.

**Mentre si compone**, ogni colonna di una sezione è tratteggiata e una colonna vuota dice «+ Aggiungi
qui»: sceglierla manda lì il prossimo componente della barra di sinistra. Prima un componente finiva
**sempre nella prima colonna**. Il visitatore non vede niente di tutto questo: è lo stesso contesto di
picking del 9 settembre, che sul sito pubblico non esiste. Il trascinamento dalla barra alla colonna
resta il punto 3 aperto del 10 settembre.

**Changelog 0.57** (10 set 2026): **un documento pubblicato dirà di sé**, e due richieste restano
sul tavolo. Nota `decisions/2026-09-09-il-documento-dice-di-se.md`, scritta perché nessuna delle tre
vivesse solo in chat.

**Deciso: il piè di pagina di un documento** sta **alla fine**, sempre lì, e chi edita sceglie solo
se mostrarlo. Dice chi ha pubblicato, quando, e — facoltativo — il **ciclo AIRAC**. Metà esiste già:
`cms_content_versions` porta `version`, `changelog`, `published_at` e `published_by` da sempre, e il
servizio di pubblicazione li scrive a ogni giro. L'AIRAC è **una colonna sulla versione**, non sulla
riga: è una proprietà di quella pubblicazione. ⚠️ E **non** è il ciclo AIRAC di vIPI, che §9.3
scartava: qui è un'etichetta facoltativa, non un meccanismo di release. Non costruito.

**Parcheggiate**: la **pubblicazione programmata** — che è (c), con tre domande aperte, e il cui
esempio (un evento) tocca il design di M2 — e la **stampa dei soli documenti**, che è piccola e ha
dentro una trappola: `tabs` e `accordion` nascondono testo, e su carta devono essere aperti.

**Changelog 0.56** (10 set 2026): **una sezione contiene righe**, e due comandi si scelgono
guardando invece che scrivendo. Nota `decisions/2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`, nata
guardando il page builder di HQ nel browser di Carmine.

⚠️ **La riga non è un modello nuovo: era già nostro e non l'aveva mai acceso nessuno.** `MaxDepth` è
3 dal M1, il renderer disegna una sezione annidata dentro il contenitore di larghezza del genitore,
`templateDiff` le confronta per `parentKey` e `clampColumns` ricorre. Mancava solo che `addSection`
sapesse mettere qualcosa **dentro** — e infatti nessuna delle dieci pagine e dei template seminati
annida. Avevamo costruito tre livelli, li validavamo, li disegnavamo, e l'editor ne offriva due.

Che cosa compra: la sezione porta la **cornice** — lo sfondo, l'aria, la larghezza — e ogni riga
dentro porta le **proprie colonne**. Una sola fascia di colore può tenere due colonne e poi tre, che
prima voleva dire due sezioni e quindi due fasce.

**Sfondo e colonne escono dal form** e diventano pastiglie e diagrammi applicati al clic
(`SectionFrame`): sono le due cose di una sezione che si giudicano a occhio, e un form che tenesse un
valore vecchio disferebbe la scelta al primo «Applica». Restano nel pannello e non sopra la sezione
come fa HQ, perché il nostro renderer è **lo stesso del sito pubblico** e non deve mettere su chrome
da editor.

⚠️ E il censimento di §2.3-ter va letto con una correzione: **il page builder di HQ non è una tela.**
Misurato nella loro pagina — nessuna libreria di trascinamento, zero elementi in posizione assoluta.
È un albero ordinato Sezione → Riga → Blocco dove il trascinamento riordina. La «tela drag & drop»
che il piano aveva scartato come «il pezzo più costoso» non esiste nemmeno da chi l'aveva ispirata.

**Changelog 0.55** (9 set 2026): **una pagina si compone guardandola.** Il canvas drag & drop era
stato scartato in una riga (§2.3-ter); Carmine ha chiesto di riaprirla, la nota
`decisions/2026-09-09-comporre-una-pagina-guardandola.md` ha messo le due strade a confronto, e lui
ha scelto la **(A)**: l'anteprima diventa la superficie di composizione.

Si clicca un blocco nella pagina disegnata e si aprono i suoi campi, con la pagina che resta sotto
gli occhi. E con essa **la pagina stessa è diventata una selezione**: i metadati non sono più un
modulo sopra l'editor — misurava 1182 px in una finestra da 950, con la pagina e i pulsanti sotto la
piega — ma le proprietà della pagina, nello stesso pannello. La barra è in cima e appiccicata, e
`Save draft` invia il form da fuori con `form="…"`. La pagina che si compone comincia a 466 px invece
di 1588. **Il modello dei dati non cambia di una riga**: niente coordinate, niente dimensioni sui
blocchi, i template continuano a significare quello che significavano, il responsive e la stampa
restano gratis.

⚠️ E c'è un patto che vale la pena scrivere in §16, perché è il prezzo di avere **un renderer solo**
per il pubblico e per l'editor: l'interattività è un contesto che vale `null` e che **il percorso
pubblico non monta**. Non un flag da spegnere: una cosa che nella pagina di un visitatore non esiste.
Il primo test del pezzo asserisce esattamente quello, ed è quello che non si allenta.

⚠️ Due obiezioni del piano contro la tela sono cadute e la nota le registra: comporre da telefono non
è un requisito (si compone da PC o tablet), e `locked` conserva il suo significato anche su una tela.
Resta in piedi il costo vero, che da fuori non si vede — **un blocco oggi non ha una dimensione** — e
la (B) resta aperta se la (A) non basta.

**Changelog 0.54** (9 set 2026): **M2 si divide in due**, e la ragione non è tecnica.

Il piano metteva nella stessa milestone il **primo deploy su staging Plesk** e il **modulo Events**.
Il deploy era già in attesa delle risposte A9 di Ivao.It (§15.2c); adesso si aggiunge che **chi
materialmente carica su Plesk non è disponibile** (detto da Carmine il 9 set 2026). Due attese
diverse sullo stesso pezzo, e nessuna delle due dipende da noi.

Quindi M2 procede **dal modulo**: design, tabelle `evt_`, schermate, permessi. Il deploy resta nella
milestone e ne è la seconda metà, da fare appena si sciolgono i due nodi — non si sposta a M3, perché
il pacchetto va provato su Plesk prima che ci siano tre moduli sopra.

⚠️ Quello che si perde ad aspettare va scritto, o si finge che sia gratis: fino al primo deploy vero
**non sappiamo se il pacchetto self-contained gira su quella macchina** — la CI lo costruisce e i
test girano su una MariaDB 11.4.10 vera, ma Passenger, il document root, i privilegi dell'utente DB e
il `sql_mode` di quel server non li ha ancora visti nessuno. È il rischio n.1 di §11.3 e resta
aperto, più a lungo di quanto il piano prevedesse.

**Changelog 0.53** (9 set 2026, terza esecuzione della demo): tre difetti, e due di essi sono
decisioni.

**L'ora si scrive come in aviazione**, dappertutto: **24 ore**, `Z` per lo zulu, `LT` per l'ora
locale, e la **data solo dove non c'è già** — nella griglia lo dice il quadrato, nella lista
l'intestazione del giorno, e resta nell'agenda, che è una lista che corre in avanti e non ha né
l'uno né l'altra. Una riga legge `14:00Z (16:00 LT)`. È una riga sola di codice perché `useMoment` è
l'unico posto che formatta un istante — che è la ragione per cui esiste.

⚠️ **La seconda deroga ad «Atmosphere così com'è»** (§4, §16.C), e va contata: la loro `Select` dà al
popup l'altezza del **trigger**, quindi la lista è alta una riga qualunque cosa contenga — misurato,
46 px per righe da 30. Non è gusto come il grigio: è un controllo che mostra una voce di quattro e
non dà al lettore modo di sapere che ce ne sono altre. Una regola in `index.css` restituisce al
popup l'altezza della sua lista, limitata da quella disponibile sullo schermo. **Le deroghe sono
due, e vanno tenute due.**

⚠️ E un difetto che nessun test poteva vedere, perché vive nella cucitura dello sviluppo: in dev la
SPA e il backend sono **due server su due porte**, e `/media/{id}/{name}` non era fra i percorsi
inoltrati — quindi ogni immagine era un `<img>` che puntava a `index.html`. Ora l'elenco dei percorsi
del backend è un file solo (`web/backendPaths.ts`), il proxy nasce da lì, e un test lo difende. Con
lui erano rotti anche `/sitemap.xml` e `/robots.txt`.

**Changelog 0.52** (9 set 2026): **un campo suggerito può chiedere al server** — la **nona**
estensione del generatore di form, e chiude il difetto che il changelog 0.51 apriva.

`onSuggestSearch` è una funzione che il form chiama con il nome del campo e quello che ci si sta
scrivendo, dopo trecento millisecondi di pausa. Il generatore non sa che cosa farne: la schermata la
riceve e rifà la sua domanda con `q`. Nel menu quel testo diventa la ricerca delle pagine e dei link,
che il server già sa fare su titolo e slug — quindi **zero endpoint nuovi**, e il tetto di cento
righe smette di essere un tetto perché non è più l'elenco intero a dover stare in una pagina.

⚠️ È opt-in: un form che non passa la funzione filtra in memoria come prima, ed è quello che vuole
un elenco corto. Un test lo tiene fermo, perché il rischio di un'estensione così è che tutte le
schermate comincino a fare richieste senza che nessuno lo abbia chiesto.

**Changelog 0.51** (9 set 2026): **i template di un dipartimento hanno una schermata**, che è
l'ultimo dei sei difetti di rifinitura elencati dal rapporto di chiusura di M1.

⚠️ **E un difetto nuovo, che la decisione dell'8 settembre ha creato e che va deciso**: l'indirizzo
di una voce di menu offre **cento pagine**, una richiesta sola, e adesso che il campo *decide* invece
di *suggerire*, la pagina numero centouno è un indirizzo che non si può scegliere — mentre la casella
dice «qui non corrisponde niente», che non è vero. Trovato dal giro completo, che su un banco con 120
pagine non trovava più `/start`. La strada giusta è **il campo che cerca sul server** — la nona
estensione del generatore di form, quindi una decisione — e sta scritta in
`decisions/2026-09-08-dove-puo-portare-una-voce-di-menu.md`.

§9.4 del design M1 e §2 di `CLAUDE.md` dicevano già di chi sono i template — Director, Assistant
Director, WM, AWM e, sul proprio dipartimento, coordinator e assistant coordinator, con
`Content.ManageTemplates` — e nel back-office non c'era **niente**: tenuti fuori dalla lista dei
contenuti di proposito, offerti dal picker solo per farne una pagina, e l'unico modo di aprirne uno
era scriverne l'indirizzo. Adesso `/staff/<dip>/templates` è la lista generica con il filtro
rovesciato, l'editor è **lo stesso** dei contenuti (un template è una riga di `cms_contents`, e un
secondo editor sarebbe esattamente ciò che §9.3 esiste per impedire), e un pulsante ne crea uno.

Tre cose decise mentre si faceva, e scritte qui perché sono scelte e non dettagli:

- **il `kind` si sceglie prima**, accanto al pulsante, perché decide quali campi il form disegna e un
  form che si ridisegna sotto le mani di chi lo compila è peggio;
- **quante righe sono nate da un template si legge sulla sua schermata e non come colonna della
  lista**: una colonna sarebbe una richiesta per riga, e `DataList` disegna una query sola. È anche
  dove serve — davanti a chi sta per modificarlo;
- **niente endpoint nuovo**: il conto è la stessa lista filtrata per `templateId`, letta per il suo
  `total`. Gli endpoint a mano restano otto.

⚠️ La regola vera resta del server, come sempre: `ExtraWritePolicy` chiede `Content.ManageTemplates`
sull'entità **dopo** che il payload le è stato applicato, quindi «creare un template» è già rifiutato
a chi non può cambiarne uno. Nessuno lo aveva mai provato perché nessun client lo aveva mai chiesto —
i template si seminavano soltanto — e adesso un test di integrazione lo prova.

**Changelog 0.50** (9 set 2026): **il tema scuro ha il suo grigio**, e con esso la prima deroga a
«Atmosphere così com'è» (§4, §16.C). Nota `decisions/2026-09-09-il-grigio-dei-testi-secondari.md`.

Atmosphere ribalta ogni colore di testo per il tema scuro tranne `--muted-foreground`, che resta
fuselage-500 in tutti e due: un grigio scuro su bianco fa 5,89 : 1 e su `#12131b` fa **3,14 : 1**,
sotto il 4,5 : 1 che AA chiede a 12 e 14 px. **Una riga** in fondo a `web/src/styles/index.css` lo
porta a fuselage-400 nel solo tema scuro — 5,66 : 1 — e il tema chiaro non si muove.

⚠️ Perché una deroga e non una passata sulle nostre schermate: **quel token lo usano anche i
componenti di Atmosphere**, 24 volte nel loro bundle. Riscrivere le nostre 72 occorrenze ne
lascerebbe 24 illeggibili che non raggiungiamo. La deroga è **una** e va tenuta tale: si scrive qui,
sta in un posto solo, e un test la difende.

⚠️ Il punto delicato è **dove** sta la riga: il foglio di Atmosphere si carica dopo le utility di
Tailwind, quindi in fondo a `index.css` e non prima — misurato in un browser, come già era servito
per `hidden sm:block`. `e2e/contrast.spec.ts` misura ogni testo secondario visibile di nove schermate
in tema scuro e fallisce se la riga sparisce o smette di vincere.

**Changelog 0.49** (9 set 2026): **nell'indice di ricerca finisce solo prosa**, e la regola diventa
strutturale invece che scritta.

§16.C e `CLAUDE.md` §4 vietavano già «nessuna stringa che non sia prosa dentro `props`». Il divieto
non ha funzionato: `hero` non l'ha seguito, e nessuno poteva accorgersene finché uno snippet non ha
contenuto prosa vera — «… quattro semplici passi. `left muted` Prima di tutto…», che sono `align` e
`tone`. Adesso l'estrattore indicizza **solo i valori dentro una mappa tradotta**: la prosa in un
blocco è `Localized`, un'enumerazione è una stringa nuda e un URL pure. Il server continua a **non
conoscere nessuno schema** (design M0 §5.3), che era il vincolo. Nello stesso punto la prosa perde il
Markdown, perché uno snippet non deve leggersi con gli asterischi.

⚠️ Due conseguenze da tenere. La prima: «la prosa in un blocco è sempre `Localized`» **non era vera**
— `logoWall.items[].name` e `testimonial.author` sono nomi propri, scritti una volta perché uguali in
ogni lingua, e da oggi non si cercano più; renderli tradotti è una migrazione di props, cioè una
decisione a sé. La seconda: **non esiste una reindicizzazione**, e non si costruisce per questo — le
righe già scritte si aggiornano quando qualcuno le salva.

**Changelog 0.48** (8 set 2026, **G13**, seconda esecuzione della demo): due decisioni, e la seconda
è la più stretta che questo prodotto abbia preso su un campo.

**Una riga si modifica dalla lista** (deciso da Carmine, nota
`decisions/2026-09-08-modificare-da-una-lista.md`, opzione 2): la lista generica disegna un controllo
in una cella per **tre soli tipi** — numero, booleano, enumerazione — e mai per un testo tradotto o un
file, che hanno bisogno del form. Nessun verbo nuovo: la cella rilegge la riga e la riscrive, quindi
il `rowVersion` risponde 409 a chi ha salvato nel frattempo esattamente come dal form. La schermata
deve darle un modo di salvare, o il controllo non compare: due condizioni, o su una lista senza
salvataggio si vedrebbe un campo che non fa niente.

**Una voce di menu porta solo dove il sito possiede qualcosa** (deciso da Carmine, nota
`decisions/2026-09-08-dove-puo-portare-una-voce-di-menu.md`): una pagina di `cms_contents` — **anche
bozza** —, una schermata dell'applicazione, o un link **in uso** di `cms_links`. Nient'altro, e il
campo nel form non è più libero: quello che si scrive cerca nell'elenco, non è un valore.

⚠️ Il motivo non è il menu, ed è la ragione per cui questa è una regola e non un suggerimento: **ogni
indirizzo che esce dal sito vive in una tabella sola.** Un menu che accetta qualunque URL è un sito
con indirizzi sparsi dentro; con questa regola, spostare il forum è una riga di `cms_links` e il menu
la segue. La regola sta **sul server** come tutte le altre (§16.6): il campo chiuso nel client è una
comodità, e una comodità non è una regola. Costa una costante scritta a mano in due posti — le
schermate del router, che il contratto non può portare — e i test che la tengono ferma.

**Changelog 0.47** (7–8 set 2026, **G13**, dopo che Carmine ha eseguito la demo): due difetti trovati
usando, e tre decisioni — il segno di un dipartimento, l'avviso a quattro stati, e il vocabolario dei
tipi di evento.

**Il soffitto di visibilità vale anche per le immagini**, ed è lo stesso `VisibilityCeiling` del
changelog 0.29 — non un secondo controllo. Un file arriva nella libreria visibile allo staff
(diventa pubblico perché qualcuno lo dice, non per essere arrivato), quindi un'immagine caricata e
messa in una pagina era staff-only, la pagina usciva lo stesso e il visitatore vedeva un'immagine
rotta: l'indirizzo di un file che non può vedere risponde 404, ed è giusto. Ora la pubblicazione
**rifiuta** — non ripara, perché pubblicare una pagina non deve rendere pubblico un file di
nascosto — e lo dice con il percorso della proprietà, come per una traduzione mancante. Vale per
i tre modi di nominare un file: il corpo, la copertina di una news e il file di un documento.
`BlockDocumentWalker` sa dire quali file mostra un documento, con i nomi delle proprietà presi da
`JsonQuery`, l'unico posto che già li conosceva.

**Il logout non ridisegnava la pagina** perché il bootstrap non è solo una query: la radice lo carica
una volta e lo passa come **contesto** del router, ed è quella copia che l'header, la sidebar e le
guardie leggono. Invalidare una query non rifà un `beforeLoad`. Un solo posto lo dice adesso —
`sessionChanged` — e lo usa anche la risposta al 401.

**Il generatore di form ha imparato la settima cosa**, regola (b): `slugFrom`, un campo che si
propone da un altro. §12 del design M1 ne prevedeva cinque, G11a ha fatto la sesta e questa è la
settima — il numero da riportare alla chiusura di G13 è **sette**, e la ragione dello scarto è che
due le ha chieste l'uso, non i blocchi. Segue il titolo finché il campo contiene esattamente ciò che
è stato proposto, e smette per sempre appena qualcuno ci scrive: un indirizzo sopravvive alla pagina,
e uno che si riscrive sotto le dita di chi lo sta scrivendo sarebbe peggio di uno da scrivere a mano.

**I tipi di evento del calendario sono un vocabolario di divisione** (deciso da Carmine l'8 set
2026, nota `decisions/2026-09-08-tipi-di-evento-di-divisione.md`): `cms_calendar_kinds`, servita dal
motore CRUD in **modalità globale** — quella che i grant usano da M0 — letta con `Calendar.View` e
scritta con `Calendar.ManageKinds`, che è **globale** e quindi appartiene per costruzione ai ruoli
che raggiungono ogni dipartimento. Non sono le categorie, che restano per dipartimento.

⚠️ Due conseguenze da non perdere. Il `kind` di una voce **non è più testo libero**: il validatore
chiede al vocabolario, ed è l'unico validatore dell'hub che interroga il database — una voce che un
modulo proietta non passa da quel DTO e resta libera. E il vocabolario viaggia in **`/api/me`**,
perché una chip su un calendario pubblico deve dire la parola e il colore e un visitatore non può
leggere `/api/calendar-kinds`. Il colore è una colonna: un colore scelto accomuna due tipi che vanno
insieme, un hash no.

**«Cosa manca per pubblicare» è una domanda al server, non un calcolo del client.** Le regole della
pubblicazione stanno in un posto solo; il client che se le ricalcolasse sarebbe la seconda copia, e
la seconda copia è quella che invecchia (§16.E, regola (b)). Quindi
`GET /api/content/{id}/publish-problems` fa gli stessi controlli senza scrivere niente, e la
schermata li disegna. ⚠️ È il **quarto** verbo a mano appeso a un gruppo `MapCrud` — §16 chiede che
ognuno sia giustificato, e questa è la giustificazione. Non è un CRUD a mano: quelli restano **zero**.

**L'avviso a quattro stati è il quinto componente dell'elenco chiuso** (§8.3), chiesto da Carmine e
scritto **con la riga nel piano**, che è la condizione che il piano di implementazione poneva.
`Notice` più `useNotice()`: lo stesso avviso come riquadro e come conferma in un angolo, una tabella
sola di quattro toni, e la conferma che l'editor deve a chi clicca (richiesta 6) è il suo primo
cliente. `ProblemAlert` resta dov'è.

**Niente icone per i dipartimenti: la sigla è il segno** (deciso da Carmine). Erano nove scudi
identici. Ragione: un fork non-IVAO riscrive comunque l'enum `Department`, quindi una mappa
«dipartimento → icona» vivrebbe nel perimetro IVAO e gli costerebbe lavoro, e la sigla è già
l'identificatore che lo staff usa. ⚠️ Il segno **non** entra nell'elenco chiuso di §8.3: non prende
props, si monta solo nello slot di un'icona, e nasce dai dati. Il quinto componente dell'elenco
resta quello che Carmine ha chiesto — l'avviso a quattro stati — e va scritto lì quando si fa.

**Changelog 0.45** (7 set 2026): **M1 è chiusa.** Il conto contro la previsione di design M1 §12 —
6 tabelle / 3 aree di permessi / 5 estensioni del generatore / 4 componenti custom / 1 endpoint a
mano — è **6 / 3 / 6 / 4 / 7**: tre esatti, uno spiegato (la sesta estensione del generatore l'ha
chiesta *scrivere un template*, non i blocchi), e uno che ha insegnato qualcosa.

⚠️ **§16 va letta con una metrica diversa, ed è l'unica correzione che questa chiusura chiede.**
«Endpoint scritti a mano» contava la cosa sbagliata. Dei sette di M1, **cinque sono dichiarati nel
testo del design** e semplicemente non erano stati contati (l'upload multipart e il file servito da
disco di §2, le due preferenze della famiglia `/api/me/…` di §5.2); i due che nessuno aveva previsto
sono `sitemap.xml` e `robots.txt`, che non sono endpoint dell'applicazione ma due file che il server
produce. E **nessuno dei sette è un CRUD scritto a mano**: i primi tre pendono da un gruppo
`MapCrud` con `options.MapCreate = false`, cioè il motore fa lista, dettaglio, modifica e
cancellazione e la mano scrive solo il verbo che il motore non può fare — la regola (b) di §16.E
applicata, non aggirata.

Da M2 i numeri da portare nel rapporto di chiusura sono quindi **due**:

- **CRUD scritti a mano: 0** — è questo che deve restare zero, ed è ciò che §16.6 protegge davvero;
- **verbi a mano appesi a un gruppo `MapCrud`** — oggi tre, e ognuno va giustificato nella PR.

La revisione §16.E su tutto il codice di M1 è in `decisions/2026-09-07-m1-checklist.md`: zero tabelle
`*_translations`, un solo authorization handler, zero `fetch` a mano, **zero liste e zero form non
generati** (che M0 non poteva ancora dire: aveva tre eccezioni), quattro componenti custom e sono i
quattro previsti, zero SMTP fuori dal servizio notifiche, zero riferimenti fra moduli. ⚠️ Due voci
della checklist vanno riformulate per M2 e la nota dice come: la domanda sulle FK fra contesti non ha
ancora un caso vero — c'è un solo `DbContext` — e quella sui componenti custom va distinta fra pezzo
condiviso e pezzo di una schermata, che finora si è fatto a memoria.

**Changelog 0.44** (7 set 2026, **deciso da Carmine**): **vIPI entra nell'hub in due tempi**, e la
decisione sta in `decisions/2026-09-07-vipi-dentro-l-hub.md`, scritta dopo aver **misurato i due
repository** invece di ricordarli. Il quadro è migliore di come §9.2 lo lasciava: vIPI è già su
**MariaDB** sullo stesso server (`itivao_atc`, 48 migrazioni dedicate), è già progettata per essere
montata (`AddVipiModule`/`MapVipiModule`, identità dell'host per **mappatura di claim** e non per
codice), e **Blazor Server dietro questo Plesk funziona in produzione da agosto**. L'ostacolo è uno
solo ed è di versioni: un processo ha **una sola** versione di EF Core, l'hub è su EF 9 + Pomelo 9
(Pomelo non ha una build per EF Core 10) e il MariaDB di vIPI vive **solo** sul ramo net8/EF 8/Pomelo
8. La terna che servirebbe — `net10 + EF 9 + Pomelo 9` — è quella che l'hub esercita da nove fasi, e
farla nascere è lavoro **nel repository di vIPI**. Quindi: **oggi il proxy** (`/services/vsop`
inoltrato alla vhost `public_atc` che gira già, più una voce in `cms_menu_items`, che da G8 è una
tabella), **in M5 il montaggio** quando quel ramo esiste ed è provato dalla sua suite. ⚠️ Corretto
anche un refuso interno: §2.3 e §9.5 dicevano **M4**, §13 e §9.2 dicono **M5**, e vale M5.

**Changelog 0.43** (6 set 2026, **correzione di Carmine il giorno stesso**): la proposta scritta in
0.42 — un grant che conferisce un **livello** — è **scartata**, ed è utile dire perché.

Serviva l'opposto. Gli esempi sono «i CH gestiscono i training del TD ma nient'altro» e «il FOD
inserisce le rotte di un evento ma non le postazioni da aprire»: si autorizza qualcuno su **una
capacità precisa**, e un livello è un pacchetto che non si può stringere.

E la buona notizia è che il meccanismo c'è già: un grant è **un permesso più un dipartimento**, che è
il caso d'uso che §6.3 scrive da sempre. Quello che serve non è codice nuovo ma **due regole di
design**, entrambe scritte in `decisions/2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento.md`:

- **La granularità sta nel catalogo del modulo.** Una capacità che ha senso delegare a un altro
  dipartimento **ha un nome suo** (`Training.AssignTrainer` accanto a `Training.Edit`). Ci si accorge
  al design del modulo, non il giorno del grant.
- **E una capacità delegabile è una riga sua, con la sua area.** Lo impone la spina dorsale: la
  guardia dell'interceptor chiede `<Area>.Edit` **per tipo di entità**, quindi permessi per *campo*
  non esistono e non vanno inventati. «Il FOD scrive le rotte ma non le postazioni» significa due
  entità, non due colonne — e da lì la scelta, che è di M2, se quelle righe appartengano all'evento
  (e serva un grant) o al FOD (e non serva).

Resta valida di 0.42 solo la parte del **difetto**: un grant non fa ancora raggiungere il
dipartimento, e la correzione entra in G8. Nessuna fase nuova: `GrantKind.Level` non si costruisce.

**Changelog 0.42** (6 set 2026, **decisioni di Carmine**): due, e la seconda è un difetto trovato
rispondendo alla prima.

- **La dashboard di dipartimento è a blocchi**, non a widget: una riga di `cms_contents` per
  dipartimento (`kind = Dashboard`), seminata da un template di sistema e modificata nell'editor che
  esiste già. Il criterio non è tecnico ma di libertà: **la base e i tool li dà chi costruisce
  l'hub, la gestione è del dipartimento**, e con i widget la seconda metà vorrebbe un secondo editor
  di disposizione. I widget restano ciò che sono, le tile della dashboard **personale** `/me`; un
  modulo che vorrà mettere qualcosa sulla dashboard di un dipartimento registrerà un **blocco Data**.
  Si costruisce dentro **M1/G8** (`decisions/2026-09-05-dashboard-di-dipartimento.md`).
- ⚠️ **§6.3, un grant non fa raggiungere il dipartimento su cui è dato.** Verificato leggendo il
  codice: `HubClaims.BuildIdentity` scrive i claim `dept` **solo dalle posizioni staff**, e su quei
  claim si reggono il global query filter e il filtro di dipartimento di ogni lista. Chi riceve un
  grant su un altro dipartimento apre la riga se ne conosce l'id, ma **la lista gli esce vuota** e le
  righe `Visibility.Department` restano nascoste. Il test di F8 provava il dettaglio e mai la lista.
  La correzione entra in **G8**, perché è ciò che rende vera la visibilità decisa per la dashboard.
  ⚠️ La seconda parte di questa voce — un grant che porta un **livello** — è stata **scartata lo
  stesso giorno**: vedi il changelog 0.43 qui sopra. Serviva l'opposto di un pacchetto.

**Changelog 0.41** (6 set 2026): **G7 di M1 ha costruito i contatti e il servizio notifiche**, e
due decisioni di Carmine cambiano una riga ciascuna di questo piano.

- **§11.4 (GDPR), «dati IVAO minimi (niente email se non serve al modulo)»: adesso serve.** Il
  servizio notifiche è il modulo che ne ha bisogno, quindi l'indirizzo del profilo IVAO — lo scope
  `email` era già chiesto al login e il dato veniva buttato — si conserva in `hub_users.email` **per
  la coda e per nient'altro**. Nessun DTO lo espone, e un test di architettura
  (`NoDtoCarriesAnEmailAddress`) è ciò che lo rende un fatto invece di un'intenzione. La regola non
  cambia: resta «il minimo indispensabile», con un'eccezione che ha un motivo scritto
  (`decisions/2026-09-06-indirizzo-di-un-destinatario.md`).
- **§4.1, `division.json` guadagna `departmentMailboxes`** (facoltativa): la casella condivisa di un
  dipartimento, dove una notifica raggiunge un ufficio invece di una persona. È comportamento della
  divisione, non contenuto: nessuna tabella, nessuna schermata, e chi forka mette le proprie.
- **§16, la spina dorsale guadagna il terzo della famiglia: `ISubmittedByMembers`.**
  `ISharedForReading` allarga la lettura, `CrudOptions.ReadOnlyRows` restringe la scrittura, e questa
  allarga **la sola creazione**: un messaggio di contatto è una riga che qualcuno scrive nello spazio
  di un dipartimento a cui non appartiene, e la guardia dell'interceptor lo rifiuterebbe. Vale solo
  per `EntityState.Added` e solo per i tipi che la dichiarano; muovere quella riga dopo resta una
  scrittura ordinaria. *(0.91, T11a: con un'eccezione — una riga che è anche `IHasStakeholder` la
  modifica il suo interessato, se resta sua e negli stessi dipartimenti: un pilota ritira e corregge
  il proprio PIREP. Nota `decisions/2026-09-23-il-pirep.md` §5.)* M2 (iscrizione a un evento) e M4 (richiesta di esame) sono la stessa forma
  (`decisions/2026-09-06-una-riga-scritta-da-fuori.md`).

Il resto della fase non ha aperto perimetro: la coda, il job Quartz con i tentativi, le preferenze
per VID e il namespace `mail` nei file di lingua erano tutti già scritti in `03-design-m1.md` §5.

**Changelog 0.40** (6 set 2026): **G3 di M1 ha aggiunto i sedici blocchi Content, Layout, Interactive
e Structure** — il registry ne conta 21 — e con il set davanti si chiude **§16.C**, che dal 2 set 2026
aspettava esattamente questo. Le convenzioni stanno in `docs/UI-GUIDELINES.md`: la spaziatura e lo
sfondo sono della **sezione** e mai del blocco, gli sfondi sono quattro (`image` porta un `mediaId`),
le larghezze quattro, una sezione `locked` mostra i campi e non la struttura, un blocco sconosciuto
lo vede solo lo staff, ogni blocco dichiara la propria icona, nessun blocco contiene blocchi, e i
riquadri (`video`, `embed`) puntano solo a host di una **allowlist** che ricostruisce l'indirizzo del
player invece di rimandare indietro quello scritto.

Tre cose che la fase ha deciso scrivendo, tutte e tre già dentro le regole:

- **`BlockDocumentWalker` impara gli sfondi.** Erano l'unico insieme chiuso dell'envelope che il
  server non controllava; un valore che nessuno rifiuta il renderer lo legge come «nessuno sfondo».
  È la stessa coppia di `Layouts` e `RenderModes` — TypeScript da una parte, C# dall'altra, un test
  di integrazione che posta un valore sconosciuto a tenerle d'accordo.
- **L'`alt` di un'immagine non si eredita dalla libreria** (nota
  `decisions/2026-09-06-alt-delle-immagini.md`): vuoto significa decorativa. L'eredità richiederebbe
  o che il server legga dentro `props` — vietato da §16.5 — o un endpoint pubblico dei metadati, che
  sarebbe il secondo endpoint scritto a mano di M1 per una riga di design.
- **Il generatore di form salva solo ciò che è stato scritto** (`writtenValues`) e fa nascere una voce
  di lista con i campi già controllati (`blankEntry`). Nessuna delle due è una comodità: una props
  tradotta opzionale lasciata vuota viaggerebbe come `{ en: "", it: "" }` e la pubblicazione — che il
  corpo lo legge senza sapere cosa sia un blocco — la leggerebbe come una traduzione a metà,
  rifiutando la pagina. La regola sul server **non** si è indebolita.

E una precisazione alla convenzione del changelog 0.39: il nome con cui un blocco nomina un file è
**`mediaId`**, a qualunque profondità — un blocco che ne mostra molti tiene una lista di **oggetti**
con dentro `mediaId`, perché il generatore disegna liste di oggetti. `mediaIds[]` resta capito dalla
stessa query per i corpi già scritti, e non lo scrive più nessuno. È anche il motivo per cui lo
sfondo `image` di una sezione porta `mediaId` e non `backgroundMediaId`: con l'altro nome,
`JsonQuery.UsingMedia` non avrebbe visto che quella pagina usa quel file.

**Changelog 0.39** (5 set 2026): la media library (G1 di M1) è stata costruita **senza scrivere un
caso speciale**, e per riuscirci `MapCrud` ha imparato tre cose. Sono estensioni, non eccezioni —
regola (b) di §16.E — e stanno in §16.6 perché quello è il posto dove si legge che cos'è il motore.

- **`CrudOptions.MapCreate`**: una risorsa può dichiarare di non avere una create JSON. Era già
  previsto nel changelog 0.37 per l'upload multipart, ed è la forma con cui è stato fatto: una riga
  `cms_media` senza file su disco non deve poter esistere, e un secondo indirizzo per «creare una
  media» sarebbe stato il secondo modo di fare la stessa cosa.
- **`CrudOptions.Delete`**: che cosa significa cancellare, per questa risorsa. Il motore chiama
  quello invece di `Remove` e **salva lo stesso**, quindi audit, guardia di dipartimento e proiezioni
  restano quelle di una scrittura qualsiasi. Serviva perché cancellare una media prende il file e non
  la riga: la riga è ciò che una pagina già pubblicata nomina.
- **`CrudOptions.CustomFilters`**: un `filter[nome]` che non è un'uguaglianza su una colonna. «Quali
  pagine usano questo file?» si legge dentro un `body_json`, non in una colonna, e la risposta la dà
  la lista dei contenuti — la risorsa che possiede il dato — invece di un endpoint nuovo. Allarga
  l'unico confronto che il filtro sapeva fare, che era un limite scritto fra i debiti di M0.

Accanto a loro nasce **`Core/Data/JsonQuery.cs`**, accanto a `FullTextSearch`: «questo documento JSON
nomina questo id?», come funzione mappata sul modello, nessuna tabella e nessuna migrazione. ⚠️ Poggia
su una convenzione — un blocco nomina un file in `mediaId` o in `mediaIds` — che è ora scritta in
`docs/UI-GUIDELINES.md`, perché è l'unica cosa su cui il server e gli schemi in TypeScript devono
mettersi d'accordo **per nome**: §16.5 resta intatta, il backend continua a non leggere una `props`.

**Changelog 0.38** (5 set 2026): due decisioni di Carmine dopo la prima fase di M1, e la prima
delle due è nata **facendo** il giro in un browser invece che leggendolo.

**I template sono strumenti di dipartimento, e ogni staff li legge tutti** (§9.3). G0 ha mostrato che
i tre template seminati appartengono a WD e che `Content.View` è di dipartimento: per un coordinatore
di qualunque altro dipartimento «Nuovo da template» non esisteva affatto, e — peggio — una pagina nata
da un template che il suo editore non può leggere perde nell'editor i vincoli di quel template. Ogni
dipartimento si fa i propri template e li modifica con il `Content.ManageTemplates` che ha già sul
proprio; la lettura diventa comune, la scrittura resta dove era. Usare il template di un altro crea
una pagina **nel proprio** dipartimento; copiarlo — una copia che diventa tua — è il modo di
divergere e si costruisce quando serve. Nota:
`decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`; lavoro in G5 del piano di M1.

**Ogni dipartimento nasce con una dashboard** (§8.2), che poi modifica. Non era in nessun documento:
il piano conosceva `/staff/{dept}/**` come «spazio del dipartimento» senza dire che cosa si vedesse
arrivandoci, e `/me` è la dashboard di una **persona**. La forma raccomandata la scrive
`decisions/2026-09-05-dashboard-di-dipartimento.md` — una riga di `cms_contents` per dipartimento,
seminata da un template e modificata nell'editor che esiste, invece di un secondo modo di comporre
una schermata — ed è **da confermare** prima di G8, che è dove entra.

**Changelog 0.37** (5 set 2026): **M1 ha il suo piano di implementazione**,
`04-piano-implementazione-m1.md` v1.1 — tredici fasi G0–G12, una per sessione, con i prompt di apertura
e i rischi, come `02-` per M0. §13 lo nomina accanto al design nella riga M1.

Scriverlo ha richiesto **tre decisioni** che il design lasciava a chi implementa, prese da Carmine lo
stesso giorno. Le prime due non toccano questo piano e vivono nella fase che le riguarda: le dimensioni
di un'immagine le legge un **parser di header** per PNG/JPEG/WebP in un helper del nucleo, invece di una
dipendenza (`ImageSharp` ha una licenza da verificare, `SkiaSharp` porta asset nativi dentro un pacchetto
self-contained); e l'upload multipart convive con `MapCrud` **estendendo `CrudOptions`** perché una
risorsa possa non mappare la create — non spostando l'upload su un secondo indirizzo, che sarebbe il
secondo modo di creare una media (§16.2). La terza ha corretto una **contraddizione dentro il design di
M1**, che è passato a v1.1: §5.2 nominava la tabella delle preferenze di notifica e §10.2 non la
contava. La forma decisa è `hub_notification_preferences` (`Vid`, `Type`, `Enabled`) e non una colonna
di `hub_users`, perché al secondo tipo di notifica la colonna costerebbe una migrazione; le tabelle
nuove di M1 sono **sei**, non cinque.

**Changelog 0.36** (5 set 2026): **M1 ha il suo documento di design**, `03-design-m1.md`, come
§13 chiede per ogni milestone. Il piano cambia in tre punti, e tutti e tre nascono da una decisione
presa aprendo M1 invece che scoprendola a metà.

**Lo staging Plesk esce da M1 ed entra in M2** (§13). Era in M1 dal 2 set 2026, quando M0 lo aveva
ceduto in attesa delle risposte A9 di Ivao.It (§15.2c); quelle risposte al 5 set non ci sono ancora, e
una milestone non si progetta intorno a una risposta che non è arrivata. Non si perde niente di
concreto: la CI produce l'artefatto `publish/` da M0, quindi quello che si sposta è **il deploy**, non
la capacità di pacchettizzare. §15.2c resta aperta e ora blocca M2.

**La migrazione dei contenuti dal Blazor è manuale** (§13): si ricopia dall'editor, nessun import.
Un mapper da un modello che non conosciamo verso l'envelope a blocchi sarebbe codice usato una volta
sola, e ricopiare `/about` a mano è il collaudo vero dell'editor — se è faticoso, l'editor non è finito.

**L'URL dei documenti è `/documents`, non `/docs`** (§8.2, §9.4). Non è una preferenza: `ContentEntry.Url`
lo decide già così ed è l'unico punto che dice dove il pubblico legge una riga e cosa finisce in
`search_index`. L'entità si chiama `Document`, il `kind` si chiama `document`: tre parole uguali e un
URL diverso sono una cosa in più da ricordare.

Il resto delle decisioni di M1 — 22 blocchi nuovi, le convenzioni dei blocchi che chiudono §16.C,
il menu editoriale, il vocabolario delle categorie che chiude §15.8 senza doverne conoscere il
contenuto — vive nel design e non duplica il piano: sono dettagli di una milestone, non architettura.

**Changelog 0.35** (5 set 2026): §4.2 guadagna una regola, accanto a quella che il progetto
applica dal primo giorno. Il piano chiede da sempre **«questo nomina l'Italia?»** — nessun codice
ICAO, nome FIR, posizione staff o URL italiano nel codice. La regola nuova è la stessa domanda un
livello sopra: **«questo nomina IVAO?»**, e se la risposta è sì il codice sta dentro il perimetro
che già esiste — `Core/Ivao/`, la metà IVAO di `Core/Auth/`, le tabelle `ref_ivao_*` e l'enum
`Department` — e da nessun'altra parte.

Non è un meccanismo nuovo e non introduce un'astrazione: è una domanda da farsi mentre si scrive.
Un `IIdentityProvider` o un `Department` configurabile oggi sarebbero codice speculativo che
peggiora questo prodotto — l'enum compra sicurezza a compile time — e per §16.E richiederebbero
comunque una nota di decisione prima. La regola è gratis, l'astrazione no.

Il perimetro è stato **misurato**, non stimato, leggendo tutti i 119 file `.cs` e i 117 `.ts`/`.tsx`
a `369851a`: tolto il namespace `IvaoHub.*`, i file che nominano davvero IVAO sono **una ventina su
119**, e **13 tabelle su 15** non sanno cosa sia. La spina dorsale di §16 — contratti di dominio,
motore CRUD, modello editoriale, `Localized<T>`, sistema a moduli, audit, grant, permessi, i18n —
ne è già completamente libera. La regola serve a tenerla tale mentre M1 e i moduli di dipartimento
aggiungono molte volte il volume attuale sopra: ogni riferimento che sfugge adesso si paga a mano
dopo.

Il perimetro è stato anche **verificato**, non solo dichiarato: `IIvaoApiClient` risulta usato
soltanto dentro `Core/Ivao/`, e i due soli punti fuori perimetro che importano quel namespace sono
`HubDbContext` (i `DbSet` dei dati `ref_`) e la composition root di `IvaoHub.Web` — entrambi
inevitabili e registrati come tali in §4.2, perché una regola che dichiara fuori posto due file che
stanno al posto giusto è una regola che si impara a ignorare.

La rete che esiste — il test della divisione fittizia «XX» — prende l'Italia, non IVAO. Se un
giorno il perimetro va reso meccanico, la via a buon mercato è un test di architettura che verifica
che nulla fuori da `Core/Ivao/` e `Core/Auth/Ivao*` importi il client IVAO. **Non è deciso**, e per
§16.E vorrebbe una nota di decisione: resta scritto qui perché il giorno che servirà non si debba
ricominciare a cercare da dove.

**Changelog 0.34** (4 set 2026, terzo hotfix): **ogni schermata di `/staff` era disegnata in una
colonna larga 255 pixel**, in alto a sinistra, con il resto della finestra vuoto, la tabella tagliata
e il bottone «Close sidebar» ripetuto due volte. `StaffLayout` avvolgeva `Sidebar` in un
`SidebarProvider` e un `SidebarContainer` propri; ma **`Sidebar` è già completo** — porta i suoi — e
**`SidebarContainer` non è un guscio a due colonne: è l'`<aside>`**, largo `w-72`. Sidebar e `<main>`
finivano quindi impilati dentro un aside da 288 px. Misurato prima: `main` a `x=16, width=255`; dopo:
`x=288, width=992`.

**Terza volta in un giorno che un contratto di Atmosphere è stato assunto invece che verificato in un
browser** (dopo `DarkModeToggle`, che scarta i `children` e vuole `title` e non `aria-label`, e il
`Select` che spande le props due volte). Il piano lo registra perché è diventato un modello: le firme
TypeScript di quella libreria non descrivono come i suoi componenti vanno **composti**, e la
composizione è esattamente ciò su cui questo progetto poggia.

La differenza rispetto agli altri due hotfix, e la ragione della rete nuova: qui **funzionava tutto**.
Tutte le parole c'erano, nell'ordine giusto, cliccabili — gli otto smoke passavano su un back-office
inutilizzabile. Un test che chiede «c'è?» non chiede «dov'è?». Il nono smoke misura quindi la
**geometria** (`main.x > 200`, `main.width > 600`, un solo «Close sidebar»), ed è verificato
rimettendo il layout vecchio: fallisce con `Received: 16`.

Il difetto è emerso da una **verifica visiva guidata**: schermate reali catturate in Chromium con una
sessione staff finta, e guardate. Tre delle cinque cose che sembravano difetti erano invece artefatti
della fixture o comportamenti voluti (un valore d'enum inesistente, la doppia data che è UTC più il
fuso della divisione, gli smoke rossi per un server di prova senza fallback SPA): **prima di chiamare
difetto qualcosa, si controlla se è la fixture.** Restano due cose viste e non corrette perché
richiedono una decisione, scritte in HANDOFF §13: l'intestazione di colonna che riusa la chiave
dell'etichetta del form, e i campi tradotti più stretti degli altri.

Test: 353 .NET, 76 Vitest, **9 Playwright**. HANDOFF §13.

**Changelog 0.33** (4 set 2026, secondo hotfix): **nessun form del back-office era raggiungibile in
un browser.** Le tre route di dettaglio — `links`, `content`, `admin/permissions` — erano **figlie**
delle rispettive liste, e in TanStack un figlio si disegna dentro l'`Outlet` del padre: nessun
componente di lista ne rendeva uno. Cliccando «nuovo link» l'indirizzo cambiava, non partiva nessuna
chiamata, non veniva lanciata nessuna eccezione, e la lista restava sullo schermo. Non si poteva
creare o modificare un link, aprire l'editor di una pagina, né toccare un grant: la metà «form» di
F6 e F7 non era mai stata raggiunta.

**È lo stesso difetto del changelog 0.32 visto una seconda volta**, e questa è la ragione per cui
merita una riga nel piano e non solo una nota: lì era la composizione dei **provider**, qui è la
composizione delle **route**. In entrambi i casi ogni pezzo preso da solo era corretto — verificato
uno per uno durante la diagnosi: il router costruisce l'href giusto, `Button asChild` produce un
vero `<a href>`, la `parse` del padre non perde `id`. Tre ipotesi, tutte plausibili, tutte false. Il
guasto non era in nessun pezzo, ed è esattamente il punto cieco che questo progetto ha per
costruzione, perché tutto ciò che fa è comporre pezzi generici.

**Una decisione**, `docs/internal/decisions/2026-09-04-rotte-di-dettaglio.md`: una lista e il suo
dettaglio sono **tre** route — un layout che possiede la guardia e rende l'`Outlet`, un `index` con
i search params e il loader, un dettaglio fratello. Scartata l'alternativa di un `<Outlet />` dentro
ogni lista: farebbe comparire il form sotto la tabella, che è un layout master-detail che nessuno ha
progettato, e soprattutto lascerebbe in piedi la struttura in cui il difetto è possibile.

**Design §7.3 corretta**, ed è la lezione che vale più della correzione: la ricetta 2 era scritta in
un file solo, cioè **nella forma sbagliata**, ed è stata copiata tre volte fedelmente da chi faceva
esattamente ciò che il progetto chiede. **Una ricetta che si copia è un moltiplicatore**: giusta fa
risparmiare tre volte, sbagliata replica il difetto tre volte e nessuno lo rimette in discussione,
perché copiarla *è* la procedura. Una ricetta nuova va provata in un browser prima di diventare il
quarto esemplare.

Rete: `web/e2e/back-office.spec.ts`, quattro smoke con una sessione staff finta — un coordinatore
con un solo dipartimento, non un superadmin, perché solo il primo esercita la guardia. Verificati
togliendo l'`Outlet`: tre su quattro falliscono. Test: 353 .NET, 76 Vitest, **7 Playwright**. Design
a 2.2, HANDOFF §12.

**Changelog 0.32** (4 set 2026, dopo il tag): **`v0.1.0-m0` puntava a un'applicazione che non si
apriva.** `DarkModeToggle` di Atmosphere si avvolge da sé in un `Tooltip` di Radix, un tooltip senza
`TooltipProvider` **lancia** invece di degradare, e `main.tsx` non ne montava uno. Poiché quel
componente sta in `Chrome.tsx`, cioè nel frame di tutti e tre i layout, **ogni schermata dietro un
layout era morta**. Corretto dentro lo stesso tag, rifatto sul commit dell'hotfix.

La parte che vale la pena scrivere nel piano non è il difetto: è **perché 353 test .NET e 74 Vitest
erano verdi**, e lo erano legittimamente. Il guasto non stava in un componente ma **nell'albero**, e
niente montava l'albero: l'harness dei test dà a un pezzo per volta il minimo che gli serve, nessun
test montava `Chrome`, e nessuno montava i provider dell'applicazione — tanto che `ThemeProvider`, la
prima volta che è stato montato in un test, ha chiesto un `window.matchMedia` che jsdom non ha e che
in nove fasi nessuno aveva mai dovuto stubare. **Il punto cieco stava esattamente dove il sistema fa
la sua scommessa più grossa**, cioè che una schermata sia composizione di pezzi generici.

**Una decisione**, in `docs/internal/decisions/2026-09-04-smoke-in-un-browser.md`: lo smoke in un
browser diventa **bloccante in CI** e non aspetta M1. Il design §8 lo dichiarava «solo `pnpm e2e`,
non bloccante in M0»; il costo di quel timore è stato misurato ed è un tag di release su
un'applicazione che non parte. Uno smoke che non può fermare una release non è una rete, è un
rapporto. Restano tre reti nuove, tutte **verificate togliendo la correzione** — un test di
regressione che passa in entrambi i casi non è un test: `HubProviders` (l'albero dei provider è un
componente, montato sia dall'applicazione sia dal test, perché la prima stesura del test elencava i
provider per conto suo e sarebbe rimasta verde con l'applicazione rotta), `Chrome.test.tsx`, e tre
smoke Playwright su Chromium contro il bundle di produzione. Resta scoperta, e scritta come debito,
la metà con l'API vera e una pagina pubblicata da un seed.

Trovate di rimbalzo due cose già corrette: il tooltip del selettore di tema mostrava l'inglese di
Atmosphere perché riceveva `aria-label` ma non `title` — **terza stringa non tradotta in due giorni
che sopravvive perché si vede solo passandoci sopra** — e i tipi di Atmosphere pretendono `children`
su `DarkModeToggle` mentre il runtime li scarta, quindi la nostra icona era markup morto da F6.
Test: 353 .NET, **76 Vitest**, **3 Playwright**. Design a 2.1, HANDOFF §11.

**Changelog 0.31** (4 set 2026): chiusa la fase **F9**, e con lei **M0**. Nessun meccanismo nuovo:
F9 è la fase che verifica invece di costruire, e quello che ha prodotto è la prova che le altre
otto hanno fatto quello che dicevano.

La **revisione §16.E su tutto il codice di M0** (`docs/internal/decisions/2026-09-04-m0-review.md`)
ha letto le undici domande del template di PR una per una contro 119 file `.cs`, 110 `.ts`/`.tsx` e
40 file di test. Il risultato in breve: zero tabelle `*_translations`, un solo authorization handler,
zero `fetch` a mano, zero componenti fuori dall'elenco chiuso, zero FK fra contesti, zero SMTP, zero
riferimenti fra moduli, zero `ExecuteDelete`, `IgnoreQueryFilters` nei due soli posti che l'allow-list
prevede, migrazioni additive (i `Drop` sono tutti dentro `Down()`), nessun `TODO` in tutto il
repository, e tutte e dodici le decisioni di M0 citate dai documenti. Le eccezioni sono **tre
schermate che non passano dal motore lista+form** — `/staff/admin/modules`, l'elenco dei VID di
`SuperadminPanel`, e il dettaglio di `/staff/admin/audit` che semplicemente non esiste — e tutte e
tre per la stessa ragione, che vale la pena avere scritta: **dietro non c'è una risorsa paginata**.
`DataList` è il motore di una lista servita dal server; dove la risposta è l'elenco del bootstrap o
un `IReadOnlyList<int>`, usarlo avrebbe voluto dire inventare un endpoint per farlo funzionare. Se
M1 si trovasse con la quarta, la domanda da farsi non è «uso `DataList`?» ma «questa risposta doveva
essere una risorsa?».

La revisione ha trovato **tre stringhe visibili all'utente nel codice**, tutte corrette dentro la
fase (regola (a), ed è l'unica modifica al codice di produzione che F9 contiene): un
`aria-label="breadcrumb"` in `PageShell` — sopravvissuto tanto a lungo proprio perché **non si
vede**, lo legge solo uno screen reader, e lo faceva in inglese a un lettore italiano su ogni pagina
di `/staff` — un `placeholder="my-new-page"` nel selettore di template, e il dominio
`it.ivao.aero` dentro il messaggio con cui l'applicazione si rifiuta di partire in produzione senza
`AllowedHosts`. Quest'ultimo non è una chiave i18n e resta in inglese di proposito, ma nominava
**questa** divisione dentro `src/`, che è la regola di forkabilità di §4: dopo la correzione `src/`
non contiene più il dominio della divisione da nessuna parte. `ForkabilityXxDivisionTests` non poteva
prenderlo, perché controlla le risposte HTTP e quel messaggio non ne è una — il che è il limite di
quel test, e vale la pena saperlo.

**`tools/demo-m0.md`** (in inglese, come tutta la documentazione pubblica) è la demo end-to-end che
§16.15 chiede: da una cartella vuota a una pagina pubblicata, in sette parti, con la checklist
«definizione di fatto» del design §0.1 spuntata punto per punto e, sotto ogni parte, il nome del test
che asserisce la stessa proprietà. Non è uno script: uno script che passa dice che lo script passa.
**`docs/FORKING.md`** guadagna i passi reali di un fork — i sei comandi e le sei cose da fare dopo,
`division.json`, `ivao-oauth.json`, `locales/`, il primo login, i template seedati e le pagine — e la
frase che riassume tutto: nessuno di quei passi è la modifica di un file sorgente.

**Playwright non è entrato in M0**, deciso da Carmine all'apertura di F9: non è fra i cinque task
della fase, il design §8 lo dichiara «solo `pnpm e2e`, non bloccante», e la demo che il piano chiede
è quella da eseguire a mano. Resta la prima voce del backlog di M1.

Stato finale di M0: **353 test .NET** (253 unit + 100 di integrazione su MariaDB 11.4.10 vera) e
**74 Vitest**, nessuno skippato; sedici tabelle, quattro migrazioni additive, un modulo, tre
schermate di amministrazione, e un pacchetto self-contained che si scompatta come applicazione.

**Changelog 0.30** (4 set 2026): chiusa la fase **F8**. Il nucleo compone i moduli e non ne nomina
nessuno: `IModule` (con `ModuleBase`, che rende opzionale tutto tranne la chiave), `ModuleRegistry`
alimentato dai due elenchi espliciti — `IvaoHub.Web/Modules.cs` e `web/src/modules/index.ts` — e
`IvaoHub.Modules.Atc` come primo modulo vero, che porta una voce di menu, quattro esclusioni dal
fallback della SPA, un endpoint e una rotta React con il proprio namespace i18n. Le esclusioni
cablate di F0 non ci sono più: le compone il registry.

**Una decisione**, scritta in `docs/internal/decisions/2026-09-04-grant-e-sessione.md` e presa con
Carmine: un grant rigenera lo `security_stamp` del suo titolare attraverso un'**interfaccia
sull'entità** (`IAffectsUserSession`, applicata dall'interceptor nella stessa transazione) e non
attraverso un secondo gancio di `MapCrud`. La ragione è la stessa che ha messo la guardia di
scrittura nell'interceptor: un gancio sul motore CRUD è dimenticabile, un'interfaccia sull'entità
no. Effetto reale, scritto per non farsi illusioni: il cookie vecchio viene **rifiutato** alla
richiesta successiva (401), non riscritto, e chi ha già dato il consenso a IVAO rifà il login in
silenzio.

**Il catalogo dei permessi diventa composto** (`PermissionCatalog` = nucleo ∪ moduli abilitati): lo
interrogano il policy provider, il calcolatore dei permessi effettivi e il validatore di un grant,
perché un permesso di modulo dev'essere un permesso ovunque o in nessun posto.

**`MapCrud` in modalità globale ha finalmente tre usi veri**: `/api/admin/grants`, `/api/admin/audit`
in sola lettura, e — appena fuori dal motore — `/api/admin/superadmins`. La schermata dei grant
offre i permessi che l'installazione ha davvero, perché `/api/me` pubblica il catalogo; e il
generatore di form ha imparato la quinta annotazione, `.meta({ choices })` **su una stringa**, per
un insieme che si conosce solo a runtime.

**`GET /api/search`** legge il FULLTEXT di `cms_search_index` (helper unico in `Core/Data/`,
`EF.Functions.Match`), attraverso lo stesso query filter di tutto il resto: nessun endpoint decide
chi vede che cosa. Solo l'endpoint: la schermata è M1.

**La manutenzione** chiude un modulo alle scritture e lascia passare le letture, prima del routing,
con la riga di audit scritta dall'interceptor (`DivisionSetting` è ora `[Audited]`).

**`ForkabilityXxDivision`** avvia un'installazione della divisione fittizia XX su un database
proprio, con `config/division.xx.json`, una sola lingua e i suoi seed: la catena di migrazioni gira
da zero e nessuna risposta nomina l'Italia. È il test che rende la forkabilità un fatto invece che
un'intenzione.

**Changelog 0.29** (4 set 2026, sera): giro sui debiti che F7 aveva scoperto, prima del merge.

**La domanda aperta di 0.28 è decisa**, con l'opzione raccomandata: una cattura `frozen` non può
essere più visibile della pagina che la contiene. La pubblicazione dice al provider dove finirà la
risposta (`DataBlockContext`), e il provider si ferma a ciò che quella pagina può mostrare; il
tetto è una **tabella** in `VisibilityCeiling`, non un ordinamento, perché `Department` è più
stretta di `Staff` ma nomina persone diverse per ogni dipartimento. Non è una seconda copia del
query filter: quello risponde «questo lettore può vedere questa riga», questo risponde «questa riga
può essere copiata dentro una pagina che leggerà qualcun altro», ed esiste solo perché la
pubblicazione copia. Nota `2026-09-04-frozen-e-visibilita.md` aggiornata a **decisa**.

**Il generatore di form ha imparato tre cose**, tutte regola (b), tutte perché un blocco le
chiedeva: legge il `.default()` che un campo dichiara (così «con cosa nasce un blocco nuovo» sta
accanto al campo e non in un secondo posto); disegna una **select** per un numero annotato
`.meta({ choices })` — un numero e non un `z.enum`, perché ogni stringa dentro le `props` finisce
nell'indice di ricerca come testo della pagina e il livello di un titolo non è testo; e dà a una
`z.enum` **opzionale** la voce «nessuno», senza la quale una select non ha modo di tornare indietro
e la prima scelta sarebbe definitiva. Il `department` di un `linkList` smette così di essere testo
libero, e un nome che il server non riconosce restringe a **nessuna riga** invece che a tutte.

E l'anteprima dell'editor dice quello che sta facendo: una bozza non porta nessuna cattura — la
pubblicazione la scrive nella versione — quindi un blocco `frozen` in anteprima mostra dati live, e
il badge distingue «catturato alla pubblicazione» da «ora dal vivo, catturato quando pubblichi».

**Changelog 0.28** (4 set 2026): chiusa la fase **F7**, i contenuti. È la fase che dimostra §9.3 per intero: una riga di `cms_contents` nata da un template, modificata in un editor a lista, pubblicata, e letta da un anonimo — con un blocco Data catturato alla pubblicazione che **non** cambia quando cambiano i link sotto, e che torna a cambiare appena lo si rimette `live` e si ripubblica. Il test `ContentPublishFreezesDataBlocks` è quella demo, eseguita invece che descritta.

Il registry dei blocchi esiste ora su tutti e due i lati: cinque descrittori nel nucleo (`heading`, `text`, `callout`, `cta`, `linkList`) pubblicati da `/api/me`, e cinque registrazioni TypeScript con schema, componente ed esempio. Il backend continua a non sapere che cosa significhi una `props`: valida l'envelope, estrae il testo per la ricerca, e passa le proprietà opache al provider. `LinkListProvider` è il primo `IDataBlockProvider`, e risponde sia alla pubblicazione sia a `GET /api/blocks/data/{type}` — stesso servizio, stesse regole di visibilità, momenti diversi.

**Una decisione** e **una domanda aperta**, entrambe scritte in `docs/internal/decisions/`. La decisione: «nuovo da template» è `POST /api/content/from-template/{templateId}` e non una query su `POST /api/content`, perché quella rotta è già la creazione generata da `MapCrud` e le minimal API non instradano per query string; l'alternativa sarebbe stata insegnare al motore CRUD che cosa sia un template, cioè il caso speciale che §16.6 vieta. La domanda aperta: **la cattura `frozen` vede quello che vede chi pubblica**, che è ciò che §9.3 e il design §5.5 dicono, ma significa che un coordinatore che pubblica una pagina pubblica può congelarci dentro righe `Staff`. Il percorso `live` è corretto per costruzione. Tre opzioni e una raccomandazione in `2026-09-04-frozen-e-visibilita.md`.

**Due precisazioni al design** (v1.7), tutte e due estensioni di meccanismi esistenti e non meccanismi nuovi. (1) `CrudOptions.DefaultFilters`: un filtro che la lista applica finché il chiamante non nomina quella proprietà — così la lista dei contenuti nasconde i template senza che il motore sappia che cosa sia un template. (2) `CrudSource.BackOffice<T>`: la lettura con i filtri di visibilità spenti diventa un metodo con un nome dentro `Data/Crud/`, che il servizio di pubblicazione può chiedere invece di scrivere `IgnoreQueryFilters` per conto suo; il test di architettura resta identico.

E una cosa che i test hanno trovato da soli: il seed dei template scriveva **con l'identità di chi stava usando il sito**, perché il doppione di `ICurrentUser` dei test di integrazione valeva anche durante l'avvio dell'applicazione. In produzione non succede — fuori da una richiesta non c'è nessun autenticato, ed è proprio su questo che la guardia di scrittura dell'interceptor conta — ma il doppione mentiva, e ora è anonimo finché `ApplicationStarted` non è passato.

**Changelog 0.27** (3 set 2026, notte): chiusa la fase **F6**, la spina dorsale del frontend. Tre layout dietro le loro guardie (`_public`, `_member`, `_staff`), le tre ricette del router copiate e documentate in `web/src/routes/README.md`, `DataList` e `SchemaForm`, `LocaleFields`, `useProblemDetails`, i componenti dell'elenco chiuso di §16.C, `/staff/admin/ui-kit`, e il back-office di `links` — che è il punto di tutto: **nessuna riga di JSX di tabella o di form**, solo un elenco di colonne e uno schema zod. `docs/UI-GUIDELINES.md` scritta.

**Una decisione**, presa da Carmine: **`react-markdown`** per `MarkdownContent`. Il design lo elencava fra i componenti di M0 ma §0.3 non pinnava nessun renderer di Markdown; scriverne uno a mano sarebbe stato codice da buttare appena i contenuti veri fossero arrivati in F7, e rimandare il componente avrebbe aperto F7 con la ui-kit già incompleta. Costruisce un albero React e non tocca mai `innerHTML`, l'HTML grezzo non è abilitato, e finisce in un chunk suo. Nota in `docs/internal/decisions/2026-09-03-markdown-content.md`.

**Tre precisazioni al design** (v1.5), tutte nella direzione «il design abbozzava, l'implementazione ha misurato». (1) **`DataList` prende `search` e `onSearchChange`** invece dell'oggetto `route`: un componente generico che entrasse nel router dovrebbe riallargare i search params a `unknown`, cioè buttare via esattamente la tipizzazione per cui la ricetta 2 esiste. Le due righe di collegamento stanno nel file di route, dove i tipi ci sono. (2) **Il bootstrap dichiara `hasAllDepartments`**: la sidebar staff deve elencare tutti i dipartimenti a un director, e la regola già scritta vieta di dedurlo dalla forma della lista dei permessi — è il claim `alldept`, e ora viaggia anche verso il client. (3) **`HubPolicies.SignedIn`**, l'unica policy che non è un permesso del catalogo: serve al nuovo `PUT /api/me/locale`, che chiede soltanto di essere autenticati. C'è posto per esattamente una policy di questo tipo.

Due cose in più che F6 ha sistemato mentre passava. Il **debito del chunk JS oltre i 500 kB** è chiuso: il router splitta le schermate e `manualChunks` separa React, Atmosphere e il renderer Markdown, così il pezzo più grosso è 422 kB e nessuna pagina paga per quello che non usa. E **`pnpm i18n:check` ora controlla anche il codice**: ogni chiave scritta come stringa letterale in `src/` deve esistere in tutte le lingue della divisione, non solo le lingue essere allineate fra loro.

**Changelog 0.26** (3 set 2026, sera): chiusa la fase **F5** (PR #8) e **confermate le due note lasciate aperte da quella fase**, con le correzioni portate nel design (v1.4).

(1) **L'OpenAPI a build-time esegue davvero l'applicazione.** §7.4 e §9 punto 12 dicevano «senza avviare l'app»: è falso. `Microsoft.Extensions.ApiDescription.Server` invoca il nostro `Program` per riflessione e lo lascia arrivare fino ad `app.Run()`, perché è lì che gli endpoint minimal API esistono — sono registrati **dopo** `builder.Build()`, e la prova è secca: con `app.Run()` saltata il documento usciva con `"paths": { }`. Ciò che è vero e che conta è l'altra metà, cioè **senza database e senza client OAuth**, e a garantirla è `HubConfiguration.IsOpenApiDocumentGeneration`, che riconosce il processo dal nome dell'assembly d'ingresso e gli toglie da davanti l'irrigidimento di Production, la validazione OAuth e `InitializeAsync`. Il riconoscimento è volutamente specifico: sotto `WebApplicationFactory` l'assembly d'ingresso è l'host dei test, quindi il flag resta falso e i test di integrazione continuano a migrare e servire per davvero. **Questa è anche la ragione per cui la revisione di 0.25 ha dovuto esentare i proxy fidati**: la stessa guardia serviva a `ForwardedHeaders:TrustedNetworks`, e senza di essa ogni build falliva su un'impostazione obbligatoria in produzione mentre scriveva un file JSON.

(2) **Un campo `Localized<T>?` non valorizzato esce `null`, non `{}`.** §3.1 va letta su due piani distinti, e confonderli costava un 500 sul primo `GET` di un link senza descrizione: una **lingua** che manca dentro un `Localized<T>` torna vuota e mai null (regola di F4, intatta, così nessun chiamante distingue fra «assente» e «vuota»); un **campo dichiarato** `Localized<T>?` e mai scritto torna `null`, che è quello che lo schema OpenAPI generato già dichiara. Rendere `{}` anche il secondo caso mentirebbe al client generato e renderebbe indistinguibili «nessuno ha mai scritto una descrizione» e «la descrizione è stata svuotata» — che è esattamente la ragione per cui la colonna è nullable a database.

Note in `docs/internal/decisions/2026-09-03-openapi-a-build-time.md` e `2026-09-03-localized-nullable-nelle-api.md`, entrambe passate da «da confermare» a **confermata**.

**Changelog 0.25** (3 set 2026, sera): **revisione senior di tutto il repository** prima di aprire F5, letto come se fosse di altri. Ha prodotto tre decisioni, tutte con nota in `docs/internal/decisions/`.

(1) **`HasAllDepartments` è un fatto del ruolo, non una forma della lista dei permessi.** Il design §3.3 lo definiva già per ruolo (Director, Web, superadmin) ma l'implementazione lo **deduceva** da «esiste un permesso non-globale con dipartimento `null`», e quella deduzione sbagliava in tutte e due le direzioni: dava «vede ogni dipartimento» a una posizione IVAO HQ (che tiene `Content.View` senza dipartimento perché legge, e così scavalcava l'intero filtro di visibilità) e lo **toglieva** a un Director colpito da un deny, perché l'espansione del deny consuma proprio le entrate `null` da cui la deduzione leggeva — un deny su un dipartimento gliene chiudeva sette, e in F5 sarebbe stato un **403** su ogni lista (design §3.9). Ora è il claim `alldept`, scritto da `HubClaims.BuildIdentity` dalle posizioni. Conseguenza voluta: una posizione HQ non raggiunge più ogni dipartimento — la lettura **più restrittiva** delle due, coerente con la convenzione «scegliere sempre l'opzione più stretta, così una correzione può solo allargare». Design §3.3 precisata.

(2) **I proxy di cui si crede `X-Forwarded-For` si dichiarano**, in CIDR, e in Production sono obbligatori (`ForwardedHeaders:TrustedNetworks`). Svuotare `KnownNetworks` e `KnownProxies` senza rimpiazzarle, che è ciò che si faceva, non vuol dire «fidati di Cloudflare» ma «fidati di chiunque»: il rate limiter di `/auth/*` si aggirava cambiando un header a ogni richiesta, e la colonna `ip` di `hub_audit_log` (§7, che questa revisione smette di lasciare vuota) l'avrebbe scelta chi scriveva. Con lo schema finalmente affidabile si aggiungono anche **HSTS** (30 giorni, senza `includeSubDomains` né preload: l'hub è un host sotto un dominio condiviso, e una policy HSTS è reversibile solo quanto il suo `max-age`) e la **redirezione a https**, entrambe dopo `UseForwardedHeaders`. Design §2.3 precisata.

(3) **Lo snapshot `ref_` cancella ciò che IVAO non elenca più**, ma solo su risposta non vuota. Il job faceva solo upsert, quindi una FIR dismessa restava per sempre in `ref_ivao_centers` e continuava a far riconoscere posizioni staff obsolete tramite `IFirDirectory`, cioè permessi. Vale perché i due endpoint rispondono con **l'insieme completo** per un paese (misurato: 7 centri e 221 aeroporti per l'IT); se diventassero paginati o incrementali la decisione va riaperta, ed è scritto nella nota.

Corretti inoltre cinque difetti senza rango di decisione (la lingua di un nuovo membro, che la regola documentata non riusciva mai a decidere; il secondo tempo dell'interceptor che lasciava una transazione aperta quando falliva; il ramo di errore del sync che committava metà snapshot; i permessi duplicati nel cookie; il cookie `hub.auth` senza `SecurePolicy`), chiuso l'N+1 delle proiezioni **con un anticipo su F5** — leggere una volta per salvataggio invece di tre query per riga, dentro la transazione della scrittura — e portate `cms_search_index` e `cms_calendar_entries` **sotto il global query filter**, che F8 §6 dava per compito proprio e ora trova già fatto. `docs/internal/HANDOFF.md` §8 tiene il conto completo, comprese le **undici segnalazioni rientrate** perché erano cose che il piano di implementazione già prevedeva.

**Changelog 0.24** (3 set 2026, sera): **licenza decisa — Apache-2.0**, copyright «2026 Carmine Granato» (§15.5 punto 5, che restava aperto fra MIT e Apache-2.0; il criterio «coerente con gli SDK `ivao-italy`» non decideva da solo, perché quell'organizzazione è mista). Il file `LICENSE` porta ora il testo canonico completo al posto del `TBD`, che alla lettera non concedeva niente a nessuno e rendeva non forkabile un repository che si presenta come forkabile. Nessun header di licenza nei singoli file: Apache-2.0 li raccomanda ma non li impone, e sarebbero rumore in ogni diff. Il file **`NOTICE` invece c'è fin da subito** (deciso da Carmine subito dopo): oggi porta solo l'attribuzione di questo progetto, ma è il posto dove va ogni attribuzione di terzi che il codice dovesse incorporare, ed è l'unica cosa che la licenza chiede a un fork di portarsi dietro alla lettera — averlo dal primo giorno significa che chi forka lo trova già, invece di doverselo inventare. Metterlo anche nel pacchetto pubblicato è compito di F5 (§D/F5 punto 5). `README.md`, `docs/FORKING.md`, design `01` §10 e HANDOFF aggiornati; nota in `docs/internal/decisions/2026-09-03-licenza.md`.

**Changelog 0.23** (3 set 2026, sera): chiusa la fase **F4**, la spina dorsale del dominio (PR #6). Due correzioni al design `01`, decise da Carmine e documentate in `docs/internal/decisions/`. (1) **`IProjectable.Project()` riceve un `ProjectionContext`** (lingue della divisione, lingua di default, `BlockDocumentWalker`): un'entità EF non si fa iniettare niente, ma un contenuto per proiettarsi ha bisogno delle lingue — cablarle sarebbe esattamente ciò che un hub forkabile non può fare, e farlo al posto suo nel `ProjectionWriter` toglierebbe a quello la sua unica ragione di esistere, cioè essere generico. Design §3.6 aggiornata. (2) **`ICurrentUser` fa due domande separate**, `Has(permission, department)` («su questa riga?») e `HasAny(permission)` («in generale?»), al posto di un solo metodo con il dipartimento opzionale: il comportamento è quello che §3.7 già pretendeva — senza risorsa basta un dipartimento qualsiasi, altrimenti in F5 la lista del back-office si chiuderebbe in faccia a ogni coordinatore — ma smette di dipendere da cosa significhi un `null`. Design §3.3 e §3.7 aggiornate. Inoltre **`LocaleCatalog` passa da F4 a F5**: il perimetro di F4 non lo elencava e il primo che ne ha davvero bisogno è il `ValidationProblem` di `MapCrud`; con lui si sistema anche il pacchetto pubblicato, che porta le lingue in `wwwroot/locales/` (per la SPA) ma non alla radice, dove le cerca il backend, e non porta affatto i `config/*.example.json` (piano `02` §D/F5).

Allineata inoltre tutta la documentazione a ciò che il codice fa davvero: design `01` a v1.2 (§3.4 — le righe di audit si scrivono nel secondo tempo come le proiezioni, e il flag di rientranza è per contesto, non un campo di `HubDbContext`; §3.5 — i nomi veri delle proprietà del filtro e il fatto che leggono `ICurrentUser` a query lanciata; §5.3 — la firma vera del walker), i **codici di dipartimento del changelog 0.21 che erano rimasti negli esempi** (§9.2 del piano, §3.3/§5.6/§6.4/§7.3/§8 del design, §D/F5-F6-F8 del piano `02`), e la documentazione pubblica: `README.md` e `docs/FORKING.md` dicevano ancora «phase F1» e «phase F0».

**Changelog 0.22** (3 set 2026): il file `config/ivao-oauth.json` guadagna la chiave **`ApiScopes`**, separata da `Scopes`: gli scope che l'applicazione chiede per se' con `client_credentials` non sono quelli che si chiedono al membro (`client_credentials` non ha `openid`, `profile` o `email` da chiedere). **Misurato il 3 set 2026 contro l'API vera**: per `/v2/centers` e `/v2/airports/all` basta un token `client_credentials` **senza alcuno scope**, quindi `ApiScopes` resta vuoto finche' non servira' `tracker` o simili; le credenziali della divisione IT coprono entrambi gli endpoint (7 centri e 221 aeroporti). Le fixture di `Ivao:UseFixtures=true` restano per la CI e per chi forka senza credenziali. Aggiornate §6.1 e il design `01` §2.2 e §4.6.

**Changelog 0.21** (3 set 2026): i codici dei dipartimenti diventano quelli che usa **IVAO**, confermati da Carmine: `HQ`, **`SOD`**, **`FOD`**, **`AOD`**, **`TD`**, **`MD`**, **`ED`**, **`PRD`**, **`WD`** (prima erano `HQ`, `SO`, `FO`, `AO`, `TR`, `MB`, `EV`, `PR`, `WM`). Non è un suffisso meccanico: ATC operations è `AOD` ma training è `TD`, e l'headquarters resta `HQ`. I **suffissi delle posizioni staff** non cambiano (`AOC`, `AOAC`, `AOA1`, `TC`, `TAC`, `TA1`, `T01`…): cambia solo il dipartimento su cui mappano. La colonna `owner_department` passa da `varchar(2)` a `varchar(4)` con una migrazione **additiva** (`WidenDepartmentCodes`) che converte anche le righe già scritte; `Initial` non è stata toccata. Aggiornate §7 e il design `01` §3.2.

**Changelog 0.20** (2 set 2026, notte): le cartelle di runtime prendono un nome **inglese**, coerente con la regola §4.2 «tutto ciò che non è documentazione interna è in inglese»: la cartella dei segreti si chiama **`secrets/`**, quella della diagnostica **`diagnostics/`** e il file di avvio **`startup.txt`** (prima erano `segreti/`, `diagnostica/` e `avvio.txt`). I nomi italiani venivano da vIPI, che resta un riferimento su *come* funziona il deploy su Plesk, non un vincolo sui nomi. Aggiornate §2.5, §11.3, §14 e il design `01` §2.3-2.4; chi forka non trova più una parola italiana dentro una path.

**Changelog 0.19** (2 set 2026, notte): chiarito che un **modulo non è un plugin caricato a runtime**: si aggiunge nel monorepo e si ricompila (niente NuGet di `Core`, niente `AssemblyLoadContext`, niente bundle JS dinamici — scartati per costo, §16.9 e design `01` §6.5). Per lasciare aperta la porta a costo zero, il confine del modulo vale anche nella SPA: tutto il frontend di un modulo sta in `web/src/modules/<key>/` con un manifest unico (blocchi, widget, route, namespace i18n); elenchi **espliciti** dei moduli in `IvaoHub.Web/Modules.cs` e in `web/src/modules/index.ts` (niente scansione degli assembly); regola ESLint che vieta import tra moduli e da `features/` verso `modules/`. §5.1 aggiornata. Aggiungere un modulo = un progetto + una cartella + due righe.

**Changelog 0.18** (2 set 2026, sera): scritti `docs/internal/01-design-m0.md` (firme di `Localized<T>`, interfacce trasversali, interceptor unico con guardia di scrittura per dipartimento, `IProjectable`, grammatica e matrice dei permessi, `MapCrud`, `/api/me`, `IModule`, envelope di `body_json`, set minimo di blocchi, publish con cattura `frozen`, template seedati, convenzioni UI, ricette di routing, test della spina dorsale) e `02-piano-implementazione-m0.md` (fasi F0–F9 con criteri di accettazione e prompt di apertura per Claude Code). Decisioni: **TanStack Router** al posto di React Router (§3.3, §5.3); **deploy su staging Plesk fuori da M0**, spostato a M1 (§13); icone **`lucide-react` confermate** (dipendenza di Atmosphere 3.1.0, §16.C); blocchi Data risolti lato server da `IDataBlockProvider` registrati per tipo (cattura `frozen` nel servizio di pubblicazione, `live` via `/api/blocks/data/{type}`; il backend continua a ignorare `props`); `security_stamp` in `hub_users` per invalidare il cookie al cambio di grant/superadmin; `cms_search_index` con una riga per lingua (`source_module, source_id, locale`) e FULLTEXT su titolo/testo (è una proiezione riscritta a ogni upsert, non una tabella di traduzioni; nessuna colonna cablata per lingua, per la forkabilità); OpenAPI generato a build-time; unicità slug su `(kind, slug, is_template)`; `MapCrud` in modalità dipartimentale/globale; pagine di sistema seedate in M1, in M0 solo i template. Versioni rivalidate: Pomelo resta 9.0.0 (nessuna 10.x), TanStack Router 1.170.

**Changelog 0.17** (2 set 2026): analisi pre-M0 con il criterio «quanto meno codice possibile, ogni pezzo scritto una volta». Blocchi Data con `renderMode` live/frozen catturato alla pubblicazione (§9.3). Nuova **§16 Meccanismi generici** (15 punti decisi): traduzioni in colonna JSON `Localized<T>` al posto delle tabelle `*_translations`; colonne trasversali come interfacce + un interceptor EF + un solo authorization handler per dipartimento; grammatica dei permessi; proiezioni (calendario, ricerca, award) via `IProjectable` nella stessa transazione, senza bus di eventi né MediatR; un solo motore lista+form nel back-office; un solo endpoint di bootstrap; un solo set di file di lingua; un solo progetto `IvaoHub.Core` (niente `Infrastructure`/`Content` separati); niente `/api/v1`; niente prefisso lingua né prerender per ora; roster staff = chi ha fatto login almeno una volta. **§9.3 riscritta**: pagine, news e documenti diventano **un solo contenuto a sezioni** (`cms_contents`, §7) con **template** che sono contenuti anch'essi, sul modello `Document`/`SectionCatalog` di vIPI; regole di propagazione dei template e chi può crearli. §5.1, §5.2, §5.3, §7, §9.1, §9.4, §9.5, §9.7, §13 e §15 allineate. §16.C: convenzioni UI (icone, componenti ammessi, pagina ui-kit) da fissare nel design di M0. §16.E: processo per i cambi in corso d'opera (classifica a/b/c, checklist PR, test della spina dorsale) e `CLAUDE.md` (privato, gitignored, in italiano) come raccolta delle regole operative.

**Changelog 0.16** (1 set 2026, notte): documentazione degli aeroporti/avvicinamenti militari — decisa l'opzione "fonte unica con viste per pubblico": si scrive solo nelle vSOP di vIPI, che **già oggi** permettono di marcare ogni sezione come *per ATC*, *per piloti* o *per tutti*; manca solo l'endpoint API che espone le sezioni piloti (lavoro nel backlog di vIPI) e la resa nell'hub, che le mostra dentro `/pilots` e nella pagina SO come già fa con le statistiche ATC. Aggiornate §9.4 e la tabella collaborazioni in §9.7.

**Changelog 0.15** (1 set 2026, notte): contratto `IModule` **confermato** dopo verifica sui casi reali di collaborazione tra moduli (§9.7): Events↔Training via calendario unico; ATC↔Events via `ref_` + API vIPI; FlightOps↔Events risolto spostando gli **award nel nucleo** (catalogo + assegnazioni, sempre manuali: il sistema *segnala* a chi assegna, mai assegnazione automatica; `Awards.Assign` configurabile per divisione); SpecialOps↔ATC: la documentazione degli aeroporti/avvicinamenti militari — info ATC **e** piloti — resta in vSOP/vIPI curata dal SOD, l'hub la linka. §15.11 chiusa.

**Changelog 0.14** (1 set 2026, sera): nuova **§9.7 Contratti trasversali**: comportamento in `maintenance` (contenuti in sola lettura, azioni 503, job in pausa), widget di dashboard registrati dai moduli, notifiche e preferenze nel nucleo, privacy (nessun profilo membro pubblico nell'hub: si linka il profilo IVAO ufficiale; GDPR allineato alle norme IVAO, niente export dati utente), ricerca globale con indice centrale nel nucleo alimentato dai moduli; bozza del contratto `IModule` ⚠️ in discussione.

**Changelog 0.13** (1 set 2026, sera): Special Operations entra nel catalogo come **primo modulo opzionale** (`modules.specialops`, acceso per IT), contenuto segnaposto da definire col dipartimento SO — il vSOP militare resta in vIPI; blocco `testimonial` aggiunto al set (§9.3), contenuti di proprietà PR; registro **Virtual Airlines** dentro il modulo `flightops`, mostrato in `/pilots`; chiarito che SES (Slot Events) del template HQ per noi è il `booking_mode` dentro Events.

**Changelog 0.12** (1 set 2026): catalogo moduli deciso. Principio "dipartimento = proprietà, modulo = logica" (§9.0); nucleo editoriale department-aware con pagine a blocchi, documenti per dipartimento e calendario unico (§9.1, §9.3–9.5); quattro moduli di dipartimento obbligatori: Events, Flight Ops (tour), Training, ATC (vIPI) (§9.2); Onboarding non è più un modulo ma una pagina; test system sospeso; ordine di uscita Events → Tours → Training (§13); Events senza migrazione dello storico di `ivao-booking` (§12). Ricerca sul backend del template HQ va.ivao.aero (§2.3-ter).

---

## 1. Obiettivi e vincoli

### 1.1 Obiettivi

1. Un unico punto d'ingresso per la community italiana IVAO, con login IVAO, che inglobi i servizi oggi sparsi su siti secondari (training, booking eventi, onboarding, tour; le info operative ATC restano in vIPI, **raggiunto con un link** — il montaggio nell'hub è sospeso dal 13 set 2026, `decisions/2026-09-13-staccarsi-da-vipi.md`) invece di linkarli.
2. Aderenza al design system ufficiale IVAO **Atmosphere**, così che il sito sia riconoscibile come "IVAO 2.0" e non come un sito divisionale fatto in casa.
3. **Forkabilità**: un'altra divisione deve poter clonare il repository, cambiare un file di configurazione e i file di lingua, e avere il proprio hub funzionante. Nessun "IT" hardcodato nel codice.
4. Manutenibilità da parte di **una sola persona**: pochi pezzi mobili, stack che Carmine già padroneggia (C#/.NET), deploy ripetibile.
5. Migrazione dei dati dai servizi esistenti dove ha senso, senza big-bang: i vecchi servizi restano vivi finché il modulo corrispondente non è pronto.

### 1.2 Vincoli non negoziabili

| Vincolo | Implicazione |
|---|---|
| Hosting **Plesk Linux** (Passenger, solo FTP, Cloudflare davanti — vedi §2.5) | Un processo ASP.NET Core self-contained per (sotto)dominio, dietro nginx di Plesk. Niente Docker in produzione, niente shell, niente servizi aggiuntivi (Redis, RabbitMQ). Migrazioni all'avvio, segreti in cartella dedicata. |
| **MariaDB 11.4.10** | Provider EF Core: Pomelo 9.x (supporta esplicitamente MariaDB 11.4). Charset `utf8mb4`, InnoDB, tutte le date in UTC. |
| **Autenticazione IVAO OAuth2/OIDC** | Nessun account locale: l'identità è sempre quella IVAO. Credenziali (client_id/secret) da richiedere a web@ivao.aero con la lista dei redirect URL. |
| **Atmosphere** | È un pacchetto React (`@ivao/atmosphere-react` v3, Tailwind v4, Node ≥ 20, React 18/19). Usarlo appieno vincola il frontend a React. |
| Bilingue IT/EN | i18n dal giorno zero, sia nella UI sia nei contenuti editoriali. |

---

## 2. Cosa ho trovato (ricerca)

### 2.1 Il sito attuale `it.ivao.aero`

Tecnologia: **Blazor Web** (.NET, `blazor.web.js`) + Bootstrap 5.3, con login proprio su `/Account/Login`. Struttura: Home, Chi siamo, Piloti, ATC, Eventi, Special Ops, Calendario attività. Quasi tutta la documentazione è dietro login ("facendo il login puoi avere accesso a tutta la documentazione"). In homepage: widget "ATC schedulati oggi" (da ATC Scheduling HQ), prossime attività, partner random, link ai social.

Il sito attuale è quindi essenzialmente un **portale di contenuti + calendario**; i servizi veri sono altrove.

### 2.2 I servizi satellite oggi

| Servizio | URL | Cosa fa | Tecnologia nota | Proprietà |
|---|---|---|---|---|
| Training (PATS) | `training.ivao.it` | Richiesta training pratici e esami dopo il teorico, accordo date con trainer, mock exam PP/ADC | Web app con login IVAO | Divisione IT |
| QuickOverview | `quickoverview.ivao.it` | Info operative aeroporti/FIR italiane (LIBB, LIMM, LIPP, LIRR), vPIV/FLIP, vAOIS | v2.6.0, pubblico | Divisione IT — **destinato a sparire: vPIV e il resto confluiscono in vIPI** |
| Onboarding wizard | `welcome.it.ivao.aero` | Guida passo-passo per i nuovi membri | JS statico (repo `ivao-italy/onboarding`) | Divisione IT — diventa una **pagina a blocchi** `/start` (§9.3), non un modulo |
| Booking RFE | repo `ivao-italy/ivao-booking` | Prenotazione slot per Real Flight Event | PHP + MySQL, OAuth IVAO | Divisione IT (fork) |
| Tour system | `tours.th.ivao.aero?div=IT` | Tour divisionali, report leg | PHP, gestito da TH | HQ/altra divisione → **modulo `flightops` dell'hub** (§9.2), assorbe il progetto `Ivao Italy Toursystem` e `AutomaticValidatorTour` |
| Test system | — | Esami a crocette anti-AI | progetto Carmine (C#) | Divisione IT — **sospeso**: rientra solo se Carmine lo ripropone |
| **ATC Services (vIPI)** | `atc.it.ivao.aero` | vSOP (documentazione operativa ACC/APP/aeroporti/vLOA per FIR LIBB, LIMM, LIPP, LIRR, guida, vista Live con AoR top-down), vSOP militari, biblioteca allegati (incl. tipo **PIV**), statistiche ATC personali, Aurora Profile Swapper + bridge desktop Aurora, spazi aerei 3D, editor staff con release AIRAC e traduzione IT/EN | **Blazor Server** net8 (librerie multi-target net8/net10), Clean Architecture (`Vipi.Domain/Application/Infrastructure/Ui/Hosting/Host`), EF Core + Pomelo 8 su **MariaDB 11.4.10**, ~5 000 test, OIDC IVAO standalone, Apache-2.0. **È un modulo montabile in-process in un host ASP.NET Core** (`AddVipiModule`/`MapVipiModule`, prefisso fisso `/services/vsop`) | **progetto Carmine, in produzione dal 16 ago 2026 (v1.3.0)**, in `D:\Programmazione\IVAO_Test\vIPI Ivao Italy` |
| ATC Scheduling | `atc.ivao.aero` | Prenotazione posizioni ATC | IVAO 2.0 | **HQ — solo integrazione via API** |
| Calendario training/esami | `it.ivao.aero/events/calendar` | Eventi ATC Training/Exam con trainer/esaminatore | nel sito Blazor | Divisione IT |
| Discord | `discord.ivao.it` | Community | Bot C# (repo `ivao-italy/discord`) | Divisione IT |
| Forum, WebEye, Wiki | HQ | — | — | **HQ — solo link** |

La org GitHub `ivao-italy` è quasi interamente **C#**: `Ivao.It.IvaoApiSdk` (SDK per API IVAO), `Ivao.It.WhazzupData.SDK`, bot Discord, AuroraHelper. Questo è capitale riutilizzabile.

### 2.3 Atmosphere (design system IVAO)

Repository monorepo pnpm con due pacchetti pubblicati su npm:

- **`@ivao/atmosphere-brand` 3.0.0** — sorgente di verità *framework-neutral*: token DTCG (`tokens.json`), CSS custom properties (`--ivao-color-atmos-700`, `--ivao-font-sans`…), adapter tema Tailwind v4 (`theme.css` → utilities `bg-atmos-700`, `text-fuselage-800`, `font-head`).
- **`@ivao/atmosphere-react` 3.1.0** — libreria componenti React basata su shadcn/ui + Radix: accordion, alert, badge, button, calendar, card, carousel, checkbox, command palette, **data-table** (TanStack), date-picker, dialog, dropdown, **navbar**, **navigation-menu**, **sidebar**, pagination, popover, progress, select, sheet, skeleton, slider, switch, table, tabs, toast, tooltip, typography, **dark-mode-toggle**, ivao-logo. Documentazione Storybook su `ivaoaero.github.io/atmosphere/main`.

Palette: `atmos` (blu brand, default 700 `#0d2c99`), `ocean` (blu secondario, default 600), `fuselage` (grigi/neutri 50–950), semantici red/green/yellow/blue, colori prodotto (Aurora verde-teal, Altitude blu notte, Artifice arancio, Creators viola). Font: **Poppins** per i titoli, **Nunito Sans** per il testo, **IBM Plex Mono** per il mono. Dark mode via classe `.dark`. Container max 87.5rem.

Requisiti: Node 20+, React 18.2+, Tailwind CSS v4, browser moderni (Safari 16.4+, Chrome 111+, Firefox 128+).

**Conseguenza:** con React si usa tutto; con Blazor/Razor si userebbero solo i token e si ricostruirebbero ~45 componenti a mano. È la ragione principale della scelta di stack.

### 2.3-bis Il "sito di default" IVAO per le divisioni (`va.ivao.aero`)

HQ propone alle divisioni un sito template (esempio vivo: IVAO Vatican). È una **one-page** con pagebuilder e temi: navbar (logo IVAO, nome divisione, selettore lingua, menu), hero a gradiente navy→blu con eyebrow verde maiuscolo, titolo, due CTA e **tile numeriche** (membri attivi, prossimi eventi); sezioni About, Events, News, Virtual Airlines, Contact (form solo dopo login IVAO); footer con "Staff Login" e i link legali HQ (Terms, Privacy, IP Policy). Il tema `ivao-classic.css` usa **la palette Atmosphere** (`--ivao-primary #0D2C99` = atmos-700, `#091D66` ≈ atmos-800, `#3C55AC` = ocean-600, verde `#2EC662` e rosso `#E93434` semantici), Poppins per la UI e Nunito Sans per i titoli, radius 12px, ombre blu morbide, container 1140px. Niente dark mode.

**Cosa ne prendiamo** (§8): il ritmo della home pubblica (hero + numeri + eventi + news + contatti), le tile numeriche vive, l'eyebrow di sezione, il form di contatto dietro login, il footer legale HQ, il selettore lingua in navbar. **Cosa no**: la struttura one-page ad ancore (l'hub ha molte sezioni vere), il pagebuilder (abbiamo il CMS), l'assenza di dark mode. Conferma utile: un hub in Atmosphere è visivamente coerente con ciò che HQ già propone, e una divisione che passa dal template all'hub non cambia look.

### 2.3-ter Il backend del template HQ (`va.ivao.aero/backend`, v3.7.6) — visto il 1° set 2026

Osservato con il login staff di Carmine (Module Manager, User Management, Audit Trail e Site Settings non accessibili: `no_permission`). Serve come riferimento di *cosa* offrono le divisioni che usano il template, non di *come* lo costruiamo.

- **Catalogo**: Events (scope Divisional/HQ/RFO/RFE; tipo Online/Live/Training/Exam; booking slot), SES – Slot Events, Live Network, Calendar, Tourcenter (tour, award, validazione PIREP, submission award, import, statistiche), TDCenter (richieste, sessioni, group training, disponibilità trainer, calendario training, flight briefing, esiti/storico, staff TD, GCA holders, import, settings), Virtual Airlines, LoA/SOP (documenti con tipo/categoria/versione/stato), Page Builder, News & Posts, Contact Messages, Media Library, Discord Bot, Mail, IVAO API; amministrazione: User Management, Module Manager, Audit Trail, Site Settings. UI in 5 lingue.
- **Page Builder**: pagina = albero **Section** (sfondo, padding S–XL, larghezza narrow/default/wide/full) → **Row** (preset di colonne su griglia 12, gap, allineamento verticale) → **Block**. 24 blocchi in 5 gruppi: Content (Text, Hero, Image, Video, Embed), Layout (Card Grid, Icon Grid, Columns, Gallery, Logo Grid, Tabs), Data (Stats, Network Stats, Virtual Airlines, Calendar, Table, Progress/Timeline), Interactive (Accordion/FAQ, Testimonial, Call to Action, Alert/Notice, Button Group), Structure (Spacer, Divider). Ogni blocco ha un pannello proprietà a form (Card Grid: colonne + per card icona FA, titolo, descrizione, link). I blocchi *Data* leggono dai moduli: è ciò che rende le pagine vive. Pagine su `/page/{slug}`, bozza/pubblicata, template di partenza (Landing, About Us, Services, Events, Contact), anteprima desktop/tablet/mobile.
- **Cosa ne prendiamo**: il modello dati a blocchi e l'idea dei blocchi Data (§9.3); l'organizzazione di TDCenter e Tourcenter come checklist funzionale per Training e Flight Ops. **Cosa no**: il canvas drag & drop (il pezzo più costoso, e mantenuto da HQ), SES come modulo separato (per noi è il `booking_mode` di Events) e LoA/SOP (è vSOP, sta in vIPI). Virtual Airlines, Testimonial e Special Operations rientrano in forma diversa: registro VA in `flightops`, blocco `testimonial` di PR, modulo `specialops` opzionale (§9.6).

### 2.4 Autenticazione IVAO

- Provider **OpenID Connect standard**: discovery su `https://api.ivao.aero/.well-known/openid-configuration`; token endpoint `https://api.ivao.aero/v2/oauth/token`.
- Flussi: **authorization code** (utente, con client secret lato server, PKCE opzionale) e **client credentials** (server-to-server, per chiamare le API IVAO senza utente). Authorization code valido 5 minuti, access token 1 ora, refresh token disponibile.
- Scope documentati: `openid profile email discord location birthday configuration tracker flight_plans:read/write bookings:read/write friends:read/write training supervisor` (alcuni non ancora implementati).
- Userinfo: `GET /v2/users/me` con `Authorization: Bearer`.
- Claim utili per i ruoli: `ivao.aero/staff_positions` e `ivao.aero/permissions` (mappati nel sample ASP.NET Core `Ivao.AspNetCore.Authentication.OpenIdConnect`, che usa `Microsoft.AspNetCore.Authentication.OpenIdConnect` con `GetClaimsFromUserInfoEndpoint = true`, validazione nonce disabilitata, `SaveTokens = true`).
- Credenziali: per lo **sviluppo** si usano le credenziali di test di Carmine (legate al suo account IVAO, riciclabili tra progetti; quelle pubbliche nel README funzionano solo su `/v2/users/me`). Per la **produzione** le credenziali divisionali verranno inserite dalla divisione nel file JSON dedicato al momento del caricamento del sito. La registrazione dell'app lato IVAO richiede due URL: l'**URL di richiesta del login** (la pagina del nostro sito da cui parte il login, es. `https://<dominio>/auth/login`) e l'**URL di redirect** (la callback, es. `https://<dominio>/auth/callback`); entrambi devono coincidere esattamente con quelli configurati nel JSON.
- Nota del README: "IVAO 2.0 websites (Webeye, FPL, Tracker)" sono SPA React con `oidc-client-ts` + PKCE senza backend.

### 2.5 Plesk e MariaDB — com'è davvero il server (dai deploy di vIPI)

Il server di produzione è lo stesso su cui gira oggi `atc.it.ivao.aero`, quindi i suoi `deploy/atc-ivao/LEGGIMI-*.md` sono la fonte più affidabile che abbiamo. Fatti verificati sul campo tra il 16 e il 31 agosto 2026:

| Fatto | Conseguenza per l'hub |
|---|---|
| Sottoscrizione Plesk `it.ivao.aero`; l'app ATC vive in `/var/www/vhosts/it.ivao.aero/public_atc/` | L'hub sarà un'altra cartella della stessa sottoscrizione (`httpdocs/` o `public_hub/`). Stesso utente di sistema (`itivao`). |
| Le app .NET sono avviate da **Phusion Passenger** (start command `dotnet …/X.dll`), **non** dal .NET Toolkit; riavvio toccando `tmp/restart.txt` | Pacchetto **self-contained linux-x64** (il runtime viaggia nel pacchetto: non dipendiamo dalla versione .NET installata → .NET 10 è possibile). |
| Accesso solo **FTP**, confinato alla cartella dell'app; niente shell; i pacchetti li carica **il committente** (staff Ivao.It), non Carmine | Deploy = zip + foglio istruzioni; niente `dotnet ef database update` a mano; le migrazioni girano **all'avvio** dell'app (`Database.Migrate()`), quindi vanno progettate **additive e sicure**. |
| La cartella dell'app **è stata il document root**: `appsettings.Production.json` fu scaricabile (24–25 ago); ora davanti c'è **Cloudflare** e le direttive nginx negano i file sensibili | I segreti stanno in `secrets/<nome-non-indovinabile>.json` (l'app carica ogni `*.json` di quella cartella, che vince su appsettings); deny nginx su `appsettings*.json`, `*.dll`, `*.pdb`, `diagnostics/`, `secrets/`, `keys/`. Forwarded headers da Cloudflare. |
| Data Protection: le chiavi devono stare in una cartella **scrivibile e persistente dentro l'app** (`vipi-keys/`), da non cancellare a ogni upload | Stessa soluzione: `hub-keys/` + avviso in grassetto nel foglio di aggiornamento. Perderla slogga tutti. |
| MariaDB **11.4.10** condivisa: `max_user_connections` ~25–50, pool limitato a 20, `max_allowed_packet` non confermato, **backup non confermato** (A9), utente creato dal pannello con privilegi non verificati | Pool ≤ 15 per l'hub (condivide il tetto con vIPI!), upload file su disco e non in `longblob`, migrazioni che non richiedono `DROP`, e la domanda backup va chiusa **prima** del primo dato reale. |
| WebSocket passano dal proxy (Blazor Server funziona in produzione) | Se un giorno servisse SignalR nell'hub, è fattibile. |
| I due database (hub e vIPI) si leggono a vicenda **solo attraverso viste `v_share_`** con un utente MariaDB dedicato di sola lettura (14 set 2026, §9.7) | ⚠️ Da verificare con chi amministra il server: che si possa creare quell'utente con `SELECT` sulle sole viste. Se no, ripiego su un'API HTTP di sola lettura. |
| Un pacchetto consegnato in una "finestra cieca" (nessuno che possa ripristinare) è un rischio reale | Finestre di consegna concordate; ogni pacchetto porta un **timbro di versione** visibile (`/api/version`) e una sonda di verifica post-deploy. |

Note residue:
- **Next.js non è supportato ufficialmente** da Plesk; Node.js serve solo in CI per la build della SPA.
- **Pomelo**: la 9.0.0 (ago 2025) supporta MariaDB 11.4 e EF Core 9; gira su runtime .NET 10. ⚠️ Non esiste ancora Pomelo per EF Core 10. vIPI oggi usa Pomelo 8 su **net8, la cui fine supporto è il 10 novembre 2026**: il problema è comune ai due progetti e va risolto una volta sola (vedi §9 riga 7b e §15).

---

## 3. Decisione di stack

### 3.1 Scelta: **ASP.NET Core 10 (API + host) + React SPA con Atmosphere**, deploy come *un solo processo*

```
┌──────────────────────────── Plesk (dominio hub) ────────────────────────────┐
│  Cloudflare → nginx Plesk → Passenger (avvia `dotnet IvaoHub.Web.dll`)      │
│      │                                                                      │
│      ▼                                                                      │
│  Kestrel — IvaoHub.Web (.NET 10, self-contained)                            │
│   ├── /api/**            → controller/minimal API (JSON)                    │
│   ├── /auth/**           → login/callback/logout OIDC (BFF)                 │
│   ├── /health, /api/version                                                 │
│   └── /**                → wwwroot (React SPA buildata, fallback index.html)│
│                                                                             │
│  Hosted services (job schedulati: sync whazzup, mail, cleanup)              │
│      │                                                                      │
│      ▼                                                                      │
│  MariaDB 11.4 (stesso server Plesk)          api.ivao.aero (HTTPS, OAuth)   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Perché così:**

- *Un processo, un dominio*: niente CORS, niente secondo sito Node da tenere acceso, un solo punto di deploy. Per un manutentore singolo è la differenza tra "funziona" e "mi si è spento il frontend".
- *Atmosphere nativo*: la SPA React usa `@ivao/atmosphere-react` così com'è; il frontend è visivamente indistinguibile dai siti HQ.
- *Continuità con l'ecosistema IT-DIV*: SDK C# già esistenti (`Ivao.It.IvaoApiSdk`, Whazzup SDK), stesso stack del tour system e del test system → potranno diventare moduli dell'hub o condividere librerie.
- *Plesk-friendly*: pacchetto self-contained avviato da Passenger, esattamente come vIPI oggi; Node serve solo in CI per la build.
- *Sicurezza dei token*: con il pattern **BFF** (Backend-for-Frontend) access/refresh token IVAO restano sul server; il browser ha solo un cookie di sessione `HttpOnly` + `SameSite`. Il client secret non tocca mai il browser.

### 3.2 Alternative scartate

| Alternativa | Perché no |
|---|---|
| Blazor (come oggi) + token Atmosphere | Si perdono tutti i componenti Atmosphere; ricostruirli in Razor è lavoro enorme e diverge dal look HQ. Blazor Server inoltre soffre dietro proxy (SignalR/WebSocket su Plesk). |
| Next.js full-stack | Miglior DX React, ma Plesk non lo supporta ufficialmente; server custom + Node a runtime = fragilità in più. Stack lontano dal C# di Carmine e dagli SDK IT-DIV. |
| Laravel + Inertia/React | Ottimo su Plesk, ma stack nuovo per il manutentore; si perde la condivisione con tour/test system. |
| SPA React "pura" con `oidc-client-ts` (come i siti HQ) | Richiede comunque un backend per DB e job; e il PKCE public client espone gli access token IVAO nel browser. Meglio BFF. |

### 3.3 Versioni di riferimento (da rivalidare al kickoff)

| Componente | Versione | Note |
|---|---|---|
| .NET SDK/runtime | 10.x LTS | self-contained linux-x64, avviato da Passenger |
| EF Core + Pomelo MySql | 9.0.x + 9.0.x | ⚠️ EF Core 9 finché Pomelo 10 non esce |
| MySqlConnector | ≥ 2.4 | dipendenza Pomelo |
| Node (solo build/CI) | 22 LTS | Atmosphere richiede ≥ 20 |
| pnpm | 10/11 | come il monorepo Atmosphere |
| React | 19 | supportato da Atmosphere 3.x |
| Vite | 7 | |
| TypeScript | 5.x | |
| Tailwind CSS | v4 | obbligatorio per Atmosphere 3 |
| `@ivao/atmosphere-react` / `-brand` | 3.1.0 / 3.0.x | dipende da `lucide-react` (set di icone, §16.C) |
| TanStack Query + **TanStack Router** | 5.x / 1.170.x | data fetching e routing type-safe — router **deciso** il 2 set 2026 (search params tipizzati con zod per il motore lista, integrazione nativa con Query); ricette in `01-design-m0.md` §7.3 |
| react-i18next + i18next | ultime | i18n frontend |
| Serilog | ultima | logging strutturato su file (Plesk) |
| Quartz.NET | ultima | job schedulati in-process |
| MailKit | ultima | SMTP (Plesk mail o esterno) |
| FluentValidation, Mapperly | ultime | validazione DTO, mapping source-generated |
| xUnit + Testcontainers (MariaDB) | ultime | test d'integrazione in locale/CI |

---

## 4. Forkabilità: il sito come "prodotto divisionale"

Sì, si può fare — ma va deciso ora, perché costa poco all'inizio e tantissimo dopo. Principio: **il codice non sa di essere italiano**. Tutto ciò che è specifico della divisione vive in tre posti soltanto.

### 4.1 I tre punti di personalizzazione

1. **`division.json`** (o tabella `division_settings` con seed) — **solo ciò di cui il codice ha bisogno per comportarsi**, il minimo indispensabile:
   ```json
   {
     "code": "IT",
     "countryId": "IT",
     "name": { "it": "IVAO Italia", "en": "IVAO Italy" },
     "domain": "it.ivao.aero",
     "defaultLocale": "it",
     "locales": ["it", "en"],
     "timezone": "Europe/Rome",
     "icaoPrefixes": ["LI"],
     "modules": { "specialops": true },
     "departmentMailboxes": { "WD": "web@example.org" },
     "superAdmins": [704798],
     "firStaffScope": "all"
   }
   ```
   - `modules`: **solo i moduli opzionali** aggiunti in futuro (§9.6). I tre moduli di dipartimento — `events`, `flightops`, `training` — e il nucleo editoriale sono **sempre presenti** (decisione del 1° set 2026: obbligatori per IT e per chi forka; si spengono solo a caldo con `maintenance`, §4.2; `atc` è uscito dall'elenco il 13 set 2026). Primo modulo opzionale: `specialops` (§9.2, riga 5), acceso per IT; `atc` rinascerà opzionale quando servirà.
   - `superAdmins`: elenco di VID che **bypassano ogni policy** (vedi §6.3). Per IT è Carmine (704798); una divisione che forka mette i propri. ⚠️ È solo il **bootstrap**: viene letto una sola volta, quando la tabella `hub_users` non contiene ancora nessun superadmin; da lì in poi la verità sta nel DB e il file è ignorato (§6.3 spiega perché). Il test di forkabilità gira con la lista vuota.
   - `departmentMailboxes` (dal 6 set 2026, **facoltativa**): la casella condivisa di un dipartimento, dove il servizio notifiche scrive quando la notifica riguarda un ufficio e non una persona. Parziale per natura: un dipartimento senza voce viene raggiunto sulle sue persone, e una divisione che non ha caselle non scrive la chiave.
   - `firStaffScope`: `"all"` (default, come vIPI oggi: CH/ACH/CHAx editano i documenti di tutte le FIR) oppure `"own"` (ogni team FIR accede solo ai contenuti della propria FIR). È una scelta della divisione, non del codice.
   Cosa **non** c'è, e perché:
   - **FIR/centri**: si leggono da IVAO, `GET https://api.ivao.aero/v2/centers?countryId={countryId}` (token `client_credentials`), con cache giornaliera e snapshot in tabella `ivao_centers` così l'hub funziona anche se l'API è giù.
   - **Aeroporti**: idem, `GET https://api.ivao.aero/v2/airports/all?countryId={countryId}&includeRunways=true`, snapshot in `ivao_airports` (con piste). `icaoPrefixes` resta comunque in configurazione come rete di sicurezza per filtri e validazioni che non passano dallo snapshot.
   - **Posizioni staff**: la nomenclatura IVAO è **uguale per tutte le divisioni** (fonte: [IVAO Staff Positions and their Roles](https://wiki.ivao.aero/en/home/ivao/role-descriptions)), con `CODE` di **due o tre caratteri** per la divisione e il **codice ICAO della FIR** per i team FIR. Quindi la mappa sta **nel codice** (`StaffRoleMap`, un solo posto), senza configurazione divisionale. Il filtro applica **due prefissi**: `^{division.code}-` per le posizioni divisionali e `^{fir}-` per ogni FIR presente in `ivao_centers` (es. `LIRR-CH` per la divisione IT). Un override in `division_settings` esiste solo per i casi anomali.

   **`StaffRoleMap` — posizioni divisionali (`{CODE}-…`)**

   | Dipartimento | Suffissi | Ruolo interno | Livello |
   |---|---|---|---|
   | Division HQ | `DIR`, `ADIR` | `Director` | coordinator |
   | Special Operations | `SOC`, `SOAC`, `SOA[1-9]` | `SpecialOps` | coordinator / assistant / advisor |
   | Flight Operations | `FOC`, `FOAC`, `FOA[1-9]` | `FlightOps` | idem |
   | ATC Operations | `AOC`, `AOAC`, `AOA[1-9]` | `AtcOps` | idem |
   | Training | `TC`, `TAC`, `TA[1-9]` | `Training` | idem |
   | Training — trainer | `T(0[1-9]\|[1-9][0-9])` (T01–T99) | `Trainer` | member of department |
   | Membership | `MC`, `MAC`, `MA[1-9]` | `Membership` | coordinator / assistant / advisor |
   | Events | `EC`, `EAC`, `EA[1-9]` | `Events` | idem |
   | Public Relations | `PRC`, `PRAC`, `PRA[1-9]` | `PublicRelations` | idem |
   | Web Development | `WM`, `AWM`, `WMA[1-9]` | `Web` | idem |

   **Posizioni FIR (`{FIR}-…`, es. `LIRR-CH`)**: `CH` → `FirChief`, `ACH` → `FirAssistantChief`, `CHA[1-9]` → `FirAdvisor`, con la FIR come attributo (`fir = LIRR`). Il perimetro lo decide `firStaffScope` in `division.json`: con `"all"` (default, come vIPI oggi) i team FIR editano i contenuti di tutte le FIR; con `"own"` solo quelli della propria. Le policy ricevono la FIR della risorsa e quella dell'utente e applicano l'opzione; il Director e i coordinatori di dipartimento non sono mai limitati per FIR.

   Ogni posizione si risolve in una tripla `(Department, Level, Fir?)`; le policy dell'hub ragionano su quelle (es. `Events.Manage` = `Director` ∪ `Events` con livello coordinator/assistant; `Training.Assign` = `Director` ∪ `Training` coordinator/assistant; `Trainer` vede solo le proprie sessioni). Il pattern `T\d\d` va provato **prima** di `TA\d`, e `TA\d` prima di `TC`/`TAC`: il matching è ordinato, dal più specifico al più generico, e coperto da test con l'elenco completo qui sopra. Le posizioni HQ (senza prefisso divisionale né FIR) danno `HqStaff`, sola lettura.

   **Grant manuali per VID**: la derivazione dai claim è la base, non il tetto. Un coordinatore (o il Director) può concedere a un VID specifico un ruolo o un permesso aggiuntivo — l'esempio tipico: `IT-AOA1` che dà una mano all'Events department riceve `Events.Manage` senza cambiare posizione su IVAO. Ogni grant ha chi lo ha concesso, quando, una scadenza opzionale e finisce nell'audit; i permessi effettivi di un utente sono **unione** di derivati + grant, e l'area `/staff/permissions` li mostra separati (così si vede cosa è "di ruolo" e cosa è concesso). Anche una **revoca** puntuale è possibile (un permesso derivato negato a un VID), per i casi rari.
   - **Link (Discord, social, ANSP nazionale…)**: sono contenuto editoriale, non comportamento → vivono nel CMS (`cms_links`, gestiti dallo staff web dall'area riservata), come qualsiasi altro testo o riferimento.
2. **File di lingua** — `locales/it/*.json`, `locales/en/*.json`, **un solo set** letto sia dalla SPA sia dal backend (mail, messaggi di errore): niente `.resx`, un formato e una cartella per chi traduce (§16.8). Una nuova divisione aggiunge `locales/fr/` e imposta `defaultLocale`.
3. **Contenuti editoriali nel DB** — pagine, news, documenti, FAQ, staff directory, link, partner: mai nel codice. Un seed iniziale crea la struttura vuota + pagine "Lorem" tradotte.

### 4.2 Regole di progetto per restare forkabili

- **Lingua del progetto: inglese.** Tutto il codice (identificatori, nomi di file, tabelle e colonne, chiavi i18n, commenti), i messaggi di commit, i nomi di branch, le issue/PR e tutta la documentazione destinata al pubblico (`README.md`, `FORKING.md`, `docs/api`, ADR, changelog, `.example` di configurazione, fogli di deploy) sono in **inglese**: chi forka non deve capire l'italiano. Unica eccezione voluta: la documentazione **interna di progetto** — questo piano, i documenti di design dei moduli, l'`HANDOFF` — resta in italiano perché la legge Carmine ogni giorno, e vive separata in `docs/internal/` (esclusa dai link del README e con una nota in testa che dice che è interna). Le stringhe italiane esistono in un solo posto: `locales/it/`.
- Nessuna stringa visibile all'utente nel codice: sempre chiave i18n.
- Nessun codice ICAO, nome FIR, posizione staff o URL italiano nel codice: FIR e centri dall'API IVAO, il resto da `division.json`/DB.
- **«Questo nomina IVAO?»** — la stessa domanda della riga sopra, un livello più in alto. Il codice
  specifico di IVAO vive dentro un perimetro che esiste già e non ne esce: `src/IvaoHub.Core/Ivao/`
  (il client, il token provider, il job dei dati `ref_`, le fixture), la metà IVAO di
  `src/IvaoHub.Core/Auth/` (`IvaoAuthenticationExtensions`, `IvaoOAuthOptions` e il suo validatore,
  `IvaoOidcProtocolValidator`, `IvaoUserProfileReader`, `IvaoUserTokenStore`, `UserSyncService`,
  `StaffRoleMap`), le tabelle `ref_ivao_*` e l'enum `Department` di `Division/Vocabulary.cs`.
  Tutto il resto — i contratti di dominio, il motore CRUD, il modello editoriale, `Localized<T>`,
  il sistema a moduli, audit, grant, permessi, i18n — oggi ne è libero e ci resta. Un modulo in
  particolare non parla mai con IVAO da sé: c'è un solo `IIvaoApiClient` e lo si usa (§16).
  Due punti fuori dal perimetro nominano IVAO per forza, ed è giusto così: `Data/HubDbContext.cs`,
  che espone i `DbSet` dei dati `ref_`, e la composition root di `IvaoHub.Web` (`Program.cs`,
  `HubPipeline.cs`), che registra i servizi. Una radice di composizione nomina tutto: è il suo
  mestiere. **Verificato il 5 set 2026**: `IIvaoApiClient` è usato solo dentro `Core/Ivao/` (più
  due test), e nessun altro file del nucleo generico importa `IvaoHub.Core.Ivao`.
  È una domanda da farsi mentre si scrive, **non un'astrazione da costruire**: la stessa disciplina
  che rende il fork di un'altra divisione una questione di configurazione tiene il nucleo generico
  riusabile in generale, e costa zero finché si paga riga per riga invece che tutta insieme dopo.
- I **moduli sono feature flag su due livelli**, perché su Plesk "riavviare" significa FTP + `tmp/restart.txt` e non deve essere l'unico modo per spegnere qualcosa:
  - `modules.<nome> = false` in `division.json` (letto **all'avvio**): il modulo non viene registrato nel processo — niente rotte, menu, job, né nuove migrazioni. È la scelta strutturale di *quali moduli usa questa divisione*, cambia raramente e richiede un riavvio. ⚠️ Le migrazioni già applicate **non vengono mai annullate** e i dati restano: disattivare nasconde, non cancella; riattivare riporta tutto com'era. All'avvio l'app avvisa nel log se un modulo disattivato ha ancora tabelle popolate.
  - `maintenance` **a caldo** da `/staff/modules` (Director e superadmin, con audit): il modulo resta caricato ma risponde 503 con una pagina cortese e tradotta, sparisce dal menu e i suoi job vanno in pausa. Nessun riavvio, effetto immediato, reversibile con un clic. È il livello per "il booking ha un problema, spegnilo finché non lo sistemo" e per le finestre di manutenzione annunciate.
  - Un modulo disattivato dal file non può essere acceso dall'interfaccia (l'interfaccia non lo vede proprio): il file è il tetto, l'interfaccia lavora sotto.
- Il ruolo dell'utente deriva dai claim IVAO `userStaffPositions` filtrati per `^{division.code}-` e per `^{fir}-` (FIR da `ivao_centers`), con il suffisso mappato dalla `StaffRoleMap` universale. Così una divisione XX o XXX funziona senza toccare codice né configurazione.
- Brand: Atmosphere è uguale per tutti (è il punto). Personalizzabile solo: logo secondario divisionale, immagini hero, colore d'accento opzionale entro la palette Atmosphere.
- Licenza open source **Apache-2.0** (§15.5, decisa il 3 set 2026), `README` di fork in inglese, template `.env.example`, `docs/FORKING.md`.
- Un **test automatico** che avvia l'app con `division.json` di una divisione fittizia ("XX") in lingua `en` e verifica che non compaiano stringhe italiane né riferimenti IT: è la rete di sicurezza contro le regressioni di forkabilità.

### 4.3 Cosa resta per forza della divisione che forka

Credenziali OAuth (ogni divisione registra la propria app con i propri login/redirect URL e le inserisce in `config/ivao-oauth.json`), server Plesk/DB, SMTP, contenuti, traduzioni. Il repository fornisce tutto il resto.

---

## 5. Architettura applicativa

### 5.1 Struttura del repository (monorepo)

```
ivao-division-hub/
├── src/
│   ├── IvaoHub.Web/              # host ASP.NET Core: Program.cs, auth, static SPA, DI dei moduli
│   ├── IvaoHub.Core/             # NUCLEO, un solo progetto (§16.9): dominio (utenti, divisione, ruoli, i18n),
│   │   ├── Data/                 #   EF Core (Pomelo), interceptor, migrazioni del nucleo
│   │   ├── Ivao/                 #   client API IVAO, snapshot ref_
│   │   ├── Content/              #   contenuti a sezioni (pagine/news/documenti), calendario unico, media,
│   │   │                         #   contatti, staff directory, award — tutto con owner_department
│   │   └── Services/             #   mail/notifiche, Quartz, live status, ricerca
│   ├── IvaoHub.Modules.Events/   # Events dept: eventi, slot RFE/RFO, booking
│   ├── IvaoHub.Modules.FlightOps/# Flight Ops dept: tour, leg, award, validatore automatico (ex Ivao Italy Toursystem)
│   ├── IvaoHub.Modules.Training/ # Training dept: richieste, trainer, disponibilità, sessioni, esiti, mock exam
│   └── IvaoHub.Modules.SpecialOps/ # SOD dept (OPZIONALE, modules.specialops): contenuto da definire col dipartimento
├── web/                          # React SPA (Vite + TS + Atmosphere)
│   ├── src/app/                  # router, layout, providers
│   ├── src/blocks/               # componenti React dei blocchi pagina (§9.3): registry nome → componente
│   ├── src/features/<area>/      # nucleo (auth, me, staff, admin, content, links)
│   ├── src/modules/<key>/        # TUTTO il frontend di un modulo: manifest (blocchi, widget, route, i18n) — design 01 §6.5
│   ├── src/shared/               # api client generato, hooks, i18n, componenti comuni
│   └── locales/{it,en}/
├── tests/
│   ├── IvaoHub.UnitTests/
│   └── IvaoHub.IntegrationTests/ # Testcontainers MariaDB 11.4
├── config/
│   ├── division.json             # identità divisione (IT di default)
│   └── division.example.json
├── deploy/                       # script Plesk, appsettings.Production.template.json
├── docs/                         # EN, pubblica: FORKING.md, ADR, API.md, deploy
│   └── internal/                 # IT, interna: questo piano, design dei moduli, HANDOFF
├── docker-compose.yml            # MariaDB 11.4 + mailpit per lo sviluppo locale
└── .github/workflows/            # build, test, release artifact
```

**Modular monolith**: un solo deploy, ma ogni modulo ha il proprio `DbContext` (o schema-prefix `trn_`, `evt_`…), le proprie rotte `/api/<modulo>/…`, le proprie migrazioni e il proprio `IModule` che si auto-registra se abilitato in `division.json` e che espone un interruttore `maintenance` a caldo (vedi §4.2). I moduli comunicano tramite interfacce di `Core`; ciò che proiettano nel nucleo (calendario, ricerca, segnalazioni award) passa da `IProjectable` e dall'interceptor EF (§16.4), non da un bus di eventi (niente MediatR, che dal 2025 è a licenza commerciale). Mai join cross-modulo, mai FK tra contesti (§16.12).

### 5.2 Backend — layer e convenzioni

- **API**: minimal API o controller con `[ApiController]`, DTO espliciti, `ProblemDetails` per gli errori, **nessun versionamento** (`/api/...`: frontend e backend viaggiano nello stesso pacchetto, §16.10). OpenAPI generato (`Microsoft.AspNetCore.OpenApi` + Scalar UI in dev).
- **Client TypeScript generato** dall'OpenAPI in CI (`openapi-typescript` + `openapi-fetch`): il frontend non scrive mai fetch a mano e rompe la build se il contratto cambia.
- **Persistenza**: EF Core code-first, migrazioni per modulo, `DateTime` sempre UTC (`datetime(6)`), chiavi `int`/`bigint` autoincrement per le tabelle interne e **VID IVAO come identificatore naturale dell'utente**.
- **Job**: Quartz.NET in-process (Plesk = un processo): sync periodico dati IVAO (whazzup, ATC online, booking), invio mail in coda, pulizia sessioni. Tabella `jobs_log`.
- **Cache**: `IMemoryCache`/`HybridCache` per le risposte API IVAO (whazzup 15–60 s, dati statici ore). Niente Redis.
- **Configurazione**: `appsettings.json` + variabili d'ambiente Plesk per i segreti (`IVAO__ClientSecret`, `ConnectionStrings__Default`, `Smtp__Password`). Mai segreti nel repo.
- **Logging**: Serilog → file rolling in `logs/` + console; livello configurabile; correlation id per richiesta.

### 5.3 Frontend — struttura e convenzioni

- Vite + React 19 + TypeScript strict, Tailwind v4 con `@import '@ivao/atmosphere-react/theme.css'`.
- Routing: **TanStack Router** (file-based, type-safe; deciso il 2 set 2026, tre ricette in `01-design-m0.md` §7.3). Layout a due livelli: **pubblico** (navbar Atmosphere + footer) e **area riservata** (navbar + `Sidebar` Atmosphere con i moduli abilitati).
- Stato server: TanStack Query; stato UI locale: React state/`zustand` se serve.
- Form: `react-hook-form` + `zod`; **un solo generatore di form dallo schema zod** per blocchi ed entità (§16.6). Regola: *valida il server, il client mostra* i `ProblemDetails` campo per campo — nessuna regola scritta due volte.
- i18n: `react-i18next`, namespace per modulo, lingua da profilo utente → cookie → `Accept-Language` → `defaultLocale`. Date/numeri con `Intl` e timezone della divisione, ma **orari operativi sempre anche in UTC** (standard IVAO).
- Accessibilità: Atmosphere è Radix-based (già accessibile); si mantiene focus visibile, contrasto, `aria-*` sulle tabelle custom.
- Build: `pnpm build` produce `web/dist` che la pipeline copia in `IvaoHub.Web/wwwroot`. In sviluppo Vite gira su `:5173` con proxy verso Kestrel `:5000`.

---

## 6. Autenticazione, sessione e autorizzazione

### 6.1 Flusso di login (BFF, authorization code)

1. La SPA fa `GET /auth/login?returnUrl=/dashboard` → il backend genera `state` (+ `nonce`, + PKCE anche se non obbligatorio) e redirige a `authorization_endpoint` con scope `openid profile email discord` (+ `tracker`, `bookings:read` se il modulo lo richiede).
2. IVAO riporta l'utente su `/auth/callback?code&state` → il backend scambia il code (`grant_type=authorization_code`, client secret) e ottiene access/refresh token.
3. Il backend chiama `/v2/users/me`, **crea/aggiorna l'utente locale** (VID, nome, rating, divisione, staff positions, permessi, discord id, lingua) e calcola i ruoli interni.
4. Emette un **cookie di sessione** `HttpOnly; Secure; SameSite=Lax` (ASP.NET Core cookie auth, ticket criptato con Data Protection su file system Plesk). I token IVAO restano nel ticket lato server o in tabella `user_tokens` cifrata.
5. Un middleware rinnova l'access token con il refresh token quando serve chiamare le API IVAO per conto dell'utente.
6. `POST /auth/logout` cancella cookie e token; opzionale `end_session_endpoint` se esposto dal provider.

Punto di partenza del codice: **`Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs`**, già collaudato in produzione contro l'IdP IVAO, non il sample GitHub. Da lì si ereditano le scelte che costano settimane a riscoprire: `ResponseType=code` + `UsePkce=true`, i **claim reali** dell'userinfo IVAO (`id` = VID, `centerId`, `firstName`, `lastName`, `publicNickname`, `userStaffPositions` come array di oggetti → mappato ai soli codici, `ivao.aero/permissions` scartato), `OnRemoteFailure` gestito con pagina dedicata, `AllowedHosts` bloccato sul dominio (altrimenti il `redirect_uri` viene costruito dall'header Host), `SaveTokens=false` per non gonfiare il cookie. L'hub si discosta su un solo punto: poiché alcuni moduli chiamano le API IVAO per conto dell'utente, access/refresh token vengono salvati **in tabella cifrata** (`user_tokens`, Data Protection), mai nel cookie. Schema cookie applicativo, **niente tabelle Identity**. Lo stesso codice, estratto in una libreria `IvaoHub.Auth`, può servire app satellite future (il test system, se tornerà; il tour system invece è un modulo dell'hub e non ne ha bisogno).

**File credenziali dedicato — `config/ivao-oauth.json`** (fuori dal repository, in `.gitignore`; nel repo c'è solo `ivao-oauth.example.json`):

```json
{
  "Ivao": {
    "Authority": "https://api.ivao.aero",
    "ClientId": "<client id>",
    "ClientSecret": "<client secret>",
    "LoginUrl": "https://it.ivao.aero/auth/login",
    "RedirectUri": "https://it.ivao.aero/auth/callback",
    "PostLogoutRedirectUri": "https://it.ivao.aero/",
    "Scopes": ["openid", "profile", "email", "discord"],
    "ApiScopes": []
  }
}
```

Regole: il file è caricato all'avvio con `AddJsonFile("config/ivao-oauth.json", optional: false, reloadOnChange: true)`; l'app **rifiuta di partire** se manca un campo o se `RedirectUri` non termina con `/auth/callback`; `LoginUrl` e `RedirectUri` devono coincidere carattere per carattere con quelli registrati su IVAO (schema, host, porta, path, niente slash finale in più). In sviluppo il file contiene le credenziali di test di Carmine con `http://localhost:5173/auth/login` e `http://localhost:5173/auth/callback` (o le URL autorizzate per quelle credenziali); in produzione lo compila la divisione al caricamento del sito. Le variabili d'ambiente Plesk (`Ivao__ClientSecret`) restano supportate come alternativa e, se presenti, hanno la precedenza sul JSON. Il segreto non viene mai loggato né esposto da `/api`.

### 6.2 Server-to-server

Un `IvaoApiClient` con `client_credentials` (scope in `ApiScopes`, separati da quelli del membro; **misurato il 3 set 2026**: centri e aeroporti non ne richiedono nessuno) e token cache per: whazzup/tracker (chi è online in FIR italiane), ATC bookings, dati aeroporti. Un solo client tipizzato, retry con Polly, rate limit rispettoso.

### 6.3 Autorizzazione

- **Ruoli derivati, mai assegnati a mano** come default: `Member` (tutti), `Staff` (almeno una posizione `{code}-*` o `{fir}-*`), ruoli funzionali dalla `StaffRoleMap` universale nel codice (tabella completa in §4.1: dipartimento + livello + FIR opzionale), `HqStaff` (posizioni non divisionali, sola lettura).
- **Superadmin** (per IT il VID 704798): bypassa **ogni** policy e vede ogni area, modulo e ambiente, per poter verificare l'intero sistema senza dover simulare posizioni. È l'unico ruolo non derivato né concesso. **Un'eccezione, dal 16 set 2026** (piano 0.79): su una riga di cui è l'**interessato** (il suo PIREP) non ha i permessi che il catalogo nega all'interessato (`Tours.Validate`), come chiunque altro.

  *Modello di minaccia, detto onestamente*: chi ha l'FTP sulla cartella dell'app controlla già tutto (segreti, chiavi Data Protection con cui si forgia un cookie per qualsiasi VID, i binari stessi). Nessuna configurazione può difendere da quel livello di accesso; lo staff che carica i pacchetti è nella cerchia di fiducia per costruzione. L'obiettivo è quindi che cambiare il superadmin **via file non serva a nulla**, e che qualsiasi cambio sia **visibile e attribuibile**:
  - la verità è la colonna `hub_users.is_superadmin` nel **DB**; `division.json → superAdmins` è letto **solo al bootstrap**, cioè ogni volta che nel DB **non c'è nessun superadmin attivo** (primo avvio, oppure dopo che l'ultimo è stato rimosso) e poi ignorato — modificare il file su Plesk mentre esiste un superadmin non ha effetto. È anche la via di recupero della divisione se resta senza superadmin;
  - il superadmin è il ruolo del **manutentore del sistema**, non un ruolo divisionale: è **indipendente dalle posizioni staff IVAO** e non decade se la persona le perde. Nessun automatismo, nessuna notifica, nessun badge legati allo stato staff del superadmin (deciso da Carmine): si aggiunge e si rimuove solo come descritto sopra;
  - i cambi avvengono solo da `/staff/permissions`, da parte di un superadmin esistente, mai per grant; impossibile rimuovere l'ultimo superadmin;
  - ogni **cambio**, ogni **login** in veste di superadmin e ogni **avvio** in cui l'insieme effettivo dei superadmin differisce dall'ultimo noto (hash salvato in `division_settings`) genera una **email a tutti i superadmin** e una riga di audit con flag `superadmin`; l'interfaccia mostra un badge permanente quando si opera in quella veste;
  - un cambio "fuori banda" resta possibile solo scrivendo nel DB o sostituendo il binario: azioni più grosse, più rumorose e più facili da attribuire di una riga di JSON — e la notifica all'avvio le fa emergere comunque;
  - in staging/sviluppo il superadmin può **impersonare** un altro VID in sola lettura (`/staff/impersonate`), spento in produzione salvo esplicita abilitazione.
  Se in futuro si volesse alzare ancora l'asticella: firma dell'elenco superadmin con una chiave privata di Carmine e verifica con la chiave pubblica compilata nel binario — costringe a ricompilare per manomettere. Non è previsto in M0.
- **Grant manuali per VID** (tabella `user_grants`): ruoli o singoli permessi concessi o revocati a un VID specifico da chi ha `Permissions.Manage` (Director, coordinatori per il proprio dipartimento), con `granted_by`, `granted_at`, `expires_at`, motivo, audit. Casi d'uso: uno staffista che aiuta un altro dipartimento (`IT-AOA1` → `Events.Manage`), un permesso temporaneo per un evento. **Dal 13 set 2026 il soggetto di un grant è un VID oppure una posizione** (dipartimento + livello): è così che una divisione dice chi, da ogni dipartimento, può fare che cosa nei moduli, con i valori iniziali da `division.json` (nota `2026-09-13-moduli-non-subordinati-ai-dipartimenti`). Permessi effettivi = derivati dai claim ∪ grant − revoche; ricalcolati a ogni login e cacheati nella sessione.
  **Salvagente**: un grant si può concedere **solo a chi ha già almeno una posizione staff** derivata dai claim IVAO (divisionale o FIR). L'interfaccia non propone nemmeno gli altri VID, e il server lo verifica comunque. Se l'utente perde tutte le posizioni staff (rilevato al login o dal sync giornaliero del roster), i suoi grant vengono **sospesi** automaticamente (non cancellati: tornano attivi se rientra nello staff) e i superadmin ricevono una notifica. Un grant non può mai conferire `Permissions.Manage` né lo stato di superadmin. Così nessuno può "aprire" il sistema a un VID qualsiasi: il perimetro dello staff lo decide sempre IVAO.
- **Un grant su una riga sola, e chi ha un interesse non decide** (deciso il 15 set 2026, nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`): un grant può portare uno `resource_scope` (`flightops:tour:42`) e allora vale solo sulle risorse che dichiarano quello scope (`IHasResourceScope`) — è «aggiungi validatore» su un tour —; una risorsa con un interessato (`IHasStakeholder`) gli nega i permessi segnati nel catalogo (`DeniedToStakeholder`), superadmin compreso. Tutto nell'unico handler.
- **Token personali** (deciso il 15 set 2026, nota `2026-09-15-token-personali-e-agente-del-validatore`): un programma esterno dell'utente (l'agente del validatore) si autentica con un token creato e revocato da `/me/tokens`, valido **solo** sugli endpoint della sua `audience`, con i permessi ricostruiti a ogni richiesta come per il cookie e solo se l'utente ha fatto login negli ultimi 30 giorni. Il cookie resta l'unico modo di usare il sito. **Scritto in T19a** (24 set 2026, nota `2026-09-24-i-token-personali`): un'audience la dichiara un modulo (`IModule.TokenAudiences`, con il permesso che serve per crearne uno) e un endpoint la chiede con `PersonalTokenPolicy.For(audience)`; il rifiuto è 401 con un `code`; una scrittura fatta con un token porta il suo id in `hub_audit_log.token_id`.
- Policy ASP.NET Core (`[Authorize(Policy = "Training.Manage")]`) + le stesse policy esposte alla SPA in `/api/me` per nascondere menu e pulsanti (la sicurezza vera è sempre lato server).
- Tabella `audit_log` per ogni azione di staff (chi, cosa, quando, prima/dopo).

### 6.4 Sicurezza trasversale

CSRF: cookie `SameSite=Lax` + header custom `X-Requested-With` richiesto sulle mutazioni + antiforgery token per i form. CSP restrittiva (self + `static.ivao.aero` per il logo). Rate limiting su `/auth/*` e sulle API pubbliche. HSTS. Segreti solo via env. GDPR: pagina privacy, export/cancellazione dati utente su richiesta, retention log 90 giorni, dati IVAO minimi (niente email se non serve al modulo — dal 6 set 2026 serve al servizio notifiche, e `hub_users.email` esiste per quello soltanto: nessun DTO la espone, e un test di architettura lo verifica).

---

## 7. Modello dati (nucleo)

**Regola sulle chiavi**: ogni tabella ha una PK esplicita (InnoDB altrimenti usa un row-id nascosto, la replica per riga degrada e EF Core mappa le entità senza chiave solo in sola lettura). Chiave **naturale** dove esiste ed è stabile, **composta** per le associazioni, **surrogata** `id BIGINT AUTO_INCREMENT` per tutto ciò che è storico o a righe multiple.

Schema `hub_` — identità e permessi, condiviso da tutti i moduli:

| Tabella | PK | Altri campi | Note |
|---|---|---|---|
| `users` | `vid` | `first_name`, `last_name`, `division_code`, `country`, `rating_atc`, `rating_pilot`, `discord_id`, `locale`, `is_staff`, `is_superadmin`, `last_login_at`, `created_at` | fonte: `/v2/users/me` ad ogni login; `is_superadmin` solo da bootstrap o da un altro superadmin |
| `user_staff_positions` | `(vid, position)` | `department`, `level`, `fir`, `synced_at` | snapshot dei claim + colonne derivate dalla `StaffRoleMap` |
| `user_grants` | `id` | `vid FK`, `kind` (role/permission), `value`, `effect` (grant/deny), `granted_by`, `granted_at`, `expires_at`, `suspended_at`, `reason` | permessi per VID oltre a quelli derivati; indice `(vid, effect)` |
| `user_tokens` | `vid` | `access_token_enc`, `refresh_token_enc`, `expires_at`, `scopes` | uno-a-uno con `users`, cifrati con Data Protection |
| `division_settings` | `key` | `value_json`, `updated_by`, `updated_at` | override runtime di `division.json` |
| `audit_log` | `id` | `vid`, `action`, `entity`, `entity_id`, `before_json`, `after_json`, `ip`, `is_superadmin`, `at` | indice `(entity, entity_id)`, `(vid, at)` |
| `jobs_log` | `id` | `job`, `started_at`, `finished_at`, `status`, `message` | indice `(job, started_at)` |

Schema `ref_` — **dati di riferimento IVAO**, nel nucleo perché servono a più moduli (Events per gli aeroporti degli slot, il nucleo stesso per riconoscere le posizioni staff FIR, il live status per le FIR online) e il nucleo non può dipendere da un modulo opzionale. Sono in sola lettura per l'app e alimentati dai job giornalieri; vIPI, quando montato, continua a usare il proprio `IAirportDirectory` sul proprio DB:

| Tabella | PK | Altri campi | Note |
|---|---|---|---|
| `ivao_centers` | `id` (es. `LIRR`) | `name`, `country_id`, `raw_json`, `synced_at` | snapshot di `/v2/centers?countryId=…`: le FIR non si configurano |
| `ivao_airports` | `icao` | `name`, `country_id`, `center_id FK`, `runways_json`, `raw_json`, `synced_at` | snapshot di `/v2/airports/all?countryId=…&includeRunways=true`; indice `(country_id)`, `(center_id)` |

**Convenzione `owner_department`** (§9.0): ogni riga editoriale o operativa — pagina, news, documento, voce di calendario, messaggio di contatto, evento, tour, sessione — porta `owner_department` (enum: `HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`, `WD` — i codici che usa IVAO, non un suffisso meccanico; stesso vocabolario di `StaffRoleMap`). Le policy confrontano quel valore con i dipartimenti delle posizioni staff dell'utente; `Director` e `Web` (`WD`) passano sempre. Indice su `(owner_department, status)` ovunque.

**Convenzione traduzioni** (§16.1): nessuna tabella `*_translations`. Ogni campo tradotto è una colonna JSON `{ "it": …, "en": … }` mappata su `Localized<T>`; un solo converter EF, un solo componente di editing, un solo validatore «tutte le lingue di `division.locales` prima di pubblicare». Nelle tabelle qui sotto i campi `*_i18n` sono di questo tipo.

Schema `cms_` (**nucleo** editoriale, cartella `Content` di `IvaoHub.Core`), tutte con `id` surrogata salvo dove indicato:

| Tabella | PK | Campi principali | Note |
|---|---|---|---|
| `contents` | `id` | `kind` (page/news/document), `slug` (univoco per kind), `owner_department`, `visibility` (public/members/staff/department), `status` (draft/ready/published — `ready` dal 13 set 2026, §9.3), `template_id FK → contents` (nullable), `is_template`, `title_i18n`, `summary_i18n`, `seo_i18n`, `body_json` (albero sezioni/blocchi, §9.3, con `schema_version`), `published_version_id`, `published_at`, `updated_by`; per `news`: `category`, `cover_media_id`, `pinned`; per `document`: `category`, `sort`, `file_media_id` (nullable: documento-file) | **Un solo contenuto** per pagine, news e documenti (§9.3); `/start`, `/pilots`, `/about`, la home sono righe `kind = page`. Indici `(kind, status)`, `(owner_department, status)`, `(template_id)` |
| `content_versions` | `id` | `content_id FK`, `version`, `title_i18n`, `body_json`, `changelog`, `published_at`, `published_by` | fotografia **congelata** di ciò che il pubblico vede; il pubblico legge sempre la versione pubblicata, mai la bozza (§9.3) |
| `calendar_entries` | `id` | `owner_department`, `kind` (event/rfe/training/exam/tour/meeting/deadline/other), `starts_at_utc`, `ends_at_utc`, `all_day`, `visibility`, `source_module`, `source_id`, `url`, `title_i18n`, `description_i18n`, `created_by` | §9.5: **un calendario per tutto**; le voci dei moduli sono proiezioni `IProjectable` (`source_module` + `source_id` + `sequence` univoci dal 16 set 2026, più voci per riga, §16.4), quelle interne (riunioni, scadenze) create a mano dallo staff |
| `search_index` | `id` | `source_module`, `source_id`, `kind`, `url`, `owner_department`, `visibility`, `title_i18n`, `text_i18n` (FULLTEXT) | §9.7: proiezione `IProjectable`, stesso meccanismo del calendario |
| `media` | `id` | `owner_department`, `kind` (image/file), `path`, `mime`, `size`, `width`, `height`, `alt_i18n`, `uploaded_by` | storage su disco sotto `data/media/`, limite di upload esplicito (proxy Plesk) |
| `contact_messages` | `id` | `to_department`, `from_vid`, `subject`, `body`, `status`, `handled_by`, `handled_at` | il form pubblico è per autenticati; ogni dipartimento vede i propri |
| `staff_directory` | `position` | `vid`, `sort`, `description_i18n` | dal roster (chi ha fatto login, §16.13) + ordinamento editoriale |
| `links`, `partners`, `faq` | `id` | `owner_department`, campi `*_i18n` | `faq` è anche un blocco Accordion; `links` è l'entità-cavia di M0 (§16.15) |
| `awards`, `award_assignments`, `award_signals` | `id` | catalogo `hub_awards` (`name_i18n`, `description_i18n`, `criteria_i18n`, `image_media_id`, `owner_department`, `is_active`); assegnazioni `hub_award_assignments` (`award_id`, `vid`, `reason`, `signal_id` unico, chi e quando nelle colonne d'audit); segnalazioni `cms_award_signals` (`source_module`, `source_id`, `vid`, `reason`, `award_id` proposto, `status`) — piano 0.82 | §9.1/§9.7: le segnalazioni sono una proiezione `IProjectable` come calendario e ricerca |

Schema `evt_` (modulo Events), `id` surrogata: `events` (tipo: RFE/online day/training/exam/…, `starts_at_utc`, `ends_at_utc`, `airport_icao FK → ref_.ivao_airports`, banner, visibility, `booking_mode`, `title_i18n`, `description_json` a sezioni §9.3), `event_slots` (callsign, dep/arr ICAO, times, aircraft, `booked_by_vid`, stato; indice univoco `(event_id, callsign)`), `event_participants` (PK `(event_id, vid)`), `event_atc_positions` (PK `(event_id, position)`).

Gli schemi degli altri moduli (`fo_` Flight Ops/tour + registro VA, `trn_` Training, `so_` Special Ops) si definiscono nel documento di design di ciascun modulo (vedi §9.2).

Convenzioni MariaDB: `utf8mb4_unicode_ci`, InnoDB, `datetime(6)` UTC, soft delete solo dove serve storicità, indici su ogni FK e sui campi di ricerca, `JSON` nativo MariaDB per i campi flessibili (`value_json`, `before_json`).

---

## 8. Design e architettura dell'informazione

### 8.1 Principi

- **Atmosphere così com'è**: stessa navbar (logo IVAO + divisore + titolo "Italy"), stessi radius, stesse card. La personalità divisionale sta nei contenuti e nelle foto, non nei colori.
- **Due mondi, una navigazione**: area pubblica editoriale (chi siamo, come iniziare, eventi, news) e area riservata operativa (dashboard personale, moduli). Il login non è un muro: le pagine pubbliche sono davvero pubbliche (oggi non lo sono), l'accesso sblocca i servizi.
- **Dashboard personale come home post-login**: "cosa posso fare oggi" — prossimi eventi a cui sono iscritto, richieste training in corso, mie prenotazioni, ATC online in Italia adesso, avvisi staff. **Decisa il 13 set 2026** (`decisions/2026-09-13-le-dashboard-a-tutto-schermo.md`): una riga `Dashboard` di blocchi Data che rispondono per chi guarda, a tutto schermo, a tessere.
- **Dark mode** di serie (Atmosphere la fornisce), preferenza salvata nel profilo.
- **Mobile-first per la consultazione**, desktop per la gestione (data-table, back-office).

### 8.2 Sitemap proposta

```
/                          Home (ispirata al template HQ, §2.3-bis): hero con eyebrow + titolo + CTA "inizia qui" (pilota/ATC) + tile numeriche vive (membri attivi, ATC online, piloti in area, prossimi eventi), poi prossimi eventi, news, "come iniziare", contatti (form dietro login)
/start                     Onboarding: pagina a blocchi (Timeline + Card + CTA) che sostituisce welcome.it.ivao.aero — non è un modulo
/pilots                    Sezione piloti (Flight Ops): guide, documenti del dipartimento, link software, card verso i tour, registro Virtual Airlines (Logo Grid)
/atc                       Sezione ATC: pagina di sistema (carriera, rating, posizioni, sector file); la documentazione operativa è un link ad atc.it.ivao.aero, voce di menu (13 set 2026)
/events                    Calendario + lista eventi; /events/{slug} dettaglio + booking slot
/training                  Modulo Training: richieste training/esami, disponibilità trainer, sessioni, esiti, mock exam
/tours, /tours/{slug}      Modulo Flight Ops: tour, leg, classifica, award; /tours/{slug}/report per il PIREP
/calendar                  Calendario unico (eventi, RFE, training, esami, tour; voci interne solo per staff)
/documents, /documents/{dept}  Indice generale dei documenti (visibilità per ruolo); /documents/{slug} il singolo documento; nelle pagine per raccolta (§9.4)
/news, /news/{slug}        Indice generale delle news; nelle pagine per raccolta (§9.4)
/{a}[/{b}[/{c}]]           Le pagine, in gerarchia fino a tre livelli; il primo livello lo creano WD e HQ, l'ultimo pezzo si genera dal titolo (13 set 2026, nota contenuti-centralizzati §3.7)
/about                     Divisione, staff directory (da claim IVAO), partner, contatti
/me                        Dashboard personale; /me/profile, /me/bookings, /me/training, /me/tours
/staff                     Back-office. La dashboard personale da staffista (decisa il 13 set 2026): ciò che aspetta me, le mie bozze, il calendario interno, i miei dipartimenti, i riquadri dei moduli
/staff/{dept}              Dashboard del dipartimento: seminata alla nascita, poi modificata dal dipartimento nell'editor dei contenuti (riga di `cms_contents` con visibilità `department`)
/staff/content             Pagine, news, documenti e template di tutti i dipartimenti che raggiungo, filtrati per `kind` e `department` (13 set 2026, §9.3); «Pagine» nel menu del dipartimento porta qui già filtrata
/staff/links, /staff/media Stessa forma: si vedono e si scelgono tutti, si gestiscono i propri (§9.1)
/staff/events, /staff/tours, /staff/training  I moduli, sezioni a sé e non di un dipartimento (13 set 2026, nota moduli-non-subordinati)
/staff/{dept}/**           Spazio del dipartimento: dashboard, voci di calendario interne, contatti
/staff/admin/**            Solo Director/WM/superadmin: utenti e grant, moduli/maintenance, impostazioni divisione, audit
/{locale}/...              prefisso lingua opzionale per SEO delle pagine pubbliche
```

### 8.3 Componenti chiave (tutti da Atmosphere)

Navbar + NavigationMenu (pubblico), Sidebar (riservato/staff), Card (eventi, moduli), DataTable (slot, richieste, utenti), Calendar/DatePicker (eventi, disponibilità trainer), Dialog/Sheet (booking, form rapidi), Badge (rating, stato), Tabs, Toast, Command palette (`⌘K` per staff: cerca utente/evento/pagina), DarkModeToggle, Skeleton per il loading.

Componenti custom (pochi, costruiti con i token): `Hero` (gradiente atmos-800→atmos-600, eyebrow verde, CTA), `StatTile` (numero grande + etichetta, dati vivi), `SectionHeader` (eyebrow + titolo, come nel template HQ), `LiveStatusStrip` (ATC/piloti online), `RatingBadge`, `AirportCard`, `EventTimeline`, `LocaleSwitcher`, `MarkdownContent`, `ContactForm` (visibile solo autenticati). Footer con link legali HQ (Terms of Use, Privacy Policy, IP Policy) e "Staff area".

⚠️ **L'elenco vero e chiuso è `web/src/shared/ui/catalog.ts`**, ed è scritto per chi forka in
`docs/UI-GUIDELINES.md` §3; questo paragrafo è l'intenzione con cui è nato. Il **quinto** aggiunto
dopo M0 — e il primo dopo la chiusura di M1, che ne contava quattro e sono i quattro previsti — è
**`Notice`** (7 set 2026, G13, chiesto da Carmine dopo la demo): un avviso a quattro stati (errore,
avviso, successo, informazione) usabile ovunque, in due forme che leggono la stessa tabella — un
riquadro che resta, e la stessa frase detta in un angolo dello schermo e poi via, che `useNotice()`
mette nella coda di toast di Atmosphere. Non sostituisce `ProblemAlert`, che disegna il rifiuto del
server campo per campo: unire i due tocca ogni schermata del back-office ed è una decisione a sé,
che Carmine ha scelto di non prendere adesso.

**Tre componenti in più con M2** (decisi in T0, 16 set 2026, piano 0.79): **`RouteMap`** (la mappa delle leg, MapLibre, nota
`2026-09-15-la-mappa`), **`LegGrid`** (l'editor delle leg a tabella, eccezione dichiarata al motore lista e form, `05-design-m2.md`
§8.4) e **`MessageThread`** (il filo di un contatto con le risposte, nota `2026-09-15-contatti-con-risposte`). Entrano nell'elenco nelle
fasi T10, T7 e T14. ⚠️ **`RouteMap` è entrato con T10** (22 set 2026) ed è il **ventiduesimo** dell'elenco: sta in `shared/ui/` e non
nel modulo — non sa che cosa sia un tour, prende coppie di aeroporti — quindi è in `catalog.ts` e nella galleria come gli altri. La sua
mappa di base è un file dell'installazione, non del pacchetto, e senza quel file disegna comunque le tratte su un fondo neutro (nota
`2026-09-22-il-pubblico-dei-tour`). ⚠️ `LegGrid` è entrato con **T7a** (18 set 2026) e vive **nel modulo**, perché conosce le leg: non è ancora in
`catalog.ts` né nella galleria, che non importa da `modules/`; come un modulo ci porta i suoi componenti lo dice T20. `ConfirmDialog` è
stato esteso nella stessa fase con `onOpenChange` e `confirmDisabled` (nota `2026-09-18-le-leg-dei-tour`). ⚠️ **`MessageThread` è entrato
con T14a** (23 set 2026, piano 0.96) ed è il **ventitreesimo**: sta in `shared/ui/`, disegna quello che il server manda (per il mittente
il server ha già tolto chi ha risposto) ed è montato dal back office dei contatti e da `/me/contacts/{id}`.

---

## 9. Catalogo moduli — deciso il 1° settembre 2026

### 9.0 Il principio: il dipartimento è l'asse di proprietà, il modulo è il confine del codice

L'idea di partenza di Carmine era "un modulo per dipartimento, e dentro ciò che serve al dipartimento". Presa alla lettera duplicherebbe news, documenti e calendario in ogni dipartimento (tre sistemi news, sette calendari); presa nel modo giusto è la spina dorsale di tutto l'hub. La forma decisa:

- **Ogni contenuto appartiene a un dipartimento.** Pagine, news, documenti, voci di calendario, messaggi di contatto, eventi, tour, sessioni di training: tutti hanno `owner_department` obbligatorio (§7). Quel campo decide tre cose, senza regole aggiuntive: *chi può modificarlo* (lo staff del dipartimento, più Director e Web; gli altri via grant per VID, §6.3), *dove compare nel back-office* e *come si filtra sul sito pubblico* (`/training` mostra automaticamente news, documenti ed eventi del Training).
- **Il back-office è organizzato per dipartimento.** Uno staff Events entra in `/staff` e trova "Events Department": i suoi eventi, le sue news, i suoi documenti, le sue voci di calendario, i contatti ricevuti. **Non vede** gli altri dipartimenti (deciso: nessuna lettura trasversale; chi aiuta un altro dipartimento riceve un grant). Director, Assistant Director e Web vedono tutto; il superadmin anche.
- **I servizi comuni si scrivono una volta sola** e stanno nel *nucleo editoriale* (cartella `Content` di `IvaoHub.Core`): non sono un modulo, non si spengono, portano l'etichetta del dipartimento su ogni riga.
- **Rivisto il 13 set 2026 — i moduli non appartengono ai dipartimenti** (`decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`): eventi, tour e training sono sezioni a sé, pubbliche e nel back-office (`/staff/events`, `/staff/tours`, `/staff/training`); i permessi di un modulo si danno a posizioni e VID con i grant; una riga di modulo appartiene a **uno o più** dipartimenti («a cura di»), e quell'insieme decide chi la modifica; le dashboard di dipartimento mostrano i moduli con blocchi Data. Dove il testo qui sotto dice «il modulo del dipartimento X», va letto «il modulo di cui X è di solito a cura».
- **Un modulo di codice esiste solo dove un dipartimento ha logica che nessun altro ha**: Events (slot e prenotazioni), Flight Operations (tour, leg, award, validatore), Training (richieste, trainer, sessioni, esiti). ATC Operations ha perso il suo modulo il 13 set 2026 insieme al montaggio di vIPI: `/atc` è una pagina di sistema. Membership, PR e Web usano i servizi comuni e hanno il loro spazio nel back-office, ma nessun modulo di codice finché non serve qualcosa di specifico (allora nasce un modulo **opzionale**, §9.6).
- **La navigazione pubblica non segue l'organigramma**: un nuovo membro cerca "Piloti / ATC / Eventi / Training", non "Flight Operations Department". La sitemap (§8.2) resta per pubblico; il dipartimento è visibile solo come etichetta e filtro.

### 9.1 Nucleo (sempre presente, `IvaoHub.Core`)

| Servizio | Cosa fa | Dipendenze | Note |
|---|---|---|---|
| Utenti, permessi, audit | login OIDC BFF, `StaffRoleMap`, grant per VID, superadmin, audit | IVAO OAuth | §6 |
| Dati di riferimento IVAO | FIR/centri, aeroporti (snapshot giornalieri) | API IVAO `client_credentials` | §7 schema `ref_`; **dal 16 set 2026** (M2, T1) aeroporti **del mondo** con IATA e coordinate, piste con le testate (per gli aeroporti che le chiedono), tipi di aereo con varianti, equipaggiamenti e transponder |
| **Confini dei FIR** (M2) | poligoni dei FIR del mondo, «in quale FIR sta questo punto» | il dataset di **VATSpy**, scaricato da un job e mai committato | nota `2026-09-16-i-confini-dei-fir` (sostituisce OpenAIP, misurato in T1); CC BY-SA 4.0 con attribuzione; senza il file, `ref_firs` è vuota e la proposta degli ATC si ferma agli aeroporti |
| **Meteo** (M2) | METAR e TAF attuali e della storia recente (`IWeatherSource`) | NOAA, ripiego del METAR su IVAO e VATSIM | stessa nota; il salvataggio è del modulo dei tour (`fo_weather_reports`, nota `2026-09-24-il-meteo-salvato`) |
| **Token personali** (M2) | un programma esterno dell'utente chiama gli endpoint di una `audience` | — | nota `2026-09-15-token-personali-e-agente-del-validatore`, §6.3 |
| **Preferenze dell'utente** (M2) | chiave e valore per utente, chiavi dichiarate dai moduli | — | l'ordine della coda del validatore; come le preferenze delle notifiche |
| **Contenuti a sezioni** | pagine, news e documenti: un solo modello `cms_contents` a sezioni e blocchi, con template, versioni, per dipartimento | media | §9.3; include `/start` (onboarding), `/pilots`, `/about`, la home |
| **News** | articoli con categoria, copertina, pin, RSS; corpo a blocchi | media | ogni dipartimento pubblica le proprie |
| **Documenti** | documenti per dipartimento, di qualsiasi natura, con raccolte, versioni, visibilità per ruolo | media | §9.4; nelle pagine per raccolta |
| **Calendario unico** | tutte le voci: eventi, RFE, training, esami, tour, riunioni staff, meeting di divisione, scadenze | proiezioni `IProjectable` dai moduli (§16.4) | §9.5 |
| Media library | upload immagini/file con alt tradotto, per dipartimento | disco Plesk | limite upload esplicito; dal 13 set 2026 **letta e scelta da tutto lo staff** (`ISharedForReading`), gestita dal proprietario, da WD e HQ e dai grant — lo stesso per i link. **Dal 16 set 2026** (nota `2026-09-15-file-con-scadenza`) le righe dei moduli dichiarano i file che usano **con una scadenza** (`IProjectable`, `cms_media_uses`), e un job del nucleo elimina i file con tutti gli usi scaduti: i banner di un tour un mese dopo la chiusura, poi quelli degli eventi |
| Contatti | form (solo autenticati) indirizzato a un dipartimento; coda nel back-office | mail | sostituisce il form del sito Blazor. **Dal 16 set 2026** (nota `2026-09-15-contatti-con-risposte`) un contatto è un **filo** con le risposte, un tipo (`Dispute`, `Clarification`…), riferimenti a oggetti dei moduli e partecipanti in più; il membro risponde da `/me/contacts`. Il primo modulo che lo usa sono i tour (T14b, piano 0.97): la contestazione aperta da una proiezione del PIREP e decisa con una risposta nel filo, il chiarimento dalle pagine dei tour |
| **Award** | catalogo award (nome, immagine, dipartimento, criterio) + assegnazioni per VID con motivazione e audit; **il catalogo lo scrive il dipartimento** (`Awards.View`/`Awards.Edit`, letto da tutti) e **assegna chi ha `Awards.Assign`**, globale (in IT: MD e HQ con i grant — varia per divisione, quindi è configurazione, non codice); il membro vede i suoi award sul profilo IVAO, mai nell'hub (piano 0.82) | segnalazioni dai moduli, mail | **mai assegnazione automatica**: i moduli segnalano a chi assegna cosa c'è da assegnare (coda "da verificare"), l'assegnazione è sempre umana (§9.7) |
| Mail | SMTP, template tradotti, coda con retry (Quartz) | SMTP Plesk | infrastruttura, non un modulo |
| Live status | ATC/piloti online in area, FIR online, tile della home | Whazzup SDK | polling, niente SignalR in prima fase |
| Discord | link "collega Discord" + Discord ID in profilo; il bot resta separato | scope `discord` | API interna hub→bot in M6 |
| Staff directory, partner, link, FAQ | dai claim + ordinamento editoriale; contenuti trasversali | — | FAQ riusata dal blocco Accordion |

### 9.2 Moduli di dipartimento (obbligatori, tutti nel monorepo)

Decisione: i moduli 1–3 sono **obbligatori** (erano 1–4 fino al 13 set 2026, quando `atc` è uscito insieme a vIPI) per IT e per chi forka (non hanno flag in `division.json`; hanno solo `maintenance` a caldo). Tutto ciò che si aggiunge dopo è opzionale — il primo è `specialops` (riga 5). **Il numero della riga non è l'ordine di costruzione**: dal 13 set 2026 si fanno `flightops` (M2), `training` (M3), `events` (M4), e nessun altro per ora; `specialops` resta un segnaposto senza milestone (piano 0.77). Coordinator e assistant del dipartimento di base hanno tutte le funzioni del modulo, per grant a una posizione; gli advisor li decide il design del modulo.

| # | Modulo | Dipartimento | Contenuto | Complessità | Dipendenze | Note e decisioni |
|---|---|---|---|---|---|---|
| 1 | **`events`** | Events (ED) | eventi (divisionali, HQ, RFE, RFO; online/live), slot con prenotazione, partecipanti, posizioni ATC, pubblicazione nel calendario unico | Media-alta | `ref_` aeroporti, API IVAO bookings ATC, mail | Sostituisce `ivao-booking` PHP e il calendario del Blazor. **Nessuna migrazione dello storico**: il vecchio booking resta consultabile in sola lettura per un periodo, poi redirect (§12). |
| 2 | **`flightops`** | Flight Operations (FOD) | tour dell'anno (creabili in anticipo, sette tipi), leg, PIREP con validazione umana e **controlli automatici che suggeriscono** (sul server, e sul PC del validatore per quelli che vogliono i dati di navigazione), contestazioni e chiarimenti, ban, statistiche dei validatori, ~~classifiche~~ **mai punti né classifiche** (15 set 2026), **segnalazioni di completamento per gli award** (il registro award vive nel nucleo, §9.1/§9.7: FlightOps segnala, chi ha `Awards.Assign` assegna), ~~registro Virtual Airlines~~ **rimandato** (15 set 2026), voci nel calendario unico | Alta | API IVAO (tracker, aeroporti, aerei), mail, `ref_` aeroporti e piste, meteo e FIR del nucleo, archivio ATC di vIPI (facoltativo) | Assorbe il progetto `Ivao Italy Toursystem`: il suo design confluisce nel documento di design di questo modulo e il repo separato si chiude. Sistema **divisionale** (i tour HQ restano su tours.th.ivao.aero). **Design: `05-design-m2.md`** (chiuso il 15 set 2026); fasi T1–T21 in `06-piano-implementazione-m2.md` parte C (piano 0.79). |
| 3 | **`training`** | Training (TD) | richieste training/esame, matching trainer, disponibilità, sessioni, esiti e storico, mock exam PP/ADC, group training, voci nel calendario unico | Alta | API IVAO (scope `training` non disponibile → input manuale/CSV, `ITheoryExamSource`), mail | Sostituisce PATS (`training.ivao.it`). Migrazione dello storico ⚠️ da decidere quando si sa chi mantiene PATS e se il DB è accessibile (§15). Checklist funzionale: il TDCenter del template HQ (§2.3-ter). |
| 4 | ~~**`atc`**~~ **tolto il 13 set 2026** | ATC Operations (AOD) | **Non più obbligatorio** (`decisions/2026-09-13-staccarsi-da-vipi.md`): senza vIPI non ha niente di suo; `/atc` è una pagina di sistema e la documentazione operativa è un link ad `atc.it.ivao.aero`. Rinasce **opzionale** (`modules.atc`) con il suo design quando l'ATC avrà logica che nessun altro ha. Testo di prima: sezione `/atc` (carriera, rating, posizioni, sector file), card e deep link verso vIPI, statistiche ATC in dashboard via API vIPI; in M5 il **montaggio in-process** di vIPI sotto `/services/vsop` | Bassa ora, media al montaggio | vIPI (Blazor, net8 → net10) | Due tempi come da §15.2: oggi app separata con SSO, domani un solo processo. Il modulo esiste da subito così le rotte riservate a vIPI sono escluse dal fallback SPA fin da M0. |
| 5 | **`specialops`** ⚠️ | Special Operations (SOD) | **Segnaposto, da definire col dipartimento SO** (candidati: presentazione del gruppo, arruolamento, attività/missioni con iscrizioni e voci nel calendario). ~~Il vSOP militare e la documentazione operativa SO restano in vIPI~~ Dal 13 set 2026 i documenti SO possono essere documenti dell'hub (§9.4) | Da stimare | da definire | **OPZIONALE** (`modules.specialops`, acceso per IT): non tutte le divisioni hanno un gruppo SO attivo. Design rimandato (§15.10); nel frattempo SO ha comunque il suo spazio di dipartimento nel back-office (news, documenti, pagine, calendario). |

Ogni modulo: proprio progetto `IvaoHub.Modules.<Nome>`, proprio schema (`evt_`, `fo_`, `trn_`), proprie rotte `/api/<modulo>`, proprie migrazioni, proprio `IModule`; comunica col nucleo tramite interfacce ed eventi di dominio (es. `EventPublished` → il nucleo crea la voce di calendario), mai con join cross-schema.

### 9.3 Contenuti a sezioni — il modello unico (deciso il 2 settembre 2026)

Deciso da Carmine: **tutti i contenuti creati dal sito sono documenti modulari**, composti da sezioni predisposte per obiettivi specifici, e chi crea qualcosa crea *un documento o un template di documento* sul quale se ne costruiscono altri. Il riferimento è il modello a cui vIPI è arrivata dopo il refactor 08 (`Document → DocumentVersion → DocumentSection → ContentBlock`, `SectionCatalog`, profili per tipo, pubblico congelato alla release, editor che mostra cosa non va). Nell'hub lo si prende **allargando** di un livello il modello a blocchi già deciso, non aggiungendo un secondo sistema. Chiude la decisione «markdown vs WYSIWYG» e sostituisce le tre entità separate pagine/news/documenti.

- **Un solo contenuto.** Pagine, news e documenti sono righe della stessa tabella `cms_contents` (§7) con `kind`; condividono CRUD, editor, renderer, versioni, proiezione nella ricerca. Nel back-office restano separati come filtro su `kind`, non come codice. Le poche colonne specifiche (categoria e file per i documenti, copertina e pin per le news) sono nullable sulla stessa riga.
- **Struttura**: `Content → Section[] → Block[]`, con **sotto-sezioni fino a profondità 3** (vIPI l'ha trovata sufficiente), serializzata in `body_json` con `schema_version`. Una *Section* ha `key` (stabile, es. `purpose`, `syllabus`, `hero`), `title_i18n`, `layout` (`stacked` oppure colonne `1/2+1/2`, `1/3+2/3`, `3×1/3`… — il livello *Row* del Page Builder HQ diventa una proprietà della sezione), sfondo/padding/larghezza, `collapsed`, e i blocchi. Un *Block* ha `type` + `props` validati da uno schema `zod` con versione; i campi testuali dei blocchi sono `Localized` come tutto il resto (una sola traduzione da gestire, non un albero per lingua).
- **Le sezioni «predisposte per un obiettivo» non sono un nuovo tipo di oggetto.** In vIPI la distinzione Derived/Editorial/Host è costata registry e ponti; nell'hub il registry dei blocchi basta. Tre forme coprono tutto: sezione **libera** (blocchi testo, tabella, callout, immagine, video, embed, galleria, accordion, CTA…); sezione **strutturata** (un solo blocco con schema — «scheda evento», «syllabus», «scheda Virtual Airline», «verbale» — il cui form è generato dallo stesso motore di §16.6, bloccato dal template); sezione **derivata** (un blocco *Data* vivo: `calendar`, `newsList`, `documentList`, `eventList`, `staffList`, `networkStats`, `stats`, `timeline`). I moduli **registrano blocchi**, come già deciso in §9.7, non tipi di sezione.
- **Il template è un contenuto anch'esso**: una riga di `cms_contents` con `is_template = true`, stesso dipartimento, stesso editor. In più, per ogni sezione, porta tre attributi che il contenuto normale ignora: `required`, `locked` (struttura fissa: si compila, non si ristruttura) e `allowedBlocks`. «Nuovo da template» è una copia profonda che conserva `template_id`. Niente tabella dei template, niente editor dei template. I template di sistema (Landing, Section page, About, Contact, Onboarding, Verbale, Guida, Policy) e quelli portati dai moduli sono **seed JSON**, non codice, e chi forka li modifica dall'interfaccia.
- **Cambio di template dopo la creazione dei figli** (deciso): nessuna propagazione automatica del contenuto. Nell'**editor** il contenuto figlio mostra la sezione nuova da compilare ed evidenzia quella che il template ha tolto, con un'azione «allinea»; il **pubblico** continua a vedere la versione pubblicata, congelata in `cms_content_versions`, finché qualcuno non ripubblica. È lo stesso patto di vIPI: documento pubblico congelato, editor che mostra cosa non va.
- **Chi crea o modifica template** (deciso): solo Director, Assistant Director, WM, AWM e, per il proprio dipartimento, coordinator e assistant coordinator — permesso `Content.ManageTemplates` nella grammatica di §16.3. Advisor e membri usano i template, non li cambiano.
- **Chi li legge** (deciso il 5 set 2026, dopo G0 di M1): **tutto lo staff, di qualunque dipartimento**. Il template appartiene a un dipartimento — ognuno si fa i suoi — ma la lettura è comune: altrimenti i template di sistema, che nascono nel dipartimento Web, sarebbero invisibili a tutti gli altri e «Nuovo da template» non comparirebbe nemmeno; e una pagina nata da un template che il suo editore non può leggere perde nell'editor i vincoli di quel template, cioè la riga precedente smette di valere. Usare il template di un altro dipartimento crea una pagina **nel proprio**; **copiarlo** nel proprio — una copia che da quel momento è sua — è il modo di divergere, e si costruisce quando serve davvero. La scrittura resta dove è: `Content.ManageTemplates` sul dipartimento che possiede il template. Nota: `decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`.
- **Pubblicazione e versioni**: ogni «Pubblica» crea una riga in `cms_content_versions` (fotografia di titolo e `body_json`); il sito pubblico legge **solo** la versione pubblicata.
- **Le pagine si approvano** (deciso il 13 set 2026, `decisions/2026-09-13-contenuti-centralizzati.md`): chi scrive una pagina la segna **pronta** (`status = ready`), e questo fotografa una **versione candidata**; chi ha **`Content.Approve`** sul dipartimento della pagina — Director e Web, cioè HQ e WD, e chi lo riceve per grant; **non** il coordinator del dipartimento — la pubblica esattamente com'è o la rimanda in bozza con una nota. Una modifica dopo «pronta» riporta in bozza. Chi ha `Content.Approve` pubblica direttamente le proprie. Ogni ripubblicazione ripassa dall'approvazione, e il pubblico vede la versione precedente finché non c'è. **News, documenti e template non si approvano**: li pubblica il dipartimento con `Content.Publish`. Quali `kind` si approvano è **configurazione** (`division.json → contentApproval`, `["page"]` per IT), letta dal servizio di pubblicazione: nessun authorization handler nuovo. Le notifiche («da approvare», «rimandata», «pubblicata») sono intenti del servizio del nucleo.
- **Blocchi Data: live o frozen, a scelta** (deciso da Carmine, come le sezioni derivate di vIPI). Ogni blocco *Data* porta `renderMode: live | frozen`. *Live*: la versione pubblicata interroga i dati al momento della lettura (un elenco «prossimi eventi» in una pagina di sezione). *Frozen*: alla pubblicazione il renderer cattura il risultato del blocco e lo salva nella versione (`frozen_json` accanto alle `props`), così un verbale o una policy fotografano i dati di quel giorno anche se la fonte cambia. Il template può fissare il `renderMode` di una sezione `locked`; alcuni blocchi sono **sempre live** per natura (`networkStats`: uno stato della rete congelato è un dato scaduto spacciato per attuale, la stessa regola del METAR in vIPI) e non espongono il toggle. Non si prende il ciclo AIRAC di vIPI: qui la «release» è la singola pubblicazione.
- **Rendering**: ogni `type` ha un componente React in `web/src/blocks/` costruito con Atmosphere; registry `type → componente`; blocchi sconosciuti rendono un avviso solo per lo staff. Lo schema dei blocchi vive **solo** in TypeScript/zod (§16.5): il backend tratta `body_json` come opaco (controlla `schema_version` e dimensione), estrae il testo per la ricerca con un walker generico delle stringhe e non replica lo schema in C#. Sanitizzazione di markdown ed `embed` (allowlist di host) in un solo componente. Per il SEO delle pagine pubbliche non si fa prerender (§16.11).
- **La dashboard di un dipartimento è una riga come le altre** (deciso il 5 set 2026, forma da confermare): ogni dipartimento nasce con la propria, seminata da un template, con visibilità `department`, e la modifica nello stesso editor. Non è una seconda macchina per comporre schermate: i widget restano le tile della dashboard **personale** `/me`, dove la composizione è per persona e non editoriale. Nota: `decisions/2026-09-05-dashboard-di-dipartimento.md`.
- **Una schermata per tipo di oggetto, non per dipartimento** (deciso il 13 set 2026): `/staff/content` elenca pagine, news, documenti e template di **tutti i dipartimenti che la persona raggiunge**, con `kind` e `department` come filtri nei search params; le voci del menu di un dipartimento portano alla stessa schermata già filtrata; alla creazione il dipartimento si sceglie fra quelli in cui si scrive. È un'estensione del motore di lista (§16.6), non una lista nuova, e vale uguale per `/staff/links` e `/staff/media`. Vedono tutto Director, Web, il superadmin e chi riceve un grant su ogni dipartimento.
- **Editor** (`/staff/content/{id}`, prima `/staff/{dept}/contents`): albero di sezioni e blocchi «a lista» con aggiungi / sposta su-giù / duplica / elimina, form proprietà generato dallo schema zod, anteprima nella stessa pagina, bozza/pubblicato, lingue affiancate nello stesso form («copia dall'altra lingua»). Niente canvas, niente drag libero (al più `dnd-kit` sulla lista, in un secondo momento). Le sezioni `locked` mostrano solo i campi da compilare.
- **Le pagine «di sistema»** (home, `/start`, `/pilots`, `/about`) sono righe `kind = page` seedate al primo avvio dai template con contenuto Lorem tradotto: chi forka le riempie dall'editor, mai dal codice.
- ~~**Confine con vIPI invariato**~~: caduto il 13 set 2026 (§9.4). Questo modello serve a qualsiasi contenuto.

### 9.4 Documenti per dipartimento

- Ogni documento è una riga di `cms_contents` con `kind = document` (§9.3) e ha `owner_department` obbligatorio, `category` (vocabolario per dipartimento: es. Training → syllabus, guide, materiale esami; Flight Ops → guide piloti, briefing; Membership → regolamenti, policy; HQ → verbali, policy divisionali), `visibility` (public / members / staff / department), **versioni** con changelog e data (`cms_content_versions`), corpo come file (PDF, `file_media_id`) **o** come contenuto a sezioni (§9.3), tipicamente da un template del dipartimento, per i documenti che conviene leggere nel browser.
- **Un documento è di qualsiasi natura** (deciso il 13 set 2026, `decisions/2026-09-13-staccarsi-da-vipi.md`): non sa di ATC, di aeroporti o di posizioni. Il vecchio «confine netto con vIPI» — SOP, LoA, vPIV e spazi aerei solo in vIPI, le sezioni piloti delle vSOP consumate via API in `/pilots`, la regola «se ha una FIR o un aeroporto come soggetto è vIPI» — è **caduto** insieme al montaggio: l'hub non consuma vIPI e la raggiunge con un link. Dove tenere una procedura è una scelta di contenuto della divisione, non un confine del codice. Della G14 restano il ciclo di vita (**archiviato**, **sostituito** dal successore), **«in vigore dal»**, la **data di revisione** con l'avviso al dipartimento e il **piè di pagina** con la stampa; il tipo SOP/LoA, le posizioni, ICAO, FIR e AIRAC si tolgono in una fase dopo il merge della pila #59–#64 (expand/contract).
- **I documenti e le news stanno nelle pagine per raccolta** (deciso il 13 set 2026, `decisions/2026-09-13-contenuti-centralizzati.md`). La **raccolta** allarga la categoria: un vocabolario per dipartimento (dato) e, sul contenuto, un **elenco** di raccolte invece di un valore solo. Una sezione documenti di una pagina è un blocco `documentList` (o `newsList`) che nomina dipartimento e raccolta; chi crea il documento sceglie in quali raccolte sta, e così in quali pagine compare — anche **più di una**. La pagina **tira**: nessuna tabella di collocazioni, perché saperle vorrebbe dire leggere le `props` dei blocchi. L'editor del documento mostra in sola lettura «compare in». Un documento nuovo in una pagina già approvata non ripassa dall'approvazione. Una pagina può elencare la raccolta di **un altro dipartimento** (una pagina TD che mostra le guide AOD): lo decide chi scrive la pagina, non chi scrive il documento (confermato da Carmine, 13 set 2026).
- Sul sito pubblico: `/documents` e `/news` **restano indici generali** (tutti i pubblici, filtrabili), con `/documents/{dept}`, gli indirizzi dei singoli contenuti e i blocchi `documentList`/`newsList` nelle pagine. Ricerca full-text sul titolo/sommario (MariaDB FULLTEXT), non sul PDF in prima fase.

### 9.5 Calendario unico

Deciso da Carmine: **un calendario per tutto** — eventi, RFE/RFO, training ed esami, tour, ma anche attività interne di divisione (riunioni staff, meeting di divisione, scadenze).

- Tabella `cms_calendar_entries` nel nucleo (§7). I moduli **non** scrivono direttamente: le loro entità (evento, sessione di training, leg di tour) implementano `IProjectable` e l'interceptor EF del nucleo crea/aggiorna/rimuove la voce con `source_module` + `source_id` **nella stessa transazione** (§16.4). Le voci interne (`meeting`, `deadline`, `other`) le crea lo staff a mano nel proprio spazio di dipartimento.
- `visibility` per voce: `public` (sito), `members` (dietro login), `staff` (tutto lo staff), `department` (solo il dipartimento proprietario). Una riunione dello staff Events è `department`; il meeting di divisione è `staff`; un RFE è `public`.
- Viste: `/calendar` pubblico (mese/settimana/agenda, filtri per `kind`), blocco `calendar` nelle pagine, dashboard `/me` (le mie voci: eventi a cui sono iscritto, sessioni, scadenze del mio dipartimento), feed **iCal** per utente con token (così finisce nel calendario personale) e per dipartimento.
- Orari sempre salvati in UTC, mostrati in UTC + fuso della divisione (standard IVAO).

### 9.6 Cosa NON entra (e cosa è opzionale)

| Cosa | Decisione |
|---|---|
| Onboarding come modulo | No: è la pagina `/start` a blocchi (Timeline + Card + CTA). Se un giorno servirà lo stato per utente (checklist "fatto/da fare" in `/me`), diventerà un piccolo modulo opzionale. |
| Test system (esami anti-AI) | **Sospeso.** Non è nel catalogo e non ha una data: rientra solo se Carmine lo ripropone, e in quel caso come app separata, estraendo allora l'auth dell'hub in una libreria condivisa (non prima: §16.9). |
| QuickOverview / Operations | Non esiste: confluito in vIPI (redirect 301 quando si spegne). |
| SES – Slot Events come modulo separato | Non replicato: gli eventi "a slot" (RFE/RFO) sono il `booking_mode` del modulo Events, non un modulo a parte. |
| ~~Virtual Airlines~~ | Ripescato (1° set, sera): registro VA dentro `flightops`, mostrato in `/pilots`. |
| ~~Testimonial~~ | Ripescato (1° set, sera): blocco `testimonial` nel set §9.3, contenuti di proprietà PR. |
| ~~Special Operations~~ | Ripescato (1° set, sera): modulo `specialops` opzionale, segnaposto (§9.2 riga 5). |
| ATC Scheduling, Forum, WebEye, Wiki, FPL | Solo link/embed: sono HQ. |
| Membership, PR, Web come moduli di codice | No: hanno il loro spazio nel back-office con i servizi comuni; un modulo opzionale nascerà solo per logica specifica (es. gestione soci, registro VA). |

Regola per il futuro: **tutto ciò che si aggiunge dopo questo catalogo è opzionale** (`modules.<nome>` in `division.json`, §4.2) — i quattro moduli di dipartimento obbligatori e il nucleo no. `specialops` è il primo modulo opzionale e fa da modello per i prossimi.

### 9.7 Contratti trasversali nucleo↔moduli (decisi il 1° set 2026, sera)

Regole che valgono per **ogni** modulo, presente e futuro — si scrivono una volta nel nucleo e si dettagliano nel documento di design di M0:

- **Maintenance**: con il modulo in manutenzione, i contenuti già pubblicati restano **visibili in sola lettura** (voci di calendario incluse); le *azioni* (prenotare, iscriversi, inviare un PIREP) rispondono 503 con pagina cortese e tradotta; i job del modulo vanno in pausa. Implementato nel nucleo, uguale per tutti.
- **Widget di dashboard** ~~ogni modulo registra i propri widget~~ **dal 13 set 2026 sono blocchi Data** (`decisions/2026-09-13-le-dashboard-a-tutto-schermo.md`): «le mie prenotazioni», «le mie richieste training», «i miei tour in corso» sono blocchi Data del modulo che rispondono per chi guarda, e `/me`, `/staff`, le dashboard dei dipartimenti e le pagine li compongono con l'editor; il registro dei widget sparisce. Stesso principio del registry dei blocchi: più il sito è flessibile, più è general purpose. I blocchi *Data* che dipendono da un modulo (`eventList`…) sono anch'essi registrati dal modulo, non cablati nel nucleo.
- **Dati di vIPI** (deciso il 14 set 2026, `decisions/2026-09-14-dati-condivisi-con-vipi.md`): due database sullo stesso server, **ogni dato ha un solo padrone** che lo scrive (l'hub: persone, permessi, contenuti, moduli; vIPI: aeroporti curati, settori, SOP, archivio delle sessioni ATC), e l'altro **legge viste `v_share_` di sola lettura** con un utente MariaDB dedicato, mai scritture incrociate. Nell'hub è un'**integrazione opzionale del nucleo** accesa da `division.json`: nessun modulo nomina vIPI, e senza vIPI il nucleo risponde con i dati IVAO o dichiara il dato non disponibile.
- **Notifiche**: servizio unico nel **nucleo** (mail ora, Discord in M6): i moduli pubblicano *intenti* di notifica, mai SMTP diretto — un cambiamento al servizio si fa in un punto solo. Preferenze per tipo di notifica in `/me/profile`.
- **Privacy dei membri**: l'hub **non ha un profilo utente pubblico**. L'unico profilo pubblico è quello ufficiale IVAO (`https://www.ivao.aero/Member.aspx?Id={VID}`): ovunque compaia un membro (classifiche tour, staff directory, partecipanti) si mostra il minimo necessario e si linka lì. Nessuna funzione di export dei dati utente (IVAO non la prevede); per il GDPR ci si allinea alle norme e alla privacy policy IVAO, e ogni modulo documenta nel proprio design cosa conserva di personale e per quanto (così una richiesta di cancellazione ha un percorso noto). **Il percorso esiste dal 25 set 2026** (T20b, piano 1.08, §16 punto 16): il superadmin cancella i dati di una persona; ciò che la riguarda va via, ciò che la divisione deve tenere (il registro disciplinare, il lavoro fatto come staff) resta sotto uno pseudonimo, e un ban in vigore resta con il VID finché non scade.
- **Ricerca globale**: indice centrale `search_index` nel **nucleo** (titolo, testo, tipo, url, dipartimento, visibilità), alimentato dai moduli via `IProjectable` con `source_module`+`source_id` — lo stesso pattern del calendario (§16.4). Matching, ranking e UI (⌘K e ricerca pubblica) vivono solo nel nucleo: un fix alla ricerca **non tocca i moduli**; un modulo si limita a dire "indicizza questo".
- **Proiezioni transazionali**: tutto ciò che i moduli proiettano nel nucleo (calendario, indice di ricerca, segnalazioni award) passa da un'unica interfaccia `IProjectable` — l'entità restituisce uno snapshot (titolo per lingua, url, dipartimento, visibilità, intervallo di tempo opzionale, testo per la ricerca, eventuale segnalazione award) e l'interceptor EF del nucleo fa l'upsert con chiave `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente bus di eventi, niente job di riconciliazione: non c'è nulla che possa restare a metà (§16.4). Gli eventi asincroni restano solo per le notifiche. **Dal 16 set 2026** (piano 0.79) lo snapshot porta anche **più voci di calendario**, gli **usi dei file con scadenza** e l'**apertura di un filo di contatti** (una volta sola, come la segnalazione award). ⚠️ **E la promessa vale davvero per i moduli solo da T4**: fino ad allora l'interceptor proiettava solo nel contesto del nucleo, e una riga di modulo saltava la proiezione in silenzio; T4 mappa le tabelle delle proiezioni nei contesti dei moduli, escluse dalle loro migrazioni, così la scrittura resta nella stessa transazione. **Fatto in T4a** (piano 0.81): e un contesto che non le mappa ora è un errore, non un salto. **Dal 16 set 2026** (T6a, piano 0.84) `Project()` riceve nel `ProjectionContext` anche l'**orologio** dell'host, perché un tour
è dello staff fino al rilascio e di tutti dopo; e una riga così si riproietta **senza essere scritta** (`ProjectionRefresh`), dal job
del rilascio dei tour — l'unica eccezione, dichiarata, a «niente job di riconciliazione».
- **Prodotti esterni con un contratto** (deciso il 15 set 2026, piano 0.79): **l'agente del validatore** (un'app sul PC dello staff, fuori dal repository) legge e scrive attraverso un contratto versionato nell'intestazione `Hub-Agent-Contract`, con un token personale (scritto in T19b, 24 set 2026: `/api/flightops/agent`, `docs/agent-contract.md`; un controllo ha un solo esecutore, il server o l'agente, e gli esiti dell'agente dicono sulla riga chi li ha mandati invece di andare nell'audit); **le fonti esterne** del nucleo (NOAA e VATSIM per il meteo, VATSpy per i confini dei FIR dal 16 set 2026) le nomina solo il nucleo, come IVAO (§4.2): un modulo non le nomina, e un test di architettura lo verifica. Una fonte esterna **si sceglie misurando i suoi dati**, non leggendo la sua documentazione: OpenAIP dichiarava i FIR e ne aveva 108 nel mondo.

**Contratto `IModule`** (confermato il 1° set 2026 dopo la verifica sui casi reali qui sotto; firma esatta nel design di M0): un modulo dichiara identità e dipartimento; rotte `/api/<modulo>` e voci di navigazione (pubblica e staff); le proprie migrazioni; il proprio catalogo permessi (`Events.Manage`…); widget di dashboard e blocchi pagina che fornisce; i propri job Quartz; le entità `IProjectable` (calendario, indice di ricerca, segnalazioni award) e gli intenti di notifica che pubblica; **dal 16 set 2026** anche le **preferenze** dei suoi membri (`Preferences`, T4b, piano 0.82) e le sue **impostazioni** (`Settings`, tenute dal nucleo in `hub_division_settings` e servite a `/api/modules/{key}/settings`, T5, piano 0.83). Regole dure: un modulo non referenzia **mai** un altro modulo (solo `Core`); il nucleo non referenzia i moduli (riceve solo contributi registrati); la comunicazione tra moduli passa dal nucleo.

**Collaborazioni tra moduli — i casi reali, e come rientrano nel contratto**:

| Caso | Soluzione |
|---|---|
| Events↔Training: training ed esami a calendario | Calendario unico: le sessioni di Training sono `IProjectable`, il nucleo crea le voci. Nessun contatto diretto. |
| ATC↔Events: quali settori/posizioni servono all'evento | Dato di riferimento: `ref_` (aeroporti, FIR) nel nucleo. ~~+ API pubblica di vIPI per le posizioni note dalle SOP~~: dal 13 set 2026 l'hub non consuma vIPI; il design di Events deciderà se le posizioni si scrivono sull'evento. |
| FlightOps↔Events: award "ATC/pilot event support", award legati a eventi | **Award nel nucleo** (§9.1). Non esistono meccanismi automatici di assegnazione: i moduli *segnalano* (FlightOps: "VID X ha completato il tour Y"; Events: eventi e partecipanti nel periodo, via calendario unico) e chi ha `Awards.Assign` (in IT: MD e HQ; configurabile per divisione) verifica e assegna a mano, con audit. |
| SpecialOps↔ATC: documenti degli aeroporti/avvicinamenti militari | **Riaperto il 13 set 2026**: senza vIPI dietro, i documenti SO sono documenti dell'hub come gli altri, e una pagina piloti li mostra elencando la raccolta SO (§9.4). Il design di `specialops` dirà se basta. Testo di prima — **Fonte unica in vIPI, viste per pubblico** (§9.4): il SOD scrive solo nelle vSOP, che già marcano ogni sezione per ATC/piloti/tutti; vIPI espone le sezioni piloti via API e l'hub le renderizza in `/pilots` e nella pagina SO (deep link finché l'API non esiste). Nessun meccanismo di co-proprietà documenti nell'hub. |
| Discord bot (M6) | Parla solo col servizio notifiche/API del nucleo, mai coi moduli. |
| Membership: vista d'insieme del membro nel back-office | Composizione dei widget registrati dai moduli, come `/me`. |
| Training↔ATC: posizioni per gli esami | `ref_` nel nucleo, come per Events. |

---

## 10. Integrazioni con le API IVAO

| Uso | Endpoint (v2) | Auth | Frequenza/cache |
|---|---|---|---|
| Profilo al login | `/users/me` | token utente | ad ogni login |
| Chi è online (ATC/piloti in area) | `/tracker/now/atc/summary`, `/tracker/now/pilots/summary` (o Whazzup v2 JSON) | client_credentials `tracker` | job ogni 30–60 s, cache |
| Prenotazioni ATC dell'evento | `/atc/bookings` (verificare path) | client_credentials | job ogni 5 min |
| Aeroporti della divisione (con piste) | `/airports/all?countryId={countryId}&includeRunways=true` | client_credentials | giornaliero, snapshot in `ivao_airports` |
| Posizioni ATC | `/atc/positions` | client_credentials | giornaliero |
| FIR/centri della divisione | `/centers?countryId={countryId}` (es. `?page=1&region=Europe&countryId=IT`) | client_credentials | giornaliero, snapshot in `ivao_centers` |
| Sessione live utente | `/users/me/sessions/now` | token utente, scope `tracker` | on demand |
| **Tour (M2)**: aeroporti del mondo, piste | `/airports/all`, `/airports/{icao}/runways` | client_credentials | giornaliero; piste per gli aeroporti delle leg e dei PIREP |
| **Tour (M2)**: tipi di aereo | `/aircrafts/all`, `/aircrafts/{icaoCode}`, `/aircrafts/{aircraftId}/variants`, `/aircrafts/manufacturers`, `/aircrafts/equipments`, `/aircrafts/transponderTypes` | client_credentials (da misurare in T1) | giornaliero |
| **Tour (M2)**: il volo del PIREP | `/tracker/sessions`, `/tracker/sessions/{id}/flightPlans`, `/tracker/sessions/{id}/tracks` | da misurare in T2 | all'invio del PIREP |
| **Tour (M2)**: METAR di ripiego | `/airports/{icao}/metar` (**minuscolo**) | client_credentials | ogni 30 min, solo se NOAA non risponde |

Tutto passa da `IvaoApiClient` (riuso/aggiornamento di `Ivao.It.IvaoApiSdk`), con log delle chiamate e circuit breaker: se `api.ivao.aero` è giù, l'hub degrada (widget "dati non disponibili"), non cade.

---

## 11. Ambiente di sviluppo, CI e deploy

### 11.1 Locale

- `docker-compose up`: MariaDB 11.4 (stessa minor della produzione) + Mailpit (SMTP finto con UI).
- `dotnet run` su `IvaoHub.Web` (`https://localhost:5001`) + `pnpm dev` su `web/` con proxy.
- OAuth in locale con le **credenziali di test di Carmine** in `config/ivao-oauth.json` (gitignored), con login/redirect URL su `localhost` come autorizzati per quelle credenziali. Se qualche endpoint API non è raggiungibile con le credenziali di test, mock dell'`IvaoApiClient` con fixture JSON.
- `dotnet ef migrations add` per modulo; seed di sviluppo con divisione IT + utenti finti con vari ruoli.

### 11.2 CI (GitHub Actions)

`build-test.yml`: restore → build .NET → test unit → test integrazione con Testcontainers MariaDB → `pnpm install/lint/typecheck/build` → genera client OpenAPI e verifica che sia allineato → artefatto `publish/` (`dotnet publish -c Release` con `wwwroot` popolato).
`release.yml` (su tag): crea la release GitHub con lo zip pronto per Plesk + note. Le divisioni che forkano ereditano la pipeline.

### 11.3 Deploy su Plesk — il modello di vIPI, riusato

La procedura ricalca quella già rodata per `atc.it.ivao.aero` (`deploy/atc-ivao/LEGGIMI-*.md`), perché il server e le persone sono gli stessi.

1. **Pacchetto**: `dotnet publish -c Release -r linux-x64 --self-contained` con `wwwroot` già popolato dalla SPA; asset minificati e precompressi `.br/.gz`; timbro di versione (`AssemblyMetadata` + commit) esposto su `/api/version`. Zip + foglio `LEGGIMI-PACCHETTO-x.y.z.md` con l'elenco dei file e i controlli post-deploy.
2. **Cartella dell'app** nella sottoscrizione `it.ivao.aero`, avviata da **Passenger** (`dotnet IvaoHub.Web.dll`), `ASPNETCORE_ENVIRONMENT=Production`. Struttura: `wwwroot/`, `config/division.json`, `config/ivao-oauth.json` (compilato dalla divisione), `secrets/<nome-non-indovinabile>.json` (connection string, SMTP, secret — l'app carica ogni `*.json` di `secrets/`), `hub-keys/` (Data Protection, **persistente, mai cancellare**), `uploads/` (documenti), `logs/`, `diagnostics/`.
3. **Direttive nginx aggiuntive** in Plesk: `deny all` su `secrets/`, `hub-keys/`, `diagnostics/`, `logs/`, `appsettings*.json`, `*.dll`, `*.pdb`, `*.json` alla radice; `Cache-Control: no-store` su `/api/*` (Cloudflare davanti). Verifica dall'esterno con `curl -I` dopo ogni cambio di hosting.
4. **Database**: DB + utente dedicati dal pannello (`GRANT ALL` sul solo schema, verificare che la prima migrazione con `ALTER DATABASE CHARACTER SET utf8mb4` passi); pool `MaximumPoolSize≤15` perché il tetto per utente è condiviso; `max_allowed_packet` confermato ≥ 4 MB o upload solo su disco.
5. **Migrazioni**: `Database.Migrate()` all'avvio (senza shell non c'è alternativa), con tre regole ferree: solo migrazioni **additive** (mai `DROP`/rename distruttivi nello stesso pacchetto che smette di usare la colonna → pattern *expand/contract* in due release), test CI che applica l'intera catena su una **MariaDB 11.4.10 vera**, e un `diagnostics/startup.txt` che dice quale migrazione ha applicato. Niente consegne con migrazioni nelle finestre in cui nessuno può ripristinare.
6. **Aggiornamento**: upload via FTP in **binario**, rimettere il bit di esecuzione all'eseguibile, non toccare `hub-keys/`, `secrets/`, `uploads/`; poi `tmp/restart.txt`. Sonda post-deploy (`/api/version`, `/health`, login, una pagina per modulo) eseguita **non** nel minuto del riavvio.
7. **Backup**: conferma scritta da Ivao.It su frequenza, retention, inclusione di `hub-keys/` e `uploads/` (non stanno nel DB) e un ripristino provato. Finché non c'è, si pianifica come se non ci fosse.
8. **Staging**: sottodominio dedicato nella stessa sottoscrizione, stesso pacchetto, credenziali OAuth di test con i propri login/redirect URL.

---

## 12. Migrazione e convivenza

Strategia **strangler**: l'hub nasce accanto ai siti esistenti, li sostituisce un modulo alla volta.

1. Hub online su un dominio temporaneo (es. `beta.it.ivao.aero`) con Content + auth + live status. Contenuti migrati dal Blazor (export manuale/script delle pagine).
2. Switch del dominio principale quando Content è completo; il vecchio sito resta raggiungibile in sola lettura per un periodo.
3. Events/Booking: **nessun import dello storico** (deciso): il modulo parte vuoto; `ivao-booking` resta acceso in sola lettura per un periodo concordato con lo staff Events, poi redirect 301 verso `/events`. Se in seguito servisse lo storico, lo script di import è un'aggiunta, non un prerequisito.
4. Flight Ops/Tour: i tour nascono nel nuovo modulo (i tour dell'anno successivo si creano direttamente nel tool); lo storico di `tours.th.ivao.aero` ⚠️ da decidere (import dei leg validati per le classifiche, o solo link al vecchio sistema).
5. Training: import di trainer, richieste e storico esiti da PATS **se** il DB è accessibile (⚠️ §15); periodo di doppia lettura, poi spegnimento.
6. Onboarding: i contenuti del wizard vengono riscritti come pagina `/start` a blocchi; redirect del sottodominio `welcome.it.ivao.aero`. QuickOverview: nessuna migrazione nell'hub (confluisce in vIPI), solo redirect 301 verso le pagine vIPI corrispondenti quando si spegne.
Ogni migrazione ha: script idempotente in `tools/migrate-<sorgente>/`, report di riconciliazione (conteggi prima/dopo), piano di rollback (il vecchio sistema resta acceso fino al go).

---

## 13. Roadmap proposta

| Fase | Contenuto | Uscita |
|---|---|---|
| **M0 — Fondamenta** ✅ **chiusa** (4 set 2026, `v0.1.0-m0`) | Repo, soluzione .NET, SPA Vite+Atmosphere, docker-compose, CI, `division.json`, i18n IT/EN, login OIDC BFF con credenziali di test, `users` + ruoli, layout pubblico/riservato, dashboard vuota; **la spina dorsale generica di §16** (`Localized<T>`, interfacce trasversali + interceptor + authorization handler, grammatica permessi, `IProjectable`, motore lista+form, endpoint di bootstrap) **dimostrata end-to-end** su `links` e su un primo `cms_contents` creato da template (§16.15) | Skeleton navigabile, login funzionante, meccanismi generici provati. Design: `01-design-m0.md`; fasi: `02-piano-implementazione-m0.md`. Il **deploy su staging Plesk** è spostato a M1 (deciso 2 set 2026: attende le risposte A9). Demo da eseguire a mano: `tools/demo-m0.md`; revisione finale: `decisions/2026-09-04-m0-review.md` |
| **M1 — Sito pubblico** | Nucleo editoriale: pagine a blocchi (**set completo dei blocchi del nucleo**, 22 nuovi), news, documenti per dipartimento con vocabolario delle categorie, calendario unico con UI (con sole voci interne per ora), media library, contatti + servizio notifiche, staff directory, live status; **menu editoriale**; pagine di sistema seedate (`/start`, `/pilots`, `/atc`, `/about`, home); back-office per dipartimento; schermata di ricerca; ~~modulo `atc` come sezione `/atc` con deep link a vIPI~~ `/atc` pagina di sistema e un link ad `atc.it.ivao.aero` (13 set 2026); SEO minima; migrazione contenuti dal Blazor **a mano dall'editor**. Il **giro e2e contro l'API vera** è la prima fase. Design: `03-design-m1.md`; fasi: `04-piano-implementazione-m1.md` (G0–G12). **Aggiunto il 13 set 2026**, prima di M2, nelle fasi **G16–G20** di `04-piano-implementazione-m1.md`, che partono dopo il merge della pila #59–#65: G16 via vIPI (modulo `atc`, metà ATC della G14); G17 una schermata per oggetto; G18 l'indirizzo composto; G19 l'approvazione delle pagine; G20 le raccolte e l'indice derivato (§9.3, §9.4) | Sostituisce `it.ivao.aero` |
| **M2 — Tour** (era «Eventi» fino al 13 set 2026, nota `ordine-dei-moduli`) | **Prima di tutto, i moduli fuori dai dipartimenti** ✅ (H1–H3) (deciso il 13 set 2026, nota `moduli-non-subordinati-ai-dipartimenti` §4): i grant a una posizione con il seed da `division.json`, `IOwnedByDepartment` a insieme con i test della spina dorsale, `modules.<key>.baseDepartment`, la sezione del modulo nella barra dello staff. **Poi le due dashboard personali** ✅ (D1–D3, deciso l'11 set 2026, piano 0.59, nota `le-dashboard-a-tutto-schermo`): `/me` e `/staff` fatte di blocchi Data, via il registro dei widget. Poi **due metà, e si fanno in quest'ordine** (deciso il 9 set 2026). **(a) Il modulo Flight Ops** (`flightops`, la sezione Tours), che parte da `05-design-m2.md` (chiuso il 15 set 2026): tour, leg, PIREP, validazione con controlli automatici che suggeriscono, contestazioni e chiarimenti, ban, segnalazioni award, voci nel calendario; **mai classifiche**; design ereditato da `Ivao Italy Toursystem`. Coordinator e assistant del FOD hanno tutte le funzioni, gli advisor quello che decide il design (piano 0.77). **Le fasi sono T0–T21** in `06-piano-implementazione-m2.md` parte C (piano 0.79): T1–T4 nel nucleo, T5–T20 nel modulo, T21 nell'app del validatore fuori dal repository. **(b) Il primo pacchetto self-contained e il deploy su staging Plesk** (foglio `LEGGIMI`), spostato qui da M1 il 5 set 2026: aspetta le risposte A9 (§15.2c) **e** la persona che carica su Plesk, che al 9 set non è disponibile | I tour IT lasciano `tours.th.ivao.aero` |
| **M3 — Training** (era M4) | Modulo Training: richieste, trainer, disponibilità, sessioni, esiti, mock exam, group training, import storico se possibile. Coordinator e assistant del TD hanno tutte le funzioni. **Lo scrive `dalberone`** dal 24 set 2026, prima il design (nota `un-secondo-sviluppatore`) | Spegne `training.ivao.it` |
| **M4 — Eventi** (era M2) | Modulo Events: eventi, slot RFE/RFO, booking, partecipanti, notifiche mail, voci nel calendario unico, blocco Data `eventList`, back-office Events. Nessun import. Coordinator e assistant dell'ED hanno tutte le funzioni; un dipartimento in collaborazione modifica ma **non cancella** (piano 0.77, da precisare nel design) | Spegne `ivao-booking` |
| ~~**M5 — vIPI dentro l'hub**~~ **sospeso il 13 set 2026** | **Fuori dalla roadmap, senza data** (`decisions/2026-09-13-staccarsi-da-vipi.md`): l'hub linka `atc.it.ivao.aero`. Il numero M5 resta libero perché M6 non cambi nome. Testo di prima: allineamento TFM (il ramo **net10 + EF 9 + Pomelo 9** di vIPI, lavoro nel suo repository), montaggio in-process sotto `/services/vsop`, `atc.it.ivao.aero` → redirect, spegnimento di `quickoverview.ivao.it` (già confluito in vIPI). ⚠️ Fino ad allora l'indirizzo è servito **per proxy** dalla vhost che esiste: il lettore vede un sito solo da subito (decisione del 7 set 2026) | Un solo sito ATC+hub |
| **M6 — Ecosistema** | API interne per il bot Discord, iCal, prerender SEO, primi moduli opzionali se richiesti, `FORKING.md` rifinito, prima divisione pilota che forka | Prodotto divisionale |

M5 è sospeso (13 set 2026); nulla in M1–M4 ne dipendeva. **L'ordine è Tour → Training → Eventi** (deciso da Carmine il 13 set 2026 dopo il confronto con lo staff di IVAO, `decisions/2026-09-13-ordine-dei-moduli.md`); sostituisce Events → Tour → Training del 1° set 2026. Il tour resta il più avanti, con design e validatore già scritti. Per ora **nessun altro modulo** entra in roadmap. **Due staffisti in parallelo, deciso il 24 set 2026** (`decisions/2026-09-24-un-secondo-sviluppatore.md`, chiude la nota del 13 set §3.5): **M3 lo scrive `dalberone`** con il suo Claude Code, su branch del repository e una PR per fase, a partire dal design (`07-design-m3.md`, poi `08-piano-implementazione-m3.md` e `HANDOFF-M3.md`), mentre Carmine chiude M2; **su `main` unisce solo Carmine**, dopo la revisione del suo Claude (`CLAUDE.md` §0).

Ogni modulo dopo M0 riceve il proprio breve documento di design (modello dati, schermate, permessi, migrazione) prima del codice, come per M0 stesso.

---

## 14. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| Login URL / redirect URL registrati su IVAO diversi da quelli configurati | Validazione all'avvio del JSON; checklist di go-live che confronta i due URL con quelli registrati; staging con URL propri registrati a parte. |
| Scope `training` non implementato lato IVAO → esiti teorici non leggibili via API | Inserimento manuale/CSV dallo staff training; astrazione `ITheoryExamSource` per sostituirla quando l'API arriva. |
| Pomelo senza release per EF Core 10; vIPI in produzione su net8 che esce dal supporto il 10 nov 2026 | Hub: EF Core 9 + Pomelo 9 su runtime .NET 10. vIPI: verificare se il ramo net10 può usare EF Core 9 + Pomelo 9 (sblocca sia l'EOL sia il montaggio in-process); altrimenti attendere Pomelo 10. Decisione unica per i due progetti. |
| Migrazioni che girano da sole all'avvio senza possibilità di ripristino | Regola *additive-only / expand-contract*, test su MariaDB 11.4.10 vera in CI, finestre di consegna concordate, backup confermato prima del primo dato reale. |
| Segreti/chiavi esposti o persi via FTP (è già successo a vIPI il 24–25 ago) | Cartella `secrets/` con file dal nome non indovinabile, deny nginx verificato con `curl -I`, `hub-keys/` nel foglio "non cancellare", rotazione credenziali a ogni sospetto. |
| Tetto connessioni MariaDB condiviso con vIPI | Pool ≤ 15, `ConnectionIdleTimeout` basso, query brevi, cache in memoria per le letture calde. |
| Plesk: limiti del proxy (timeout, dimensione upload) | Niente SignalR nella prima fase (polling per live status); upload documenti con limite esplicito; test su staging Plesk fin da M0. |
| Cambi delle API/OAuth IVAO (il README avvisa di messaggi d'errore in cambiamento) | Client isolato, contratti in un solo posto, test con fixture; iscrizione ai canali dev IVAO. |
| Un solo manutentore | Documentazione nel repo, ADR per ogni decisione, CI che blocca regressioni, dipendenze minime, niente magia. |
| Forkabilità che si erode | Test "divisione XX" in CI; review checklist nel template PR. |
| GDPR (dati personali di membri, email, discord id) | Minimizzazione scope, retention, export/cancellazione, privacy policy divisionale. |
| Fonti esterne dei tour (M2) che cambiano licenza o spariscono: OpenAIP, NOAA, Navigraph per l'agente | Ognuna dietro un'interfaccia del nucleo, con «non disponibile» e mai «fallito» quando manca; la licenza di OpenAIP va letta a mano, e prima di distribuire l'agente si chiede a Navigraph per iscritto (note del 15 set 2026). |
| La mappa di base dei tour (179 MB) non caricata, o servita senza richieste parziali da Passenger | La mappa disegna leg e aeroporti su un fondo neutro; ✅ in sviluppo `/tiles/basemap.pmtiles` risponde **206 con un `ETag` forte** (T10, `PublicTourTests`), su Passenger lo dirà lo staging. ⚠️ **Le build di Protomaps durano circa una settimana**: chi rifà l'archivio passa una data recente a `tools/basemap.mjs`. |

---

## 15. Decisioni aperte ⚠️

1. ~~Quali moduli inglobare e in che ordine~~ **Deciso il 1° set 2026** (§9, §13): nucleo editoriale + `events`, `flightops`, `training` (`atc` tolto il 13 set 2026); ordine Events → Tour → Training; ~~vIPI montato appena il TFM lo consente~~ vIPI sospeso, raggiunto con un link (13 set 2026); test system sospeso.
2. ~~**vIPI nell'hub — quando e come**~~ **Sospesa il 13 set 2026** (`decisions/2026-09-13-staccarsi-da-vipi.md`): l'hub linka `atc.it.ivao.aero` e non monta vIPI; la domanda torna solo se Carmine la ripropone. **Dal 14 set 2026 i due siti condividono i dati senza copiarli** (`decisions/2026-09-14-dati-condivisi-con-vipi.md`): due database, un padrone per ogni dato, viste di sola lettura; da verificare sul server l'utente MariaDB dedicato. Testo di prima: il montaggio in-process è la destinazione (§9 riga 7b), il nodo è il TFM. Da verificare in vIPI: può il ramo `net10.0` di `Vipi.Infrastructure` usare EF Core 9 + Pomelo 9 invece di EF Core 10 (le 65+ migrazioni sono generate con EF 10 ma applicate anche da EF 8 — con EF 9 dovrebbero passare)? Se sì, si sblocca insieme l'EOL di net8 e il montaggio. Decidere anche il dominio finale della parte ATC (`it.ivao.aero/services/vsop` con redirect da `atc.it.ivao.aero`, o viceversa proxy).
2b. ~~Tour system e test system~~ **Deciso**: il tour system è il modulo `flightops` nel monorepo dell'hub (repo separato chiuso, design confluisce). Il test system è sospeso; se tornerà, sarà app separata (auth estratta in libreria solo allora).
2d. ~~**Storico tour**: importare i leg validati da `tours.th.ivao.aero` per le classifiche, o partire da zero come per gli eventi?~~ **Chiusa il 15 set 2026** (`05-design-m2.md` §0.2, piano 0.79): nessun import; il sistema entra in uso con la stagione 2027, e le classifiche non esistono.
2c. **Hosting dell'hub** (blocca **la seconda metà di M2**, il deploy, non il modulo Events: diviso
    il 9 set 2026 — e da quel giorno il deploy aspetta anche la persona che carica su Plesk): chiedere a Ivao.It (stesse domande A9 di vIPI, già scritte): dove sta la cartella dell'hub nella sottoscrizione, se il document root può essere diverso dalla cartella dell'app, privilegi dell'utente DB, `max_allowed_packet`, `sql_mode`, backup con retention e ripristino provato, se esiste un sottodominio di staging.
3. **Dominio di staging** e nomi finali (`beta.it.ivao.aero`?), perché login URL e redirect URL vanno registrati su IVAO per ogni ambiente.
4. ~~Editor contenuti~~ **Deciso**: pagine a blocchi con editor a lista (§9.3); il blocco `text` usa markdown con anteprima. Prerender SEO: **no per ora** (§16.11).
5. ~~Licenza del repository pubblico~~ **Decisa il 3 set 2026**: **Apache-2.0**, copyright «2026 Carmine Granato». Nota in `docs/internal/decisions/2026-09-03-licenza.md`.
6. ~~Prefisso lingua negli URL~~ **Deciso: no per ora** (§16.11); lingua da profilo → cookie → `Accept-Language`.
7. **Accesso ai DB esistenti** (PATS, sito Blazor) per stimare le migrazioni — `ivao-booking` non serve più (nessun import). Chi mantiene PATS oggi?
7b. ~~Roster completo dello staff~~ **Deciso il 2 set 2026**: non esiste un endpoint IVAO per il roster; il roster dell'hub è **chi ha fatto login almeno una volta** (§16.13).
8. **Vocabolario delle categorie documenti per dipartimento** (§9.4): da definire con ogni coordinatore prima di M1.
9. **Feed iCal e notifiche del calendario unico**: per utente con token, per dipartimento, o entrambi; e se le voci `department` devono generare mail/Discord.
10. **Contenuto del modulo `specialops`** (§9.2 riga 5): da definire con il dipartimento SO (presentazione? arruolamento con workflow? missioni/attività?). Finché non si decide, il modulo resta un segnaposto senza tabelle.
11. ~~Contratto `IModule`~~ **Confermato** (1° set 2026, §9.7); firma esatta, snapshot di `IProjectable` e registry (widget/blocchi) **scritti** in `01-design-m0.md` §3.6 e §6.

---

## 16. Meccanismi generici — decisi il 2 settembre 2026

Criterio di Carmine: **quanto meno codice possibile; un pezzo usato in due punti si scrive una volta, mai due**. Il catalogo di §9 dice *cosa* costruire; questa sezione dice *con quali pezzi generici*, perché se non nascono in M0 verranno riscritti in M1 per pagine, news, documenti, calendario, link, partner, FAQ e poi in ogni modulo. Ogni punto è deciso; le firme sono in `01-design-m0.md`.

**A. La spina dorsale che M0 deve contenere**

1. **Traduzioni**: nessuna tabella `*_translations`. Ogni campo tradotto è una colonna JSON `{ "it": …, "en": … }` mappata su un value object `Localized<T>`; un converter EF, un componente React `LocaleFields`, un validatore «tutte le lingue di `division.locales` prima di pubblicare». Si perde il FULLTEXT sul titolo (la ricerca passa da `search_index`, che ha le sue colonne per lingua).
2. **Colonne trasversali come interfacce**: `IOwnedByDepartment`, `IVisible`, `IPublishable`, `IAuditable`. Un `SaveChangesInterceptor` compila audit e timestamp; un global query filter applica `visibility` all'utente corrente; **un solo** authorization handler confronta posizioni ∪ grant dell'utente con l'`owner_department` della risorsa. Nessun modulo riscrive «può modificare questa riga?». **Dal 16 set 2026** (piano 0.79) lo stesso handler conosce anche lo **scope per risorsa** di un grant (`IHasResourceScope`), l'**interessato** a cui una riga nega dei permessi (`IHasStakeholder`) e i **partecipanti** a cui ne concede la lettura e la risposta (`IHasParticipants`): tre relazioni della riga, nessun ramo che nomina un modulo.
3. **Grammatica dei permessi**: `<Area>.<Azione>` (`Content.Edit`, `Content.ManageTemplates`, `Content.Approve` — dal 13 set 2026, Director e Web ovunque più i grant, §9.3 —, `Events.Manage`, `Training.Assign`…) con lo scope di dipartimento **implicito** dalla risorsa. I moduli aggiungono nomi al catalogo, non handler. Fanno eccezione i permessi senza risorsa dipartimentale (`Permissions.Manage`, `Awards.Assign` configurato per divisione).
4. **Proiezioni via `IProjectable`**: calendario, indice di ricerca e segnalazioni award sono proiezioni dello stesso interceptor, upsert con `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente MediatR (licenza commerciale dal 2025), niente bus, niente job di riconciliazione. Eventi asincroni solo per le notifiche. **Dal 16 set 2026** (piano 0.79, M2) le proiezioni sono cinque: ricerca, calendario (**più voci per riga**), segnalazioni award, **usi dei file con scadenza** (riscritti per intero come ricerca e calendario) e **apertura di un filo di contatti** (una volta sola, come l'award); e da T4 valgono anche nei contesti dei moduli (§9.7) — **fatto il 16 set 2026** (T4a, piano 0.81). Una riga non pubblicata tiene solo i suoi usi dei file. **Dal 16 set 2026** (T4b, piano 0.82) la segnalazione award porta anche l'award che la riga propone. **Dal 16 set 2026** (T6a, piano
0.84) `Project()` sa che ore sono (`ProjectionContext.Clock`), e una riga la cui proiezione cambia con il tempo si **riproietta senza
essere scritta** con `ProjectionRefresh`: l'unico che lo fa è il job del rilascio dei tour, eccezione dichiarata a «niente job di
riconciliazione» (nota `2026-09-16-i-tour-nel-back-office`).
5. **Un solo documento a sezioni** (§9.3): editor, renderer e registry dei blocchi unici per pagine, news, documenti e per i corpi testuali dei moduli. Schema **solo** in TypeScript/zod; il backend tratta il JSON come opaco (`schema_version` + dimensione), estrae il testo per la ricerca con un walker generico delle stringhe; sanitizzazione markdown/`embed` (allowlist host) in un solo componente.
6. **Un solo motore di back-office**: lista generica su `DataTable` Atmosphere guidata da una configurazione di colonne + form generato dallo schema zod (lo stesso dei blocchi) anche per le entità; lato server un helper `MapCrud<TEntity, TDto>` che porta già la policy di dipartimento. **Dal 16 set 2026** (T4a, piano 0.81) una lista può mappare **una pagina di righe in una volta** (`CrudOptions.ToListPage`) quando la riga mostra un fatto di altre tabelle — il primo è la data di eliminazione di un file — con una query per pagina, mai una per riga. **Dal 16 set 2026** (T6a, piano 0.84) una risorsa può chiedere per l'eliminazione una
policy **in più** di quella di scrittura (`CrudOptions.DeletePolicy`): chi modifica un tour non è per forza chi lo elimina. Regola: **valida
il server, il client mostra** i `ProblemDetails` campo per campo. **L'unica eccezione dichiarata** è l'editor delle leg dei tour (M2, T7a, piano 0.85): una tabella dove ogni riga si salva da sola e inserire o togliere rinumera le altre, con sei verbi scritti a mano anche lato server (nota `2026-09-18-le-leg-dei-tour`).
   Quando una risorsa non rientra, si estende `CrudOptions` e mai il motore con un ramo che la nomina: oggi può dire che una riga si scrive solo con un permesso in più (`ExtraWritePolicy`), che non ha una create JSON (`MapCreate`), che cosa significa cancellarla (`Delete`), che accetta un filtro che è una domanda invece di un confronto su una colonna (`CustomFilters`) e — **dal 21 set 2026** (T7b, piano 0.86) — che cosa una riga prende da un'altra prima che il suo permesso venga chiesto (`BeforeAuthorize`: le righe figlie di un tour ne prendono la cura, nota `2026-09-21-la-forma-dei-tour`) e — **dal 23 set 2026** (T15a, piano 0.98) — che cosa segue una scrittura salvata (`AfterSave`: la mail di un ban, nota `2026-09-23-completamento-validatori-piloti-ban`). **Una schermata CRUD scritta a mano non si accetta**, e un endpoint scritto a mano accanto al motore è un evento da scrivere nel rapporto di chiusura della milestone. **Eccezione dichiarata di M2** (piano 0.79): l'**editor delle leg a tabella** (`LegGrid`, `05-design-m2.md` §8.4), perché comporre trenta leg una per volta in un form non si regge; salva comunque riga per riga con `row_version` e mostra i `ProblemDetails` sulla cella.
7. **Un solo endpoint di bootstrap** (`/api/me`): menu pubblico e staff, moduli abilitati / in maintenance, permessi effettivi, widget e blocchi registrati. La SPA non ha nulla di cablato.
8. **Un solo set di file di lingua** `locales/{lang}/*.json`, letto sia dalla SPA sia dal backend (mail, errori). Niente `.resx`.

**B. Cose tagliate o accorpate**

9. **Un solo progetto per il nucleo**: `IvaoHub.Core` (dominio + EF + client IVAO + `Content` come cartella) + `IvaoHub.Web` + un progetto per modulo. Niente `IvaoHub.Infrastructure` (interfacce e implementazioni in coppia sono codice doppio per costruzione: Clean Architecture ha senso alla scala di vIPI, non qui), niente `IvaoHub.Auth` finché il test system resta sospeso. Il confine compile-time che conta è tra i moduli e il nucleo.
10. **Niente `/api/v1`**: frontend e backend viaggiano nello stesso pacchetto. L'unico cliente che non viaggia con il pacchetto è l'**agente del validatore** (M2): il suo contratto si versiona nell'intestazione `Hub-Agent-Contract`, non nell'indirizzo (nota `2026-09-15-token-personali-e-agente-del-validatore`); senza intestazione, o con una versione che l'hub non parla, 400 con le versioni accettate (T19b, `docs/agent-contract.md`).
11. **Niente prefisso lingua negli URL e niente prerender SEO**, per ora: le pagine pubbliche sono poche e Google renderizza le SPA (§15.4, §15.6 chiuse).
12. **DbContext per modulo** con tabella `__EFMigrationsHistory_<modulo>` separata (su MariaDB gli «schemi» sono solo prefissi) e **nessuna FK tra contesti**: solo colonne `vid`/`airport_icao` non vincolate.

**C. Convenzioni UI — da trattare nel design di M0, prima della prima schermata** (concordato il 2 set 2026)

Il problema noto (un pezzo nuovo che arriva con un design diverso dal resto della pagina) si risolve prima di tutto **per costruzione**: ogni schermata di back-office passa dal motore lista+form (punto 6) e ogni contenuto dal renderer dei blocchi (punto 5), quindi un design divergente non ha dove entrare. Le convenzioni coprono il residuo. Nel design di M0 si fissano: (a) il **set di icone** unico — **`lucide-react`, confermato** il 2 set 2026: è già una dipendenza di `@ivao/atmosphere-react` 3.1.0 — con la regola «se manca un'icona si cerca prima nel set; se proprio non c'è si aggiunge in `web/src/shared/icons/` nello stesso stile, mai inline nella schermata»; (b) l'**elenco chiuso dei componenti custom** oltre Atmosphere (§8.3): un pezzo nuovo si compone da quelli, non si scrive da zero, e aggiungerne uno è una decisione esplicita; (c) una pagina **`/staff/admin/ui-kit`** che mostra tutti i componenti e i blocchi in uso: riferimento vivo e test visivo quando si aggiunge qualcosa. Le regole finiscono in `docs/UI-GUIDELINES.md` (inglese, valgono anche per chi forka). Le convenzioni **dei blocchi** (spaziature tra sezioni, varianti di sfondo, resa di una sezione `locked` nell'editor) si discutono in **M1**, con il set di blocchi davanti. ✅ **Chiuso il 6 settembre 2026 con G3 di M1**: i 21 blocchi esistono e le convenzioni sono scritte in `docs/UI-GUIDELINES.md`, sezione «The conventions every block follows» — la spaziatura e lo sfondo sono della sezione e mai del blocco, quattro sfondi (`none`, `muted`, `accent`, `image` con `mediaId`) — **sette dall'11 settembre 2026**, con i tre fondi scuri `brand`, `deep`, `dark` disegnati nel tema scuro e ancora nessun colore libero (changelog 0.58), e **otto dal 12 settembre** con `aurora`, mentre `accent` è diventato l'azzurro `ocean-50` invece del quarto grigio che era (changelog 0.67) —, quattro larghezze, l'**accento di un blocco** su un insieme chiuso di quattro famiglie del brand, disegnato sui grafici (un'icona, un filetto, una barretta) e mai sotto una parola, la resa di una sezione `locked`, il blocco sconosciuto visibile solo allo staff, l'icona dichiarata dal tipo, nessuna stringa che non sia prosa dentro `props`, nessun blocco che contiene blocchi, e l'allowlist degli host per i riquadri.

**D. Buchi chiusi**

13. **Roster dello staff** (deciso da Carmine): non esiste un endpoint IVAO per l'elenco delle posizioni di una divisione; il roster dell'hub è **chi ha fatto login almeno una volta**. Staff directory, sospensione dei grant e scelta dei VID a cui proporre un grant si basano su quello.
14. **Perdita di `hub-keys/`**: oltre al logout di tutti, i token in `user_tokens` diventano illeggibili; il codice li tratta come assenti e forza il re-login, mai un'eccezione.
15. **Definizione di «fatto» per M0**: la spina dorsale esiste ed è dimostrata end-to-end su un'entità banale — `links`: localizzata, con dipartimento, visibilità, audit, CRUD via motore lista+form, proiettata nella ricerca — e su un primo contenuto di `cms_contents` creato da un template. Se passa, news e documenti in M1 sono configurazione più che codice.
16. **La cancellazione dei dati di una persona** (T20b, piano 1.08, nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`): un
    solo percorso, del superadmin (`/api/admin/erasure/{vid}`, pannello nella pagina dei permessi). Ogni modulo che tiene dati **su**
    una persona registra un `IPersonalDataEraser` (cancella o svuota le sue righe su di lei); il nucleo fa il resto per tutti: le sue
    righe (utente, accessi, notifiche a lei e su di lei, preferenze, award, fili aperti da lei), lo **pseudonimo** — un numero negativo
    nuovo, senza tabella di corrispondenza — in ogni colonna che per **convenzione** nomina una persona (`vid`, `*_vid`, `*_by`,
    `PersonColumns`), e l'audit (copie delle righe cancellate svuotate, il VID sostituito ovunque, una riga `erasure`). L'interceptor ha
    una **modalità cancellazione**: niente timbri, e una riga cancellata non si ricopia nell'audit. Un modulo nuovo non scrive niente per
    le colonne che seguono la convenzione; una colonna di persona con un altro nome sfuggirebbe, e il test
    `TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` è l'elenco con cui la revisione la confronta. Una riga che il modulo tiene
    **apposta** con il VID — un ban in vigore — la passa a `ErasureRequest.Keep`, e il nucleo non la tocca (piano 1.09).

**E. Come si cambia il sistema mentre si scrive codice** (concordato il 2 set 2026)

Durante il codice emergerà spesso che «serve altro». Il modello regge i cambi in corsa solo se, **prima di scrivere una riga**, la richiesta viene classificata:

- **(a) È un dato o una configurazione** — una sezione o un blocco in un template, un seed, una colonna di lista, una chiave i18n: si fa dentro il task, senza cerimonie.
- **(b) Rientra in un meccanismo generico esistente** — `IProjectable`, `MapCrud`, registry di blocchi/widget, `Localized<T>`, authorization handler: si usa quello. Se il meccanismo non copre il caso al 100 %, **si estende il meccanismo**, mai lo si aggira con un caso speciale.
- **(c) Serve un meccanismo nuovo o una funzione nuova di modulo**: ci si ferma. Nota di design breve (mezza pagina in `docs/internal/decisions/` o un paragrafo nel design del modulo: cosa serve, perché nessun meccanismo esistente basta, cosa si tocca), decisione insieme, poi aggiornamento del piano. Il task originale si chiude senza quella parte o resta aperto: **non si chiude «a qualunque costo»**.

Reti di sicurezza: la **checklist del template PR** («ho aggiunto una tabella `*_translations`, un handler di autorizzazione, un fetch a mano, una lista o un form non generati, un componente UI fuori dall'elenco? Se sì, perché?») e i **test della spina dorsale** di M0, che rompono la build se si bypassa l'interceptor o l'authorization handler. Le regole operative complete, nella forma che Claude Code legge a ogni sessione, stanno in **`CLAUDE.md`** alla radice del repository. **Dal 24 settembre 2026 è pubblico, in inglese e versionato** (`decisions/2026-09-24-un-secondo-sviluppatore.md`): lo leggono le sessioni di Carmine e quelle di ogni collaboratore, e il suo §0 dice chi unisce, che cosa un collaboratore non tocca e come ottiene una decisione; `CONTRIBUTING.md` ne è la metà pratica (una fase, i test, le trappole già pagate). Le istruzioni personali di Carmine e la procedura di revisione stanno in **`CLAUDE.local.md`**, privato e fuori dal repository come `.claude/`. Fino al 24 settembre `CLAUDE.md` era un file privato di Carmine, in italiano. Terza rete di sicurezza, accanto al template e ai test della spina dorsale: il check **`core-guard`**, che su una PR di un collaboratore ferma i file riservati al maintainer e chiede una nota nuova per ogni file del nucleo.

---

## 17. Fonti consultate

- Sito attuale: https://it.ivao.aero/ (home, /about, /pilots, /atc, /events, /special-ops, /events/calendar)
- Atmosphere: https://github.com/ivaoaero/atmosphere (README, `brand/README.md`, `brand/src/tokens.json`, `components/react/package.json`, `components/react/UPGRADE.md`, `src/styles/theme.css`, componenti) — docs https://ivaoaero.github.io/atmosphere/main
- OAuth-samples: https://github.com/ivaoaero/OAuth-samples (README, `php-pure`, `nodejs-pure`, `aspnetcore7/Ivao.OpenIdConnect`, `reactjs-with-lib`, `laravel-pure`)
- Scope OAuth IVAO: https://wiki.ivao.aero/en/home/devops/api/oauth-scopes
- Org GitHub IVAO Italy: https://github.com/ivao-italy (repos `ivao-booking`, `onboarding`, `Ivao.It.IvaoApiSdk`, `Ivao.It.WhazzupData.SDK`, `discord`)
- Servizi satellite: https://training.ivao.it/ , https://quickoverview.ivao.it/ , https://atc.it.ivao.aero/ (vIPI ATC Services)
- Template HQ per i siti divisionali: https://va.ivao.aero/ (IVAO Vatican; `assets/css/themes/ivao-classic.css`, `frontend.css`) e il suo backend https://va.ivao.aero/backend/ v3.7.6 (dashboard, Page Builder ed editor, LoA/SOP, TDCenter, Tourcenter, Events — visto con login staff il 1° set 2026)
- Repository vIPI (`D:\Programmazione\IVAO_Test\vIPI Ivao Italy\vIPI Ivao Italy`): `README.md`, `HANDOFF.md`, `docs/guide/integration.md`, `docs/lavori-aperti.md` §A (cutover MariaDB, A9 domande hosting), `deploy/atc-ivao/LEGGIMI-DEPLOY.md`, `LEGGIMI-SEGRETI.md`, `appsettings.Production.json`, `src/Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs`
- Plesk: https://support.plesk.com/hc/en-us/articles/12377600431511-ASP-NET-Core-support-in-Plesk , https://support.plesk.com/hc/en-us/articles/12376965359511-Does-Plesk-support-Next-JS , https://support.plesk.com/hc/en-us/articles/12377519856023-Which-NET-versions-are-supported-by-Plesk
- Pomelo EF Core MySql: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/releases , https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql
