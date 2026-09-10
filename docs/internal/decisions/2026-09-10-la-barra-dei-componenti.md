# La barra dei componenti, e le due cose che ha portato via con sé

**Data:** 10 settembre 2026 — quarto giro sull'editor, subito dopo il terzo. Carmine:

> «Voglio una barra a sinistra con i vari components. Ogni gruppo/sottogruppo di componenti deve
> essere comprimibile/espandibile. I componenti sono aggiunti sempre via codice. Al centro l'editor
> visuale, a destra il campo per editare il singolo componente.»

**Stato:** **fatta.** Questa nota non chiede una decisione: la richiesta era esplicita e tutto quello
che serviva era **(b)** di piano §16.E — estensioni di meccanismi che c'erano. Esiste per le **due
cose che sono state tolte** senza che nessuno le avesse chieste, e che vanno scritte perché una
riga tolta senza una ragione scritta è una riga che torna.

## Che cosa è costata la richiesta, per intero

Nessun meccanismo nuovo, nessun componente fuori dall'elenco chiuso di §8.3, nessuna tabella.

- **Due campi su `BlockRegistration`**: `group` (obbligatorio) e `subgroup` (facoltativo), con i due
  insiemi chiusi accanto. I cinque gruppi **non sono inventati qui**: sono le famiglie che design M1
  §1.2 nomina già, e di cui G3 porta quattro nel titolo e G4 la quinta;
- **`BlockPalette`**, un pezzo della schermata dell'editor accanto a `SectionTree`, `BlockProperties`
  e `PreviewFrame` — non un componente condiviso, quindi **non** entra in
  `shared/ui/catalog.ts`, esattamente come nessuno di quei tre;
- **comprimibile/espandibile**: `AccordionRoot` di Atmosphere, che c'era. ⚠️ Due liste di stato e non
  una, perché gli accordion sono annidati e uno controllato riporta soltanto le voci proprie: con una
  lista sola, aprire *Media* avrebbe chiuso *Contenuto*;
- **le chiavi i18n** dei gruppi in entrambe le lingue, con il test del registry che rifiuta un blocco
  il cui cassetto non ha un nome;
- **tre e2e** che misurano quello che nessun test di unità può vedere: che le tre colonne siano
  davvero **affiancate** a 1440 px, e non tre righe.

«Sempre via codice» è la parte che vale di più ed è quella che non si vede: la tavolozza **è** il
registry disegnato. Non c'è una tabella delle voci e non c'è una schermata dove qualcuno le
riordina, perché sarebbe il secondo posto in cui vive il catalogo (`CLAUDE.md` §2).

## Le due cose tolte, e perché

### 1. La tavolozza dentro la sezione — **tolta**

Ogni sezione dell'outline si portava dietro i 27 blocchi in fila. Con la barra a sinistra diventavano
**due** tavolozze, e nella colonna centrale la seconda occupava più spazio della pagina che si stava
componendo — si vede nella prima delle due schermate allegate alla conversazione, presa prima di
toglierla.

Motivo, in ordine di peso: «una barra a sinistra» **è** la richiesta, e i componenti in due posti la
contraddicono; due tavolozze sono la stessa cosa scritta due volte; e nessun test dipendeva da
quella (la coppia di frecce dell'outline, che è la strada da tastiera, non è stata toccata).

⚠️ **Che cosa si perde:** aggiungere a una sezione che non è selezionata ora costa un clic in più —
prima la sezione, poi il componente. È il modo in cui funziona anche il builder di HQ, ed è il
prezzo di avere un posto solo. Il filtro del template non si perde: la barra **disabilita** invece di
filtrare, e dice perché.

### 2. L'anteprima come posto dove si va — **tolta**

«Al centro l'editor visuale» vuol dire che la pagina è quello che si vede aprendo, non quello che si
raggiunge premendo un tasto. La strada (A) del 9 settembre — comporre guardando la pagina — era già
decisa, ma era dietro un pulsante: cioè la strada scelta era quella che nessuno prendeva.

Adesso la colonna centrale **apre sulla pagina**, e il pulsante porta all'outline. Il pulsante ha
cambiato nome di conseguenza: `Preview` / `Back to editing` non descrivevano più niente, e sono
diventati `Outline` / `On the page`. `backToEditing` è stato tolto dai file di lingua invece di
restare lì.

⚠️ **La conseguenza sui test**: `e2e/full/template.spec.ts` premeva `Preview` per arrivarci. Quel
clic è stato tolto, non riscritto: non c'è più niente da premere.

## Che cosa non è stato verificato

L'editor **non è stato guardato con un vero login**: sta dietro OAuth IVAO e le credenziali non si
toccano. Quello che c'è al posto di un giro a mano sono i tre e2e contro l'API finta di
`e2e/fixtures.ts`, che è come il resto del back-office è verificato, e due schermate prese dallo
stesso browser. **Il giro vero lo fa Carmine.**

## Dove sta il resto

Le regole per chi forka sono in `docs/UI-GUIDELINES.md`, in due punti: che cassetto dichiara un
blocco (nella sezione dei blocchi, perché è un campo della registrazione) e le due regole nuove
dell'editor — tre colonne di cui cambia solo quella di mezzo, e un posto solo da cui si aggiunge un
blocco.
