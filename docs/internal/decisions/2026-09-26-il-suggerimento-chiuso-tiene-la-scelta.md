# Il suggerimento chiuso tiene la scelta cliccata dopo aver scritto (A6c)

**Data:** 26 settembre 2026 — fase A6c di M3, PR del nucleo, trovata dalla sessione di A6b (#144) scrivendo lo smoke della richiesta
**Stato:** **Proposta** — la domanda a Carmine è al §5, in un [commento sulla PR #145][q]. Il codice di questa PR è la proposta del §3.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è — il campo suggerito del generatore di form, con l'insieme
chiuso (`suggestionsOnly`, nota `2026-09-08-dove-puo-portare-una-voce-di-menu`, piano §16.6) — e ha un difetto che nessun test vedeva.
Si corregge il meccanismo nel suo posto unico (`Suggest` in `web/src/shared/forms/SchemaForm.tsx`), non lo si aggira nelle schermate.
PR del nucleo a sé (`CLAUDE.md` §0 regola 6); **non va in coda**: non tocca il modulo e non migra niente.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5849495355

## 1. Che cosa è successo

La sessione di A6b l'ha misurato il 26 settembre 2026 nel browser (lo smoke della richiesta e l'app pubblicata sul banco): nella casella
della postazione si scrive «XXBB» per cercare, si clicca l'opzione, e la casella resta **vuota**.

- **Il meccanismo**, riletto qui sul codice di `main` e rifatto sull'indirizzo di una voce del menu:
  1. alla pressione sull'opzione il fuoco passa **dalla casella alla lista**: la lista di `cmdk` ha `tabindex="-1"`, e una pressione
     porta il fuoco al primo antenato che lo accetta;
  2. l'`onBlur` della casella applica la regola del campo chiuso — «uscire dalla casella con qualcosa che nessuno ha offerto rimette
     quello che c'era» —, perché il testo scritto non è un'opzione: il valore torna quello dell'apertura (`opened`; vuoto su una riga
     nuova, `/pilots` sulla voce di menu dello smoke);
  3. il testo che stringeva la lista è sparito, e **la lista torna intera sotto il puntatore**;
  4. il tasto si rilascia su un'altra riga, e il `click` va all'antenato comune delle due: nessuna opzione è scelta.
- **Cliccare la casella e poi un'opzione, senza scrivere, funziona** (non c'è niente da rimettere e la lista non si muove), e anche
  scrivere il valore intero. **Una pressione su un'intestazione o sulla barra di scorrimento della lista** butta via allo stesso modo
  ciò che si è scritto: la ricerca riparte da capo.
- **Perché nessuno l'ha visto**: `back-office.spec.ts` sceglie senza scrivere; la sua prova «oltre la centesima» scrive e poi clicca
  l'unica riga che il server risponde, che **per caso** è la prima anche della lista tornata intera (le pagine vengono prima delle
  schermate), e il clic ci cade sopra lo stesso. **jsdom non fa layout**: il clic di user-event arriva all'elemento a cui è mirato,
  qualunque cosa si sia mossa sotto.
- **Vale per ogni campo chiuso dell'hub**: l'indirizzo di una voce del menu, le postazioni nascoste delle impostazioni del training
  (A4), la postazione della richiesta di training (A6b), gli aerei dei tour (il tipo di un aereo, i tipi di un gruppo, l'aereo di
  riferimento).

## 2. Che cosa serve

Che l'opzione cliccata sia quella scelta, dopo aver scritto o no, e che il campo mantenga quello che promette oggi: **uscire con un
testo che non è un'opzione rimette il valore di prima** (`back-office.spec.ts` lo afferma sull'indirizzo del menu); **Escape chiude**
la lista; **la lista non ruba il fuoco** mentre si scrive (si apre senza prenderlo); **un clic nella casella non la chiude**. E che
non smetta di funzionare niente che oggi funziona.

## 3. La correzione (raccomandata)

**La casella e la sua lista sono un campo solo**: la regola del campo chiuso vale quando il fuoco **esce dal campo**, non quando passa
dalla casella alla sua lista. Quattro punti, tutti in `Suggest`; nessuna schermata cambia:

1. **L'`onBlur` della casella non applica la regola quando il fuoco va nella lista** (`relatedTarget` dentro il `PopoverContent`, che
   ora ha un `ref`). La lista non si muove, e il clic arriva all'opzione premuta.
2. **Quando la lista si chiude con il fuoco fuori dalla casella** — un clic altrove dopo una pressione sulla lista, Tab o Escape da
   dentro la lista — **la regola la applica la chiusura** (`onOpenChange(false)` di Radix): la casella non ha avuto un `blur` che lo
   dicesse. Radix chiude per un clic fuori solo al `click`, quando il fuoco si è già spostato; Escape premuto **nella casella** non
   la applica, come oggi (il testo resta finché non si esce).
3. **Tornare nella casella dalla lista non è arrivarci**: l'`onFocus` non rimette `opened`. Altrimenti il testo scritto diventerebbe
   «quello che c'era», e la regola lo rimetterebbe all'uscita.
4. **Una scelta scrive `opened`**: da quel momento «quello che c'era» è la scelta. Serve perché si può rientrare nella casella dalla
   lista mentre questa si sta ancora chiudendo (Radix ne anima l'uscita), e quel rientro, per il punto 3, non rilegge il valore.

**Un campo suggerito aperto** (oggi solo la collezione nelle proprietà di un blocco) non ha la regola, e per lui cambia soltanto che
tornando dalla lista la ricerca continua invece di ripartire.

**Provato, in un browser** (Playwright, smoke con l'API finta, sulla voce di menu): la spec nuova `web/e2e/closed-suggestion.spec.ts`
ha cinque prove — l'opzione cliccata dopo averne scritto una parte, e l'uscita dopo la scelta che rimette la scelta; una pressione
sull'intestazione che tiene la ricerca, e l'uscita da lì che rimette il valore di prima; il ritorno nella casella che continua la
ricerca; Escape; **la barra di scorrimento che si trascina** e l'opzione scelta dopo. Sul codice di `main` ne cadono quattro (Escape
passa: è una promessa di oggi); togliendo un pezzo alla volta della correzione cade la prova che lo tiene — il punto 2 la seconda, il
punto 3 la terza, il punto 4 la prova del menu di `back-office.spec.ts` (clicca di nuovo la casella mentre la lista si chiude).

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **Tenere il fuoco nella casella mentre si preme la lista** (`onMouseDown={(event) => event.preventDefault()}` sulla lista), la correzione che proponeva A6b | Aggiusta l'opzione e l'intestazione, e **ferma la barra di scorrimento della lista**: Chromium non trascina una barra il cui `mousedown` è annullato. Misurato qui, su un riquadro di prova nella pagina dello smoke: 611 px di scorrimento senza, **0** con. Oggi aprire la lista e trascinarne la barra funziona (la lista è alta al massimo 300 px: le torri, i tipi di aereo) e smetterebbe di funzionare. Fatta e provata per prima: tutte le prove verdi, la barra ferma. Lasciar passare la sola pressione sulla barra riporterebbe comunque i punti 1–3, più un clic che riapra la lista e il fuoco da rimettere dopo il trascinamento |
| Solo il punto 1 (l'`onBlur` che non rimette quando il fuoco va nella lista) | Aggiusta l'opzione, ma un campo lasciato **dalla** lista (un clic altrove dopo aver premuto un'intestazione o la barra) terrebbe il testo scritto, e tornare nella casella lo farebbe diventare «quello che c'era» |
| Scegliere l'opzione alla pressione (`pointerdown`) invece che al `click` | `cmdk` sceglie al clic e con Invio; una scelta alla pressione è una scelta per sbaglio mentre si scorre con il dito |
| Lasciarlo, e far scegliere alle spec senza scrivere | È un difetto di ogni campo chiuso per chiunque scriva per cercare, cioè per il motivo per cui il campo ha una ricerca |

## 5. La domanda per Carmine

**Il suggerimento chiuso si corregge come al §3** — la casella e la sua lista un campo solo, la regola quando il fuoco esce da tutte
e due —, e **non** tenendo il fuoco nella casella come proponeva A6b, che ferma la barra di scorrimento della lista? **Raccomandata:
sì**, perché è la sola forma che aggiusta la scelta senza togliere niente di ciò che oggi funziona, e sta tutta in `Suggest`.

La domanda è nel [commento su #145][q], del 26 settembre 2026; la risposta di Carmine entra qui, con la data e il link al suo commento.

## 6. Che cosa si tocca

- **A6c** (questa PR, nucleo): `web/src/shared/forms/SchemaForm.tsx` (`Suggest`); la spec nuova `web/e2e/closed-suggestion.spec.ts`
  (per quel file Playwright disegna le barre di scorrimento: headless le nasconde con `--hide-scrollbars`); questa nota; `08`, la fase
  A6c; `HANDOFF-M3.md`.
- **Niente altro**: nessuna schermata, nessuno schema, nessun test del maintainer.
- **Dopo il merge**: le spec di A6b (`web/e2e/training-request.spec.ts`, `web/e2e/full/training-request.spec.ts`), che oggi scelgono la
  postazione dall'elenco apposta, possono scriverne una parte.

## Da portare nel piano

- **§16.6** (il generatore di form), il campo suggerito chiuso: tiene solo ciò che è offerto **quando il fuoco esce dal campo — la
  casella e la sua lista —**, non quando passa dalla casella alla lista; l'opzione cliccata dopo averne scritto una parte è quella
  scelta. Una riga nel changelog.
- **`CONTRIBUTING.md`, «Traps already paid for»**: una lista in un popover non annulla la pressione per tenere il fuoco altrove,
  perché **Chromium non trascina una barra di scorrimento il cui `mousedown` è annullato**; e Playwright headless **nasconde le barre**
  (`--hide-scrollbars`), quindi una prova che ne preme una le riaccende (`launchOptions.ignoreDefaultArgs`).
