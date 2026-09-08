# Modificare una riga dalla lista, senza aprire il form

**Data:** 8 settembre 2026 — G13, chiesto da Carmine eseguendo la parte 1 della demo
**Stato:** ⚠️ **proposta, da decidere.** Niente è stato scritto.
**Perché esiste:** regola (c) di piano §16.E. La lista generica è **un meccanismo della spina
dorsale** — la usano tutte le schermate del back-office — e insegnarle a scrivere la cambia per
tutte insieme.

## Che cosa ha chiesto Carmine

> «In `/staff/wd/menu`, *Order* e *Visible to* devono essere editabili dalla tabella direttamente.»

È una richiesta ragionevole e riguarda **una lista sola nella pratica** (l'ordine del menu si
sistema guardando l'insieme, non una riga alla volta), ma il pezzo che la esaudisce è generico.

## Il punto che va deciso, ed è tecnico

La lista non ha con che scrivere. `DataList` riceve una **proiezione** — `MenuItemListDto` — e il
motore CRUD scrive con il **DTO di scrittura completo** (`PUT` sostituisce la riga). Cambiare
`sort` da una cella vuol dire mandare al server tutta la riga, e la riga la lista non ce l'ha.

Da qui le tre strade:

1. **Il motore impara un verbo generico**: `PATCH /api/<risorsa>/{id}` che accetta **solo i campi
   che la risorsa dichiara** modificabili (`options.InlineEditable = [nameof(MenuItem.Sort), …]`) e
   rifiuta tutto il resto, con lo stesso `rowVersion` e le stesse policy di sempre. Lato client
   `col.number('sort', { editable: true })` e la cella diventa un campo.
   ⚠️ È **un verbo in più** appeso a ogni gruppo `MapCrud` — oggi i verbi a mano sono quattro e il
   piano chiede che ognuno sia giustificato — ma è **uno**, del motore, non uno per schermata.
2. **La cella legge il dettaglio e lo riscrive**: al primo click `GET /api/menu/{id}`, cambia il
   campo, `PUT`. Zero API nuove; due viaggi per ogni modifica e una finestra più larga in cui
   qualcun altro può aver salvato — che però il `rowVersion` intercetta, rispondendo 409 come già fa.
3. **Non farlo nella tabella**: una schermata di **riordino** per il menu (trascina le voci, un
   salvataggio solo alla fine). Risolve il caso vero — l'ordine — e non tocca `DataList`; non
   risolve *Visible to*.

## La raccomandazione

**La 2**, e con una riserva onesta.

È l'unica che non allarga la superficie dell'API, e la lentezza in più (un `GET` prima del `PUT`)
non si vede su una riga sola. La 1 è più pulita da usare e più veloce, ma introduce un verbo che poi
esiste per **tutte** le risorse: il giorno che qualcuno lo usa per aggirare una validazione che vive
nel `WriteDto`, la regola «si valida una volta sola» si è rotta in un posto che nessuno guarda.

Se la 2 si rivelasse fastidiosa all'uso — e si vedrebbe subito, sul menu — la 1 resta lì e il
passaggio costa poco, perché la parte client (`editable` sulla colonna) è la stessa.

Quello che porta con sé, qualunque delle due:

- `DataList` che disegna un campo in una cella per **tre soli tipi** — numero, booleano, enumerazione
  — e mai per un testo tradotto o un file: quelli hanno bisogno del form, e una cella che ne apre
  metà è la seconda via di scrivere una riga che §16.6 vieta;
- che cosa succede quando il server rifiuta: la cella torna al valore di prima e la ragione la dice
  un `Notice` in tono errore, che ora esiste;
- il salvataggio **alla perdita del fuoco** e non a ogni tasto, o una tabella di venti righe fa venti
  richieste mentre uno scrive.

## Che cosa succede se si decide di non farla adesso

Niente si rompe: si modifica una voce del menu aprendola, che è come si fa oggi per ogni altra
risorsa. La richiesta resta scritta qui, con il conto già fatto.
