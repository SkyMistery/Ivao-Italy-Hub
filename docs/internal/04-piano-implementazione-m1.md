# IVAO Division Hub — Piano di implementazione di M1 (per Claude Code / Opus)

> Documento **interno** (italiano). Prerequisiti di lettura per chi implementa: `CLAUDE.md` (radice),
> piano `00-piano-di-progettazione.md` v0.37 (§8, §9.1, §9.3–§9.5, §16), design `03-design-m1.md` v1.1
> (perimetro, set dei blocchi, firme), `HANDOFF.md` (§3 e §4 sono le regole già attive, §10 i debiti,
> §11–§13 i tre difetti che i test non vedevano). Il design M0 `01-design-m0.md` resta la fonte delle
> firme della spina dorsale.
> Questo file dice **in che ordine** si costruisce M1, **cosa** consegna ogni fase, **come si verifica**
> che sia finita. L'ordine è quello di design §12 (G0–G12); qui ogni fase diventa un perimetro, una
> lista di task e dei criteri di accettazione che sono test.

**Versione:** 1.1 — 5 settembre 2026 (**le tre decisioni che il piano aveva sollevato sono prese**, e
sono nelle fasi che le riguardano: il parser di header per le dimensioni delle immagini in G1,
l'estensione di `CrudOptions` per non mappare la create in G1, la tabella `hub_notification_preferences`
in G7 — quest'ultima ha corretto una contraddizione dentro il design, che è passato a v1.1. Nessuna fase
aperta.)

---

## A. Come si lavora con Claude Code su M1

Le regole sono quelle di M0 (`02-piano-implementazione-m0.md` §A), che hanno retto nove fasi. Si
ripetono qui per intero perché questo file deve bastare da solo, con **quattro aggiunte** che nascono
da come è finita M0.

1. **Una fase per sessione.** Ogni sessione parte con il prompt di apertura (§C). Non si anticipa
   lavoro delle fasi successive «già che ci siamo». Due fasi di M1 sono grosse per costruzione (G3 e
   G8) e possono prendere due sessioni: si resta sullo **stesso branch**, si spezza come dice la fase,
   e la PR si apre alla fine.
2. **`gh pr list` prima di cominciare** (HANDOFF, «Come si apre M1»). Il 3 set 2026 due sessioni in
   worktree diversi non si sono viste e una PR si è ritrovata dodici commit indietro. Costa due secondi.
3. **Branch per fase**: `m1/g<N>-<slug>` da `main`; una PR per fase con il template e la checklist
   §16.E compilata onestamente; merge solo con CI verde. Commit in inglese, Conventional Commits.
4. **Regola (a)/(b)/(c)** di `CLAUDE.md` §5 sempre attiva: se serve un meccanismo che il design non
   prevede, la sessione **si ferma**, scrive `docs/internal/decisions/YYYY-MM-DD-<argomento>.md` (mezza
   pagina) e chiede a Carmine. La fase può chiudersi senza quella parte. In M1 la (b) è la regola
   normale: quasi tutto quello che «manca» è un'estensione di `SchemaForm`, di `MapCrud` o del registry.
5. **Criteri di accettazione = test**: una fase è chiusa quando i test elencati esistono e passano in
   CI, non quando «funziona a mano». I test della spina dorsale di M0 non si spostano né si marcano
   `Skip`.
6. **Niente** stringhe utente nel codice, `fetch` a mano, tabelle `*_translations`, authorization
   handler oltre a `DepartmentAuthorizationHandler`, schermate CRUD scritte a mano, SMTP fuori dal
   servizio notifiche. Se una PR ne contiene, la checklist lo dichiara e Carmine decide.
7. Alla fine di ogni fase si aggiorna `docs/internal/HANDOFF.md` (stato, cosa manca, debiti nuovi), il
   changelog del piano 00 **solo se** c'è stata una decisione, e `CLAUDE.md` **solo su indicazione di
   Carmine**.
8. Versioni delle dipendenze: quelle del design M0 §0.3. In M1 se ne aggiunge **una sola** ed è già
   decisa dal design: `dnd-kit` (G11). Qualunque altra dipendenza nuova è una decisione (c) — vedi §E,
   prima riga.

**Le quattro aggiunte di M1**

9. **Ogni schermata nuova risponde a una domanda in più**: «che cosa, di questa schermata, un test non
   può vedere?» (HANDOFF §10 e §13). In pratica: ogni schermata pubblica nuova porta almeno
   un'asserzione su una **misura** in `web/e2e/` — la colonna di lettura non è più stretta di *n* px,
   la griglia a tre colonne ne ha davvero tre a 1280 px, il menu non copre il contenuto. Il testo
   giusto nel posto sbagliato è passato per otto smoke (§13).
10. **Un test di regressione si verifica rompendolo.** Si toglie la correzione, si guarda che il test
    fallisca, si rimette. Costa trenta secondi e in M0 ha salvato una rete finta (HANDOFF §11: la prima
    versione di `Chrome.test.tsx` sarebbe rimasta verde con l'applicazione rotta).
11. **Il conto della previsione si tiene strada facendo.** Design §12 chiude M1 con un numero: sei
    tabelle, tre aree di permessi, cinque estensioni al generatore di form, quattro componenti custom,
    **un** endpoint scritto a mano. Ogni PR di M1 scrive nel corpo, in tre righe: *endpoint scritti a
    mano aggiunti*, *componenti custom aggiunti*, *meccanismi nuovi aggiunti*. G12 somma; non
    ricostruisce.
12. **Prima di chiamare difetto qualcosa, controllare se è la fixture** (HANDOFF §13: tre falsi allarmi
    su cinque). Costa un grep.

## B. Sequenza delle fasi

L'ordine è quello di design §12, con le dipendenze rese esplicite.

| Fase | Nome | Dipende da | Risultato verificabile |
|---|---|---|---|
| G0 | Rete e2e con l'API vera in CI | — | `pnpm e2e:full`: crea da template → blocchi → pubblica → anonimo vede il pubblicato, in un browser, contro MariaDB vera |
| G1 | Media library | G0 | upload, servizio dei file dietro il query filter, `MediaPicker`, back-office generato |
| G2 | Le cinque estensioni di `SchemaForm` | G1 | media, icona, data, oggetto tradotto, riordino; debiti n.3 e n.4 chiusi |
| G3 | I 16 blocchi Content / Layout / Interactive / Structure | G2 | 21 blocchi nella ui-kit, convenzioni in `UI-GUIDELINES.md` (chiude piano §16.C) |
| G4 | I 6 blocchi Data e i loro provider | G3 | 27 blocchi; `networkStats` mai congelato; provider dietro il query filter |
| G5 | News, documenti, categorie | G4 | due `kind`, due configurazioni di lista, cinque rotte pubbliche, `cms_categories` |
| G6 | Calendario: CRUD interne, `/calendar`, `CalendarView` | G4 | proiezioni in sola lettura, UTC + fuso divisione, il blocco monta lo stesso componente |
| G7 | Contatti, servizio notifiche, namespace `mail` | G2 | un messaggio genera una mail in Mailpit passando dalla coda |
| G8 | Menu editoriale, pagine di sistema, sito pubblico, SEO | G3, G4, G5 | togliere una voce dal menu la toglie dal sito senza ricompilare; `/`, `/start`, `/pilots`, `/atc`, `/about` seedate |
| G9 | Live status e staff directory | G4 | `LiveStatusStrip`, sezione staff di `/about`, nessun profilo pubblico |
| G10 | Ricerca: schermata, rilevanza, evidenziazione | G5, G8 | `/search` e ⌘K; le tre domande di HANDOFF §10 n.10 hanno una risposta scritta e testata |
| G11 | Editor: differenze dal template, dnd-kit, anteprima | G8 | tre stati della diff, «allinea» una differenza alla volta, su/giù da tastiera intatto |
| G12 | Migrazione a mano, giro visivo, chiusura di M1 | tutte | `/about` e `/start` ricopiati, giro visivo eseguito, rapporto di chiusura con i numeri, tag `v0.2.0-m1` |

**Parallelismo.** G5 e G6 non si toccano (tabelle, rotte e schermate diverse) e possono girare in
sessioni parallele **se** si rispetta la regola 2 di §A. G7 dipende solo da G2 e può anticipare G5/G6
se serve. G9 e G10 seguono G8 in qualsiasi ordine. Tutto il resto è sequenziale: G1 → G2 → G3 → G4 è
una catena vera, perché ogni anello è la cosa che l'anello dopo usa.

**Perché G0 è prima.** È il debito n.1 di HANDOFF §10 e la decisione 16 del design. Da G1 in poi ogni
fase aggiunge schermate; una rete che nasce dopo le schermate è una rete che va scritta per venti
schermate insieme, cioè non nasce.

---

## C. Prompt di apertura di ogni sessione (da incollare, sostituendo `<N>`)

```
Stiamo implementando la fase G<N> di M1 dell'IVAO Division Hub.
Esegui `gh pr list` prima di qualunque cosa: altre sessioni possono lavorare in parallelo.
Leggi nell'ordine: CLAUDE.md, docs/internal/03-design-m1.md (tutto), docs/internal/04-piano-implementazione-m1.md
(sezioni A, C, E e la fase G<N>), docs/internal/HANDOFF.md (§3, §4, §10 e i tre racconti §11-§13), poi le
sezioni del piano 00 e del design M0 richiamate dalla fase.
Vincoli: solo il perimetro della fase G<N>; codice, commenti, commit e docs pubbliche in inglese; nessuna stringa
utente nel codice; usa i meccanismi generici, non copie locali — se uno non copre il caso al 100% lo si estende,
non lo si aggira. Se serve un meccanismo che il design non prevede, fermati e scrivi una nota in
docs/internal/decisions/ invece di improvvisare.
Ogni schermata nuova: chiediti che cosa un test non può vederne, e scrivi almeno un'asserzione su una misura.
Ogni test di regressione: verificalo rompendo la correzione.
Chiudi la fase solo con i test dei criteri di accettazione verdi. Alla fine aggiorna docs/internal/HANDOFF.md e
prepara la PR con la checklist compilata e le tre righe del conto (endpoint a mano, componenti custom, meccanismi
nuovi aggiunti da questa fase).
Prima di scrivere codice, elenca in 10 righe cosa farai e quali file toccherai; poi procedi.
```

---

## D. Le fasi

### G0 — Rete e2e con l'API vera in CI

**Obiettivo**: il giro che nessuno ha mai eseguito in un browser — crea da template, aggiungi blocchi,
pubblica, apri `/{slug}` da anonimo — gira in CI contro l'API vera e una MariaDB vera. Design §11.1;
debito n.1 di HANDOFF §10.

Task:
1. **Ambiente `E2E` lato server**: uno schema di autenticazione di prova che emette il cookie
   applicativo per un utente configurato (VID, dipartimenti, posizioni), attivo **solo** quando
   `ASPNETCORE_ENVIRONMENT=E2E`. ⚠️ È un bypass di autenticazione: l'app **rifiuta di partire** se lo
   schema risulta registrato in `Production`, e c'è un test che lo pretende. Non è un login IVAO vero,
   che in CI non è riproducibile (design §11.1).
2. **La SPA la serve l'API**, non un server statico: `dotnet publish` mette `web/dist` in `wwwroot` e
   `MapFallbackToFile` fa il fallback. Così il banco di prova è il pacchetto, non una build a parte, e
   il difetto del tag di M0 (HANDOFF, «Il tag»: `python -m http.server` risponde 404 a
   `/staff/ed/links`) non si ripresenta. Controllo di sanità del banco, prima dei test:
   `curl -o /dev/null -w '%{http_code}' <host>/staff/ed/links` deve dare 200.
3. **Playwright a due progetti**: `smoke` (i 10 esistenti, contro `vite preview`, `/api/me` da
   `e2e/fixtures.ts`, veloci, invariati) e `full` (`e2e/full/`, `baseURL` sull'API pubblicata). Script
   `pnpm e2e` (solo smoke, come oggi) e `pnpm e2e:full`. I due progetti non condividono fixture: quelle
   dello smoke esistono apposta per **non** avere un'API.
4. **CI** (`build-test.yml`): servizio `mariadb:11.4.10` (non Testcontainers — qui il DB serve al
   processo, non al test), `dotnet publish`, avvio in background, attesa su `/health`, `pnpm e2e:full`,
   log dell'API caricati come artefatto quando fallisce. `release.yml` continua a dipendere da
   `build-test`, quindi anche il giro pieno gira prima che uno zip esista.
5. **Il giro**, in `e2e/full/publish.spec.ts`: login finto come staff → crea un contenuto da template →
   aggiunge tre blocchi di famiglie diverse fra i cinque di M0 → pubblica → un **contesto anonimo**
   apre `/{slug}` e vede il pubblicato → modifica la bozza → il pubblico **non** cambia. Due trappole
   già note (design §11.2): il badge dei blocchi Data lo vede solo lo staff, e dopo aver modificato la
   bozza le due rese **devono** divergere.
6. Nota in `docs/internal/decisions/` sull'ambiente `E2E`: perché un bypass, com'è recintato.

**Accettazione**: `pnpm e2e:full` verde in CI e in locale con docker-compose; il test del giro
verificato **rompendolo** (si toglie la ripubblicazione dopo la modifica e l'asserzione sulla
divergenza deve cambiare esito); l'app rifiuta di partire con lo schema di prova in `Production`; i 10
smoke e i 442 test di M0 ancora verdi.

**Non fare**: schermate nuove, blocchi nuovi, tabelle nuove.

---

### G1 — Media library

**Obiettivo**: un'immagine caricata una volta si riusa ovunque. Design §2. Otto dei ventidue blocchi la
nominano, ed è per questo che viene prima di loro.

**Deciso il 5 settembre 2026 — chi legge larghezza e altezza di un'immagine**: un **parser di header**
per i soli formati dell'allowlist (PNG, JPEG, WebP), in **un** helper di `Core/Content`, una sessantina
di righe. Niente `ImageSharp` (licenza da verificare prima di aggiungerla) e niente `SkiaSharp` (asset
nativi dentro un pacchetto self-contained linux-x64, cioè un problema di deploy in cambio di due
numeri). ⚠️ Il perimetro è **quei tre formati**: se un formato futuro chiede di più, non si allarga il
parser di nascosto — è una (c), con la nota.

Task:
1. Entità `MediaAsset` → tabella `cms_media` con le colonne di design §2 (`FileName`, `StoredName`
   opaco, `ContentType`, `ByteSize`, `Width?`, `Height?`, `Alt`, `Title?`, `Category?`);
   `IOwnedByDepartment, IVisible, IAuditable`, **non** `IProjectable` (un file non si cerca da solo, si
   cerca la pagina che lo usa). Migrazione **additiva**.
2. `MediaOptions` (dimensione massima, tipi ammessi, cartella) validate come le altre opzioni;
   `HubPaths.Media` con sottocartelle anno/mese, così che una cartella non arrivi a decine di migliaia
   di voci.
3. **`POST /api/media`**: l'unico endpoint scritto a mano di M1. Valida tipo e dimensione contro il
   limite in configurazione, calcola le dimensioni, scrive su disco e **poi** la riga. Collisione con
   `MapCrud`, che mappa la `POST` di creazione, **risolta il 5 settembre 2026 estendendo
   `CrudOptions`** perché una risorsa possa non mappare la create (regola (b)) — non spostando l'upload
   su `/api/media/upload`. Motivo: una riga `cms_media` senza file non deve poter esistere, e un secondo
   indirizzo per «creare una media» sarebbe il secondo modo di fare la stessa cosa. ⚠️ L'estensione è
   **generica** e vive in `Core/Data/Crud/`: non un ramo `if (typeof(T) == typeof(MediaAsset))`. Se
   `MapCrud` resiste anche così, ci si ferma e si scrive la nota.
4. I **metadati** (alt, titolo, categoria, visibilità) sono `MapCrud` come tutto il resto — lista,
   dettaglio, aggiornamento, cancellazione, policy di dipartimento inclusa — più il back-office
   `/staff/{dept}/media` con la ricetta a tre route.
5. **`GET /media/{id}/{slug}`** da Kestrel con il **query filter** davanti (una media `Staff` non si
   scarica da anonimo conoscendone l'id), `Cache-Control` lungo e immutabile perché l'URL contiene l'id
   e un file non cambia contenuto. Va aggiunto a `SpaFallbackExclusions`, altrimenti la SPA se lo mangia.
6. **Cancellazione**: marca la riga, non tocca il file finché una versione pubblicata lo nomina —
   cancellare un file mentre una pagina già stampata lo mostra la romperebbe. Chi cancella vede prima
   **dove è usata**: una query su `body_json` in **un** helper di `Core/Data` (`JSON_SEARCH`
   parametrizzato, accanto a `FullTextSearch`), mai sparsa negli endpoint. ⚠️ Design §2: se questa parte
   supera la mezza giornata, ci si ferma e si scrive la nota — è il caso (c) dichiarato in anticipo.
7. Componente custom **`MediaPicker`** (elenco chiuso, `docs/UI-GUIDELINES.md` §3) + voce nella ui-kit.
8. Permessi `Media.View`, `Media.Edit` nel catalogo e nella matrice, con la riga di test.

**Accettazione**: `MediaUploadRejectsTypeAndSize`, `MediaStoredNameIsOpaque` (due `logo.png` di due
dipartimenti non si sovrascrivono, e nessun nome caricato diventa un percorso),
`MediaServedBehindVisibilityFilter` (anonimo su una media `Staff` → 404, non 403, che confermerebbe
l'esistenza), `MediaDeleteKeepsFileWhileAVersionUsesIt`, `MediaUsageQueryFindsPagesByMediaId`; Vitest
di `MediaPicker`; a mano, un'immagine caricata dal back-office ricompare nella lista e si scarica.

**Non fare**: usarla nei blocchi (è G3), il campo `.meta({ media: true })` del generatore (è G2).

---

### G2 — Le cinque estensioni di `SchemaForm`

**Obiettivo**: il generatore sa disegnare tutto quello che i 22 blocchi chiedono, e **nessun form si
scrive a mano**. Design §1.6. Chiude di rimbalzo i debiti n.3 e n.4 di HANDOFF §10.

Task, tutti in `web/src/shared/forms/schema.ts` e nei suoi campi:
1. **Selettore di media**: `.meta({ media: true })` su un `z.number()` → apre `MediaPicker`, mostra
   l'anteprima. Mai un id da digitare: un campo numerico libero produce pagine che puntano a file
   cancellati (design §1.5).
2. **Selettore di icona**: `.meta({ icon: true })` su un `z.string()` → select sull'**allowlist** di
   `web/src/blocks/icons.ts` (una trentina di nomi `lucide` per cominciare, che cresce quando serve),
   con l'icona disegnata accanto al nome. Se un'icona manca dal set nasce `web/src/shared/icons/`, che
   oggi non esiste, e nasce **una volta** (design §1.4).
3. **Data e ora**: `.meta({ date: true })` / `datetime`; input nativo, valore ISO in **UTC**, mostrato
   in UTC + fuso della divisione (lo stesso che `DateCell` fa già in lista). `expiresAt` dei grant
   smette di essere una casella di testo: debito n.4 chiuso.
4. **Oggetto tradotto**: `kind: 'localizedObject'` per `Localized<JsonNode>`. Il primo cliente è `Seo`,
   la cui forma design §9.2 decide: `{ title, description, ogImageMediaId }`. Un coordinatore non
   scrive JSON: debito n.3 chiuso.
5. **Riordino dentro una lista**: su/giù accanto ad aggiungi/rimuovi, sulla lista con `useFieldArray`
   che esiste già. Il drag-and-drop è G11 e **non sostituisce** il su/giù, che è quello che funziona da
   tastiera.
6. `docs/UI-GUIDELINES.md` e la ui-kit mostrano i cinque tipi nuovi.

**Accettazione**: un Vitest per ciascuno dei cinque; il form dei grant mostra un campo data e
`expiresAt` arriva al server come ISO UTC; il form dei metadati di un contenuto mostra `seo` come
oggetto tradotto e non come JSON grezzo; `SchemaForm` continua a **lanciare** su un tipo che non sa
disegnare — è la proprietà per cui nessuno scrive un form a mano — e c'è il test che lo pretende.

**Non fare**: dnd-kit, blocchi.

---

### G3 — I 16 blocchi Content, Layout, Interactive, Structure

**Obiettivo**: il grosso del volume di M1, e **zero meccanismo nuovo**. Design §1.2, §1.3, §1.4, §1.5.

I sedici: `hero`, `image`, `video`, `embed`, `timeline`, `table` — `cardGrid`, `iconGrid`, `gallery`,
`logoGrid`, `tabs`, `accordion` — `testimonial`, `buttonGroup`, `spacer`, `divider`.

Ognuno costa **cinque** cose e non una di più (design §1.3): schema zod in `blocks/schemas.ts`,
componente in `blocks/blocks.tsx`, registrazione in `core.ts` (`type`, `version`, `kind`, `schema`,
`component`, `example`, `editorLabelKey`, `icon`), chiavi i18n in tutte le lingue, e per i Data — qui
nessuno — un provider. **Nessuno aggiunge una sezione alla ui-kit**: la galleria monta ciò che il
registry dichiara, ed è la proprietà per cui esiste.

Task:
1. I sedici blocchi, nell'ordine dei gruppi. **Se la fase prende due sessioni**, si spezza qui:
   Content + Layout nella prima, Interactive + Structure nella seconda, stesso branch, PR alla fine.
2. **Allowlist degli host** per `video` ed `embed`, in **un** punto (`web/src/blocks/allowlist.ts`).
3. **Le convenzioni dei blocchi in `docs/UI-GUIDELINES.md`** (design §1.4): la spaziatura la mette la
   sezione e mai il blocco, quattro sfondi, tre larghezze, resa di una sezione `locked`, blocco
   sconosciuto visibile solo allo staff, ogni blocco dichiara la propria icona. **Questo chiude piano
   §16.C**, e la cosa va nel changelog del piano 00.
4. ⚠️ **Due disallineamenti fra §1.4 e il codice di M0, da chiudere qui.** `web/src/blocks/envelope.ts`
   oggi ha `BACKGROUNDS = none | muted | accent` (tre; il design ne vuole **quattro**, con `image` +
   `mediaId`) e `WIDTHS = narrow | default | wide | full` (quattro; il design ne nomina **tre**).
   Raccomandazione: **aggiungere `image`** — envelope zod **e** `BlockDocumentWalker`, che sono la
   coppia che deve restare d'accordo a mano, più il test di integrazione che posta un valore che il
   server non conosce — e **tenere `narrow`**, perché toglierlo non sarebbe additivo su corpi già
   pubblicati. Design §1.4 va corretto di conseguenza e la cosa va nel rapporto di chiusura.
5. ⚠️ **Le trappole di §1.5, una per una**: `tabs` e `accordion` **non contengono blocchi** (portano
   markdown per voce, lo stesso `MarkdownContent` sanitizzato di `text`); `mediaId` apre sempre il
   picker; `icon` è l'allowlist; e ogni props che non è prosa — livelli, allineamenti, nomi di icona —
   è `z.enum` o numero, perché **ogni stringa dentro `props` finisce nel testo della ricerca**.

**Accettazione**: il test accanto a `/staff/admin/ui-kit` monta **21** blocchi e verifica che ogni
`example` soddisfi il proprio schema (un blocco che non compare lì fa fallire la CI); Vitest per gruppo;
un e2e che compone una pagina con `cardGrid` a tre colonne e **misura** che a 1280 px le colonne siano
davvero tre; `pnpm i18n:check` verde con le chiavi di tutti e sedici.

**Non fare**: provider, blocchi Data, il sito pubblico.

---

### G4 — I sei blocchi Data e i loro provider

**Obiettivo**: i sei blocchi che leggono quello che l'hub sa. Design §1.2, gruppo Data. Non aspettano
G5 né G6: leggono tabelle che esistono da M0.

I sei: `stats`, `networkStats` (**`alwaysLive`**), `calendar`, `newsList`, `documentList`, `staffList`.

Task:
1. Lato server, per ciascuno: un `IDataBlockProvider` registrato per `type` e un `IBlockDescriptor` nel
   nucleo, perché il tipo compaia in `/api/me`. `AlwaysLive` esiste già su entrambi i lati del contratto
   (`IBlockDescriptor`, `web/src/shared/modules.ts`): `networkStats` lo dichiara, l'editor non mostra il
   toggle e `ContentPublishService` non congela — la regola sta **nel tipo**, non in un `if` (design §1.5).
2. `stats` risponde su un **insieme chiuso** di metriche del nucleo: membri noti, staff, news
   pubblicate, documenti pubblicati, voci di calendario in arrivo. Non nasce un registro delle metriche:
   un modulo che vuole la propria cifra registra il proprio blocco (design §1.2, correzione 2).
3. ⚠️ **`IvaoApiClient` guadagna qui la lettura dello stato della rete** (Whazzup, cache breve di 60 s,
   non lancia mai), non in G9: `networkStats` è il primo che ne ha bisogno, e G9 aggiunge la striscia,
   non il dato. Sta in `Core/Ivao/`, che è il solo posto dove il nome IVAO può comparire (piano §4.2).
4. ⚠️ **Il componente del blocco `calendar` in G4 è la sola vista agenda.** `CalendarView` nasce in G6,
   quando due schermate lo montano — che è il criterio dell'elenco chiuso, non un dettaglio. In G6 il
   blocco passa a montarlo e il componente provvisorio sparisce.
5. Ogni provider legge **dietro il query filter**: un `newsList` non mostra a un anonimo una news
   `Staff` per il fatto che qualcuno l'ha messa in pagina.

**Accettazione**: `EveryDataBlockTypeHasAProvider` (registry e provider non divergono),
`NetworkStatsIsNeverFrozenOnPublish`, `PublishFreezesNewsListButNotNetworkStats`,
`DataBlockRespectsVisibility` (anonimo contro staff sulla stessa pagina), `StatsMetricsAreAClosedSet`;
la ui-kit monta **27** blocchi e ogni `exampleData` soddisfa il proprio schema.

**Non fare**: le schermate di news, documenti e calendario (G5, G6).

---

### G5 — News, documenti, categorie

**Obiettivo**: dimostrare che due `kind` non sono due tabelle. Design §3. **Questa fase è corta, o
§9.3 non ha retto** — e se non è corta va scritto nel rapporto di chiusura.

Task:
1. Tabella `cms_categories` (`Kind`, `OwnerDepartment`, `Key` stabile, `Label` localizzata, `Sort`,
   `IsActive`), `IOwnedByDepartment, IAuditable`, `MapCrud` e back-office `/staff/{dept}/categories`.
   Migrazione additiva. **Seed vuoto**: le categorie le scrivono i coordinatori dall'interfaccia, ed è
   la risposta a piano §15.8 — non serviva sapere quali sono, serviva che non fossero codice.
   ⚠️ `ContentEntry.Category` resta una **stringa** e contiene la `Key`: nessuna FK, e una categoria
   cancellata lascia la riga con la sua chiave.
2. Back-office: `/staff/{dept}/news` e `/staff/{dept}/documents` sono la **stessa** lista di
   `/staff/{dept}/content` con un filtro fisso su `kind` e una configurazione di colonne diversa (news:
   categoria, copertina, pin; documenti: categoria, ordine, file). Il form dei metadati è lo stesso
   `SchemaForm` con qualche campo in più o in meno. ⚠️ Ricetta a **tre** route (HANDOFF §12): layout con
   guardia e `Outlet`, `index` con i search params, dettaglio fratello. Se serve una route scritta a
   mano invece di una configurazione, è un segnale e va scritto.
3. Pubblico: `/news`, `/news/{slug}`, `/documents`, `/documents/{dept}`, `/documents/{slug}` (design
   §3.3). ⚠️ `/documents`, non `/docs`: `ContentEntry.Url` lo scrive già così ed è quello che finisce in
   `search_index`; il piano è stato corretto in v0.36.
4. Un documento con `FileMediaId` è una scheda con il download (la media di G1); senza, si legge nel
   browser come una pagina qualsiasi.

**Accettazione**: `NoSecondContentEntity` (test di architettura: nessuna entità nuova con un corpo a
blocchi), `PublicNewsShowsOnlyPublishedAndVisible`, `PinnedNewsComeFirst`,
`DocumentWithFileOffersDownload`, `CategoryDeletionLeavesTheContentKey`,
`CategoriesAreScopedToDepartment`; e2e con una misura sulla lista `/news`; **e la riga onesta nella
PR**: è servita una colonna nuova non nullable? un secondo editor? un renderer separato? Se sì, §9.3
non ha retto e va detto.

---

### G6 — Calendario: voci interne, `/calendar`, `CalendarView`

**Obiettivo**: il calendario unico guadagna la UI che gli manca. Design §4. Il modello esiste tutto da
M0 e **non si tocca**.

Task:
1. CRUD delle voci interne (`meeting`, `deadline`, `other`, e qualunque altra stringa lo staff usi) con
   `MapCrud` in `/staff/{dept}/calendar`.
2. ⚠️ **Le voci con `SourceModule != "core"` sono proiezioni**: sola lettura, con un badge nella lista
   che lo dice, e la scrittura impedita da un `ExtraWritePolicy` — non da un handler nuovo. Modificarle
   a mano significa vederle tornare indietro al primo salvataggio dell'entità sorgente.
3. `/calendar` pubblico: mese, settimana, agenda; filtri per `kind` e dipartimento; orari in UTC **e**
   nel fuso della divisione, che viene da `/api/me` (`division.timezone`) e mai da una costante.
4. Componente custom **`CalendarView`** (elenco chiuso, `UI-GUIDELINES.md` §3): lo montano due
   schermate — `/calendar` e il blocco `calendar` — che è esattamente il criterio scritto lì. Atmosphere
   ha `Calendar` come *date picker*, che è un'altra cosa.
5. Il blocco `calendar` di G4 passa a montare `CalendarView`; il componente provvisorio sparisce.
6. Le voci `department` non compaiono al pubblico e non generano notifiche in M1.

**Accettazione**: `ProjectedEntriesAreReadOnly` (una `PUT` su una voce proiettata → 403),
`CalendarPublicHidesDepartmentEntries`, `CalendarShowsUtcAndDivisionTimezone` — Vitest con un fuso
**diverso** da UTC nella fixture: in M0 la fixture aveva `timezone: "UTC"` e le due righe coincidevano,
che è uno dei tre falsi allarmi di HANDOFF §13; e2e con una misura sulla griglia del mese.

---

### G7 — Contatti, servizio notifiche, namespace `mail`

**Obiettivo**: il servizio notifiche del nucleo nasce con **un solo** mittente di intenti, nella forma
che M2 e M3 useranno senza toccarla. Design §5.

**Deciso il 5 settembre 2026 — le preferenze di notifica sono una tabella.** Design §5.2 diceva che in
M1 «esiste la tabella» e §10.2 ne elencava cinque senza contarla: due letture dello stesso capitolo. La
forma è **`hub_notification_preferences`** (`Vid`, `Type`, `Enabled`), non una colonna su `hub_users`,
perché al secondo tipo di notifica la colonna costerebbe una migrazione e la tabella una riga. Il design
è passato a **v1.1** e §10.2 ora dice **sei** tabelle: la sesta non è perimetro nuovo, è la stessa
tabella contata una volta.

Task:
1. Tabella `cms_contact_messages` (design §5.1): `OwnerDepartment` **è** il dipartimento destinatario,
   così la coda del back-office e la policy di scrittura escono gratis dall'handler che esiste già.
2. Form dei contatti **solo per autenticati** (`HubPolicies.SignedIn`, piano §9.1): niente mittente da
   verificare, niente captcha, niente spam; il VID è quello della sessione e non un campo. Componente
   custom **`ContactForm`**, già previsto in `UI-GUIDELINES.md` §3.
3. Back-office `/staff/{dept}/contacts`: lista generata, dettaglio in sola lettura, cambio di stato
   (`new | read | answered | closed`).
4. `INotificationService.QueueAsync(NotificationIntent)` + tabella `hub_notifications` con stato e
   tentativi + job Quartz che svuota la coda con retry. **Non è un bus di eventi**: qui l'asincronia è
   corretta, perché una mail che non parte non deve far fallire il salvataggio.
5. Tabella `hub_notification_preferences` (`Vid`, `Type`, `Enabled`) con **una** preferenza dentro — i
   contatti del proprio dipartimento — e la sua riga in `/me/profile`. Nessuna schermata elaborata
   finché non ci sono tipi da scegliere: il servizio la interroga già, così il secondo tipo di notifica
   non è una migrazione ma una riga.
6. **I template sono file di lingua**: namespace `mail` in `locales/{lng}/mail.json`, letto dal backend
   con `LocaleCatalog`, che esiste già. L'intento porta la lingua **del destinatario**, non quella di
   chi ha scatenato l'invio. `pnpm i18n:check` lo prende in carico senza modifiche, perché legge tutti
   i namespace che trova.
7. In sviluppo l'SMTP è **Mailpit**, già in `docker-compose.yml` dal primo giorno.
8. `ForkabilityXxDivision` cresce fino a coprire le mail: sono il posto nuovo dove una stringa italiana
   può nascondersi (design §11.2).

**Accettazione**: `ContactMessageQueuesOneIntentForTheTargetDepartment`, `NotificationUsesRecipientLocale`,
`NotificationRetriesThenGivesUp`, `NotificationSkippedWhenThePreferenceIsOff`, `ContactFormRefusesAnonymous`,
`NoSmtpOutsideTheNotificationService` (test di architettura: il client SMTP compare in un file solo),
`ForkabilityXxDivision` esteso alle mail; a mano, un messaggio dal form arriva in Mailpit nella lingua
del destinatario.

---

### G8 — Menu editoriale, pagine di sistema, sito pubblico, SEO

**Obiettivo**: il sito pubblico esiste e **non lo disegna il codice**. Design §8. È la fase che risponde
alla domanda di M1, ed è grossa: può prendere due sessioni (menu + pagine seedate, poi rotte pubbliche
+ SEO), stesso branch.

Task:
1. Tabella `cms_menu_items` (`Scope` `public | footer`, `ParentId?`, `Sort`, `Label` localizzata,
   `Path`, `Visibility`, `IsActive`), con `OwnerDepartment` fissato al dipartimento **Web** così
   l'handler di autorizzazione che esiste già decide chi lo tocca senza righe nuove; permessi
   `Menu.View`, `Menu.Edit`; gestione in `/staff/wd/menu`, lista e form generati come tutto il resto.
   Profondità **uno**: un menu a tre livelli è un menu che nessuno usa.
2. `/api/me` compone voci editoriali **∪** voci dei moduli, ordinate. ⚠️ Il contratto `NavItem`
   guadagna un `Label` **opzionale** accanto a `Key`: una voce di modulo porta una chiave i18n (il
   server non sa la lingua), una editoriale porta il testo già tradotto per ogni lingua. Sono due cose
   diverse e restano due campi, non una stringa che a volte è una chiave. Il contratto cambia → OpenAPI
   e `schema.d.ts` rigenerati e committati, con il `git diff --exit-code` della CI che già esiste.
3. Pagine di sistema seedate da `seed/content-pages/*.json`, accanto a `seed/content-templates/`,
   applicate **una volta per file** con la stessa chiave in `hub_division_settings` che
   `ContentTemplateSeeder` usa già (`page.system:<slug>`). Un file nuovo in una release successiva
   aggiunge una pagina senza toccare quelle che lo staff ha modificato.
4. ⚠️ **Il Lorem è tradotto e non nomina l'Italia.** Una frase di riempimento che dice «Benvenuti nella
   divisione italiana» fa fallire `ForkabilityXxDivision`, ed è giusto così.
5. Rotte pubbliche `/`, `/start`, `/pilots`, `/atc`, `/about`, rese dal `ContentRenderer` che esiste.
   `/atc` è una pagina di sistema come le altre, più le card e i deep link verso vIPI che il modulo
   `atc` registra: il modulo resta a bassa complessità e **non** guadagna tabelle (piano §9.2).
6. SEO minima (design §8.4): `<title>` e meta description dalla riga `Seo`, `og:` per pagine e news,
   `sitemap.xml` generata dalle righe pubblicate, `robots.txt`. ⚠️ Entrambi i file vanno in
   `SpaFallbackExclusions`, o la SPA se li mangia. Nessun prerender, nessun prefisso lingua negli URL.

**Accettazione**: `MenuComposesEditorialAndModuleItems`, `MenuIsOwnedByTheWebDepartment` (un
coordinatore di un altro dipartimento → 403), `SystemPagesSeedAppliesOnceAndKeepsStaffEdits`,
`SitemapListsOnlyPublishedAndVisible`, `ForkabilityXxDivision` esteso a pagine seedate e menu;
**e2e**: si toglie una voce dal menu e sparisce dal sito **senza ricompilare**, e una misura sulla home
(la colonna di lettura non più stretta di *n* px, il menu che non copre il contenuto).

---

### G9 — Live status e staff directory

**Obiettivo**: le due cose che leggono da fuori. Design §6.

Task:
1. **Staff directory** da `hub_users` + `hub_user_staff_positions`, già popolate da `UserSyncService`:
   raggruppata per dipartimento, ordinata per livello della posizione (`StaffRoleMap` lo sa già);
   sezione della pagina `/about` e blocco `staffList`, il cui provider è di G4.
2. ⚠️ **Nessun profilo pubblico** (piano §9.7): nome, posizione, e il link al profilo ufficiale IVAO.
   Niente email, niente Discord, niente statistiche.
3. ⚠️ Chi non ha mai fatto login **non compare**, e la pagina lo **dice** con una riga onesta invece di
   fingere completezza. Il roster è «chi ha fatto login almeno una volta» (piano §16.13): non è un
   limite da aggirare, è il dato che si ha — ed è anche l'incentivo giusto.
4. **`LiveStatusStrip`** (componente custom, elenco chiuso) sulla lettura Whazzup di G4: **polling**,
   non SignalR — il proxy Plesk non è il posto per un websocket, e una striscia che si aggiorna ogni
   minuto è più che sufficiente.

**Accettazione**: `StaffDirectoryOrdersByStaffLevel`, `StaffDirectoryExposesNoContactData` (il DTO non
contiene email né altro), `StaffDirectorySaysWhoIsMissing` (Vitest sulla riga onesta),
`LiveStatusDegradesWhenIvaoIsDown` (il client non lancia mai: la striscia mostra l'ultimo dato o niente);
e2e con una misura sulla striscia.

---

### G10 — Ricerca: schermata, rilevanza, evidenziazione

**Obiettivo**: `GET /api/search` esiste da F8; qui guadagna una schermata e **le tre risposte** che M0
aveva lasciato aperte (HANDOFF §10, debito n.10). Design §7.

Task:
1. **Rilevanza**: si ordina per il punteggio di `MATCH … AGAINST` in modalità naturale, **calcolato una
   volta** e selezionato come colonna, non ricalcolato nella `ORDER BY` — su MariaDB la seconda `MATCH`
   identica è ottimizzata, ma scriverla due volte è comunque due posti da tenere uguali. A parità di
   punteggio, il più recente per primo. Sta nell'helper `Core/Data/FullTextSearch.cs`, non negli endpoint.
2. **Evidenziazione lato client**: l'API aggiunge alla riga uno `snippet` **per lingua** — un estratto
   intorno alla prima occorrenza — e il client marca i termini. Il server non sa in che lingua sta
   guardando il browser e non deve tornare HTML. Nessuna libreria: una funzione in `shared/`, testata.
3. **Parole corte**: InnoDB ignora i termini sotto le tre lettere e su una MariaDB condivisa
   `innodb_ft_min_token_size` **non si tocca**. Se la query contiene solo termini troppo corti, la
   risposta lo **dice** con un messaggio tradotto invece di restituire zero risultati senza spiegazione.
   È l'unica delle tre che il codice non può risolvere, e quindi l'unica che deve parlare.
4. Schermate: `/search?q=` pubblica (gli stessi filtri di visibilità del query filter, quindi un anonimo
   trova solo il pubblico) e la **palette ⌘K** per lo staff, che cerca nelle stesse righe più le rotte
   del back-office. La palette è `Command` di Atmosphere: **non** è un componente custom nuovo.

**Accettazione**: `SearchOrdersByRelevanceThenRecency`, `SearchReturnsSnippetPerLocale`,
`SearchTellsWhenEveryTermIsTooShort`, `SearchRespectsVisibility` (esiste da F8 e resta verde); Vitest
dell'evidenziazione, compresi accenti e maiuscole; e2e della palette ⌘K.

---

### G11 — Editor: differenze dal template, dnd-kit, anteprima

**Obiettivo**: le rifiniture, **dopo** che l'editor è stato usato davvero in G8. Design §9.

Task:
1. **Differenze rispetto al template** (debito n.2): l'editor legge il template per `key` di sezione —
   già fa così, le restrizioni non viaggiano nella copia — e mostra tre stati: sezione **nuova** nel
   template e assente qui (con «aggiungi»), sezione presente qui e **tolta** dal template (con
   «rimuovi», mai automatica), sezione **cambiata** nei vincoli (`locked`, `allowedBlocks`). ⚠️ La
   regola non cambia: un template **non riscrive mai** una pagina da solo, e il pubblico continua a
   vedere la versione pubblicata.
2. **«Allinea» applica una differenza alla volta**, mai tutte insieme: un pulsante che riscrive una
   pagina in un colpo è un pulsante che qualcuno preme per sbaglio.
3. **Sezione `locked`** resa come dice design §1.4: i campi sì, la struttura no, e in testa una riga che
   dice **da quale template** viene il vincolo e chi può cambiarlo (`Content.ManageTemplates`). Un
   pulsante disabilitato senza spiegazione produce ticket; una riga che spiega no.
4. **dnd-kit** sulla lista di sezioni e blocchi, **sopra** il su/giù di G2, che resta perché è quello
   che funziona da tastiera.
5. **Anteprima multi-device**: tre larghezze, la stessa pagina. Non è un emulatore, è un `max-width`. Il
   badge dell'anteprima di F7 resta com'è e resta visibile solo allo staff.

**Accettazione**: `TemplateDiffDetectsAddedRemovedAndChanged` (Vitest); «allinea» applica **una**
differenza e lascia le altre; il riordino da tastiera funziona ancora, con il test verificato
**rompendolo**; e2e: si aggiunge una sezione al template, si apre una pagina che ne è nata, l'editor lo
dice, e la pagina pubblica non è cambiata.

---

### G12 — Migrazione a mano, giro visivo, chiusura di M1

**Obiettivo**: usare quello che si è costruito, guardarlo, e chiudere con un numero. Design §0.1, §8.3,
§11, §12.

Task:
1. **Ricopiare `/about` e `/start`** dal sito Blazor **a mano dall'editor**. Non è lavoro di
   riempimento: è il collaudo vero dell'editor, e il risultato atteso è che emergano due o tre attriti
   che nessun test poteva mostrare. Si scrivono; quelli piccoli si correggono qui, quelli grossi
   diventano una nota.
2. **Il giro visivo**, come quello di M0 che in un giorno ha trovato tre difetti (HANDOFF §11–§13):
   ogni schermata nuova aperta in un browser, nei due temi, nelle due lingue, a 1280 e a 375 px.
   ⚠️ Prima di chiamare difetto qualcosa, controllare se è la fixture.
3. **`tools/demo-m1.md`** (EN) con i passi della demo e la «definizione di fatto» di design §0.1
   spuntata voce per voce, comprese le due che M0 non aveva potuto spuntare (anteprima dell'editor
   contro pagina pubblica).
4. **Il rapporto di chiusura con i numeri**, `docs/internal/decisions/2026-XX-XX-m1-review.md`: quante
   tabelle, quante aree di permessi, quante estensioni al generatore, quanti componenti custom, **quanti
   endpoint scritti a mano**, quante righe di meccanismo nuovo — contro la previsione di design §12
   (6 / 3 / 5 / 4 / 1). Se gli endpoint a mano sono cinque e i componenti custom dodici, il messaggio
   non è che M1 è andata male: è che **piano §16 va corretta, e va scritto dove**. Le tre righe di ogni
   PR (§A.11) si sommano, non si ricostruiscono.
5. Revisione della checklist §16.E su tutto il codice di M1; aggiornamento del piano 00 (versione +
   changelog) e di `HANDOFF.md`; `docs/UI-GUIDELINES.md` finale.
6. Tag `v0.2.0-m1`, release CI con artefatto. ⚠️ Il tag si spinge **dopo** il merge e si verifica
   **sull'artefatto**, non sul commit: in M0 ci sono voluti cinque tentativi, il server di prova deve
   fare il fallback SPA, e un grep su un bundle minificato non è una verifica — la verifica è
   comportamentale o non è.

**Accettazione**: Carmine esegue `tools/demo-m1.md` da zero (clone, docker-compose, run) e ogni punto
passa; gli otto punti della «definizione di fatto» di design §0.1 sono spuntati o hanno una riga che
dice perché no; i test della spina dorsale di M0 sono **tutti** ancora verdi; `ForkabilityXxDivision`
passa con il sito pubblico completo; `pnpm i18n:check` è verde con il namespace `mail`.

---

## E. Rischi specifici di M1 e come Claude Code deve reagire

| Situazione | Reazione attesa |
|---|---|
| Serve una **dipendenza nuova** (lettura delle dimensioni di un'immagine, drag-and-drop, un date picker) | Solo `dnd-kit` è già decisa (design §9.3). Ogni altra è una **(c)**: nota di decisione con licenza, peso e cosa succede in un pacchetto self-contained linux-x64. Per le dimensioni delle immagini la raccomandazione è già in G1: un parser di header in un helper, zero dipendenze |
| `MapCrud` non copre un caso (upload multipart, create da non mappare, filtro fisso su `kind`) | **Si estende `MapCrud`** (regola (b)) e lo si scrive nella PR. Mai una schermata CRUD a mano, mai un endpoint «solo per questo caso». Il **secondo** endpoint scritto a mano di M1 è un evento da riportare nel rapporto di chiusura |
| Il generatore di form non copre un tipo | Estendere `shared/forms/schema.ts` (b). Lancia apposta su ciò che non sa disegnare: quella proprietà non si indebolisce per far passare una fase |
| Viene la tentazione di un blocco che **contiene blocchi** (`tabs` con dentro un'immagine e una tabella) | No (design §1.5). Markdown per voce, o una sezione con `layout` a colonne. Un blocco che contiene blocchi è un secondo albero, con un secondo validatore e un secondo modo di sbagliare la profondità |
| Viene la tentazione di un blocco `Columns` | No (design §1.1, HANDOFF §10). Il livello *Row* è già una **proprietà della sezione**, validata dall'envelope da F7. È l'errore più facile copiando la palette di HQ voce per voce, ed è scritto in due documenti perché qualcuno lo proporrà |
| Un componente Atmosphere non si comporta come sembra | **Misurarlo in un browser**, non assumerlo: in due giorni ne sono saltati fuori quattro (`DarkModeToggle`, `Select`, `SidebarContainer`, `Tabs` pinnato a 400 px). Poi wrappare in `shared/ui` restando nell'elenco chiuso; un componente custom nuovo è una decisione, e in M1 ne sono decisi quattro e non di più |
| Serve una **ricetta di route** nuova | Provarla in un browser **prima** che diventi il quarto esemplare (HANDOFF §12: una ricetta sbagliata è stata copiata tre volte, fedelmente, da chi faceva esattamente ciò che il progetto chiede) |
| Una schermata nuova «sembra rotta» | Controllare la fixture prima di segnalare (HANDOFF §13: tre falsi allarmi su cinque). Costa un grep |
| Pomelo/EF non supporta un costrutto (JSON path, `JSON_SEARCH`, punteggio FULLTEXT) | SQL raw **parametrizzato** in un solo helper di `Core/Data`, accanto a `FullTextSearch`. Mai sparso negli endpoint |
| Serve una colonna o una tabella | Migrazione **additiva** nuova. Mai modificare una migrazione già mergiata; mai un `DROP` o un rename nello stesso pacchetto che smette di usare la colonna |
| Serve un permesso non nel catalogo | Aggiungerlo al catalogo e alla matrice (a), con la riga di test. M1 ne prevede sei nomi nuovi in tre aree e **nessun handler** |
| Una fase cresce oltre il suo perimetro (la cancellazione delle media, la diff dal template) | Fermarsi a mezza giornata e scrivere la nota. Per la cancellazione delle media il design lo dice già (§2): il caso (c) è dichiarato in anticipo apposta |
| L'ambiente `E2E` di G0 sembra comodo anche per altro | No. È un bypass di autenticazione: vive in un ambiente solo, l'app rifiuta di partire con esso in `Production`, e nessuna fase successiva lo allarga |
| Un test della spina dorsale «dà fastidio» | Non si skippa: si corregge il codice, o si ferma la fase con una nota. Vale in particolare per `Chrome.test.tsx` e per gli smoke, che sono le uniche cose che montano la **composizione** (HANDOFF §11) |
| Un test nuovo passa sia con la correzione sia senza | Non è un test. Verificarlo rompendo la correzione, ogni volta |
