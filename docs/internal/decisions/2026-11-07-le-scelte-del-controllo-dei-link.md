# Le scelte del controllo dei link che il design non diceva

- **Data**: 7 novembre 2026
- **Stato**: scelte di una sessione di lavoro di Carmine dentro il design approvato (`12-design-editor-fase-3.md` §5.9), da leggere in revisione sulla PR 3.15. Nessuna domanda per Carmine; la foto rimasta da scegliere è nella nota `2026-11-07-la-foto-rimasta-da-scegliere`
- **Classificazione** (`CLAUDE.md` §5): **(b)**: il servizio degli indirizzi tolto da `MenuItemWriteDtoValidator` (`SiteDestinations`), `BlockDocumentWalker` e `ContentPublishService`

## 1. Che cosa conta come «porta da qualche parte»

1. **Una schermata dell'applicazione è più delle sei del menu.** `Screens` ne ha sei, ma un pulsante verso `/tours`, `/events/...`, `/training` o `/me` oggi si pubblica ed è normale: rifiutarlo sarebbe una regressione. Vale ogni primo segmento che l'applicazione o un modulo riservano (`ContentAddresses.AllReservedSegments`, la lista che un test confronta già con le rotte). Che cosa c'è dietro `/tours/{slug}` lo sa il modulo, e il nucleo non lo chiede.
2. **Il menu resta com'era**: l'insieme chiuso del 8 settembre, con le bozze che contano. Il servizio è uno e le domande sono due, perché le due cose vogliono risposte diverse: una voce di menu si scrive prima che la pagina esca, una pagina che esce porta a ciò che si può cliccare oggi.
3. **Un indirizzo lasciato indietro arriva**: una pagina spostata tiene il vecchio e rimanda al nuovo (`PreviousPathsJson`). La visibilità è quella della pagina di adesso.
4. **Una pagina che porta a se stessa** non si ferma alla prima pubblicazione: è online quando qualcuno segue il link.
5. **`?` e `#` e l'ultima barra non contano**: `/contact?x=1#form` è `/contact`.
6. **`//altrove.example` è un indirizzo di fuori**, e non si controlla come tutti gli altri.

## 2. La regola della visibilità

7. **La tabella è `VisibilityCeiling`**, quella delle foto, non l'elenco del pubblico di un blocco: dice anche che una pagina di un dipartimento non porta a una pagina «Dipartimento» di un altro.
8. **`/staff/...` ha i lettori dello staff**: è l'esempio del design («una pagina per tutti non porta a una pagina dello staff»), anche se è una schermata e non una riga. **`/me` no**: una pagina pubblica che dice «vai alla tua area» è normale, e chiede l'accesso a chi non l'ha fatto.

## 3. Che cosa si legge

9. **Solo `props`**: `frozen` è quello che ha risposto un provider, non l'ha scritto nessuno. Una stringa qualunque dentro `props` può essere Markdown; un'immagine `![...](...)` non è un link.
10. **Un link rotto in due lingue sono due righe** dell'elenco (`…props.markdown.it`, `…props.markdown.en`), come sono due i valori da correggere.

## 4. Due cose cambiate per farlo reggere

11. ⚠️ **Il seeder pubblica a giri, dopo aver creato tutte le pagine.** `home.json` porta a `/start`, che arriva dopo in ordine di file: con la pubblicazione subito, la home di un'installazione nuova sarebbe rimasta una bozza con una sola riga nel log. Un giro che non pubblica niente è la fine, e il resto si dice nel log come prima.
12. **L'elenco «che cosa manca» dice tutto insieme**: prima si fermava al primo tipo di problema, e con tre tipi in più chi corregge una lingua avrebbe scoperto il link solo dopo. Le chiavi per percorso si sommano; ogni controllo resta quello di prima.

## 5. Che cosa non fa, e si sa

- ⚠️ **Due pagine nuove che si linkano a vicenda** non escono tutte e due al primo colpo: una deve uscire prima senza quel link. È la conseguenza di «contenuto pubblicato» del design.
- **Vale nell'istante della pubblicazione.** Una pagina di destinazione che più tardi si ritira o si restringe non ferma chi porta a lei; e nemmeno un cambio di visibilità senza ripubblicare (`NewReadersProblemsAsync` non guarda i link: il design §6 aggiunge i problemi solo alla pubblicazione e all'approvazione).
- **Solo `cms_contents`**: la descrizione di un evento e il briefing di un tour non passano da `ContentPublishService`.
- **`SITE_SCREENS` nel browser resta di sei**: l'import segnala `/tours` come indirizzo da controllare, e la pubblicazione lo lascia passare. Si segnala e non si rifiuta, quindi non si ferma niente.

## Da portare nel piano

- **§16** (meccanismi generici): `SiteDestinations`, il servizio solo degli indirizzi del sito, con le due domande del menu e della pubblicazione; il controllo dei link alla pubblicazione con `VisibilityCeiling`; `/staff/...` con i lettori dello staff.
- **§9.3** (modello dei contenuti): l'elenco di che cosa manca per pubblicare dice tutti i problemi insieme; un indirizzo lasciato indietro arriva.
- **Il seeder** (design M1 §8.2): le pagine seminate escono a giri, dopo che ci sono tutte.
