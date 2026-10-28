# Il pubblico di un blocco: i blocchi dello staff su una pagina per tutti

- **Data**: 28 ottobre 2026
- **Stato**: Decisa da Carmine in chat al master, 28 ottobre 2026
- **Chi**: una sessione di lavoro di Carmine (issue #362, fase 2 di `2026-10-22-il-piano-dopo-la-prova-dello-staff.md`, XVIII)
- **Classificazione** (`CLAUDE.md` §5): **(b)**, estensione di due meccanismi del nucleo — il registro dei blocchi
  (`BlockRegistry`, `IBlockDescriptor`) e la validazione del salvataggio (`ContentWriteDtoValidator`) —, quindi una PR del
  nucleo a sé con questa nota.

## Che cosa serve

Alcuni blocchi hanno senso solo per lo staff: le code (`myWork` «Che cosa aspetta me», `flightops.reviewQueue`,
`flightops.openIssues`, `events.staffQueue`, `training.trainerQueue`, `training.approvalQueue`). Su una pagina la cui
visibilità è **pubblica** o **membri** chi legge non vede niente di utile, e chi compone non lo sa. Carmine (XVIII): nella
tavolozza restano, **in grigio e non selezionabili, con una riga che dice perché**; e **il server li rifiuta** su una pagina
così, perché una regola solo nel browser non è una regola.

## Perché nessun meccanismo esistente basta

- `BlockRegistration.permission` (TypeScript) toglie dalla tavolozza un blocco che **chi scrive** non può aggiungere: è un
  fatto del lettore, e il blocco sparisce. Qui il fatto è della **pagina** (la sua visibilità), e il blocco deve restare
  visibile e spento, come quello che un modello vieta (`rule.allowedBlocks`).
- `allowedBlocks` dei modelli è per sezione e lo scrive chi fa il modello: una pagina senza modello non ha regole, e
  ripeterlo su ogni modello è proprio la copia che `CLAUDE.md` §2 vieta.
- Il server oggi sa di un blocco solo tipo, versione, natura, `AlwaysLive`, `SharedFor`, `ProviderKey`. Non c'è un posto
  dove dire «questo blocco è per lo staff».

## Che cosa si tocca (la proposta)

1. **`IBlockDescriptor` riceve un campo `Audience`** (il `Visibility` del nucleo, predefinito `Public`): la visibilità **più
   larga** di una pagina su cui il blocco ha senso. Le code dichiarano `Audience: Visibility.Staff`. Si dichiara **una volta,
   in C#**, accanto a `AlwaysLive`, che è già un fatto del tipo letto da entrambi i lati.
2. **`/api/me` lo pubblica** in `BootstrapBlock` (come `AlwaysLive`): il browser lo legge da lì, **non** da un secondo campo in
   TypeScript. Una sola dichiarazione, due lettori (`CLAUDE.md` §2: «tutto quello che la SPA deve sapere all'avvio… `/api/me`»).
3. **La validazione del salvataggio** (`ContentWriteDtoValidator`, accanto a `ValidateEnvelope`): un blocco il cui `Audience` è
   più stretto della visibilità della riga (`Public` < `Members` < `Staff` < `Department`) è rifiutato con
   `errors.body.blockAudience`, sotto il percorso del blocco (`body.sections[0].blocks[2]`), così l'editor lo mette accanto al
   blocco. Vale anche quando si **cambia la visibilità** di una pagina che già li contiene: la pagina non si salva più come
   pubblica finché il blocco c'è, e l'errore dice quale.
4. **La tavolozza** (`BlockPalette`) riceve la visibilità della riga dal form e spegne le voci con un `Audience` più stretto,
   con il `title` tradotto `content.editor.notForThisAudience` («Solo per le pagine dello staff: cambia la visibilità della
   pagina per aggiungerlo»), come già fa per un blocco vietato dal modello. Fuori da `cms_contents` (la descrizione di un
   evento, il briefing di un tour) non c'è una visibilità della riga e la tavolozza non spegne niente.
5. I moduli dichiarano il campo sui propri descrittori (`FlightOpsModule`, `EventsModule`, `TrainingModule`): una riga
   ciascuno, nessun handler, nessuna regola del modulo.

I modelli (`is_template = true`) non hanno un pubblico: lì il controllo non si fa, si fa sulla pagina che nasce dal modello.

## Alternative

- **A. `staffOnly: true` solo nel TypeScript**, controllato solo nel browser. Scartata: Carmine vuole il rifiuto del server.
- **B. Un booleano `StaffOnly` in C#** invece di `Audience`. Più semplice, ma la stessa domanda tornerà per i blocchi «miei»
  (`events.myEvents`, `flightops.myTours`, `training.myTraining`, `welcome`) su una pagina **pubblica**, dove un visitatore
  anonimo non ha niente da vedere; con `Audience` è un valore in più, non un secondo campo.
- **C. `Audience` sul descrittore, pubblicato in `/api/me`** (raccomandata, è quella sopra).

## Le domande per Carmine

1. **Va bene `Audience` (alternativa C)** invece di un semplice «solo staff»?
2. **Quali blocchi.** Proposta: `Staff` per le sei code sopra; e, se dici sì, `Members` per i blocchi «miei» (`welcome`,
   `myDepartments`, `events.myEvents`, `flightops.myTours`, `training.myTraining`), che su una pagina pubblica sarebbero
   spenti nello stesso modo. Se preferisci restare a quello che dice la issue, solo i sei `Staff`.
3. **Le pagine che li hanno già**: con il rifiuto sul salvataggio, una pagina pubblica che oggi contiene una coda non si
   salva finché il blocco non si toglie (l'errore dice quale). Raccomandazione: va bene così — è la regola — e lo scrivo nelle
   note di rilascio.

## Risposta di Carmine

Decisa da Carmine in chat al master, 28 ottobre 2026: «Sì, come consigli», sulle raccomandazioni del master. Nella chat della
sessione di lavoro aveva prima risposto «Sì, tutta la proposta», che metteva a `Members` anche i blocchi «miei»; messo davanti
alle due risposte, ha scelto: «Public, come il master». Quindi:

1. **`Audience`** sul descrittore C#, pubblicato da `/api/me` (alternativa C).
2. **Solo le sei code** hanno `Audience: Staff`: `myWork`, `flightops.reviewQueue`, `flightops.openIssues`,
   `events.staffQueue`, `training.trainerQueue`, `training.approvalQueue`. I blocchi «miei» (`welcome`, `myDepartments`,
   `events.myEvents`, `flightops.myTours`, `training.myTraining`) **restano `Public`**: una home pubblica con `welcome` deve
   continuare a salvarsi. `Members` resta un valore possibile del campo, che oggi nessun blocco dichiara.
3. **Le pagine che hanno già una coda**, se pubbliche o per i membri, non si salvano finché il blocco c'è, e l'errore dice
   quale; si scrive nella PR e nelle note di rilascio.

La risposta è stata data in chat e non in un commento: la nota è di una sessione di Carmine, non del collaboratore.

## Da portare nel piano

- §9.3 (modello dei contenuti) e §16 (meccanismi generici): il descrittore di un blocco dichiara il suo pubblico (`Audience`);
  il salvataggio rifiuta un blocco più stretto della visibilità della riga; la tavolozza lo mostra spento con la ragione.
- La tabella di `CLAUDE.md` §2 alla riga del blocco (registrato in due metà): il pubblico sta nella metà C#, pubblicato da
  `/api/me`.
