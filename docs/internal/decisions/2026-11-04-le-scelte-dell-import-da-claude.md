# Le scelte dell'import da Claude che la nota del 30 ottobre non diceva

- **Data**: 4 novembre 2026
- **Stato**: scelte di una sessione di lavoro di Carmine dentro la nota approvata, lette dal revisore sulla #453. Una sola è una decisione di Carmine: la foto da scegliere del punto 11 (in chat al master, 5 novembre 2026: «Sì, la ferma»). L'altra domanda andata a lui, la riga della versione, è in `2026-11-04-la-bozza-che-viene-da-un-file.md`
- **Classificazione** (`CLAUDE.md` §5): **(b)**, dentro `2026-10-30-una-pagina-scritta-con-claude.md` §3 e §4: nessun meccanismo nuovo, nessun tocco al server

## 1. Che cosa fa l'import, oltre a quello che la nota dice

1. **Un numero di foto che la bozza non ha viene tolto**, e il campo resta da scegliere come uno vuoto. Claude non ha visto nessuna libreria: quel numero viene da un esempio o è inventato, e la foto di un altro finirebbe sulla pagina senza che nessuno se ne accorga. La nota dice solo che Claude lascia vuoti i campi `media`, salvo quelli della bozza: qui non ci si fida della sua parola.
2. **Il codice di un altro**: chi non ha `Content.EmbedCode` non importa un file che toglie il codice di un interattivo della bozza. Il server quel salvataggio lo prende, poi rifiuta come «codice nuovo» ogni salvataggio che lo rimette (`CodeNotTheirsAsync` legge la riga salvata): Annulla lo farebbe vedere e non riuscirebbe mai a salvarlo. Le istruzioni lo dicono a Claude.
3. **Un blocco della bozza che il file non cambia non viene controllato**: può essere nella forma di una versione vecchia, o un blocco che non si propone più. Nemmeno i suoi link e le sue foto vengono segnalati, perché non li ha scritti il file.
4. **La versione di un blocco tenuto**: se il file ne cambia le `props`, le ha lette lo schema di oggi, e il blocco prende la versione di oggi; altrimenti tiene la sua.
5. **Un valore che Claude non può sapere** (nota §3.5): vale solo sui blocchi di dati, per un testo obbligatorio vuoto o mancante. Su ogni altro blocco un testo obbligatorio mancante è un rifiuto.

## 2. Le sezioni del modello

6. **Una sezione bloccata** può cambiare solo le `props` dei suoi blocchi. Il suo `title` torna quello della bozza invece di rifiutare il file, perché le istruzioni chiedono un titolo su ogni sezione. Forma e blocchi cambiati sono un rifiuto. ⚠️ **L'ordine fra le sezioni sorelle non si controlla**: nell'editor una sezione bloccata non si sposta, un file sì. L'ordine delle sezioni non è la loro forma, e la pagina resta approvata da una persona che legge il confronto.
7. **Una sezione nuova con una `key`**: la tiene solo se il modello ha quella chiave e nessun'altra sezione la usa; altrimenti la chiave si perde e la sezione è libera. Una sezione tenuta riprende la `key` della bozza, qualunque cosa dica il file.
8. **`required`, `locked` e `allowedBlocks`** nel file si tolgono, perché una pagina non si toglie da sola i vincoli.

## 3. I link

9. **Che cosa si segnala**: un link vuoto, un indirizzo del sito che le istruzioni non elencavano, e tutto quello che non è né l'uno né un indirizzo intero (`about/team`, `www.example.org`, `javascript:…`). Si considerano interi `https://`, `http://`, `mailto:`, `tel:` e un'ancora `#…`. Si segnala e non si rifiuta (nota §3.5).
10. **Se la lista degli indirizzi non si può leggere**, l'import non parte e dice di riprovare: i link «si segnalano», ma non si può segnalare contro niente, e non è stato scritto ancora nulla.

## 4. Dove stanno le cose

11. **L'elenco di quello che resta da fare** sta nel `localStorage` di chi importa, per riga: regge un ricaricamento, ma un altro non lo vede. Oggi una foto non scelta non ferma la pubblicazione e non disegna niente, quindi fino alla 3.15 l'elenco è l'unica parola su quel buco. **Decisione di Carmine**, alla domanda se una foto rimasta da scegliere debba fermare la pubblicazione, in chat al master il 5 novembre 2026: «Sì, la ferma». Nel codice con la 3.15 (design §5.9, il controllo alla pubblicazione), non in questa PR, e vale anche per le pagine scritte a mano. ⚠️ Il server non legge gli schemi (`CLAUDE.md` §2), quindi da solo non sa quale `mediaId` è obbligatorio: la 3.15 dice come lo sa, nella sua nota, e se serve un meccanismo nuovo è una domanda per Carmine.
12. **Le parole della riga** sono «Importata da Claude», come le cita la nota §3.3, e non «importata da un file» della domanda 6.
13. **Un 409 sull'import ferma l'editor** come un conflitto dell'autosalvataggio: scaricare di nuovo comporrebbe dalla stessa riga vecchia, e il file dopo prenderebbe un altro 409.

## Da portare nel piano

- **§9.3** (modello dei contenuti), accanto alle righe della nota del 30 ottobre: i punti 1, 2, 6 e 9, che sono le regole dell'import; e che un file non sceglie mai `renderMode`, `span`, `frozen` di un blocco tenuto né la `key` di una sezione tenuta.
- **La fase 3** (design `12-design-editor-fase-3.md`, §5.9 e la riga 3.15 del §7): una foto rimasta da scegliere ferma la pubblicazione, per ogni pagina, importata o scritta a mano (Carmine, 5 novembre 2026: «Sì, la ferma»). La 3.15 dice nella sua nota come il server sa quale `mediaId` è obbligatorio.
