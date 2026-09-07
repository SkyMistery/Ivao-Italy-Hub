# Il giro visivo di M1: che cosa si vede quando il contenuto è vero

**Data:** 7 settembre 2026 — G12, task 2 (`04-piano-implementazione-m1.md`)
**Stato:** rilievi raccolti; due corretti sul posto, quattro sono decisioni e restano aperti

Fatto **dopo** la ricopiatura a mano, e non prima, per una ragione che si è rivelata giusta: con il
Lorem addosso metà di questi difetti non esiste. Le due pagine vere sono quelle che li hanno fatti
uscire.

## Come è stato fatto, e che cosa non copre

Misurato invece che guardato, perché una cattura dello schermo mi aveva già dato un falso allarme
(una sezione che sembrava spostata a destra e non lo era — HANDOFF §13, la regola funziona). Per ogni
pagina pubblica: larghezza di scorrimento contro larghezza della finestra, elementi più larghi del
viewport, elementi tagliati con `overflow` visibile, e il **rapporto di contrasto** di ogni nodo di
testo contro il suo sfondo effettivo.

⚠️ **Quello che questo giro non copre**, e va detto:

- **il back-office a 375 px non è stato guardato.** Il pannello del browser interno non scende sotto
  **419 px**, e il browser dove c'è la sessione dello staff non si può ridimensionare. Le pagine
  pubbliche sono state misurate a 419 e a 1280; le schermate di staff solo a 1912.
- il tema chiaro e scuro sono stati confrontati **per contrasto**, non per gusto.

## Corretti sul posto

1. **I titoli delle sezioni nuove di `/start` erano `h1`.** Il blocco Titolo nasce a **livello 1**:
   chi aggiunge un titolo si ritrova un `h1` se non ci pensa, e la pagina ne aveva quattro. Portati a
   livello 2 e ripubblicata; adesso la gerarchia è un `h1` più quattro `h2`.
   ⚠️ Il difetto vero non è la pagina, è il **default**: su una pagina fatta di blocchi il livello
   naturale di un titolo nuovo è 2, non 1. Cambiarlo è una riga in `blankValues`, ma cambia il
   comportamento di ogni pagina già scritta, quindi è una decisione e non l'ho presa qui.
2. I quattro difetti nei seed (template contro pagine) — già corretti e coperti da un test, PR #56.

## Aperti, e ognuno è una decisione

### 1. ⚠️ Il grigio dei testi secondari non passa AA nel tema scuro

`--muted-foreground` è **lo stesso colore nei due temi**: `rgb(96, 98, 130)`.

| | sfondo | rapporto | verdetto |
|---|---|---|---|
| chiaro | `rgb(255,255,255)` | **5.89 : 1** | passa |
| scuro | `rgb(18,19,27)` | **3.14 : 1** | **non passa** sotto i 18,66 px |

Misurato su `/about`: i codici posizione dello staff (`IT-AOA1`, 14 px) e la riga «solo i membri che
hanno fatto login» (12 px). Riguarda ogni schermata che usa `text-muted-foreground` per il testo
piccolo — schede dello staff, note sotto i campi, piè di pagina, descrizioni nelle liste.

È un token di **Atmosphere**, non nostro: o lo si sovrascrive nel tema della divisione, o si smette
di usare il grigio per il testo piccolo. Entrambe sono decisioni, e la seconda è molta UI.

### 2. ⚠️ Le props che non sono prosa finiscono nell'indice di ricerca

Nell'indice di `/start`, in mezzo al testo: **«… quattro semplici passi. `left muted` Prima di tutto…»**
— sono `align` e `tone` del blocco `hero`, due enumerazioni salvate come stringhe.

Rompe una regola che il progetto ha già scritto (`CLAUDE.md` §4: «**nessuna stringa che non sia prosa
dentro `props`**, finisce nell'indice di ricerca»), ed è la ragione per cui il livello di un titolo è
un **numero** e non un enum. `hero` non ha seguito la regola, e nessuno poteva accorgersene finché lo
snippet non conteneva prosa vera.

Il rimedio generico esiste ed è più forte del divieto: **indicizzare solo i valori dentro le mappe
tradotte**. La prosa in un blocco è sempre `Localized`; le enumerazioni sono stringhe nude, e gli URL
pure — che nell'indice non ci vogliono comunque. Il server distingue le due cose **senza conoscere
gli schemi**, che è esattamente il vincolo di design M0 §5.3. Tocca l'estrattore, quindi è una nota.

### 3. La sintassi Markdown finisce nell'indice e nello snippet

Sempre in `/start`: «`**IVAO Italia**` è la community…». Chi cerca trova lo stesso, ma legge gli
asterischi. Meno grave del n.2 e con lo stesso punto di intervento.

### 4. Ogni pagina pubblica ha due `h1`

Uno è il titolo della riga, disegnato `sr-only` dalla rotta pubblica; l'altro è il blocco in cima —
`hero` o `heading` di livello 1. Su `/about` sono «About» e «Welcome to the Italian Division of
IVAO!». Il commento nella rotta dice che il titolo visibile è il blocco, quindi la scelta è
consapevole: quello che non è deciso è che siano **due `h1`**. O l'`sr-only` diventa un `h2`, o non
si disegna quando il primo blocco è già un titolo.

## Quello che invece è a posto

- **Nessuno scorrimento orizzontale** su nessuna pagina pubblica, né a 419 px né a 1280.
- **Nessun elemento più largo del viewport** e nessuno tagliato con `overflow` visibile.
- Le larghezze delle sezioni sono quelle dichiarate: 1024 px per `default`, 768 per `narrow`,
  centrate. Il `narrow` di `/about` si vede ed è voluto.
- **Tema scuro: zero fallimenti di contrasto su `/start`.** L'unico problema è il n.1, e si vede solo
  dove il testo è piccolo.
- Tema chiaro: i titoli di sezione stanno a 3,27 : 1 a 30 px in grassetto — **passa** AA per il testo
  grande, ma accanto agli `h1` quasi neri sembrano sbiaditi. Osservazione, non difetto.
