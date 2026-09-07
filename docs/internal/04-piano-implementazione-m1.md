# IVAO Division Hub — Piano di implementazione di M1 (per Claude Code / Opus)

> Documento **interno** (italiano). Prerequisiti di lettura per chi implementa: `CLAUDE.md` (radice),
> piano `00-piano-di-progettazione.md` v0.37 (§8, §9.1, §9.3–§9.5, §16), design `03-design-m1.md` v1.1
> (perimetro, set dei blocchi, firme), `HANDOFF.md` (§3 e §4 sono le regole già attive, §10 i debiti,
> §11–§13 i tre difetti che i test non vedevano). Il design M0 `01-design-m0.md` resta la fonte delle
> firme della spina dorsale.
> Questo file dice **in che ordine** si costruisce M1, **cosa** consegna ogni fase, **come si verifica**
> che sia finita. L'ordine è quello di design §12 (G0–G12); qui ogni fase diventa un perimetro, una
> lista di task e dei criteri di accettazione che sono test.

**Versione:** 2.8 — 7 settembre 2026 (**i quattro difetti di G13 sono chiusi**, e la prima delle
richieste con loro: il logout ridisegna la pagina — il bootstrap non è una query ma il contesto del
router, e una query invalidata non rifà un `beforeLoad` — e una pagina non si pubblica più portando
un'immagine che i suoi lettori non possono vedere, sotto lo stesso `VisibilityCeiling` dei blocchi
Data. La sigla di un dipartimento è il suo segno. Fatte anche le richieste 5, 6 e 9: lo `slug` proposto dal titolo — la **settima** estensione del
generatore di form — e l'avviso a quattro stati, che è il **quinto componente custom** e porta con
sé la conferma che l'editor deve a chi clicca. Fatte anche la 7 — con lei il **quarto** verbo a mano appeso a `MapCrud` — e le 10, 12 e 13.
Restano la **11**, che va proposta prima di scriverla, la **14** e il tag.)

**2.7** — 7 settembre 2026 (**si apre G13**, che non era previsto e c'è per una buona
ragione: Carmine ha eseguito `tools/demo-m1.md` fino al punto 7 e ha trovato **quattro difetti e
dodici richieste**. Due difetti sono già corretti — ogni data dell'hub era mostrata due ore indietro,
e cancellare una riga lasciava la pagina aperta — e il secondo era una **regressione della correzione
del loader** della stessa mattina. Il tag `v0.2.0-m1` **aspetta la fine di G13**. Elenco completo e
decisioni in `decisions/2026-09-07-dopo-la-demo.md`.)

**2.6** — 7 settembre 2026 (**G12 è chiusa, e con lei M1**, meno il tag. La fase che
verifica invece di costruire ha trovato più difetti di qualunque altra, e nessuno era trovabile
prima: `/about` e `/start` ricopiate a mano dall'editor hanno fatto uscire che **ogni form del
back-office si poteva salvare una volta sola per caricamento di pagina** e che **le pagine seminate
non soddisfacevano i propri template**, in quattro modi. Il giro visivo, misurato invece che
guardato, ha aggiunto il grigio che non passa AA nel tema scuro e le props che finiscono nell'indice
di ricerca. Il conto contro la previsione di design §12 è **6 / 3 / 6 / 4 / 7** contro 6 / 3 / 5 / 4
/ 1 — e il settimo numero ha insegnato che la metrica misurava la cosa sbagliata: di CRUD scritti a
mano ce ne sono **zero**. Restano due cose, entrambe di Carmine: eseguire `tools/demo-m1.md` da zero,
e il tag `v0.2.0-m1` dopo il merge.)

**2.5** — 7 settembre 2026 (**G11a è chiusa**, la mezza fase che G11 si è lasciata dietro:
i quattro campi su cui poggia tutto §9.1 — `key`, `required`, `locked`, `allowedBlocks` — si scrivono
da una schermata, e design §9.4 smette di promettere ai coordinatori una cosa che il prodotto non
manteneva. Il generatore di form ha imparato il **sesto** tipo di campo, `multi`, e §12 ne prevedeva
cinque: il numero da riportare alla chiusura è sei, e la ragione dello scarto è scritta. La `key` si
scrive una volta sola. Zero tabelle, zero permessi, zero endpoint, zero migrazioni, zero dipendenze.
La prossima è G12, l'ultima.)

**2.4** — 7 settembre 2026 (**G11 è chiusa**: l'editor dice quando il template si è mosso,
applica **una** differenza alla volta, si trascina con dnd-kit senza perdere le frecce che sono
l'unica strada da tastiera, e ha un'anteprima a tre larghezze che il banco **misura**. Il terzo stato
di design §9.1 è stato corretto invece che improvvisato: «i vincoli sono cambiati» non è calcolabile,
«la pagina non soddisfa più il vincolo di adesso» sì. Zero tabelle, zero permessi, zero endpoint a
mano, zero migrazioni; una dipendenza nuova, dnd-kit, che il design chiedeva per nome. La prossima è
G12, l'ultima.)

**2.3** — 7 settembre 2026 (**G10 è chiusa**, e con essa il **debito n.10 di M0**: la
ricerca ha una schermata pubblica, una palette ⌘K per lo staff, e le tre domande lasciate aperte
hanno una risposta ciascuna. Una colonna nuova sull'indice, zero tabelle, zero permessi, zero
endpoint a mano, zero componenti custom. Due contratti di libreria misurati invece che supposti — EF
Core sul filtro di una proiezione, `cmdk` sul filtro dei suoi item — e tutti e due hanno cambiato il
codice. La prossima è G11.)

**2.2** — 7 settembre 2026 (**G9 è chiusa**, ed è stata **corta** perché la staff directory
era già in piedi: il provider di G4 e la pagina `/about` che G8 ha seminato la reggevano già tutta.
La fase ha aggiunto `LiveStatusStrip` — terzo dei quattro componenti custom, in polling e senza un
endpoint suo — e ha scritto i test delle tre promesse di design §6.1, uno dei quali ha scoperto che
la garanzia è più forte di come il design la descriveva. Zero tabelle, zero permessi, zero endpoint,
zero meccanismi nuovi; uno slot in `Shell`. La prossima è G10.)

**2.1** — 7 settembre 2026 (**G8 è chiusa**: il sito pubblico esiste e non lo disegna il
codice. Il menu è una tabella — si toglie una voce dal back office e sparisce dal sito, provato in un
browser contro l'API vera — le cinque pagine di sistema sono seminate da template con Lorem tradotto,
ogni dipartimento nasce con la propria dashboard a blocchi, e un grant fa finalmente raggiungere il
dipartimento su cui è dato. Una tabella, due permessi, due endpoint scritti a mano (`sitemap.xml` e
`robots.txt`), un componente che non disegna niente. Quattro deviazioni dalla lettera di questa
pagina sono scritte dentro la fase, e la fase ne ha trovato **tre di difetti**: uno più vecchio di
M1 — nessun form poteva creare una riga contro l'API vera — e due di igiene dei test. La prossima è
G9 o G10, in qualsiasi ordine.)

**2.0** — 6 settembre 2026 (**G6 è chiusa**: il calendario ha la sua UI, `CalendarView` è il
secondo componente custom dei quattro, e una voce proiettata da un modulo non la scrive nessuno.
Zero tabelle, zero permessi, zero endpoint; due estensioni generiche al motore CRUD e al provider.
Una deviazione dalla lettera di questa pagina è scritta dentro la fase, ed è che `ExtraWritePolicy`
**non poteva** fare il lavoro che le era stato assegnato. La prossima è G7.)

**1.9** — **G5 è chiusa**: news e documenti sono due `kind` e non due
tabelle, `cms_categories` esiste, i template li legge tutto lo staff. La fase è stata **corta**,
come §9.3 del piano prometteva: zero entità nuove con un corpo a blocchi, zero editor nuovi, zero
renderer nuovi. Cinque deviazioni dalla lettera del design sono scritte dentro la fase, e due estensioni
generiche sono nate per non aggirare un meccanismo. **Due delle sei iniziali le ha corrette Carmine
lo stesso giorno** — la colonna «file» c'è, come link e non come miniatura; e `/documents/{dept}`
non esiste come indirizzo, i documenti di un dipartimento sono un filtro come su `/news` (design
changelog 1.6). La prossima è G6 — oppure G7, che non dipende da
nulla di quanto resta.)

**1.7** — **G4 è chiusa**: i sei blocchi Data e i loro provider esistono, il registry ne conta 27.

**1.6** — **G3 è chiusa**: i sedici blocchi esistono, la ui-kit ne monta
21, e §16.C del piano si chiude. Quattro deviazioni dalla lettera del design sono scritte dentro la
fase. La prossima è G4.

**1.5** — **G2 è chiusa**: il generatore disegna i cinque tipi che i
blocchi chiederanno, e due deviazioni dalla lettera di questa pagina sono scritte dentro la fase.

**1.4** — **G1 è chiusa**: la media library esiste, e tre estensioni
generiche di `MapCrud` sono nate per non aggirarlo.

**1.3** — due decisioni di Carmine entrano nelle fasi: la **lettura
condivisa dei template** in G5, e la **dashboard di dipartimento** in G8 — quest'ultima con la forma
da confermare, nota in `decisions/2026-09-05-dashboard-di-dipartimento.md`.

**1.2** — G0 è chiusa, PR #35: il giro contro l'API vera gira in CI, e
due cose viste facendone uno stanno in fondo alla fase — una chiede una decisione prima di G5. La
prossima fase è G1.)

**1.1** — le tre decisioni che il piano aveva sollevato sono prese, e stanno nelle fasi che le
riguardano: il parser di header per le dimensioni delle immagini e l'estensione di `CrudOptions` per
non mappare la create in G1, la tabella `hub_notification_preferences` in G7 — quest'ultima ha
corretto una contraddizione dentro il design, che è passato a v1.1.

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
| G0 | Rete e2e con l'API vera in CI — **fatta** | — | `pnpm e2e:full`: crea da template → blocchi → pubblica → anonimo vede il pubblicato, in un browser, contro MariaDB vera |
| G1 | Media library — **fatta** | G0 | upload, servizio dei file dietro il query filter, `MediaPicker`, back-office generato |
| G2 | Le cinque estensioni di `SchemaForm` — **fatta** | G1 | media, icona, data, oggetto tradotto, riordino; debiti n.3 e n.4 chiusi |
| G3 | I 16 blocchi Content / Layout / Interactive / Structure — **fatta** | G2 | 21 blocchi nella ui-kit, convenzioni in `UI-GUIDELINES.md` (chiude piano §16.C) |
| G4 | I 6 blocchi Data e i loro provider — **fatta** | G3 | 27 blocchi; `networkStats` mai congelato; provider dietro il query filter |
| G5 | News, documenti, categorie — **fatta** | G4 | due `kind`, due configurazioni di lista, cinque rotte pubbliche, `cms_categories` |
| G6 | Calendario: CRUD interne, `/calendar`, `CalendarView` — **fatta** | G4 | proiezioni in sola lettura, UTC + fuso divisione, il blocco monta lo stesso componente |
| G7 | Contatti, servizio notifiche, namespace `mail` — **fatta** | G2 | un messaggio genera una mail in Mailpit passando dalla coda |
| G8 | Menu editoriale, pagine di sistema, dashboard di dipartimento, sito pubblico, SEO — **fatta** | G3, G4, G5 | togliere una voce dal menu la toglie dal sito senza ricompilare; `/`, `/start`, `/pilots`, `/atc`, `/about` seedate; ogni dipartimento apre `/staff/{dept}` e trova la propria dashboard |
| G9 | Live status e staff directory — **fatta** | G4 | `LiveStatusStrip`, sezione staff di `/about`, nessun profilo pubblico |
| G10 | Ricerca: schermata, rilevanza, evidenziazione — **fatta** | G5, G8 | `/search` e ⌘K; le tre domande di HANDOFF §10 n.10 hanno una risposta scritta e testata |
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

**Chiusa il 5 settembre 2026** (PR #35), con i tre test verdi in CI accanto ai 355 .NET, ai 79 Vitest
e ai 10 smoke. Il banco è l'applicazione pubblicata; `POST /e2e/signin` è recintato due volte e il
flag fuori dal suo ambiente ferma l'applicazione (`decisions/2026-09-05-ambiente-e2e.md`).

Due cose emerse **facendo** il giro, nessuna corretta nella fase perché fuori perimetro:

1. ⚠️ **I template di sistema li vede solo il dipartimento Web** (`OwnerDepartment = WD` e
   `Content.View` è di dipartimento): per un coordinatore ED «Nuovo da template» non compare affatto.
   Tocca G5, G8 e **G11**, dove l'editor deve leggere il template per mostrare le differenze. Serve
   una decisione prima di G5: `decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`. Nel
   frattempo il banco firma come coordinatore Web, che però raggiunge ogni dipartimento — quindi il
   giro **non** esercita la guardia di dipartimento, che resta di `back-office.spec.ts`.
2. ⚠️ **Pubblicare non dice niente a schermo**: da raccogliere in G11 o nel giro visivo di G12.

E una conferma della regola §A.10: la prima versione del test «una bozza non è visibile» **passava
con la bozza pubblicata apposta**. Rompere la promessa del prodotto e guardare il test è ciò che l'ha
trovato, in trenta secondi.

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

**Chiusa il 5 settembre 2026**, con i sei test di integrazione, i cinque unitari del parser, i sette
Vitest e i due smoke nuovi verdi accanto alle suite di M0 e ai tre del giro pieno. Il racconto è in
`HANDOFF.md` §15; qui restano le tre cose che la fase ha deciso e che le fasi dopo useranno.

1. **Tre estensioni di `MapCrud`, tutte regola (b), tutte in `Core/Data/Crud/` e nessuna che nomini
   la media**: `MapCreate` (una risorsa che non ha una create JSON — era già previsto qui),
   `Delete` (che cosa significa cancellare per questa risorsa: il motore chiama quello invece di
   `Remove` e salva lo stesso, quindi audit, guardia e proiezioni non cambiano) e `CustomFilters`
   (un `filter[nome]` che è una domanda e non un confronto su una colonna). L'ultima chiude di
   sponda il debito di `HANDOFF` §7 sul `filter` che fa una sola uguaglianza.
2. **`Core/Data/JsonQuery.cs`**, accanto a `FullTextSearch`: «questo documento JSON nomina questo
   id?», come funzione mappata sul modello. È ciò che permette di non cancellare un file sotto una
   pagina già pubblicata senza che il server sappia com'è fatto un blocco. ⚠️ Poggia sulla
   convenzione `mediaId` / `mediaIds`, ora scritta in `docs/UI-GUIDELINES.md`: **G3 ci si attiene**.
3. **Il conto**: un endpoint scritto a mano (l'upload, quello previsto), un componente custom
   (`MediaPicker`, quello previsto), tre meccanismi nuovi — le tre estensioni sopra — e nessuna nota
   di decisione, perché le due cose che potevano diventare una (c) erano già decise in questa pagina
   prima che la sessione si aprisse.

Due debiti nuovi, entrambi in `HANDOFF` §15: **nessuno ripulisce i file che nessuna versione nomina
più**, e **il limite di dimensione morde dopo che il corpo è arrivato** (è configurazione del server,
e il posto per guardarlo è il pacchetto di M2).

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

**Chiusa il 5 settembre 2026**, con undici Vitest nuovi e uno smoke che li misura tutti e cinque
nella galleria, accanto alle suite che c'erano. Il racconto è in `HANDOFF.md` §16; qui restano le due
cose che **questa pagina diceva diversamente**, entrambe da conoscere prima di aprire G3.

1. ⚠️ **L'allowlist delle icone sta in `web/src/shared/icons/`, non in `web/src/blocks/icons.ts`.**
   `blocks/` importa già il generatore (per `localized()`), quindi il generatore che importa
   `blocks/` chiuderebbe un ciclo fra i due. La cartella è quella che il design §1.4 aveva già
   previsto per M1 e nasce una volta sola. G3 la legge da lì.
2. ⚠️ **Le icone sono una griglia di radio, non un select.** `SelectItemProps` di Atmosphere ha
   `label?: string`: un select può elencare i nomi e nient'altro, e un nome senza la sua figura è la
   scelta che nessuno può fare. L'insieme resta chiuso, che è ciò di cui parla la regola. Quinto
   contratto di Atmosphere misurato invece che assunto.

E una terza cosa, decisa **smontando** la propria idea: avevo aggiunto una famiglia di chiavi
`groups` per non far collidere il nome di un gruppo con quelli dei suoi figli, poi ho misurato
i18next e ho scoperto che una chiave puntata si risolve in entrambi i modi. La famiglia nuova è stata
tolta e la convenzione resta quella che i test usano da M0: `"seo"` accanto a `"seo.title"`, piatte.
G3, che è pieno di `cards[]` e `items[]`, è il primo a incontrarla davvero.

Il conto della fase: **zero endpoint scritti a mano**, **zero componenti custom nuovi** (`LocaleTabs`
è il guscio che `LocaleFields` e l'oggetto tradotto condividono, interno a `shared/forms`), **cinque
estensioni** del generatore — cioè esattamente quelle che il design §1.6 prevedeva.

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
4. ⚠️ **Due disallineamenti fra §1.4 e il codice di M0, da chiudere qui.** ✅ *Chiusi il 6 set 2026
   come raccomandato qui sotto: gli sfondi sono quattro, le larghezze restano quattro, e il design è
   passato a v1.3.* `web/src/blocks/envelope.ts`
   aveva `BACKGROUNDS = none | muted | accent` (tre; il design ne vuole **quattro**, con `image` +
   `mediaId`) e `WIDTHS = narrow | default | wide | full` (quattro; il design ne nominava **tre**).
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

#### Com'è andata (6 settembre 2026)

Tutti i criteri sono verdi: 176 test Vitest, 259 unit C#, 109 di integrazione, 16 e2e. Il conto della
fase: **zero** endpoint scritti a mano, **zero** componenti custom nuovi, **zero** meccanismi nuovi.

**Quattro deviazioni dalla lettera del design**, tutte scritte anche nel design (v1.3) e nessuna che
allarghi il perimetro.

1. **`table` e `gallery` non hanno liste nude.** Le righe sono `rows[] { cells[] { text L } }` e le
   immagini `images[] { mediaId }`. Il generatore disegna liste di **oggetti**; una lista di valori
   nudi sarebbe stata la sesta estensione a `SchemaForm` per una forma che nessun altro blocco chiede.
   Costo: una chiave in più nel JSON. Beneficio: la chiave resta `mediaId`, che è quella che
   `JsonQuery.UsingMedia` cerca a ogni profondità — una `mediaIds[]` dentro un oggetto non lo sarebbe
   stata.
2. **`alt` non eredita dalla libreria** — nota `decisions/2026-09-06-alt-delle-immagini.md`, decisa da
   Carmine prima di scrivere gli schemi. Vuoto significa immagine decorativa.
3. **Le larghezze restano quattro** (`narrow` incluso), come questa fase raccomandava, e gli sfondi
   diventano quattro con `image` + `mediaId`. La chiave sulla sezione si chiama `mediaId` e non
   `backgroundMediaId` proprio per la ragione del punto 1.
4. **`aspect` di `video` vale `16x9 | 4x3 | 1x1`.** I due punti sono il separatore di namespace di
   i18next: una chiave `options.aspect.16:9` non si risolve, e il campo avrebbe mostrato la chiave.

**Due lacune del generatore che i blocchi sono i primi a toccare**, chiuse estendendolo (regola (b)):

- `blankEntry`: una voce nuova di lista nasceva come `{}`, cioè con campi che React non controlla — e
  undici blocchi su sedici hanno una lista di oggetti. Verificato rompendolo.
- `writtenValues`: una props tradotta **opzionale** lasciata vuota viaggiava come `{ en: "", it: "" }`
  e la pubblicazione la leggeva come traduzione a metà, **rifiutando la pagina**. Ora le props si
  salvano senza i campi opzionali che nessuno ha scritto. ⚠️ La regola sul server non è stata
  toccata: un campo obbligatorio vuoto viene ancora rifiutato, ed è giusto così.

**Una cosa tolta dopo averla misurata**: il blocco `table` aveva un contenitore `overflow-x-auto`
nostro. L'e2e che misura la pagina passava identico togliendolo — perché la tabella di Atmosphere si
avvolge già in `relative w-full overflow-auto`. Era una copia locale di un meccanismo esistente
(`CLAUDE.md` §2), ed è stata rimossa; il test resta, perché la proprietà (la pagina non scorre di
lato) va difesa comunque.

**`BlockDocumentWalker` ha imparato gli sfondi**, che erano l'unico insieme chiuso dell'envelope che
il server non controllava. Coppia da tenere allineata a mano come `Layouts` e `RenderModes`, con il
test di integrazione che posta un valore sconosciuto.

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

### G5 — News, documenti, categorie — **fatta il 6 settembre 2026**

**Obiettivo**: dimostrare che due `kind` non sono due tabelle. Design §3. **Questa fase è corta, o
§9.3 non ha retto** — e se non è corta va scritto nel rapporto di chiusura.

Task:
1. **Prima di tutto, la lettura condivisa dei template** (design §9.4, decisa il 5 set 2026, nota
   `decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`). Senza, un coordinatore che non sia
   del dipartimento Web non vede alcun template e «Nuovo da template» non compare: news e documenti
   nascerebbero solo dalla pagina vuota. Due estensioni **generiche**, nessuna delle quali sa che cosa
   sia un template: un predicato di righe condivise in `CrudOptions` che il motore mette in `OR` con
   il filtro di dipartimento, e la stessa dichiarazione letta dall'**unico** authorization handler
   quando il permesso è di lettura. ⚠️ I due lati devono dire la stessa cosa — uno è SQL, l'altro un
   controllo in memoria: una sola fonte sull'entità e un test che li confronta, come per la coppia
   envelope/walker. La scrittura non si muove: modificare un template resta `Content.ManageTemplates`
   sul dipartimento che lo possiede. **Non** si costruisce qui la copia di un template in un altro
   dipartimento: si scrive quando qualcuno vuole divergere davvero.
2. Tabella `cms_categories` (`Kind`, `OwnerDepartment`, `Key` stabile, `Label` localizzata, `Sort`,
   `IsActive`), `IOwnedByDepartment, IAuditable`, `MapCrud` e back-office `/staff/{dept}/categories`.
   Migrazione additiva. **Seed vuoto**: le categorie le scrivono i coordinatori dall'interfaccia, ed è
   la risposta a piano §15.8 — non serviva sapere quali sono, serviva che non fossero codice.
   ⚠️ `ContentEntry.Category` resta una **stringa** e contiene la `Key`: nessuna FK, e una categoria
   cancellata lascia la riga con la sua chiave.
3. Back-office: `/staff/{dept}/news` e `/staff/{dept}/documents` sono la **stessa** lista di
   `/staff/{dept}/content` con un filtro fisso su `kind` e una configurazione di colonne diversa (news:
   categoria, copertina, pin; documenti: categoria, ordine, file). Il form dei metadati è lo stesso
   `SchemaForm` con qualche campo in più o in meno. ⚠️ Ricetta a **tre** route (HANDOFF §12): layout con
   guardia e `Outlet`, `index` con i search params, dettaglio fratello. Se serve una route scritta a
   mano invece di una configurazione, è un segnale e va scritto.
4. Pubblico: `/news`, `/news/{slug}`, `/documents`, `/documents/{dept}`, `/documents/{slug}` (design
   §3.3). ⚠️ `/documents`, non `/docs`: `ContentEntry.Url` lo scrive già così ed è quello che finisce in
   `search_index`; il piano è stato corretto in v0.36.
5. Un documento con `FileMediaId` è una scheda con il download (la media di G1); senza, si legge nel
   browser come una pagina qualsiasi.

**Accettazione**: `TemplatesAreReadableByAnyStaff` e `TemplatesAreWritableOnlyByTheirDepartment` (la
coppia che tiene onesta l'estensione del task 1, con il caso «coordinatore ED vede i template di WD e
prende 403 se prova a modificarne uno»), `NoSecondContentEntity` (test di architettura: nessuna entità
nuova con un corpo a blocchi), `PublicNewsShowsOnlyPublishedAndVisible`, `PinnedNewsComeFirst`,
`DocumentWithFileOffersDownload`, `CategoryDeletionLeavesTheContentKey`,
`CategoriesAreScopedToDepartment`; e2e con una misura sulla lista `/news`; **e la riga onesta nella
PR**: è servita una colonna nuova non nullable? un secondo editor? un renderer separato? Se sì, §9.3
non ha retto e va detto.

**La riga onesta, scritta**: **no a tutte e tre.** Nessuna colonna nuova su `cms_contents` — le
cinque che news e documenti usano (`category`, `cover_media_id`, `pinned`, `sort`, `file_media_id`)
ci sono da M0 e in G5 sono soltanto **entrate nel contratto**. Nessun secondo editor: `ContentEditor`
riceve `kind` e le categorie del dipartimento, e lo schema decide quali tre dei cinque campi
disegnare. Nessun renderer separato: il pubblico legge news e documenti con lo stesso
`ContentRenderer`, e le liste pubbliche **sono i blocchi Data di G4**. L'unica tabella nuova è
`cms_categories`, che il design contava già fra le sei di §10.2.

**Cinque deviazioni dalla lettera del design**, scritte nei changelog 1.5 e 1.6 del design M1: le
liste del back-office sono tre e non due (`/staff/{dept}/content` è quella delle **pagine**, con
`kind` fisso); le rotte pubbliche dei documenti sono due e non tre, perché
**`/documents/{dept}` non esiste** — un segmento non può essere un dipartimento e una slug insieme,
e i documenti di un dipartimento sono `?department=`, lo stesso filtro che `/news` ha già; il
vocabolario viaggia dentro la risposta del provider di lista; le categorie non hanno permessi propri
(area `Content`); e le tre liste sono **una schermata sola** montata tre volte (`ContentListScreen`,
`ContentFormScreen`, `kinds.ts`).

La colonna «file» dei documenti **c'è** ed è un `col.file`: un link che apre il file, non una
miniatura. Così non ha bisogno di sapere che tipo sia ciò a cui punta — che è la ragione per cui la
prima versione l'aveva tolta — e una riga senza file resta vuota, che è uno stato vero.

**Due estensioni generiche, nate per non aggirare un meccanismo** (regola (b)):

1. **`CrudOptions.Name`** — due risorse nella stessa area di permessi collidevano sul nome
   dell'operazione: `/api/categories` e `/api/content` rispondevano entrambe a `ContentList`, e il
   generatore del client teneva l'ultima letta. Cioè una risorsa che spariva dal client in silenzio,
   non un errore. Il nome nel contratto e l'area dei permessi sono due cose diverse che finora
   coincidevano.
2. **`.meta({ choices })` con etichette a runtime** — `{ value, label }` accanto ai valori nudi che
   già accettava, più la voce «nessuna scelta» che un `z.enum` opzionale aveva e un `text` con
   `choices` no. La categoria di una news è una chiave stabile mostrata con la parola che un
   coordinatore ha scritto in un'altra tabella: né un `z.enum` (l'insieme non è noto a compile time)
   né una chiave i18n (l'etichetta è un dato) potevano portarla. **Non è un sesto tipo di campo.**

E **due** righe in più al vocabolario delle colonne: `col.media`, una miniatura invece del numero
con cui una copertina è salvata, e `col.file`, un link per un allegato di cui la riga non conosce il
tipo. Ognuna è una riga in `columns.ts` e un `case` in `DataList`, che è esattamente ciò che quel
file dice di fare quando serve una cella nuova.

**Due cose viste facendo la fase, entrambe corrette qui.**

- ⚠️ **Un test di regressione passava anche senza la correzione, e sono due volte.** La prima:
  `TemplatesAreWritableOnlyByTheirDepartment` end-to-end resta verde con la regola «solo in lettura»
  cancellata, perché un coordinatore che scrive il template di un altro dipartimento è respinto
  **due volte** — dall'handler e di nuovo dall'interceptor. La rete vera è un test di unità
  sull'handler da solo (`SharedForReadingTests`), e l'end-to-end resta come prova della proprietà.
  La seconda: il test di accoppiamento fra il lato SQL e quello in memoria di `SharedForReading`
  aveva quattro righe scelte a mano, tutte alla visibilità di default, e restava verde mentre una
  seconda metà scritta a mano dissentiva su **ogni template vero** — che è `Visibility.Staff`. Ora è
  il prodotto cartesiano delle proprietà che o l'una o l'altra metà potrebbe guardare.
- ⚠️ **Una `Label` puntava a nulla.** Il filtro pubblico aveva `htmlFor` senza un `id` sul `Select`;
  `Select` di Atmosphere **inoltra `id` al trigger** (misurato nel bundle, non assunto: è il quinto
  contratto di quella libreria che va guardato). Senza, il controllo non ha nome per chi legge con
  uno screen reader — e il test non riusciva a trovarlo, che è come si è visto.

---

### G6 — Calendario: voci interne, `/calendar`, `CalendarView` — **fatta il 6 settembre 2026**

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

**Fatti tutti**, più tre che la fase ha chiesto scrivendola: `TwoEntriesOfOneDepartmentCanBothExist`,
`AWindowAnswersForTheDaysAGridDraws`, e due smoke sulla schermata dello staff. Il Vitest del fuso usa
**Asia/Tokyo** (nove ore di scarto), l'e2e **Europe/Rome** (due): due fusi diversi da UTC e diversi
fra loro, così nessuna delle due reti può passare per coincidenza.

**Una deviazione dalla lettera di questa pagina**, e vale la pena scriverla per esteso perché il
task 2 chiedeva una cosa che non si può fare. ⚠️ **`ExtraWritePolicy` non può impedire una
scrittura**: restituisce il *nome di un permesso*, e non esiste un permesso che significhi «nessuno»
— un superadmin li ha tutti, ed è esattamente chi non deve poter modificare una proiezione, perché
la sua modifica tornerebbe indietro come quella di chiunque altro. Il punto che il task stava
facendo, **«non un handler nuovo»**, è rispettato in pieno: la regola sta nel motore, dove sta già
quella del dipartimento, come `CrudOptions.ReadOnlyRows`. È il gemello di `SharedForReading` di G5.

**La seconda estensione**: il provider del calendario accetta una **finestra esplicita** (`from`,
`to`) accanto al `range` relativo. Una griglia che mostra settembre mostra settembre, non «i
prossimi trentun giorni», e `range` non sa dirlo. ⚠️ Le due props **non stanno nello schema zod del
blocco**: lo schema è ciò che un redattore *salva*, e un corpo inchiodato a un mese sarebbe scaduto
il giorno dopo la pubblicazione. Quello è ciò che una *schermata* chiede.

**Due cose viste facendo la fase.**

- ⚠️ **Il modello aveva un vincolo che nessuno aveva mai incontrato**: `(source_module, source_id)`
  è unico, e nessuno aveva mai creato una voce scritta dallo staff — la prima passa, la seconda va a
  sbattere perché sono entrambe `("core", "")`. La risposta è un identificativo opaco generato alla
  creazione, come il nome su disco di un file della libreria; nessuna migrazione, nessun indice
  toccato. C'è un test che crea due voci nello stesso dipartimento.
- ⚠️ **La finestra e i quadrati erano due conti separati**, e una griglia del mese aperta il 28
  chiedeva l'ultima settimana disegnando vuote le prime tre. Trovato scrivendo il test, non
  guardando: adesso `calendarWindow` legge i giorni che `calendarDays` disegna, e il test le
  confronta su tre giorni diversi del mese.

---

### G7 — Contatti, servizio notifiche, namespace `mail` — **fatta il 6 settembre 2026**

**Obiettivo**: il servizio notifiche del nucleo nasce con **un solo** mittente di intenti, nella forma
che M2 e M3 useranno senza toccarla. Design §5.

**Deciso il 5 settembre 2026 — le preferenze di notifica sono una tabella.** Design §5.2 diceva che in
M1 «esiste la tabella» e §10.2 ne elencava cinque senza contarla: due letture dello stesso capitolo. La
forma è **`hub_notification_preferences`** (`Vid`, `Type`, `Enabled`), non una colonna su `hub_users`,
perché al secondo tipo di notifica la colonna costerebbe una migrazione e la tabella una riga. Il design
è passato a **v1.1** e §10.2 ora dice **sei** tabelle: la sesta non è perimetro nuovo, è la stessa
tabella contata una volta.

⚠️ **Come è andata** (6 set 2026, design M1 v1.8): due decisioni di Carmine e una correzione.
`ISubmittedByMembers` è nata perché la guardia dell'interceptor rifiutava il mittente — che per
definizione non fa parte del dipartimento a cui scrive — e allarga **la sola creazione**;
`hub_users.Email` esiste perché senza indirizzo non c'è coda, e accanto alle persone ci sono le
caselle di dipartimento (`division.json → departmentMailboxes`); i permessi si chiamano
`Contacts.View` e **`Contacts.Edit`**, non `.Manage`, perché la guardia e `MapCrud` chiedono `.Edit`.
Le due note stanno in `decisions/2026-09-06-*.md`.

Task:
1. Tabella `cms_contact_messages` (design §5.1): `OwnerDepartment` **è** il dipartimento destinatario,
   così la coda del back-office e la policy di scrittura escono gratis dall'handler che esiste già.
   ⚠️ Senza `FromVid` e senza `HandledBy`: sono `CreatedBy` e `UpdatedBy`, che l'interceptor scrive.
2. Form dei contatti **solo per autenticati** (`HubPolicies.SignedIn`, piano §9.1): niente mittente da
   verificare, niente captcha, niente spam; il VID è quello della sessione e non un campo. Componente
   custom **`ContactForm`**, già previsto in `UI-GUIDELINES.md` §3.
3. Back-office `/staff/{dept}/contacts`: lista generata, dettaglio in sola lettura, cambio di stato
   (`New | Read | Answered | Closed`). ⚠️ «In sola lettura» è un **tipo**: il payload di scrittura
   porta solo lo stato, così non è una schermata a essere gentile ma il contratto a non avere il
   campo.
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

**Accettazione** (tutti verdi il 6 set 2026): `ContactMessageQueuesOneIntentForTheTargetDepartment`, `NotificationUsesRecipientLocale`,
`NotificationRetriesThenGivesUp`, `NotificationSkippedWhenThePreferenceIsOff`, `ContactFormRefusesAnonymous`,
`NoSmtpOutsideTheNotificationService` (test di architettura: il client SMTP compare in un file solo),
`ForkabilityXxDivision` esteso alle mail; a mano, un messaggio dal form arriva in Mailpit nella lingua
del destinatario.

---

### G8 — Menu editoriale, pagine di sistema, dashboard di dipartimento, sito pubblico, SEO — **fatta il 7 settembre 2026**

⚠️ **Come è andata** (7 set 2026, design M1 v1.11): quattro deviazioni dalla lettera di questa
pagina, tutte scritte nel design, e **tre difetti trovati facendo**.

Le deviazioni. **Chi possiede il sito è una costante sola** (`SiteOwnership`) e arriva alla SPA da
`/api/me`: la fase diceva «gestione in `/staff/wd/menu`», e un file di route con `wd` nel nome
sarebbe stato un codice di dipartimento scritto dentro il client. L'indirizzo è quello, la guardia
lo confronta con ciò che il bootstrap dichiara. **`NavItem` porta anche i figli**, e la navigazione
del bootstrap guadagna lo scope `footer`: la tabella ha due scope da progetto e senza il secondo
metà di essa non sarebbe disegnabile. **La voce fissa `nav.home` non esiste più** — la home è una
riga di menu seminata con la pagina, o il menu non è dati. **Il template della dashboard non porta
blocchi filtrati per dipartimento**: il template è uno e le righe sono nove, quindi un `department`
scritto lì mentirebbe per otto; il filtro lo mette il dipartimento nell'editor, che è la divisione
del lavoro che la nota di decisione descrive.

I difetti. ⚠️ **Nessun form del back office poteva creare una riga contro l'API vera**: mandavano
`rowVersion: ""`, che non è una data. Vale per link, categorie, pagine e menu, viene da M0, ed è
sopravvissuto perché il giro di G0 crea da template e gli smoke stubbano l'API — G8 è la prima fase
che ha spedito un form vuoto al banco. E due di igiene dei test, entrambi in questa fase e entrambi
visibili solo eseguendo la suite intera: un intervallo di VID che apparteneva già a
`CalendarEndToEndTests` (il cui 660002 è un superadmin, quindi due rifiuti smettevano di essere
rifiuti) e una riga di menu che un test lasciava in tabella, nascondendo la voce del modulo `atc` a
ogni classe successiva.

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
6. **La dashboard di dipartimento** (design §14, nota
   `decisions/2026-09-05-dashboard-di-dipartimento.md`). ✅ **Decisa il 6 set 2026: blocchi**, e il
   lavoro è quello che la nota descriveva — un valore in fondo a `ContentKind`, `Url` che per quel
   `kind` è `/staff/{dept}` e non un indirizzo pubblico, un file in `seed/content-pages/` applicato
   **una volta per dipartimento** con la chiave `page.dashboard:<dept>` in `hub_division_settings`, e
   la rotta `/staff/$dept` che oggi non esiste. Nessun permesso nuovo: leggerla è `Content.View` sul
   proprio dipartimento, modificarla `Content.Edit`.
   ⚠️ **Più una correzione senza la quale la visibilità decisa non è vera**
   (`decisions/2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento.md`): la dashboard la vede il proprio dipartimento
   **più quelli a cui il VID è autorizzato**, e oggi un grant non fa raggiungere il dipartimento —
   `HubClaims.BuildIdentity` scrive i claim `dept` solo dalle posizioni staff, quindi la lista esce
   vuota e le righe `Department` restano nascoste. Due righe lì, e i test sulla **lista** che il test
   dei grant di F8 non ha mai fatto. Il resto della nota — autorizzare qualcuno su **un pezzo** di un
   altro dipartimento — **non è di questa fase**: non è un meccanismo ma due regole che vincolano i
   design di M2 e M4 (una capacità delegabile ha un nome suo nel catalogo del modulo, ed è una riga
   sua con la sua area).
7. SEO minima (design §8.4): `<title>` e meta description dalla riga `Seo`, `og:` per pagine e news,
   `sitemap.xml` generata dalle righe pubblicate, `robots.txt`. ⚠️ Entrambi i file vanno in
   `SpaFallbackExclusions`, o la SPA se li mangia. Nessun prerender, nessun prefisso lingua negli URL.

**Accettazione** (tutti verdi il 7 set 2026): `MenuComposesEditorialAndModuleItems`, `MenuIsOwnedByTheWebDepartment` (un
coordinatore di un altro dipartimento → 403), `SystemPagesSeedAppliesOnceAndKeepsStaffEdits`,
`EveryDepartmentIsBornWithADashboard` e `ADashboardIsNotPublic` (una riga `Department` non esce mai
dalla rotta pubblica, che serve solo `kind = Page`),
`SitemapListsOnlyPublishedAndVisible`, `ForkabilityXxDivision` esteso a pagine seedate e menu;
**e2e**: si toglie una voce dal menu e sparisce dal sito **senza ricompilare**, e una misura sulla home
(la colonna di lettura non più stretta di *n* px, il menu che non copre il contenuto).

---

### G9 — Live status e staff directory — **fatta il 7 settembre 2026**

⚠️ **Come è andata** (7 set 2026, design M1 v1.12): la fase è stata **corta**, e per una ragione che
vale la pena scrivere: **tre dei quattro task erano già fatti**. Il provider `staffList` è di G4, la
pagina `/about` che lo monta l'ha seminata G8, e raggruppamento, ordinamento per anzianità, assenza
di dati di contatto e riga onesta c'erano tutti. Quello che G9 doveva davvero era la striscia e i
**test**, che è esattamente ciò che §A.5 intende dicendo che i criteri di accettazione sono test
anche quando il codice li precede.

Due cose trovate facendo. ⚠️ **La garanzia sul roster è più forte del provider**:
`hub_user_staff_positions.vid` è una chiave esterna verso `hub_users`, quindi la posizione di chi non
ha mai fatto login non è scrivibile — la directory non può mostrarlo perché non può esistere. Il test
l'ha scoperto provando a costruire il caso contrario. ⚠️ E **la striscia dentro la colonna di lettura
non è una striscia**: 1120 px in una finestra da 1280. `Shell` ha adesso uno slot `banner` fra header
e contenuto, e una misura in `web/e2e/live-status.spec.ts` fallisce se qualcuno la rimette dentro.

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

**Accettazione** (tutti verdi il 7 set 2026): `StaffDirectoryOrdersByStaffLevel`, `StaffDirectoryExposesNoContactData` (il DTO non
contiene email né altro), `StaffDirectorySaysWhoIsMissing` (Vitest sulla riga onesta),
`LiveStatusDegradesWhenIvaoIsDown` (il client non lancia mai: la striscia mostra l'ultimo dato o niente);
e2e con una misura sulla striscia.

---

### G10 — Ricerca: schermata, rilevanza, evidenziazione — **fatta il 7 settembre 2026**

⚠️ **Come è andata** (7 set 2026, design M1 v1.13): tre scoperte, tutte per aver misurato invece che
supposto, e tutte e tre hanno cambiato il codice.

1. **Il punteggio non si può selezionare come colonna e poi filtrare.** EF Core risponde «the LINQ
   expression could not be translated» a un `Where` su un membro di una proiezione — provato contro
   MariaDB vera, non dedotto. Quello che si scrive una volta è allora **l'espressione**, usata come
   ordinamento e, maggiore di zero, come filtro: il sorgente ha una `MATCH` sola, che era la ragione
   della regola.
2. **«Il più recente per primo» voleva una data che non c'era.** `cms_search_index` guadagna
   `updated_at` — la data della **proiezione**, non della sorgente, perché una proiezione porta ciò
   che ogni riga proiettabile può promettere e una data di pubblicazione non lo è.
3. ⚠️ **`CommandDialog` di Atmosphere non lascia spegnere il filtro di `cmdk`**: inoltra le props al
   dialogo, e avvolge già un comando. Con il filtro acceso la palette butta via i risultati trovati
   nel **corpo** di una pagina — quelli per cui esiste lo snippet. La palette assembla quindi i tre
   pezzi che `CommandDialogRoot` mette insieme, con la proprietà in più.

E un test che non provava niente, trovato rompendolo: la prima versione di quello sullo snippet aveva
un testo **più corto di uno snippet**, quindi tornava intero comunque e passava anche ignorando del
tutto la query.

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

**Accettazione** (tutti verdi il 7 set 2026): `SearchOrdersByRelevanceThenRecency`, `SearchReturnsSnippetPerLocale`,
`SearchTellsWhenEveryTermIsTooShort`, `SearchRespectsVisibility` (esiste da F8 e resta verde); Vitest
dell'evidenziazione, compresi accenti e maiuscole; e2e della palette ⌘K.

---

### G11 — Editor: differenze dal template, dnd-kit, anteprima — **fatta il 7 settembre 2026**

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

**Accettazione** (tutti verdi il 7 set 2026): `templateDiff.test.ts`, dodici casi, il primo dei quali
è `TemplateDiffDetectsAddedRemovedAndChanged`; «allinea» applica **una** differenza e lascia le altre
(`applyDifference`, quattro casi); il riordino da tastiera funziona ancora
(`SectionTree.test.tsx`, che ci arriva tabulando), **verificato rompendolo due volte** — frecce
trasformate in `span role="button"`, e frecce tolte del tutto; e2e `full/template.spec.ts`: si
aggiunge una sezione al template, si apre una pagina che ne è nata, l'editor lo dice, e la pagina
pubblica non è cambiata **né allora né dopo che qualcuno ha accettato la differenza nella bozza**;
più la misura delle tre larghezze dell'anteprima. Anche i due e2e verificati rompendo il prodotto.

**Che cosa è cambiato rispetto al task 1**: il terzo stato non è «cambiata nei vincoli» ma «non
soddisfa più il vincolo di adesso», e **non ha un «allinea»**. Il perché sta in design §9.1 e nel
changelog 1.14 del design: i vincoli di prima non esistono da nessuna parte, ed è voluto.

⚠️ **Resta fuori**: `key`, `required`, `locked` e `allowedBlocks` — i campi su cui poggia tutto
questo — non si scrivono da nessuna schermata. Un template nasce da un seed o da una `PUT`. Non era
nel perimetro di G11 e non ci è entrato; è nei debiti di `HANDOFF.md`.

---

### G11a — Scrivere un template da una schermata — **fatta il 7 settembre 2026**

**Obiettivo**: chiudere il debito che G11 ha lasciato. Nota di decisione
`decisions/2026-09-07-scrivere-un-template.md`, decisa da Carmine il 7 set 2026. Design §1.6, §9.1.

**Perché non era in G11**: fuori dal suo perimetro, e la regola è che un task non si allarga da sé.
Ma senza questa mezza fase §9.1 poggia su quattro campi che nessuno può scrivere, e §9.4 promette a
ogni dipartimento dei template che nessun coordinatore può fare.

Task:

1. **La sesta estensione del generatore**: `.meta({ multi: true, choices })` su un
   `z.array(z.string())`, disegnato come una casella per valore. ⚠️ §12 ne prevedeva cinque: il
   numero cambia e si riporta, non si nasconde. Una lista ripetibile di select per scegliere cinque
   tipi su ventisette sarebbe stata la forma del generatore imposta al problema.
2. **`sectionSettingsSchema` diventa una funzione**, come `contentMetadataSchema` è una funzione di
   `kind`: i quattro campi solo se la riga è un template. ⚠️ Su una pagina il walker ne rifiuta tre
   a priori, quindi disegnarli sarebbe un 400 a ogni salvataggio.
3. **La `key` si scrive una volta sola**: offerta finché è vuota, mostrata dopo, con la riga che
   dice perché. Cambiarla romperebbe in silenzio la corrispondenza con ogni pagina già nata.
4. **`allowedBlocks` vuoto è l'assenza della chiave**, non una lista vuota: vuoto vuol dire
   «qualunque blocco», una lista vuota vorrebbe dire «nessuno».

**Accettazione** (tutti verdi il 7 set 2026): due casi nuovi in `extensions.test.tsx` sul sesto tipo
di campo, compreso che quello che esce è **nell'ordine dell'insieme** e non in quello in cui si è
spuntato; e2e `full/template.spec.ts`, il giro che prima non si poteva fare affatto — si scrive una
sezione di template dall'editor, si salva, si riapre e la chiave è una riga invece che un campo, poi
nasce una pagina da quel template e la sua palette offre **il solo blocco permesso**. Verificato
rompendo il prodotto due volte: la chiave sempre modificabile, e `allowedBlocks` scritto sempre nullo.

---

### G12 — Migrazione a mano, giro visivo, chiusura di M1 — **fatta il 7 settembre 2026**

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

**Stato al 7 set 2026**: i task 1-5 sono fatti. Verdi: **456 .NET** (300 unit + 156 integrazione),
**253 Vitest**, **42 smoke**, **12 del giro pieno**, nessuno skippato. `tools/demo-m1.md` è scritto e
aspetta di essere **eseguito da Carmine**, che è l'accettazione vera; il tag è il task 6 e viene dopo
il merge.

**Quello che la fase ha trovato**, con le note: due difetti seri — il `loader` che non è la riga
(`decisions/2026-09-07-il-loader-non-e-la-riga.md`, undici rotte) e le pagine seminate che violavano
i propri template (corretto, con `seeds.test.ts` a guardia) — più sei rifiniture aperte elencate in
`decisions/2026-09-07-giro-visivo-m1.md` e in HANDOFF §27. ⚠️ Il **back-office a 375 px** è l'unico
pezzo di giro visivo che manca, e manca per uno strumento: nessuno dei due browser a disposizione
sapeva mostrarlo stretto **e** autenticato insieme.

**La correzione al piano 00** che questa chiusura chiede è una sola, ed è in §16: «endpoint scritti a
mano» contava la cosa sbagliata. Da M2 i numeri sono due — CRUD scritti a mano (deve restare zero) e
verbi a mano appesi a un gruppo `MapCrud` (oggi tre, ognuno da giustificare).

---


### G13 — I difetti trovati usando, e le rifiniture che Carmine ha chiesto

**Obiettivo**: chiudere quello che è uscito eseguendo la demo. Nota
`decisions/2026-09-07-dopo-la-demo.md`, che è la fonte di questa fase: contiene i quattro difetti, le
dodici richieste e le quattro decisioni già prese.

⚠️ **Il tag `v0.2.0-m1` non si mette prima della fine dei difetti.** Le richieste possono anche
seguire il tag, se Carmine preferisce; i difetti no.

**I difetti** (l'ordine è quello di priorità):

1. ~~Ogni istante mostrato due ore indietro~~ — **fatto** (`6f8217e`), con
   `InstantsAreUtcOnTheWireTests`.
2. ~~Cancellare lascia la pagina aperta~~ — **fatto** (`fc33848`), sei mutazioni, con
   `features/menu/mutations.test.tsx`. Era una regressione della correzione del loader.
3. ~~Il logout non aggiorna la pagina~~ — **fatto** (`686ee82`), con
   `web/src/features/me/logout.test.tsx`. L'ipotesi era giusta ed è stata misurata prima di
   correggere: il bootstrap è il **contesto del router**, non una query che qualcuno osserva, e
   invalidarla non rifà un `beforeLoad`. `sessionChanged` lo rimuove e chiama `router.invalidate()`;
   lo usa anche la risposta al 401. Uscire porta prima a casa, o la guardia del back-office
   risponderebbe al clic con il login di IVAO.
4. ~~Un documento pubblicato con un'immagine non mostra l'immagine~~ — **fatto** (`0e28db1`), con
   `MediaEndToEndTests.PublishRefusesAPageShowingAPictureItsReadersMayNotSee`. Era la visibilità
   della riga media: un file nasce `Staff`, la pagina usciva lo stesso e il lettore riceveva 404.
   La pubblicazione ora **rifiuta** sotto `VisibilityCeiling` — il corpo, la copertina di una news e
   il file di un documento — e non ripara, perché pubblicare non deve rendere pubblico un file di
   nascosto.

**Le richieste**, nell'ordine che toglie più attrito a chi userà l'hub:

5. ~~Lo `slug` proposto dal titolo e correggibile~~ — **fatto** (`38c6e1c`), con quattro test in
   `shared/forms/extensions.test.tsx`. È l'annotazione `slugFrom` del **generatore di form**, non
   una funzione della schermata dei contenuti: la **settima** estensione, e il numero da riportare
   alla chiusura della fase (§12 ne prevedeva cinque, G11a ha fatto la sesta). Segue il titolo
   finché il campo contiene esattamente quello che è stato proposto, e smette per sempre appena
   qualcuno ci scrive — una riga che aveva già un indirizzo non lo sposta mai.
6. ~~La conferma che l'editor ha fatto quello che è stato cliccato~~ — **fatto** (con la 9): salvare,
   pubblicare ed eliminare rispondono con un toast, e una pubblicazione rifiutata lo dice anche lei
   — la ragione resta in `PublishProblems`, che nomina il blocco e la lingua, ma quella lista sta
   sopra il form e può essere fuori schermo.
7. ~~«Cosa manca per pubblicare», viva e prima del rifiuto~~ — **fatto**, con
   `ContentEndToEndTests.PublishProblemsSayTheSameThingBeforeAnybodyPresses`. Il servizio si è
   diviso in due: `ProblemsAsync` fa i controlli, `PublishAsync` li fa e poi scrive. ⚠️ **Quarto
   verbo a mano appeso al gruppo `MapCrud`** (`GET /api/content/{id}/publish-problems`), deciso con
   Carmine: l'alternativa era il client che ricalcola le regole di pubblicazione, cioè le stesse
   regole scritte due volte. Non c'è più una lista dopo il rifiuto e una prima: è **una**, in tono
   `warning`, e si svuota da sola quando l'ultima cosa è sistemata e salvata.
8. ~~Le **sigle** dei dipartimenti al posto delle nove icone identiche~~ — **fatto** (`6584438`),
   con `web/src/app/layouts/staffDestinations.test.tsx`, e guardata in un browser. ⚠️ Il segno non
   entra nell'elenco chiuso di §8.3: non prende props, si monta solo in uno slot di icona, e nasce
   dai dati.
9. ~~L'**avviso a quattro stati** condiviso~~ — **fatto**: `Notice` più `useNotice()`, riquadro e
   conferma in un angolo che leggono la stessa tabella di quattro toni. È il **quinto componente
   custom** e la riga in §8.3 del piano c'è, insieme a quella di `docs/UI-GUIDELINES.md` §3, alla
   voce in `catalog.ts` e alla sezione della ui-kit — che il test accanto alla galleria pretende.
   ⚠️ Deciso da Carmine: **non** sostituisce `ProblemAlert`, perché unirli tocca ogni schermata del
   back-office ed è una decisione a sé. La forma della conferma — toast e non pannello — è sua.
10. ~~Il calendario: chip colorata per tipo, orario UTC con il locale fra parentesi, e **quattro
    viste**~~ — **fatto**. Le due liste disegnano gli **stessi** giorni della griglia, saltando
    quelli vuoti, con lo stesso `Entry`: non è un secondo calendario. `agenda` resta ma non è più
    fra le viste della schermata — è quella che mostra un **blocco** dentro una pagina. ⚠️ La chip
    è colorata **senza** vocabolario: i cinque tipi che l'entità documenta hanno un colore ciascuno
    e tutto il resto lo deriva dalla parola, finché la 11 non arriva.
11. I **tipi di evento decisi centralmente** e uguali per tutti. ⚠️ Non sono le categorie, che sono
    per dipartimento: serve un vocabolario di divisione, con un permesso di scope diverso da
    `Calendar.Edit`. Da proporre prima di scriverlo.
12. ~~`LiveStatusStrip` con una gerarchia visiva vera~~ — **fatto**, e senza aggiungere niente: il
    numero è la cosa più forte della banda, le parole la più debole, un'icona per figura, e il
    puntino che dice «di questo minuto» respira (solo per chi non ha chiesto meno movimento).
13. ~~Una barra di ricerca nella sidebar dello staff~~ — **fatto**, con la scorciatoia scritta
    sopra. ⚠️ In cima alla colonna del contenuto e **non** dentro la sidebar, che è il componente di
    Atmosphere e non ha slot: avvolgerla in una colonna nostra è ciò che disegnò tutto il
    back-office in 255 pixel (HANDOFF §13).
14. Il giro sull'editor «più intuitivo», **alla fine**, quando il resto è a posto.

**Accettazione**: i quattro difetti hanno un test ciascuno, verificato rompendolo; Carmine rifà
`tools/demo-m1.md` **dal punto 1** e arriva in fondo — compresi i punti 8 e 9, che non ha ancora
eseguito.

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
