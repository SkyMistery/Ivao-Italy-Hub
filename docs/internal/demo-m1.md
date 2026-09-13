# Dimostrare M1 — versione italiana

> **Copia di lavoro in italiano di `tools/demo-m1.md`**, che resta la versione ufficiale ed è in
> inglese perché chi forka deve poterla leggere (CLAUDE.md §1). Le due dicono la stessa cosa: se una
> cambia, cambia anche l'altra nello stesso commit. Questa è aggiornata al **12 settembre 2026**:
> contiene G13 con le dodici richieste della prima esecuzione, poi G15 (l'editor che risponde), G14
> (il documento operativo) e il colore.

M0 ha costruito una spina dorsale e l'ha provata su un'entità noiosa. M1 è ciò per cui quella spina
dorsale esisteva: un sito pubblico che nessuno ha dovuto programmare, un editor che un coordinatore
può usare, e schermate che sono configurazione molto più che codice.

Quindi questo giro non è «guarda che funzionalità». È **una domanda sola, fatta nove volte: questa
cosa è una riga o è codice?** Ogni parte finisce con una casella, e le caselle sono gli otto punti
della definizione di fatto del design M1 §0.1.

Ci vogliono una quarantina di minuti. Niente è uno script, di proposito: uno script che passa ti dice
che passa lo script.

> Ogni parte nomina il test automatico che asserisce la stessa proprietà. Se un passo qui fallisce e
> il suo test passa, quella differenza è la cosa più interessante della stanza.

---

## Che cosa serve

| Strumento | Versione |
| --- | --- |
| .NET SDK | 10.0.x |
| Node.js | 22 LTS o più recente |
| pnpm | 10.x |
| Docker | una versione recente qualunque |
| Un client OAuth IVAO | URL di login e redirect registrati per `http://localhost:5173` |

```bash
docker compose up -d
```

```bash
dotnet run --project src/IvaoHub.Web
```

```bash
cd web && pnpm dev
```

Il primo comando alza MariaDB 11.4.10 e Mailpit; il secondo l'API su `:5000`; il terzo la SPA su
`:5173`. `config/ivao-oauth.json` deve esistere ed essere completo, o l'applicazione si rifiuta di
partire — è voluto.

Fai il login su <http://localhost:5173> **prima della parte 2**. Tutto quello che viene prima è
quello che vede un visitatore.

---

## Parte 1 — Il sito pubblico è fatto di righe

Apri `/`, `/start`, `/pilots`, `/atc`, `/about` da visitatore. Cinque pagine, e **nessun componente
ne disegna nessuna**: sono righe di `cms_contents`, seminate da template in
`seed/content-pages/*.json`, con un corpo fatto di blocchi.

Poi il menu, che di solito è la parte che è codice:

1. vai su `/staff/wd/menu` e cancella la voce **Pilots**;
2. ricarica il sito pubblico. È sparita dall'intestazione, senza ricompilare niente;
3. rimettila.

⚠️ Il menu è una tabella **del dipartimento web**, non una schermata che ha ogni dipartimento. Prova
`/staff/ed/menu`: non c'è.

**Nuovo in G13, ed è la regola più stretta che questo prodotto abbia preso su un campo.** Nella
tabella, **Order** e **Visible to** si scrivono nella cella — niente form, e se il server rifiuta la
cella torna com'era. E aprendo una voce, l'**Address** è un elenco **chiuso**:

- le pagine del sito, divise per dipartimento che le ha scritte, **anche le bozze** (una bozza lo
  dice): scrivi la voce adesso, l'accendi quando la pagina esce;
- le schermate dell'applicazione, che sono rotte e non righe;
- i link della libreria, **quelli in uso**.

Scrivi qualcosa che non è uno di questi e sparisce appena lasci il campo, e anche il server lo
rifiuta — il campo è una comodità, la regola è del server. **Il punto non è il menu**: è che ogni
indirizzo che esce da questo sito vive in una tabella sola, quindi spostare il forum è una riga di
`/staff/wd/links` e il menu la segue.

- [ ] **Punto 1** — il sito pubblico esiste e non lo disegna il codice, e una voce di menu non può
      puntare da nessuna parte che il sito non possieda.
      Asserito da `e2e/full/menu.spec.ts`, `e2e/back-office.spec.ts` e
      `SiteMenuAndDashboardTests.AMenuEntryOnlyLeadsWhereTheSiteOwnsSomething`.

---

## Parte 2 — News e documenti sono un'entità sola con due `kind`

Apri `/staff/wd/news` e `/staff/wd/documents`. Due liste, due form, due sezioni pubbliche — e **una**
tabella, un editor, un renderer, una via di pubblicazione, una proiezione.

La prova sta nella forma più che nelle schermate: `features/content/kinds.ts` è ciò che rende una
news una news, ed è un oggetto di configurazione. Scrivi una news, pubblicala e leggila su `/news` e
`/news/{slug}`; scrivi un documento con un file e leggi `/documents` e `/documents/wd`.

Se una delle due avesse richiesto una colonna non nullable nuova o un secondo editor, il design §9.3
non avrebbe tenuto e il rapporto di chiusura dovrebbe dirlo. Ha tenuto.

**Novità di G13 che vedi qui**: l'indirizzo (`slug`) te lo **propone il sistema** dal titolo finché
non lo scrivi tu, e salvare ora **risponde** — un toast in un angolo. Una riga che ha già un
indirizzo non lo sposta mai.

**Novità di G14: il documento operativo, che è la cosa più pesante che questa affermazione abbia
dovuto reggere.** Su `/staff/aod/documents` creane uno e dagli un **tipo** — SOP o LoA — un aeroporto
e una FIR **scelti da un elenco** (il validatore rifiuta un ICAO che non sta nello spazio aereo della
divisione), le posizioni di cui parla, il giorno in cui entra in vigore e quello entro cui va
rivisto. Pubblicalo e leggilo: una striscia sotto il titolo dice di che cosa parla, un avviso in cima
compare quando non è più in vigore e nomina chi lo sostituisce, e **Stampa** lo mette su carta con
schede e fisarmoniche aperte. ⚠️ Poi guarda quello che **non** è: nessuna tabella nuova, nessuna
entità nuova, nessun secondo editor, nessun terzo `kind`. Sei colonne nullable, due blocchi e un piè
di pagina — e `NoSecondContentEntity` è ancora verde, che è il test che avrebbe detto il contrario.

- [ ] **Punto 2** — due `kind`, non due tabelle.

---

## Parte 3 — Il set dei blocchi, e la galleria che si costruisce da sé

Apri `/staff/admin/ui-kit`. Ogni blocco che il registry dichiara è lì, disegnato dalla registrazione
stessa: nessuno aggiunge una sezione a quella pagina quando si aggiunge un blocco. Contali — **29**,
di cui **7** sono blocchi Data, che chiedono al server il proprio contenuto, e due sono quelli di G14:
la tabella delle frequenze e la tabella di coordinamento di un documento operativo.

Nella stessa pagina, in fondo ai componenti, c'è la novità di G13: **`Notice`**, l'avviso a quattro
stati (errore, avviso, successo, informazione) nelle sue due forme — il riquadro che resta e la
conferma che compare in un angolo e se ne va. È il **quinto** componente dell'elenco chiuso.

Poi leggi `docs/UI-GUIDELINES.md`. Le convenzioni dei blocchi sono decise e scritte lì: la spaziatura
e lo sfondo sono della **sezione** e mai del blocco, **otto** sfondi, quattro larghezze, nessun blocco
contiene blocchi, e la prosa di un blocco è quella tradotta.

⚠️ Quest'ultima era una regola da ricordare — «nessuna stringa dentro `props` che non sia prosa» — ed
era rotta da un blocco del primo set, che il giro visivo aveva trovato
(`decisions/2026-09-07-giro-visivo-m1.md`). Dal 9 settembre è una proprietà del meccanismo:
l'estrattore tiene una stringa solo se ci è arrivato attraverso una mappa `Localized`. Il corollario è
il prezzo, ed è scritto lì accanto: un nome cercabile che nessuno traduce non si trova.

- [ ] **Punto 3** — il set dei blocchi di §1, ogni blocco in galleria, convenzioni scritte.

---

## Parte 4 — Un calendario solo, con la sua schermata

`/calendar` da visitatore: i filtri stanno nell'indirizzo, quindi una vista filtrata è un link che
puoi mandare. Poi `/staff/wd/calendar`, dove lo staff scrive le voci del proprio dipartimento, e un
blocco `calendar` messo in una pagina qualunque mostra le stesse voci.

Il punto è che il calendario è **uno**: una voce scritta da un modulo e una scritta a mano sono la
stessa riga, perché i moduli ci proiettano dentro invece di tenersene uno.

**Novità di G13, tutte da guardare qui:**

- **quattro viste** invece di tre: settimana e mese come griglia, settimana e mese **come lista** —
  la lista salta i giorni vuoti, che è tutta la differenza;
- l'ora è **UTC** e, **fra parentesi**, quella della divisione;
- ogni voce ha una **chip colorata** per tipo;
- e i **tipi sono un vocabolario di divisione**: `/staff/admin/calendar-kinds`. Li decide chi ha
  `Calendar.ManageKinds` — direttore, vice, WM, AWM — e sono gli stessi per tutti i dipartimenti.
  ⚠️ Scrivendo una voce il tipo si **sceglie da un elenco**: non è più testo libero, e una parola che
  non è nel vocabolario viene rifiutata dal server.

Prova anche a **ritirare** un tipo (togli «in uso»): le voci già scritte con quello restano come
sono — non c'è nessuna foreign key, di proposito — ma nessuno può più archiviarci dentro.

- [ ] **Punto 4** — il calendario unico ha la sua UI.

---

## Parte 5 — Media, contatti, la directory dello staff, lo stato della rete

- **Media** (`/staff/wd/media`): carica un'immagine. Usala in un `hero`, in una `gallery` e come
  copertina di una news. Un file, tre usi, e la finestra di cancellazione ti dice dove è usato
  *prima* che tu prema qualcosa.
  ⚠️ **Novità di G13, e la incontri subito**: un file nasce visibile **allo staff**. Se provi a
  pubblicare una pagina pubblica che lo mostra, la pubblicazione **rifiuta** e ti dice quale
  immagine — prima non diceva niente e il visitatore vedeva un'immagine rotta. Rendila pubblica
  nella libreria e ripubblica.
- **Contatti**: `/contact` — è una pagina da **membro**, perché un messaggio porta il VID di chi lo
  ha scritto. Mandane uno a un dipartimento, poi leggilo su `/staff/wd/contacts` e leggi la mail che
  Mailpit ha catturato su <http://localhost:8025>. ⚠️ È arrivata dal servizio notifiche del nucleo;
  nessun modulo parla SMTP.
- **La directory dello staff**: su `/about`. Elenca chi ha fatto login almeno una volta — che non è
  una query ma una **foreign key**: la posizione di qualcuno che non ha mai aperto l'hub non si può
  scrivere.
- **Lo stato della rete**: la striscia sopra ogni pagina pubblica. Se la rete non si può interrogare
  non disegna **niente**, perché quattro zeri sarebbero il sito che risponde a una domanda che non ha
  fatto.
  ⚠️ **Novità di G13**: la striscia ha una gerarchia vera — il numero è la cosa più forte, le parole
  la più debole, un'icona per figura, e il puntino che dice «di questo minuto» respira.

- [ ] **Punto 5** — media, contatti, directory e stato della rete funzionano.

---

## Parte 6 — La ricerca

`/search?q=` da visitatore: la domanda è l'indirizzo, le parole cercate tornano evidenziate, e trovi
solo quello che potresti aprire — lo stesso query filter, non una seconda regola.

Poi fai il login e premi **⌘K / Ctrl-K** in qualunque punto del back-office: le stesse righe più le
schermate del back-office. Le due liste vengono da `staffDestinations`, quindi una schermata non può
essere raggiungibile da una e non dall'altra.

**Novità di G13**: in cima alla colonna del back-office c'è una **barra di ricerca visibile**, con la
scorciatoia scritta sopra — apre la stessa palette, non è una seconda ricerca.

Cerca una parola di due lettere. **Dice** che le parole erano troppo corte invece di rispondere con
il nulla, che era una delle tre domande lasciate aperte da M0.

- [ ] **Punto 6** — la ricerca ha una schermata, e le tre domande hanno una risposta scritta.

---

## Parte 7 — L'editor, e quello che il template continua a dire

È la parte per cui M1 esiste, ed è quella che è cambiata di più dopo la prima esecuzione di questa
scheda: G15 ha rifatto il modo in cui risponde, G14 gli ha dato una finestra di pubblicazione, e il
colore del 12 settembre gli arriva attraverso lo stesso renderer del sito pubblico. Su una pagina
qualunque di `/staff/wd/content`:

1. **Spostare le cose.** Una sezione si sposta in tre modi: trascinata dalla maniglia nell'outline,
   con le frecce accanto, e **dalla pagina stessa** — sceglila e porta una targhetta con i comandi che
   il suo template permette. Un blocco fa lo stesso e si può lasciare su **qualunque slot della
   pagina, anche di un'altra sezione**; un componente trascinato dalla barra atterra **fra** due
   blocchi invece che in fondo. Le frecce, e il selettore «Sezione» nelle proprietà di un blocco, sono
   la strada da tastiera, e non sono decorazione: sono tutto ciò che funziona senza mouse.
   ⚠️ **Annulla e Ripeti**, cinquanta passi, `Ctrl/⌘+Z`, `Ctrl/⌘+Shift+Z` e `Ctrl+Y` — e una frase
   scritta dentro un blocco è **un** passo da annullare invece di venti, perché modifiche consecutive
   alla stessa cosa si accorpano. Le scorciatoie non fanno niente **dentro** un campo, di proposito:
   lì ⌘Z vuol dire ancora «annulla quello che ho appena scritto», che è mestiere del browser e non
   nostro.
2. **Le proprietà si applicano mentre scrivi.** Non c'è più nessun pulsante «Applica»: scrivi, e la
   pagina si ridisegna un quinto di secondo dopo che ti fermi — misurato a ~180 ms. Un valore che lo
   schema rifiuta non cambia niente e lo dice nel suo campo, così un titolo svuotato a metà di una
   riscrittura non svuota il blocco. L'unico pulsante rimasto sotto un form è **«Fissa la chiave»** su
   una sezione di template, perché una chiave si fissa una volta sola e scrivendo «in» diventerebbe la
   chiave di «intro».
3. **Non si perde niente.** La bozza si salva da sola **dieci secondi** dopo che ti fermi, e di nuovo
   quando lasci la pagina; la riga accanto ai pulsanti dice «Salvato alle 14:32» / «Salvataggio…» /
   «Modifiche non salvate». «Pubblica» prima svuota il salvataggio in sospeso, così resta un gesto
   solo. Una riga che non esiste ancora non la crea mai un autosalvataggio — la crea solo «Salva
   bozza» — e un 409 lo ferma e dice perché: qualcun altro ha salvato quella riga, e la strada giusta
   è ricaricare, non sovrascrivere ogni dieci secondi. ⚠️ Quello che costa al database condiviso è la
   cosa da guardare: un autosalvataggio lascia una riga di audit `autosaved` con l'elenco dei campi
   mossi e **senza corpo** — circa 200 byte invece di due copie della pagina.
4. **L'anteprima dice la verità.** Tre larghezze, una lingua, e Bozza | Pubblicato. È un `max-width`
   sullo stesso identico renderer del sito pubblico — non un emulatore — e a «Phone» una sezione a due
   colonne diventa davvero **una** colonna, perché ogni larghezza che decide un layout sotto `blocks/`
   è una container query sulla pagina e non una media query sulla finestra. Prima di G15 disegnava due
   colonne da 167 px dentro una cornice larga 390, e il test che misurava la cornice passava mentre
   l'anteprima mentiva.
5. **Una sezione bloccata** mostra i suoi campi e non la sua struttura, con una riga che dice **quale
   template** la fissa e chi può cambiarlo. La barra sopra dice la stessa cosa invece di promettere un
   impossibile «Aggiunge a: Welcome» sopra pulsanti disabilitati a ragione.
6. **Le differenze dal template.** Apri un template — `/staff/wd/templates` — aggiungici una sezione,
   poi riapri una pagina nata da quel template: l'editor dice che una sezione è stata aggiunta e si
   offre di aggiungerla — **una differenza alla volta**, mai tutte insieme. ⚠️ E la pagina che legge
   un visitatore non è cambiata, e non cambia nemmeno dopo che hai accettato la differenza nella
   bozza. Solo la pubblicazione muove quello che vede il pubblico.
7. **Scrivere un template.** Su una riga template una sezione ha quattro campi in più — `key`, se le
   pagine possono cancellarla, se possono ristrutturarla, e quali blocchi ammette. Spunta un tipo di
   blocco, salva, fai una pagina da quel template: la sua palette offre quel blocco e nessun altro. Un
   template si crea **da un pulsante** su quella schermata — scegli per che cosa vale, e l'editor si
   apre su una riga che è già un template. Aprendone uno esistente ti dice **quante righe sono nate da
   lui**, che è la frase che ferma una modifica distratta. La schermata è dietro
   `Content.ManageTemplates`: ogni membro dello staff *legge* i template, così «nuovo da template»
   funziona fra dipartimenti, e la vede solo chi può cambiarli.
8. **La pubblicazione chiede** (nuovo in G14). «Pubblica» apre una finestra con **che cosa è
   cambiato** — la riga di changelog che ogni versione poteva portare da M0 e non aveva mai avuto una
   casella — e, su un documento, il **ciclo AIRAC** a cui appartiene. È `ConfirmDialog`, esteso con dei
   campi invece che scritto accanto come quinto dialogo suo.

**Le comodità, aggiunte fra il 10 e il 12 settembre mentre Carmine lo usava:** doppio clic su un
blocco o una sezione per sceglierlo e finire nel primo campo del pannello; `Canc` elimina, `⌘D`
duplica, `Esc` lascia; l'oggetto scelto viene portato in vista; una sezione si duplica da sé,
identificatori nuovi compresi; le sezioni si annidano fino a **quattro** livelli; un blocco in cui non
è scritto niente disegna un segnaposto tratteggiato col suo nome, che un visitatore non vede mai; la
barra dei componenti ha un campo di ricerca e i pannelli laterali barre di scorrimento sottili; il
selettore di file carica nella libreria del dipartimento e ci porta; l'anteprima segue la lingua che
hai scelto; e due immagini identiche caricate due volte sono **un** file, con la risposta che è la
riga che c'era già.

**E il colore, 12 settembre.** Una sezione sta su uno di **otto** fondi: `accent` è l'azzurro del
marchio invece del quarto grigio che era, e `aurora` è un quarto fondo scuro accanto a `brand`, `deep`
e `dark`. `hero`, `cardGrid`, `iconGrid` e `timeline` prendono uno di quattro accenti del marchio,
disegnato su un'icona, sul filetto sopra una scheda, sulla barretta sopra un hero — e mai sotto una
parola, perché un grafico deve stare a 3 : 1 dal suo fondo e una parola a 4,5 : 1. E i titoli non sono
più il grigio pallido che Atmosphere gli dà.

- [ ] **Punto 7a** — un template non riscrive mai una pagina da solo, e l'editor lo dice.
      Asserito da `e2e/full/template.spec.ts`.

---

## Parte 8 — Il giro, e l'anteprima contro la pagina pubblica

Quello che M0 non poteva spuntare, due volte.

1. Crea una pagina da un template, aggiungi un titolo, un testo e un callout, e **premi Anteprima**.
   Tienila lì.
2. Mettila su Pubblica, salva, pubblica.
3. Apri `/{slug}` in una **finestra privata**, da non loggato.

Confronta le due. Sono la stessa pagina perché sono lo stesso renderer — è tutto il motivo per cui
l'editor non ha un'anteprima sua. Poi:

4. Torna nell'editor, cambia il callout e salva **senza pubblicare**.
5. Ricarica la finestra privata. Non si è mossa.

Quest'ultimo passo è il senso di tutta la milestone: il pubblico legge la versione pubblicata, e una
bozza è privata finché qualcuno non decide altrimenti.

⚠️ Due trappole già note: il badge dei blocchi Data lo vede **solo lo staff**, quindi in finestra
anonima non c'è ed è giusto; e se modifichi la bozza dopo aver pubblicato, le due **devono**
divergere.

- [ ] **Punto 7b** — il giro è stato eseguito in un browser contro l'API vera, e l'anteprima
      dell'editor **è** la pagina pubblica. Asserito da `e2e/full/round.spec.ts`.

---

## Parte 9 — E tutto quanto, asserito

```bash
dotnet build IvaoHub.sln
```

```bash
dotnet test --solution IvaoHub.sln --configuration Release
```

```bash
cd web && pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
```

```bash
cd web && pnpm gen:api && git diff --exit-code
```

```bash
cd web && pnpm e2e
```

```bash
cd web && pnpm e2e:full
```

Aspettati **471 test .NET** (306 unit, 165 di integrazione contro una MariaDB 11.4.10 vera), **279
Vitest**, **52 smoke Playwright** e **12 del giro pieno**. Nessuno skippato.

⚠️ **Correzione:** `dotnet test --solution` su questa macchina dice «Zero tests ran» con uscita 5
**in tutte e due le configurazioni** — Release non lo evita, come questa scheda diceva prima — mentre
gli stessi binari passano tutto eseguiti a mano:
`tests/IvaoHub.UnitTests/bin/<config>/net10.0/IvaoHub.UnitTests.exe` e quello di integrazione
accanto, che accettano `-class <NomeCompleto>` per eseguire una classe sola. Se dice zero, esegui i binari
prima di credere che qualcosa sia rotto.

⚠️ **Ferma l'API prima di compilare.** Con `dotnet run` in esecuzione MSBuild fallisce con
`MSB3027`/`MSB3021` — DLL bloccate — e **senza nessun errore `CS`: un `grep "error CS"` non se ne
accorge.** Si guarda `Error(s)` nel riepilogo.

`pnpm gen:api && git diff --exit-code` è il controllo del contratto: il client generato non deve
muoversi. Se si muove, il server ha cambiato la sua API e il client non se n'era accorto.

- [ ] **Punto 8** — i test della spina dorsale passano ancora, la divisione fittizia XX passa con il
      sito pubblico completo, e `pnpm i18n:check` è verde.

---

## La definizione di fatto, in un posto solo

Design M1 §0.1, spuntata contro le parti qui sopra.

| # | | Si vede in |
| --- | --- | --- |
| 1 | Il sito pubblico esiste e non lo disegna il codice; il menu è una tabella e togliere una voce la toglie dal sito | Parte 1 |
| 2 | News e documenti sono due `kind` di un'entità sola, non due tabelle | Parte 2 |
| 3 | Il set dei blocchi di §1, ogni blocco in `/staff/admin/ui-kit`, convenzioni in `docs/UI-GUIDELINES.md` | Parte 3 |
| 4 | Un calendario con la sua UI: pubblico, interno, e come blocco | Parte 4 |
| 5 | Media, contatti con il servizio notifiche del nucleo, directory dello staff, stato della rete | Parte 5 |
| 6 | La ricerca ha una schermata, e le tre domande lasciate aperte da M0 hanno una risposta | Parte 6 |
| 7 | Il giro in un browser contro l'API vera, e l'anteprima dell'editor **è** la pagina pubblica | Parti 7 e 8 |
| 8 | I test della spina dorsale di M0 passano tutti, la divisione XX passa con il sito completo, `i18n:check` verde con `mail` | Parte 9 |

Se ogni casella è spuntata, M1 è fatta. Che cosa vuol dire vale la pena dirlo chiaro: da qui in poi
un modulo nuovo deve essere **schermate fatte di configurazione** — uno schema, un elenco di colonne,
il nome di un permesso e una registrazione — e il rapporto di chiusura
(`decisions/2026-09-07-m1-review.md`) è dove quella affermazione si controlla contro i numeri invece
di essere asserita.
