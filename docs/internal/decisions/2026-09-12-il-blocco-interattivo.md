# Il blocco interattivo: un'animazione scritta da Claude, servita in un frame che non può fare niente

**Data:** 12 settembre 2026 — Carmine: «è possibile aggiungere una sezione che permetta di inserire
codice che realizza animazioni in un documento?», con il caso d'uso scritto per intero: creo un
documento, scarico le linee guida, le do a Claude Code — «realizza un'animazione che mostri una pista
09/27 e un traffico VFR in circuito sinistro» —, incollo il codice, e chi apre il documento vede
l'animazione.

**Stato:** **deciso da Carmine il 12 settembre**, sulle tre domande che gli ho messo davanti:
**(C)** un endpoint che serve il frame, non un `srcdoc`; in **stampa** la sezione si chiude e non
occupa spazio («un banner non serve a nulla»); **permesso dedicato**. Piano 0.70. Viene **dopo**
`2026-09-12-gli-header-di-sicurezza.md`, che è il pezzo su cui poggia.

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

## Che cosa **non** si fa

Nessun `postMessage` oltre l'altezza (un numero, dal frame giusto, limitato); nessuno stato salvato;
nessun indirizzo che porti a una scelta; nessuna libreria servita da noi; nessun blocco dentro il
frame che parli con l'hub. E nessun caricamento di file HTML nella libreria: era l'opzione (B), e
Carmine l'ha scartata prima delle altre.
