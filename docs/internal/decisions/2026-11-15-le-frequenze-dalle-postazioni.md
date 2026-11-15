# Le frequenze dalle postazioni ATC: che cosa il design non diceva

**Data:** 15 novembre 2026, PR 3.14 della fase 3 (ramo `feat/atc-frequencies`; `12-design-editor-fase-3.md` §5.8, riga 3.14 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.8 ha già deciso (approvato da Carmine il 31 ottobre 2026, §9,
risposta 6: i valori si **copiano**, con la segnalazione e «Riallinea»). Dicono dove sta e come si legge quello che il design vuole. Il
punto 4 è un'interpretazione del testo del design che la revisione deve guardare.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design. Il §5.8 dice «nota sua: no»; questa nota c'è per i punti che non scrive e
che il codice ha dovuto scegliere, come le note della 3.10–3.13. Il punto 3 è un'estensione del form generato, caso (b), dentro la PR
perché la fase è tutta nucleo (come `Dialog wide` nella 3.12).

## 1. La colonna

- `ref_ivao_atc_positions.frequency`, `varchar(8)` nullable, **migrazione additiva** di `HubDbContext` (`AddAtcPositionFrequency`).
  Testo e non numero: è **scritta come la legge un controllore**, con tre decimali (`118.700`), la stessa forma della lettura di chi è
  in frequenza (`IvaoWhazzup`, `"0.000"`) e di quello che la riga del blocco tiene già. Otto caratteri bastano per ogni frequenza
  aeronautica; una più larga, zero, negativa o non numerica è nessuna frequenza (`IvaoAtcPositionReader`), e la riga resta una
  postazione.
- La riempie il sincronismo della notte (`RefDataSyncJob`), che già riscrive ogni riga. **Finché non è passato è vuota** (§5.8): niente
  `UPDATE` dal `raw_json` nella migrazione, che sarebbe SQL scritto a mano per un giorno di attesa; il pannello lo dice (punto 5).
- `AtcPositionDto` porta `Frequency` **in fondo e facoltativa** (`= null`): i test di Eventi costruiscono le loro postazioni finte con
  cinque valori, e un parametro obbligatorio li avrebbe rotti — sono file di `dalberone`, che questa PR non tocca. Il DTO letto dal
  database la ha sempre. I test del nucleo che confrontavano un DTO intero (`AtcPositionTests`) ora scrivono anche la frequenza.

## 2. La lettura

- `GET /api/atc/positions?q=`, `Core/Ivao/DivisionAtcPositionEndpoints.cs`, l'indirizzo del §5.8, dietro **`Content.Edit`** («per chi
  scrive contenuti»), etichetta OpenAPI `Reference` come aeroporti e tipi d'aereo. ⚠️ **Un endpoint scritto a mano in più** nel
  perimetro IVAO del nucleo: il conteggio del §16.6 lo registra.
- **Risponde l'elenco intero della divisione**, ordinato per nominativo, e `q` lo restringe per nominativo o nome, in qualunque
  maiuscola. È `IAtcPositionDirectory.OfDivisionAsync` filtrato in memoria, **senza un metodo nuovo nell'interfaccia**: i test di
  Eventi la implementano, e un metodo in più li avrebbe rotti per duecento righe (232 in Italia il 25 settembre 2026).
- **L'editor la legge intera, una volta** (quindici minuti di cache; il sincronismo è di notte), senza `q`: per confrontare le righe
  serve la postazione di ognuna, e il campo filtra da sé quello che offre. `q` resta per chi vorrà una ricerca su una divisione più
  grande; il test d'integrazione lo prova.
- ⚠️ Chi scrive il briefing di un tour senza `Content.Edit` non riceve i suggerimenti né la segnalazione, e scrive le righe a mano come
  prima: la stessa regola del lettore degli indirizzi del sito (`GET /api/content/addresses`). Il blocco non cambia per lui.

## 3. Il form: un suggerimento che copia accanto a sé

- **`Suggestion.fills`** (`shared/forms/schema.ts`): quello che scegliere un suggerimento **dalla lista** scrive nei campi **accanto**,
  nello stesso oggetto, per nome. `Suggest` dice la scelta (`onChoose`, col puntatore o con Invio) e il caso `suggest` di `SchemaForm`
  scrive i campi fratelli con `setValue`. **Scrivere a mano lo stesso testo non copia niente**: un nominativo digitato è testo del
  campo, e la riga resta com'è (la segnalazione del punto 5 c'è per questo).
- L'annotazione **`atcPosition`** sul nominativo e i suoi aiutanti (`suggestAtcPositions`, `namesAtcPositions`, `atcPositionListOf`),
  come `address` e le collezioni: lo schema dice che cosa è il campo, chi disegna il form (`BlockProperties`) lo trasforma in
  suggerimenti. Raggruppati per FIR (duecento righe senza titoli sono un muro), con nome e frequenza nell'etichetta.
- Che cosa si copia, solo se la riga ha il campo: il **nominativo** come lo scrive IVAO, il **nome** se IVAO ne dà uno, il **tipo** se è
  fra quelli della tabella (gli otto di IVAO lo sono tutti), la **frequenza** quando c'è — una postazione non ancora sincronizzata non
  scrive una frequenza vuota sopra quella scritta a mano.

## 4. Il nome della postazione ⚖️

- Il §5.8 dice che «il nome della postazione, il tipo e la frequenza si copiano nella riga», ma la riga di `frequencyTable` non aveva un
  nome (§1 del design: nominativo, frequenza, tipo, CPDLC, rating). **La riga ha un campo `name` facoltativo**, testo non tradotto come
  il nominativo («Malpensa Tower» è quello che si dice in frequenza), dopo il nominativo.
- **La tabella disegna la colonna «Nome» solo se almeno una riga ne ha uno**: una tabella scritta prima, o una che non lo vuole, si
  disegna con le colonne di sempre. Nessun corpo si riscrive (principio 4).
- L'altra lettura possibile era «il nome» = il nominativo come lo scrive IVAO, senza un campo nuovo. Scelta la prima perché è la lettera
  del design approvato; se Carmine preferisce l'altra, si toglie un campo e una colonna e la copia resta.

## 5. La segnalazione e «Riallinea»

- Sopra il form del blocco, un avviso **per riga** che IVAO ora descrive altrimenti, con quello che cambia («Frequenza: 118.700 qui,
  118.705 su IVAO») e il pulsante «Riallinea», che ricopia quella riga e lascia il resto (CPDLC, rating, nota) com'era. È un passo suo
  per l'annulla, e il form si ridisegna con la riga copiata.
- **Che cosa è diverso**: il nominativo scritto in un'altra maiuscola, il tipo, la frequenza **come numero** (`118.7` scritto a mano è
  `118.700`), il nome **solo se la riga ne ha uno** — una tabella scritta prima della 3.14 non ha nomi, e ogni sua riga sarebbe
  segnalata. Una postazione che IVAO non elenca non è segnalata: è dello staffer.
- **Nessuna modifica da sola**: aprire il blocco non cambia la pagina; la riga cambia solo con il pulsante, e la pagina pubblicata solo
  con una nuova pubblicazione.
- Finché il sincronismo non ha portato le frequenze (postazioni elencate, nessuna con la frequenza), un avviso lo dice: le postazioni
  copiano nome e tipo, e la frequenza si scrive a mano.
- Le parole nominano IVAO, quindi stanno nel gruppo `atcPositions` dei file di lingua del nucleo, accanto a `belowMinimum`.

## 6. Le istruzioni per Claude

⚠️ **L'impronta `rules` cambia**: lo schema di `frequencyTable` ha `name` e l'annotazione del nominativo. Un file per Claude scaricato
prima di questa PR è rifiutato (`content.import.rulesChanged`), e le note di rilascio lo dicono, come per la 3.9–3.13. La parte fissa
delle istruzioni non cambia: Claude non sceglie postazioni, scrive le righe.

## Da portare nel piano

- **§16** (meccanismi generici): il form generato con il suggerimento che copia accanto a sé (`Suggestion.fills`, solo sulla scelta
  dalla lista) e l'annotazione `atcPosition`, trasformata in suggerimenti da chi disegna il form come `address` e le collezioni.
- **§16.6**: un endpoint scritto a mano in più nel perimetro IVAO del nucleo, `GET /api/atc/positions` (dietro `Content.Edit`).
- **§10** (integrazioni con le API IVAO): `ref_ivao_atc_positions.frequency`, scritta con tre decimali, dal sincronismo della notte;
  `AtcPositionDto.Frequency`.
- **§9.3** (il modello dei contenuti) o il catalogo dei blocchi: la riga di `frequencyTable` ha `name`, facoltativo e non tradotto; la
  colonna si disegna solo se una riga ne ha uno.
- **§13**: la PR 3.14 della fase 3 dell'editor; con la 3.15 già unita, la fase 3 è chiusa.
- Il §5.8 del design, «Com'è andata»: questa nota.
