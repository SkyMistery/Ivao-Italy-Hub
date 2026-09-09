# Comporre una pagina guardandola: anteprima modificabile o tela libera

**Data:** 9 settembre 2026 — chiesto da Carmine dopo la terza esecuzione della demo
**Stato:** ⚠️ **da decidere.** Nessuna riga di codice prima.
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
pannello proprietà; lo si trascina fra due sezioni; lo spazio che occupa si vede perché *è* la
pagina. L'outline resta come seconda vista.

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
