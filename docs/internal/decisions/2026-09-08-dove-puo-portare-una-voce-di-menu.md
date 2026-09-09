# Dove può portare una voce di menu

**Data:** 8 settembre 2026 — G13, richiesta nata eseguendo la parte 1 della demo
**Stato:** **decisa da Carmine l'8 settembre 2026** — insieme chiuso: pagine (anche bozze) +
schermate + link in uso, di **tutti** i dipartimenti — e costruita lo stesso giorno. In fondo c'è
che cosa è costata.
**Perché esiste:** regola (b) di piano §16.E portata al limite. Il pezzo che la esaudisce esiste già
(il campo suggerito, nato per lo `slug` di pagine e news), ma **rendere l'insieme chiuso non è
suggerire**: è una regola di validazione nuova sul server, e cambia che cosa un menu può contenere.

## Come ci si è arrivati, in tre passi

Sono tre messaggi di Carmine nella stessa serata, e il terzo è quello che conta.

1. > «Tutto ciò che nel sito è editabile e ha un link deve avere, per l'address, lo stesso
   > meccanismo introdotto per documenti e news, ovvero il sistema deve proporre l'indirizzo.»

2. > «Secondo me per queste voci è meglio se suggerisse i link delle pagine create nel sito (divise
   > per dipartimento che le ha create) con la possibilità di scrivere per cercarne uno oppure
   > inserire link esterni.»

3. > «Si potrebbe proprio **bloccare** quel campo ai soli elementi suggeriti e aggiungere tra i
   > suggeriti anche tutti i link della pagina `/staff/wd/links`: in questo modo i link delle voci
   > in menu sono blindati e possono essere o nel sito stesso o elencati nella pagina links, in
   > questo modo non c'è possibilità di avere link a cose distribuiti per il sito.»

## Il punto, e non è il menu

Il menu è l'occasione, non il motivo. Il motivo è la frase finale: **ogni indirizzo che esce dal
sito deve vivere in una tabella sola.** Un menu che accetta qualunque URL è un sito con indirizzi
sparsi dentro, ognuno dei quali è una cosa che nessuno mantiene: il giorno che il forum cambia
dominio si va a cercarli uno per uno, e quello nel menu lo si trova per ultimo.

Con l'insieme chiuso, quel giorno è **una riga di `cms_links`**, e il menu la segue.

## Che cosa c'è dentro l'insieme

Tre gruppi, e nient'altro:

- **le pagine del sito** — `cms_contents` con `kind = Page`, template esclusi. **Anche le bozze**, di
  proposito: scrivere la voce prima di pubblicare la pagina è come si costruisce un menu davvero, e
  la voce può aspettare spenta finché la pagina non esce. Nel campo una bozza lo dice, accanto al
  titolo;
- **le schermate dell'applicazione** — `/`, `/calendar`, `/news`, `/documents`, `/search`,
  `/contact`. Non sono righe e non lo saranno mai: sono rotte del router;
- **i link della libreria** in uso — `cms_links` con `is_active = true`. Un link ritirato non è più
  un indirizzo di questo sito, e una voce che ci puntava viene rifiutata alla prima riscrittura.

Di **tutti** i dipartimenti, ed è gratis: il menu appartiene al dipartimento che possiede il sito,
quindi chi può modificarlo è un coordinatore web o un direttore — e quelli
(`RolePermissionMatrix.ReachesEveryDepartment`) raggiungono ogni dipartimento. La lista generica non
li restringe, quindi i gruppi per dipartimento sono veri e non un gruppo solo.

## Dove vive la regola

**Sul server**, in `MenuItemWriteDtoValidator`, come tutte le altre: il campo bloccato nel client è
una comodità, e una comodità non è una regola (piano §16.6 — si valida una volta sola, e lo fa il
server). La regola è una `MustAsync` che risponde `errors.menu.pathNotAllowed`, e sta **dopo** il
controllo di forma che c'era già, che resta il primo: uno `javascript:` non deve arrivare a una
query.

Le due letture delle tabelle passano da `CrudSource.BackOffice`, cioè **oltre** il query filter: la
domanda è se la destinazione **esiste**, non se chi scrive la voce ha il diritto di vederla.

⚠️ **Una costante scritta a mano in due posti**, e non si può fare altrimenti: `Screens` nel
validatore e `SITE_SCREENS` in `staff.$dept.menu.$id.tsx`. Una rotta del client non è una cosa che
il contratto OpenAPI possa portare. Tengono la parola i test — uno di integrazione che accetta una
schermata, uno di Playwright che la vede offerta — esattamente come per gli sfondi di una sezione.

## Che cosa si perde

Una voce che porta a un indirizzo esterno **non si scrive più dal menu**: prima si aggiunge alla
pagina Links, poi la si sceglie. È un passaggio in più, ed è il prezzo che Carmine ha chiesto di
pagare.

E le voci **già scritte** che puntano altrove non si rompono — restano nel database e il sito le
disegna — ma la prima volta che qualcuno le salva vengono rifiutate sul campo. È il comportamento
giusto, ed è lo stesso della parola di calendario fuori vocabolario.

---

## Che cosa è costata

- `MenuItemWriteDtoValidator` con una regola in più e l'elenco delle schermate, più
  `errors.menu.pathNotAllowed` nelle due lingue (e, trovate strada facendo, le due chiavi
  `errors.menu.parentInvalid` e `errors.path.invalid` che erano **citate e mai scritte**).
- Il campo suggerito ha imparato a essere **chiuso**: `suggestionsOnly` nell'annotazione, `only` sul
  nodo, il valore che torna quello di prima quando si lascia il campo, e un messaggio diverso quando
  non corrisponde niente — «qui non corrisponde niente», non «vale quello che scrivi».
- `activeLinksQuery`, e `publishedPagesQuery` diventata `menuDestinationPagesQuery` perché ora porta
  anche le bozze.
- I test: uno di integrazione con i tre accettati e i tre rifiutati, uno di Vitest sul campo chiuso,
  uno di Playwright — il browser vero — sui gruppi e sul valore che non resta. Verificati tutti e
  tre rompendo la regola.
- ⚠️ E la scoperta che il banco di prova costruiva le voci con indirizzi inventati: sei chiamate in
  `SiteMenuAndDashboardTests` che ora puntano a una schermata, perché quei test parlano di proprietà
  e di profondità e non di dove porta la voce.

---

## Un difetto che questa decisione ha creato — trovato e chiuso il 9 settembre 2026

⚠️ **L'elenco delle pagine è una richiesta sola di cento righe.** Finché il campo *suggeriva*, cento
era una comodità e chi non trovava la sua pagina la scriveva a mano. Adesso che il campo **decide**,
una pagina oltre la centesima è un indirizzo che **non si può scegliere**: esiste, il server la
accetterebbe, e il form non la offre. Peggio, la casella dice «qui non corrisponde niente», che in
quel caso non è vero.

Trovato eseguendo il giro completo: il banco ha 120 pagine accumulate fra un'esecuzione e l'altra,
117 delle quali stanno prima di `start` in ordine di slug — e `/start`, che è una pagina seminata,
non era nell'elenco. Il test è stato spostato su una **schermata**, che è una costante e non dipende
da quante pagine ci sono, ma il difetto resta.

Le strade, e nessuna è gratis:

1. **Il campo cerca sul server**: quello che si scrive diventa la `q` di una richiesta, invece di
   filtrare in memoria un elenco già scaricato. È la strada giusta e la sola che regge a mille
   pagine — ed è una **nona estensione del generatore di form**, cioè una decisione (§16.E, regola
   (c)): oggi `suggestions` è un elenco, diventerebbe un elenco *che si aggiorna*.
2. **Alzare il tetto**: non si può, cento è già il massimo che il motore lista accetta.
3. **Cambiare l'ordine** (le più recenti prima invece che per slug): sposta il problema senza
   risolverlo, e dà l'impressione di averlo risolto. Scartata.

**Deciso da Carmine il 9 settembre 2026: la prima**, e fatta lo stesso giorno.

### Che cosa è costata

- **La nona estensione del generatore di form**, ed è la più piccola delle nove: `onSuggestSearch`,
  una funzione che il form chiama con il nome del campo e quello che ci si sta scrivendo, **dopo una
  pausa di trecento millisecondi** — la stessa che aspetta la casella di ricerca di una lista, perché
  è lo stesso gesto. Il generatore non sa che cosa farne: chi la riceve è la schermata.
- `menuDestinationPagesQuery(q)` e `activeLinksQuery(q)` passano quel testo come `q` della lista. Il
  server cerca già su titolo e slug per i contenuti, su titolo e indirizzo per i link — cioè
  esattamente le due righe che la voce mostra. Zero endpoint nuovi.
- `keepPreviousData` sulle due query: senza, fra un tasto e la risposta l'elenco resta vuoto per un
  istante, e un elenco vuoto in quella casella si legge «qui non corrisponde niente» — l'unica cosa
  che non deve dire mentre sta chiedendo.
- ⚠️ La richiesta parte **solo mentre la tendina è aperta**. Scegliere una voce scrive un indirizzo
  intero nel campo, e chiedere al server di quello sarebbe una domanda su una cosa già scelta.
- I test: due di Vitest — che il campo riporta quello che si scrive, e che un form **senza** la
  funzione si comporta come prima, perché una schermata deve poter non chiedere niente — e uno di
  Playwright dove il banco risponde con una riga **solo** se la richiesta porta la ricerca, cioè una
  pagina che nessun'altra chiamata restituisce. Verificati tutti e tre rompendo il pezzo che provano:
  il richiamo, la pausa, e il collegamento fra il testo e la query.
