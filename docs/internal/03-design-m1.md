# IVAO Division Hub — Design di M1 (sito pubblico e nucleo editoriale)

**Versione documento:** 1.2 — 5 settembre 2026
**Autore:** Carmine (IT-DIV), con supporto Claude
**Fonte di verità:** `00-piano-di-progettazione.md` (§8, §9.1, §9.3–§9.5, §16). Perimetro e firme di M0:
`01-design-m0.md`. Stato di M0: `HANDOFF.md`, in particolare §10.
**Stato:** perimetro deciso, quattro bivi di apertura chiusi (§0.4). Le voci ⚠️ di §14 non bloccano M1.

**Changelog 1.2** (5 set 2026): due cose decise dopo G0, entrambe nate dal **fare** il giro invece
che dal leggerlo. **§9.4, i template**: sono strumenti di dipartimento, ma ogni staff li **legge**
tutti — senza, otto dipartimenti su nove non vedono nemmeno «Nuovo da template», e §9.1 (le
differenze rispetto al template) non ha il dato da mostrare. **§14, la dashboard di dipartimento**:
non era in nessun documento, serve, ed entra in G8 nella forma raccomandata dalla nota
`decisions/2026-09-05-dashboard-di-dipartimento.md` — una riga di `cms_contents` per dipartimento,
non un secondo modo di comporre una schermata.

**Changelog 1.1** (5 set 2026): scrivendo il piano di implementazione (`04-piano-implementazione-m1.md`)
è emersa una **contraddizione dentro questo documento**: §5.2 diceva che in M1 «esiste la tabella» delle
preferenze di notifica, e §10.2 elencava cinque tabelle nuove senza contarla. Decisa da Carmine la forma
— `hub_notification_preferences` — e corrette §5.2, §10.2 (che ora dice **sei**), §12 (la previsione da
verificare alla chiusura) e §13 punto 12. Nient'altro del perimetro cambia.

> M0 ha costruito i meccanismi. M1 è la milestone in cui si vede **se erano quelli giusti**, perché il
> sito pubblico dovrebbe essere configurazione molto più che codice (piano §16.15). Questo documento è
> scritto in modo che quella domanda si possa rispondere con un numero alla fine: quante righe di
> meccanismo nuovo sono servite. La previsione è in §12, e chi chiude M1 la confronta con la realtà.

---

## 0. Perimetro di M1

### 0.1 Definizione di «fatto»

M1 è finita quando, in locale (docker-compose + `dotnet run` + `pnpm dev`) e con la CI verde:

1. **il sito pubblico esiste e non lo disegna il codice**: home, `/start`, `/pilots`, `/atc`, `/about`
   sono righe `cms_contents` seedate da template con Lorem tradotto, il **menu** è una tabella che lo
   staff modifica dal back-office, e togliere una voce dal menu la toglie dal sito senza ricompilare;
2. **news e documenti sono due `kind`, non due tabelle**: stessa entità, stesso editor, stesso
   renderer, stessa pubblicazione, stessa proiezione — nel back-office due configurazioni di lista, sul
   pubblico `/news`, `/news/{slug}`, `/documents`, `/documents/{dept}`, `/documents/{slug}`. Se per
   farlo è servita una colonna nuova non nullable o un secondo editor, **§9.3 non ha retto** e va
   scritto nel rapporto di chiusura;
3. **il set dei blocchi è quello di §1**, ogni blocco compare da sé in `/staff/admin/ui-kit`, e le
   convenzioni dei blocchi (§1.4) sono scritte in `docs/UI-GUIDELINES.md` — questo chiude piano §16.C;
4. **il calendario unico ha una UI**: `/calendar` pubblico con filtri, le voci interne create dallo
   staff nel proprio dipartimento, il blocco `calendar` nelle pagine;
5. **media, contatti, staff directory e live status** funzionano: un'immagine caricata una volta si usa
   in un `hero`, in una `gallery` e come copertina di una news; un messaggio di contatto arriva a un
   dipartimento e genera una mail dal **servizio notifiche del nucleo**, mai da SMTP diretto;
6. **la ricerca ha una schermata** (⌘K per lo staff, `/search` pubblica) e le tre domande che M0 aveva
   lasciato aperte (rilevanza, evidenziazione, parole corte) hanno una risposta scritta;
7. **il giro vero è stato eseguito in un browser, contro l'API vera**: crea da template → aggiungi
   blocchi → pubblica → apri `/{slug}` da finestra anonima → il contenuto è quello pubblicato e non la
   bozza. È il debito n.1 di HANDOFF §10 e in M1 diventa una rete, non una speranza (§11);
8. i test della spina dorsale di M0 passano ancora **tutti**, il test di forkabilità «divisione XX»
   passa anche con il sito pubblico completo, e `pnpm i18n:check` è verde con il namespace `mail` nato.

### 0.2 Fuori perimetro (deciso il 5 settembre 2026)

- **Primo pacchetto self-contained e deploy su staging Plesk, foglio `LEGGIMI`**: **spostati a M2**.
  Dipendono dalle risposte A9 di Ivao.It (piano §15.2c) che al 5 set 2026 non sono arrivate, e una
  milestone non si progetta intorno a una risposta che non c'è: se arrivassero durante M1 si aprirebbe
  comunque una fase a parte. Il piano §13 è stato corretto di conseguenza (v0.36, 5 set 2026). La CI
  continua a produrre l'artefatto `publish/` come già fa da M0, quindi non si perde niente: quello che
  si sposta è **il deploy**, non la capacità di pacchettizzare.
- **Import automatico dei contenuti dal sito Blazor**: non si fa. I contenuti si ricopiano **a mano
  dall'editor** (§8.3). Un mapper da un modello che non conosciamo verso l'envelope a blocchi
  costerebbe codice usato una volta sola, e ricopiare è anche il **collaudo vero dell'editor**: se
  rifare `/about` a mano è faticoso, l'editor non è finito e M1 non è finita.
- **Blocchi Data di proprietà di un modulo**: `eventList` arriva con Events (M2), `virtualAirlines` con
  Flight Ops (M3). Piano §9.7 dice che un blocco Data lo registra il modulo che possiede il dato;
  scriverli ora vorrebbe dire scrivere provider vuoti da riscrivere due milestone dopo.
- **iCal, RSS, prerender SEO, SignalR, Discord**: restano dove il piano li ha messi (M6, §16.11).
  Il calendario di M1 si legge nel browser; il feed personale con token è M6.
- **Impersonazione, export dati utente, profilo membro pubblico**: non esistono e non nasceranno
  (piano §9.7, §7 di `CLAUDE.md`).
- **Sospensione automatica dei grant al sync del roster**: era già rimandata da M0 §0.2 e resta fuori:
  il roster è «chi ha fatto login almeno una volta» e non ha un evento di sync da cui pendere.

### 0.3 Quello che M1 non rimette in discussione

Le regole di HANDOFF §3 e §4 sono la spina dorsale, non un'opinione di M0. Un campo tradotto è una
colonna JSON; un CRUD è `MapCrud`; una schermata di back-office è una configurazione di colonne più uno
schema zod; l'autorizzazione è **un** handler; l'audit e le proiezioni le scrive l'interceptor;
l'identità si legge da `ICurrentUser`; il menu, i moduli e i registry arrivano da `/api/me`.

M1 aggiunge molto **volume** sopra questa spina dorsale e pochissimo **meccanismo**. Ogni volta che in
questo documento compare qualcosa di nuovo, accanto c'è scritto perché nessun meccanismo esistente
bastava (regola (c) di `CLAUDE.md` §5) — e nella maggior parte dei casi la risposta è che uno bastava e
si è **esteso** (regola (b)).

### 0.4 Le quattro decisioni di apertura (5 settembre 2026)

| Domanda | Decisione | Dove vive |
|---|---|---|
| Quanto del catalogo di 24 blocchi copre M1 | **Tutti quelli che il nucleo possiede**: 22 nuovi (§1.2). I Data di un modulo arrivano col loro modulo | §1 |
| Staging Plesk e primo pacchetto | **Fuori da M1**, spostati a M2: le risposte A9 non ci sono | §0.2, §13 |
| Migrazione contenuti dal Blazor | **A mano dall'editor**, nessun import | §0.2, §8.3 |
| Il debito n.1 (e2e con API vera) | **Fase presto in M1**, prima che le schermate nuove si accumulino | §11, §12 |

---

## 1. Il set dei blocchi

È il primo capitolo perché piano §16.C dice che **le convenzioni dei blocchi si decidono con il set
davanti**, e il set esiste già come catalogo dal 2 settembre 2026 (piano §9.3, dall'analisi di
`va.ivao.aero/backend`). Qui il catalogo diventa un elenco di righe di registry.

### 1.1 Chi possiede cosa

Il catalogo di §9.3 ha 24 voci. Si tolgono:

- **`Columns`**, che non è un blocco e non lo diventa: il livello *Row* del Page Builder HQ è già una
  **proprietà della sezione** (`layout: stacked | 1/2+1/2 | 1/3+2/3 | 2/3+1/3 | 3x1/3`) e l'envelope lo
  valida da F7 — un blocco `Columns` sarebbe un secondo modo di fare la stessa cosa (`CLAUDE.md` §2).
  È l'errore più facile da fare copiando la palette di HQ voce per voce, ed è scritto qui perché
  qualcuno lo proporrà;
- **`Text`, `CTA`, `Alert/Notice`**, che M0 ha già come `text`, `cta`, `callout`;
- **`Virtual Airlines`**, che è un blocco Data di `flightops` e arriva in M3.

Restano **19** voci del catalogo. M1 ne aggiunge **22**: quelle 19 più tre blocchi Data che §9.3
nominava fra le sezioni derivate senza che il catalogo HQ li avesse — `newsList`, `documentList`,
`staffList`. Il conto voce per voce è in §1.2.

Il registry alla fine di M1 ha **27 blocchi** (5 di M0 + 22), tutti del nucleo. `eventList` (M2) e
`virtualAirlines` (M3) portano il totale a 29 quando i loro moduli esistono.

### 1.2 I ventidue blocchi, voce per voce

`kind` è quello del contratto (`Content` | `Data`), scritto come lo scrive il server. Le props sono la
forma essenziale: la forma esatta è lo schema zod, e vive **solo** in TypeScript (piano §16.5).
`L` = campo `Localized`.

**Gruppo Content**

| type | kind | props (essenziale) | Note |
|---|---|---|---|
| `hero` | Content | `eyebrow L?`, `title L`, `text L?`, `mediaId?`, `align`, `tone`, `primary {label L, href}?`, `secondary?` | Rende il componente custom `Hero` che esiste già dall'elenco chiuso. È il blocco della home e di ogni pagina di sezione |
| `image` | Content | `mediaId`, `alt L?`, `caption L?`, `width`, `rounded` | `alt` vuoto **eredita** quello della media library: l'alt si scrive una volta accanto al file, non a ogni uso |
| `video` | Content | `url` \| `mediaId`, `caption L?`, `aspect` | Host in allowlist, la stessa di `embed`, in **un** punto |
| `embed` | Content | `url`, `title L`, `height` | `title` è l'attributo dell'iframe: senza, un lettore da tastiera trova un riquadro senza nome |
| `timeline` | Content | `variant (steps\|timeline)`, `items[] {title L, text L?, date?, icon?}` | È il blocco di `/start` (piano §8.2: «Timeline + Card + CTA») |
| `table` | Content | `caption L?`, `columns[] {label L, align}`, `rows[][]` di celle `L` | Vedi §1.5: le celle sono `Localized`, non markdown |

**Gruppo Layout e contenitori** (`accordion` sta fra gli *Interactive* nel raggruppamento HQ; qui sta con gli altri contenitori, perché il vincolo che conta su di lui è quello di §1.5)

| type | kind | props (essenziale) | Note |
|---|---|---|---|
| `cardGrid` | Content | `columns (2\|3\|4)`, `cards[] {title L, text L?, mediaId?, href?, icon?}` | Le card della home e di `/pilots` |
| `iconGrid` | Content | `columns`, `items[] {icon, title L, text L?}` | `icon` è un nome `lucide` scelto da una **allowlist** (§1.5), non testo libero |
| `gallery` | Content | `mediaIds[]`, `columns`, `lightbox` | |
| `logoGrid` | Content | `columns`, `items[] {mediaId, name, href?}` | Partner, e in M3 il registro Virtual Airlines lo riuserà con i propri dati |
| `tabs` | Content | `tabs[] {label L, body L markdown}` | ⚠️ **non contiene blocchi**: vedi §1.5 |
| `accordion` | Content | `allowMultiple`, `items[] {question L, answer L markdown}` | Copre la FAQ di piano §9.1 senza un secondo sistema |

**Gruppo Interactive e Structure**

| type | kind | props (essenziale) | Note |
|---|---|---|---|
| `testimonial` | Content | `quote L`, `author`, `role L?`, `mediaId?` | Contenuti di proprietà PR (piano §9.6) |
| `buttonGroup` | Content | `align`, `buttons[] {label L, href, variant}` | |
| `spacer` | Content | `size (sm\|md\|lg\|xl)` | |
| `divider` | Content | `variant (line\|dots)`, `spacing` | |

**Gruppo Data** (ognuno ha un `IDataBlockProvider` registrato per `type` nel nucleo)

| type | kind | props (essenziale) | `alwaysLive` | Provider risponde |
|---|---|---|---|---|
| `stats` | Data | `metrics[]` (insieme chiuso), `columns` | no | Numeri della divisione: membri noti, staff, news pubblicate, documenti pubblicati, voci di calendario in arrivo |
| `networkStats` | Data | `figures[]`, `showFirs` | **sì** | ATC e piloti online in area, dalle API IVAO. Uno stato della rete congelato è un dato scaduto spacciato per attuale (piano §9.3) |
| `calendar` | Data | `kinds[]`, `department?`, `range`, `view`, `limit` | no | Voci di `cms_calendar_entries` dietro il query filter |
| `newsList` | Data | `category?`, `department?`, `limit`, `layout`, `pinnedFirst` | no | Righe `kind = news` pubblicate |
| `documentList` | Data | `category?`, `department?`, `limit`, `groupByCategory` | no | Righe `kind = document` pubblicate |
| `staffList` | Data | `department?`, `includeFirStaff`, `layout` | no | `hub_users` con posizioni, cioè chi ha fatto login almeno una volta (piano §16.13) |

Totale: 6 + 6 + 4 + 6 = **22**.

**Due correzioni al catalogo, dette esplicitamente perché sono cambi di lettura di §9.3.**

1. **`table` e `timeline` sono blocchi `Content`, non `Data`.** §9.3 li elenca fra le sezioni derivate
   («`calendar`, `newsList`, … `timeline`»), seguendo il raggruppamento **visivo** di HQ. Ma la
   distinzione che conta qui non è come un blocco appare, è **da dove viene il suo contenuto**: un
   blocco `Data` ha un provider lato server, un `renderMode`, e la possibilità di essere congelato alla
   pubblicazione. Nessuna query produce una tabella arbitraria e nessuna produce i passi di `/start`:
   sono cose che un redattore **scrive**. Farli `Data` vorrebbe dire inventare un provider che
   restituisce quello che ha ricevuto — cioè un giro a vuoto con un `frozen_json` inutile accanto.
2. **`stats` ha un insieme chiuso di metriche del nucleo.** Un modulo che vuole la propria cifra
   registra **il proprio blocco** (`events.stats`), come §9.7 prescrive per i blocchi Data. Non nasce
   un registro delle metriche: sarebbe un secondo registry accanto a quello dei blocchi, per fare la
   stessa cosa con un livello in più.

### 1.3 Che cosa costa un blocco

Aggiungere un blocco non è spuntare una lista. Sono **cinque** cose, e in M1 si ripetono 22 volte:

1. uno **schema zod** in `web/src/blocks/schemas.ts` (o nel file del modulo);
2. un **componente** in `web/src/blocks/blocks.tsx`, costruito con Atmosphere e con i token del tema;
3. una **registrazione** in `core.ts`: `type`, `version`, `kind`, `schema`, `component`, `example`,
   `editorLabelKey`, `icon` — e `exampleData` se è Data;
4. le **chiavi i18n**: il nome del blocco e l'etichetta di ogni campo, in tutte le lingue della
   divisione (`pnpm i18n:check` lo impone);
5. per i **Data**, un `IDataBlockProvider` lato server registrato per `type`, e un `IBlockDescriptor`
   nel nucleo perché il tipo compaia in `/api/me`.

Nient'altro. In particolare **nessuno aggiunge una sezione alla ui-kit**: `/staff/admin/ui-kit` monta
tutto ciò che il registry dichiara, e il test che le sta accanto fallisce se un `example` non soddisfa
il proprio schema. È la proprietà per cui la galleria esiste.

### 1.4 Le convenzioni dei blocchi (chiude piano §16.C)

Piano §16.C aveva lasciato a M1 tre cose: **spaziature tra sezioni**, **varianti di sfondo**, **resa di
una sezione `locked` nell'editor**. Con il set davanti si decidono così, e finiscono in
`docs/UI-GUIDELINES.md` (inglese, valgono per chi forka).

- **La spaziatura la mette la sezione, mai il blocco.** Un blocco disegna sé stesso e non tocca il
  margine intorno: la sezione ha `padding: none | sm | md | lg` e i blocchi dentro una colonna sono
  distanziati da un solo `gap` costante. Motivo: se due blocchi diversi portassero il proprio margine,
  la distanza fra loro dipenderebbe da quali sono, e nessuno saprebbe più dove cambiarla. `spacer`
  esiste per l'eccezione dichiarata, non per compensare margini incoerenti.
- **Gli sfondi sono quattro e sono della sezione**: `none`, `muted`, `accent`, `image` (con `mediaId`).
  Un blocco non ha sfondo proprio, salvo quelli la cui identità *è* lo sfondo (`hero`, `callout`,
  `testimonial`), che usano comunque i token semantici del tema Atmosphere e mai un colore scritto a
  mano. Due sezioni `muted` consecutive si fondono e va bene: alternare è una scelta del redattore.
- **La larghezza è della sezione**: `default` (la colonna di testo), `wide`, `full`. `full` esiste per
  `hero`, `gallery` e `image`; una sezione di testo larga tutto lo schermo non si legge.
- **Una sezione `locked` nell'editor mostra i campi, non la struttura.** Niente «aggiungi blocco»,
  niente «sposta», niente «elimina»: si vede l'elenco dei blocchi che il template ha messo, ognuno con
  il proprio form di proprietà, e in testa una riga che dice **da quale template** viene il vincolo e
  chi può cambiarlo (`Content.ManageTemplates`). Motivo: un pulsante disabilitato senza spiegazione
  produce ticket; una riga che dice «questa sezione è fissata dal template *Policy*» no.
- **Un blocco sconosciuto** (il registry del server lo dichiara, il client non ha il componente, o
  viceversa) rende un avviso **solo per lo staff**, con il `type`. Il pubblico non vede niente: una
  pagina non si rompe perché un browser è indietro di una release.
- **Ogni blocco dichiara la propria icona `lucide`**, il tipo lo impone. Se un'icona manca dal set si
  aggiunge in `web/src/shared/icons/` nello stesso stile — mai inline in una schermata. La cartella non
  esiste ancora perché in nove fasi nessuna icona è mancata; se M1 la crea, la crea una volta.

### 1.5 Le trappole del set, e come si evitano

- ⚠️ **`tabs` e `accordion` non contengono blocchi.** La tentazione è ovvia — un tab con dentro
  un'immagine e una tabella — e la risposta è no: l'unico annidamento del modello è quello delle
  **sotto-sezioni** (profondità ≤ 3, validata dall'envelope da F7). Un blocco che contenesse blocchi
  sarebbe un secondo albero, con un secondo validatore, un secondo editor e un secondo modo di
  sbagliare la profondità. `tabs` e `accordion` portano **markdown per voce**, che è lo stesso
  `MarkdownContent` sanitizzato di `text` e copre il 90 % dei casi; il restante 10 % è una sezione con
  `layout` a colonne.
- ⚠️ **`mediaId` non è un numero da digitare.** Ogni props che nomina una media apre il **selettore**
  della libreria (§1.6). Un campo numerico libero produrrebbe pagine che puntano a file cancellati.
- ⚠️ **`icon` non è testo libero.** È un nome `lucide` scelto da una allowlist esplicita in
  `web/src/blocks/icons.ts`: l'insieme completo di `lucide` sono migliaia di nomi, e un select con
  migliaia di voci è un campo di testo con più passaggi. L'allowlist parte da una trentina di icone che
  le pagine del sito usano davvero e cresce quando serve.
- ⚠️ **Ogni stringa dentro `props` finisce nel testo della ricerca.** Il walker generico concatena le
  stringhe foglia (design M0 §5.3). Quindi un valore che *non* è prosa — un livello, un allineamento,
  un nome di icona — va modellato come **numero con `choices`** o come `z.enum` con valori che nessuno
  si aspetta di trovare cercando. `heading.level` è già scritto così apposta, e i 22 blocchi seguono la
  stessa regola.
- ⚠️ **`renderMode` non si offre su `networkStats`.** È l'unico `alwaysLive` del set. L'editor non
  mostra il toggle e la pubblicazione non congela: la regola è già nel tipo (`alwaysLive`), non in un
  `if` nell'editor.

### 1.6 Che cosa il set chiede al generatore di form

Il set è la prima cosa che mette sotto sforzo `SchemaForm`, ed è una buona notizia che chieda **poco**:
le liste di oggetti — le `cards`, gli `items`, i `buttons` — sono **già** disegnate (`kind: 'list'` con
`useFieldArray`). Quello che manca è tutto un'estensione di `shared/forms/schema.ts`, mai un form
scritto a mano: il generatore **lancia** su un tipo che non sa disegnare, apposta, e ogni voce qui sotto
è un'eccezione che oggi lo farebbe lanciare.

| Serve | Come si estende | Chi lo chiede |
|---|---|---|
| **Selettore di media** | `.meta({ media: true })` su un `z.number()`; il campo apre `MediaPicker` e mostra l'anteprima | `hero`, `image`, `gallery`, `logoGrid`, `cardGrid`, `testimonial`, copertina delle news |
| **Selettore di icona** | `.meta({ icon: true })` su un `z.string()`; select sull'allowlist, con l'icona disegnata accanto al nome | `iconGrid`, `cardGrid`, `timeline` |
| **Data e ora** | `.meta({ date: true })` / `datetime`; input nativo, valore ISO in UTC, mostrato in UTC + fuso della divisione | voci di calendario, `expiresAt` di un grant (debito n.4 di HANDOFF §10, che si chiude qui di rimbalzo) |
| **Oggetto tradotto** | un `kind: 'localizedObject'` per `Localized<JsonNode>` | il campo `seo` (debito n.3), la cui forma si decide in §9.2 |
| **Riordino dentro una lista** | su/giù accanto ad aggiungi/rimuovi, sulla lista che già esiste | ogni blocco con `items[]` |

Cinque estensioni, un file. È la prova che il generatore era la scelta giusta: 22 blocchi e nessun form
scritto a mano.

---

## 2. Media library

Prima cosa che M1 costruisce dopo i blocchi, perché otto blocchi su ventidue la nominano.

- **Tabella `cms_media`**: `Id`, `OwnerDepartment`, `Visibility`, `FileName` (quello originale, per chi
  scarica), `StoredName` (opaco, generato), `ContentType`, `ByteSize`, `Width?`, `Height?`,
  `Alt: Localized<string>`, `Title: Localized<string>?`, `Category?`, audit, `RowVersion`. Implementa
  `IOwnedByDepartment, IVisible, IAuditable`. **Non** è `IProjectable`: un file non si cerca da solo,
  si cerca la pagina che lo usa.
- **I file stanno su disco**, mai in `longblob` (piano §11.3): sotto `HubPaths.Media`, in
  sottocartelle per anno/mese così che una cartella non arrivi a decine di migliaia di voci. Il nome su
  disco è opaco e non deriva dal nome originale: due file `logo.png` di due dipartimenti non si
  sovrascrivono, e un nome caricato non diventa un percorso.
- **L'upload non passa da `MapCrud`** — ed è l'unico endpoint scritto a mano di M1. `MapCrud` parla
  JSON, e un multipart non è un JSON con un campo in più. `POST /api/media` accetta il file, valida
  **tipo e dimensione** contro un limite esplicito in configurazione, calcola le dimensioni per le
  immagini, scrive su disco e poi la riga. I **metadati** (alt, titolo, categoria, visibilità) sono
  invece `MapCrud` come tutto il resto: lista, form generato, policy di dipartimento inclusa.
- **Servire i file**: `GET /media/{id}/{slug-del-nome}` da Kestrel, con il **query filter** davanti —
  una media `Staff` non si scarica da anonimo per il fatto di conoscerne l'id. Cache lunga e
  immutabile, perché l'URL contiene l'id e un file non cambia contenuto: si sostituisce caricandone un
  altro.
- **Cancellare una media in uso**: la riga si cancella, il file no — subito. Cancellare un file mentre
  una versione pubblicata lo mostra romperebbe una pagina già stampata. La cancellazione **marca** e il
  file resta finché nessuna versione lo nomina; chi cancella vede prima **dove è usata** (una query sul
  walker: le pagine il cui `body_json` contiene quell'id). Se questa parte cresce oltre mezza giornata
  di lavoro, si ferma e si scrive una nota: è esattamente il caso (c).

Permessi nuovi: `Media.View`, `Media.Edit`.

---

## 3. News e documenti

**Questo capitolo è corto apposta.** Se fosse lungo, §9.3 non avrebbe retto.

### 3.1 Non c'è niente da costruire nel dominio

`ContentEntry` ha già `Kind` (`Page | News | Document`) e già le colonne nullable dei due:
`Category`, `CoverMediaId`, `Pinned` per le news; `Sort`, `FileMediaId` per i documenti. Ha già
`Url`, che è **l'unico punto** che decide dove il pubblico legge una riga, e che l'indice di ricerca
usa così com'è. Non nasce una tabella, non nasce un editor, non nasce un renderer.

### 3.2 Nel back-office sono due configurazioni

`/staff/{dept}/news` e `/staff/{dept}/documents` sono la **stessa** lista di `/staff/{dept}/content`
con un filtro fisso su `kind` e una configurazione di colonne diversa (le news mostrano categoria,
copertina e pin; i documenti categoria, ordine e file). Il form dei metadati è lo stesso `SchemaForm`
con qualche campo in più o in meno. Se serve una route nuova a mano invece di una configurazione, è un
segnale e va scritto.

### 3.3 Sul pubblico

| Rotta | Cosa mostra |
|---|---|
| `/news` | Elenco delle news pubblicate visibili, pinned in testa, filtro per categoria e dipartimento |
| `/news/{slug}` | Una news: titolo, copertina, data, dipartimento, corpo a blocchi |
| `/documents` | Tutti i documenti pubblici, filtrabili per dipartimento e categoria |
| `/documents/{dept}` | I documenti di un dipartimento |
| `/documents/{slug}` | Un documento: se ha `FileMediaId` è una scheda con il download, altrimenti si legge nel browser |

⚠️ **`/documents` e non `/docs`.** Piano §8.2 scriveva `/docs`, `ContentEntry.Url` scrive
`/documents/{slug}`. Sono due, e uno dei due è già codice che decide da solo dove va una riga e cosa
finisce in `search_index`. Vince il codice: `/documents`, e §8.2 e §9.4 del piano sono stati corretti
(v0.36). Non è una
preferenza estetica — è che l'entità si chiama `Document`, il `kind` si chiama `document` e il
permesso si chiama `Content.*`: tre parole uguali e un URL diverso sono una cosa in più da ricordare.

### 3.4 Il vocabolario delle categorie

Piano §15.8 lo lascia aperto («da definire con ogni coordinatore prima di M1») e in M1 non si può
aspettare, perché senza vocabolario `Category` resta testo libero e due redattori scrivono «Guide» e
«guide». La forma decisa, che non richiede di sapere *quali* siano le categorie:

- tabella `cms_categories`: `Id`, `Kind` (news | document), `OwnerDepartment`, `Key` (stabile),
  `Label: Localized<string>`, `Sort`, `IsActive`, audit. `IOwnedByDepartment, IAuditable`;
- gestita da `MapCrud` come tutto il resto, dallo spazio del proprio dipartimento;
- `Category` su `ContentEntry` resta una **stringa**, e contiene la `Key`. Nessuna FK: è la stessa
  regola dei moduli (nessuna FK fra contesti) applicata a un vocabolario che può cambiare sotto i
  piedi di righe già pubblicate. Una categoria cancellata lascia la news con la sua chiave e la
  lista la mostra com'è;
- il seed è **vuoto**. Le categorie le scrivono i coordinatori dall'interfaccia, che è la risposta a
  §15.8: non serviva sapere quali sono, serviva che non fossero codice.

---

## 4. Calendario unico con UI

Il modello esiste tutto da M0: `cms_calendar_entries`, `CalendarEntry` con `IOwnedByDepartment`,
`IVisible`, `IAuditable` e `[PermissionArea("Calendar")]`, i permessi `Calendar.View`/`Calendar.Edit`
già in catalogo, e la scrittura delle voci di modulo affidata all'interceptor. Manca la UI.

- **Voci interne** (`meeting`, `deadline`, `other`, e qualunque altra stringa lo staff usi): CRUD con
  `MapCrud` in `/staff/{dept}/calendar`. ⚠️ Le voci con `SourceModule != "core"` sono **proiezioni** e
  vanno mostrate in sola lettura: modificarle a mano significa vederle tornare indietro al primo
  salvataggio dell'entità sorgente. La lista lo dice con un badge, e la write policy lo impedisce.
- **`/calendar` pubblico**: mese, settimana e agenda, filtri per `kind` e dipartimento, orari in UTC
  **e** nel fuso della divisione (standard IVAO, piano §9.5). Il fuso viene da `/api/me`
  (`division.timezone`), mai da una costante.
- **Blocco `calendar`** (§1.2), che è la stessa vista in piccolo dentro una pagina.
- **Componente custom nuovo: `CalendarView`** — decisione esplicita, elenco chiuso di
  `docs/UI-GUIDELINES.md` §3. Motivo: due schermate lo montano (`/calendar` e il blocco), che è
  esattamente il criterio scritto lì; Atmosphere ha `Calendar` come *date picker*, che è un'altra cosa.
- Le voci `department` non compaiono al pubblico e non generano notifiche in M1 (piano §15.9 resta
  aperta per la parte iCal/notifiche, che è M6).

---

## 5. Contatti e servizio notifiche

### 5.1 Contatti

- Tabella `cms_contact_messages`: `Id`, `TargetDepartment`, `FromVid`, `Subject`, `Body`, `Status`
  (`new | read | answered | closed`), `HandledBy?`, `HandledAt?`, audit. `IOwnedByDepartment` —
  `OwnerDepartment` **è** il dipartimento destinatario, così la coda del back-office e la policy di
  scrittura escono gratis dall'handler che esiste già.
- **Il form è visibile solo agli autenticati** (piano §9.1): niente mittente da verificare, niente
  captcha, niente spam. Il VID è quello della sessione e non un campo.
- Back-office: `/staff/{dept}/contacts`, lista generata, dettaglio in sola lettura più cambio di stato.
- Componente custom nuovo: **`ContactForm`**, già previsto in `UI-GUIDELINES.md` §3 come componente di
  M1.

### 5.2 Servizio notifiche

Piano §9.7: **servizio unico nel nucleo, i moduli pubblicano intenti, mai SMTP da un modulo**. In M1
nasce con un solo mittente di intenti — i contatti — ed è la forma che M2 e M3 useranno senza
toccarla.

- `INotificationService.QueueAsync(NotificationIntent)`; l'intento porta destinatari, chiave del
  template, dati, e la lingua **del destinatario** (non quella di chi ha scatenato l'invio).
- Tabella `hub_notifications` con stato e tentativi; un job Quartz svuota la coda con retry. Non è un
  bus di eventi (piano §16.4 lo esclude per le proiezioni): qui l'asincronia è corretta, perché una mail
  che non parte non deve far fallire il salvataggio.
- **I template sono file di lingua**, non righe: namespace `mail` in `locales/{lng}/mail.json`, letto
  dal backend con `LocaleCatalog` che già esiste. È il namespace che HANDOFF §7 diceva sarebbe nato da
  sé, e `pnpm i18n:check` lo prende in carico senza modifiche perché legge tutti quelli che trova.
- In sviluppo l'SMTP è **Mailpit**, già in `docker-compose.yml` dal primo giorno.
- Preferenze per tipo di notifica in `/me/profile`: in M1 nasce la tabella
  **`hub_notification_preferences`** (`Vid`, `Type`, `Enabled`) con una sola preferenza dentro — i
  contatti del proprio dipartimento. Nessuna schermata elaborata finché non ci sono tipi da scegliere.
  Una tabella e non una colonna su `hub_users`: al secondo tipo di notifica la colonna costerebbe una
  migrazione, la tabella una riga (deciso il 5 set 2026; §10.2 la conta).

---

## 6. Staff directory e live status

### 6.1 Staff directory

Il roster è **chi ha fatto login almeno una volta** (piano §16.13): non esiste un endpoint IVAO per il
roster di una divisione, e questo non è un limite da aggirare ma il dato che si ha.

- Sorgente: `hub_users` + `hub_user_staff_positions`, già popolate da `UserSyncService`.
- Blocco `staffList` e sezione della pagina `/about`, raggruppati per dipartimento, ordinati per
  livello della posizione (`StaffRoleMap` lo sa già).
- ⚠️ **Nessun profilo pubblico** (piano §9.7): nome, posizione, e link al profilo ufficiale IVAO
  (`https://www.ivao.aero/Member.aspx?Id={VID}`). Niente email, niente Discord, niente statistiche.
- ⚠️ Chi non ha mai fatto login **non compare**, e la pagina lo dice con una riga onesta invece di
  fingere completezza. È anche l'incentivo giusto.

### 6.2 Live status

- `IvaoApiClient` guadagna la lettura dello stato della rete (Whazzup), con **cache breve** (60 s) e la
  stessa regola di tutte le altre chiamate: non lancia mai, e un dato di un minuto fa batte una pagina
  ferma. Sta in `Core/Ivao/`, che è il solo posto dove il nome IVAO può comparire (piano §4.2).
- Alimenta il blocco `networkStats` e il componente custom **`LiveStatusStrip`**, già previsto in
  `UI-GUIDELINES.md` §3.
- **Polling, non SignalR** (piano §16, §14): il proxy Plesk non è il posto per un websocket, e una
  striscia che si aggiorna ogni minuto è più che sufficiente.

---

## 7. La ricerca: la schermata, e le tre domande che M0 ha lasciato

`GET /api/search?q=` esiste da F8 e legge il FULLTEXT dietro il query filter. M1 gli mette davanti una
schermata e risponde alle tre domande aperte (HANDOFF §10, debito n.10).

1. **Rilevanza sopra la paginazione.** Oggi l'ordinamento non è esplicito. Si ordina per il punteggio
   di `MATCH ... AGAINST` in modalità naturale, **calcolato una volta** e selezionato come colonna, non
   ricalcolato nella `ORDER BY` (su MariaDB la seconda `MATCH` identica è ottimizzata, ma scriverlo due
   volte è comunque due posti da tenere uguali). A parità di punteggio, il più recente per primo.
2. **Evidenziazione.** Lato **client**, sul testo che l'API restituisce: il server non sa in che lingua
   sta guardando il browser e non deve tornare HTML. L'API aggiunge alla riga un `snippet` per lingua —
   un estratto intorno alla prima occorrenza — e il client marca i termini. Nessuna libreria: è una
   funzione in `shared/`, testata.
3. **Parole corte.** InnoDB ignora i termini sotto le tre lettere (`innodb_ft_min_token_size`, che su
   una MariaDB condivisa **non si tocca**). Non è un bug da nascondere: se la query contiene solo
   termini troppo corti, la risposta lo **dice** con un messaggio tradotto invece di restituire zero
   risultati senza spiegazione. È l'unica delle tre che il codice non può risolvere e quindi l'unica
   che deve parlare.

Schermate: `/search?q=` pubblica (stessi filtri di visibilità del query filter, quindi un anonimo trova
solo il pubblico) e la **palette ⌘K** per lo staff, che cerca nelle stesse righe più le rotte del
back-office. La palette è `Command` di Atmosphere: non è un componente custom nuovo.

---

## 8. Il sito pubblico

### 8.1 Il menu non è nel codice

Oggi `/api/me` compone la navigazione pubblica da una voce fissa (`nav.home`) più quelle che i moduli
registrano. Per M1 non basta: il menu di un sito editoriale è editoriale.

- Tabella `cms_menu_items`: `Id`, `Scope` (`public | footer`), `ParentId?`, `Sort`,
  `Label: Localized<string>`, `Path`, `Visibility`, `IsActive`, audit. `OwnerDepartment` è fissato al
  dipartimento **Web**, così l'handler di autorizzazione che esiste già decide chi lo tocca senza
  righe nuove; area permessi `Menu` (`Menu.View`, `Menu.Edit`).
- `/api/me` compone: voci editoriali **∪** voci dei moduli, ordinate. Il contratto `NavItem` guadagna
  un `Label` opzionale accanto a `Key`: una voce di modulo porta una **chiave i18n** (il server non sa
  la lingua), una voce editoriale porta il testo già tradotto per ogni lingua. Sono due cose diverse e
  si vedono come due campi, non come una stringa che a volte è una chiave.
- Profondità **uno** (voce e sotto-voci). Un menu a tre livelli è un menu che nessuno usa.
- Gestione in `/staff/wd/menu`, lista + form generati come tutto il resto.

### 8.2 Le pagine di sistema

Piano §9.3 e design M0 §5.6: le pagine di sistema (home, `/start`, `/pilots`, `/atc`, `/about`) sono
righe `kind = page` **seedate al primo avvio dai template**, con contenuto Lorem tradotto. Chi forka le
riempie dall'editor, **mai dal codice**.

- I seed stanno in `seed/content-pages/*.json`, accanto a `seed/content-templates/`, e si applicano una
  volta sola per file con la stessa chiave in `hub_division_settings` che `ContentTemplateSeeder` usa
  già (`page.system:<slug>`). Un file nuovo in una release successiva aggiunge una pagina senza toccare
  quelle che lo staff ha modificato.
- ⚠️ **Il Lorem è tradotto e non nomina l'Italia.** Il test di forkabilità «divisione XX» apre queste
  pagine: una frase di riempimento che dice «Benvenuti nella divisione italiana» fa fallire la CI, ed è
  giusto così.
- `/atc` è una pagina di sistema come le altre, più le card e i deep link verso vIPI che il modulo
  `atc` registra. Piano §9.2 riga 4: in M1 il modulo resta «bassa complessità» e non guadagna tabelle.

### 8.3 La migrazione dei contenuti

Si ricopia a mano dall'editor (§0.2). Il risultato atteso è che, ricopiando `/about` e `/start`,
emergano due o tre attriti dell'editor che nessun test poteva mostrare — ed è per questo che si fa
prima della fine, non l'ultimo giorno.

### 8.4 SEO

Nessun prerender (piano §16.11, §15.4 chiusa). Quello che M1 fa è il minimo che serva davvero:
`<title>` e meta description dalla riga `Seo`, `og:` per news e pagine, `sitemap.xml` generata dalle
righe pubblicate, `robots.txt`. Nessun prefisso lingua negli URL (§15.6 chiusa).

---

## 9. L'editor: le rifiniture di M1

### 9.1 Differenze rispetto al template (debito n.2)

La regola non cambia: **un template non riscrive mai una pagina da solo**, e il pubblico continua a
vedere la versione pubblicata. Quello che manca è che l'editor lo **dica**.

- L'editor legge il template per `key` di sezione (già fa così: le restrizioni non viaggiano nella
  copia) e mostra tre stati: sezione **nuova** nel template e assente qui (con «aggiungi»), sezione
  presente qui e **tolta** dal template (con «rimuovi», mai automatica), sezione **cambiata** nei
  vincoli (`locked`, `allowedBlocks`).
- L'azione «allinea» applica **una** differenza alla volta, mai tutte insieme: un pulsante che
  riscrive una pagina in un colpo è un pulsante che qualcuno preme per sbaglio.

### 9.2 La forma di `seo` (debito n.3)

`Seo` è `Localized<JsonNode>` e nessuno ha ancora detto cosa contiene. M1 è l'unica milestone che ha
una ragione per deciderlo, e decide il minimo: `{ title, description, ogImageMediaId }`. Nel form è un
**oggetto tradotto** — l'estensione di §1.6 — e non un campo JSON grezzo: un coordinatore non scrive
JSON.

### 9.3 Il resto

- **dnd-kit** sulla lista di sezioni e blocchi, sopra il su/giù che già esiste. Il su/giù resta:
  è quello che funziona da tastiera.
- **Anteprima multi-device**: tre larghezze, la stessa pagina. Non è un emulatore, è un `max-width`.
- **Il badge dell'anteprima** che F7 ha introdotto resta com'è, e resta visibile solo allo staff.

### 9.4 Di chi sono i template, e chi li legge (deciso il 5 set 2026)

Un template **appartiene a un dipartimento**: ogni dipartimento si fa i suoi, e li modifica con il
`Content.ManageTemplates` che ogni coordinatore ha già sul proprio. Quello che cambia rispetto a M0 è
che **ogni staff li legge tutti** — l'elenco e il corpo.

Motivo, trovato in G0 con una sessione vera: i tre template seminati appartengono a WD, `Content.View`
è di dipartimento, e quindi per un coordinatore ED `filter[isTemplate]=true` risponde zero righe e il
selettore non compare affatto. Peggio: una pagina nata da un template che il suo editore non può
leggere perde i vincoli del template nell'editor (`templateRules` cade su `NO_RULES`), perché le
restrizioni non viaggiano nella copia — quindi **§9.1 non funzionerebbe** per nessuno fuori da WD.

Non è una concessione grande: i template sono `Visibility.Staff`, non sono pubblicabili, e non
contengono dati ma struttura. La scrittura non si muove di un millimetro. **Usare** il template di un
altro dipartimento crea una pagina **nel proprio**, che è già ciò che `CreateFromTemplateAsync`
chiede; **copiarlo** nel proprio dipartimento — una copia che diventa tua — è il modo di divergere e
si costruisce quando serve, perché è la stessa copia profonda che esiste.

Si implementa estendendo due meccanismi in modo generico, mai insegnando loro che cosa sia un
template: un predicato di righe condivise in `CrudOptions` per il restringimento della lista, e la
stessa dichiarazione letta dall'**unico** authorization handler quando il permesso è di lettura.
Nota: `decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`. Lavoro in **G5**.

---

## 10. Permessi, tabelle e migrazioni

### 10.1 Permessi nuovi

| Permesso | Scope | Perché |
|---|---|---|
| `Media.View`, `Media.Edit` | dipartimento | La libreria è per dipartimento come tutto il resto |
| `Contacts.View`, `Contacts.Manage` | dipartimento | La coda dei messaggi del proprio dipartimento |
| `Menu.View`, `Menu.Edit` | dipartimento (Web) | Il menu appartiene al dipartimento Web |

Nessun handler nuovo. Ogni riga qui sopra è un nome nel catalogo e una riga nella matrice
(`RolePermissionMatrix`), che è esattamente quello che piano §16.3 prometteva.

### 10.2 Tabelle nuove

`cms_media`, `cms_categories`, `cms_menu_items`, `cms_contact_messages`, `hub_notifications`,
`hub_notification_preferences`. **Sei**, tutte **additive**. Le prime quattro hanno lo stesso stampo
(localized + dipartimento + visibilità + audit); le due `hub_` no, perché una coda e una preferenza non
appartengono a un dipartimento e non si traducono.

⚠️ Fino alla v1.0 di questo documento erano **cinque**: §5.2 nominava la tabella delle preferenze e
questo elenco non la contava. La sesta non è perimetro nuovo, è la stessa tabella contata una volta.

### 10.3 Migrazioni

Regola invariata e non negoziabile (piano §11.3): **solo additive**, expand/contract su due release,
mai `DROP` o rename nello stesso pacchetto che smette di usare la colonna, catena intera applicata su
una MariaDB 11.4.10 vera in CI. M1 aggiunge tabelle e non tocca colonne esistenti: è il caso facile, e
va tenuto tale.

---

## 11. La rete di test di M1

HANDOFF §11–§13 racconta tre difetti trovati **guardando** l'applicazione, nessuno da un test, e li
mette in scala: prima non si disegnava niente, poi niente era raggiungibile, poi tutto funzionava dentro
una colonna da 255 pixel. La domanda che M1 si fa su ogni schermata nuova non è «ho scritto i test?» ma
**«che cosa, di questa schermata, un test non può vedere?»**.

### 11.1 Il giro vero, presto (debito n.1)

Oggi gli smoke Playwright stubbano `/api/me` da `e2e/fixtures.ts` e ogni altra chiamata `/api`
fallisce apposta. Quindi «lo staff apre l'editor, aggiunge un blocco, pubblica» non è **mai** stato
eseguito in un browser. In M1 è una fase, ed è presto — prima che le schermate nuove si accumulino:

- un servizio MariaDB in CI (non Testcontainers: qui il DB serve al processo, non al test);
- l'API avviata e **attesa su `/health`**, con `ICurrentUser` forzato a uno staff finto tramite una
  configurazione di test — non un login IVAO vero, che non è riproducibile in CI;
- la SPA di produzione servita davanti con il **fallback SPA** (⚠️ HANDOFF §1: un server statico
  risponde 404 a `/staff/ed/links` e sembra un difetto del pacchetto; il controllo che dice subito da
  che parte sta il problema è `curl -o /dev/null -w '%{http_code}' <host>/staff/ed/links`);
- il giro: crea da template → aggiungi tre blocchi di famiglie diverse → pubblica → apri `/{slug}` in
  un contesto **anonimo** → il contenuto è quello pubblicato; modifica la bozza → il pubblico **non**
  cambia.

### 11.2 Quello che le schermate di M1 aggiungono

- **Geometria, non solo testo** (la lezione di §13): ogni schermata pubblica nuova ha almeno
  un'asserzione su una **misura** — la colonna di lettura non è più stretta di *n* px, il menu non
  copre il contenuto, la griglia a tre colonne ne ha davvero tre a 1280 px.
- **Il set dei blocchi si prova nella galleria**: il test accanto a `/staff/admin/ui-kit` monta tutti e
  27 e verifica che ogni `example` soddisfi il proprio schema. Un blocco nuovo che non compare lì fa
  fallire la CI.
- **L'anteprima contro la pagina pubblica**, l'unico punto della «definizione di fatto» di M0 che
  nessuno ha ancora guardato con gli occhi (HANDOFF, «il giro visivo»). Con l'API vera in CI diventa
  finalmente automatizzabile: stessa riga, due rese, stesso `ContentRenderer`, e se divergono non lo
  stanno usando entrambe. ⚠️ Due trappole già note: il badge dei blocchi Data lo vede **solo lo staff**
  (in finestra anonima non c'è, ed è corretto), e se si modifica la bozza dopo aver pubblicato le due
  **devono** divergere.
- **Forkabilità**: `ForkabilityXxDivision` cresce fino a coprire le pagine di sistema seedate, il menu
  e le mail. Le mail sono il posto nuovo dove una stringa italiana può nascondersi.

---

## 12. Ordine di lavoro proposto

Non è il piano di implementazione — quello è il documento successivo, `04-piano-implementazione-m1.md`,
con una fase per sessione e i prompt di apertura, come `02-` per M0. È l'ordine, con il motivo.

| # | Fase | Perché qui |
|---|---|---|
| **G0** | Rete e2e con API vera in CI | Debito n.1. Prima di tutto: da qui in poi ogni schermata ci si appoggia |
| **G1** | Media library (tabella, upload, servire, picker) | Otto blocchi su ventidue la nominano |
| **G2** | Estensioni di `SchemaForm` (media, icona, data, oggetto tradotto, riordino) | Le chiedono i blocchi, e chiudono i debiti n.3 e n.4 |
| **G3** | Il set dei blocchi, gruppi Content/Layout/Interactive/Structure (16) | Il grosso del volume, zero meccanismo |
| **G4** | I blocchi Data (6) e i loro provider | Non aspettano G5 né G6: `newsList` e `documentList` leggono `cms_contents`, `calendar` legge `cms_calendar_entries`, `staffList` legge `hub_users` — tutte tabelle che esistono da M0 |
| **G5** | News, documenti, categorie | Configurazione, non codice. Se costa più di una fase, §9.3 non ha retto |
| **G6** | Calendario: CRUD interne, `/calendar`, `CalendarView` | |
| **G7** | Contatti + servizio notifiche + namespace `mail` | Il servizio nasce con un solo mittente di intenti |
| **G8** | Menu editoriale, pagine di sistema seedate, **dashboard di dipartimento** (§14), sito pubblico, SEO minima | Ha bisogno dei blocchi (G3/G4) per avere qualcosa da mostrare; la dashboard è la stessa macchina di seed, una riga per dipartimento |
| **G9** | Live status e staff directory | |
| **G10** | Ricerca: schermata, rilevanza, evidenziazione | |
| **G11** | Editor: differenze dal template, dnd-kit, anteprima multi-device | Le rifiniture dopo che l'editor è stato usato davvero in G8 |
| **G12** | Migrazione a mano dei contenuti, giro visivo, chiusura | Il giro visivo di M0 ha trovato tre difetti in un giorno: si rifà |

**La previsione da verificare alla chiusura**: M1 aggiunge sei tabelle, tre aree di permessi, cinque
estensioni al generatore di form, quattro componenti custom (`CalendarView`, `ContactForm`,
`LiveStatusStrip`, `MediaPicker`) e **un solo endpoint scritto a mano** (l'upload multipart). Tutto il
resto — 22 blocchi, sei schermate di back-office, otto rotte pubbliche — dovrebbe essere
configurazione. Se alla fine gli endpoint a mano sono cinque e i componenti custom dodici, il messaggio
non è che M1 è andata male: è che §16 va corretta, e va scritto dove.

---

## 13. Decisioni prese in questo design (da riportare nel piano)

1. **Staging Plesk, primo pacchetto e foglio `LEGGIMI` escono da M1 e vanno in M2** (§0.2). Fatto nel
   piano v0.36: la riga M1 di §13 perde il deploy, la riga M2 lo acquista, e §15.2c dice ora che blocca
   M2.
2. **Nessun import dei contenuti dal Blazor**: si ricopia a mano dall'editor (§0.2, §8.3). Piano §13
   riga M1 perde «migrazione contenuti dal Blazor» nel senso automatico e la tiene nel senso manuale.
3. **Il set dei blocchi di M1 è di 22 voci** (§1.2); i Data di proprietà di un modulo arrivano col
   modulo (`eventList` M2, `virtualAirlines` M3).
4. **`table` e `timeline` sono blocchi `Content`**, non `Data`: §9.3 li elencava fra i derivati
   seguendo il raggruppamento visivo di HQ, ma non hanno un provider e non hanno senso congelati.
5. **`tabs` e `accordion` non contengono blocchi**, portano markdown per voce: l'unico annidamento del
   modello resta quello delle sotto-sezioni (≤ 3).
6. **`stats` ha un insieme chiuso di metriche del nucleo**; un modulo che vuole la propria cifra
   registra il proprio blocco. Nessun registro delle metriche.
7. **Le convenzioni dei blocchi** (spaziature dalla sezione, quattro sfondi, tre larghezze, resa di una
   sezione `locked`, blocco sconosciuto solo per lo staff) sono decise in §1.4 e chiudono piano §16.C.
8. **L'URL dei documenti è `/documents`**, non `/docs`: piano §8.2 e §9.4 corretti in v0.36 (§3.3).
9. **Il menu pubblico è editoriale**: tabella `cms_menu_items`, composta con le voci dei moduli in
   `/api/me`; `NavItem` guadagna un `Label` tradotto accanto alla `Key` (§8.1).
10. **Il vocabolario delle categorie è una tabella gestita dai coordinatori** (`cms_categories`), non
    una configurazione e non codice: chiude piano §15.8 senza doverne conoscere il contenuto (§3.4).
11. **Le media stanno su disco con nome opaco**, servite da una rotta con il query filter davanti, e
    l'upload è **l'unico endpoint scritto a mano** di M1 (§2).
12. **Il servizio notifiche nasce in M1** con i contatti come unico mittente di intenti, coda + Quartz,
    template nel namespace `mail` dei file di lingua, e le preferenze in una **tabella**
    (`hub_notification_preferences`) e non in una colonna di `hub_users` (§5.2, §10.2).
13. **Le tre risposte della ricerca**: rilevanza esplicita con punteggio selezionato una volta,
    evidenziazione lato client su uno `snippet` per lingua, parole corte **dette** all'utente (§7).
14. **Quattro componenti custom nuovi** nell'elenco chiuso: `CalendarView`, `ContactForm`,
    `LiveStatusStrip`, `MediaPicker`. `RatingBadge`, `AirportCard` ed `EventTimeline` restano ai moduli
    che li useranno (M2, M3) e non si iniziano in M1.
15. **Cinque estensioni al generatore di form**, mai un form a mano (§1.6): media, icona, data,
    oggetto tradotto, riordino nelle liste. Chiudono anche i debiti n.3 e n.4 di HANDOFF §10.
16. **La rete e2e con API vera è la prima fase di M1**, non l'ultima (§11.1, §12).
17. **I template sono di dipartimento e li legge tutto lo staff** (§9.4): la scrittura resta
    `Content.ManageTemplates` sul proprietario, usare quello di un altro crea una pagina nel proprio,
    copiarlo nel proprio è il modo di divergere e si costruisce quando serve.
18. **Ogni dipartimento nasce con una dashboard**, una riga di `cms_contents` seminata e poi
    modificabile nell'editor che esiste — non un secondo modo di comporre una schermata (§14 e la
    nota dedicata). Entra in G8; la forma è raccomandata e va confermata.

---

## 14. Ancora aperto (non blocca M1)

- ⚠️ **La forma della dashboard di dipartimento.** Chiesta il 5 set 2026 e assente da ogni documento
  fino a quel giorno: ogni dipartimento nasce con la propria dashboard, poi la modifica. La nota
  `decisions/2026-09-05-dashboard-di-dipartimento.md` misura il bivio — **blocchi** (una riga di
  `cms_contents` per dipartimento, `kind = Dashboard`, `visibility = Department`, seminata da un
  template e modificata nell'editor che esiste) contro **widget** (le tile di `/me`, che però per
  essere disposte per dipartimento vorrebbero un secondo editor) — e raccomanda i blocchi. Con quella
  forma il lavoro è un delta piccolo dentro **G8**, che il seed delle pagine di sistema lo costruisce
  comunque; con l'altra è un meccanismo nuovo e la sua casa è M2. Da confermare prima di G8.
  È anche il primo cliente vero di §9.4: senza lettura condivisa dei template, otto dipartimenti su
  nove non potrebbero leggere il proprio template di partenza.
- ⚠️ **Risposte A9 di Ivao.It** (piano §15.2c) e **dominio di staging** (§15.3): ora bloccano M2, non M1.
- ⚠️ **Cosa significa `firStaffScope`** (debito n.6 di HANDOFF §10). In M0 le posizioni FIR non danno
  nessun permesso, che è la lettura più restrittiva e quella che si può solo allargare. M1 non ne ha
  bisogno; il primo modulo che tratta una FIR come un ambito (Events, M2) è il posto dove deciderlo.
- ⚠️ **Feed iCal e notifiche del calendario** (piano §15.9): M6.
- ⚠️ **Storico tour** (§15.2d), **accesso al DB di PATS** (§15.7), **contenuto di `specialops`**
  (§15.10): fuori da M1 per costruzione.
- `LocalizedExtensions` va spostato nei progetti di test al primo giro che li tocca (debito n.7).
- `HubUser` è `[Audited]` e ogni login lascia una riga (debito n.8): se in M1 la tabella dà fastidio,
  si restringe lì e non prima.
- La cache della manutenzione è di cinque secondi con un processo solo (debito n.9): invariato.
