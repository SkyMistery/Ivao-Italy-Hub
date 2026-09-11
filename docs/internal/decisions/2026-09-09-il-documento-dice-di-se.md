# Il documento dice di sé, esce quando decidi, e si porta via su carta

**Data:** 9 settembre 2026 — tre richieste di Carmine nello stesso messaggio, mentre si parlava di
come si compone una pagina
**Stato:** la **1 è decisa e non ancora costruita**; la **2 e la 3 sono parcheggiate** («2 e 3
vediamoli dopo»). Questa nota esiste perché nessuna delle tre viva solo in chat: `CLAUDE.md` dice che
una decisione non sta in una conversazione, e la 1 è già decisa.
**Perché esiste:** la 1 è regola (b) di piano §16.E — estende la pubblicazione, che c'è. La **2 è (c)**:
serve un meccanismo che non c'è. La 3 è (b), piccola, con una trappola dentro.

## 1. Il piè di pagina di un documento — **deciso**

> «Deve essere alla fine del documento, poi chi edita sceglie se mostrarlo o no, ma sempre lì deve
> essere.»

Quindi: **lo disegna il renderer**, in fondo, per `kind = Document`; non è un blocco che qualcuno
posiziona. L'unica scelta di chi edita è un interruttore, mostra sì o no.

Che cosa dice: **chi ha pubblicato**, **quando**, e — se chi edita lo ritiene opportuno — il **ciclo
AIRAC**.

### La notizia buona: la metà esiste già

`cms_content_versions` ha da sempre `version`, `changelog`, `published_at` e **`published_by`**, e
`ContentPublishService` li scrive a **ogni** pubblicazione. Il DTO pubblico porta già `Version` e
`PublishedAt` sul filo.

Manca quindi molto meno di quanto sembri:

- **chi**: il VID è nella riga, non nel DTO pubblico. Un campo, e il nome lo risolve la staff
  directory che c'è;
- **quando** e **quale versione**: già sul filo, basta disegnarli;
- **l'AIRAC**: l'unica cosa nuova, ed è **una colonna** — ma va sulla **versione**, non sulla riga del
  documento. Un ciclo AIRAC è una proprietà di *quella pubblicazione*, non del documento in eterno; e
  «se lo ritiene opportuno» vuol dire opzionale per pubblicazione, che è esattamente la forma del
  `changelog`, già scritto nella finestra di pubblicazione. Una casella accanto a quella.

⚠️ **Non è il ciclo AIRAC di vIPI.** Piano §9.3 dice, e resta vero, «non si prende il ciclo AIRAC di
vIPI: qui la release è la singola pubblicazione». Questo è **un'etichetta facoltativa su una
pubblicazione**, non un meccanismo di release. Se un giorno diventasse il secondo, quella riga del
piano va riaperta apposta.

**Costo:** una migrazione additiva di una colonna, un campo nel DTO, un pezzo di renderer.

## 2. La pubblicazione programmata — **parcheggiata, ed è (c)**

> «Se rilascio un nuovo evento voglio poterne programmare l'uscita pubblica per una certa data.»

Serve un momento nel futuro e qualcosa che agisca a quel momento. Il «qualcosa» c'è: Quartz gira già
con due job (sincronizzazione dei dati di riferimento, invio notifiche).

Le tre domande da decidere **prima** di scrivere una riga, e non sono di dettaglio:

1. **Che cosa esce all'ora X**: la bozza *com'è in quel momento*, o una fotografia presa quando si è
   programmato? Sono due prodotti diversi. La prima è più semplice e più pericolosa (qualcuno tocca
   la bozza il giorno prima ed esce quello); la seconda è più fedele a «programmo questa cosa qui».
2. **E se alla scadenza la pubblicazione verrebbe rifiutata?** Oggi pubblicare può dire di no —
   traduzioni mancanti, un'immagine che i lettori non possono vedere — e `publish-problems` risponde
   già *prima* che qualcuno prema. Ma un job che alle 20:00 trova un problema deve dirlo a qualcuno:
   e quel qualcuno è il servizio notifiche, che c'è.
3. **Si programma anche il contrario?** Un documento che *smette* di essere pubblico a una data.
   Carmine non l'ha chiesto e non si aggiunge da soli, ma è la stessa colonna: meglio saperlo prima.

⚠️ E una nota sull'esempio: in M2 **un evento è un'entità sua**, con le sue date e la sua visibilità.
Quello che si programma qui è *la pagina o la news che parla dell'evento*. Se si vuole programmare
anche la comparsa dell'evento, è una decisione del design di M2 — e sarebbe bene che i due usassero
lo stesso meccanismo invece di due.

## 3. La stampa, solo sui documenti — **parcheggiata**

> «I documenti (e solo quelli) devono essere stampabili con apposito tasto print.»

**Costo: (b).** Un pulsante su una schermata e un foglio di stile di stampa. Nessuna dipendenza
nuova e nessun PDF generato dal server: la stampa la fa il browser, che è anche il modo in cui la
gente salva in PDF.

⚠️ **La trappola:** `tabs` e `accordion` **nascondono contenuto**. Su carta devono essere tutti
aperti, o la stampa perde testo senza dirlo. Con loro se ne vanno intestazione, barra laterale e
navigazione del piè di pagina, e i colori vanno forzati chiari. È il tipo di cosa che si verifica
solo guardando una stampa vera.

E «solo i documenti» è una regola da scrivere: la mette il renderer sul `kind`, non un pulsante che
qualcuno si ricorda di aggiungere.

## Come procedere

Le tre sono un tema solo — *il documento pubblicato dice di sé, esce quando decidi, e si porta via su
carta*. La **1** si può costruire quando si vuole: è decisa e non ha domande aperte. La **2** ha
bisogno delle tre risposte qui sopra prima del codice. La **3** si fa insieme alla 1, perché toccano
lo stesso renderer.
