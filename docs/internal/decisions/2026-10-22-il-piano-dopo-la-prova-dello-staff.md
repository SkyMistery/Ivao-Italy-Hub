# Il piano dopo la prova dello staff

**Data:** 22 ottobre 2026
**Stato:** **decisa da Carmine in chat al master**, 22 ottobre 2026 sera, sull'elenco che lui stesso ha dato dopo aver provato l'hub
con lo staff: le risposte alle domande del §6 sono nel §7, e l'ordine del §5 è approvato («si va bene … Vai, procedi pure»).
**Regola applicata:** la fase di test (`2026-10-18-la-fase-di-test`): ognuno corregge il suo codice. Il nucleo, il sito e il CMS e i
Tour sono di Carmine (sessioni di lavoro); Eventi e Training di `dalberone`. Un difetto è una issue e una PR; una funzione nuova o un
cambio di regola è il caso (c) di `CLAUDE.md` §5: prima la nota, poi il codice.

## 1. Che cosa è successo

Carmine ha fatto provare l'editor dei contenuti e i Tour a staff che non ha costruito il sito. Il risultato: **creare una pagina, una
news o un documento è troppo complicato per chi non lo conosce**. L'elenco (i numeri sono quelli di Carmine: 1–10 l'editor, I–XXIII i
blocchi, a–q il resto) mescola tre cose diverse, che qui si tengono separate:

- **difetti** (qualcosa che non fa quello che dice): si correggono e basta;
- **domande** («che cos'è…»): spesso la risposta è che la parola sullo schermo non lo dice, e la correzione è la parola;
- **funzioni nuove o regole da cambiare**: una nota e una decisione prima del codice.

## 2. I due difetti gravi

- **G1 — Il contenuto sparisce mentre si lavora** (Carmine: succede soprattutto **trascinando un blocco in una sezione**; torna
  ricaricando, passando da «inline» a «on page», o con bozza → pubblica → bozza). È un difetto di stato dell'editor, non dei dati: il
  server ha la bozza, è lo schermo che la perde. **Blocker**: prima di tutto il resto. Si comincia riproducendolo in un test e2e
  (trascinare un blocco della tavolozza in una sezione, poi controllare che il corpo sia ancora disegnato), poi si corregge.
- **G2 — «Che cosa è cambiato» è sempre vuoto** (il punto 9). Verificato sul codice: la riga **si salva** (`ContentPublishRequest` →
  `cms_content_versions.changelog`, `ContentPublishService.cs`), ma **nessuna schermata la mostra**: nessun DTO la porta fuori e non
  c'è una storia delle versioni. In più il campo riparte vuoto a ogni pubblicazione. La correzione: la **storia delle versioni** nel
  pannello del contenuto (numero, data, chi, la riga), e il campo del dialog precompilato con un riassunto automatico di che cosa è
  cambiato (sezioni e blocchi aggiunti, tolti, modificati), che chi pubblica può riscrivere.

## 3. Le risposte alle domande

Che cosa sono oggi le cose che lo staff non ha capito, e che cosa ne facciamo.

| N. | La domanda | Che cos'è oggi | Proposta |
|---|---|---|---|
| IV | Elenco di link | Mostra i link della schermata **Link** (`/staff/links`), filtrati per categoria e dipartimento: non si scrivono nel blocco | Una frase sotto il blocco nell'editor che lo dice, con il link alla schermata dei Link |
| VII | Pagina incorporata | Un `iframe` di una pagina di **un altro sito**, solo dagli host dell'elenco consentito (`docs/UI-GUIDELINES.md`, `web/src/blocks/allowlist.ts`) | La frase che lo dice e l'elenco degli host nell'aiuto del blocco |
| XI | Colore d'accento delle icone | Il colore delle icone dei riquadri (blu, azzurro, verde acqua, arancione), che si vede poco perché è solo sul disegno | Mostrarlo meglio (il cerchio dietro l'icona) o toglierlo: ⚖️ |
| XV-bis | «Kinds» del calendario | I tipi di voce del calendario (eventi, training, esami, …), oggi un **testo libero** | Una scelta multipla dai tipi che il calendario conosce (difetto) |
| XVI-bis | «What this block shows» | La modalità `live` / `frozen` di un blocco di dati: la risposta del momento, o quella catturata alla pubblicazione | Rinominarlo in parole semplici («Aggiornato sempre» / «Fermo alla pubblicazione») e mostrarlo solo dove serve |
| XVII | «Public errors» | Il blocco dei Tour che mostra il **catalogo degli errori** che un validatore segna su un rapporto, solo quelli marcati pubblici | Il nome giusto («Errori dei rapporti dei tour») e l'aiuto |
| e | Le collezioni | Gruppi di news e documenti (per esempio «Regolamenti»), che `newsList` e `documentList` sanno filtrare | Sì alla proposta di Carmine: si scelgono dalla pagina del documento o della news, da un elenco, con «nuova collezione» |
| VI | Le immagini nel video | Il campo «oppure un file della libreria» è per un **file video** caricato, ma il selettore mostra tutti i media | Il selettore mostra solo i video (difetto) |

## 4. Che cosa c'è nel resto dell'elenco, raggruppato

### A. L'editor (nucleo, Carmine)

- **Le sezioni si vedono e si creano meglio** (2, 8): nell'editor, e solo lì, un bordo e un'etichetta per ogni sezione («Sezione 2 —
  sfondo azzurro»), un «+ sezione» fra due sezioni, e un nome per ogni sezione che il pannello mostra. Non esistono sottosezioni nel
  modello (`Content → Section → Block`, e niente blocchi dentro blocchi): «sottosezione» oggi è un blocco a colonne o le schede; si
  scrive una frase che lo dice invece di aggiungere un livello.
- **Più spazio** (3, 5): aprendo l'editor la barra laterale dello staff si chiude da sola; i pannelli dell'editor si allargano
  trascinando un bordo sottile.
- **Scrivere nel blocco** (3): il testo di un titolo, di un paragrafo, di un callout si scrive cliccandoci sopra nella pagina, non solo
  nel pannello. È il pezzo più grosso dell'editor: chiede una nota sua.
- **Il testo formattato** (7): oggi il testo è Markdown, quindi grassetto, corsivo, barrato, elenchi annidati esistono già, ma bisogna
  conoscerne la sintassi. Una **barra di formattazione** sopra il campo (grassetto, corsivo, sottolineato, barrato, elenchi numerati e
  puntati annidabili, allineamento). **Il colore di una parola** va contro una regola decisa (`docs/UI-GUIDELINES.md`: «nessun colore
  libero», l'accento «mai sotto una parola»): ⚖️.
- **Anteprima in una scheda nuova** (6): la pagina come la vedrà il sito, con i link disattivati, aggiornata da un tasto.
- **Traduzione su richiesta** (4): «Traduci dall'italiano» sulla lingua vuota. Chiede un servizio esterno, una chiave segreta e una
  scelta di chi lo paga: ⚖️.
- **Le voci ripetute si chiudono** (IX): ogni elenco di voci (passi, pulsanti, righe, domande, …) mostra le voci chiuse con il loro
  titolo, e si aprono una alla volta. Una data di un passo si può cancellare. Vale per il form generato, quindi ovunque.
- **Media e link più veloci** (XXII, XXIII): dove si sceglie un media, **cerca** (nome, categoria) e **carica qui** (il caricamento si
  apre accanto, si salva, e il media nuovo è già scelto); dove si scrive un indirizzo del sito, si **sceglie una pagina** da un elenco
  (II).

### B. I blocchi (nucleo e sito, Carmine)

- **Callout** (I): titolo facoltativo, icona allineata al testo, «Attenzione» giallo e non rosso. Difetto.
- **Pulsanti** (II, III): la scelta di una pagina del sito da un elenco, il controllo che il link porti a una pagina che esiste, e il
  rifiuto di un pulsante verso una pagina **meno visibile** della pagina che lo contiene (una pagina per tutti non porta a una pagina
  dello staff). «Pulsante nel testo» e «Link e pulsanti» sono lo stesso pezzo: resta quello degli interattivi, l'altro si toglie dalla
  tavolozza (le pagine che lo usano continuano a disegnarlo).
- **Immagine** (V): la scelta della larghezza (fra le quattro decise) e il clic per vederla a tutto schermo.
- **Blocco interattivo** (VIII): l'altezza si adatta al contenuto, senza barra di scorrimento; le istruzioni per l'IA dicono di
  adattarsi a tablet e telefono.
- **Tabella** (X): incollare da Excel o da un testo con tabulazioni, importare un CSV, «crea N righe × M colonne», scrivere nella
  cella, e righe divise o no.
- **Galleria** (XII): più modi di scorrere (griglia, carosello, a scorrimento), con una didascalia sotto ogni immagine.
- **Loghi** (XIII): il selettore mostra solo i media della categoria «logo».
- **Allineamento** (XIV): sinistra, centro, destra per ogni blocco, nel pannello comune del blocco.
- **Domande e risposte** (XV): un titolo, le domande numerate, più risposte per domanda, aperte o chiuse quando si apre la pagina.
- **Blocchi di dati** (XVI): l'etichetta sotto ogni numero si può cambiare.
- **Blocchi solo per lo staff** (XVIII): su una pagina pubblica compaiono nella tavolozza in grigio, non selezionabili, con la ragione.
- **Tabella delle frequenze** (XX): le postazioni si scelgono da un elenco con ricerca, dalle postazioni che l'hub conosce già (le
  postazioni ATC della divisione, `ref_`), e i dati arrivano da lì.
- **Coordinamento** (XXI): si toglie dalla tavolozza, perché i coordinamenti stanno su `atc.it.ivao.aero`.

### C. Indirizzi e struttura del sito (nucleo e sito, Carmine) — il punto 1

La proposta di Carmine regge e il master aggiunge due cose:

1. **Chi crea una pagina sceglie dove va, non l'indirizzo**: un albero del sito (le pagine e le loro figlie) da cui si sceglie la
   pagina madre; l'indirizzo nasce dal titolo sotto quella madre (`/regolamenti/nuova-pagina`) e lo può cambiare solo il Web
   department, HQ o il superadmin.
2. **Le pagine si approvano**: `Content.Approve` sulle pagine a WD e HQ (in `division.json → positionGrants`, non nel codice), come
   chiede Carmine; il superadmin approva già.
3. **La mappa del sito** come pagina del Web department, generata dall'albero (non scritta a mano, così non invecchia).
4. **Le pagine scritte nel codice** (o) compaiono nell'albero e negli elenchi, segnate, non modificabili e con l'indirizzo fisso.
5. **Una guida per lo staff** (in più): una pagina «Come si fa una pagina» del Web department, con le regole dell'indirizzo, che cosa
   va in una sezione, quale blocco per che cosa.

### D. Le fasce ai lati (10) (nucleo, Carmine)

Due fasce facoltative ai lati di una pagina, visibili solo su uno schermo largo (dai 1600 px in su), con blocchi di dati (per esempio
gli enti aperti a sinistra e gli eventi dei prossimi 7 giorni a destra nella home). Cambia il modello della pagina (una regione oltre
le sezioni): ⚖️ nel §6.

### E. Date e calendari (nucleo, e i moduli)

- **Il formato della data** (a): `dd/mm/yyyy` di default, con la divisione che sceglie `mm/dd/yyyy` o `yyyy/mm/dd` in `division.json`.
  Un pezzo del nucleo usato da tutti: una PR del nucleo, poi i moduli lo usano.
- **La fine si calcola dalla durata** (b, Training, `dalberone`): un training o un esame con un inizio prende la fine dalla durata
  delle impostazioni (120 minuti di default).
- **Il calendario che mostra il libero e l'occupato** (c): nel selettore della data, i giorni liberi in verde e gli occupati con il
  colore del tipo (legenda), e nella finestra dell'ora le ore occupate segnate; un evento che **blocca** quell'ora non si può scegliere.
  È un pezzo del nucleo (il selettore, che legge il calendario unico) e poi un uso in Training ed Eventi.

### F. La libreria dei media (nucleo, Carmine)

- **Categorie fisse** (d): Immagine, Video, Icona, Logo, Documento (PDF), Banner, invece del testo libero. Le categorie già scritte si
  riportano a queste in una migrazione additiva.

### G. I Tour (modulo FlightOps, Carmine)

- **I rating** (f): da un elenco, non numeri, e **controllati**: un pilota sotto il rating minimo non manda il rapporto. I rating
  esistono già nel nucleo (rating e postazioni della divisione, #204); si usa quello, per tutti i moduli che hanno un
  rating. Se IVAO non li dà dall'API, un file JSON della divisione.
- **Gli aerei** (g, i, j, k): la ricerca degli aerei che a volte non trova «A320» è un difetto; i tipi delle leg e degli aerei ammessi
  si scelgono da un elenco; la chiave `tours.fields.allowedAircraft.icao` che compare al posto della parola è un difetto; «aggiungi
  dopo» copia anche il tipo della leg (o quello del tour).
- **I campi di un altro tipo di tour** (h) sono grigi e non si scrivono.
- **Le regole in un posto solo** (l): tutto in «Regole», niente in «Impostazioni».
- **Hub e rotazioni da file** (p), come le leg.
- **La pagina di un tour e l'elenco dei tour** (m): ripensate, scritte nel codice (con il loro design prima, da approvare).
- **Le schermate per creare i tour** (q): dopo, con lo stesso design.

### H. Gli eventi (modulo Events, `dalberone`)

- **L'elenco degli eventi come lo vogliamo** (n): una pagina scritta nel codice, dal suo design.

## 5. L'ordine proposto

Si va per fasi; ogni fase si consegna sulla prova prima della successiva, così lo staff prova le correzioni mentre si scrive il resto.

| Fase | Che cosa | Di chi | Perché in questo punto |
|---|---|---|---|
| **0** | La consegna della **0.11.0** sulla prova | Carmine | È pronta: sei correzioni che lo staff deve vedere |
| **1** | **G1** (il contenuto che sparisce) e **G2** (la storia delle versioni) | Carmine | Un editor che perde il lavoro rende inutile ogni altra prova |
| **2** | **Le correzioni piccole**: tutte le domande del §3, callout, video, loghi, calendario a scelta, coordinamento e pulsante doppio fuori dalla tavolozza, `allowedAircraft`, la ricerca degli aerei, i campi grigi dei tour, i blocchi dello staff in grigio. Insieme, la coda già aperta: #328 punti 5 e 7, #319 punto 2, le minori della revisione (#319–#321, #325, #326, #332, #334, quelle `security` prima) | Carmine | Poche righe ciascuna, nessuna decisione: tolgono la metà dello sconcerto |
| **3** | **La nota dell'editor** (A e B, con le ⚖️ risposte): poi il codice in più PR — sezioni visibili e barra che si chiude, pannelli allargabili, voci che si chiudono, media e link veloci, barra di formattazione, anteprima in una scheda, poi i blocchi (tabella, galleria, immagine, domande, interattivo, allineamento, pulsanti con la scelta della pagina) | Carmine | È il grosso del problema dello staff, e va disegnato insieme |
| **4** | **Indirizzi e struttura** (C): l'albero, l'indirizzo dal titolo, l'approvazione di WD, la mappa del sito, la guida, le pagine del codice negli elenchi | Carmine | Dopo l'editor: usa il selettore delle pagine della fase 3 |
| **5** | **Date** (E): il formato della data nel nucleo, poi il calendario libero/occupato nel nucleo; i moduli dopo | Carmine (nucleo), `dalberone` (b, c nei moduli) | Il nucleo prima, i moduli lo usano |
| **6** | **I Tour** (G): rating dal nucleo e controllati, aerei da elenco, regole in un posto, hub e rotazioni da file; poi il design della pagina di un tour e dell'elenco | Carmine | Indipendente dalle fasi 3–5: può correre accanto se ci sono sessioni libere |
| **7** | **Le cose grandi e nuove**: scrivere nel blocco (3), la traduzione (4), le fasce ai lati (10), il colore delle parole se deciso | Carmine | Ognuna con la sua nota; dopo che lo staff ha provato le fasi 1–4 |
| **8** | **Il Gate Manager nell'hub** (design, poi codice) e **l'elenco degli eventi** (n) | `dalberone` | Già deciso dopo le altre fasi di M4 |
| — | In parallelo, sempre: **la coda di `dalberone`** (#311–#314, #322, #327, #333, GHSA-h2hw) e b, c nei moduli | `dalberone` | Non tocca i file delle fasi di Carmine |
| **dopo** | La produzione su `it.ivao.aero` (nota `2026-10-18-la-produzione`) ed E9 | Carmine | Quando la fase di test è finita (nota `2026-10-18-la-fase-di-test` §6) |

## 6. Le domande per Carmine ⚖️

1. **Il colore di una parola** (7). La regola di oggi dice «nessun colore libero». (a) Un insieme chiuso di colori del marchio
   (blu, azzurro, verde acqua, arancione, rosso per un avviso), che restano leggibili in chiaro e in scuro — **raccomandata**; (b) un
   colore qualsiasi; (c) niente colore, solo grassetto e corsivo.
2. **La traduzione su richiesta** (4). Serve un servizio esterno con una chiave nei segreti. (a) DeepL (versione gratuita fino a
   500.000 caratteri al mese, poi a pagamento) — **raccomandata**, buona sull'italiano; (b) un modello linguistico (Claude o altri),
   a consumo; (c) rimandare. Il testo dei contenuti esce dal server verso quel servizio: va scritto nella privacy.
3. **Le fasce ai lati** (10). (a) Due regioni della pagina, solo blocchi di dati, visibili da 1600 px — **raccomandata**; (b) solo
   nella home; (c) rimandare.
4. **L'immagine ridimensionabile** (V). (a) Una scelta fra le quattro larghezze decise, più il clic a tutto schermo —
   **raccomandata**, resta dentro le regole dei blocchi; (b) trascinare l'angolo a una larghezza qualsiasi.
5. **Il colore d'accento delle icone** (XI). (a) Renderlo visibile (il cerchio colorato dietro l'icona) — **raccomandata**; (b)
   toglierlo.
6. **L'ordine** del §5: va bene, o qualcosa va prima?

## 7. Le risposte di Carmine (22 ottobre 2026, in chat al master)

1. **Il colore di una parola: (a)**, un insieme chiuso di colori del marchio, leggibili in chiaro e in scuro. Cambia la regola
   «nessun colore libero» di `docs/UI-GUIDELINES.md` per il testo: un colore sì, ma solo dall'insieme.
2. **La traduzione**: Carmine chiede **Microsoft Translator** («Microsoft translator non si può usare?»). Si può: è il servizio
   Azure AI Translator, con un piano gratuito (fino a 2 milioni di caratteri al mese, secondo le condizioni pubblicate da Microsoft,
   da ricontrollare quando si apre l'account) e una chiave che va nei segreti. **Si usa quello.** Il resto della domanda 2 resta: il
   testo esce dal server verso quel servizio, va scritto nella privacy; e la nota della fase 7 dice dove si chiama, chi può chiederlo
   (lo staff che scrive) e che cosa succede senza la chiave (il tasto non c'è).
3. **Le fasce ai lati: (a)**, due fasce con soli blocchi di dati, visibili da 1600 px, **«però strutturati bene»**: la nota della
   fase 7 le disegna come una parte vera del modello della pagina, non un'aggiunta.
4. **L'immagine: (b)**, si ridimensiona trascinando, a una larghezza qualsiasi, più il clic a tutto schermo. Cambia la regola delle
   «quattro larghezze» per l'immagine: la nota dell'editor (fase 3) dice dentro quali limiti (non più larga della sezione, non più
   stretta di un minimo leggibile, la larghezza in percentuale perché segua lo schermo).
5. **Il colore d'accento delle icone: (a)**, si rende visibile.
6. **L'ordine del §5: approvato.**

## Da portare nel piano

- **§13**: la fase di test continua con le fasi del §5 di questa nota, prima della produzione.
- **§8.3** e `docs/UI-GUIDELINES.md`: le risposte 1, 4 e 5 (i colori del testo da un insieme chiuso, l'immagine a larghezza libera
  dentro i limiti, l'accento delle icone visibile).
- **§10** o **§15**: la traduzione con Microsoft Translator (Azure AI Translator), la chiave nei segreti, la riga nella privacy.
- **§9.3**: le fasce ai lati, se decise (domanda 3); l'albero e l'indirizzo dal titolo (§4.C).
- **§16**: il formato della data del nucleo (§4.E).
