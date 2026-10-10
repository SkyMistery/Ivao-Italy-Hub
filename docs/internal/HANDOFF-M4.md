# HANDOFF — stato di M4 (Events)

> Documento **interno** (italiano). È il punto d'ingresso di ogni sessione che lavora al modulo Events. Lo scrive **la sessione
> di lavoro** di ogni fase, alla fine, con un paragrafo «Che cosa ha lasciato <fase>» in cima alla sezione «Lo stato». Lo stato
> generale del progetto sta in `HANDOFF.md`, che scrive solo il master. Le regole — chi unisce, che cosa non si tocca, come si
> ottiene una decisione — sono in `CLAUDE.md` §0 e in `10-piano-implementazione-m4.md`, «Regole di tutte le fasi», e non si
> ripetono qui.

**Ultimo aggiornamento:** 11 ottobre 2026 — **fase E10k** (nucleo: le parole delle liste e i titoli delle schede), sul branch
`m4/e10k-lists-and-titles`, **PR #238** verso `main`, nata da `main` a `0f72737`, senza coda, e **unita di nuovo a `main`** a `aa3707a`
(la #241, 0.6.6: il test del meteo corretto), a `ba06d66` (E6a, E6b, il piano 1.32 e la #244, 0.6.7: l'indirizzo di ritorno dopo
l'accesso) e a `0c2f85a` (E10i, la #237). Sono unite E1 (#200), E2 (#209), E2b (#212), E3a (#214), E3b (#221), E4 (#223), **E4b
(#226)**, **E5 (#228)**, **E6a (#233)**, **E6b (#240)**, E10a (#210), E10b (#208), E10c (#204), E10d (#205), E10e (#206), E10f (#213),
**E10g (#230)**, **E10h (#232)**, **E10i (#237)** ed E15a (#207), il passaggio dei tour al calcolo del nucleo (#211), la `0.6.0` (#216),
i piani 1.29 (#217), 1.30 (#229), 1.31 (#234) e **1.32 (#243)**, la parola degli eventi nella ricerca (#222), le altre correzioni del
nucleo fino alla `0.6.7` (#218, #219, #225, **#241** — il test del meteo con `IClock` — e **#244** — l'indirizzo di ritorno dopo
l'accesso) e gli spec che dicono al banco che cosa rimettono a posto (#227). **E4b** non migra niente; la striscia di chi è online sugli
scali dell'evento, il giorno dell'evento, l'ha montata E6b, dentro la pagina («Che cosa ha lasciato E6b», sotto). **In corso, del
nucleo**: **E10k** (questa, la #238) ed **E10j** (#239: il recupero dei giri dei job), con **E4c** (#242: il titolo di `/events` e le
frasi vuote delle schede «Slot» e «Rotte», gli usi degli eventi di E10k) in coda dopo la #238. **`WeatherTests` del nucleo**, che cadeva
su ogni branch dal mattino del 9 ottobre (#236), **è corretto in `main` dalla #241** di Carmine (0.6.6, la sera stessa).
**Il prossimo passo**: l'ordine in cui Carmine unisce è **#238 → #242 → #239**: **E10k** (questa, la #238: **decisa da Carmine**
sull'issue #224 e, dopo la revisione, sulla #238; il revisore ha letto le correzioni, e con `main` unito e la CI verde la dice pronta al
maintainer), poi **E4c** (la #242), poi **E10j** (la #239); ed **E7** (gli slot privati), il passo del modulo dopo E6b. Da E3b un evento si **pubblica**, entra nel
calendario e nella ricerca quando si vede e ne esce alla fine, e tiene i suoi file; da E4 ha la sua pagina `/events/{slug}`, sta in
`/events` e nel blocco `events.eventList`, il FOD ne scrive le rotte, e il suo tipo è uno dei tipi degli eventi («Che cosa ha lasciato E4»,
sotto); da E5 ha i suoi **slot pubblici**, caricati da una tabella con le rotazioni e mostrati sulla sua pagina, e il Gate Manager li legge
con un token personale («Che cosa ha lasciato E5», sotto); da E6a **si prenota**, sul server: un pilota prende uno slot pubblico o tutta una
rotazione e ritira fino all'off block, lo staff toglie una prenotazione con un motivo, e chi ha prenotato sa di un annullamento e di nuovi
orari, e di una correzione del volo che ha prenotato («Che cosa ha lasciato E6a», sotto); da E6b **si prenota dalle pagine**: «Prenota» e
«Prenota tutta la rotazione» nel dialog di uno slot, i filtri, l'apertura con il conto alla rovescia, `/events/mine` con «Ritira», il
blocco `events.myEvents`, la scheda «Prenotazioni» dello staff con «Togli», la conferma prima di «Pubblica» e il promemoria del giorno
prima, `events-reminders` («Che cosa ha lasciato E6b», sotto). **Carmine ha risposto sulla #240**
([1–5](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/240#issuecomment-6083877158),
[6](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/240#issuecomment-6083894346)): **sì a tutto**, e la nota
`2026-10-09-le-pagine-delle-prenotazioni` è **decisa**; ⚠️ **un lettore di nomi per VID nel nucleo entra nella coda del nucleo prima
che se ne scriva un quinto** (`EventsPeople` è il quarto, accettato per ora). **Il revisore ha letto la #240**: le quattro correzioni del
dialog di uno slot che chiedeva sono fatte sulla stessa PR. **Le tre domande di E6a hanno la risposta di
Carmine** ([sulla #233](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6069142670), nota
`2026-10-07-le-prenotazioni-sul-server` ora **decisa**): sì alle prime dieci letture; uno slot prenotato si corregge ancora e il pilota è
avvisato quando cambia il volo (`events.bookingChanged`, fatto sulla #233); l'eraser degli eventi resta a E8b, con la regola qui sotto; il
log binario è un punto aperto. ⚠️ **Prima di aprire le prenotazioni di un evento vero** (Carmine, 8 ottobre 2026, sulla #233):
**nessuna prenotazione su un'installazione vera finché E8b non è unita** (l'eraser degli eventi; l'installazione di prova non è
vincolata); e **il formato del log binario della MariaDB si prova con la prima prenotazione sull'installazione di prova, alla prossima
consegna** — con `STATEMENT` quella prenotazione fallisce subito e la lettura 1 della nota di E6a si riapre. **Punto aperto: lo chiude il
maintainer, alla consegna.** **Il master ha letto la #233**: le quattro correzioni che chiedeva sono fatte sulla stessa PR («Che cosa ha
lasciato E6a»). **Alla domanda della #232 Carmine ha detto sì**
([la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039778269)): la nota di E10h è decisa, e la sua
testa dopo la revisione (`ec9b3b4`) è unita qui. **Carmine ha risposto
sulla #228** ([le sue risposte][a228]): sì alle otto letture della nota `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, ora
**decisa**, e due punti in più, fatti sulla stessa PR — uno slot cade nella finestra del suo evento (sei ore per parte), e l'esportazione
porta la versione del suo contratto. ⚠️ **Il controllo della versione è del nucleo**: lo porta **E10g** (`ContractVersion`, la #230:
«Che cosa ha lasciato E10g», sotto), perché quello dei tour sta nel loro modulo e una copia negli eventi sarebbe lo stesso pezzo scritto
due volte. **Dopo la prova sul banco (7 ottobre) dalberone ha riaperto la #228** per la pagina degli slot: il tipo principale, partenze e
arrivi per scalo, le rotazioni segnate, il dettaglio di uno slot, e `aircraft_types` nell'esportazione — fatti sulla #228, con una nota
**decisa** da Carmine sulla #228 ([le sue risposte][a228b]: sì ai sette punti, e un ottavo: `Hint` resta un pezzo di questa schermata)
(`2026-10-07-gli-slot-sulla-pagina-dell-evento`); **E6a ha unito quella testa** (`25d23f5`, poi `ecf88b9` e `ba53a97`), e l'esportazione
dà `aircraft_types` accanto a `booked_by` e `aircraft_icao`. ⚠️ **Fra E3b ed E4 nessuna consegna e nessun «Pubblica»
sull'installazione di prova** (Carmine, 6 ottobre 2026, [sulla #221][seq221]): la voce di calendario e la riga di ricerca di un evento
pubblicato puntano a `/events/{slug}`, una pagina che porta solo E4 — **con E4 unita dopo la #221 il vincolo cade** (la pagina c'è).
**Le tre domande di E4 hanno la risposta di Carmine** ([sulla #223][ok223]): sì alle cinque letture del pubblico, E4b come fase del
nucleo, la seconda lettura scritta a mano accettata, i tipi come raccomandato — fatti sulla #223. **La
lettura dei preset** (`GET /api/events/kind-presets`) **resta**: Carmine l'ha accettata sulla #214 come scostamento dal design §7.2 (nota
`2026-10-01-la-lettura-dei-preset-dei-tipi`; «Che cosa ha lasciato E3a», sotto). ⚠️
**`EffectivePermission.FromOutside` è uno solo**, con le due vie che lo danno — un grant a una posizione su un altro dipartimento o al
team di un FIR (E2b), un permesso globale da un grant (E10f) — e una condizione sola nel calcolo (nota di E2b, §3.8; «Che cosa ha
lasciato E2b», sotto). **E11a** ed **E11b** trovano in E10c le postazioni della divisione, il rating preferito e il minimo di una
postazione (l'FRA di IVAO); E10a serve a **E13a**, che aspetta anche E12; **E11b** ed **E13a** trovano in E10b la storia di un
controllore e la presenza in un turno; **E14a** ed **E14b** trovano in E10e la distanza nel nucleo; **E14b** trova in E10d il riepilogo
a chi assegna gli award, e non chiama niente: con E10f il riepilogo arriva anche al coordinatore e all'assistente dell'MD; **E15b** (dopo
E14b) trova in E15a le prenotazioni della rete.

## Per chi prende M4 (`dalberone`)

Scritta dal master il 30 settembre 2026, quando Carmine ha affidato **tutta M4** a `dalberone`, fasi del nucleo comprese (nota
`2026-09-30-m4-al-collaboratore`). È quello che le sessioni del maintainer sanno e che M3 può non aver mostrato; ogni punto è
verificato su `origin/main` quel giorno.

- **Le regole di tutte le fasi** di `10-piano-implementazione-m4.md` valgono per te, con una correzione: dove dicono «una sessione di
  lavoro di Carmine» leggi **la tua sessione, nel tuo worktree**, e l'avviso che «`core-guard` non giudica le PR del proprietario» non
  vale più: le tue PR le giudica. `core-guard.sh` riconosce come tuoi `src/IvaoHub.Modules.Events/`, `web/src/modules/events/`,
  `locales/<lang>/events.json` e i test e le spec che cominciano con `Events`/`events` (`EventsSkeletonTests.cs`,
  `web/e2e/full/events-*.spec.ts`); **`tests/IvaoHub.IntegrationTests/SampleEvents.cs`** e le sue migrazioni `…_AddSampleEvents` sono
  il modulo di prova del nucleo, non gli eventi: toccarli è nucleo. La voce «Chi scrive» di `10` è già corretta (piano 1.25).
- **Le fasi del nucleo** (E1, E10a–E10e, E15a) sono **ognuna una PR a sé con una nota nuova** in `decisions/` (caso (b): quale
  meccanismo si estende e perché il modulo non ne fa a meno), unite **prima** della fase del modulo che le usa. Senza la nota
  `core-guard` va rosso sui file del nucleo. Non si mischiano con codice del modulo.
- **Il perimetro di IVAO** (`CLAUDE.md` §3): il codice che nomina IVAO sta in `src/IvaoHub.Core/Ivao/` e nella metà IVAO di
  `src/IvaoHub.Core/Auth/`. E10a, E10b ed E15a toccano `Core/Ivao/`: si estende **l'unico** `IIvaoApiClient`
  (`IIvaoApiClient.cs`, `IvaoApiClient.cs` con `IMemoryCache`; il resiliente è `AddStandardResilienceHandler()` in
  `IvaoServiceCollectionExtensions.cs`), e **anche `FixtureIvaoApiClient.cs`**, che in sviluppo risponde al posto di IVAO. Il modulo
  non parla mai con IVAO. Le risposte si leggono in un lettore unico per i due client (`IvaoTrackerReader` in `IvaoTracker.cs`: «a
  fixture that parsed itself differently from production would be a fixture that proves nothing»).
- **La richiesta al tracker oggi** è `IvaoSessionQuery` (`IvaoTracker.cs`): **per VID** (`userId`), una finestra, facoltativi partenza
  e arrivo; `PageSize = 50` («what the API was measured to accept») e **al più `MaxSessions = 200`** sessioni, poi si smette di
  sfogliare. E10a (senza VID, per postazione e tipo di connessione) cambia la forma della domanda: la misura del limite di pagina e del
  tetto va rifatta con il token vero, non ereditata.
- **Le fixture** si registrano con `tools/record-ivao-fixtures.mjs` (dalla radice, con `config/ivao-oauth.json`): anonimizza il VID nel
  VID dei test e toglie l'oggetto `user` di IVAO; oggi registra per VID, per elenco, aeroporti e `/users/me`. E10a ed E15a aggiungono
  le loro modalità allo script (è un file del nucleo: `tools/`). **Mai una chiamata vera in un test.** Le tracce restano su IVAO circa
  novanta giorni.
- **La cancellazione** (`tests/IvaoHub.IntegrationTests/ErasureTests.cs`): dalla #187 il test legge da solo i contesti di **ogni**
  modulo abilitato e confronta le colonne `…Vid` e `…By` con la lista `PersonColumnsOfTheHub`. **Da E2** (`evt_events.cancelled_by`)
  ogni fase che crea una colonna di persona scrive la sua riga lì, o il test va rosso; è uno dei due casi in cui un test condiviso si
  tocca (`10`, «Regole di tutte le fasi»), e la PR lo dice.
- **Gli aiuti del nucleo che conosci da M3** valgono uguali: `personName`/`isErased` (`web/src/shared/ui/people.ts`) e la colonna
  `col.person` della lista generata (`web/src/shared/list/columns.ts`) per «persona cancellata»; `IHasAssignee` con `OnlyForAssignee`
  (e `AlsoOnCreation`/`AlsoOnDeletion`) per le righe affidate a chi scrive; i grant `firTeam` al team di un FIR; `Refusals` con
  `CrudProblems.Validation` (`src/IvaoHub.Core/Data/Crud/Refusals.cs`) per i rifiuti raccolti a mano. Mai una copia nel modulo.
- **Il banco e2e** (`web/scripts/e2e-server.mjs`): `999001` il web master (`IT-WM`), `999002` il pilota (`?as=pilot`), `999003` l'assistente
  dei tour (`?as=assistant`, `IT-FOAC`), `999004` il trainer (`?as=trainer`, `IT-T01`). **E1 aggiunge il personaggio dell'ED**
  (`?as=events`, `IT-EC`, un VID dopo `999004`). Il database del banco `ivaohub_e2e` **si accumula** fra un giro locale e l'altro: una
  spec si riprende le sue righe (`CONTRIBUTING.md`, «Tests»).
- **La versione**: le tue PR **non alzano** `<Version>` in `Directory.Build.props`; la alza il master prima di un rilascio, con la regola
  scritta lì (la prossima è la `0.5.0`, piano 1.25). Sulla prova (`test.it.ivao.aero`) gira la **`0.4.1`**; tag, rilasci e consegne sono
  del maintainer.
- **Le decisioni**: una domanda a Carmine è un **commento sulla PR** con la nota «Proposta» e la raccomandazione; Carmine risponde lì,
  oppure in chat al master, che pubblica la risposta sulla PR su sua istruzione: il link al commento va nella nota. Il master non
  risponde mai al posto di Carmine.

## Da leggere, nell'ordine

1. `CLAUDE.md` (tutto, §0 per primo) e `CONTRIBUTING.md`.
2. Il piano `00-piano-di-progettazione.md`: l'intestazione e il changelog 1.24, **§9** (la riga Events di §9.2), **§9.3**, **§9.5**
   (il calendario unico e i tipi degli eventi), **§9.7** (contratti nucleo↔moduli, «Privacy dei membri», le collaborazioni
   ATC↔Events e FlightOps↔Events), **§10** (l'API di IVAO), **§13** (la riga M4), **§15** punto 12, **§16**.
3. **Il design `09-design-m4.md`**, tutto: è deciso (§17, con i link ai commenti di Carmine sulla #180).
4. **Le dieci note della fase E0** (`decisions/2026-09-29-*`, elencate in `10`, E0) e le note che decidono come stanno i moduli:
   `2026-09-13-moduli-non-subordinati-ai-dipartimenti`, `2026-09-13-ordine-dei-moduli`,
   `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`, `2026-09-27-i-capi-fir-sul-loro-fir`,
   `2026-09-28-i-job-quando-passenger-spegne-l-hub`.
5. **`10-piano-implementazione-m4.md`**: le regole di tutte le fasi, «Com'è andata» di E0 (che cosa c'è nel codice e che cosa no) e la
   fase che si apre.
6. **I moduli che esistono già**, da leggere e non da toccare né importare: `src/IvaoHub.Modules.FlightOps/` (lo stato dalle date,
   `TourReleaseJob`, le leg come righe figlie con `BeforeAuthorize`, il token dell'agente, `myTours`) e
   `src/IvaoHub.Modules.Training/` (lo scheletro di A4, `TrainingArchitectureTests`).

## Che cosa il nucleo dà già a Events

Verificato nel codice il 29 settembre 2026 (`10`, E0, «Trovato leggendo il codice»):

- **Il modulo** (`IModule`): permessi nel catalogo, impostazioni (`ModuleSettings`, lette campo per campo sopra i predefiniti), tipi
  di notifica, preferenze del membro, audience dei token personali, segmenti riservati, il contesto con la sua storia delle
  migrazioni. `division.json → modules.events.baseDepartment` è già `ED`.
- **Chi lavora**: i grant a una posizione con uno `scope` (`PositionGrantSeed`), al team di un FIR (`firTeam`, A11a), su una riga
  (`resource_scope`, `events:event:{id}`); `IHasStakeholder` con `DeniedToStakeholder`; `ISubmittedByMembers`; `IHasFir`.
- **Calendario, ricerca, award, usi dei file** con `IProjectable`; il job `media-expiry` che elimina un file quando tutti i suoi usi
  sono scaduti; `ProjectionRefresh` per riproiettare (come `TourReleaseJob`).
- **Lista e form generati** (`MapCrud`, `DataList`, `SchemaForm`), `BeforeAuthorize`, `DeletePolicy`, `Refusals` e
  `CrudProblems.Validation`, `BlockDocument` per la descrizione, `MediaPicker`, `CalendarView`.
- **Da IVAO**: `SearchSessionsAsync` **per VID**, `GetNetworkStatusAsync` (senza VID), `IAirportDirectory` con le coordinate,
  `IAircraftTypeDirectory`, `IvaoAirspace.Covers`/`Serves` per «della divisione».
- **La cancellazione dei dati di una persona**: lo pseudonimo nelle colonne `…Vid` e `…By` di ogni contesto dei moduli, e
  `IPersonalDataEraser` per quello che il modulo deve cancellare.

**Che cosa manca, e quale fase lo porta**: ~~i tipi `rfe`, `rfo`, `mse`, `onlineDay` e uno staff degli eventi sul banco (E1)~~
**portati da E1** (la chiave è `online-day`: sotto, «Che cosa ha lasciato E1»); le sessioni
senza VID, con il tipo di connessione (E10a); ~~il VID nelle sessioni condivise (E10b)~~ **portato da E10b** (e la storia di un
controllore: sotto, «Che cosa ha lasciato E10b»); ~~il rating preferito e minimo, le postazioni della
divisione per nominativo (E10c)~~ **portati da E10c** (il minimo è l'FRA di IVAO: sotto, «Che cosa ha lasciato E10c»); ~~la mail a chi assegna (E10d)~~ **portata da E10d** (un riepilogo al giorno: sotto, «Che cosa ha
lasciato E10d»); ~~la distanza nel nucleo (E10e)~~ **portata da E10e** (`GreatCircle` in `Core/Airspace/`: sotto, «Che cosa ha
lasciato E10e»); ~~le prenotazioni ATC della rete (E15a)~~ **portate da E15a** (`IAtcBookingSource`: sotto, «Che cosa ha lasciato
E15a»); la versione del contratto di un programma esterno, che E0 non prevedeva, **portata da E10g** (`ContractVersion`, per
l'esportazione di E5: sotto, «Che cosa ha lasciato E10g»); il membro che cancella la riga che ha mandato, che E0 non prevedeva, **portato
da E10h** (`[WithdrawnByStakeholder]`, per il ritiro della prenotazione di E6a: sotto, «Che cosa ha lasciato E10h»); il grant su un evento
solo che scrive davvero l'evento e le sue righe, che E0 dava per esistente, **portato da E10i** (il guardiano chiede con lo scope della
riga: sotto, «Che cosa ha lasciato E10i»);
l'helper «persona cancellata» e `ErasureTests` che legge ogni modulo sono già arrivati con A12a di M3 (#187): **E8a è tolta** (piano
1.25), e da E2 ogni fase che crea una colonna di persona scrive la sua riga in `ErasureTests`.

## Per i test

VID `761001–761099`, slug `evt-test-` (il master li scrive in `CONTRIBUTING.md` prima che E2 sia unita). **Attenzione all'ED e
all'MD**: i test dei contatti affermano i destinatari esatti dell'MD e seminano `IT-EC` con un indirizzo; nessuno staff dell'ED o
dell'MD con un indirizzo nei test del modulo, i permessi con grant a un VID. Nessuna chiamata a IVAO: fixture.

## Lo stato

*(Qui, in cima, il paragrafo «Che cosa ha lasciato <fase>» di ogni fase chiusa, la più recente per prima.)*

### Che cosa ha lasciato E10k (9 ottobre 2026, branch `m4/e10k-lists-and-titles`, PR #238, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-09-le-parole-delle-liste-e-i-titoli-delle-schede.md`, **decisa** da Carmine sull'issue
  #224, [la sua risposta][a224k]: sì ai quattro punti; e sulla #238, [la sua risposta][a238k]: sì alla lettura più larga — il titolo
  predefinito su ogni pagina, il back office compreso, e i titoli di `/news`, `/documents`, `/search` — e la dipendenza da React 19
  accettata; il dettaglio in `10`, E10k, «Com'è andata» e «Dopo la revisione»). Tutto nel browser, niente in C#:
  - **La paginazione della lista generata** (`Pages` in `web/src/shared/list/DataList.tsx`): i pezzi di `Pagination` di Atmosphere
    (`PaginationRoot`, `PaginationContent`, `PaginationItem`, `PaginationLink`) con le parole `list.pages.*` del nucleo — «‹ Precedente
    1 2 3 Successiva ›», i due «…» per la prima e l'ultima pagina con il loro nome. Un «…» solo dove nasconde una pagina.
  - **La frase vuota per lista**: `emptyDescription` di `DataList`, la frase della schermata già tradotta; senza, `list.empty.description`
    come prima. Detta solo quando non si cerca niente.
  - **Il titolo della scheda**: `DivisionTitle` (`web/src/shared/seo/PageMetadata.tsx`), montato una volta da `Root` in
    `web/src/routes/__root.tsx`, scrive il nome della divisione come titolo predefinito di ogni pagina e lo dà all'albero sotto.
    `PageMetadata` prende anche una frase già tradotta come `title` e `description`, e `divisionName` è **facoltativo** (senza, il nome
    della radice). `NotFound` dice il suo titolo; `/news`, `/documents`, `/calendar` e `/search` il loro; `_public/$.tsx`, `HomePage` e
    `PublicEntryScreen` non passano più il nome a mano.
  - **La frase di Invio del form generato** (`whereEnterSaves` in `web/src/shared/forms/SchemaForm.tsx`): niente frase dove nessuna
    casella è di una riga, `form.submitHintOneLine` dove ci sono caselle di una riga e di più righe, `form.submitHint` com'era dove sono
    tutte di una riga. Le caselle di una riga di una **lista ripetibile non contano** (sullo schermo ci sono solo dopo «Aggiungi»), quelle
    di più righe sì (dalla revisione della #238).
  - **Sei chiavi nuove del nucleo** in `locales/{en,it}/common.json`: `list.pages.label`, `.previous`, `.next`, `.first`, `.last`,
    `form.submitHintOneLine`. Nessun doppione nei file dei moduli.
  - **I test** (vitest, 16): `web/src/shared/list/DataList.words.test.tsx`, `web/src/shared/forms/SchemaForm.enter.test.tsx`,
    `web/src/routes/-titles.test.tsx` (sulla radice vera). `docs/UI-GUIDELINES.md` dice tutto questo in inglese.
- **Che cosa devono sapere le fasi dopo**:
  - **La fase degli eventi dopo questa** (scelta di dalberone il 9 ottobre: la regola 6 vuole il nucleo e il modulo in due PR): in
    `EventsPublicPage` (`web/src/modules/events/screens/public.tsx`)
    `<PageMetadata title={t('events:public.title')} description={t('events:public.description')} />`; in `EventScreen`, lo stesso file,
    il `divisionName` calcolato a mano si toglie; le schede «Slot» (`screens/slots.tsx`) e «Rotte» (`screens/routes.tsx`) passano
    `emptyDescription={t('events:…')}` con parole loro, chiavi nuove in `events.json` (poi `pnpm i18n:sync`).
  - **Ogni pagina nuova** dice il suo titolo con `PageMetadata`, la stessa chiave del suo `H1`, e **non passa `divisionName`**: lo dice la
    radice. Una lista di un modulo dice la sua frase vuota con `emptyDescription`.
  - ⚠️ **Il titolo predefinito regge sull'ordine di React**: React 19 mette un `<title>` che monta prima di quelli già nella testa, e il
    browser mostra il primo; la radice monta prima di ogni pagina. **Mai un `<title>` predefinito sotto una pagina o in un layout**: il
    titolo di una pagina montata prima di lui perderebbe la scheda. `web/src/routes/-titles.test.tsx` lo prova sulla radice vera.
  - ⚠️ **`/tours` è del maintainer**: dice il nome della divisione (il predefinito) finché il maintainer non aggiunge la sua riga in
    `PublicToursPage`. La pagina di un tour passa ancora `divisionName`, e va bene così.
  - ⚠️ **`WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`** (unità, del maintainer) **cade dalle 06:00 UTC del 9 ottobre su ogni
    branch**, `main` compreso: chiede la storia del 9 settembre 2026, e oltre i 30 giorni di `IWeatherSource.HistoryWindow`
    `NoaaWeatherClient.GetHistoryAsync` risponde `null`. Non si tocca (regola 3): l'issue #236 lo porta a Carmine. Fino alla sua
    correzione `build-test` è rosso su ogni PR, e la PR lo dice. ⚠️ **E con «Test .NET» rosso la CI salta ogni passo dopo**: lint,
    `format:check`, typecheck, vitest, la smoke, il giro completo. Si fanno in locale, `pnpm -C web run format:check` compreso (E10k l'ha
    mancato al primo push), e la PR li elenca.
  - **In un test che naviga su un router fatto per il test**, `router.navigate({ to })` prende i tipi delle rotte registrate dall'app
    (`/search` vuole i suoi parametri): `router.history.push(path)` non ha tipi di rotta.

[a224k]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6070089222
[a238k]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/238#issuecomment-6083876286

### Che cosa ha lasciato E10i (9–10 ottobre 2026, branch `m4/e10i-scoped-grant-writes`, PR #237, del nucleo, senza coda; `main` unito a `ba06d66`, con E6a ed E6b)

- **Che cosa c'è** (nota `decisions/2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga.md`, **decisa** da Carmine sulla #235: sì alla
  fase, e un grant su un evento solo crea un figlio di quell'evento, mai un evento nuovo; e dopo la revisione sulla #237: l'alternativa
  che crea chiede lo scope della riga nuova, nessuna alternativa sposta una riga fra scope, «mai un evento nuovo» per convenzione, e
  `EventBookings.Edit` su un evento solo con la propria prenotazione resta com'è; il dettaglio in `10`, E10i, «Com'è andata» e «Dopo la
  revisione»):
  - **Il guardiano** (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`) chiede `{Area}.Edit` **con lo scope della riga**, come l'unico
    handler: in modifica (la riga com'è scritta), all'eliminazione (la riga com'era) e alla creazione (lo scope con cui la riga nuova
    risponde). Una domanda sola, `Holds`, per `Edit` e per ogni alternativa.
  - **Un grant su un evento solo** (`events:event:{id}`) cambia e toglie l'evento e le sue righe — scali, rotte, slot e prenotazioni —,
    ne **crea** i figli (uno slot, una rotta, un caricamento della tabella degli slot), e **non crea un evento nuovo**: lo scope proprio di
    un evento nuovo è `events:event:0`, la chiave che il database non ha ancora dato, che nessun grant nomina.
  - **Lo spostamento fra scope** (`IsMoved`): una modifica che cambia dipartimenti, FIR o scope chiede `Edit` anche sulla riga di prima,
    e nessuna alternativa la sposta.
  - **Il modulo di prova**: `SamplePart` (`smp_parts`, `tests/IvaoHub.IntegrationTests/SampleParts.cs`), una parte che risponde con lo
    scope del suo item (`SampleItem.ScopeOf`), migrazione `AddSampleParts`; i test `ResourceScopeWriteTests` (7, sul guardiano) ed
    `EventsScopedGrantTests` (2, sugli endpoint veri degli eventi), VID 761097.
  - Nessuna migrazione del nucleo, nessun endpoint, nessuna chiave, niente nel browser; l'unico handler, il motore CRUD e la lista generata
    non cambiano.
- **Che cosa devono sapere le fasi dopo**:
  - ⚠️ **Una riga figlia risponde con lo scope della riga sopra di lei** (`ResourceScope => Event.ScopeOf(EventId)`), com'è già per
    scali, rotte e slot: così un grant su un evento la scrive e la crea. Ogni tabella nuova delle righe dello staff di un evento (per
    esempio le postazioni di E11a, le regole di award di E14b) fa lo stesso, come vuole la nota `2026-09-29-chi-lavora-sugli-eventi`
    (punto 6), e il grant su un evento solo vale anche lì senza altro codice.
  - ⚠️ **Lo scope proprio di una riga si costruisce sulla sua chiave**, mai su qualcosa che chi la scrive sceglie (l'indirizzo, un codice):
    un grant scritto su quello creerebbe la riga. E **un grant con scope si scrive su una riga che esiste**: la schermata del modulo che
    darà il grant «su un evento solo» lo scrive per un evento che ha trovato (`ModuleGrants.GiveAsync`), mai su un id che le arriva e
    basta. Sono le due condizioni del riassunto di `IHasResourceScope`; «mai un evento nuovo» si regge su questa convenzione, accettata da
    Carmine senza un controllo sulla chiave (risposta 3 sulla #237).
  - ⚠️ **Lo scope di prima si legge dai valori originali del tracker** (all'eliminazione, e in modifica quando la riga si sposta): quindi
    **uno stub mai letto è creduto com'è scritto, scope compreso** — il limite di E10h vale anche qui, e l'endpoint carica la riga che
    scrive —, e **uno scope si costruisce dalle sole colonne mappate della riga** (`Event.ScopeOf(EventId)`), mai da una navigazione, che
    quella copia non carica.
  - **`EventBookings.Edit` tenuto su un evento solo** lascia correggere anche la propria prenotazione in quell'evento: Carmine lo lascia
    così (risposta 4 sulla #237), chi lo tiene è staff dell'evento e l'audit dice chi ha fatto che cosa.
  - **E6a ed E6b** (unite): con E10i un grant di `EventBookings.Edit` su un evento solo toglie le prenotazioni di quell'evento («Togli»
    della scheda «Prenotazioni» di E6b) e ne corregge gli slot: **la trappola «Un grant su un evento solo non scrive»** del blocco di E6a,
    qui sotto, **è chiusa da questa fase**. `EventsBookingsTests.TheStaffTakeABookingAwayWithThePermissionOnItsEventAsItIsNow` resta com'è
    (il membro di un evento solo agisce su un altro evento: 403 dall'handler); il caso positivo, se lo si vuole, è una riga in più di quel
    test, in una fase del modulo.
  - **`WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`** (unità, del maintainer) cadeva dal 9 ottobre 2026 alle 06:00 UTC su ogni
    ramo: una data fissa contro `DateTime.UtcNow` e la finestra di 30 giorni di NOAA (l'issue #236, aperta dalla sessione di E6a). **La #241
    di Carmine l'ha corretta** (0.6.6): un ramo nato da `main` prima di `aa3707a` la prende unendo `main`, come ha fatto E10i.

### Che cosa ha lasciato E6b (9 ottobre 2026, branch `m4/e6b-booking-pages`, PR #240, in coda dopo la #233)

- **Che cosa c'è** (il dettaglio in `10`, E6b, «Com'è andata»; nessuna migrazione; del nucleo solo i due conteggi dei blocchi; una nota
  nuova, `2026-10-09-le-pagine-delle-prenotazioni`, **decisa** da Carmine sulla #240 — sì a tutto —; dopo la revisione, le quattro
  correzioni del dialog di uno slot: un 409 dice «riprova», chi accede dal dialog torna alla pagina, un rifiuto rilegge la pagina e il
  dialog segue lo slot com'è letto adesso, nessun colore libero):
  - **La pagina di un evento** (`screens/public.tsx`, `EventSlots.tsx`, `SlotBooking.tsx`, `BookingOpening.tsx`): i filtri degli slot
    nell'indirizzo (`direction`, `from`, `until`, `type`, `airline`, `rotation`: `eventPageSearchSchema`), offerti solo quando restringono;
    **«Prenota»** con la scelta dell'aereo e **«Prenota tutta la rotazione»** nel dialog di uno slot, solo quando il server lo
    accetterebbe, altrimenti il perché; **«Tuo»** sugli slot del lettore, dalla sua lista; la riga **«Prenotazioni»** con l'apertura e il
    conto alla rovescia (`bookingOpensAtUtc` è nella lettura della pagina, `PublicEventDto`); **la striscia di E4b** con gli scali
    dell'evento nei suoi giorni, nell'ora della divisione (`EventDayStrip`), dentro la pagina sotto il titolo.
  - **`/events/mine`** (`screens/mine.tsx`, una rotta del membro): «Da volare» e «Passate», per evento, «Ritira» finché `withdrawable`.
    L'indirizzo `mine` non lo prende nessun evento (`EventWriteDtoValidator.MinePage`, `events:errors.slugReserved`).
  - **Il blocco `events.myEvents`** (`blocks/myEvents.tsx`, `Bookings/MyEventsProvider.cs`, sempre vivo): le prenotazioni ancora da volare
    (`PilotBookings.MineAsync`), a un visitatore `signedIn: false`. Nessun seme lo mette su `/me`: lo mette il back office.
  - **La scheda «Prenotazioni»** dello staff (`screens/bookings.tsx`): `GET /api/events/bookings`, una risorsa `MapCrud` in sola lettura
    (`StaffBookings`: l'ordine dell'orario del volo, il pilota `{ vid, name }` da `EventsPeople`), `col.person`, «Togli» con il motivo.
  - **Il promemoria del giorno prima**: `events-reminders` (`Bookings/BookingRemindersJob.cs`), ai minuti 10, 25, 40, 55 UTC;
    `events.bookingReminder` una volta per prenotazione (`reminded_at`, `[NotAudited]`), le vicine in una mail con le rotte del FOD, sotto
    un `FOR UPDATE` in `READ COMMITTED`; scrive la sua riga in `hub_jobs_log` a ogni giro, come `events-release`.
  - **«Pubblica» chiede conferma** (`screens/events.tsx`).
  - **I test**: `EventsBookingPagesTests` (integrazione, 6, VID **761063–761066**, scali `XEG1`–`XEG3`, tipi `XE7A`/`XE7B`, slug
    `evt-test-e6b-…`), `EventsRemindersTests` (unità, 4), vitest, sei smoke in `events-public.spec.ts`, il giro completo
    `full/events-bookings.spec.ts` (slug `evt-test-e2e-e6b-…`, voli `XEE601`–`XEE603`) e la conferma in `full/events-staff.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **E7** (i privati): il promemoria, il blocco e `/events/mine` contano sull'off block di uno slot pubblico — il job prende solo
    `SlotKind.Public` con un off block, e mostra quello che `MineAsync` dà —. Un privato ha l'orario allo scalo e `other_time_utc`: E7
    allarga la query dei dovuti e `RemindAsync` di `BookingRemindersJob`, le righe della mail (`EventsMail`, `Flight`) e decide quale
    orario fa «dovuto». Il dialog di `SlotBooking` è di uno slot pubblico; i filtri leggono la compagnia dal nominativo dello slot.
  - **E8b** (la cancellazione): la scheda dello staff dice lo pseudonimo con `col.person`; il blocco e `/events/mine` sono del pilota
    stesso; il promemoria non va a uno pseudonimo (`EventsMail`).
  - **E12** (il roster): `events.myEvents` aspetta i turni. La risposta ha `signedIn` e `bookings`; i turni sono un campo in più della
    stessa risposta, e il blocco li disegna sotto le prenotazioni.
  - **E13a/E13b**: `reminded_at` è la contabilità del job, non un dato del volo, e non lascia righe d'audit.
  - **E4c** (dopo la #238 di E10k, i titoli e le frasi delle liste vuote degli eventi): la scheda «Prenotazioni» è una lista generata in
    più degli eventi (`screens/bookings.tsx`, le parole `events:bookings`), e non c'era quando E4c è nata; e **`/events/mine` non dice il
    suo titolo** nella scheda del browser: con il `PageMetadata` di E10k è una riga, `<PageMetadata title={t('events:mine.title')} />`,
    come per `/events`. La aggiunge chi delle due, E4c o E6b, prende l'altra per seconda; e chi lo fa aggiunge anche `bookingOpensAtUtc:
    null` dopo `endsAtUtc` nel `PublicEventDto` scritto per intero da `titlesAndEmptyLists.test.tsx` di E4c (il campo di E6b è
    obbligatorio nel client generato; detto dalla sessione di E4c il 9 ottobre). Per il resto i due branch si uniscono da soli, tranne
    l'intestazione di questo file.
  - ⚠️ **Le schede dello staff di un evento sbordano a 1280 px** con la barra laterale: «Prenotazioni» (1026 px su 927) come «Slot» di
    E5 (1052): la lista scorre, e «Togli» si raggiunge scorrendo; a 1440 px ci stanno. Il dialog di uno slot dice «Close» in inglese al
    lettore di schermo (il `Dialog` di Atmosphere, del nucleo).
  - ⚠️ **Il banco del giro completo riacceso per guardare** manda subito la coda di mail che il giro ha lasciato: chi lo riaccende con
    Mailpit tiene il lock di Mailpit.
  - **E10j** (#239, il recupero dei giri): `events-reminders` decide dai suoi dati, quindi un giro recuperato o ripetuto non manda niente due
    volte; il suo trigger dice il fuso (UTC).
  - ⚠️ **E10i** (#237): finché non è unita, un grant su un evento solo mostra la scheda «Prenotazioni», ma «Togli» risponde 403 (il
    guardiano, «Che cosa ha lasciato E6a»).
  - ⚠️ **Un lettore di nomi per VID nel nucleo, prima di un quinto** (risposta 4 di Carmine sulla #240): oggi ce ne sono quattro —
    `PirepReview.NamesAsync` dei tour, `TrainingPeople`, `ContactThreads` del nucleo ed `EventsPeople` —; una fase che ne vorrebbe un
    altro aspetta quello del nucleo, che entra nella sua coda, e i quattro poi passano a lui. La striscia degli scali resta dentro la
    pagina (risposta 2): il banner sarebbe una fase del nucleo, non chiesta.
  - ⚠️ **Un 409 di una prenotazione non passa da `describeProblem`**: il nucleo risponde a ogni 409 con la sua frase («qualcuno ha
    cambiato…») e non legge il titolo del server; il dialog dice `events:errors.bookingTryAgain`. Lo stesso vale per i verbi dei privati
    di E7. E un link d'accesso porta il percorso e la query (`window.location.pathname` + `search`): `SafeReturnUrl` del server manda
    alla home un indirizzo intero — lo stesso difetto è in `main` nelle pagine di tour e training (il punto 9 della revisione, non di
    questa fase).
  - ⚠️ **Nessuna prenotazione su un'installazione vera finché E8b non è unita**, e il log binario si prova alla consegna (risposte 2 e 4 di
    Carmine sulla #233): vale anche per il segno del promemoria, scritto in una transazione `READ COMMITTED`.
  - ⚠️ **Una mail del giro completo** parte con la coda del nucleo, una volta al minuto, **nella lingua della persona**: il pilota del
    banco legge in italiano. Una spec che la aspetta alza `test.setTimeout` e la cerca per il timbro del giro, che sta nel titolo delle
    due lingue.
  - ⚠️ `englishCommon` (`web/e2e/locales.ts`, del nucleo) non ha `common.cancel` né `liveStatus.airportsTitle`: le spec del modulo le
    leggono da `locales/en/common.json`.
  - **`WeatherTests`** (#236) è corretto dalla #241 (0.6.6): la #240 porta `main` a `aa3707a` attraverso la testa di E6a, e la CI gira
    intera.

### Che cosa ha lasciato E6a (7–9 ottobre 2026, branch `m4/e6a-booking-server`, PR #233; la #228 e la #232 sono unite)

- **Che cosa c'è** (il dettaglio in `10`, E6a, «Com'è andata»; una migrazione additiva, `AddEventBookings`; del nucleo solo le due righe
  di `ErasureTests`; due note nuove: `2026-10-07-le-prenotazioni-sul-server`, **decisa** da Carmine sulla #233 l'8 ottobre — sì alle
  prime dieci letture, il log binario aperto fino alla consegna, uno slot prenotato corretto avvisa il pilota, l'eraser a E8b con la sua
  regola —, e
  `2026-10-07-le-colonne-delle-prenotazioni-in-erasuretests`, nessuna decisione nuova; il ritiro poggia sulla fase del nucleo **E10h**, la
  #232, unita a questo branch; dopo l'apertura della PR, la testa di E5 dopo la prova sul banco, `25d23f5`, unita anche lei — `d767cc0`,
  i conflitti dell'esportazione e del commento dello slot risolti tenendo i due lati —; **le correzioni della revisione del master**
  ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6039678626)), sulla stessa PR, punto per punto in `10`;
  poi, quando la coda si è mossa, la testa di E5 che ha preso E10g e `main` a `7b84a75` (`ecf88b9`, unita in `67c870f`), quella decisa di
  E10h (`ec9b3b4`, in `8ab7c69`) e l'ultima di E5 (`ba53a97`, in `d36f4b7`), conflitti solo nei documenti: la PR non è più in conflitto
  con `main`; l'8 ottobre la testa finale di E10h (`eb8e8ef`, in `6451158`), con `main` a `1f2a687`, e la frase che Carmine chiede a ogni
  fase che mette il segno — perché la prenotazione non porta una decisione, la nota §2):
  - **`evt_bookings` intera** (`EventBooking`, design §1.6): `event_id`, `slot_id` (univoco), `booker_vid`, `aircraft_icao`, `callsign`,
    `other_icao`, `other_time_utc`, `paired_booking_id` (E7), `flown_at`, `flown_session_id`, `flown_checked_at` (E13a),
    `unflown_excused_by`, `unflown_excused_note` (E13b), `reminded_at` (E6b), `created_at`, più dipartimento, maschera e visibilità
    (`Members`). Una riga di un membro: `ISubmittedByMembers`, `IHasStakeholder` (il pilota), `[Audited]`, lo scope dell'evento,
    `[WithdrawnByStakeholder]` (E10h), e un **`IEventChild`**: la cura dell'evento si copia quando lo staff agisce, non solo il giorno della
    prenotazione. Non `IAuditable`. **Una chiave verso lo slot con `RESTRICT`** (uno slot prenotato non si elimina, nemmeno nel
    database), **nessuna verso l'evento** (il blocco del pilota, sotto).
  - **I verbi del pilota** (`/api/events/mine/bookings`, `Bookings/`): `GET` le sue (anche passate, per off block), `POST` uno slot
    pubblico con l'aereo, `POST …/rotation` tutta la rotazione di una tratta con un aereo solo (sempre 200: le prenotate, e il perché di
    ogni altra), `DELETE …/{id}` il ritiro fino all'off block. **Il blocco del pilota**: `SELECT … FOR UPDATE` sulla sua prima
    prenotazione dell'evento, o sulla riga dell'evento, in una transazione **`READ COMMITTED`** (`PilotBookings.LockThePilotAsync`), e
    l'evento si rilegge sotto il blocco, con la condizione della prima lettura (`EventState.Seen`: un annullamento salvato mentre la
    prenotazione aspettava si vede, e un evento che ha smesso di vedersi è 404). I rifiuti su `slotId` (`slotAlreadyYours`,
    `slotJustTaken`, `bookingIncompatible`, `slotClosed`, `bookingNotOpen`, `bookingCancelledEvent`, `bookingPrivateSlot`) e su
    `aircraftIcao` (`aircraftNotAllowed`); 404 per un evento che il pilota non vede; **409 «riprova»** (`events:errors.bookingTryAgain`) per
    un deadlock, di una prenotazione come della rotazione — mai «preso»; 201 senza un indirizzo (`Location`).
  - **Lo staff toglie** (`POST /api/events/bookings/{id}/remove`, `EventBookings.Edit` sulla prenotazione nella cura dell'evento com'è
    ora — `EventChildren.AdoptAsync` prima del permesso —, un motivo obbligatorio) e il pilota riceve **`events.bookingRemoved`**, dopo che
    la cancellazione è salvata (voluto: la nota, lettura 6); **`EventsMail`** è il posto delle mail del modulo (un intento per lingua, il
    titolo `{{title}}`, mai a uno pseudonimo).
  - **Le regole che crescono**: `SlotRows.Free` = gli slot che nessuna prenotazione nomina; uno slot prenotato non si elimina
    (`slotBooked`); un evento con prenotazioni non si elimina (`eventHasBookings`); annullare manda `eventCancelled`, spostare l'inizio o la
    fine `eventChanged` (`EventSaving.AfterSaveAsync`), a chi ha prenotato, una volta per persona. **Uno slot prenotato si corregge
    ancora** dal suo form e la prenotazione resta; se la correzione cambia callsign, orari, aeroporti o tipi ammessi (questi come insieme),
    il pilota riceve **`events.bookingChanged`** con il volo com'è ora e il suo aereo (`SlotSaving.AfterSaveAsync`, risposta 3 di Carmine);
    stand, numero di volo, rotazione o il solo tipo principale non avvisano nessuno. La compatibilità non si ricontrolla.
  - **La pagina** dice «preso» (`PublicEventSlotDto.Taken`), **l'esportazione** `booked_by` e `aircraft_icao`, accanto ad
    `aircraft_types` di E5 (i tipi ammessi, il principale per primo).
  - **I test**: `EventsBookingsTests` (unità, 12; integrazione, 16, VID 761043–761046 e, dalla revisione, 761084–761086 — tre membri
    dello staff per un grant a un VID, senza indirizzo, avanzi di E10f e di E4b; liberi ancora 761087–761088 e 761096–761098 —, scali
    `XEF1`–`XEF4`, tipi `XE6A`/`XE6B`, slug `evt-test-e6a-…`), con **le gare
    deterministiche**: una transazione del test (`HeldTransaction`) tiene quello che terrebbe la prima richiesta, la richiesta in prova
    aspetta nel database (`INNODB_TRX`, con root), il test conferma; e, dalla revisione, un deadlock costruito (la transazione del test più
    pesante, così il database riporta indietro il pilota) e sei prime prenotazioni mandate insieme. VID 761047–761048 e 761037 sono di
    E10h.
- **Che cosa deve sapere la fase dopo**:
  - **E6b** (le pagine): i verbi ci sono tutti, e il client generato li conosce (`schema.d.ts`). Manca **la lista dello staff** delle
    prenotazioni: una risorsa `MapCrud` in sola lettura (`EventBookings.View`, `filter[eventId]`, `Source` con `CrudSource.BackOffice`) con
    il pilota come `{ vid, name }` — una pagina di nomi per pagina di righe (`ToListPage`), come `TrainingPeople` — per `col.person`, e
    «togli» chiama `…/remove`. La pagina di un evento sa già «preso»; «Prenota» — nel dialog dello slot di E5 (`SlotDetail`, «Che cosa
    ha lasciato E5») — manda `POST /api/events/mine/bookings` con lo slot e il tipo scelto fra quelli ammessi, e i rifiuti arrivano su
    `slotId` e `aircraftIcao`; «Prenota tutta la rotazione» mostra le tratte di `notBooked` con il loro perché (chiavi `events:errors.*`).
    Un **409** di tutti e due è «riprova» (il titolo del problema): niente è prenotato, e la stessa richiesta può passare un attimo dopo.
    **E4b è in questo branch** (con `main` a `7b84a75`, dalla testa di E5): E6b, che nasce da qui, trova la striscia «chi è online sugli
    scali» da montare («Che cosa ha lasciato E4b»). `/events/mine` legge `GET` e ritira con `DELETE`; `withdrawable` dice se si può. **Il promemoria** (`events-reminders`) scrive `reminded_at`: valuti `[NotAudited]` per quella colonna, come l'ultimo uso di un
    token (T19a), se il suo giro non deve riempire l'audit. `EventsMail` ha il posto per `bookingReminder`.
  - **E7** (i privati): `BookingRules.ClosesAt` e `BookingInterval.Of` rispondono solo per uno slot pubblico (un privato oggi è chiuso e
    senza intervallo); E7 li allarga con l'orario allo scalo e `other_time_utc`, e `PilotBookings.BookAsync` rifiuta un privato con
    `bookingPrivateSlot` finché E7 non porta il suo verbo. L'esportazione legge già la prenotazione per ogni slot (`Flight(slot, booking)`).
  - **E8b** (la cancellazione): oggi il nucleo scrive lo pseudonimo in `booker_vid` e lo slot resta preso da nessuno; `EventsPersonalData`
    cancella le prenotazioni degli eventi non conclusi (il ritiro di E10h non serve: è un job del nucleo, in modalità cancellazione) e
    svuota i dati del volo privato di quelle tenute. L'esportazione dice un `booked_by` negativo, e il documento lo spiega.
  - **E13a/E13b**: le colonne `flown_*` e `unflown_excused_*` ci sono; un job che le scrive è anonimo e il guardiano lo lascia.
    ⚠️ **La prenotazione porta `[WithdrawnByStakeholder]` perché non porta una decisione** finché il pilota può ritirarla (la regola di
    Carmine sulla #232, punto 6: la nota di E6a, §2, dice perché). La giustificazione di E13b e la verifica di E13a cadono dopo il volo. Ma
    uno slot può partire fino a sei ore dopo la fine dell'evento (`SlotWindow.Margin`) e `events-after` gira dopo `ends_at_utc`: non
    toccate una prenotazione ancora ritirabile, o il segno va riguardato.
  - ⚠️ **Il log binario, punto aperto** (risposta 2 di Carmine): con `binlog_format=STATEMENT` MariaDB rifiuta le scritture di una
    transazione `READ COMMITTED`, e ogni prenotazione cadrebbe. Il predefinito di MariaDB 11.4 è `MIXED`; la CI ha il log spento; il
    maintainer non vede le variabili del server. Si prova con la prima prenotazione sull'installazione di prova, alla prossima consegna,
    prima di aprire le prenotazioni di un evento vero: lo chiude il maintainer.
  - ⚠️ **Nessuna prenotazione su un'installazione vera finché E8b non è unita** (risposta 4 di Carmine): l'eraser degli eventi arriva con
    E8b; l'installazione di prova non è vincolata.
  - ⚠️ **Uno slot prenotato corretto non si ricontrolla**: la prenotazione resta anche se ora è troppo vicina a un'altra del pilota, o se il
    suo aereo non è più ammesso; la mail `bookingChanged` glielo fa sapere e decide lui (ritirarla fino all'off block). E6b mostra le
    prenotazioni del pilota: lì si può dire che una non va più con un'altra.
  - ⚠️ **Il motivo di «togli» non si conserva**: è nella mail; l'audit dice chi ha tolto che cosa e quando.
  - ⚠️ **Un grant su un evento solo non scrive** (trovato in E6a, del nucleo, non toccato qui; lo porta la fase del nucleo **E10i**, in
    corso, che fa chiedere al guardiano `{Area}.Edit` con lo scope della riga): passa l'unico handler, ma il guardiano
    dell'interceptor chiede `{Area}.Edit` sui dipartimenti della riga senza il suo scope (`RequireAny`), e il salvataggio lo rifiuta
    (`ForbiddenDomainException`) — una prenotazione da togliere come uno slot da correggere. Il design vuole un grant a un VID «anche su un
    evento solo» (§6): finché il nucleo non lo legge, la lista dello staff di E6b lo mostra a chi ha quel grant e «togli» risponde 403.
  - ⚠️ **Due 500 restano**: un'attesa di blocco scaduta in una prenotazione (50 secondi, `innodb_lock_wait_timeout`) e un deadlock in
    «elimina i liberi»; rari, la risposta giusta sarebbe «riprova».
  - ⚠️ **`EventsArchitectureTests` rifiuta la stringa `"event"`** anche come nome di un segnaposto di una mail: il titolo è `{{title}}`.

### Che cosa ha lasciato E10h (7 ottobre 2026, branch `m4/e10h-stakeholder-withdraws`, PR #232, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga.md`, **decisa** da Carmine sulla #232: sì alla
  forma, il limite dello stub scritto come per T11, e sul rilievo 6 una frase nel riassunto del segno, §6.3 della nota; il dettaglio in
  `10`, E10h, «Com'è andata» e «Dopo la revisione»):
  - **Il segno**: `[WithdrawnByStakeholder]` (`WithdrawnByStakeholderAttribute`, namespace **`IvaoHub.Core.Division`**, in
    `src/IvaoHub.Core/Division/DomainContracts.cs` accanto a `ISubmittedByMembers`), sulla classe dell'entità.
  - **Il guardiano** (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`): una riga `ISubmittedByMembers` e `IHasStakeholder` la cui entità
    ha il segno la **cancella** il membro che ne è l'interessato, **com'era caricata** (i valori originali del tracker: chi legge la riga
    di un altro, ci scrive il suo VID e poi la toglie non passa), e nessun altro senza `{Area}.Edit` (o un'alternativa segnata
    `AlsoOnDeletion`). Senza il segno cancellarla resta del dipartimento (il PIREP, il training). Lo staff con `{Area}.Edit`, il
    superadmin e l'anonimo come prima; l'audit scrive `deleted` con il VID di chi ha cancellato.
  - **All'avvio** `HubSaveChangesInterceptor.VerifyWithdrawals`, chiamato da `HubPipeline.InitializeAsync` accanto a
    `VerifyAlternatives`, ferma l'hub se il segno sta su un'entità che non è insieme `IOwnedByDepartment`, `ISubmittedByMembers` e
    `IHasStakeholder`.
  - **Il modulo di prova**: `SampleSubmission` (`smp_submissions`, con il segno) e `SampleReport` (`smp_reports`, senza) in
    `tests/IvaoHub.IntegrationTests/SampleSubmissions.cs`, migrazione `AddSampleSubmissions`; i test `WithdrawnByStakeholderTests`
    (integrazione) e `VerifyWithdrawalsTests` (unità). `ErasureTests` non cambia: non legge il contesto di prova.
  - Nessuna migrazione del nucleo, nessun endpoint, nessuna chiave, niente nel browser; l'unico handler e il motore CRUD non cambiano.
- **Che cosa devono sapere le fasi dopo**:
  - **E6a** (la #233, in coda dopo la #232): il segno su `EventBooking` l'ha già messo la sua sessione, che ha unito questo branch; il
    ritiro passa dal suo endpoint del flusso del pilota (`DELETE /api/events/mine/bookings/{id}`), che legge la riga del pilota e la
    elimina, e il guardiano controlla di nuovo sotto.
  - ⚠️ **Il segno non apre il motore CRUD**: la DELETE di `MapCrud` chiede all'handler la policy di scrittura, e un membro non la tiene.
    Il ritiro di un membro è sempre un endpoint del suo flusso.
  - ⚠️ **L'endpoint deve caricare la riga; uno stub passa** (risposta 2 di Carmine sulla #232, come per T11): il guardiano legge
    l'interessato dai valori originali del tracker, e per una riga attaccata senza leggerla (`Remove(new X { Id = id, … })`) sono quelli
    che ha scritto chi chiama: un membro toglierebbe la riga di un altro. L'endpoint legge la riga del membro — per id **e** per
    interessato, come `PilotBookings.WithdrawAsync` di E6a — e poi la toglie. Lo fissa
    `WithdrawnByStakeholderTests.AStubNeverLoadedIsBelievedAsItsCallerWroteIt`.
  - **E11a** (la disponibilità di un controllore, che «la ritira fino alla chiusura», design §4.2) ed **E16** (l'iscrizione a un evento
    in presenza, «si ritira fino all'inizio dell'evento», §4-bis.2): se la fase dice che ritirare vuol dire cancellare la riga, basta il
    segno sull'entità, che dev'essere `IOwnedByDepartment`, `ISubmittedByMembers` e `IHasStakeholder` (o l'hub non parte); se il ritiro è
    uno stato, come per il PIREP, il segno non serve: cambiare la propria riga lo permette già l'eccezione di T11.
  - ⚠️ **Il segno si legge con `inherit: false`**: va sulla classe dell'entità, non su una sua base.
  - ⚠️ **Il segno mai su una riga su cui lo staff decide qualcosa del membro** (risposta 3 di Carmine sulla #232, nota §6.3: una frase
    nel riassunto del segno, non un rifiuto all'avvio): un PIREP di supporto, la cessione di un turno — cancellarla cancellerebbe la
    decisione. Il guardiano non sa né lo stato né l'ora: fino a quando si ritira lo dice l'endpoint. **La nota di ogni fase che mette il
    segno dice perché la sua riga non porta decisioni**, e la revisione lo controlla (per una prenotazione: le verifiche e le scuse dello
    staff arrivano dopo l'evento, quando il ritiro, che finisce all'off block, è chiuso).

### Che cosa ha lasciato E5 (6–7 ottobre 2026, branch `m4/e5-public-slots`, PR #228, nata in coda dopo la #223, unita prima che si aprisse; dalla revisione in coda dopo la #230 di E10g)

- **Che cosa c'è** (il dettaglio in `10`, E5, «Com'è andata»; una migrazione additiva, `AddEventSlots`; del nucleo solo le due righe di
  `ErasureTests`; due note nuove: `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, **decisa** da Carmine sulla #228 — [le sue
  risposte][a228]: sì alle otto letture, e i punti 9 e 10 sulle domande del revisore ([osservazioni][v228]) —, e
  `2026-10-06-le-colonne-degli-slot-in-erasuretests`, nessuna decisione nuova; il controllo della versione dell'esportazione è del nucleo,
  dalla #230 di E10g, unita a questo branch; dopo la prova sul banco, una terza nota, `2026-10-07-gli-slot-sulla-pagina-dell-evento`, il
  tipo principale e la pagina pubblica degli slot che dalberone ha chiesto, **decisa** da Carmine sulla #228 — [le sue risposte][a228b]:
  sì ai sette punti, e un ottavo sulla [seconda lettura del revisore][v228b], `Hint` resta un pezzo di questa schermata):
  - **`evt_slots` intera** (`EventSlot`, design §1.5), per i pubblici di E5 e i privati di E7: `kind`, `event_airport_icao`, `is_arrival`,
    `callsign`, `flight_number`, `aircraft_types` (JSON), `departure_icao`, `arrival_icao`, `off_block_utc`, `on_block_utc`, `stand`,
    `rotation_code`, `rotation_leg`, `generated`, `row_version`; univoco `(event_id, callsign, off_block_utc)` (un privato non ha callsign, e
    l'indice ne lascia passare quanti vuole). Riga `IEventChild` nell'area **`EventBookings`**, `[Audited]`, lo scope dell'evento.
  - **Incolla o carica** (`POST /api/events/events/{id}/slots/load`, `EventBookings.Edit` sull'evento; `Staff/SlotLoading.cs`): il testo —
    incollato da un foglio di calcolo, o il file CSV letto dal browser nella stessa casella — e il modo (`Add`, `ReplaceFree`). **Lo legge il
    server** (`Staff/SlotSheet.cs`): l'intestazione del design in qualunque ordine, tabulazioni, punto e virgola o virgole, le virgolette; le
    righe contate come le conta la tabella (`rows[12].aircraft_types`); al più mille. Ogni riga uno slot pubblico (`SlotDraft.Read`): orari
    solo `2026-10-17 14:30` in UTC, tipi separati da `/`; tipi e scali chiesti al nucleo una volta per tutta la tabella; **il verso dagli
    ICAO** (`SlotDirection`: partenza da uno scalo dell'evento, anche fra due scali dell'evento, altrimenti arrivo, altrimenti rifiutato);
    callsign e off block una volta nell'evento; **le rotazioni** (`SlotChains`, con gli slot salvati che restano): i posti, o gli orari
    quando nessun posto è scritto, lo scalo che coincide, l'ordine, `bookingGapMinutes`; **la finestra dell'evento** (`SlotWindow`, il
    punto 10 di Carmine): l'orario allo scalo dell'evento — l'off block di una partenza, l'on block di un arrivo — fra sei ore prima
    dell'inizio e sei ore dopo la fine, `events:errors.slotOutsideWindow` sulla colonna di quell'orario; l'altro orario è libero. **Tutto
    o niente**, una transazione; **409 «carica di nuovo» solo per un'altra scrittura delle stesse righe** (una chiave che l'indice univoco
    ha già, o il deadlock di due insert della stessa chiave, cercati nella catena dell'eccezione come fa `InitialisationMarker`): ogni
    altro errore esce com'è (revisione, punto 4).
  - **La lista e il form generati** (`/api/events/slots`, `MapCrud`, `EventBookings.View`/`.Edit`): la scheda **«Slot»** della pagina
    dell'evento, su un evento con slot e scali suoi; il form di uno slot (`/staff/events/{id}/slots/{slotId}`, anche «Nuovo slot») tiene le
    regole del caricamento (`SlotSaving`, la finestra compresa), e i tipi in **due campi, «Tipo principale» e «Altri tipi»**
    (`MainAircraftType`, `OtherAircraftTypes` di `EventSlotWriteDto`), salvati principale per primo (`SlotValues.MainFirst`), ogni rifiuto
    sul suo campo; **il primo di `aircraft_types` è il principale**, anche nella cella `A320/A20N` della tabella; **«Elimina i liberi»**
    (`POST …/slots/delete-free`): ogni slot libero dell'evento, pubblici e privati. La pagina del caricamento
    (`/staff/events/{id}/slots/load`): l'intestazione da copiare, il file CSV, i rifiuti elencati per riga e colonna.
  - **Le regole che crescono**: niente caricamento su un evento senza slot pubblici o senza scali; l'interruttore degli slot pubblici non si
    spegne sotto gli slot (`events:errors.hasPublicSlots`); uno scalo con slot non si elimina né cambia codice (`airportHasSlots`); eliminare
    un evento porta via i suoi slot con l'audit (`EventSaving.DeleteAsync`).
  - **La pagina dell'evento** elenca gli slot pubblici (`PublicEventDto.Slots`, nella lettura che c'è già; `screens/EventSlots.tsx` e
    `screens/slotList.ts`), come dalberone l'ha chiesta dopo il banco (nota del 7 ottobre, «Proposta»): **per scalo dell'evento**, una
    sezione ciascuno quando sono più d'uno, e in ognuno **«Partenze» e «Arrivi»** in due tabelle, per l'orario allo scalo; **il tipo
    principale**, gli altri nel tooltip di Atmosphere al passaggio del mouse, al focus e al tocco; le tratte di una rotazione ognuna nella
    sua tabella, **segnate da un'icona** (`Repeat`) che lo dice; **una riga apre lo slot** in sola lettura in un dialog di Atmosphere, con
    tutti i tipi ammessi, gli orari, lo stand e le tratte della rotazione — e quando si chiude **il focus torna al nominativo** che l'ha
    aperto; un click che chiude una selezione di testo non apre niente; **libero o preso — mai chi**. «Prenota» è di E6b, nel dialog.
  - **L'esportazione** per il Gate Manager (`GET /api/events/{slug}/bookings/export`, `Export/BookingsExport.cs`): con un token personale
    dell'`audience` **`events.bookings`** (`EventsModule.TokenAudiences`, permesso `EventBookings.View`, la parola
    `events:tokenAudiences.bookings`), chiesto anche all'unico handler sulla riga; un array con i nomi del Gate Manager (`slot_id`,
    `callsign`, `flight_number`, `booked_by`, `aircraft_icao`, `aircraft_types`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`,
    `rotation`, `leg`, `paired_slot_id` — `aircraft_types` aggiunto dopo il banco, i tipi ammessi con il principale per primo, vuoto su un
    privato, un'aggiunta alla versione 1), orari UTC con la `Z`, **nell'ordine dell'orario allo scalo dell'evento** (l'on block di un arrivo: fino alla
    revisione andava per off block, contro la nota). **Una bozza mai**: 409 `code: "draft"`; il 404 prima del 403 è voluto (revisione,
    punto 7). **La versione del contratto** (punto 9 di Carmine): l'intestazione **`Hub-Bookings-Contract: 1`**; senza, o con una versione
    che l'hub non parla, 400 `code: "bookingsContract"` con `current` e `accepted`; la controlla il `ContractVersion` del nucleo (E10g),
    dopo il token e prima dell'evento. Il contratto per chi scrive il programma è **`docs/events-bookings-export.md`** (inglese).
  - **I test**: `EventsSlotsTests` (unità, 41: il lettore, una riga, gli istanti, il verso, le catene, la finestra, i tipi del form),
    `EventsSlotsTests` (integrazione, 5 — la finestra, il volo ricaricato, l'ordine e la versione dell'esportazione dalla revisione, i tipi
    del form e `aircraft_types` dal banco —, VID 761012–761014, scali `XED1`–`XED4`, tipi `XE5A`/`XE5B`, slug `evt-test-e5-…`),
    `screens/slotList.test.ts` (vitest, 6) ed `screens/EventSlots.test.tsx` (vitest, 8: le sezioni, il tooltip al focus e al tocco, una
    pressione dimenticata, l'icona detta una volta, il dialog, il focus che torna, la selezione), due test nella smoke
    `web/e2e/events-public.spec.ts` (10, **uno su un telefono**: `hasTouch`, il tocco vero in Chromium; e il focus dopo Escape), il giro
    `web/e2e/full/events-slots.spec.ts` (il «fatta quando», con `afterwards(…)`; e il form di uno slot con i suoi due campi nel browser).
    `EventsTestRows` toglie anche gli slot; `ErasureTests` ha le due righe di `evt_slots`. VID 761015–761016 e 761062–761067 restano
    liberi.
- **Che cosa deve sapere la fase dopo**:
  - **L'esportazione è la versione 1 di un contratto** (`docs/events-bookings-export.md`): dentro una versione l'hub solo aggiunge. E6a
    riempie `booked_by` e `aircraft_icao`, che ci sono già: nessuna versione nuova. Un campo tolto o rinominato, o un significato cambiato,
    è la versione 2, accettata accanto alla 1 per almeno un rilascio; il documento cambia nella stessa PR del codice.
  - **E6a**: **«libero» si dice in un posto solo**, `SlotRows.Free` (`Staff/SlotLoading.cs`): oggi ogni slot, perché non c'è ancora una
    prenotazione; E6a lo restringe agli slot che nessuna prenotazione nomina, e «sostituisci» ed «elimina i liberi» seguono. Poi:
    `PublicEventSlotDto.Taken` (oggi `false`), `booked_by` e `aircraft_icao` dell'esportazione (oggi vuoti; `aircraft_icao` è **il tipo scelto
    dal pilota**), «uno slot prenotato non si elimina» (il `Delete` del CRUD degli slot e il primo rifiuto di `EventSaving.DeleteAsync`), e le
    righe delle colonne di persona di `evt_bookings` in `ErasureTests`.
  - **E6b**: «Prenota» va **nel dialog dello slot** (`SlotDetail` in `screens/EventSlots.tsx`), che mostra già tutti i tipi ammessi, il
    principale per primo: fra quelli il pilota sceglie il suo, che diventa `aircraft_icao` dell'esportazione. La pagina pubblica non ha una
    lettura sua: tutto viene da `PublicEventDto.Slots`, e un campo che servisse è un'aggiunta a quel DTO. ⚠️ Il dialog non ha un bottone
    suo che lo apre: **il focus torna al nominativo** grazie a `openedBy` di `EventSlots`, che lo rimette quando il dialog è sparito (dentro,
    la trappola del focus lo riprenderebbe). Un dialog che si chiude dopo «Prenota» passa da lì anche lui.
  - ⚠️ **Il tooltip che si apre al tocco** (`Hint` in `screens/EventSlots.tsx`): il tooltip di Radix si apre solo al passaggio del mouse e
    al focus, e fra la pressione e il click lo chiude e lo riapre da sé (un tocco dà il focus al bottone dopo che il dito si alza).
    Per questo il click rovescia quello che si vedeva **quando la pressione è cominciata**; un click della tastiera (`detail` 0) rovescia
    quello che si vede, e una pressione annullata si dimentica. Un semplice «al click si rovescia» lascia aperto il secondo tocco (provato
    al contrario nella smoke con `hasTouch`). **`Hint` resta un pezzo di questa schermata** (Carmine, punto 8 sulla #228): il giorno che
    una seconda schermata vuole un tooltip che si apre al tocco, è una decisione da portare a Carmine — non una copia, e non un riuso
    fatto da sé.
  - **E7**: un privato è `Kind = Private`, `Generated`, lo scalo e il verso, e l'orario allo scalo in `OffBlockUtc` (partenza) o
    `OnBlockUtc` (arrivo); l'esportazione li porta già così (gate vuoto, `paired_slot_id` vuoto finché E7 non lo legge dalla prenotazione);
    il form di uno slot li rifiuta (`events:errors.slotNotPublic`) e la scheda non dà loro «Modifica»; «elimina i liberi» li toglie.
  - **E8b** («Duplica») copia, a scelta, gli slot pubblici con le rotazioni: le colonne sono quelle di `SlotDraft`, e i posti ci sono già.
    Gli slot copiati si spostano con le date del nuovo evento, o cadono fuori dalla sua finestra (`SlotWindow`).
  - ⚠️ **Il verso si fissa quando lo slot si scrive** (`IsArrival` ed `EventAirportIcao`; revisione, punto 8): uno scalo aggiunto all'evento
    dopo lascia uno slot salvato com'era, finché qualcuno non lo salva di nuovo.
  - ⚠️ **Le date dell'evento che cambiano non ricontrollano la finestra**: uno slot rimasto fuori resta finché non si salva di nuovo
    (allora è rifiutato) o un «sostituisci» non lo toglie, se è libero.
  - ⚠️ **Le catene usano `bookingGapMinutes` di quando si caricano**: cambiare l'impostazione dopo non ricontrolla le rotazioni salvate.
  - ⚠️ **Due caricamenti dello stesso evento nello stesso momento li tiene solo l'indice univoco** (revisione, punto 6): ognuno controlla le
    catene con gli slot che legge, quindi insieme possono salvare una catena che nessuno dei due avrebbe accettato da solo. Due persone
    che caricano lo stesso evento nello stesso secondo sono un caso lontano; una catena così resta salvata, e chi corregge poi una delle sue
    tratte se la vede rifiutare finché non la ripara.
  - ⚠️ **FluentValidation: un `.When` alla fine di una catena di regole vale per tutta la catena** (`ApplyConditionTo.AllValidators`):
    scritta così, la regola del formato del callsign avrebbe spento anche «obbligatorio», e un callsign vuoto sarebbe passato. Trovato
    rileggendo, prima dei test: ora il formato sta in un `RuleFor` suo, e il test d'integrazione manda un callsign vuoto.
  - ⚠️ **Il test d'integrazione scrive un privato sul database** (nessuno lo genera ancora): E7 lo sostituisce con il generatore.
  - ⚠️ **Uno spec del giro completo dice che cosa rimette a posto con `afterwards(…)` del banco, mai in un `finally`** (#227,
    `CONTRIBUTING.md`): `events-slots.spec.ts` lo fa dal merge di `main` che è arrivato con il branch di E10g; `events-public.spec.ts` ed
    `events-staff.spec.ts` del giro completo, di E4 e di prima, hanno ancora il `finally`.

[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6021830879
[a228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040717010
[v228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040474527

### Che cosa ha lasciato E10g (6–7 ottobre 2026, branch `m4/e10g-contract-version`, PR #230, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-06-la-versione-di-un-contratto-nel-nucleo.md`, **decisa**: Carmine sulla #230,
  [le sue risposte](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039777720) — sì al nucleo invece di una copia,
  il passaggio dei tour in una sua sessione, la riga in `CLAUDE.md` §2 dal master):
  - **La versione di un contratto nel nucleo**: `ContractVersion` in `src/IvaoHub.Core/Auth/ContractVersion.cs`, namespace
    **`IvaoHub.Core.Auth`**, accanto ai token personali. Si costruisce con l'intestazione del contratto, la versione corrente, le
    accettate, la chiave del titolo e il `code`. `RequireAsync` è un filtro di endpoint che risponde come quello dei tour
    (`AgentContract.RequireVersionAsync`): l'intestazione senza spazi intorno, sole cifre ASCII, fra le accettate; altrimenti **400** con
    `code`, `current` e `accepted` e il titolo nella lingua di chi chiede; se accettata, la risposta ripete nella stessa intestazione **la
    versione parlata** (non la corrente), e l'endpoint risponde.
  - **Il costruttore rifiuta** un'intestazione che non è un token di HTTP, nessuna versione, una versione non positiva o ripetuta, la
    corrente fuori dalle accettate, una chiave o un `code` vuoti: quando il modulo dichiara il contratto, all'avvio.
  - **I test**: `tests/IvaoHub.UnitTests/ContractVersionTests.cs` (unità, 21), con **il test gemello**, che confronta il nucleo con i
    valori dei tour e il filtro dei tour su 120 coppie, byte per byte.
  - Nessuna migrazione, nessun endpoint, nessuna chiave del nucleo, niente nel browser.
- **Che cosa deve sapere la fase dopo** (E5, la #228, in coda dopo questa, che porta già questo branch):
  - **Come la usa un modulo**: un campo `static readonly ContractVersion` con i valori del suo contratto — **un'intestazione sua**, non
    quella dell'agente (Carmine, punto 9 sulla #228), la versione corrente, le accettate, **la chiave del titolo con il namespace del
    modulo**, nel file delle parole del modulo in ogni lingua, e il suo `code` — e `.AddEndpointFilter(contratto.RequireAsync)` sugli
    endpoint del contratto. La policy del token risponde prima del filtro (401, 403): l'autorizzazione è un middleware, il filtro gira
    dentro l'endpoint.
  - ⚠️ **Il titolo è del modulo**: il nucleo non aggiunge parole. Senza la chiave nel file del modulo il 400 ha per titolo la chiave
    stessa, e `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer` vuole una chiave con il namespace che il file del modulo
    dichiari.
  - ⚠️ **Un endpoint aperto** che dice il contratto prima che il programma parli una versione (i tour hanno `/contract`) non mette il
    filtro e scrive da sé `Header` con `Current`: il nucleo non ha un `Announce` (nota §3). L'esportazione di E5 non ne ha uno.
  - **Il documento pubblico** del contratto è del modulo, come `docs/agent-contract.md` dei tour: l'intestazione, il 400 con `code`,
    `current` e `accepted`, la regola delle versioni (dentro una versione solo aggiunte; una rottura è la versione dopo, accettata
    accanto alla vecchia per almeno un rilascio).
  - ⚠️ **La copia dei tour c'è ancora** (`AgentContract.RequireVersionAsync`): la toglie una sessione di Carmine **dopo l'unione di
    questa PR** ([sua risposta sulla #230](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039777720), alla
    [richiesta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6026482474); nota §5). Fino ad allora il test gemello
    tiene le due copie uguali; con il passaggio se ne va anche lui. **Sì anche alla riga in `CLAUDE.md` §2** (la versione del contratto di
    un programma esterno è `ContractVersion` del nucleo, mai un controllo del modulo), che aggiunge il master con il piano.

### Che cosa ha lasciato E4b (6 ottobre 2026, branch `m4/e4b-online-at-airports`, PR #226, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-06-chi-e-online-sugli-scali-nel-nucleo.md`, scelta tecnica sulla (b) che Carmine ha scelto
  sulla #223, punto 2; il dettaglio in `10`, E4b, «Com'è andata»; nessuna migrazione, nessun endpoint, nessun blocco nuovo):
  - **`networkStats` con gli scali che una schermata chiede**: `airports`, un elenco di ICAO nelle `props` della domanda, **fuori dallo
    schema zod** come `from` e `to` del calendario (un editor non li salva). Con l'elenco lo spazio è quello degli scali — controllori
    la cui stazione è uno di loro (la torre e l'avvicinamento di `LIRF`, non il centro `LIRR` sopra), piloti il cui piano di volo parte
    da uno di loro o ci arriva — e le due cifre `divisionAtc`, `divisionPilots` lo contano; senza, la divisione come prima. **Un elenco
    senza nessuno scalo non conta nessuno**, mai la divisione; uno scalo è da una a quattro lettere o cifre, e se ne contano al più 50,
    **dopo la pulizia** (`IvaoAirspace.OfAirports(scali, limite)`).
  - **Un whazzup al minuto per tutti** (`IvaoNetworkPicture`, in `src/IvaoHub.Core/Ivao/IvaoWhazzup.cs`): la lettura comune per un
    minuto, contata per ogni spazio con la sua `CacheKey`, che ora **nomina gli scali** quando non ci sono centri; la lettura **tiene le
    risposte di al più 16 spazi** (`MaxKeptAnswers`) e conta ogni volta gli altri. Lo scostamento dalla nota di E4 e la misura (6 ottobre
    2026: 0,8 MB, dalla cache di Cloudflare; mille insiemi inventati in 24 ms invece di mille scaricamenti) sono nella nota, §2 e §4; lo
    scostamento e le parole del titolo li porta a Carmine il revisore (rilievi sulla #226, punto 5).
  - **`<LiveStatusStrip airports={…} />`** (`web/src/shared/ui/LiveStatusStrip.tsx`): la domanda con gli scali e il titolo
    `liveStatus.airportsTitle` («Su questi scali adesso», «At these airports now»); senza `airports`, la striscia di sempre.
  - **I test**: `LiveStatusAirportsTests` (unità, 8), `NetworkStatsAirportsTests` (integrazione, 5, sulla fixture `whazzup.json`),
    `LiveStatusStrip.airports.test.tsx` (vitest, 4). Nessun VID, nessuno slug.
- **Che cosa deve sapere la fase dopo** — ⚠️ **la prima fase del modulo dopo il merge di E4b** (Carmine sulla #223, punto 2; «per esempio
  E6b» diceva la nota di E4) **monta la striscia sulla pagina dell'evento**:
  - `<LiveStatusStrip airports={gli ICAO degli scali dell'evento} />` sulla pagina di E4 (`web/src/modules/events/screens/public.tsx`),
    **il giorno dell'evento** (design §7.1): la fase scrive come legge «il giorno» (nell'ora della divisione da mezzanotte a mezzanotte, o
    dall'inizio alla fine) e lo dice in «Com'è andata». **Un evento di tutta la divisione** non ha scali e non monta niente: la striscia
    della divisione è già in cima al sito.
  - ⚠️ **Dove**: `docs/UI-GUIDELINES.md` vuole la striscia «in the banner slot of `Shell`», con una misura in `web/e2e/live-status.spec.ts`,
    e il banner del layout `_public` ha già la striscia della divisione. Dentro la pagina è una scelta della fase, da scrivere; nel banner
    al posto di quella della divisione vuole che il layout sappia gli scali della rotta: un'altra modifica del nucleo, con la sua nota.
  - **I test della fase**: una spec che ferma `networkStats` con `stubTheBlockData` e cerca `liveStatus.airportsTitle`, leggendo nella
    richiesta gli scali chiesti (`props` in base64url); sul banco la fixture `tests/fixtures/ivao/whazzup.json` ha `LIRR_CTR`, `LIMC_APP`,
    `LIRF_TWR`, `EDDF_TWR` e i voli LIRF→LIMC, EDDF→LIMC, EDDF→EGLL: un evento a `LIRF` mostra un controllore e un pilota, uno a `LIMC`
    un controllore e due piloti.
  - Il modulo **non nomina la rete**: chiede `networkStats` attraverso la striscia del nucleo, e `EventsArchitectureTests` resta com'è.

### Che cosa ha lasciato E4 (6 ottobre 2026, branch `m4/e4-public-and-routes`, PR #223, in coda dopo la #221)

- **Che cosa c'è** (il dettaglio in `10`, E4, «Com'è andata»; una migrazione additiva, `AddEventRoutes`; nessun file del nucleo oltre ai
  tre test condivisi che la fase deve toccare; quattro note nuove, tre decise da Carmine sulla #223 — [la risposta][ok223]):
  - **Le rotte** (`evt_routes`, `EventRoute`, design §1.4): da uno scalo a un altro — mai a sé stesso, `events:errors.routeToItself`
    (la revisione della #223) —, la rotta da inserire (obbligatoria, 1024 caratteri, non tradotta) e le note (`remarks_i18n`, tradotte:
    scritte in una lingua, in tutte quelle della divisione; 500 per lingua). Riga `IEventChild` nell'area **`EventRoutes`**
    (`[PermissionArea]`, `[Audited]`, lo scope dell'evento); CRUD `/api/events/routes` (`EventRoutes.View`/`.Edit`, `filter[eventId]`,
    l'evento adottato in `BeforeAuthorize`, i due scali conosciuti dal nucleo; più rotte fra gli stessi due scali ammesse). Il FOD, che
    tiene l'area sull'ED per il grant della sua posizione, scrive le rotte di ogni evento e non l'evento. **Le rotte stanno nell'ordine in
    cui il FOD le scrive** (l'`Id`), sulla pagina e nella scheda del back office, dove si riordinano per scalo a mano: l'ordine per
    partenza metteva il ritorno prima dell'andata (dal banco). **Eliminare un evento** porta via le sue rotte nello stesso salvataggio,
    con l'audit, come gli scali.
  - **La pagina** `/events/{slug}` (`GET /api/events/public/{slug}`, anonima, `Public/PublicEvents.cs`): banner, stato e tipo, quando in
    UTC e nell'ora della divisione, chi organizza (con la sua pagina), gli scali e le rotte con il nome che il nucleo conosce, la nota di
    un annullato, la descrizione. La legge chi è il pubblico dell'evento **mentre si vede** (`EventState.IsSeen` sul filtro globale: un
    evento `Members` solo a chi è entrato); **lo staff degli eventi** (`Events.View` sulla riga, chiesto all'unico handler) **in ogni
    stato**, con una riga che dice che nessun altro la vede e perché — `unseen` del DTO, da `EventState.Unseen`: bozza, non ancora
    visibile, concluso; lo stato da solo non distingue un annullato non ancora visibile da uno concluso —, più il link al back office;
    tutti gli altri **404**.
  - **Il blocco `events.eventList`** (sempre vivo, le due metà; `EventListProvider`, proprietà `kinds` e `limit`): gli eventi che si
    vedono, in arrivo e in corso, annullati compresi fino alla fine, dal più vicino. **`/events` lo legge** come `/calendar` legge il blocco
    del calendario, quindi la lista non ha un endpoint suo: schede (`screens/EventCards.tsx`, le stesse del blocco, con gli scali per
    codice e nome come la pagina: `AirportName`), filtri per tipo e per scalo nell'indirizzo (le scelte sono quelle che le schede hanno;
    lo scalo si sceglie per codice e nome, e l'indirizzo tiene il codice), il `CalendarView` sugli stessi eventi meno gli annullati. ⚠️ **Al
    più 50**: `/events` chiede al blocco `limit: 0`, e il server dà a una lista al più `PublicEvents.MaxItems` (50, il limite di una lista
    dei blocchi del nucleo, `DataBlockScope.MaxItems`), i più vicini. Una divisione che annunciasse più di cinquanta eventi insieme
    vedrebbe i cinquanta più vicini, con i filtri e il calendario su quelli; gli altri entrano man mano che i primi finiscono, e
    nessuna riga dice che ce ne sono altri. Oggi è lontano; se servisse, la pagina avrebbe una lettura sua, a pagine.
  - **`EventState.Seen(now)`**: `IsSeen` scritto in quello che SQL chiede, tenuto alla stessa risposta da `EventsStateTests`.
  - **La scheda «Rotte»** della pagina dell'evento nel back office (lista e form generati, `/staff/events/{id}/routes/{routeId}`), a chi
    legge l'area, con «Nuova rotta» a chi la scrive; e il link «La sua pagina sul sito».
  - **«Pubblica» aspetta le impostazioni salvate** (l'⚠️ che E3b lasciava a E4): spento, con una riga che dice perché, finché il form ha
    modifiche non salvate.
  - **I tipi di un evento sono quelli degli eventi** (nota `2026-10-06-i-tipi-che-un-evento-sceglie`, decisa da Carmine): quelli con una
    riga in `kindPresets`, e tutti i tipi del calendario finché non ce n'è nessuna. Il form li offre (`eventKinds` in `schemas.ts`), il
    server rifiuta gli altri su un evento nuovo o al cambio di tipo (`events:errors.kindNotOfEvents`, in `EventSaving.PrepareAsync`), e un
    evento già scritto tiene il suo tipo anche se la riga va via. ⚠️ Sul banco e sulla prova la scelta resta com'è finché le impostazioni
    degli eventi non hanno righe: per IT le scrive la divisione (RFE, RFO, MSE, Online Day e l'evento libero, con tutto spento).
  - **I test**: `EventsPublicTests` (integrazione, 4, VID 761033–761036, scali `XEC1`/`XEC2`, slug `evt-test-e4-…`), `EventsStateTests`
    (+2), un test dei tipi in `EventsStaffTests`, `screens/cards.test.ts`, `schemas.test.ts` ed `eventForm.test.ts` (vitest), la smoke
    `web/e2e/events-public.spec.ts` (8), il giro `web/e2e/full/events-public.spec.ts` (1), un terzo test in
    `web/e2e/full/events-staff.spec.ts` e, nel primo, i tipi che il form offre. `EventsTestRows` toglie anche le rotte.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Con E4 unita dopo la #221 cade l'⚠️ di E3b** («nessuna consegna e nessun «Pubblica» sulla prova fra E3b ed E4»): la voce di
    calendario e la riga di ricerca trovano la loro pagina. Quando consegnare resta di Carmine.
  - **Le tre note di E4 sono decise** da Carmine ([la risposta sulla #223][ok223], 6 ottobre 2026, autore `SkyMistery`, pubblicata dal
    master su sua istruzione): `2026-10-06-il-pubblico-degli-eventi` (sì alle cinque letture, e **la seconda lettura scritta a mano**,
    `GET /api/events/public/{slug}`, accettata come quella dei preset: uno scostamento dichiarato dal design §7.2, contato per il piano
    §16.6 — nella nota, §4), `2026-10-06-chi-e-online-sugli-scali-di-un-evento` (la `LiveStatusStrip` sugli scali dell'evento, design §7.1,
    **non è di E4**: è la fase del nucleo **E4b**, in una PR sua con la sua nota; non è urgente, può venire dopo E5, e la monta la prima
    fase del modulo dopo di lei) e `2026-10-06-i-tipi-che-un-evento-sceglie` (come raccomandato, fatta su questa PR: sopra).
  - **`search.kinds.events`** (la parola del tipo `events` nella ricerca) è arrivata con la #222 del nucleo, e il merge di `main` su questo
    branch l'ha tenuta, senza conflitti ([i rilievi sulla #223][r223], punto 3).
  - **E5** (gli slot pubblici) è **in coda sul branch di E4**: la sessione che coordina la prepara dalla testa `94ca28b` di E4, e unisce
    l'ultima spinta di E4 — con il merge di `main`, i tipi degli eventi e le risposte di Carmine. La lista pubblica degli slot va sulla
    pagina — `PublicEvents.ReadAsync` e `PublicEventDto`, oppure una lettura sua accanto —; la scheda «Slot» accanto a «Rotte»;
    `EventSlot` è `IEventChild` come le rotte. E5 non aggiunge blocchi.
  - ⚠️ **`EventSaving.DeleteAsync` elimina le righe figlie di ogni area** (scali, rotte): una fase che aggiunge una tabella figlia (slot E5,
    postazioni E11a, regole di award E14b) la aggiunge lì, e il guardiano chiederà a chi elimina l'`Edit` di quell'area — EC ed EAC tengono
    tutte le aree tranne `EventReports.Edit` (§6.2), che non ha righe figlie dell'evento.
  - **E6b** (il promemoria con la rotta del FOD) e **E8b** («Duplica» copia le rotte) trovano le rotte per evento; fra gli stessi due scali
    ce ne può essere più d'una, e le note dicono quale. L'ordine è quello di scrittura (l'`Id`): «Duplica» le copia in quell'ordine, o
    la copia le mostra in un altro.
  - ⚠️ **`EventsArchitectureTests` vieta fra apici una chiave seminata del calendario anche fuori da un tipo** — una chiave di query
    `'event'` è caduta al primo giro (ora `'page'`) — **e un ICAO di quattro maiuscole**: gli esempi della galleria usano `XX01`, `XX02` e il
    tipo `gallery`.
  - **`/events` non è nel menu pubblico**: si aggiunge dall'editor del menu, come `/tours` e `/training` (un dato, non codice).
  - ⚠️ Lo snapshot del modello degli eventi (`EventsDbContextModelSnapshot`) ha preso con `AddEventRoutes` anche la colonna `notified_at`
    di `cms_award_signals` (E10d), una tabella del nucleo mappata ed esclusa dalle migrazioni del modulo: nessun SQL, solo il modello al
    passo. La prossima migrazione degli eventi non la ritrova.

[r223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6014660539
[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

### Che cosa ha lasciato E3b (5 ottobre 2026, branch `m4/e3b-event-life`, PR #221, senza coda)

- **Che cosa c'è** (il dettaglio in `10`, E3b, «Com'è andata»; nessuna migrazione, nessun file del nucleo; dopo la revisione una nota,
  `decisions/2026-10-06-le-regole-di-pubblica.md`, decisa da Carmine: le tre regole qui sotto — pubblicabile dopo «Pubblica»,
  `bookingOpensAtUtc` per un evento con slot, il tipo `events` nella ricerca — e la scelta sui file di una bozza):
  - **«Pubblica»** (`POST /api/events/events/{id}/publish`, `Events.Edit` sulla riga): i controlli in una classe sola, `EventPublishing`
    (`src/IvaoHub.Modules.Events/Staff/EventPublishing.cs`), con i `Refusals` campo per campo — titolo e riassunto in ogni lingua, le date
    in ordine (visibile ≤ prenotazioni ≤ inizio < fine), per un evento con slot l'apertura delle prenotazioni e uno scalo (campo
    `airports`), il link per un evento di altri. Una volta; mai su un annullato; 409 su una versione vecchia.
  - **Un evento pubblicato resta pubblicabile**: `EventSaving` rifiuta una modifica con le chiavi di «Pubblica», e l'ultimo scalo di un
    evento pubblicato con slot non si elimina.
  - **La proiezione** (`Event.Project`, `IProjectable`): quando l'evento si vede (`EventState.IsSeen`: pubblicato, da `visible_from` o
    dalla pubblicazione alla fine, annullato compreso) una voce di calendario — tipo e visibilità dell'evento, `/events/{slug}` — se non
    è annullato, e una riga di ricerca per lingua (tipo `events`) se è per tutti; il banner e le immagini della descrizione come usi dei
    file: senza fine finché l'evento è una bozza, fino alla fine + 7 giorni quando è pubblicato.
  - **`events-release`** (`EventReleaseJob`, ogni 15 minuti): riproietta gli eventi pubblicati diventati visibili o conclusi
    dall'inizio dell'ultimo giro riuscito, come `TourReleaseJob`.
  - **`events.eventChanged`** dichiarato (mail e parola del profilo), senza destinatari.
  - **La schermata**: «Pubblica» nella barra della pagina di una bozza; i rifiuti come elenco «campo: che cosa manca» sotto la barra.
  - **I test**: `EventsLifeTests` (integrazione, 6, VID 761011, scalo `XEB1`, orologio dell'host spostato dal test), `EventsStateTests`
    (+1), un secondo test in `web/e2e/full/events-staff.spec.ts`; `EventsTestRows.ForgetAsync` toglie gli eventi di una classe con scali
    e proiezioni (lo usa anche `EventsStaffTests`).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Nessuna consegna e nessun «Pubblica» sull'installazione di prova fra E3b ed E4** (Carmine, 6 ottobre 2026, [sulla #221][seq221]):
    la voce di calendario e la riga di ricerca di un evento pubblicato puntano a `/events/{slug}`, che porta solo E4.
  - ⚠️ **«Pubblica» pubblica la riga salvata**: il form delle impostazioni si ridisegna sulla riga pubblicata (la sua chiave è la
    versione della riga), e una modifica non salvata si perde senza un avviso (dalla lettura del codice, non provato nel browser; il
    revisore l'ha lasciato fra i non verificati). La fase che tocca di nuovo la pagina (E4, con la scheda delle rotte) può spegnere
    «Pubblica» finché il form ha modifiche.
  - **E4** trova in `EventState.IsSeen` la regola di chi vede un evento (il pubblico: pubblicato, visibile e non concluso; annullato
    compreso, con la nota) e l'indirizzo `/events/{slug}` che calendario e ricerca usano già: la pagina pubblica deve rispondere lì.
    ⚠️ Un evento `Members` è nel calendario dei membri e mai nella ricerca (§8.1).
  - ⚠️ **Il tipo della riga di ricerca è `events`** (la chiave del modulo): `event` è una parola seminata del calendario, e
    `EventsArchitectureTests` vieta al modulo di scriverne una. Il distintivo della ricerca mostra la chiave finché `common.json` (nucleo)
    non ha `search.kinds.events`, come oggi `tour`.
  - ⚠️ **Ogni scrittura di un evento pubblicato** passa da `EventPublishing`: una fase che aggiunge un controllo a «Pubblica» lo aggiunge
    lì, e vale anche per le modifiche; una riga figlia la cui assenza rende l'evento non pubblicabile (come l'ultimo scalo) rifiuta il
    suo `Delete` allo stesso modo.
  - ⚠️ **E6a** manda `events.eventChanged` dopo il salvataggio di un evento pubblicato i cui orari cambiano (il punto è
    `EventSaving`), con i segnaposto proposti `{{event}}`, `{{starts}}`, `{{ends}}`, `{{url}}` o altri suoi.
  - ⚠️ **Gli eventi scritti prima di E3b** (banco, installazione di prova) non hanno proiezioni finché non si salvano: oggi sono bozze, e
    il banner diventa un uso alla prossima scrittura.
  - ⚠️ **Un test che sposta l'orologio** usa un host derivato (`WithWebHostBuilder` + `ConfigureTestServices` con un `IClock` suo, come
    `EventsLifeTests`), mette in pausa il job nello scheduler, toglie le righe di `hub_jobs_log` del job all'inizio e non fa richieste
    firmate mentre l'orologio è spostato.

[seq221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670

### Che cosa ha lasciato E3a (1 ottobre 2026, branch `m4/e3a-event-staff`, PR #214, in coda dopo la #212)

- **Che cosa c'è** (il dettaglio in `10`, E3a, «Com'è andata»; nessuna migrazione; una nota, dopo la revisione:
  `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`, decisa da Carmine):
  - **Lo stato dalle date** in una funzione sola, `EventState.Of` (`src/IvaoHub.Modules.Events/EventState.cs`), e le cinque viste della
    lista (`EventViews`, `filter[view]`: bozze, prossimi, in corso, conclusi, annullati), scritte anche in SQL e tenute alla stessa
    risposta da `EventsStateTests`. Ogni istante è il primo momento di ciò che apre; annullato vince su tutto, bozza su ogni data.
  - **L'evento nel CRUD** (`/api/events/events`, `src/IvaoHub.Modules.Events/Staff/`): `Events.View`/`Edit`, `DeletePolicy`
    `Events.Delete`; `EventSaving` (indirizzo libero, tipo del calendario e attivo quando lo si sceglie, niente scali su un evento di
    tutta la divisione; eliminare porta via gli scali); **gli scali** (`/api/events/airports`, `EventAirportEndpoints`): righe
    `IEventChild` che adottano dipartimento e maschera dell'evento in `BeforeAuthorize` (`EventChildren.AdoptAsync`), un aeroporto del
    nucleo una volta, la capacità in movimenti oppure in arrivi e partenze.
  - **Due endpoint a mano**: **annulla** (`POST /{id}/cancel`, `Events.Edit` sulla riga: quando, chi, la nota in ogni lingua; una volta;
    mai su un evento concluso; 409 su una versione vecchia) e **i preset dei tipi** (`GET /api/events/kind-presets`, `Events.Edit`):
    il form li legge per preimpostare gli interruttori al cambio del tipo, perché le impostazioni le legge solo chi le gestisce.
  - **Le schermate**: `/staff/events` (lista con il filtro delle viste), `/staff/events/{id}` (impostazioni, descrizione con l'editor
    dei blocchi, scali; «Annulla l'evento» ed «Elimina» a chi il server lo permette), `/staff/events/{id}/cancel`,
    `/staff/events/{id}/airports/{airportId}`. Il tipo di notifica **`events.eventCancelled`** è dichiarato, senza destinatari.
  - **I nove grant di chi collabora** (AOD, FOD, MD) nei due file della divisione, e `EventsArchitectureTests` che li vuole.
  - **I test**: `EventsStateTests` (unità, 6), `EventsStaffTests` (integrazione, 7, VID 761006–761010, aeroporti `XEA1`/`XEA2`),
    `eventForm.test.ts` (vitest, 4), `web/e2e/full/events-staff.spec.ts` (il personaggio dell'ED: un RFO con due scali creato,
    riaperto, annullato; una bozza vuota eliminata).
- **Che cosa deve sapere la fase dopo**:
  - **E3b** trova pronti gli stati che «Pubblica» accende (`Scheduled`, `Announced`, `BookingOpen` vengono da `Status == Published` e
    dalle date) e le viste; «Pubblica» è un verbo accanto ad «Annulla» in `EventEndpoints`, con i `Refusals` del design §2.2.
    ⚠️ Sul banco oggi ci sono solo bozze: le viste «prossimi», «in corso», «conclusi» le prova l'integrazione con righe scritte sul
    database.
  - ⚠️ **`SchemaForm` disegna i default una volta**: il form dell'evento si ridisegna (chiave nuova, stessi valori) quando cambia il
    tipo; l'indirizzo proposto dal titolo smette di seguirlo dopo il ridisegno se il titolo c'era già.
  - ⚠️ **E11a ed E16** portano `has_roster` e `in_person` nel form e anche in `presetSwitches`, `EventWriteDto` ed `EventMapper.Apply`:
    oggi il form ne applica tre.
  - ⚠️ **Ogni riga figlia nuova** (rotte E4, slot E5, postazioni E11a, regole di award E14b) implementa `IEventChild` e adotta l'evento
    con `EventChildren.AdoptAsync`; nel browser `writableDepartments` non offre l'ED a chi collabora (E2b): la riga si crea sotto
    l'evento. Chi collabora vede oggi la pagina dell'evento con il form senza «Salva» e gli scali.
  - ⚠️ **E6a** mette il primo rifiuto di «Elimina» in `EventSaving.DeleteAsync` e i destinatari di `events.eventCancelled` dopo il
    salvataggio in `CancelAsync`; la fase che manda la prima di quelle mail **rimette sulla pagina «Annulla»** la frase su chi la riceve
    (`events:cancel.description` e l'aiuto della nota), tolta dopo la revisione della #214 perché oggi non parte nessuna mail.
  - ⚠️ **Il banner non è ancora un uso di un file** (`Event` non è `IProjectable`): fino a E3b un banner si può eliminare dalla libreria
    mentre un evento lo mostra (rilievo 4 del revisore sulla #214). E3b subito dopo.
  - **La lettura dei preset** (`GET /api/events/kind-presets`) **resta com'è**: Carmine l'ha accettata il 1 ottobre 2026
    ([la sua risposta sulla #214](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5934118725)), uno scostamento dal
    design §7.2 («gli endpoint a mano sono verbi») scritto nella nota `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`; nessun
    permesso di lettura in `ModuleSettingsDescriptor`. ⚠️ Una fase che vuole un'altra impostazione del modulo nel form di chi non le
    gestisce non la aggiunge qui da sola: è un endpoint a mano nuovo, con la sua decisione (piano §16.6).
  - ⚠️ **Nel back office `PageShell` non disegna la `description`** (solo il tooltip del titolo): lo stato dell'evento sta nella `note`.
  - ⚠️ **`AirportEndpoints` è un nome del nucleo** (`Core/Ivao`): le classi del modulo si chiamano `Event…`.
  - La spec e2e scrive un preset dell'RFO nelle impostazioni del banco e lo rimette com'era, come `events-skeleton.spec.ts` con l'RFE.
  - ⚠️ **Il tipo di un evento si sceglie fra tutte le parole del calendario**, anche training, esame, tour, riunione e scadenza (come i
    preset di E2): nessun tipo del calendario dice di essere di un evento, e il codice non ne conosce nessuno. Un elenco più stretto
    sarebbe una domanda a Carmine e un segno del nucleo, non una lista nel modulo.

### Che cosa ha lasciato E2b (1 ottobre 2026, branch `m4/e2b-grant-without-department`, PR #212, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-il-permesso-non-il-dipartimento.md`, **decisa da Carmine** il 30 settembre, in chat al
  master e pubblicata su sua istruzione [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)):
  - **Un grant a una posizione su un dipartimento che non è il suo** — su un altro, o su tutti — e **un grant al team di un FIR** con
    `firStaffScope: all` danno **il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, scritto dal calcolo
    (`UserGrant.GivesThePermissionNotTheDepartment`) e portato nel claim da un `!` in testa al pezzo dello scope (`Events.View:ED@!`), che un
    lettore che non lo conosce legge chiuso.
  - **Chi lo tiene** non ha il claim `dept` di quel dipartimento (`HubClaims.BuildIdentity`): nessuna riga `Visibility.Department`, nessun
    gruppo nella barra dello staff, nessun'altra lista di quel dipartimento. **La lista generata** che legge con quel permesso ne tiene le
    righe (`TryNarrowToDepartments`, accanto a `onTheirFir`); **l'unico handler e il guardiano** lo tengono sulle righe del dipartimento come
    ogni permesso, e non sono cambiati.
  - **Un grant a una persona** fa ancora entrare nel dipartimento (6 settembre); **lo stesso permesso da fuori e per nome** resta quello
    per nome.
  - **I test**: `PermissionFromOutsideRulesTests` (unità, 8) e `PermissionFromOutsideTests` (integrazione, 4, VID 761091–761094; 761090 è
    un'identità delle unità; 761095–099 restano di E2b per le sue correzioni). **Le parole**: l'aiuto del form dei permessi
    (`grants.formHint`), `docs/FORKING.md` e i commenti di `config/division.example.json`. Nessuna migrazione, nessun endpoint.
  - **Tenuto da fuori su tutti i dipartimenti** (un grant a una posizione senza `scope`), un permesso di lettura dà nella sua lista le
    righe di ogni dipartimento, come i claim `dept` di tutti prima di E2b, e niente altro di loro; un divieto su un dipartimento accanto
    toglie quel dipartimento (dopo la revisione della #212).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I nove grant di chi collabora non ci sono**, nemmeno dopo l'unione di E2 (#209) in questo branch: li porta **E3a** (`10`, E3a
    punto 5), la cui sessione nasce sopra E2b, in coda dopo la #212, in `config/division.json` e in `config/division.example.json`, e cambia
    `EventsArchitectureTests` di E2, che li rifiuta finché la nota non ha risposta — ora ce l'ha. **Misurato** sopra E2b con E2 e i nove
    grant: integrazione intera 443/443, i tre test del maintainer verdi senza toccarli; fra le unità l'unico rosso è quel test di E2.
  - **Che cosa vedranno AOD, FOD e MD** (nota §3.7): gli eventi e le liste della loro area, con le righe dell'ED, ogni riga con l'unico
    handler, la sezione «Eventi»; non le righe che l'ED tiene per sé, non il gruppo dell'ED nella barra, non i suoi contatti né i suoi
    contenuti.
  - ⚠️ **`writableDepartments`** nel browser non offre il dipartimento di un permesso da fuori: una schermata che fa creare una riga a chi
    collabora (una rotta del FOD) la crea sotto l'evento, con maschera e scope dell'evento, e chiede al server le `actions`.
  - ⚠️ **Un test che prova che cosa può chi collabora** entra come una posizione di quel dipartimento, senza indirizzo, e mai come il web
    master del banco, che raggiunge ogni dipartimento; dopo aver scritto un grant rifà l'ingresso.
  - **E10f (#213) e E2b sono riconciliate in questo branch** (E10f è entrata prima): **un solo `EffectivePermission.FromOutside`** con le
    due vie nel suo `<param>`, una condizione sola nel calcolo — `fir is null && (grant.GivesThePermissionNotTheDepartment ||
    catalogue.IsGlobal(grant.Value))` —, un solo `.ThenBy(FromOutside)`, una sola riga in `BuildIdentity` con i due commenti, e le due
    aggiunte in `docs/FORKING.md` e in `config/division.example.json`. ⚠️ **Il segno ora viaggia anche per i globali**: chi assegna gli
    award per grant ha nel cookie `Awards.Assign@!` e `Awards.View@!`. Ogni riga di `Award` resta aperta: il catalogo è condiviso in
    lettura (`Award` è `ISharedForReading`, la sua lista dice `SharedForReading = award => true`) e l'unico handler risponde a una lettura
    con `HasAny(Awards.View)`, che non guarda né il dipartimento né il segno; e `Awards.View@!` si rilegge senza dipartimento, cioè su
    tutti (`10`, E2b, «Com'è andata»).
  - **Il team di un FIR con `own`** (IT) non cambia: il suo permesso porta il FIR, e il suo claim è quello di prima. ⚠️ Un cookie scritto
    prima di E2b tiene i suoi claim `dept` fino al prossimo ingresso; su IT nessuno ne ha uno da un grant a una posizione su un altro
    dipartimento.

### Che cosa ha lasciato E10c (1° ottobre 2026, branch `m4/e10c-ratings-and-positions`, PR #204, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione.md`, **decisa da Carmine**, in chat
  al master e pubblicata su sua istruzione [sulla #204](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5916282164)),
  tutto in `src/IvaoHub.Core/Ivao/`:
  - **il rating preferito** per un tipo di postazione: **la regola è della divisione**, `config/division.json → preferredAtcRatings`
    (tipo di postazione → sigla del rating ATC; [seconda risposta di Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926811688)),
    controllata all'avvio (`PreferredAtcRatingsValidator`: un tipo o un rating sconosciuti fermano l'hub con un messaggio); il
    nucleo risponde `RatingVocabulary.PreferredFor(tipo)` dal vocabolario che registra (`IvaoRatings.WithPreferred`). I valori di IT
    sono di Carmine: **AS3** su `DEL`; **ADC** su `FSS`, `GND`, `TWR`; **APC** su `APP`, `DEP`; **ACC** su `CTR`; **nessuno** su `ATIS`.
    «Ha il preferito» è `IsAtLeast(Atc, rating, PreferredFor(tipo).Number)`; `null` = nessuno è preferito su quel tipo;
  - **le postazioni della divisione**: `IAtcPositionDirectory.OfDivisionAsync()` (tutte, militari e ATIS compresi) e
    `FindAsync(nominativi)` (a lotti, per nominativo in qualunque maiuscola, come `IAirportDirectory.FindAsync`); **`AtcPositionDto`
    porta il tipo** (`Type`), accanto a nominativo, nome, aeroporto e **FIR** (per una postazione d'aeroporto, quello del suo aeroporto);
  - **il minimo di una postazione è il suo FRA su IVAO**: `MinimaAsync(nominativi)` → un `AtcPositionMinimum` per nominativo, e
    `Over(da, a)` dà il **numero di IVAO** del rating più alto fra gli FRA attivi che valgono in qualche momento del turno (giorno e
    notte, feriali e fine settimana, una data, oltre la mezzanotte); `null` = nessun minimo. Gli FRA stanno in **`ref_ivao_fras`**
    (migrazione del nucleo `AddIvaoFras`, dopo quella di E10d), rinfrescati **ogni notte** con i dati di riferimento, solo le righe per
    postazione (**nessuna persona**); **senza risposta di IVAO restano, con una risposta vuota se ne vanno** (la divisione li ha tolti
    tutti: il rilievo 2 del revisore); il conteggio, o «nessuna risposta», è nel messaggio del giro, non nel suo esito;
  - **le parole dell'avviso** sotto il minimo — togli l'FRA su IVAO per quel controllore —: la chiave del nucleo
    `atcPositions.belowMinimum` (`AtcPositionMinimum.BelowMinimumKey`), perché nominano IVAO;
  - **le misure**: `tools/record-ivao-fixtures.mjs --fras <paese> [ICAO…]`, e la fixture `tests/fixtures/ivao/fras-IT.json` (94 FRA
    delle postazioni del banco, 30 settembre 2026);
  - **i test**: `RatingVocabularyTests`, `IvaoFraReaderTests`, `AtcPositionMinimumTests` e `PreferredAtcRatingsValidatorTests`
    (unità), `AtcPositionTests` (integrazione).
- **Che cosa deve sapere la fase dopo**:
  - **E11a**: la scelta delle postazioni con `OfDivisionAsync()`, il controllo del nominativo e la copia del FIR con `FindAsync`; il
    tipo non va salvato sulla riga (lo sa la directory).
  - **E11b**: «chi può» = almeno `minimumAtcRating` **e** almeno `minima[callsign].Over(turno.da, turno.a)` quando non è `null`, sempre
    con `IsAtLeast` (un numero che il vocabolario non conosce non lo raggiunge nessuno); il proponente **non va mai sotto**. «Chi prima»
    = `PreferredFor(position.Type)`. **La correzione a mano può andare sotto l'FRA**: è un **avviso**, mai un rifiuto, con le parole di
    `atcPositions.belowMinimum` (decisione di Carmine sulla #204); lo stesso per la cessione di un turno (E12).
  - ⚠️ **Il minimo è un numero di IVAO** (`int?`), non un `Rating`: confrontalo con `IsAtLeast`, mai con `>=`.
  - ⚠️ **`IvaoRatings.Vocabulary` (statico) è la scala di IVAO da sola**, senza la regola della divisione: `PreferredFor` vi risponde
    sempre `null`. Il modulo prende il `RatingVocabulary` dal contenitore, che la porta; i suoi test costruiscono un vocabolario di
    prova con la loro mappa (`new RatingVocabulary(ratings, preferred)`).
  - ⚠️ **Gli orari degli FRA si leggono UTC**, come ogni orario di IVAO: non verificato (nessuna fonte lo dice).
  - ⚠️ **Le eccezioni per membro restano su IVAO**: un controllore che lo staff ha già sbloccato su IVAO resta «sotto il minimo» per
    l'hub, e l'avviso compare lo stesso.
  - ⚠️ **L'ordine dei nominativi è quello del database** (`utf8mb4_unicode_ci`: `_` prima delle cifre, `LIRR_NE_CTR` prima di
    `LIRR_NE1_CTR`): un test non lo confronti con un ordinamento `Ordinal`.
  - ⚠️ **E15a ed E10a** toccavano gli stessi file del client di IVAO (`IIvaoApiClient.cs`, `IvaoApiClient.cs`,
    `FixtureIvaoApiClient.cs`), lo strumento delle fixture e il suo README: entrate in E10c con due merge il 1° ottobre, lo strumento ha
    ora le quattro modalità di riferimento (`--positions`, `--bookings`, `--fras`, `--sessions-at`) e il README tutte le sezioni. **E10c
    migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione sopra `main`
    (come ha fatto E10c dopo E10d).

### Che cosa ha lasciato E10f (1 ottobre 2026, branch `m4/e10f-grantable-award-assign`, PR #213, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-chi-assegna-gli-award-con-un-grant.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643), risposta 2):
  - **`Awards.Assign` si dà con un grant, ed è il solo globale così**: il campo `PermissionDescriptor.GrantableAlthoughGlobal`, detto sul
    permesso come `DeniedToStakeholder`, vero solo per `Awards.Assign` in `CorePermissions`. Il catalogo risponde con
    `IsClosedToGrants(name)`, la domanda che fanno il calcolatore, la schermata dei permessi e il seme; e **non nasce** se lo dicesse un
    altro permesso (`Permissions.Manage` per primo, ma anche un altro globale, un permesso di un dipartimento, un globale di un modulo:
    il paletto che il revisore proponeva sulla #213, preso).
  - **Solo intero**: il calcolatore lo tiene (su nessun dipartimento, come un ruolo) solo da un grant senza dipartimento, senza scope e
    non al team di un FIR; un rifiuto intero lo toglie anche a chi lo ha per ruolo (DIR, ADIR, WM, AWM). La schermata rifiuta il dipartimento
    (**`errors.grant.globalDepartment`**, chiave nuova), il team di un FIR (`firTeamArea`, com'era) e gli altri globali
    (`globalPermission`, con un testo nuovo); il seme salta uno `scope`. **`ModuleGrants` rifiuta ancora ogni globale.**
  - **Il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, vero per un grant di un permesso globale e per la
    `Awards.View` che porta; `HubClaims.BuildIdentity` lo lascia fuori dai claim `dept`. Senza, un grant senza dipartimento mette chi lo
    tiene **dentro tutti i dipartimenti** (l'ha trovato la sessione di E2b; misurato: `user.departments` del coordinatore dell'MD
    passava da `["MD"]` a tutti e nove).
  - **La divisione**: `config/division.json` dà `Awards.Assign` al **coordinatore e all'assistente dell'MD** (primo dei `positionGrants`,
    senza `scope`); lo stesso in `config/division.example.json`, e `docs/FORKING.md` spiega l'eccezione.
  - **Il browser**: `/api/me → registries.permissions[].grantableAlthoughGlobal`; la schermata dei permessi offre `Awards.Assign`.
  - **I test**: `GrantableGlobalPermissionTests` (unità, 17), `AwardsAssignByGrantTests` (integrazione, 4, VID 761080–761083),
    `web/src/features/admin/grants/grantable.test.ts` (2); **tolto il caso `Awards.Assign`** da
    `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` (test di Carmine: il cambio l'ha deciso lui).
- **Che cosa deve sapere la fase dopo**:
  - **Il riepilogo di E10d arriva all'MD da solo**, dal primo avvio dopo il rilascio: il seme nuovo si applica una volta, poi si cambia
    dalla schermata dei permessi. **E14b** non cambia niente.
  - ⚠️ **Nei test d'integrazione l'MD ha `Awards.Assign`**, perché l'host legge `config/division.json`: un coordinatore o un assistente
    dell'MD seminato da un test (i contatti seminano `IT-MC` con un indirizzo) è fra chi assegna. `AwardQueueMailTests` conta per le sue
    persone e non se ne accorge; un test nuovo che conta i destinatari del riepilogo faccia lo stesso.
  - **Le tre scelte della sessione sono confermate da Carmine**
    ([sulla #213](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926811970)): coordinatore e assistente dell'MD; il
    rifiuto intero che vale anche per chi lo ha per ruolo; la `Awards.View` su ogni dipartimento, perché chi assegna veda ogni award.
  - ⚠️ **`FromOutside` è lo stesso campo di E2b** (PR #212, decisa sulla #209: un grant a una posizione su un dipartimento non suo dà il permesso
    e non il dipartimento), con la stessa forma: ultimo parametro di `EffectivePermission`, la stessa preferenza nella deduplicazione, la
    stessa riga in `BuildIdentity`. **Il significato non è identico** (il revisore sulla #213): in E2b il segno scrive anche un `!` nel
    cookie, e con tutte e due unite la `Awards.View` portata da `Awards.Assign` si scrive `Awards.View@!`. **Chi arriva seconda su `main`**
    tiene una dichiarazione sola, somma le due condizioni del calcolatore e rifà sul codice unito `AwardsAssignByGrantTests`,
    `GrantableGlobalPermissionTests` e le due classi di E2b; il revisore controllerà che quella `Awards.View` apra ancora ogni riga di
    `Award`. E2b porta anche la metà «liste»: fino ad allora **chi non è dentro nessun dipartimento** (il capo di un FIR a cui si desse
    `Awards.Assign` con un grant a un VID) apre la coda ma ha la lista degli award a 403. L'MD non ne è toccato.
  - Un **permesso globale di un modulo** resta chiuso ai grant: dire `GrantableAlthoughGlobal` ferma l'avvio. Un secondo permesso
    concedibile è una decisione con la sua nota, che cambia anche il paletto del catalogo.

### Che cosa ha lasciato E10a (30 settembre – 1° ottobre 2026, branch `m4/e10a-tracker-without-vid`, PR #210, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-tracker-senza-vid.md`, scelta tecnica; le misure del 30 settembre sono lì, §2, e
  la revisione in §6):
  - **La domanda senza VID**: `IvaoSessionQuery(int? Vid, from, to, departure, arrival, IvaoConnectionType? ConnectionType)` con
    `Limit` (`init`, predefinito 200, **al più `MaxLimit` = 1000**, un tetto che tiene il nucleo); `SearchSessionsAsync` è la stessa
    di prima, dalla più recente, senza doppioni, al più `Limit`, `null` quando IVAO non si è potuto chiedere;
    `IvaoTrackerSessionDto.ConnectionType`. Il giro delle pagine e la regola del tracker stanno nel lettore
    (`IvaoTrackerReader.ReadPagesAsync`, `Answers`), per il client vero e per quello delle fixture.
  - **Il client di IVAO aspetta 20 s per tentativo** (l'interruttore campiona su 40, il totale resta 30): con i 10 standard nessun
    aeroporto si leggeva. **Confermato da Carmine** con il `null` per la ricerca dei tour
    ([risposta sulla #210](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5917033792)).
  - **Le fixture**: `tools/record-ivao-fixtures.mjs --sessions-at`; `tests/fixtures/ivao/tracker-airport-LIRF.json` (una sera di
    LIRF: 10 sessioni di 9 membri, la torre) e `tracker-pages-LIRF.json` (le stesse partenze come le pagina IVAO), **spostate sul 1°
    gennaio 2001** (lo stesso giorno delle prenotazioni di E15a) e **senza `rating`, `serverId`, `software*`** (revisione, #210).
    Senza VID, `FixtureIvaoApiClient` risponde dal file dell'aeroporto chiesto. Unita dopo E15a, lo script ha le due modalità
    (`--bookings` e `--sessions-at`) e **un giorno inventato solo**, `standIn` con `movedFrom(day)`, in cima: chi registra altre righe
    con persone lo usa.
  - **I test**: `IvaoTrackerWithoutVidTests` e `IvaoApiTimeoutTests` (unità).
- **Che cosa deve sapere la fase dopo** (E13a, e chiunque legga il tracker senza VID):
  - ⚠️ **Una domanda per aeroporto che trova qualcosa costa ~10,5 s** (la pagina con l'ultima riga, sempre), e **due insieme
    ricevono 504** dal gateway di IVAO: una alla volta, poche per giro del job.
  - ⚠️ **La finestra è sull'inizio della sessione**, estremi compresi: per chi era già connesso, `FromUtc` va allargato.
  - ⚠️ **«Partenza o arrivo» sono due domande**: chiesti insieme, i due aeroporti vogliono la stessa revisione del piano. Una
    sessione può tornare da tutte e due: si conta per `Id`.
  - ⚠️ **Il DTO dice gli aeroporti della prima revisione**: trovata per la partenza da LIRF, una sessione può dire LIPZ.
  - **Il limite si dichiara**: con esattamente `Limit` sessioni la risposta può essere tagliata, e il resto sta prima della più
    vecchia.
  - **Le VID 761020–761028 sono le persone della sera di LIRF** nella fixture (761025 due volte), e **la sera sta sul 1° gennaio
    2001**, 16:00–17:59:59 UTC: un test che la legge chiede quel giorno. 761029 non è usata.
  - Il punto «La richiesta al tracker oggi» di «Per chi prende M4», qui sopra (`PageSize = 50`, `MaxSessions = 200`), è superato:
    le pagine sono da 100 e il tetto è il `Limit` di chi chiama, al più 1000.
- ⚠️ **Per i tour cambiano due cose, nessuna nei loro test**: le pagine sono da 100, e se IVAO non risponde affatto la ricerca dà
  `null` («tracker non disponibile») invece di lanciare.

### Che cosa ha lasciato E15a (30 settembre 2026, branch `m4/e15a-network-atc-bookings`, PR #207, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-prenotazioni-atc-della-rete.md`, scelta tecnica; le misure con il token vero sono
  lì, §2; **nessuna cache e al più sette giorni per richiesta confermati da Carmine** dopo la revisione, [risposta sulla
  #207](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/207#issuecomment-5916738183)):
  - **La domanda del modulo**: `IAtcBookingSource.BookedAsync(fromUtc, toUtc, callsign?)` in `src/IvaoHub.Core/Ivao/AtcBookingSource.cs`
    (namespace `IvaoHub.Core.Ivao`, che il modulo importa come fa il training: nessun nome del file o del tipo nomina IVAO) → le
    prenotazioni della rete che **si sovrappongono** alla finestra, di tutte le postazioni o di un nominativo intero, nell'ordine in cui
    cominciano; **`null` = non disponibile**, mai «nessuno ha prenotato». `AtcBookingDto(Callsign, StartsAt, EndsAt, Vid, Kind)`, con
    `AtcBookingKind` `Controlling`, `Training`, `Exam`. Al più **`IAtcBookingSource.MaxDays` = 7** giorni di UTC (oltre: `null` e un
    avviso nel log). Alla richiesta, **nessuna cache**, nessun job.
  - **Il client**: `IIvaoApiClient.GetDailyAtcBookingsAsync(DateOnly, position?)` (`/v2/atc/bookings/daily`, il giorno di IVAO com'è;
    predefinito «non disponibile» per i doppi dei test), `IvaoApiClient` con il token dell'applicazione, `FixtureIvaoApiClient`, e **un
    lettore solo** per tutti e due, `IvaoAtcBookingReader` (`IvaoAtcBookings.cs`).
  - **Lo strumento**: `tools/record-ivao-fixtures.mjs --bookings <nome> <asVid> <yyyy-mm-dd> <prefisso…>`, con la persona tolta e
    niente che ritrovi la prenotazione attraverso l'API di IVAO: né `id` né `createdAt`, e il giorno spostato sul 1° gennaio 2001.
  - **La fixture** `tests/fixtures/ivao/atc-bookings-day.json` (VID 761070–761079; il giorno vero non è scritto da nessuna parte) e il
    suo paragrafo nel README delle fixture.
  - **I test**: `AtcBookingTests` (unità, 25 casi), provati al contrario (`10`, E15a, «Com'è andata»).
- **Che cosa deve sapere la fase dopo** (E15b):
  - **Una domanda per l'evento, non una per postazione**: `BookedAsync(inizio, fine)` senza nominativo, poi le postazioni dell'evento
    con un filtro: una chiamata a IVAO per giorno di UTC invece di una per postazione. Il nominativo della domanda, quando c'è, è
    **intero** (con `LIRR` non torna niente: per IVAO sarebbero tre settori).
  - ⚠️ **IVAO lento o giù**: prima di «non disponibile» il client aspetta quello che aspetta ogni chiamata a IVAO (i tentativi e il
    tempo massimo della resilienza, 30 secondi per chiamata), una chiamata per giorno. Carmine lo ha accettato così: la pagina aspetta e
    poi dice che le prenotazioni non sono disponibili, **mentre il resto del roster funziona** — quindi **le prenotazioni si caricano a
    parte dal roster** (una richiesta loro), e il roster non aspetta IVAO.
  - **Il VID, non il nome**: un membro dell'hub con `personName`, chi non è mai entrato con il suo numero. `Exam` e `Training` dicono
    che la postazione è occupata da un esame o da un training su IVAO: vale la pena mostrarlo.
  - **Sul banco (fixture) ogni giorno è lo stesso giorno registrato**: `LIRF_TWR` 18–20 (761079), `LIMC_TWR`, `LFPG_APP`,
    `LIMM_WS2_CTR`, `LFFF_CTR`, `LIRR_SU_CTR`, `LIRR_NC_CTR` 19–21, `LIRR_NE_CTR` 19–22, l'esame di `EDDF_APP` 18:30–20:30, `SBGR_TWR`
    23–01 (UTC). I VID
    761070–761079 non sono nessuno sul banco: la pagina mostra i numeri. Una spec che vuole vedere un controllore del roster anche fra
    le prenotazioni ha bisogno di una fixture con il VID di un personaggio del banco (il roster prende solo chi è entrato nell'hub).
  - ⚠️ **E10a tocca gli stessi file nello stesso giorno** (`IIvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, lo script delle fixture):
    chi viene unita per seconda fonde `main` quando il master lo chiede (mai un rebase), e tiene tutte e due le modalità dello script.
- ⚠️ **Trappole trovate** (nota, §2): la `position` di IVAO è **il principio del nominativo** (`LI` = tutte le italiane); una
  prenotazione a cavallo della mezzanotte è nell'elenco di **tutti e due** i giorni, una che finisce alle 00:00 anche del giorno dopo;
  `date` con un'ora che non è 00:00 dà un elenco vuoto; `user` porta anche `rating`, che la documentazione non dice. ⚠️ **Una
  prenotazione registrata ritrova la persona** con l'API di IVAO dal suo `id`, e anche dal suo giorno con il nominativo (rilievo del
  revisore): chi registra altre prenotazioni lo fa con lo strumento, che toglie tutti e due. La prima registrazione, con quei dati, resta
  nella storia del branch (`fbb11ac`): fuori da `main` la tiene solo un merge a squash, che decide il maintainer.

### Che cosa ha lasciato E2 (30 settembre – 1 ottobre 2026, branch `m4/e2-events-skeleton`, PR #209, la prima fase del modulo)

- **Che cosa c'è** (il dettaglio in `10`, E2, «Com'è andata»):
  - **Il modulo** `src/IvaoHub.Modules.Events/` (referenzia solo il nucleo), registrato **per primo** in `IvaoHub.Web/Modules.cs` e in
    `web/src/modules/index.ts` (eventi, tour, training: la sezione Eventi per prima nel back office, **confermata da Carmine**
    [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)); `EventsDbContext` con
    `__EFMigrationsHistory_events` e la migrazione
    **`Initial`** (`20260930170049_Initial`), che **non si tocca più**: `evt_events` **intera** e `evt_event_airports`.
  - **Le entità** alla radice del progetto: `Event` (maschera, `IVisible`, `IPublishable`, audit, `[Audited]`, scope
    `events:event:{id}` con `Event.ScopeOf`) ed `EventAirport` (maschera, audit, lo scope del suo evento). Nessun endpoint, nessuna
    proiezione, nessun validatore del CRUD ancora.
  - **Il catalogo** `EventsPermissions` (12 permessi, 5 aree, `DeniedToStakeholder` su `EventAtc.Edit` ed `EventReports.Edit`) e **i
    grant** dell'ED (11) e del team di un FIR (2, su `EventAtc.*`) nei due file della divisione.
  - **Le impostazioni** `EventsSettings` (`Settings/EventsSettings.cs`: `kindPresets`, `bookingGapMinutes`, `pilotRetentionMonths`,
    `reminderLeadHours`) con i validatori, e la schermata generata `/staff/events/settings` (`web/src/modules/events/`), l'unica voce
    della sezione «Eventi» del back office.
  - **Il segmento riservato** `events`; le parole `events` in `web/src/modules/events/locales/` (e le copie in `locales/`).
  - **I test**: `EventsArchitectureTests` ed `EventsSettingsTests` (unità), `EventsSkeletonTests` ed `EventsXxDivisionTests`
    (integrazione, VID 761001–761005), `web/src/modules/events/schemas.test.ts`, `web/e2e/full/events-skeleton.spec.ts` (il personaggio
    dell'ED, `?as=events`); le cinque righe `evt_` in `ErasureTests`, con la nota breve
    `2026-09-30-le-colonne-degli-eventi-in-erasuretests`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I grant di AOD, FOD e MD non ci sono, e aspettano E2b.** Un grant sull'ED fa entrare chi lo tiene nell'ED per tutto quello che
    vede (`HubClaims.BuildIdentity`), e i nove del design sarebbero i primi grant a una posizione fra due dipartimenti: con loro la suite
    d'integrazione intera ha tre rossi del maintainer (`SeveralDepartmentsTests` ×2, `SearchEndpointTests`). **Carmine ha deciso la (b)**
    della nota `2026-09-30-i-grant-di-chi-collabora-sugli-eventi`
    ([sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)): un grant a una posizione su un
    dipartimento che non è il suo dà il permesso, non il dipartimento, in una fase del nucleo a sé, **E2b**, prima di E3a; i nove si
    seminano dopo di lei. `EventsArchitectureTests` li tiene fuori dai due file finché la tabella del design non li riprende: **un grant
    del seme applicato non si toglie più togliendolo dal file**. E3a, che prova FOD e AOD sull'evento, aspetta E2b.
  - ⚠️ **I test d'integrazione degli eventi avviano l'host con `useIvaoFixtures: true`** (l'avviso di E10b qui sotto): senza, un host
    chiede un token a IVAO quando `ref_ivao_centers` è vuota. `EventsSkeletonTests` si riprende alla fine di ogni test i grant e le
    posizioni che dà ai suoi VID; i membri restano, come in ogni classe.
  - **Le colonne di `evt_events`**: `starts_at_utc` ed `ends_at_utc` obbligatorie, `visible_from_utc` e `booking_opens_at_utc`
    facoltative, `shift_minutes` e i tre limiti vuoti = l'impostazione, `visibility` è una colonna (E3a la chiude a `Public` e `Members`
    nel validatore), `kind` lunga come una chiave del calendario (32). Le regole di «Pubblica» sono di E3b.
  - **E3a** scrive l'interfaccia delle righe figlie dell'evento (come `ITourChild`) con `CrudOptions.BeforeAuthorize`, e aggiunge
    `AddValidatorsFromAssemblyContaining` con la prima risorsa del CRUD (come A5 per il training): E2 non li ha.
  - **Le due verifiche del design §6.3** non chiedono il nucleo: l'intestazione per lo staff dei FIR da un endpoint dell'area ATC con
    `EventAtc.View` senza risorsa, le disponibilità per postazione con l'unico handler chiesto sulla postazione (`10`, E2, «Le due
    verifiche»). Le scrive in codice E11a.
  - ⚠️ **Fino a E11a ogni avvio scrive due avvisi** sui grant del team di un FIR («not applied: no row of its area says its FIR»): è
    voluto; il primo avvio con una riga `IHasFir` dell'area `EventAtc` li applica.
  - **La schermata delle impostazioni** offre, accanto ai tipi del calendario, un tipo che un preset nomina e il calendario non ha più
    (con la sua chiave come parola), perché la riga si veda e si tolga: il salvataggio la rifiuta sulla riga.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`** in nessun worktree (30 settembre): le spec la tollerano, come in CI.

### Che cosa ha lasciato E10d (30 settembre 2026, branch `m4/e10d-award-assigner-mail`, PR #205, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643)):
  - **Il riepilogo a chi assegna gli award**: `AwardQueueMailJob` (`award-queue-mail`, `src/IvaoHub.Core/Awards/`) gira una volta al
    giorno all'ora di **`division.json → awardDigestTime`** (`HH:mm` nell'ora della divisione, **07:00** se manca; IT non la scrive).
    Legge la coda del nucleo e, **solo se** sono entrati segnali nuovi, manda a chi ha `Awards.Assign` (`IPermissionHolders`,
    superadmin compresi) **una mail**: quanti segnali nuovi, quanti in attesa, una riga per motivo e award proposto con il numero,
    **senza VID**, il link a `/staff/awards/queue`. Tipo del nucleo **`award.toAssign`**: nel profilo da solo, spegnibile.
  - **Il segno** `cms_award_signals.notified_at` (migrazione del nucleo `AddAwardSignalNotifiedAt`; le righe già in coda sono segnate
    come dette): un segnale si racconta una volta sola, nello stesso salvataggio delle righe della mail, e uno gestito o scartato prima
    del giro non si racconta.
  - **Per ogni modulo**: il job non guarda `source_module`, e il modulo dei tour non è cambiato.
  - **I test**: `AwardQueueMailTests` (integrazione: un segnale dei tour e uno di prova; chi l'ha spento e chi non ha il permesso; una
    volta sola; l'ora dell'host), `AwardQueueMailLinesTests` e `AwardDigestTimeTests` (unità).
- **Che cosa deve sapere la fase dopo**:
  - **E14b** proietta i segnali e basta, senza chiamare niente; la mail parte all'ora di `awardDigestTime` dopo il segnale. Il suo
    «fatta quando» sul banco è il segnale in coda: il banco non ha nessuno con `Awards.Assign` e una casella (il web master non ne
    ha), e il job gira solo alla sua ora. La mail la prova `AwardQueueMailTests`.
  - ⚠️ **Oggi l'MD non ha `Awards.Assign`**: è un permesso globale, e un grant non dà mai un permesso globale (nota §5). Coda e mail
    sono di DIR, ADIR, WM, AWM e dei superadmin. **Carmine ha deciso** che diventi concedibile, **in una fase del nucleo sua** (la
    prepara la sessione che coordina, non E10d): quando ci sarà, la mail arriverà all'MD da sola.
  - ⚠️ **Gli snapshot dei contesti dei moduli** vedono `notified_at` solo al loro prossimo `migrations add` (lo scarto innocuo di
    T4b). Una fase che fa nascere o migra un contesto dopo E10d se la trova nello snapshot come tabella esclusa: è giusto così.
  - ⚠️ **In un test d'integrazione un job del nucleo gira da solo nell'host**: un test che lo fa girare lo mette prima in pausa
    (`ISchedulerFactory`, come `TourTests`) e passa `useIvaoFixtures: true` (l'avviso di E10b qui sotto).
  - ⚠️ **E10d migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione
    sopra `main`.

### Che cosa ha lasciato E10e (30 settembre 2026, branch `m4/e10e-great-circle-core`, PR #206, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo.md`, scelta tecnica, con una richiesta a
  Carmine):
  - **La distanza fra due aeroporti nel nucleo**: `GreatCircle.DistanceNm(GeoPoint, GeoPoint)`, in miglia nautiche, e
    `GreatCircle.DistanceNmRounded`, al decimo (una metà va al decimo pari), con `GeoPoint(Latitude, Longitude)` in gradi — in
    `src/IvaoHub.Core/Airspace/GreatCircle.cs`, namespace **`IvaoHub.Core.Airspace`**. È il codice dei tour con i loro numeri: la
    distanza fra i due aeroporti, non le miglia volate.
  - **Come la usa un modulo**: chiede a `IAirportDirectory.FindAsync` dove sono gli aeroporti (un `FindAsync` solo per tutte le voci),
    prende le coordinate con un pattern (`is { Latitude: { } …, Longitude: { } … }`: un aeroporto senza coordinate non ha distanza) e
    misura con `GreatCircle`. Nessuna registrazione, nessuna migrazione, nessun endpoint.
  - **I test**: `tests/IvaoHub.UnitTests/GreatCircleTests.cs` (unità, 8): le domande di `LegTests` al nucleo con le stesse risposte,
    gli antipodi e l'antimeridiano, e **il test gemello**, che confronta il nucleo e la copia dei tour bit per bit su 35.721 coppie.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Il namespace è `IvaoHub.Core.Airspace`, non `IvaoHub.Core.Ivao`** (accanto ai contorni dei FIR, geometria che non è di IVAO):
    in `Ivao` gli stessi nomi fanno cadere la build dei tour (`CS0104` su `TrackChecks.cs`, provato; nota §2 punto 3).
  - ⚠️ **La copia dei tour c'è ancora** (`src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs`): la toglie una sessione di Carmine
    **dopo l'unione di E10e** ([sua risposta sulla #206](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916695685),
    alla [richiesta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916051005); nota §5). Fino ad allora il test
    gemello tiene le due copie uguali; con il passaggio se ne va anche lui. **Sì anche alla riga in `CLAUDE.md` §2** (la distanza fra
    due aeroporti è `GreatCircle` del nucleo, mai una copia), che aggiunge il master.
  - **E14a ed E14b** la trovano qui: la distanza di una voce di un PIREP con `DistanceNmRounded` se la colonna è al decimo, come
    `fo_legs.distance_nm`, e `MinLegDistance` confrontata con quel numero.
  - Agli antipodi `h` può passare 1 di un'unità nell'ultima cifra, ma la radice lo riporta a 1: nessun NaN, misurato (nota §6).

### Che cosa ha lasciato E10b (30 settembre 2026, branch `m4/e10b-shared-sessions-by-vid`, PR #208, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-sessioni-condivise-per-vid.md`, scelta tecnica, nessuna domanda nuova), tutto in
  `src/IvaoHub.Core/Atc/`:
  - **il VID in ogni presenza**: `AtcPresence.Vid` (`int?`, una proprietà `init` sotto il costruttore), dalla colonna `vid` della
    vista; `null` dove l'archivio non lo dice (un doppio di un test), che non è il VID di nessuno. Non esce dai tour (le loro risposte
    si costruiscono campo per campo): il contratto OpenAPI non cambia;
  - **la domanda nuova** `IAtcActivitySource.SessionsOfAsync(vid, fromUtc, toUtc)`: le connessioni di un controllore aperte
    nell'intervallo, nella forma della domanda dei tour (`AtcActivity`: le presenze e da quando l'archivio è completo, per la divisione
    e per il mondo). Un controllore per domanda: vIPI indicizza `(UserId, StartUtc)`;
  - **«non disponibile»** (`null`) con `atcData.source: none`, con un archivio scritto prima di E10b (la domanda ha un corpo
    predefinito nell'interfaccia, come `IIvaoApiClient.GetAtcPositionsAsync`), con un archivio che non si legge;
  - **corretto**: con `atcData: vipi` e senza `ConnectionStrings:AtcData` **l'hub non partiva** — il contesto della vista si
    costruiva con la sorgente, fuori dal suo `try`, e all'avvio il seeder dei contenuti la costruisce attraverso i blocchi dei tour —;
    ora parte e risponde «non disponibile» (nota §4, un commit a sé);
  - **i test**: `AtcActivitySourceTests` (integrazione), con la vista di vIPI in **un database suo** e all'utente dell'hub solo il
    `SELECT` sulla vista.
- **Che cosa deve sapere la fase dopo**:
  - **E11b** (l'esperienza, design §4.3): una domanda per candidato, `SessionsOfAsync(vid, adesso − experienceMonths, adesso)`, e
    `Of(callsign)` per una postazione. «Più spesso» (sessioni o minuti) e «quel tipo» sono del modulo, e **il tipo di un nominativo lo
    dà la directory di E10c**, non questa interfaccia (la colonna `position` della vista non passa). `null` = il criterio non c'è; una
    lista vuota = nessuna esperienza.
  - **E13a** (la presenza, §4.5): `SessionsOfAsync(vid, turno.da, turno.a).Of(callsign)` per il titolare; chi ha coperto:
    `OnlineAsync(turno.da, turno.a).Of(callsign)` con un `Vid` diverso dal titolare. ⚠️ I minuti dentro il turno li conta il modulo:
    una connessione ancora aperta ha `EndedAt` nullo, una riconnessione sono due connessioni, e la prima può cominciare prima del
    turno. ⚠️ `Covers(callsign, turno.da)` falso vuol dire «non si sa» (`Unknown`), non un no-show. Con `null` il ripiego è il tracker
    per VID con `connectionType=ATC` (E10a): la scelta fra le due fonti è del modulo.
  - ⚠️ **`Vid` è `int?`**: `presence.Vid == turno.ControllerVid` con un `null` non conferma nessuno, ed è voluto.
  - ⚠️ **E10b non tocca `Core/Ivao/`**, a differenza di quello che dice «Per chi prende M4» (con E10a ed E15a): l'archivio è
    `Core/Atc/`, e non è il client di IVAO.
  - ⚠️ **Un host dei test d'integrazione senza `useIvaoFixtures` chiede un token a IVAO** quando `ref_ivao_centers` è vuota (la
    sincronizzazione all'avvio, `HubPipeline`): rifiutato con un 400, ma sono chiamate vere, cinque a ogni avvio finché la tabella
    resta vuota. I test degli eventi che avviano un host passino `useIvaoFixtures: true`, come `AtcPositionTests` e
    `AtcActivitySourceTests`.
  - `docs/FORKING.md` e `config/division.example.json` descrivono l'archivio per i tour, e restano veri: la metà degli eventi la scrive
    E15b.

### Che cosa ha lasciato E1 (30 settembre 2026, branch `m4/e1-calendar-kinds`, PR #200, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md`, scelta tecnica, nessuna domanda nuova):
  - **I quattro tipi degli eventi nel calendario**: `rfe`, `rfo`, `mse` e **`online-day`** in `seed/calendar-kinds/kinds.json`, blu come
    `event` e subito dopo (`sort` 11–14), con le etichette «RFE», «RFO», «MSE», «Online Day» (`seed.calendarKinds.*` in
    `locales/*/seed.json`). Arrivano anche a un database già avviato, e una chiave scritta a mano prima resta com'è: il seeder lo
    faceva già (A2 di M3), nessun codice cambiato. Il bootstrap (`/api/me` → `calendarKinds`) li porta a chiunque, visitatori compresi.
  - **Il coordinatore degli eventi sul banco**: `POST /e2e/signin?as=events`, VID **999005**, «Bench Events», posizione **`IT-EC`**,
    nessuna casella di Mailpit, nessun rating né ora. Oggi ha solo quello che la matrice dà a un coordinatore sul suo dipartimento
    (contenuti, link, media, calendario dell'ED); **i permessi degli eventi glieli dà il seme dei `positionGrants` di E2**.
  - **I test**: `CalendarKindSeedTests` (integrazione), `CalendarKindsXxDivisionTests` (integrazione, il fork «XX» su
    `ivaohub_xx_kinds`), `CalendarKindSeedFileTests` (unità: ogni tipo del seme passa dal validatore del back office),
    `web/e2e/full/events-bench.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **La chiave dell'Online Day è `online-day`**, non `onlineDay` come scrivono il design, la nota `i-tipi-di-evento` e il piano: la
    chiave di un tipo ha la forma di uno slug, e il back office la rilegge a ogni salvataggio. **Confermata da Carmine** il 30
    settembre ([risposta sulla #200](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912372176)). **E2** scrive
    `online-day` nei test di `kindPresets` (le chiavi che esistono nei tipi), **E3a** la trova nel bootstrap; nessun codice del modulo
    nomina un tipo.
  - ⚠️ **Le spec degli eventi entrano con `?as=events`**, non come il web master del banco: `IT-WM` raggiunge ogni dipartimento e ha
    ogni permesso di ogni modulo, quindi con lui una spec passa con qualunque `positionGrants`. Dopo E2 il personaggio ha i permessi
    degli eventi dal seme dei grant; ⚠️ un grant scritto fa rientrare il titolare (`CONTRIBUTING.md`, «Traps»): una spec che ne scrive
    uno rifà `/e2e/signin?as=events`. Quando una mail dello staff degli eventi dovrà arrivare a lui, la fase che la manda gli dà una
    casella (`E2EStaffOptions` non ne ha: la classe col campo `Email` è quella del trainer).
  - **E2 tenga d'occhio `web/e2e/full/events-bench.spec.ts`**: afferma `departments: ['ED']` e `hasAllDepartments: false` per il
    personaggio; un grant di E2 con uno `scope` diverso da ED per l'`IT-EC` (la nota `chi-lavora-sugli-eventi` non ne prevede) la
    farebbe cambiare.
  - Il commento di `TrainingSettings.ConflictKinds` («the online day joins when M4 makes its kind») è ora vero a metà: il tipo c'è,
    `online-day`; il predefinito resta `["event"]` e la divisione lo aggiunge dalle impostazioni del training.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`**: né la cartella principale né gli altri worktree hanno `tiles/basemap.pmtiles` (30
  settembre); le spec che disegnano una mappa tollerano il 404 di `/tiles/`, come in CI, quindi il giro passa lo stesso.

### Che cosa ha lasciato E0 (29 settembre 2026, branch `m4/e0-decisions`, PR #184)

- **Che cosa c'è**: dieci note in `decisions/`, una per decisione o gruppo coerente di §17 del design, ognuna con il link al
  commento di Carmine sulla #180 che la decide e con «Da portare nel piano» — `i-tre-blocchi-e-che-cosa-resta-fuori-da-m4`,
  `i-tipi-di-evento`, `chi-lavora-sugli-eventi`, `gli-slot-e-le-prenotazioni`, `la-vita-di-un-evento`, `il-roster-atc`,
  `dopo-l-evento-e-gli-award`, `gli-eventi-in-presenza`, `le-impostazioni-degli-eventi`, `i-dati-dei-membri-negli-eventi`. Il piano
  1.24 porta già quasi tutta la §18 del design: ogni nota dice che cosa c'è e che cosa resta. E `10-piano-implementazione-m4.md`: le
  regole di tutte le fasi e le fasi E0–E17, con E3, E6, E11, E13, E14 in due PR, E10 in cinque PR del nucleo (E10a–E10e), E15 in due
  (E15a del nucleo), E8 in due (E8a del nucleo, poi **tolta** nel piano 1.25 dopo A12a di M3); «Com'è andata» di E0 è già scritto.
- **Che cosa deve sapere la fase dopo**: le note di E0 registrano le decisioni, **non** la forma nel codice delle estensioni del
  nucleo: ogni fase del nucleo (E1, E10a–E10e, E15a) porta **la sua nota nuova**. ⚠️ **`core-guard` non giudica le PR di
  `SkyMistery`**, quindi il check è verde anche senza la nota: la regola la tiene chi scrive.
- **La cancellazione dei dati di una persona nasce con M4a** (E8b; E8a poi tolta) invece che in E15: proposta di E0, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184) (`10`, E0,
  scostamento 4).
- ⚠️ **Quello che il design dava per esistente e non c'è** (`10`, E0, «Trovato», punti 6–10): il tracker vuole il VID e non dice il
  tipo di connessione; le sessioni condivise non hanno il VID; il vocabolario dei rating non conosce `GND`, `DEL`, `DEP` né il minimo
  di una postazione; la directory delle postazioni cerca solo per rating; la distanza sta nel modulo dei tour. Ognuno ha la sua fase
  del nucleo, prima delle fasi di M4b che lo usano.
- ⚠️ **IT gira con `atcData: none`** (`config/division.json` non ha la chiave): senza sessioni condivise il roster perde il criterio
  dell'esperienza. È una scelta di configurazione di Carmine, da chiedere in apertura di E11b.
- ⚠️ **Prima del primo evento vero (E9)** servono la produzione (piano §15) e il recupero dei giri persi dei job nel nucleo (nota
  `2026-09-28-i-job-quando-passenger-spegne-l-hub`, non ancora nel codice).
- ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della cartella principale.
