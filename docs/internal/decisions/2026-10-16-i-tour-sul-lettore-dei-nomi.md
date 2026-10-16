# I tour sul lettore dei nomi e sulla persona del nucleo

**Data:** 16 ottobre 2026 — una sessione di lavoro di Carmine sul modulo dei tour (Flight Ops)
**Stato:** **decisa**: è il passaggio che la nota di E10m ([`2026-10-11-un-solo-lettore-dei-nomi`](2026-10-11-un-solo-lettore-dei-nomi.md),
§5) affidava a «una sessione di Carmine», e quello che la nota della persona cancellata
([`2026-09-29-la-persona-cancellata-nel-nucleo`](2026-09-29-la-persona-cancellata-nel-nucleo.md), §7 e «Da portare nel piano») lasciava
alla stessa sessione. Nessuna decisione nuova sui due meccanismi; la nota c'è perché la PR tocca un test del nucleo
(`MemberNamesTests.cs`) e un commento del nucleo (`web/src/shared/ui/people.ts`), come per la formazione
([`2026-10-13-la-formazione-sul-lettore-dei-nomi`](2026-10-13-la-formazione-sul-lettore-dei-nomi.md), A13f) e gli eventi
([`2026-10-14-gli-eventi-sul-lettore-dei-nomi`](2026-10-14-gli-eventi-sul-lettore-dei-nomi.md), E4d).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: i tour usano due meccanismi già decisi del nucleo, `MemberNames.ReadAsync` e
`personName` / `col.person`.

## 1. Il server: `PirepReview.NamesAsync` è il lettore del nucleo

`PirepReview.NamesAsync` risponde `MemberNames.ReadAsync(hub, vids, cancellationToken)`. Resta, come una riga, perché ha sette chiamate
nel modulo (due nella revisione, la coda, i ban, i piloti, i validatori, le segnalazioni sulle tratte), come `TrainingPeople.NamesAsync` e
`EventsPeople.NamesAsync`. `PirepReview.Member` resta, con il suo nome vuoto che diventa `null`.

Non cambia nessuna risposta: i tour erano **la copia più stretta**, quella su cui il lettore è stato modellato (un VID sotto uno lasciato
fuori, un nullo lasciato fuori). L'unica differenza è che con nessuno da chiedere il lettore non va al database, e la risposta era
già vuota.

## 2. Che cosa esce da `MemberNamesTests.cs`

Ne esce **la riga dei tour**, come la nota di E10m §5 diceva:

- **il test gemello, intero**: confrontava il lettore con le copie dei moduli, e quella dei tour era l'ultima. E4d (#263) è entrato
  prima di questa PR e ha lasciato solo la riga dei tour (`TheReaderAnswersWhatTheToursAnswer`); dopo il passaggio il confronto
  proverebbe il lettore con sé stesso, e il test se ne va con il suo `Sorted`, come la nota di E10m prevedeva quando non resta nessuna
  copia. Restano i test del lettore da solo: il nome unito e tagliato, il nome vuoto, il bianco ai bordi, le tre differenze;
- **da `AVidBelowOneIsNeverNamedEvenWhenAUserRowHasIt`**: la riga che diceva che i tour non nominano un utente sotto uno. È vero ancora,
  ma ora è il lettore che lo dice, e la riga sopra lo prova;
- il riassunto della classe dice che nessuna copia è rimasta, e chi è passato quando.

Resta `AnEmptyNameIsKeptAndEachModuleStillReadsItItsOwnWay`, che legge ancora `PirepReview.Member`.

## 3. Il browser: la copia `memberName` se ne va

`memberName(member, t?)` di `web/src/modules/flightops/api.ts` e la sua parola `flightops:people.erased` («Deleted person», «Persona
cancellata») erano la copia dei tour da cui è nato il pezzo del nucleo (design M2 §10.0). Se ne vanno:

- **le pagine** (la revisione, una segnalazione, i validatori, un pilota) scrivono una persona con `personName` del nucleo: la stessa
  frase, con la parola `people.deleted` del nucleo;
- **le tre liste generate** (la coda della revisione, le segnalazioni sulle tratte, i ban) non calcolano più `pilotName` e
  `assignedToName` nella query: la colonna è `col.person('pilot')` e `col.person('assignedTo')`, sul membro come lo manda il server.
  **Una cosa cambia a schermo, ed è quella voluta**: nella query non c'era `t`, e in queste liste una persona cancellata restava un
  numero negativo; ora dice «Persona cancellata», come in ogni lista del nucleo, della formazione e degli eventi. Le chiavi delle
  intestazioni seguono il campo (`fields.pilot`, `fields.assignedTo`), con le stesse parole;
- **la tabella dei conteggi di un validatore** (i validatori, per tour) chiedeva `memberName` solo per chi era fra i validatori, e
  scriveva il VID da solo per gli altri: un VID cancellato restava un numero. Ora passa sempre da `personName`.

Il commento di `web/src/shared/ui/people.ts` che nominava la copia dei tour lo dice al passato.

## 4. Che cosa resta fuori

- Gli altri tre punti della stessa richiesta (il fuso dei trigger, il riepilogo fermato a metà, `[ErasedWith]`) sono in PR loro, una
  per idea.

## Da portare nel piano

- `05-design-m2.md`, la revisione dei PIREP (il nome letto dai membri) e §10.0 (la parola della persona cancellata): **dal 16 ott 2026**
  i tour leggono i nomi con `MemberNames.ReadAsync` del nucleo e scrivono una persona con `personName` / `col.person`;
  `flightops:people.erased` non c'è più.
- `00-piano-di-progettazione.md` §16, la riga del lettore dei nomi (nota di E10m): i tour sono passati, ultimi; nessun modulo ha più una
  copia, e il test gemello se n'è andato. Versione e changelog.
