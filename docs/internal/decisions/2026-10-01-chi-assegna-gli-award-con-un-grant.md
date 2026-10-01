# Chi assegna gli award, con un grant (E10f)

**Data:** 1 ottobre 2026 — fase E10f di M4, PR del nucleo #213
**Stato:** **decisa** (Carmine, 30 settembre 2026, in chat alla sessione master, e pubblicata su sua istruzione [sulla #205][a1],
risposta 2), alla domanda del [commento della #205][q1], §2: **`Awards.Assign` diventa un permesso globale che un grant può dare**,
detto sul permesso, **solo per `Awards.Assign`** (mai `Permissions.Manage` né lo stato di superadmin); la divisione lo dà all'MD con un
`positionGrant`, come dice il piano; in una PR del nucleo sua, con la sua nota; il caso `Awards.Assign` di
`EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` cambia qui, apposta. La forma nel codice (§3) è una scelta tecnica
dentro quella risposta.
**Dopo la revisione** ([i rilievi del revisore][v213], «approvable»): Carmine ha detto **sì alle tre scelte** che la PR segnalava come
della sessione ([le sue risposte][a213], in chat al master il 1° ottobre, pubblicate su sua istruzione): (1) `Awards.Assign` al
**coordinatore e all'assistente** dell'MD con il `positionGrant` di `division.json` (§3 punto 8); (2) un **rifiuto intero** di
`Awards.Assign` lo toglie anche a chi lo ha per ruolo, DIR, ADIR, WM e AWM (§3 punto 3); (3) il grant porta anche **`Awards.View` su ogni
dipartimento**: chi assegna vede ogni award (§3 punto 3-bis). Il paletto facoltativo del revisore — rifiutare `GrantableAlthoughGlobal`
su ogni permesso che non sia `Awards.Assign` — Carmine l'ha lasciato alla sessione: **preso** (§3 punto 2).

**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende un meccanismo del nucleo che c'è già, il catalogo dei permessi, con un
campo dichiarato sul permesso, come `DeniedToStakeholder` e `OnlyForAssignee`. Nessun handler nuovo, nessun nome di permesso scritto nel
calcolatore. È una PR del nucleo a sé (`CLAUDE.md` §0 regola 6), senza codice del modulo; E10d (#205, unita) manda il riepilogo a chi ha
il permesso, qualunque cosa glielo dia, e non cambia.

[q1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5915953993
[a1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643
[e2b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144
[v213]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926660925
[a213]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926811970

## 1. Che cosa serve, e da dove viene

- **Il piano l'ha sempre detto.** §9.1, riga Award: «assegna chi ha `Awards.Assign`, globale (in IT: MD e HQ con i grant — varia per
  divisione, quindi è configurazione, non codice)». Lo dicono la nota di T4b (`2026-09-16-award-e-preferenze` §2.1: «in IT a MD e HQ con i
  grant»), il design M0 §3.7 («`Awards.Assign` con dipartimenti da `division_settings`») e il design di M4 (§R.3, §5.4: «oggi l'MD»). HQ è
  la direzione della divisione, che lo ha già per ruolo.
- **Il codice non lo permetteva.** L'ha trovato E10d (nota `2026-09-30-la-mail-a-chi-assegna-gli-award` §5): un grant non dava mai un
  permesso globale, quindi l'MD non vedeva la coda degli award, e il riepilogo di E10d arrivava solo a DIR, ADIR, WM, AWM e ai superadmin.
- **Perché nel nucleo, e sul permesso.** La regola «un grant non dà mai un permesso globale» è del nucleo (piano §6.3), e l'eccezione è di
  un permesso del nucleo. Detta sul permesso, è una riga del catalogo e non un nome scritto nel calcolatore; e un modulo che un giorno
  avesse un permesso globale che la divisione dà a chi vuole la direbbe allo stesso modo, con la sua nota.

## 2. Che cosa c'è, letto nel codice (`main` a `db9268f`)

1. **La regola sta in quattro posti**, con la stessa domanda (`PermissionCatalog.IsGlobal`):
   - il calcolatore, all'ingresso dei grant (`EffectivePermissionsCalculator.cs:164-166`): per un grant a un VID, a una posizione o al
     team di un FIR, e per un `Grant` come per un `Deny`;
   - il validatore della schermata dei permessi (`GrantWriteDtoValidator.cs:42`, `errors.grant.globalPermission`);
   - il seme dei `positionGrants` (`PositionGrantSeeder.cs:52`: saltato con un avviso, e non ricordato);
   - i grant che un modulo scrive da una schermata sua (`ModuleGrants.cs:46`: «aggiungi validatore» dei tour, su un dipartimento e,
     se serve, su una riga).
2. **Un permesso globale si chiede solo «in generale».** La coda (`/api/award-signals`) e il registro (`/api/award-assignments`) non
   sono `IOwnedByDepartment`: l'unico handler risponde con `ICurrentUser.HasAny` (`HubAuthorization.cs:157-166`), che guarda il nome e
   nient'altro — né il dipartimento, né lo scope di una riga, né il FIR. La SPA fa lo stesso (`holdsPermissionAnywhere`, le rotte
   `/staff/awards/*` e `staffDestinations.ts`).
3. **I destinatari del riepilogo di E10d** li dice `IPermissionHolders.HoldersOfAsync`, che rifà su ogni candidato il calcolo di un login
   (`UserSyncService.cs:233-270`): cambiato il calcolatore, l'MD entra fra i destinatari da solo.
4. **La schermata dei permessi** offre i nomi che il bootstrap elenca (`/api/me → registries.permissions`, `{ name, isGlobal }`) e scarta
   i globali (`web/src/features/admin/grants/schema.ts:23-25`). Una spec del maintainer (`web/e2e/permissions-fir-team.spec.ts:29-35`)
   costruisce quell'elenco con i soli `name` e `isGlobal`.
5. **Un permesso di un'area porta la `View` della sua area** (`EffectivePermissionsCalculator.cs:188-195`, `PermissionCatalog.ViewOf`):
   `Awards.Assign` porta `Awards.View`, tenuto dove è tenuto lui. È quello che la T4b voleva: «chi assegna deve vedere gli award di tutti».
6. **Il superadmin non è un permesso.** È `hub_users.is_superadmin`, si cambia solo da `/api/admin/superadmins` e solo da un superadmin
   (`GrantEndpoints.cs:65-126`); un grant ha un tipo solo, `GrantKind.Permission`. Nessun grant, di nessun permesso, lo tocca.
7. **Un grant senza dipartimento mette chi lo tiene dentro ogni dipartimento.** Il cookie dice in quali dipartimenti è una persona «per
   quello che vede»: quelli delle sue posizioni, e quelli su cui un grant l'ha raggiunta, **tutti** per un grant che non nomina un
   dipartimento (`HubClaims.BuildIdentity`, `HubClaims.cs:245-267`; note `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento` e
   `2026-09-13-contenuti-centralizzati` §3.1). Il filtro globale e ogni lista generata leggono quei dipartimenti: le righe `Department`
   di ognuno, il gruppo di ognuno nella barra (`/api/me → user.departments`). È giusto per un permesso di un dipartimento dato «su tutti»;
   per un permesso globale, che un dipartimento non ce l'ha, sarebbe un allargamento. **Non l'avevo visto**: me l'ha segnalato la sessione
   di E2b il 1° ottobre, e l'ho misurato (§6): senza la correzione del §3 punto 3-bis il coordinatore dell'MD risultava dentro tutti e nove
   i dipartimenti.

## 3. La forma nel codice (scelta tecnica)

1. **Sul permesso**: `PermissionDescriptor` prende un quinto campo facoltativo, **`GrantableAlthoughGlobal`**, falso se non è detto — il
   lato che chiude: ogni permesso scritto prima resta com'era, anche nei test che costruiscono un catalogo. Nel catalogo del nucleo lo
   dice **solo `Awards.Assign`**: `new(AwardsAssign, IsGlobal: true, GrantableAlthoughGlobal: true)`. Su un permesso di un dipartimento
   non dice niente: un grant quelli li dà comunque.
2. **Il catalogo** risponde con una domanda sola, **`IsClosedToGrants(name)`**: un permesso globale che non lo dice. Un nome che non
   conosce risponde falso, come `IsGlobal`, perché lo rifiuta prima `IsKnown`. Il calcolatore, il validatore e il seme chiedono questa
   al posto di `IsGlobal`: la regola sta in un posto.
   E **non nasce** un catalogo che rende concedibile un permesso che non sia `Awards.Assign`: `Permissions.Manage` per primo — il
   diritto di dare i permessi non arriva mai con un grant (Carmine: «mai `Permissions.Manage`») —, ma anche un altro globale del nucleo o
   di un modulo, o un permesso di un dipartimento. Un cambio del codice che lo facesse ferma l'avvio, invece di aprire il sistema; un
   secondo permesso globale concedibile è una decisione con la sua nota, che cambia anche questa riga. La prima stesura rifiutava solo
   `Permissions.Manage`; il revisore ha proposto di tenere la riga «solo `Awards.Assign`» anche per il prossimo ([v213], punto 2), e
   Carmine l'ha lasciato alla sessione ([a213]): preso.
3. **Il calcolatore** tiene un permesso globale concedibile **solo intero**: da un grant **senza dipartimento, senza scope di una riga e
   non al team di un FIR**. Un permesso globale si chiede solo «in generale» (§2 punto 2), che leggerebbe ognuna di quelle tre cose come
   «ovunque»; un grant così non si scrive né dalla schermata né dal seme (punti 4 e 5), e se c'è una riga scritta a mano il calcolatore
   non la onora — il lato che chiude, com'era per ogni grant globale. Tenuto, è senza dipartimento, come lo tiene un ruolo.
   **Vale anche per un `Deny`**: la divisione può togliere `Awards.Assign` alle posizioni che lo hanno per ruolo (DIR, ADIR, WM, AWM) con
   un grant di rifiuto a una posizione, per esempio al web. Prima un rifiuto di un permesso globale non valeva niente, e per gli altri
   globali resta così. **Confermato da Carmine** ([a213], 2).
3-bis. **Il permesso, non il dipartimento.** Un grant di un permesso globale non porta chi lo tiene dentro nessun dipartimento (§2 punto
   7). L'`EffectivePermission` prende un campo facoltativo, **`FromOutside`** (falso se non è detto: tutto quello che c'era resta com'era),
   che il calcolatore mette a vero per un grant di un permesso globale; la `View` che il permesso porta con sé (`Awards.View`) lo eredita,
   perché nasce con un `with` dalla stessa voce. `BuildIdentity` lascia fuori dai dipartimenti del cookie le voci `FromOutside`, come già
   lascia fuori quelle tenute su un FIR (A11a). Fra due voci uguali di due grant — la `Awards.View` portata da `Awards.Assign` e la stessa
   data da un grant «su tutti i dipartimenti» — il calcolatore tiene quella che porta dentro, così un permesso globale non toglie niente
   a un grant che c'era.
   - **La stessa forma di E2b.** La sessione di E2b (PR #212, decisa da Carmine sulla #209, [risposta 1][e2b]: un grant a una posizione su un
     dipartimento che non è il suo dà il permesso e non il dipartimento) introduce lo stesso campo con lo stesso nome, per il suo caso.
     Le due PR corrono insieme: chi arriva seconda su `main` tiene una dichiarazione sola e somma i due punti in cui il calcolatore lo
     mette a vero (E2b: una posizione su un dipartimento non suo; E10f: un permesso globale, da qualunque grant). Il grant a un VID di
     E10f non è coperto da E2b, che lascia i grant a una persona come sono (6 settembre): per questo E10f non aspetta E2b.
   - ⚠️ **Il significato non è identico** ([v213], punto 1): in E2b il segno scrive anche un `!` prima dello scope nel cookie
     (`HubClaims.FormatPermission` e `ReadPermission`), qui tiene solo il permesso fuori dai claim `dept`. Con tutte e due unite, la
     `Awards.View` che porta un `Awards.Assign` dato con un grant si scrive `Awards.View@!`. Chi arriva seconda tiene una dichiarazione
     sola, somma le condizioni (`fir is null && (grant.GivesThePermissionNotTheDepartment || catalogue.IsGlobal(grant.Value))`) e rifà sul
     codice unito `AwardsAssignByGrantTests`, `GrantableGlobalPermissionTests` e le due classi di test di E2b; il revisore controllerà che
     quella `Awards.View` apra ancora ogni riga di `Award`.
   - La `Awards.View` portata resta, su ogni dipartimento e senza dipartimenti nel cookie: chi assegna vede ogni award, **confermato da
     Carmine** ([a213], 3). Il catalogo degli award è condiviso in lettura, e la lista lo legge
     intero per chi è dentro **almeno un** dipartimento (`MapCrudExtensions.TryNarrowToDepartments`): il coordinatore e l'assistente
     dell'MD, e chiunque abbia una posizione di un dipartimento. ⚠️ **Chi non è dentro nessun dipartimento** (il capo di un FIR, se un
     giorno gli si desse `Awards.Assign` con un grant a un VID) aprirebbe la coda e il registro, ma la lista degli award — quella da cui
     il form dell'assegnazione sceglie — gli risponde 403. Lo chiude la metà «liste» di E2b (con un permesso tenuto «da fuori» senza
     dipartimento, la lista di quel permesso è intera); oggi nessuno ce l'ha così, e la divisione lo dà all'MD.
4. **La schermata** (`GrantWriteDtoValidator`): `errors.grant.globalPermission` solo per un globale chiuso; per `Awards.Assign` con un
   dipartimento, **`errors.grant.globalDepartment`** sul campo del dipartimento, perché sembrerebbe un limite che niente applica — come il
   nucleo rifiuta già le cose che non fanno niente (un grant scaduto prima di nascere, il team di un FIR fuori da un'area con il FIR). Il
   team di un FIR lo rifiuta già la regola di A11a (`errors.grant.firTeamArea`: nessuna riga dell'area Awards dice il suo FIR), e la
   schermata non scrive mai uno scope.
   Il testo di `errors.grant.globalPermission` diceva «un permesso che non è legato a un dipartimento non si assegna a mano»: dopo E10f
   non è più vero per `Awards.Assign`, e diventa «questo permesso viene solo dalle posizioni dello staff, e non si assegna a mano».
5. **Il seme** (`PositionGrantSeeder`): salta con un avviso un globale chiuso, come prima, e un globale concedibile con uno `scope`
   (nuovo). Non li ricorda, come fa con quello che la schermata rifiuta.
6. **`ModuleGrants` non cambia**: rifiuta ogni permesso globale, anche `Awards.Assign`. Un grant di un modulo è sempre su un dipartimento
   e può essere su una riga sola, due cose che un permesso globale non ha: un `Awards.Assign` dato su un tour, chiesto «in generale»,
   varrebbe ovunque. Nessun modulo ha ragione di darlo da una schermata sua. Lo dice un commento.
7. **Il browser**: il bootstrap porta anche il campo del permesso (`registries.permissions[].grantableAlthoughGlobal`), e la schermata
   offre «di un dipartimento, oppure globale e concedibile» (`!isGlobal || grantableAlthoughGlobal`). Il campo del permesso, e non una
   risposta calcolata («concedibile sì o no»), perché la spec del maintainer costruisce l'elenco senza il campo nuovo: con questo filtro
   un elenco senza il campo si legge come prima, con una risposta calcolata il form non le offrirebbe più niente.
8. **La divisione** (`config/division.json`): **`{ "department": "MD", "levels": ["Coordinator", "Assistant"], "permission":
   "Awards.Assign" }`**, senza `scope`, in testa ai `positionGrants`.
   - **I livelli** — coordinatore e assistente, quelli che il piano 0.77 dà a chi gestisce un modulo del suo dipartimento — erano una
     lettura della sessione: il piano dice «l'MD» e non i livelli. **Confermati da Carmine** ([a213], 1).
   - Su un'installazione già avviata arriva al primo avvio dopo il rilascio (un seme aggiunto al file si applica una volta, T5, e il
     file cambiato fa rifare i semi all'avvio); da lì si cambia dalla schermata dei permessi.
   - `config/division.example.json` porta lo stesso grant, con il suo commento; `docs/FORKING.md` spiega l'eccezione.

## 4. Il test di Carmine che cambia

`tests/IvaoHub.UnitTests/EffectivePermissionsTests.cs`, la teoria `AGrantCanNeverConferAGlobalPermission`: **tolta la riga
`[InlineData(CorePermissions.AwardsAssign)]`**, e al suo posto un commento che rimanda a questa nota e al test che prova che cosa dà
ora. Le altre quattro righe (`Permissions.Manage`, `Modules.Manage`, `Audit.View`, `Admin.Access`) restano e passano. È la sola modifica
a un test del maintainer, e l'ha decisa lui ([risposta 2][a1]); nessun altro suo test cambia: `ModuleAndAdminEndToEndTests` rifiuta
ancora `Permissions.Manage` dalla schermata e dal seme, `AGrantToAPositionCanNeverConferAGlobalPermission` lo rifiuta ancora a una
posizione.

## 5. Che cosa resta chiuso

- **Gli altri permessi globali** (`Permissions.Manage`, `Modules.Manage`, `Audit.View`, `Admin.Access`, `Calendar.ManageKinds`) e ogni
  globale di un modulo che non lo dice: né con un grant né con un rifiuto, dal calcolatore, dalla schermata e dal seme, come prima.
- **Lo stato di superadmin**: non è un permesso (§2 punto 6).
- **`Awards.Assign` a metà**: su un dipartimento, su una riga, al team di un FIR, da `ModuleGrants`.

## 6. I test

- **Unità**, `GrantableGlobalPermissionTests` (nuovo):
  - il catalogo del nucleo ha un solo permesso globale concedibile, `Awards.Assign`, e `IsClosedToGrants` risponde per ognuno;
  - un catalogo che rende concedibile un permesso che non sia `Awards.Assign` non nasce: `Permissions.Manage`, gli altri globali del
    nucleo, un permesso di un dipartimento, un globale di un modulo;
  - un grant alla posizione del coordinatore dell'MD dà `Awards.Assign` senza dipartimento, con `Awards.View`, e **nessun altro permesso
    globale**, anche accanto a un grant di `Permissions.Manage`; nessuna fonte «superadmin»; `HasAny` risponde sì;
  - lo stesso grant con un dipartimento, con uno scope o al team di un FIR non dà niente;
  - **non porta nessuno dentro un dipartimento**: il cookie di un login (`BuildIdentity`) dice MD per il coordinatore dell'MD ed ED per
    un coordinatore dell'ED a cui lo si dà per nome; accanto a un grant di `Awards.View` «su tutti», resta quello a portare dentro;
  - un rifiuto intero lo toglie al web master, e uno su un dipartimento non vale.
- **Integrazione**, `AwardsAssignByGrantTests` (nuovo), VID `761080–761089`, nessuno staff dell'ED o dell'MD con un indirizzo
  (`ContactsAndNotificationsTests` conta i destinatari dell'MD):
  - il seme di `config/division.json` dà `Awards.Assign` al coordinatore e all'assistente dell'MD: `/api/me` lo porta senza dipartimento
    e senza `Permissions.Manage`, nessuno è superadmin, **`user.departments` è `["MD"]`**, la coda risponde 200 e la schermata dei
    permessi 403; l'advisor dell'MD non lo ha (403); il coordinatore è fra i destinatari del riepilogo (`IPermissionHolders`);
  - la schermata lo accetta a un VID dello staff (e il grant morde alla richiesta dopo il nuovo login, senza portarlo in altri
    dipartimenti) e a una posizione; rifiuta il dipartimento, il team di un FIR, `Permissions.Manage` e `Admin.Access`;
  - il seme lo applica senza `scope`, salta lo `scope` e `Permissions.Manage`; `ModuleGrants` lo rifiuta.
- **Browser**, `web/src/features/admin/grants/grantable.test.ts` (nuovo): il form offre i permessi di un dipartimento e `Awards.Assign`,
  non gli altri globali; un elenco senza il campo nuovo si legge come prima.
- **Non si prova la mail vera all'MD**: servirebbe uno staff dell'MD con un indirizzo, che i test dei contatti non vogliono
  (`CONTRIBUTING.md`, «Tests»). Che il riepilogo vada a chi ha il permesso lo prova `AwardQueueMailTests` (E10d); qui si prova che l'MD
  lo ha, con la stessa domanda che usa il job.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| `Awards.Assign` dipartimentale | la coda e il registro sono di tutta la divisione; la riga Award del piano lo vuole globale |
| Il nome `Awards.Assign` scritto nel calcolatore, nel validatore e nel seme | Carmine l'ha voluto detto sul permesso, come `DeniedToStakeholder`; e sarebbero tre copie della stessa eccezione |
| Il campo nel bootstrap come risposta calcolata (vero anche per i permessi di un dipartimento) | la spec del maintainer costruisce l'elenco senza il campo, e il form non le offrirebbe più niente |
| Tenere il dipartimento di un grant globale e leggerlo come «ovunque» | un campo che sembra un limite e non lo è; il nucleo rifiuta così le cose che non fanno niente |
| `ModuleGrants` con l'eccezione | un grant su una riga di un permesso chiesto solo «in generale» varrebbe ovunque |
| Lasciare che il grant porti dentro ogni dipartimento, come un grant «su tutti» | un permesso globale non è su tutti i dipartimenti, è su nessuno: chi assegna gli award vedrebbe le righe `Department` di tutti (§2 punto 7) |
| Il catalogo dentro `BuildIdentity`, per sapere quali nomi sono globali | `BuildIdentity` prenderebbe una dipendenza e ogni chiamante cambierebbe; il calcolatore sa già tutto, e il segno sulla voce è la forma di A11a (il FIR) e di E2b |
| Non far portare la `View` a un globale dato con un grant | chi ha il grant per nome e nessuna `Awards.View` da un ruolo non potrebbe scegliere l'award; con il segno la `View` resta senza allargare niente |
| Una tabella o una chiave di `division.json` che dice chi assegna | c'è già: i `positionGrants`, con la schermata dei permessi dopo il primo avvio |

## 8. Che cosa si tocca

- **Nucleo**: `Auth/Permissions/CorePermissions.cs` (il campo e la riga di `Awards.Assign`), `Auth/Permissions/PermissionCatalog.cs`
  (`IsClosedToGrants`, il rifiuto di `Permissions.Manage`), `Auth/Permissions/EffectivePermissionsCalculator.cs` (il filtro, il segno
  `FromOutside` su `EffectivePermission`, la deduplicazione), `Auth/HubClaims.cs` (i claim `dept`),
  `Auth/GrantWriteDtoValidator.cs`, `Auth/PositionGrantSeeder.cs`, `Auth/ModuleGrants.cs` (solo un commento),
  `src/IvaoHub.Web/Endpoints/MeEndpoints.cs` (il bootstrap), `web/src/features/admin/grants/schema.ts`,
  `web/src/shared/api/schema.d.ts` (generato), `locales/{en,it}/errors.json`.
- **Configurazione e documenti pubblici**: `config/division.json`, `config/division.example.json`, `docs/FORKING.md`.
- **Test**: i nuovi del §6; la riga di `EffectivePermissionsTests` (§4); una frase del commento di `AwardQueueMailTests` (E10d), che
  diceva «un grant non dà mai un permesso globale».
- **Nessuna migrazione**: `hub_user_grants` ha già tutte le colonne.

## Da portare nel piano

- **§6.3**, «Salvagente»: «Un grant non può mai conferire `Permissions.Manage` né lo stato di superadmin» → nessun permesso globale, salvo
  `Awards.Assign` (`GrantableAlthoughGlobal`; il catalogo non nasce se lo dice un altro permesso), e solo intero (senza dipartimento,
  senza scope, non al team di un FIR), per un grant come per un rifiuto: **un rifiuto intero lo toglie anche a chi lo ha per ruolo**
  (confermato da Carmine sulla #213).
- **§6.3**, «Grant manuali»: un grant di un permesso globale **dà il permesso e non il dipartimento** (`EffectivePermission.FromOutside`,
  nessun claim `dept`), come il team di un FIR (A11a) e un grant a una posizione su un dipartimento non suo (E2b); la `View` che porta
  vale su ogni dipartimento (confermato da Carmine sulla #213).
- **§9.1, riga Award**: in IT `Awards.Assign` all'MD, **coordinatore e assistente** (confermati da Carmine sulla #213), con un
  `positionGrant` di `division.json`; la direzione e il web lo hanno per ruolo; il riepilogo di E10d arriva a loro, e chi assegna vede
  ogni award.
- **§4.1, `positionGrants`**: un grant di un permesso globale concedibile non ha `scope`.
- Nel design M0 §3.7 (le righe «Scoping» e «Grant»), se il master lo ritiene: «mai un permesso globale» → salvo i concedibili, con la
  chiave `errors.grant.globalDepartment`.
