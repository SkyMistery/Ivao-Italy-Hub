# I tipi degli eventi nel calendario, e l'ED sul banco (E1)

**Data:** 30 settembre 2026 — fase E1 di M4, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice a decisioni già prese da Carmine: l'estensione n.1 del design
(`09-design-m4.md` §13), la nota `2026-09-29-i-tipi-di-evento` §2.1 (§17.2 n.1 del design, decisa sulla #180) e il personaggio
dell'ED che `10-piano-implementazione-m4.md` chiede in E1. Nessuna domanda nuova, ma **uno scostamento di grafia** dal design e dal
piano (§2.2): la chiave dell'Online Day è `online-day`, perché `onlineDay` è una chiave che il vocabolario rifiuta. **La grafia è
confermata da Carmine** (30 settembre 2026, in chat al master, che l'ha pubblicata sulla PR #200 su sua istruzione: [risposta][ok200]):
`online-day`, e la chiave della traduzione resta `seed.calendarKinds.onlineDay`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono due meccanismi del nucleo che esistono già — il seme dei tipi del
calendario (`seed/calendar-kinds/kinds.json`, `ContentSeeder.SeedCalendarKindsAsync`) e i personaggi del banco e2e (`E2ESignIn`,
`web/scripts/e2e-server.mjs`) — e il modulo non ne scrive una copia sua. È una PR del nucleo, prima di E2 ed E3a che la usano
(`CLAUDE.md` §0 regola 6).

[ok200]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912372176

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **I tipi.** Il tipo di un evento è una chiave del vocabolario del calendario (nota `i-tipi-di-evento` §2.1): il form dell'evento
  (E3a) sceglie `kind` fra i tipi del bootstrap (`/api/me` → `calendarKinds`), la voce di calendario di un evento (E3b) porta il suo
  tipo, e `kindPresets` (E2) accetta solo chiavi che esistono. Il vocabolario è della divisione e del nucleo (`cms_calendar_kinds`,
  scritto da chi ha `Calendar.ManageKinds`), non del modulo: una release porta una parola nuova con il seme, come ha fatto A2 di M3
  con `exam`. Senza, lo staff dell'ED non potrebbe scegliere RFE, RFO, MSE né Online Day, e un'installazione nuova partirebbe senza.
- **Il personaggio dell'ED.** Il banco ha il web master (`IT-WM`), il pilota, l'assistente dei tour (`IT-FOAC`) e il trainer
  (`IT-T01`). Il web master **raggiunge ogni dipartimento** (`RolePermissionMatrix.ReachesEveryDepartment`) e ha ogni permesso del
  catalogo, di ogni modulo: con lui una spec degli eventi passerebbe anche con i `positionGrants` dell'ED sbagliati. Serve chi ha
  **solo** quello che la divisione dà all'ED — un coordinatore dell'ED, i cui permessi degli eventi arrivano dal seme dei
  `positionGrants` di E2 (in E2 «`?as=events` vede la sezione, salva un'impostazione e la rilegge»). L'assistente dei tour resta per
  le rotte (E4), il trainer per il training.

## 2. Che cosa si fa

### 2.1 I quattro tipi nel seme

- In `seed/calendar-kinds/kinds.json`, **subito dopo `event`** (`sort` 11, 12, 13, 14; `training` è a 20) e **nel suo colore,
  `blue`**: sono eventi, e il colore di un tipo esiste proprio per raggruppare le parole che stanno insieme (`CalendarKind.Colour`:
  «a colour somebody chose can group two kinds that belong together»); la tavolozza ha nove colori, i sei tipi di oggi ne usano sei e
  ne restano tre, non quattro. Colore e posto si cambiano dal back office (`/staff/admin/calendar-kinds`), come per gli altri sei.
- **Le etichette**: le chiavi `seed.calendarKinds.rfe`, `.rfo`, `.mse` e `.onlineDay` in `locales/*/seed.json`, «RFE», «RFO»,
  «MSE» e «Online Day» in tutte e due le lingue: sono i nomi con cui l'ED li chiama (design §R.3), e in inglese non nominano la
  divisione.
- **Nessun codice cambia**: il seme si ricorda chiave per chiave e salta una chiave che il back office ha già scritto
  (`SeedCalendarKindsAsync`, dal 25 settembre, A2). Quindi i quattro arrivano anche a un database già avviato, al primo avvio dopo il
  rilascio — il marcatore d'inizializzazione conta anche `seed/` (nota `2026-09-28-il-marcatore-d-inizializzazione`) —, e un `rfe`
  scritto a mano prima del rilascio resta com'è.

### 2.2 ⚠️ `online-day`, non `onlineDay`

- Il design (§1.2, §1.12, §8.1, §13 n.1), la nota `i-tipi-di-evento` e il piano (§9.5, e la voce 1.24 del changelog) scrivono la
  chiave `onlineDay`. **Il vocabolario non la accetta**: la chiave di un tipo ha la forma di uno slug, minuscole e trattini
  (`CalendarKindWriteDtoValidator`, `^[a-z0-9]+(?:-[a-z0-9]+)*$`), perché finisce nell'indirizzo di un calendario filtrato e nelle
  righe già scritte. Il seme non passa dal validatore, quindi `onlineDay` entrerebbe lo stesso; ma il validatore guarda la chiave **a
  ogni salvataggio**, anche di una riga che c'è già (`MapCrudExtensions.UpdateAsync` valida il corpo intero): il web master che
  cambia il colore dell'Online Day si vedrebbe rifiutare la chiave (`errors.slug.invalid`), e per salvare dovrebbe rinominarla —
  lasciando gli eventi scritti con `onlineDay` su una parola che non c'è più.
- **La chiave è `online-day`**: è la grafia che il vocabolario accetta, ed è quella che il back office stesso propone scrivendo
  «Online Day» (il form ricava la chiave dall'etichetta, `slugFrom` → `slugify`). L'etichetta non cambia, e la chiave della
  traduzione resta `seed.calendarKinds.onlineDay` (le chiavi i18n sono in camelCase, e non finiscono in un indirizzo).
- **Le fasi dopo scrivono `online-day`**: `kindPresets` (E2), il form dell'evento (E3a) e, quando la divisione vorrà l'Online Day
  fra i tipi che avvisano un trainer, i `conflictKinds` del training (un'impostazione, nessun codice).
- È una grafia, non una decisione nuova: la decisione (il tipo è una chiave del vocabolario) resta quella di Carmine, e la chiave
  segue la regola che il vocabolario ha da quando esiste (G13 di M1, nota `2026-09-08-tipi-di-evento-di-divisione`). Il revisore
  l'ha portata a Carmine, che l'ha **confermata** il 30 settembre 2026 ([risposta sulla #200][ok200]): `online-day`, la chiave della
  traduzione `seed.calendarKinds.onlineDay`, le note già unite e il design restano come sono scritti.

### 2.3 Un test del file del seme

Oggi nessun test legge `seed/calendar-kinds/kinds.json`: `ContentSeedTests` controlla i template e le pagine (le chiavi di
traduzione scritte in ogni lingua), non i tipi, e il seeder scrive dritto nella tabella senza passare dal validatore. **Un test di
unità nuovo, `CalendarKindSeedFileTests`,** passa ogni tipo del seme dal validatore del back office — la chiave, il colore della
tavolozza, l'ordine, l'etichetta in ogni lingua di `locales/` —: una parola che il back office rifiuterebbe, com'era `onlineDay`
(§2.2), non parte più; e il file deve contenere i quattro tipi degli eventi. `ContentSeedTests` è del maintainer e non si tocca.

### 2.4 Il personaggio dell'ED sul banco

- **`?as=events`**, VID **999005** (libero: grep del 30 settembre 2026), «Bench Events», posizione **`IT-EC`**, che `StaffRoleMap`
  legge come coordinatore dell'ED. **Nessuna casella di Mailpit** finché nessuna mail dello staff degli eventi la chiede (`10`, E1):
  come per l'assistente dei tour, un indirizzo inventato sarebbe uno a cui la coda proverebbe davvero a scrivere. Nessun rating e
  nessuna ora: nessuna fase degli eventi le chiede a chi organizza.
- **In `E2ESignIn`** la classe dell'assistente (posizioni, nessuna casella) diventa `E2EStaffOptions` e serve a tutti e due: la
  stessa forma non si scrive due volte. `E2EOptions.Events` e `E2ESignIn.AsEvents`; senza configurazione `?as=events` risponde 404,
  come gli altri.
- **Fino a E2 non ha permessi degli eventi**: il modulo non c'è ancora. Oggi ha quello che la matrice dà a un coordinatore sul suo
  dipartimento (contenuti, link, media e calendario dell'ED); i permessi degli eventi li porta il seme dei `positionGrants` di E2
  (nota `2026-09-29-chi-lavora-sugli-eventi` §2.4), e da lì le spec degli eventi entrano come lui.
- **I test dei contatti** (`ContactsAndNotificationsTests`) seminano un `IT-EC` con un indirizzo sul database dei test, non sul
  banco: il personaggio senza casella non tocca né loro né la spec dei contatti del banco, che scrive al FOD.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| `onlineDay`, come il design | una riga che il back office non salva più senza rinominarla (§2.2) |
| `onlineday` | il vocabolario la accetta, ma nessuno la scriverebbe così dal form, e il browser confronta le chiavi alla lettera (`calendarKindColour`) mentre il database ignora le maiuscole: un `onlineDay` scritto da qualche parte sarebbe la stessa chiave per l'uno e un'altra per l'altro |
| La regola della chiave allargata alle maiuscole | cambia la regola di ogni tipo per una parola sola, con lo stesso problema di maiuscole fra browser e database |
| Un colore diverso per ognuno dei quattro | la tavolozza ne ha tre liberi; il colore serve a raggruppare, e sono eventi. La divisione li ricolora dal back office |
| Le spec degli eventi con il web master del banco | raggiunge ogni dipartimento e ha ogni permesso: una spec passerebbe anche con i `positionGrants` dell'ED sbagliati (§1) |
| Il personaggio dell'ED con una casella di Mailpit | nessuna mail dello staff degli eventi esiste ancora; la fase che ne manderà una gliela darà |
| Una classe nuova per il personaggio dell'ED | ha la forma dell'assistente dei tour (posizioni, nessuna casella): una classe per tutti e due |
| Il controllo del fork «XX» in `ForkabilityXxDivisionTests` | è del maintainer, e `10` (E1) chiede di non toccarlo: una classe sua, con il suo database, come `TrainingXxDivisionTests` |

## 4. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **Il seme**: `seed/calendar-kinds/kinds.json`, `locales/en/seed.json`, `locales/it/seed.json`. Nessun codice del seeder.
- **Il banco**: `src/IvaoHub.Web/E2E/E2ESignIn.cs`, `web/scripts/e2e-server.mjs`, `web/e2e/full/README.md` e `web/e2e/locales.ts`
  (le parole dei quattro tipi, per la spec).
- **Test**: `CalendarKindSeedTests` (integrazione, scritto in A2 di M3 da questa stessa mano: due casi per i quattro, gli aiuti resi
  generali, i due casi di `exam` uguali), `CalendarKindsXxDivisionTests` (integrazione, nuovo: il fork «XX» nasce con ogni tipo del
  seme, con le sole parole inglesi), `CalendarKindSeedFileTests` (unità, nuovo, §2.3), `web/e2e/full/events-bench.spec.ts` (nuova: il
  personaggio entra come `IT-EC`, e `/api/me` elenca i quattro tipi). Nessun test del maintainer cambiato.
- `docs/FORKING.md`: una riga sulle parole del calendario che un fork riceve dal seme.

## Da portare nel piano

- **§9.5** (il calendario unico): la chiave dell'Online Day è **`online-day`**, non `onlineDay` (§2.2); i quattro tipi degli eventi
  sono nel seme dal rilascio di E1, blu, subito dopo `event`; un test di unità passa ogni tipo del seme dal validatore del back office
  (§2.3).
- **§9.5 e §13, riga M4**: il banco e2e ha un quinto personaggio, `?as=events` (999005, `IT-EC`, senza casella), che riceve i
  permessi degli eventi dal seme dei `positionGrants` di E2.
- `09-design-m4.md` (§1.2, §1.12, §8.1, §13 n.1) e la nota `2026-09-29-i-tipi-di-evento` non si toccano: la grafia `online-day`
  vale da questa nota, e il changelog lo dice.
