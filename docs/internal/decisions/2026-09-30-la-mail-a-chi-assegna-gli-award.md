# La mail a chi assegna gli award (E10d)

**Data:** 30 settembre 2026 — fase E10d di M4, PR del nucleo
**Stato:** **Proposta.** La forma nel codice (§3) è una scelta tecnica. **Il ritmo della mail** (§4, domanda 1) è una domanda a
Carmine, e il codice che ne dipende aspetta la sua risposta. **Chi ha `Awards.Assign` in IT** (§5, domanda 2) è una cosa trovata
leggendo il codice: non cambia il codice di questa PR, ma decide a chi arriva la mail.
**Regola applicata:** `CLAUDE.md` §2 (le notifiche sono del servizio del nucleo, i moduli pubblicano intenti; il segnale di award è
una proiezione `IProjectable`) e §5, caso **(b)**. Si estendono due meccanismi del nucleo che ci sono già: la coda degli award
(`cms_award_signals`, che i moduli scrivono solo attraverso l'interceptor) e il servizio delle notifiche con le preferenze per tipo.
**Nessun modulo scrive niente**: i tour non si toccano, ed E14b non dovrà chiamare nessuno. È una PR del nucleo, prima di E14b che la
usa (`CLAUDE.md` §0 regola 6). Da dove viene: la nota `2026-09-29-dopo-l-evento-e-gli-award` §2 punto 5 (decisa da Carmine sulla
#180), il design `09-design-m4.md` §5.4, §8.3 e §13 n.5, il piano §9.1 (riga Award) e `10-piano-implementazione-m4.md`, E10d.

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **Carmine** (design §R.3, c1): «a chi assegna gli award (oggi l'MD) che qualcuno deve riceverne uno». La nota di E0 (§2 punto 5):
  «a chi assegna gli award, quando entra un segnale nuovo in coda — è del nucleo (estensione n.5) e vale anche per i tour. Tutte
  disattivabili dal profilo».
- **Oggi il nucleo non avvisa nessuno** (`10`, E0, «Trovato» n.11): la coda si vede solo aprendo `/staff/awards/queue`.
- **Perché nel nucleo.** La coda è del nucleo, e i segnali li scrivono più moduli: i tour dal 16 settembre (T4b, T15), gli eventi con
  E14b, domani altri. Una mail scritta da ogni modulo sarebbe scritta due volte, e quella dei tour non ci sarebbe: il modulo dei tour
  non si tocca (`CLAUDE.md` §0 regola 2).

## 2. Che cosa c'è, letto nel codice (`main` a `c107c98`)

1. **Il segnale nasce nella transazione della riga del modulo.** Un modulo proietta un `AwardSignalProjection`, e
   `ProjectionWriter.ApplyAwardSignals` (`src/IvaoHub.Core/Content/ProjectionWriter.cs:305-340`) scrive una riga per sorgente e VID. Lo
   fa sul **contesto del modulo**, che mappa le tabelle delle proiezioni fuori dalle sue migrazioni
   (`src/IvaoHub.Core/Data/ModuleDbContext.cs:78-113`). Una riga già gestita non si riscrive; una in attesa che la sorgente non segnala
   più si toglie. Per i tour la riga la proietta l'iscrizione (`Enrolment.AwardSignal`, `Pireps/Pirep.cs:394-408`), nel salvataggio
   che accetta il PIREP che completa il tour (`TourCompletion.cs:79-94`).
2. **Il contesto di un modulo non mappa `hub_notifications`.** Il servizio delle notifiche scrive sul contesto del nucleo, e salva da
   sé (`src/IvaoHub.Core/Notifications/NotificationService.cs:41-117`).
3. **L'unica mail che segue una proiezione la manda il modulo**, dopo il suo salvataggio: l'apertura di un filo dei contatti
   (`ContactThreads.NotifyOpenedAsync`, `src/IvaoHub.Core/Content/ContactThreads.cs:137-162`, T14b). Per gli award non si può fare
   così, perché i tour non si toccano.
4. **Chi ha un permesso** lo dice `IPermissionHolders.HoldersOfAsync` (`Auth/Permissions/PermissionHolders.cs`,
   `Auth/UserSyncService.cs:233-270`): è la stessa risposta di un login (posizioni, grant a un VID o a una posizione, superadmin). La
   usa già il riepilogo dei validatori dei tour (`ReviewDigestJob.cs:70`).
5. **Il servizio delle notifiche lascia fuori** chi ha spento il tipo, chi non ha un indirizzo e i doppioni (`NotificationService.cs:29-32`);
   nessuna riga di preferenza vuol dire «sì» (`NotificationPreference.cs:7-9`). Il profilo elenca ogni tipo che il server dichiara
   (`web/src/features/me/NotificationPreferences.tsx`), con le parole in `notifications.types.{tipo}`. `NotificationTemplateTests`
   controlla l'oggetto e il testo di ogni tipo in ogni lingua.
6. **Un job del nucleo che avvisa una volta sola c'è già**: `DocumentReviewJob`, dove la riga ricorda quando ha avvisato
   (`ReviewNotifiedAt`) e il segno si scrive anche se nessuno riceve la mail (`Content/DocumentReviewJob.cs:135-138`). La regola della
   nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` (§8) è la stessa: ogni job decide dai suoi dati, così un giro perso o doppio
   non fa danni. Il recupero dei giri persi non è ancora nel codice.
7. **Nei test d'integrazione ogni host fa girare il suo Quartz** (`ContactsAndNotificationsTests.cs:29-33`), e un test che non vuole
   un giro nascosto mette in pausa il suo job (`TourTests.cs:349-354`).
8. **Chi ha `Awards.Assign` oggi**: il §5.

## 3. La forma nel codice (scelta tecnica, uguale per ogni risposta del §4)

1. **Chi si accorge del segnale è un job del nucleo che legge la coda**: non il salvataggio del modulo, e non il modulo. Non al
   salvataggio, per tre ragioni:
   - i destinatari (chi ha `Awards.Assign`, i loro indirizzi, le preferenze) sono una lettura di mezzo nucleo, e finirebbe dentro la
     transazione della riga del modulo (l'accettazione di un PIREP). Il writer delle proiezioni è fatto apposta per tenere corta quella
     transazione (`ProjectionWriter.cs:52-56`);
   - le righe di `hub_notifications` andrebbero scritte dal contesto del modulo, che dovrebbe mapparle (un'altra estensione), oppure
     fuori dalla transazione, e allora un rollback lascerebbe una mail per un segnale che non c'è;
   - un job decide dai suoi dati, come `DocumentReviewJob`, e un giro in ritardo o ripetuto non manda due volte (punto 3).
2. **Il segno «detto»**: una colonna nuova **`cms_award_signals.notified_at`** (`datetime(6)`, vuota finché il segnale non è stato
   detto), con un indice `(status, notified_at)`. Migrazione additiva del nucleo, `AddAwardSignalNotifiedAt`. La migrazione segna come
   già dette le righe che sono già in coda (`notified_at = created_at`, come `AddSearchIndexUpdatedAt` riempie la sua colonna): chi
   assegna le vede già in `/staff/awards/queue`, e il primo giro non deve presentarle come nuove.
3. **Un giro** prende i segnali **in attesa** e non ancora detti. Chiede chi ha `Awards.Assign` (`IPermissionHolders`) e mette in coda
   un intento del tipo nuovo. Poi segna i segnali **nello stesso salvataggio delle righe della mail**: il servizio delle notifiche salva
   sul contesto del job, che porta anche i segni, quindi si scrivono tutti e due o nessuno dei due. Ne segue che:
   - un segnale gestito o scartato prima del giro non si racconta;
   - un segnale già detto non si racconta più, neanche se torna «in attesa» dopo uno scarto;
   - il segno si scrive anche quando nessuno riceve la mail (l'hanno spenta tutti, o nessuno ha un indirizzo), come in
     `DocumentReviewJob`: un altro giro non raggiungerebbe nessuno in più.
   ⚠️ Due processi che girano nello stesso secondo possono mandare la mail due volte. È il limite di ogni job di oggi (nota
   `i-job-quando-passenger…`, §2 punto 5), finché il nucleo non avrà un esecutore solo per job.
4. **Il tipo**: `award.toAssign`, del nucleo, in `NotificationTypes` dopo gli altri sette. Compare da solo nel profilo, e si spegne
   da lì come gli altri. Le parole stanno in `locales/{lang}/mail.json` (`mail.award.toAssign.subject` e `.body`) e in
   `locales/{lang}/common.json` (`notifications.types.award.toAssign.title` e `.description`). Il link porta a
   `https://{dominio}/staff/awards/queue`.
5. **Vale per ogni modulo**: il job non guarda `source_module`. I tour, gli eventi (E14b) e il modulo di prova passano dalla stessa
   coda, e nessun modulo cambia.
6. **Nel contenitore**: `AddHubAwards()`, nuovo, in `src/IvaoHub.Core/Awards/`, con il job e il suo orario; una riga in `Program.cs`
   accanto a `AddHubNotifications()`.
7. **I test**: al §6.

## 4. Domanda 1: il ritmo della mail

Il codice del §3 è lo stesso. Cambiano quando gira il job e che cosa porta una mail.

- **(A) Una mail per segnale, subito.** Il job gira ogni minuto, come quello che manda le mail. Ogni mail porta un segnale: il VID (e
  il nome, se l'hub lo conosce), il motivo, l'award proposto, il link. È la lettura letterale di c1 e della nota di E0 («quando entra un
  segnale nuovo in coda»).
  ⚠️ **Le raffiche.** Con E14b, accettare un PIREP di supporto segnala una o più regole di award. Un RFE con cento piloti e venti
  controllori fa da cento a trecento segnali nei giorni della validazione, cioè altrettante mail per ognuno di quelli che hanno
  `Awards.Assign`: oggi, in IT, il direttore e il suo assistente, il web master e il suo assistente e il superadmin (§5). E chi valida
  riceverebbe una mail per ogni PIREP che ha appena accettato: in IT, dice c1, l'MD fa tutte e due le cose. Chi assegna finirebbe per
  spegnerla, e a quel punto non saprebbe più nemmeno dei tour completati.
- **(B) Un riepilogo al giorno.** Alle 07:00 nell'ora della divisione, **solo se** sono entrati segnali nuovi dall'ultimo riepilogo.
  La mail dice quanti segnali sono nuovi e quanti aspettano in tutto; poi una riga per ogni motivo e award proposto, con il numero (per
  esempio «Ha completato il tour "Giro d'Italia" — Award del Giro: 3»), **senza VID**; poi il link alla coda.
  - È il ritmo degli altri due riepiloghi dello staff: i validatori dei tour (`flightops.reviewDigest`) e quelli degli eventi
    (`reportsToValidate`, E14b). Anche il giorno in cui si valida un RFE, al più una mail al giorno.
  - Senza VID, la mail non porta i dati di una persona: la cancellazione non deve cercarli (`PersonalDataErasure.cs:271-284`), e la
    mail resta corta anche con trecento segnali.
  - Il prezzo: un segnale aspetta la mail fino al mattino dopo. Un award non è urgente: il membro lo trova sul suo profilo IVAO quando
    qualcuno lo assegna.
  - ⚠️ Finché il nucleo non recupera i giri persi (nota `i-job-quando-passenger…`, strada B), un 07:00 che cade mentre l'hub dorme
    salta. I segnali non si perdono, perché il segno dice che cosa è stato detto, e partono al primo giro che c'è.
- Un ritmo diverso (ogni ora, due volte al giorno) è il codice di (B) con un altro orario.

**Raccomandazione: (B).** Con (A) la mail la spegnerebbe proprio chi deve riceverla.

**Che cosa cambia per E14b.** `10` dice che E14b è fatta quando «un PIREP accettato mette un segnale nella coda degli award, e chi
assegna riceve la mail (E10d)». Con (B) la mail parte il mattino dopo: sul banco si vede il segnale in coda, e la mail la prova il test
d'integrazione di questa fase.

## 5. Domanda 2: chi ha `Awards.Assign` in IT (trovato; non cambia il codice di questa PR)

- **Il piano e il design dicono l'MD.** Il piano §9.1, riga Award: «assegna chi ha `Awards.Assign`, globale (in IT: MD e HQ con i
  grant — varia per divisione, quindi è configurazione, non codice)». Lo dicono anche la nota `2026-09-16-award-e-preferenze` §2.1
  («in IT a MD e HQ con i grant») e il design di M4 (§R.3, §5.4: «oggi l'MD»).
- **Il codice non lo permette.** `Awards.Assign` è globale (`CorePermissions.cs:145`), e **un grant non dà mai un permesso globale**:
  «A grant may never confer a global permission… the perimeter of the staff is always decided by IVAO»
  (`EffectivePermissionsCalculator.cs:164-166`). Vale sia per un grant a un VID sia per un grant a una posizione, e quindi anche per i
  `positionGrants` di `division.json`. Il form dei grant lo rifiuta (`GrantWriteDtoValidator.cs:42`, `errors.grant.globalPermission`),
  il seme dei grant lo salta (`PositionGrantSeeder.cs:52`), e anche `ModuleGrants.cs:46`. Il test di Carmine
  `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` lo fissa proprio con `Awards.Assign` (riga 194). Il piano §6.3 dice
  meno del codice: «un grant non può mai conferire `Permissions.Manage` né lo stato di superadmin».
- **Chi lo ha davvero, oggi**: solo il direttore e il suo assistente, il web master e il suo assistente (le posizioni che raggiungono
  ogni dipartimento hanno tutti i permessi globali: `RolePermissionMatrix.ReachesEveryDepartment`, `EffectivePermissionsCalculator.cs:229-243`),
  e i superadmin. **L'MD non vede la coda degli award**, e la mail di questa PR arriverebbe a quelle persone, non all'MD.
- **Il codice di questa PR non cambia.** La mail va a chi ha il permesso, qualunque cosa glielo dia, e arriverà all'MD il giorno in cui
  lo avrà. Ma **a chi arriva** dipende da una regola dei permessi del nucleo che ha un test di Carmine: la decide lui, e la scrive una PR
  sua, o una fase del nucleo con il suo via, non questa. Le strade che vedo:
  - **(a)** un permesso globale che un grant **può** dare, detto sul permesso (`PermissionDescriptor`, come `DeniedToStakeholder`),
    solo per `Awards.Assign`. La divisione lo dà all'MD con un `positionGrant`, come dice il piano. Cambiano il filtro del
    calcolatore, il validatore dei grant, il seme e il caso `Awards.Assign` del test di Carmine;
  - **(b)** lasciare com'è: assegnano il direttore, il web e il superadmin, e si correggono il piano, il design di M4 e la nota di T4b.
- **Raccomandazione: (a)**, perché è quello che il piano ha sempre detto («configurazione, non codice»); in una PR a sé, fuori da E10d.

## 6. I test

- **Integrazione, `AwardQueueMailTests`** (nuovo), VID `761050–761059`.
  - Chi assegna è un superadmin senza posizioni, con un indirizzo: un grant non può dare il permesso (§5), e senza posizioni non entra
    fra i destinatari dei contatti che `ContactsAndNotificationsTests` conta esatti.
  - Un **segnale dei tour** nasce dalla proiezione del modulo dei tour: un'iscrizione con il suo `AwardSignal`, salvata attraverso il
    contesto dei tour e l'interceptor, come in `TourCompletion`. Un **segnale di prova** nasce da `SampleEvent.AwardeeVid`, come in
    `AwardsTests`.
  - Tutti e due arrivano a chi assegna; non a chi assegna ma ha spento il tipo; non a chi ha un indirizzo ma non il permesso; **una
    volta sola**, perché un secondo giro non scrive niente.
  - Un segnale scartato prima del giro non si racconta, e ogni segnale detto ha il suo `notified_at`.
  - Il test mette in pausa il job del suo host, perché nessun giro nascosto lo anticipi. Alla fine toglie le sue righe e toglie il
    superadmin alle sue persone.
- **Unità**: le righe della mail, se la risposta al §4 è (B): un motivo e un award con il numero, l'ordine, un segnale senza award.
- **Nessun test del maintainer cambia.** I test che contano le notifiche filtrano per tipo o per le loro persone; `NotificationTemplateTests`
  e `ReviewTests` leggono l'elenco dei tipi e trovano da soli quello nuovo.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La mail al salvataggio, nell'interceptor o nel writer delle proiezioni | §3 punto 1: i destinatari letti dentro la transazione del modulo, `hub_notifications` da mappare in ogni modulo o scritta fuori dalla transazione |
| Il modulo che chiama il nucleo dopo il suo salvataggio, come per i fili dei contatti | i tour non si toccano, e ogni modulo che segnala dovrebbe ricordarsene |
| Il job che ricorda fin dove è arrivato (l'id dell'ultimo segnale, o l'inizio dell'ultimo giro in `hub_jobs_log`, come `TourReleaseJob`) | un segnale scritto in una transazione che si chiude dopo la lettura del giro cade fra due giri; un giro che muore dopo le mail e prima del registro le rimanda. Il segno sulla riga evita tutte e due le cose |
| Una tabella dei segnali detti | la riga c'è già: una colonna costa meno |
| Solo a chi ha `Awards.Assign` per un grant o una posizione, senza il superadmin | il superadmin ha ogni permesso per definizione (design M0 §3.3); la spegne dal profilo come tutti |

## 8. Che cosa si tocca

- **Nucleo**: `Content/AwardSignal.cs` (`NotifiedAt`), `Data/Configurations/CmsSchemaConfiguration.cs` (l'indice), la migrazione
  `AddAwardSignalNotifiedAt` e lo snapshot del contesto del nucleo, `Awards/AwardQueueMailJob.cs` e `Awards/AwardServiceCollectionExtensions.cs`
  (nuovi), `Notifications/NotificationTypes.cs`, `src/IvaoHub.Web/Program.cs`, `locales/{en,it}/mail.json` e `common.json`; i test del
  §6.
- ⚠️ **Gli snapshot dei contesti dei moduli** (tour, training, modulo di prova) non vedono la colonna finché non fanno il loro prossimo
  `migrations add`: è lo scarto innocuo già visto in T4b e T14 (`HANDOFF.md`, T14). Nessun codice di un modulo cambia.
- ⚠️ **Migra il contesto del nucleo.** Se un'altra fase del nucleo di M4 lo migra insieme a questa, chi viene unita per seconda rifà la
  sua migrazione sopra `main` (`10`, «Regole di tutte le fasi»).
- **Non si toccano**: il modulo dei tour, la coda degli award (`AwardEndpoints`), la schermata del profilo (l'interruttore arriva da
  solo).

## Da portare nel piano

- **§9.1, riga Award**: la mail a chi assegna è un job del nucleo sulla coda, con il tipo `award.toAssign`, il segno `notified_at` e il
  ritmo deciso al §4.
- **§9.1 e §6.3**: chi ha `Awards.Assign` in IT, secondo la risposta al §5; se è (b), anche il design di M4 (§R.3, §5.4, §8.3) e la nota
  di T4b dicono «oggi l'MD».
- **`10-piano-implementazione-m4.md`, E14b, «Fatta quando»**: la mail secondo il ritmo del §4.
