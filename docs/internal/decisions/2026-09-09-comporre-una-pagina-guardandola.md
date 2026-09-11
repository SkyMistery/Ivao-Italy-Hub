# Comporre una pagina guardandola: anteprima modificabile o tela libera

**Data:** 9 settembre 2026 — chiesto da Carmine dopo la terza esecuzione della demo
**Stato:** **decisa da Carmine il 9 settembre 2026 — la (A)**, «è quella più economica, vediamo come
funziona, poi se proprio non è come la pensavo io allora ci andiamo giù pesante con la B». Costruita
lo stesso giorno; in fondo c'è che cosa è costata.
**Perché esiste:** regola (c) di piano §16.E. Il canvas drag & drop era stato scartato nel censimento
del sito template di HQ (§2.3-ter) con **una riga** di motivazione — «il pezzo più costoso, e
mantenuto da HQ» — e Carmine ha chiesto di riaprirla. Una decisione presa in una riga si riapre
scrivendone mezza pagina, non rispondendo a memoria.

## Che cosa serve davvero

Le parole di Carmine, che valgono più di qualunque riassunto:

> «Il canvas con trascinamento ti permette di avere un'idea molto chiara di come sta venendo il
> documento e di dove vuoi mettere le cose, dello spazio che occupano, **senza fare avanti e indietro
> dalla preview**.»

Il bisogno è **vedere il risultato mentre si compone**. Non sono le coordinate: quelle sono un modo
di ottenerlo, e ce n'è un altro.

⚠️ Due obiezioni che il piano portava contro la tela **cadono**, e vanno tolte di mezzo prima di
confrontare:

- «e sul telefono?» riguarda **chi legge**, non chi edita. Che si componga da PC o da tablet e mai da
  telefono è una scelta ragionevole di prodotto, e Carmine l'ha fatta;
- «`locked` non vorrebbe più dire niente» è **falso**: su una tela «bloccata in quella posizione» e
  «obbligatoria in questo template» reggono benissimo. Come regge `allowedBlocks`.

Resta in piedi una cosa sola delle vecchie obiezioni, e nemmeno come obiezione: le frecce
dell'outline sono l'unica strada da tastiera, ma una vista **in più** non le toglie. È un vincolo su
come si aggiunge, non un motivo per non aggiungere.

## Le due strade

**(A) L'anteprima diventa modificabile.** Si clicca un blocco nella pagina disegnata e si apre il suo
pannello proprietà; lo spazio che occupa si vede perché *è* la pagina. L'outline resta come seconda
vista, ed è lì che si sposta. (Trascinare **dentro** l'anteprima sarebbe un secondo passo: vedi in
fondo che cosa la prima fetta ha lasciato fuori.)

**(B) La tela libera.** I blocchi si posizionano in due dimensioni dentro la sezione, con misure
proprie.

| | (A) anteprima modificabile | (B) tela libera |
|---|---|---|
| Modello dei dati | **invariato**: sezioni e blocchi in ordine | ogni blocco acquista **geometria** |
| Il set dei 27 blocchi | invariato | ognuno deve sapere **quanto è grande** |
| `layout`, `width`, `padding` della sezione | restano quello che sono | in gran parte **doppioni** della geometria |
| Template (`locked`, `required`, `allowedBlocks`) | invariati | reggono, da ridefinire su una posizione |
| Resa sul telefono del lettore | gratis: è il CSS della pagina | **da progettare**: griglia con breakpoint, o un layout per larghezza |
| Stampa su A4 (richiesta del 9 set) | gratis, stesso CSS | **un terzo layout** da risolvere |
| Backend | zero: `body_json` è opaco e il walker legge solo le mappe tradotte | zero, per la stessa ragione |
| Che cosa si costruisce | una modalità «in modifica» del renderer | un editor geometrico e 27 dimensioni |

## Il punto che decide, e non si vede da fuori

**Oggi un blocco non ha una dimensione.** Dichiara un contenuto; sono la sezione e il CSS a decidere
quanto è largo e quanta aria ha intorno. Il modello ha già un posizionamento, ed è **a una
dimensione**: `layout` sceglie fra `stacked`, `1/2+1/2`, `1/3+2/3`, `2/3+1/3`, `3x1/3`, e un blocco
dichiara in quale colonna sta (`column`).

La (B) generalizza quel posizionamento a due dimensioni. Non è una schermata nuova: è **una proprietà
nuova su tutto il set**, e due layout in più da far tornare — quello del telefono e quello della
carta. La (A) non lo tocca.

E l'altra metà del conto: l'anteprima **non è un'imitazione**. `PreviewFrame` monta lo stesso identico
renderer del sito pubblico con un `max-width` sopra. Quello che si vede è già la pagina vera — quindi
«modificare guardando il risultato» non vuol dire scrivere un secondo motore, vuol dire rendere
selezionabile quello che c'è.

## Raccomandazione

**La (A).** Dà quello che la richiesta chiede — vedere il documento mentre lo si compone, senza
rimbalzare fra outline e anteprima — e non compra niente di quello che la (B) fa pagare: il set dei
blocchi resta com'è, il responsive e la stampa restano gratis, i template continuano a significare
quello che significano.

⚠️ Il rischio della (A) è uno solo e va scritto: **il renderer è uno solo, pubblico e editor**.
L'interattività va aggiunta in modo che nel sito pubblico sia spenta per costruzione — un contesto
che il percorso pubblico non imposta mai — o la prima cosa che si rompe è la pagina del visitatore.
È lo stesso patto dell'anteprima: un renderer, due usi, mai due comportamenti che possono divergere.

Se la (B) resta comunque quello che si vuole, si può fare: ma allora si apre come una fase sua, con
le tre risposte davanti (dimensione di ogni blocco, telefono, A4) e non come una rifinitura
dell'editor.

## Che cosa non cambia in nessuno dei due casi

I template servono esattamente a quello che Carmine dice: **avere qualcosa di prefatto in cui mettere
solo le informazioni**. Nessuna delle due strade li tocca, e da oggi hanno anche una schermata.

---

## Che cosa è costata la (A)

Meno di quanto la nota prevedesse, e la ragione è quella scritta sopra: l'anteprima era già la pagina
vera.

- **`blocks/picking.ts`**, un contesto di due campi: che cosa è selezionato, e che fare di un clic.
  Vale `null` dappertutto, e **il percorso pubblico non monta nessun provider** — quindi nella pagina
  di un visitatore non c'è un gestore da togliere, un attributo da ripulire o una classe da
  sovrascrivere. È la promessa che tiene in piedi «un renderer solo», e il primo test è quello.
- **`ContentRenderer`** legge il contesto in due punti: una sezione si sceglie dal proprio spazio, un
  blocco dal proprio riquadro.
  ⚠️ Il blocco intercetta il clic in fase di **cattura**, con `preventDefault` e `stopPropagation`.
  Un blocco non è un rettangolo inerte: contiene link, pulsanti, un form di contatto. Catturare vuol
  dire che il clic arriva al blocco e non a ciò che c'è dentro — così una call to action si seleziona
  invece di portare fuori dall'editor chi sta componendo, con le modifiche non salvate — e fermarlo
  lì è ciò che lascia la sezione selezionabile dal proprio spazio. La sezione usa la fase di
  risalita, perché chi cattura per primo è quello **esterno** e altrimenti vincerebbe sempre lei.
- **L'anteprima ha smesso di essere un posto dove si va e si torna.** In anteprima ora c'è lo stesso
  pannello proprietà accanto, e il pannello è **scritto una volta sola** e disegnato nelle due vie di
  composizione: due copie sarebbero due pannelli che possono non essere d'accordo su cosa offre un
  blocco, che è lo stesso argomento che tiene un renderer solo.
- **Il pannello non dice più «a sinistra»**: adesso a sinistra c'è la pagina.
- I test: tre di Vitest — il primo dice che **la pagina del visitatore non ha niente da cliccare**, e
  quello non si allenta mai; gli altri due che un clic sceglie il blocco invece di seguire il link, e
  che lo spazio intorno sceglie la sezione. Uno del giro completo, contro il renderer **vero**, che
  apre i campi del blocco cliccato. Verificati rompendo il pezzo che provano.

### Il seguito, lo stesso giorno: il muro dei metadati

Provata, la (A) non convinceva ancora, e la ragione si misura: **l'editor si apriva su 1182 px di
metadati in una finestra da 950**. Il modulo era più alto dello schermo, e sotto la piega finivano
sia la pagina sia i pulsanti — perché la barra («Save draft», Anteprima, Annulla, Pubblica, Elimina)
era il *submit* di quel form e le sue azioni secondarie. In anteprima la pagina cominciava a
**1588 px** dall'alto.

Il dettaglio, campo per campo: Address 82, Visible to 58, Title 150, Summary 216, **Search engines
and sharing 536**. Metà del modulo era la SEO, cioè il campo che si tocca meno — il suo stesso aiuto
dice che lasciandolo vuoto si usano titolo e sommario.

Scelta da Carmine fra tre, la **A1**: *la pagina è una selezione*.

- I metadati sono **le proprietà della pagina**, nello stesso pannello di una sezione e di un blocco.
  L'outline ha una riga «Pagina» in cima, e il pannello un «‹ Pagina» per tornarci — che serve in
  anteprima, dove l'outline non c'è. Lo stato «niente selezionato» **non esiste più**: si edita la
  pagina, una sezione o un blocco.
- **La barra è una barra**: in cima, appiccicata, sempre raggiungibile.
- ⚠️ E il pezzo delicato: `Save draft` sta **fuori** dal form e lo invia con `form="…"`, che è come
  l'HTML permette da sempre a un pulsante di vivere altrove. Perché funzioni il form deve restare
  **nel documento**: quando è selezionato un blocco è `hidden`, non smontato. Il test lo dice
  esplicitamente e asserisce sulla **visibilità** e non sul conteggio, perché un conteggio passerebbe
  per la ragione sbagliata il giorno che qualcuno lo smonta.
- `SchemaForm` ha imparato due cose piccole: `id` e `actionsElsewhere`. Non sono un tipo di campo
  nuovo — il conto delle estensioni resta **nove**.

**Il risultato, misurato**: la pagina comincia a **466 px** invece di 1588, dentro la finestra invece
che due terzi di schermo più giù.

⚠️ Resta la SEO: 536 px dentro un pannello stretto sono anche più alti. Un gruppo richiudibile
sarebbe la **decima** estensione del generatore, e non l'ho infilata qui: una modifica alla volta si
giudica, due no.

### Un poscritto del 10 settembre, e ribalta una premessa

Carmine ha aperto il page builder di HQ e mi ha chiesto di guardarlo. Misurato nella loro pagina:
**nessuna libreria di trascinamento e zero elementi in posizione assoluta**. Il loro «visual drag &
drop editor» non è una tela: è un albero ordinato Sezione → **Riga** → Blocco, dove il trascinamento
serve a riordinare e a prendere un blocco dalla tavolozza.

⚠️ Quindi **la (B) non esiste nemmeno da chi l'ha inventata**, e la (A) non era il ripiego economico:
era la stessa cosa. Che cosa valga la pena prendergli davvero sta in
`2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`.

### Che cosa **non** c'è ancora, e va detto

- **Non si trascina dentro l'anteprima.** Si sceglie e si modifica; per spostare c'è l'outline, che è
  rimasto intero. Era fuori dalla prima fetta di proposito: mezzo trascinamento è peggio di nessuno.
- Scegliere nell'outline **non fa scorrere** l'anteprima fino a lì. Non serve finché le due viste si
  alternano; servirebbe il giorno che stessero accanto.
