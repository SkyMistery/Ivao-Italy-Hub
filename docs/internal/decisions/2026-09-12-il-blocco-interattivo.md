# Il blocco interattivo: un'animazione scritta da Claude, servita in un frame che non può fare niente

**Data:** 12 settembre 2026 — Carmine: «è possibile aggiungere una sezione che permetta di inserire
codice che realizza animazioni in un documento?», con il caso d'uso scritto per intero: creo un
documento, scarico le linee guida, le do a Claude Code — «realizza un'animazione che mostri una pista
09/27 e un traffico VFR in circuito sinistro» —, incollo il codice, e chi apre il documento vede
l'animazione.

**Stato:** **deciso da Carmine il 12 settembre**, sulle tre domande che gli ho messo davanti:
**(C)** un endpoint che serve il frame, non un `srcdoc`; in **stampa** la sezione si chiude e non
occupa spazio («un banner non serve a nulla»); **permesso dedicato**. Piano 0.70. Viene **dopo**
`2026-09-12-gli-header-di-sicurezza.md`, che è il pezzo su cui poggia. **Costruito per intero lo
stesso giorno** (PR #64), e **la sera usato davvero** da Carmine con un altro agente: quello che è
emerso è in fondo, «Usato davvero», e nel piano 0.71.

## I due casi d'uso, che sono il perimetro

1. Si legge di una situazione e **sotto la si vede** — un'animazione che spiega.
2. Si legge di una situazione e sotto **si vede che cosa succede secondo le scelte** che si possono
   fare — un widget con dei pulsanti.

⚠️ **E il confine, che è la parte da non dimenticare:** il widget è interattivo **dentro la sua
scatola**. Non cambia il testo del documento intorno, non ricorda che cosa hai scelto, non ha un
indirizzo che si possa mandare a qualcuno («guarda questa con la scelta B»). Il giorno che servisse
«scegli qui e il paragrafo sotto cambia», è un'altra decisione — e il primo sospetto sarà che quella
cosa vada fatta come un **blocco vero** con le sue proprietà, non come codice incollato.

## Perché non è (b)

Nessun meccanismo esistente porta codice: `props` è opaco al server per disegno, il renderer disegna
componenti registrati, e l'unico posto dove oggi entra HTML altrui è l'`embed`, che incornicia un
**indirizzo** di un'allowlist. Qui l'HTML lo scrive un redattore. È (c), ed è la ragione di questa
nota.

## La forma, in cinque decisioni

### 1. Il codice sta nell'**envelope**, non in `props`

Scegliendo (C) il server deve leggere il codice per servirlo — e `props` il server non lo guarda mai
(piano §16.5). Quindi il codice **non è una proprietà del blocco**: è un campo dell'envelope, accanto
a `renderMode` e `frozen`, che sono già campi che il server conosce su qualunque blocco. Si chiama
`source`. Lo schema zod del blocco tiene titolo, descrizione e altezza; il codice no.

Il walker lo controlla come controlla il resto dell'envelope, **senza sapere che cosa sia**: è una
stringa, ha un tetto di **64 KB** per blocco e **256 KB** per corpo (che ne ammette 1 MB in tutto), e
non vale niente su un blocco che non lo dichiara.

### 2. Un endpoint solo, due politiche di cache

`GET /embed/{contenuto}/{versione}/{blocco}` — `{versione}` è l'id di una versione pubblicata oppure
la parola `draft`.

- **pubblicata**: immutabile per costruzione (una versione non cambia mai), quindi
  `Cache-Control: public, max-age=31536000, immutable` e Cloudflare la serve da sé. Anonima come la
  pagina che la incornicia.
- **`draft`**: `no-store`, e dietro **l'unico authorization handler** — la stessa domanda «puoi
  leggere questa riga?» che risponde per l'editor. ⚠️ Il rischio vero di tutto il pezzo è qui: che
  qualcuno scriva un controllo a mano invece di chiamare quello. Un test lo prova come utente
  sbagliato e si aspetta un 403.

La risposta è un documento HTML con i **suoi** header, che è tutto il motivo per cui si è scelto (C):
`Content-Security-Policy: default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src
'unsafe-inline'; sandbox allow-scripts`, e `frame-ancestors 'self'` al posto del `'none'` della
pagina. Niente rete, niente origine, niente nostro DOM, niente cookie: l'origine è opaca.

### 3. Il guscio è il contratto, ed è un file solo

`EmbedShell.html` in `Core/Content/`: doctype, la CSP in `<meta>` oltre che nell'header, `lang` e
tema stampati dall'endpoint, i token del marchio come variabili CSS, un reset minimo, la regola
`prefers-reduced-motion`, e un `ResizeObserver` che manda l'altezza alla pagina. Poi il frammento
dell'autore, nel corpo.

Chi scrive incolla **markup + `<style>` + `<script>`** e non scrive mai idraulica: la lingua, il
tema, i colori e l'altezza arrivano dal guscio. E le **linee guida sono generate dallo stesso file** —
`GET /embed/guidelines`, Markdown, scaricabile dall'editor — così il documento che Claude legge e la
cosa che il browser costruisce non possono divergere. Un test confronta le due.

### 4. Che cosa le linee guida impongono (e sono il pezzo che decide se funziona)

- **JS di browser, ES2022**: niente TypeScript, niente `import`, niente CDN — la CSP del frame non
  gli lascia aprire la rete, quindi non è una regola d'onore.
- **SVG + CSS prima di tutto**: per il caso 1 un `@keyframes` su un SVG non ha bisogno di una riga di
  JS, rispetta `prefers-reduced-motion` con una media query e si ingrandisce. Canvas solo quando le
  cose in movimento sono tante.
- **Tastiera obbligatoria** quando ci sono scelte: nel caso 2 le scelte *sono* il contenuto, e se si
  raggiungono solo col mouse metà del documento non esiste per chi non lo usa.
- **Due lingue**: le etichette stanno dentro il codice, quindi il guscio passa la lingua corrente e
  l'esempio mostra il modo di portarsele dietro tutte e due. Senza questo, un lettore italiano trova
  i pulsanti in inglese.
- **Movimento**: `prefers-reduced-motion` va rispettato, e un'animazione lunga vuole un comando per
  fermarla.
- ⚠️ **E una responsabilità che nessun meccanismo può prendersi**: niente impedisce a un'animazione di
  **contraddire il testo** che le sta sopra — un circuito disegnato a destra sotto un SOP che dice
  sinistra. Quello che c'è è la data di revisione e la finestra di pubblicazione che chiede che cosa
  è cambiato. Sta scritto nelle linee guida come dovere di chi pubblica.
- **L'esempio è la pista 09/27 con il circuito sinistro**, scritto per intero: è il caso che Carmine
  ha chiesto, ed è il modello che Claude copia.

### 5. Chi può, e come si stampa

Un permesso dedicato, **`Content.EmbedCode`**, nel catalogo come tutti gli altri: nessun handler
nuovo, lo scope di dipartimento viene dalla risorsa. Il blocco non compare nella barra a chi non ce
l'ha.

In **stampa** la sezione si chiude e non occupa spazio — `PrintContext` c'è già da G14. Resta solo la
**descrizione**, se l'autore l'ha scritta, perché è prosa del documento e non un avviso: è anche
l'unica parte che la ricerca indicizza, essendo l'unico campo tradotto.

## Le due cose che il disegno ha fatto emergere, e che non erano nelle tre domande

1. **L'editor deve poter scrivere un campo che non è una proprietà.** Il generatore di form disegna
   `props`; `source` sta nell'envelope. Quindi il pannello delle proprietà di questo blocco ha, sotto
   il form generato, **un campo in più** — un'area di testo monospaziata con il conto dei byte. È una
   deroga dichiarata, non un secondo editor: una `<textarea>` accanto a un form, per un blocco solo,
   perché il campo è del server e non dello schema.
2. **Il renderer non sa in che pagina sta**, e l'indirizzo del frame contiene l'id del contenuto e
   della versione. Si risolve come si è già risolto il chrome dell'editor: **un contesto**
   (`blocks/embedding.ts`) che la schermata che monta il renderer fornisce — la pagina pubblica sa la
   versione, l'editor sa che è una bozza. Con il contesto assente il blocco non disegna il frame e lo
   staff vede perché, esattamente come un blocco di tipo sconosciuto. Il renderer continua a non
   sapere niente di route e di API.

## Ordine di lavoro

1. `source` nell'envelope + il walker che lo limita — test di integrazione (un `source` troppo grande
   è rifiutato, uno su un blocco qualunque è innocuo).
2. L'endpoint, il guscio, gli header, le due cache, il 403 sulla bozza altrui.
3. Il blocco: schema, componente, il contesto, la textarea nel pannello, il permesso, i18n, galleria.
4. Le linee guida generate + l'esempio del circuito, e il pulsante che le scarica dall'editor.
5. La stampa che chiude la sezione, e i test che guardano un frame da fuori (niente `allow-same-origin`,
   la pagina non si tocca).

## Costruito il 12 settembre — quello che il disegno non diceva

- **Quattro schermate su cinque non fornivano il contesto.** Il primo giro l'aveva messo solo sulla
  schermata di news e documenti, quindi una **pagina** — il caso più comune — non disegnava nessun
  frame. Lo ha detto un e2e in un browser, non un test unitario: nel piccolo nessuno dei due lati
  sbagliava. Ora il contesto lo dà un hook solo, `usePublishedEmbedding`, che le cinque schermate
  chiamano con una riga.
- **Il frame si dipingeva un fondo suo, e sul verde petrolio era un rettangolo nero.** `color-scheme:
  dark` fa disegnare al browser la tela del documento con il suo nero opaco, e un frame che sta su
  una sezione deve lasciarla vedere. Via `color-scheme`, ovunque nel guscio; il prezzo è che le barre
  di scorrimento e l'aspetto di default di un controllo dentro il frame seguono il sistema del
  lettore e non la pagina — e dentro un frame non c'è né l'uno né l'altro. **Visto guardando**: è
  esattamente quello che i passi 4 e 5 servivano a trovare.
- **La stampa vuole anche il CSS.** `PrintContext` toglie il frame dal documento, ma lo monta solo
  la schermata di un **documento** (G14) e si accende su `beforeprint`: una pagina qualunque stampata
  dal menu del browser teneva il suo rettangolo vuoto. Ora il frame porta anche `print:hidden`, che
  è la strada che un'anteprima di stampa rispetta comunque.
- **Il banco contro l'API vera** (`e2e/full/interactive.spec.ts`) è scritto e **non è stato eseguito
  su questa macchina**: Docker non si avvia da qui, e senza database non c'è banco. Lo esegue la CI,
  che quel giro lo fa. Quello che si poteva provare senza server è provato in un browser vero
  (`e2e/embed.spec.ts`, quattro prove con **il guscio vero**): il disegno, la scelta da tastiera,
  l'altezza che cresce, il fondo scuro che entra, la stampa che chiude, e le due che contano — il
  frame **non** riesce a leggere la pagina che lo incornicia, e `localStorage` gli tira un'eccezione.

## Le regole vietate si denunciano da sole (12 settembre, sera)

Carmine: «se qualcuno dovesse fare le cose vietate — rete, storage, byte — ce ne accorgeremo?».
Controllato invece che ricordato, ed erano **tre risposte diverse**: i **byte** sì (il walker
rifiuta il salvataggio e la textarea conta mentre si scrive), **rete** e **storage** no — il browser
li impedisce, ma il rifiuto finisce nella console di quel frame e muore lì. Chi ha scritto
l'animazione vede «disegna storto» e non sa perché; chi pubblica non lo sa affatto.

Ora il guscio ascolta **`securitypolicyviolation`** — che è il browser a dire «ho rifiutato questo»,
quindi niente euristiche sul codice e nessun falso allarme su un `fetch` dentro un commento — e
**`window.onerror`**, per il caso più comune di tutti: l'animazione che si rompe. Li manda alla pagina
sullo stesso canale dell'altezza (una volta per tipo, mai in loop), e la pagina disegna una riga
**solo allo staff**, come già fa per il blocco di tipo sconosciuto e per il distintivo di un blocco
Data catturato. Un visitatore non vede niente: non è la sua animazione da riparare.

⚠️ **Scartato, e scritto perché non si riproponga**: cercare `fetch(`, `localStorage`, `https://`
nel codice al salvataggio. Prenderebbe l'errore onesto prima che qualcuno guardi, ma è un'euristica
su del codice — si aggira con due stringhe concatenate e grida al lupo su un commento. Un avviso che
grida al lupo è un avviso che si impara a ignorare. Si riapre solo se vediamo che succede davvero.

## Usato davvero (12 settembre, sera)

Carmine ha scaricato le linee guida e le ha date a un altro agente con un prompt suo: una pista
09/27, un pallino con accanto il nominativo IIVAO, circuito sinistro per la 09, finale a 3 NM con una
linea graduata a tacche di un miglio. Ha portato indietro il frammento, e **anche quello uscito dallo
stesso prompt senza le nostre istruzioni**, chiedendo se il risultato deludente fosse colpa nostra.

**Non lo era.** Il frammento scritto con le linee guida **le rispettava tutte** — quattro colori, i
tratti, i controlli sotto, due lingue, dodici secondi, e il finale rollato esattamente sulla tacca dei
3 NM. Le due cose che mancavano venivano dal prompt e dall'agente: il prompt chiedeva insieme un
circuito **sinistro** e una virata **a destra** dopo la soglia (due circuiti opposti — l'agente ha
tenuto quello esplicito e offerto l'altro come pulsante), e la **partenza** non era disegnata come
fase. Quello libero era più ricco — striscia con fase/quota/distanza, frecce, velocità, cursore — e
**nell'hub non sarebbe entrato**: pagina intera, font da Google, una tavolozza sua, una lingua sola.

Da lì, tutto costruito sulla stessa PR:

- **Una sezione di stile nelle linee guida.** Mancava del tutto: dicevano cosa non fare e non come
  disegnare, e venti animazioni scritte in due anni da persone diverse divergono lì. Ogni variabile di
  colore ha un mestiere, quattro colori al massimo, mai il colore da solo; tratti e testo **in
  proporzione alla larghezza** del `viewBox` (la prima versione diceva «400 × 240», e un circuito con
  tre miglia di finale lì dentro ha una pista lunga 40 unità); una figura per blocco, controlli sotto,
  niente scorrimento; 8–15 secondi per un circuito, una cosa che si muove per volta; le **convenzioni
  di un disegno d'aeroporto** — pista orizzontale con 09 a sinistra, nord in alto altrove, quote con
  l'unità, prue a tre cifre, una posizione è il suo callsign. Una **striscia di valori** è ammessa, ed
  è la cosa che il frammento libero faceva meglio. E **una richiesta che si contraddice** si risolve
  disegnando quello del documento e dicendolo nella legenda, mai metà e metà. ⚠️ **L'esempio della
  09/27 violava le regole scritte due sezioni sopra** (controlli sopra il disegno, giunzioni non
  arrotondate, codice morto, e un salto dell'aeroplano ripartendo dopo una pausa): corretto, perché è
  la parte che viene copiata.
- **Il guscio non promette più un font che non può avere.** `default-src 'none'` blocca il file di un
  font, anche nostro: «Nunito Sans» era vero solo dove è installato. Ora `system-ui`, e le linee guida
  ne traggono la conseguenza — poche parole dentro il disegno, la prosa nel documento.
- **Le cose vietate si denunciano da sole.** Alla domanda «ce ne accorgeremo?» le risposte erano tre:
  i byte sì, rete e storage no — il browser li ferma e il rifiuto muore nella console del frame. Il
  guscio ascolta `securitypolicyviolation` (un fatto, non un'ipotesi sul codice) e `window.onerror`, e
  la pagina li scrive **solo allo staff**. ⚠️ **Scartato**, e scritto qui perché non si riproponga:
  cercare `fetch(`, `localStorage`, `https://` nel codice al salvataggio — si aggira con due stringhe
  concatenate e scatta su un commento, e un allarme che grida al lupo si impara a ignorare.
- **Il codice da un file del computer**, letto nel browser e mai caricato: il frammento resta un campo
  della riga, e un file sul server sarebbe l'opzione (B), scartata per prima. ⚠️ Una pagina intera
  (`<!doctype`, `<html>`) era rifiutata **scelta** e accettata **incollata**: ora da tutte e due.
- **Vederlo prima di pubblicarlo.** L'altro agente non poteva: `HUB` è del guscio, quindi il frammento
  aperto da solo si ferma alla prima riga — ed è giusto. Si era costruito una pagina che copiava il
  guscio a mano, e proponeva un `HUB` di ripiego nel frammento. Le linee guida ora dicono le **due**
  strade — **una bozza**, che è privata per costruzione ed è l'anteprima più fedele, oppure
  **l'anteprima locale** `/embed/preview`, un HTML generato dallo stesso guscio che gira da disco con
  lingua, tre fondi, movimento ridotto, 360 px e i rifiuti elencati — e **vietano il ripiego**: una
  seconda copia del contratto che invecchia e nasconde proprio l'errore di un guscio assente.
  L'anteprima è un **download e mai una pagina del sito**, e il guscio ci entra come stringa JSON con
  i `<` escapati, perché i suoi `</script>` scritti crudi chiuderebbero a metà lo script dell'anteprima.

Trovati per strada:

- ⚠️ **In sviluppo `/embed` tornava `index.html`**: mancava da `BACKEND_PATHS`, e Vite rispondeva con
  la SPA — un 200 della cosa sbagliata, per le linee guida e, in silenzio, per ogni frame. **La stessa
  trappola di `/media`**, trovata allo stesso modo da Carmine. Un percorso del backend che si legge
  come una pagina è quello che ci si dimentica.
- ⚠️ **Un test d'integrazione verde per la ragione sbagliata.** `EmbedFrameTests` usava il VID 640001,
  che `DataBlockEndToEndTests` crea come **superadmin** nel database condiviso; in CI quella classe
  girava prima, e ogni verifica su chi può vedere una bozza passava a prescindere — perfino con una
  posizione inesistente (`IT-WC`; il coordinatore web è `IT-WM`). Da sola, la classe falliva tre test
  su cinque.

## Che cosa **non** si fa

Nessun `postMessage` oltre l'altezza (un numero, dal frame giusto, limitato); nessuno stato salvato;
nessun indirizzo che porti a una scelta; nessuna libreria servita da noi; nessun blocco dentro il
frame che parli con l'hub. E nessun caricamento di file HTML nella libreria: era l'opzione (B), e
Carmine l'ha scartata prima delle altre.
