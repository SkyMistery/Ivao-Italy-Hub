# Che cosa ha trovato Carmine eseguendo la demo di M1

**Data:** 7 settembre 2026 — dopo il merge della PR #56, eseguendo `tools/demo-m1.md`
**Stato:** quattro difetti (due corretti, due aperti), dodici richieste, quattro decisioni prese
**Dove si lavora:** ramo `m1/g13-fixes`; il tag `v0.2.0-m1` **aspetta** che questa lista sia chiusa

Carmine si è fermato al punto 7 della demo: **i punti 8 e 9 non sono ancora stati eseguiti**, e sono
l'ultimo pezzo di accettazione della milestone.

---

## I quattro difetti

### D4 — Ogni data era mostrata due ore indietro — **corretto** (`6f8217e`)

L'API mandava `2026-09-07T14:22:35.99759`, **senza `Z` e senza offset**. Un browser legge una
stringa così come ora **locale**, quindi `new Date(...)` la spostava dell'offset e la schermata
mostrava «17:35 UTC» per un istante che erano le 19:35 UTC. Riguardava **ogni istante dell'hub** —
liste, audit, calendario, date di pubblicazione, messaggi — perché tutte formattano la stringa che
l'API ha mandato.

I valori nel database erano giusti da sempre: mancava che il **modello** dicesse che sono UTC.
`UtcDateTimeConverter` in `ConfigureConventions`, un posto solo, scrittura non toccata.

Test: `InstantsAreUtcOnTheWireTests` — asserisce sul **testo sul filo**, perché è il parsing ad aver
nascosto il difetto, e su una **lettura**, perché la risposta alla creazione serializza l'entità
ancora nel change tracker e passa anche senza la correzione.

### D2 — Cancellare lasciava la pagina aperta — **corretto** (`fc33848`)

⚠️ **Regressione della correzione del `loader` della stessa mattina.** La mutazione aspettava
`invalidateQueries`, che aspetta il refetch di ogni query **attiva** sotto quella chiave — compresa
quella della schermata che sta cancellando, la cui riga è appena sparita. Il refetch va in 404,
riprova, `onSuccess` non si risolve mai e i callback passati a `mutate` non partono: la riga sparisce
dal database e la schermata resta lì senza dire niente.

Prima che le schermate leggessero la query invece del loader, quella query non aveva osservatori e
invalidarla non rifaceva niente. **Una correzione ne ha scoperta un'altra.**

Corrette tutte e sei le mutazioni di cancellazione: l'invalidazione non si aspetta più.
Test: `web/src/features/menu/mutations.test.tsx`.

### D1 — Il logout non aggiorna la pagina — **aperto**

Si esce e si continua a vedere la versione da loggato finché non si ricarica a mano.

⚠️ **Ipotesi non ancora verificata**: il bootstrap (`/api/me`) è caricato una volta sola nella route
radice con `ensureQueryData`. Se il logout non fa ripartire quel caricamento, la SPA continua a
leggere il payload di prima — cioè **la stessa famiglia** di D2 e della correzione del loader. Da
misurare, non da assumere.

### D3 — Un documento pubblicato con un'immagine non mostra l'immagine — **aperto**

Nessuna ipotesi. Può essere l'indirizzo del file, la visibilità della riga media, o il renderer
pubblico. Va indagato dal filo: guardare che cosa l'API manda e che cosa il browser chiede.

---

## Le dodici richieste

**Editor** (punti 2 e 7 della demo)

1. L'indirizzo (`slug`) lo **propone il sistema** dal titolo, l'utente lo aggiusta. Oggi si scrive a
   mano da zero.
2. **Conferma esplicita** che l'editor ha fatto quello che è stato cliccato. Oggi un salvataggio
   riuscito non dice niente, e un'azione andata a vuoto nemmeno — è come si è persa una sezione
   durante la ricopiatura.
3. **«Cosa manca per pubblicare»**, visibile *prima* di provare. `PublishProblems` esiste ma parla
   **dopo** un rifiuto: serve la lista viva.
4. Editor più intuitivo in generale — da ragionarci alla fine di tutto il resto.

**Grafica** (punto 3)

5. **Un componente di avviso a quattro stati** — rosso errore, giallo avviso, verde successo, blu
   informazione — **usabile ovunque**, deciso da Carmine. ⚠️ È un **quinto componente custom**:
   l'elenco è chiuso (piano §8.3) e M1 si è appena chiusa dicendo «quattro, esattamente i quattro
   previsti». Va scritto nel piano, non aggiunto di straforo.
6. **Calendario**: le voci distinte da una **chip colorata**.
7. **`LiveStatusStrip`**: «troppo piatta, non fa risaltare le informazioni». Lavoro di gerarchia
   visiva, non di decorazione.
8. **Icone dei dipartimenti** — deciso, vedi sotto.

**Calendario** (punto 4)

9. **I tipi di evento sono blindati e decisi centralmente** — vedi sotto.
10. Orario **UTC** e, fra parentesi, **locale**.
11. **Quattro viste**: settimana, mese (come ora), lista settimanale, lista mensile.

**Staff** (punto 6)

12. Una **barra di ricerca nella sidebar**, accanto al ⌘K.

---

## Le quattro decisioni prese

### Il tag aspetta

`v0.2.0-m1` si mette **dopo** questa lista, non su quello che c'è ora.

### Le icone dei dipartimenti: nessuna icona, la sigla è il segno

Oggi tutti e nove i dipartimenti hanno la **stessa** icona (`ShieldCheck`) e li distingue solo la
sigla: nove scudi identici che non portano informazione.

**Deciso: la sigla come segno**, al posto dell'icona. Ragione di Carmine: *«tanto per un fork non
IVAO devo comunque rimettere mano al codice»* — cioè una mappa «dipartimento → icona» vivrebbe nel
perimetro IVAO accanto all'enum `Department`, che un fork non-IVAO riscrive comunque, quindi
l'icona non è il pezzo che gli costa. E la sigla **è già** l'identificatore che lo staff usa ogni
giorno: nove icone scelte a tavolino sarebbero nove convenzioni nuove da imparare.

⚠️ Trappola da ricordare quando si implementa: l'icona del dipartimento sta accanto alle icone delle
**risorse** (calendario, news, media, link). Qualunque segno si scelga deve essere visibilmente di
un'altra famiglia, o si aggiunge rumore.

### L'avviso a quattro stati è un componente condiviso

Non il riquadro degli errori del form: **un componente usabile ovunque**. Quinto della lista chiusa.

### I tipi di evento del calendario sono di divisione, non di dipartimento

*«L'elenco è deciso da HQ, da WD o dalla sezione admin e sono quelli per tutti.»*

⚠️ Conseguenza da non sbagliare: **non è riusare le categorie**, che sono **per dipartimento**
(`cms_categories`). Serve un vocabolario **di divisione**, con un permesso di scope diverso da
`Calendar.Edit`, che è dipartimentale. Dove metterlo va proposto prima di scriverlo.

---

## Due lezioni di metodo, pagate care

1. ⚠️ **`grep "error CS"` non dice se una build è riuscita.** Con l'API in esecuzione, MSBuild
   fallisce con `MSB3027`/`MSB3021` — file bloccati — e non emette **nessun** errore `CS`. Due volte
   ho letto «0 errori» e ho eseguito un **binario vecchio**, e una verifica «rotta apposta» è passata
   a vuoto. Si guarda `Error(s)` nel riepilogo, e si ferma l'API prima di compilare.
2. **Tre ipotesi plausibili di fila possono essere tutte sbagliate.** Su D2: componente smontato,
   promessa rifiutata, retry lento — tutte no. Le sonde dentro la mutazione hanno risolto in un
   giro: `onSuccess` iniziava e non finiva mai.

---

## Lo stato in due righe

Ramo `m1/g13-fixes`, due commit, niente di non committato. Verde in locale, tutto rieseguito dopo le
due correzioni: **457 test .NET** (300 unit + 157 integrazione, uno in più di M1 ed è quello nuovo),
**254 Vitest** (28 file), lint, typecheck, format e i18n puliti.

⚠️ Non rieseguiti: `pnpm e2e` e `pnpm e2e:full`. Vanno fatti girare prima di chiudere G13, perché la
correzione delle cancellazioni tocca sei schermate e il banco pieno è l'unico posto che le prova
davvero.
