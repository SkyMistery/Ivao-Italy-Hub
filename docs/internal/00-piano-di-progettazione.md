# IVAO Division Hub — Piano di progettazione

**Progetto:** nuovo sito/hub della divisione italiana IVAO (sostituisce `it.ivao.aero`), progettato per essere forkabile da altre divisioni.
**Versione documento:** 0.72 — 13 settembre 2026 (**staccarsi da vIPI, centralizzare i contenuti, moduli non subordinati ai dipartimenti**: M5 fuori dalla roadmap, `atc` opzionale, la G14 senza la metà ATC; una schermata per tutto lo staff, le pagine approvate da WD e HQ, i documenti nelle pagine per raccolta, media e link letti da tutti)
**Autore:** Carmine (IT-DIV), con supporto Claude
**Stato:** architettura, catalogo moduli (§9), contratti (§9.7), **meccanismi generici** (§16) e **modello unico dei contenuti** (§9.3) decisi; restano aperte solo le voci di §15 (per lo più informazioni da recuperare). **M0 è chiusa** (F0–F9, tag `v0.1.0-m0`): le fondamenta e la spina dorsale generica di §16 esistono e sono dimostrate end-to-end, come §16.15 chiedeva. **M1 ha design e piano di implementazione** (`03-design-m1.md` e `04-piano-implementazione-m1.md`, 5 set 2026): perimetro, set dei blocchi e convenzioni decisi, tredici fasi G0-G12 più la mezza G11a; **sono chiuse tutte**, e la chiusura è contata in `decisions/2026-09-07-m1-review.md`. Le sezioni marcate ⚠️ richiedono ancora una decisione

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
campo **decide i permessi**: il SOD non tocca gli eventi degli altri, l'ED li tocca tutti perché
tiene il permesso su ogni dipartimento. Si estende **l'unica** `IOwnedByDepartment` a un insieme,
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
  scrittura ordinaria. M2 (iscrizione a un evento) e M4 (richiesta di esame) sono la stessa forma
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
- **Superadmin** (per IT il VID 704798): bypassa **ogni** policy e vede ogni area, modulo e ambiente, per poter verificare l'intero sistema senza dover simulare posizioni. È l'unico ruolo non derivato né concesso.

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
| `calendar_entries` | `id` | `owner_department`, `kind` (event/rfe/training/exam/tour/meeting/deadline/other), `starts_at_utc`, `ends_at_utc`, `all_day`, `visibility`, `source_module`, `source_id`, `url`, `title_i18n`, `description_i18n`, `created_by` | §9.5: **un calendario per tutto**; le voci dei moduli sono proiezioni `IProjectable` (`source_module` + `source_id` univoci, §16.4), quelle interne (riunioni, scadenze) create a mano dallo staff |
| `search_index` | `id` | `source_module`, `source_id`, `kind`, `url`, `owner_department`, `visibility`, `title_i18n`, `text_i18n` (FULLTEXT) | §9.7: proiezione `IProjectable`, stesso meccanismo del calendario |
| `media` | `id` | `owner_department`, `kind` (image/file), `path`, `mime`, `size`, `width`, `height`, `alt_i18n`, `uploaded_by` | storage su disco sotto `data/media/`, limite di upload esplicito (proxy Plesk) |
| `contact_messages` | `id` | `to_department`, `from_vid`, `subject`, `body`, `status`, `handled_by`, `handled_at` | il form pubblico è per autenticati; ogni dipartimento vede i propri |
| `staff_directory` | `position` | `vid`, `sort`, `description_i18n` | dal roster (chi ha fatto login, §16.13) + ordinamento editoriale |
| `links`, `partners`, `faq` | `id` | `owner_department`, campi `*_i18n` | `faq` è anche un blocco Accordion; `links` è l'entità-cavia di M0 (§16.15) |
| `awards`, `award_assignments`, `award_signals` | `id` | catalogo (`name_i18n`, `image_media_id`, `owner_department`, `criteria_i18n`); assegnazioni (`award_id`, `vid`, `reason`, `assigned_by`, `assigned_at`); segnalazioni (`source_module`, `source_id`, `vid`, `reason`, `status`) | §9.1/§9.7: le segnalazioni sono una proiezione `IProjectable` come calendario e ricerca |

Schema `evt_` (modulo Events), `id` surrogata: `events` (tipo: RFE/online day/training/exam/…, `starts_at_utc`, `ends_at_utc`, `airport_icao FK → ref_.ivao_airports`, banner, visibility, `booking_mode`, `title_i18n`, `description_json` a sezioni §9.3), `event_slots` (callsign, dep/arr ICAO, times, aircraft, `booked_by_vid`, stato; indice univoco `(event_id, callsign)`), `event_participants` (PK `(event_id, vid)`), `event_atc_positions` (PK `(event_id, position)`).

Gli schemi degli altri moduli (`fo_` Flight Ops/tour + registro VA, `trn_` Training, `so_` Special Ops) si definiscono nel documento di design di ciascun modulo (vedi §9.2).

Convenzioni MariaDB: `utf8mb4_unicode_ci`, InnoDB, `datetime(6)` UTC, soft delete solo dove serve storicità, indici su ogni FK e sui campi di ricerca, `JSON` nativo MariaDB per i campi flessibili (`value_json`, `before_json`).

---

## 8. Design e architettura dell'informazione

### 8.1 Principi

- **Atmosphere così com'è**: stessa navbar (logo IVAO + divisore + titolo "Italy"), stessi radius, stesse card. La personalità divisionale sta nei contenuti e nelle foto, non nei colori.
- **Due mondi, una navigazione**: area pubblica editoriale (chi siamo, come iniziare, eventi, news) e area riservata operativa (dashboard personale, moduli). Il login non è un muro: le pagine pubbliche sono davvero pubbliche (oggi non lo sono), l'accesso sblocca i servizi.
- **Dashboard personale come home post-login**: "cosa posso fare oggi" — prossimi eventi a cui sono iscritto, richieste training in corso, mie prenotazioni, ATC online in Italia adesso, avvisi staff. La sua forma, insieme a quella della dashboard personale da staffista su `/staff`, la decide la nota di design che **apre M2** (§13, piano 0.59).
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
/staff                     Back-office: entri e vedi SOLO il tuo dipartimento (§9.0); DIR/ADIR/WM vedono tutti. Oggi porta alla dashboard del primo dipartimento; diventa la dashboard personale da staffista, progettata all'apertura di M2 (§13)
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
| Dati di riferimento IVAO | FIR/centri, aeroporti (snapshot giornalieri) | API IVAO `client_credentials` | §7 schema `ref_` |
| **Contenuti a sezioni** | pagine, news e documenti: un solo modello `cms_contents` a sezioni e blocchi, con template, versioni, per dipartimento | media | §9.3; include `/start` (onboarding), `/pilots`, `/about`, la home |
| **News** | articoli con categoria, copertina, pin, RSS; corpo a blocchi | media | ogni dipartimento pubblica le proprie |
| **Documenti** | documenti per dipartimento, di qualsiasi natura, con raccolte, versioni, visibilità per ruolo | media | §9.4; nelle pagine per raccolta |
| **Calendario unico** | tutte le voci: eventi, RFE, training, esami, tour, riunioni staff, meeting di divisione, scadenze | proiezioni `IProjectable` dai moduli (§16.4) | §9.5 |
| Media library | upload immagini/file con alt tradotto, per dipartimento | disco Plesk | limite upload esplicito; dal 13 set 2026 **letta e scelta da tutto lo staff** (`ISharedForReading`), gestita dal proprietario, da WD e HQ e dai grant — lo stesso per i link |
| Contatti | form (solo autenticati) indirizzato a un dipartimento; coda nel back-office | mail | sostituisce il form del sito Blazor |
| **Award** | catalogo award (nome, immagine, dipartimento, criterio) + assegnazioni per VID con motivazione e audit; permesso `Awards.Assign` per dipartimento/grant (in IT: MD e HQ — varia per divisione, quindi è configurazione, non codice) | segnalazioni dai moduli, mail | **mai assegnazione automatica**: i moduli segnalano a chi assegna cosa c'è da assegnare (coda "da verificare"), l'assegnazione è sempre umana (§9.7) |
| Mail | SMTP, template tradotti, coda con retry (Quartz) | SMTP Plesk | infrastruttura, non un modulo |
| Live status | ATC/piloti online in area, FIR online, tile della home | Whazzup SDK | polling, niente SignalR in prima fase |
| Discord | link "collega Discord" + Discord ID in profilo; il bot resta separato | scope `discord` | API interna hub→bot in M6 |
| Staff directory, partner, link, FAQ | dai claim + ordinamento editoriale; contenuti trasversali | — | FAQ riusata dal blocco Accordion |

### 9.2 Moduli di dipartimento (obbligatori, tutti nel monorepo)

Decisione: i moduli 1–3 sono **obbligatori** (erano 1–4 fino al 13 set 2026, quando `atc` è uscito insieme a vIPI) per IT e per chi forka (non hanno flag in `division.json`; hanno solo `maintenance` a caldo). Tutto ciò che si aggiunge dopo è opzionale — il primo è `specialops` (riga 5).

| # | Modulo | Dipartimento | Contenuto | Complessità | Dipendenze | Note e decisioni |
|---|---|---|---|---|---|---|
| 1 | **`events`** | Events (ED) | eventi (divisionali, HQ, RFE, RFO; online/live), slot con prenotazione, partecipanti, posizioni ATC, pubblicazione nel calendario unico | Media-alta | `ref_` aeroporti, API IVAO bookings ATC, mail | Sostituisce `ivao-booking` PHP e il calendario del Blazor. **Nessuna migrazione dello storico**: il vecchio booking resta consultabile in sola lettura per un periodo, poi redirect (§12). |
| 2 | **`flightops`** | Flight Operations (FOD) | tour dell'anno (creabili in anticipo), leg, PIREP con validazione (manuale + **validatore automatico** già scritto in `AutomaticValidatorTour`), classifiche, **segnalazioni di completamento per gli award** (il registro award vive nel nucleo, §9.1/§9.7: FlightOps segnala, chi ha `Awards.Assign` assegna), **registro Virtual Airlines** (nome, logo, link, descrizione — mostrato in `/pilots`), voci nel calendario unico | Alta | API IVAO (voli/tracker), mail, `ref_` aeroporti | Assorbe il progetto `Ivao Italy Toursystem`: il suo design confluisce nel documento di design di questo modulo e il repo separato si chiude. Sistema **divisionale** (i tour HQ restano su tours.th.ivao.aero). |
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
- **Widget di dashboard**: ogni modulo **registra** i propri widget ("le mie prenotazioni", "le mie richieste training", "i miei tour in corso"); `/me` — e in prospettiva le pagine — li compongono liberamente. Stesso principio del registry dei blocchi: più il sito è flessibile, più è general purpose. I blocchi *Data* che dipendono da un modulo (`eventList`…) sono anch'essi registrati dal modulo, non cablati nel nucleo.
- **Notifiche**: servizio unico nel **nucleo** (mail ora, Discord in M6): i moduli pubblicano *intenti* di notifica, mai SMTP diretto — un cambiamento al servizio si fa in un punto solo. Preferenze per tipo di notifica in `/me/profile`.
- **Privacy dei membri**: l'hub **non ha un profilo utente pubblico**. L'unico profilo pubblico è quello ufficiale IVAO (`https://www.ivao.aero/Member.aspx?Id={VID}`): ovunque compaia un membro (classifiche tour, staff directory, partecipanti) si mostra il minimo necessario e si linka lì. Nessuna funzione di export dei dati utente (IVAO non la prevede); per il GDPR ci si allinea alle norme e alla privacy policy IVAO, e ogni modulo documenta nel proprio design cosa conserva di personale e per quanto (così una richiesta di cancellazione ha un percorso noto).
- **Ricerca globale**: indice centrale `search_index` nel **nucleo** (titolo, testo, tipo, url, dipartimento, visibilità), alimentato dai moduli via `IProjectable` con `source_module`+`source_id` — lo stesso pattern del calendario (§16.4). Matching, ranking e UI (⌘K e ricerca pubblica) vivono solo nel nucleo: un fix alla ricerca **non tocca i moduli**; un modulo si limita a dire "indicizza questo".
- **Proiezioni transazionali**: tutto ciò che i moduli proiettano nel nucleo (calendario, indice di ricerca, segnalazioni award) passa da un'unica interfaccia `IProjectable` — l'entità restituisce uno snapshot (titolo per lingua, url, dipartimento, visibilità, intervallo di tempo opzionale, testo per la ricerca, eventuale segnalazione award) e l'interceptor EF del nucleo fa l'upsert con chiave `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente bus di eventi, niente job di riconciliazione: non c'è nulla che possa restare a metà (§16.4). Gli eventi asincroni restano solo per le notifiche.

**Contratto `IModule`** (confermato il 1° set 2026 dopo la verifica sui casi reali qui sotto; firma esatta nel design di M0): un modulo dichiara identità e dipartimento; rotte `/api/<modulo>` e voci di navigazione (pubblica e staff); le proprie migrazioni; il proprio catalogo permessi (`Events.Manage`…); widget di dashboard e blocchi pagina che fornisce; i propri job Quartz; le entità `IProjectable` (calendario, indice di ricerca, segnalazioni award) e gli intenti di notifica che pubblica. Regole dure: un modulo non referenzia **mai** un altro modulo (solo `Core`); il nucleo non referenzia i moduli (riceve solo contributi registrati); la comunicazione tra moduli passa dal nucleo.

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
| **M1 — Sito pubblico** | Nucleo editoriale: pagine a blocchi (**set completo dei blocchi del nucleo**, 22 nuovi), news, documenti per dipartimento con vocabolario delle categorie, calendario unico con UI (con sole voci interne per ora), media library, contatti + servizio notifiche, staff directory, live status; **menu editoriale**; pagine di sistema seedate (`/start`, `/pilots`, `/atc`, `/about`, home); back-office per dipartimento; schermata di ricerca; ~~modulo `atc` come sezione `/atc` con deep link a vIPI~~ `/atc` pagina di sistema e un link ad `atc.it.ivao.aero` (13 set 2026); SEO minima; migrazione contenuti dal Blazor **a mano dall'editor**. Il **giro e2e contro l'API vera** è la prima fase. Design: `03-design-m1.md`; fasi: `04-piano-implementazione-m1.md` (G0–G12). **Aggiunto il 13 set 2026**, prima di M2 e in fasi da scrivere dopo il merge della pila #59–#64: la schermata unica dei contenuti, di link e media; l'approvazione delle pagine; le raccolte; la rimozione della metà ATC della G14 e del modulo `atc` (§9.3, §9.4) | Sostituisce `it.ivao.aero` |
| **M2 — Eventi** | **Prima di tutto, le due dashboard personali** (deciso l'11 set 2026, piano 0.59): una nota di design su `/me` e sulla dashboard da staffista `/staff`, scritta prima di `05-design-m2.md`, perché Events è il primo modulo che registra widget per `/me` e deve trovarne la forma già decisa. Poi **due metà, e si fanno in quest'ordine** (deciso il 9 set 2026). **(a) Il modulo Events**, che parte subito: eventi, slot RFE/RFO, booking, partecipanti, notifiche mail, voci nel calendario unico, blocco Data `eventList`, back-office Events. Nessun import. **(b) Il primo pacchetto self-contained e il deploy su staging Plesk** (foglio `LEGGIMI`), spostato qui da M1 il 5 set 2026: aspetta le risposte A9 (§15.2c) **e** la persona che carica su Plesk, che al 9 set non è disponibile | Spegne `ivao-booking` |
| **M3 — Tour** | Modulo Flight Ops: tour, leg, PIREP, validatore automatico, classifiche, award con mail, voci nel calendario; design ereditato da `Ivao Italy Toursystem` | I tour IT lasciano `tours.th.ivao.aero` |
| **M4 — Training** | Modulo Training: richieste, trainer, disponibilità, sessioni, esiti, mock exam, group training, import storico se possibile | Spegne `training.ivao.it` |
| ~~**M5 — vIPI dentro l'hub**~~ **sospeso il 13 set 2026** | **Fuori dalla roadmap, senza data** (`decisions/2026-09-13-staccarsi-da-vipi.md`): l'hub linka `atc.it.ivao.aero`. Il numero M5 resta libero perché M6 non cambi nome. Testo di prima: allineamento TFM (il ramo **net10 + EF 9 + Pomelo 9** di vIPI, lavoro nel suo repository), montaggio in-process sotto `/services/vsop`, `atc.it.ivao.aero` → redirect, spegnimento di `quickoverview.ivao.it` (già confluito in vIPI). ⚠️ Fino ad allora l'indirizzo è servito **per proxy** dalla vhost che esiste: il lettore vede un sito solo da subito (decisione del 7 set 2026) | Un solo sito ATC+hub |
| **M6 — Ecosistema** | API interne per il bot Discord, iCal, prerender SEO, primi moduli opzionali se richiesti, `FORKING.md` rifinito, prima divisione pilota che forka | Prodotto divisionale |

M5 è sospeso (13 set 2026); nulla in M1–M4 ne dipendeva. L'ordine Events → Tour → Training è deciso (1° set 2026): il tour ha già design e validatore, Training è il modulo più complesso.

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

---

## 15. Decisioni aperte ⚠️

1. ~~Quali moduli inglobare e in che ordine~~ **Deciso il 1° set 2026** (§9, §13): nucleo editoriale + `events`, `flightops`, `training` (`atc` tolto il 13 set 2026); ordine Events → Tour → Training; ~~vIPI montato appena il TFM lo consente~~ vIPI sospeso, raggiunto con un link (13 set 2026); test system sospeso.
2. ~~**vIPI nell'hub — quando e come**~~ **Sospesa il 13 set 2026** (`decisions/2026-09-13-staccarsi-da-vipi.md`): l'hub linka `atc.it.ivao.aero` e non monta né consuma vIPI; la domanda torna solo se Carmine la ripropone. Testo di prima: il montaggio in-process è la destinazione (§9 riga 7b), il nodo è il TFM. Da verificare in vIPI: può il ramo `net10.0` di `Vipi.Infrastructure` usare EF Core 9 + Pomelo 9 invece di EF Core 10 (le 65+ migrazioni sono generate con EF 10 ma applicate anche da EF 8 — con EF 9 dovrebbero passare)? Se sì, si sblocca insieme l'EOL di net8 e il montaggio. Decidere anche il dominio finale della parte ATC (`it.ivao.aero/services/vsop` con redirect da `atc.it.ivao.aero`, o viceversa proxy).
2b. ~~Tour system e test system~~ **Deciso**: il tour system è il modulo `flightops` nel monorepo dell'hub (repo separato chiuso, design confluisce). Il test system è sospeso; se tornerà, sarà app separata (auth estratta in libreria solo allora).
2d. **Storico tour**: importare i leg validati da `tours.th.ivao.aero` per le classifiche, o partire da zero come per gli eventi?
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
2. **Colonne trasversali come interfacce**: `IOwnedByDepartment`, `IVisible`, `IPublishable`, `IAuditable`. Un `SaveChangesInterceptor` compila audit e timestamp; un global query filter applica `visibility` all'utente corrente; **un solo** authorization handler confronta posizioni ∪ grant dell'utente con l'`owner_department` della risorsa. Nessun modulo riscrive «può modificare questa riga?».
3. **Grammatica dei permessi**: `<Area>.<Azione>` (`Content.Edit`, `Content.ManageTemplates`, `Content.Approve` — dal 13 set 2026, Director e Web ovunque più i grant, §9.3 —, `Events.Manage`, `Training.Assign`…) con lo scope di dipartimento **implicito** dalla risorsa. I moduli aggiungono nomi al catalogo, non handler. Fanno eccezione i permessi senza risorsa dipartimentale (`Permissions.Manage`, `Awards.Assign` configurato per divisione).
4. **Proiezioni via `IProjectable`**: calendario, indice di ricerca e segnalazioni award sono proiezioni dello stesso interceptor, upsert con `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente MediatR (licenza commerciale dal 2025), niente bus, niente job di riconciliazione. Eventi asincroni solo per le notifiche.
5. **Un solo documento a sezioni** (§9.3): editor, renderer e registry dei blocchi unici per pagine, news, documenti e per i corpi testuali dei moduli. Schema **solo** in TypeScript/zod; il backend tratta il JSON come opaco (`schema_version` + dimensione), estrae il testo per la ricerca con un walker generico delle stringhe; sanitizzazione markdown/`embed` (allowlist host) in un solo componente.
6. **Un solo motore di back-office**: lista generica su `DataTable` Atmosphere guidata da una configurazione di colonne + form generato dallo schema zod (lo stesso dei blocchi) anche per le entità; lato server un helper `MapCrud<TEntity, TDto>` che porta già la policy di dipartimento. Regola: **valida il server, il client mostra** i `ProblemDetails` campo per campo.
   Quando una risorsa non rientra, si estende `CrudOptions` e mai il motore con un ramo che la nomina: oggi può dire che una riga si scrive solo con un permesso in più (`ExtraWritePolicy`), che non ha una create JSON (`MapCreate`), che cosa significa cancellarla (`Delete`) e che accetta un filtro che è una domanda invece di un confronto su una colonna (`CustomFilters`). **Una schermata CRUD scritta a mano non si accetta**, e un endpoint scritto a mano accanto al motore è un evento da scrivere nel rapporto di chiusura della milestone.
7. **Un solo endpoint di bootstrap** (`/api/me`): menu pubblico e staff, moduli abilitati / in maintenance, permessi effettivi, widget e blocchi registrati. La SPA non ha nulla di cablato.
8. **Un solo set di file di lingua** `locales/{lang}/*.json`, letto sia dalla SPA sia dal backend (mail, errori). Niente `.resx`.

**B. Cose tagliate o accorpate**

9. **Un solo progetto per il nucleo**: `IvaoHub.Core` (dominio + EF + client IVAO + `Content` come cartella) + `IvaoHub.Web` + un progetto per modulo. Niente `IvaoHub.Infrastructure` (interfacce e implementazioni in coppia sono codice doppio per costruzione: Clean Architecture ha senso alla scala di vIPI, non qui), niente `IvaoHub.Auth` finché il test system resta sospeso. Il confine compile-time che conta è tra i moduli e il nucleo.
10. **Niente `/api/v1`**: frontend e backend viaggiano nello stesso pacchetto.
11. **Niente prefisso lingua negli URL e niente prerender SEO**, per ora: le pagine pubbliche sono poche e Google renderizza le SPA (§15.4, §15.6 chiuse).
12. **DbContext per modulo** con tabella `__EFMigrationsHistory_<modulo>` separata (su MariaDB gli «schemi» sono solo prefissi) e **nessuna FK tra contesti**: solo colonne `vid`/`airport_icao` non vincolate.

**C. Convenzioni UI — da trattare nel design di M0, prima della prima schermata** (concordato il 2 set 2026)

Il problema noto (un pezzo nuovo che arriva con un design diverso dal resto della pagina) si risolve prima di tutto **per costruzione**: ogni schermata di back-office passa dal motore lista+form (punto 6) e ogni contenuto dal renderer dei blocchi (punto 5), quindi un design divergente non ha dove entrare. Le convenzioni coprono il residuo. Nel design di M0 si fissano: (a) il **set di icone** unico — **`lucide-react`, confermato** il 2 set 2026: è già una dipendenza di `@ivao/atmosphere-react` 3.1.0 — con la regola «se manca un'icona si cerca prima nel set; se proprio non c'è si aggiunge in `web/src/shared/icons/` nello stesso stile, mai inline nella schermata»; (b) l'**elenco chiuso dei componenti custom** oltre Atmosphere (§8.3): un pezzo nuovo si compone da quelli, non si scrive da zero, e aggiungerne uno è una decisione esplicita; (c) una pagina **`/staff/admin/ui-kit`** che mostra tutti i componenti e i blocchi in uso: riferimento vivo e test visivo quando si aggiunge qualcosa. Le regole finiscono in `docs/UI-GUIDELINES.md` (inglese, valgono anche per chi forka). Le convenzioni **dei blocchi** (spaziature tra sezioni, varianti di sfondo, resa di una sezione `locked` nell'editor) si discutono in **M1**, con il set di blocchi davanti. ✅ **Chiuso il 6 settembre 2026 con G3 di M1**: i 21 blocchi esistono e le convenzioni sono scritte in `docs/UI-GUIDELINES.md`, sezione «The conventions every block follows» — la spaziatura e lo sfondo sono della sezione e mai del blocco, quattro sfondi (`none`, `muted`, `accent`, `image` con `mediaId`) — **sette dall'11 settembre 2026**, con i tre fondi scuri `brand`, `deep`, `dark` disegnati nel tema scuro e ancora nessun colore libero (changelog 0.58), e **otto dal 12 settembre** con `aurora`, mentre `accent` è diventato l'azzurro `ocean-50` invece del quarto grigio che era (changelog 0.67) —, quattro larghezze, l'**accento di un blocco** su un insieme chiuso di quattro famiglie del brand, disegnato sui grafici (un'icona, un filetto, una barretta) e mai sotto una parola, la resa di una sezione `locked`, il blocco sconosciuto visibile solo allo staff, l'icona dichiarata dal tipo, nessuna stringa che non sia prosa dentro `props`, nessun blocco che contiene blocchi, e l'allowlist degli host per i riquadri.

**D. Buchi chiusi**

13. **Roster dello staff** (deciso da Carmine): non esiste un endpoint IVAO per l'elenco delle posizioni di una divisione; il roster dell'hub è **chi ha fatto login almeno una volta**. Staff directory, sospensione dei grant e scelta dei VID a cui proporre un grant si basano su quello.
14. **Perdita di `hub-keys/`**: oltre al logout di tutti, i token in `user_tokens` diventano illeggibili; il codice li tratta come assenti e forza il re-login, mai un'eccezione.
15. **Definizione di «fatto» per M0**: la spina dorsale esiste ed è dimostrata end-to-end su un'entità banale — `links`: localizzata, con dipartimento, visibilità, audit, CRUD via motore lista+form, proiettata nella ricerca — e su un primo contenuto di `cms_contents` creato da un template. Se passa, news e documenti in M1 sono configurazione più che codice.

**E. Come si cambia il sistema mentre si scrive codice** (concordato il 2 set 2026)

Durante il codice emergerà spesso che «serve altro». Il modello regge i cambi in corsa solo se, **prima di scrivere una riga**, la richiesta viene classificata:

- **(a) È un dato o una configurazione** — una sezione o un blocco in un template, un seed, una colonna di lista, una chiave i18n: si fa dentro il task, senza cerimonie.
- **(b) Rientra in un meccanismo generico esistente** — `IProjectable`, `MapCrud`, registry di blocchi/widget, `Localized<T>`, authorization handler: si usa quello. Se il meccanismo non copre il caso al 100 %, **si estende il meccanismo**, mai lo si aggira con un caso speciale.
- **(c) Serve un meccanismo nuovo o una funzione nuova di modulo**: ci si ferma. Nota di design breve (mezza pagina in `docs/internal/decisions/` o un paragrafo nel design del modulo: cosa serve, perché nessun meccanismo esistente basta, cosa si tocca), decisione insieme, poi aggiornamento del piano. Il task originale si chiude senza quella parte o resta aperto: **non si chiude «a qualunque costo»**.

Reti di sicurezza: la **checklist del template PR** («ho aggiunto una tabella `*_translations`, un handler di autorizzazione, un fetch a mano, una lista o un form non generati, un componente UI fuori dall'elenco? Se sì, perché?») e i **test della spina dorsale** di M0, che rompono la build se si bypassa l'interceptor o l'authorization handler. Le regole operative complete, nella forma che Claude Code legge a ogni sessione, stanno in **`CLAUDE.md`** alla radice della cartella di lavoro: è un **file privato di Carmine**, in italiano, escluso dal repository via `.gitignore` (insieme a `CLAUDE.local.md` e `.claude/`) — chi forka non lo riceve; le regole che devono valere anche per i fork vivono nella documentazione pubblica in inglese (`FORKING.md`, `UI-GUIDELINES.md`, template PR).

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
