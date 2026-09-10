# Che cosa fa davvero il page builder di HQ, e che cosa vale la pena prendergli

**Data:** 10 settembre 2026 — chiesto da Carmine, che ha aperto il loro editor e ha chiesto di
guardarlo insieme
**Stato:** ⚠️ **da decidere.** Nessuna riga di codice prima.
**Come è stato guardato:** nel browser di Carmine, già autenticato, su
`va.ivao.aero/backend/pagebuilder/editor.php?id=1` (`id=6` non esiste più). **In sola lettura**: nessun
salvataggio, nessun trascinamento, nessun tocco a «Unpublish» — solo navigazione, lettura del DOM e
un clic per selezionare un blocco. Alla fine l'indicatore diceva ancora «Saved».

## La cosa più importante, e non si vede da uno screenshot

**Il loro «visual drag & drop editor» non è una tela.** Misurato nella pagina, non dedotto:

- **nessuna libreria di trascinamento**: né Sortable, né jQuery UI, né interact, né GridStack, né
  dragula. È drag & drop HTML5 scritto a mano;
- **zero elementi in posizione assoluta** dentro `.pb-canvas`. **Non esiste una coordinata.**

I 38 elementi trascinabili sono: **24** voci di tavolozza, **4** sezioni, **4** righe, **6** blocchi.
Cioè il trascinamento serve a due cose sole — **riordinare**, e **prendere un blocco dalla tavolozza e
lasciarlo nella pagina**.

⚠️ Conseguenza per la nota del 9 settembre: **la strada (B) non esiste nemmeno da loro.** Il modello di
HQ è un albero ordinato **Sezione → Riga → Blocco**, che è il nostro con un livello in mezzo. La (A)
non era il ripiego economico: era la stessa cosa che fa il builder che volevamo imitare.

## Le tre differenze vere

### 1. La Riga — e **ce l'abbiamo già**

Da loro una sezione contiene **righe**, e la riga porta la suddivisione in colonne (**dieci** preset
disegnati come diagrammi), la spaziatura (`—`, XS, S, M, L) e l'allineamento verticale (Top, Mid,
Bot, Fill). Così **una sezione può avere più suddivisioni diverse** sotto un unico sfondo.

Da noi il layout sta sulla sezione, quindi una sezione ha una suddivisione sola — e sembrava il
cambio di modello più grosso dei tre. **Non lo è**, e questa è la sorpresa:

- `MaxDepth = 3` nel validatore: **le sezioni si annidano**, fino a tre livelli;
- il renderer le disegna già: una sezione annidata cade **dentro** il contenitore di larghezza del
  genitore, dopo i suoi blocchi;
- `removeSection` pota ricorsivamente. Il modello, la validazione e la resa ci sono **tutti**.

Una sezione con lo sfondo, che contiene sezioni con `background: none` e `padding: none`, ognuna con
il proprio `layout`, **è** la Sezione → Riga di HQ. Oggi.

⚠️ Quello che manca è **una riga di codice e un pulsante**: `addSection` aggiunge sempre e solo in
fondo al primo livello, quindi dall'editor una sezione annidata non si può creare. E si vede da qui
che nessuno se n'era accorto: **nessuna delle dieci pagine e dei template seminati annida** — sono
tutte profonde uno. Abbiamo costruito un modello a tre livelli, lo validiamo, lo disegniamo, e
l'editor ne offre due.

**Costo: (a)/(b).** Un `addNestedSection`, un pulsante «aggiungi sezione qui dentro» nell'outline, e
la profondità già rifiutata dal server a 3. Da decidere: se chiamarla «sezione annidata» o **«riga»**,
che è la parola che un redattore capisce.

### 2. I comandi stanno sull'oggetto, dentro la pagina

Da loro la barra della sezione porta **sei pastiglie di sfondo** (White, Off-white, Faint, Blue,
Navy, Dark), la dimensione e la larghezza; la barra della riga porta i preset di colonne, la
spaziatura e l'allineamento. Nel pannello a destra ci vanno **solo** le proprietà del blocco.

Da noi tutto sta nel pannello: per cambiare uno sfondo si seleziona la sezione e si apre un form.

**Costo: (b), e non tocca il modello.** È la scelta di quali campi meritano di stare sull'oggetto —
quelli che si cambiano guardando, cioè sfondo, larghezza, spaziatura e colonne — e quali restano nel
form. ⚠️ Va deciso *quali*, non «tutti»: una barra con dieci comandi sopra ogni sezione è rumore, e
il pannello esiste perché il generatore di form disegna i campi una volta sola.

### 3. Si trascina dalla tavolozza dentro la pagina

Da loro le 24 voci della tavolozza sono trascinabili: si prende «Card Grid» e lo si lascia dove deve
stare. Da noi si preme «aggiungi blocco» dentro una sezione e il blocco arriva in fondo.

**Costo: (b).** L'anteprima è già la superficie di composizione (deciso il 9 set), quindi servono i
punti di rilascio fra un blocco e l'altro e una tavolozza trascinabile. ⚠️ È anche il pezzo che va
misurato in un browser: il rilascio fra due blocchi è quello che si sbaglia, e le frecce dell'outline
restano l'unica strada da tastiera.

## Che cosa **non** prendere

- **La barra di testo ricco dentro il campo** (B, I, U, H2, H3, citazione, elenchi, link, immagine).
  Il markdown con anteprima è **deciso** (§15.4) e la ragione non è cambiata: due editor di testo
  sono due modi di produrre lo stesso HTML, e uno dei due invecchia.
- **Le loro dieci suddivisioni di colonne.** Cinque coprono i casi veri; dieci sono cinque preset che
  nessuno userà e cinque decisioni in più davanti a chi scrive.

## Che cosa abbiamo noi e loro no

Vale la pena scriverlo, perché il confronto non è a senso unico: le **lingue affiancate** in ogni
campo tradotto (da loro il testo è uno), i **template con `locked` / `required` / `allowedBlocks`**,
e il fatto che tutto si raggiunge **da tastiera**.

## Raccomandazione, in ordine

1. **La 1**, perché è quasi gratis e perché è un pezzo di prodotto che *abbiamo già costruito e non
   abbiamo mai acceso*. È anche quella che risponde alla sensazione «manca qualcosa»: senza righe,
   una pagina è una pila di fasce e non un impaginato.
2. **La 2**, che è la seconda in ordine di resa per riga di codice — e che rende inutile aprire il
   pannello per le tre cose che si cambiano guardando.
3. **La 3** per ultima, perché è la più delicata da far funzionare bene e la meno necessaria: un
   blocco aggiunto in fondo e poi trascinato al suo posto costa un gesto in più, non un errore.
