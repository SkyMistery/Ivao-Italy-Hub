# Il sito ha un colore: i titoli, il fondo azzurro, il quarto fondo scuro, l'accento sui grafici

**Data:** 12 settembre 2026 — Carmine, dopo G14 e la deduplica: «dai un'occhiata alla UI del sito.
Possiamo renderla più colorata e accattivante (senza violare le linee guida IVAO)?».

**Stato:** quattro idee su otto proposte, **decise da Carmine il 12 settembre** («fai 1–4, aggiorna
la documentazione e poi codice»). Le altre quattro restano scritte qui sotto, non fatte.
Piano 0.67. Branch `m1/site-colour`, sopra `m1/media-dedupe`.

**Come è stato guardato:** nel codice e nei token, **non** nel sito in funzione — Docker era spento e
né `:5000` né `:5173` rispondevano. I rapporti di contrasto scritti qui sono **calcolati** dai valori
di `@ivao/atmosphere-brand`; quelli che contano li misura `e2e/contrast.spec.ts` in un browser, ed è
la ragione per cui ognuna delle quattro cose porta una riga di quella spec con sé.

## Che cosa si è trovato

**Il pacchetto del brand porta dieci famiglie di colore; l'hub ne usa due.** `atmos` (la barra, il
piè di pagina, due fondi di sezione), `fuselage` (tutto il resto), e poi `ocean-400` su un filetto di
2 px nella barra. `ocean`, `product-aurora`, `product-artifice`, `product-creators` e il blu semantico
non compaiono in nessun'altra riga di `web/src`. Non sono colori inventati: stanno in
`@ivao/atmosphere-brand/dist/tokens.css` e Tailwind ne fa utilità (`bg-ocean-50`,
`text-product-aurora-mid`), quindi usarli **è** stare dentro le linee guida di IVAO.

**Due conseguenze misurabili.**

1. `--accent` di Atmosphere è `fuselage-250`. Il fondo di sezione che l'editor chiama «accent» è
   quindi **un quarto grigio** — e nel tema scuro è `fuselage-700`, cioè **lo stesso colore** di
   `muted`: due pastiglie diverse nella striscia, un solo colore sullo schermo.
2. La regola base di Atmosphere dipinge **h2–h6 in `fuselage-400`** (`#8b8ca9`) mentre il testo è
   `fuselage-800`: ogni titolo di sezione del sito, e ogni intestazione del back-office, è grigio
   pallido. Calcolato su bianco: **≈ 3,2 : 1**, che basta per il testo grande (h2, h3) e **non basta**
   per AA a h5 e h6. `e2e/contrast.spec.ts` non lo vedeva perché misura il testo secondario nel tema
   **scuro** e i titoli solo sui fondi scuri, dove sono `fuselage-300` su quasi nero e passano.

## 1. I titoli tornano del colore del testo — il **terzo** override di Atmosphere

`h2, h3, h4, h5, h6 { color: var(--foreground) }`, una regola, in fondo a `styles/index.css` come le
altre due. Non un colore scritto a mano: lo stesso token che decide il corpo del testo, quindi ribalta
da sé nel tema scuro e sui quattro fondi scuri, che portano `.dark`.

Le linee guida dicono che **un terzo override è una decisione e non una modifica** (`docs/UI-GUIDELINES.md`
§4), ed è per questo che sta qui. Le ragioni per cui è giusto farlo:

- è un difetto misurabile e non un gusto, almeno per h5 e h6;
- è **una riga** contro una passata su 72 schermate: `H1`…`H4` di Atmosphere non dichiarano colore,
  quindi tutto viene dalla regola base, e sostituirla vale per ogni titolo che esiste e che esisterà;
- vince senza `!important` e senza specificità gonfiata: le regole di Atmosphere stanno in
  `@layer base`, la nostra **fuori da ogni layer**, e nella cascata una regola senza layer batte una
  in un layer — la stessa strada già verificata in un browser per l'altezza del `Select`.

⚠️ **Il prezzo, ed è una scelta di gusto**: dove un `H3` o un `H4` faceva da etichetta quieta nel
back-office adesso è nero come il testo. Si guarda in `/staff/admin/ui-kit` e nelle liste dense; se
dà fastidio, la correzione è dare a quelle etichette la classe che dice ciò che sono
(`text-muted-foreground`), non rimettere il grigio a tutti i titoli del sito.

**Nuovo in `contrast.spec.ts`**: i titoli del sito pubblico misurati **nel tema chiaro** — il caso che
non era coperto — su `/`, `/about`, `/news`.

## 2. Il fondo `accent` smette di essere grigio: `ocean-50` / `ocean-900`

Una riga in `BACKGROUND` (`blocks/ContentRenderer.tsx`) e la sua gemella nella striscia
(`SectionFrame.tsx`): `bg-ocean-50 dark:bg-ocean-900`. È **(b)**: nessun insieme si allarga, i sette
fondi restano sette, cambia il colore di uno che era un doppione.

Calcolato: testo primario su `ocean-50` ≈ 14 : 1, testo secondario (`fuselage-500`) ≈ **4,8 : 1**,
quindi AA passa senza un grigio dedicato; nel tema scuro, secondario su `ocean-900` ≈ 5,4 : 1. Da qui
una pagina alterna bianco e azzurro chiaro invece di bianco e grigio, che è la metà dell'effetto
chiesto e costa mezz'ora.

## 3. Un quarto fondo scuro: `aurora`

`dark bg-product-aurora-dark text-foreground` — costruito **esattamente** come `brand`, `deep` e
`dark`: porta la classe `dark`, quindi ogni blocco dentro legge chiaro per costruzione, ed è un token
del brand e non un colore scelto a mano. I fondi di sezione passano da **sette a otto**: allarga un
insieme chiuso, quindi §16.C del piano e `docs/UI-GUIDELINES.md` cambiano nello stesso commit, e così
la lista gemella sul server (`BlockDocumentWalker.Backgrounds`, che con `BACKGROUNDS` di
`envelope.ts` va d'accordo a mano).

**Perché `aurora-dark` (`#082628`) e non `aurora-mid`**, che sarebbe più visibilmente verde: calcolato,
il testo secondario su `aurora-mid` è 2,3 : 1 e resterebbe illeggibile anche col grigio più chiaro che
`.on-brand-ground` usa sul blu — servirebbe un grigio così chiaro da non essere più secondario. Su
`aurora-dark` il secondario è ≈ 4,9 : 1 e non serve niente. Il petrolio è quindi scuro e sobrio: è il
colore che si può promettere, non quello che si vorrebbe.

**Nuovo in `contrast.spec.ts`**: `aurora` nella prova dei fondi scuri, che così ne misura quattro.

## 4. L'accento sta sui **grafici**, mai sotto il testo

Un insieme chiuso di quattro — `brand`, `ocean`, `aurora`, `artifice` — e una proprietà `accent` su
quattro blocchi: `hero`, `cardGrid`, `iconGrid`, `timeline`. È **(b)**: una proprietà di schema in più,
disegnata dal generatore come qualunque `z.enum`, con `brand` come valore predefinito — cioè esattamente
il `text-primary` di oggi, quindi nessuna pagina già scritta cambia aspetto.

Dove si vede: l'**icona** di un `iconGrid`, di una scheda di `cardGrid` e di una tappa di `timeline`;
un **filetto** di 2 px in cima a ogni scheda; una **barretta** corta sopra il contenuto di un `hero`.

⚠️ **E mai su una parola.** È la regola che rende tutto questo verificabile: un grafico deve stare a
3 : 1 dal suo fondo, una parola a 4,5 : 1, e l'arancio del brand non ci arriva — `artifice-low`
(`#a86700`) è 4,5 : 1 su bianco ma **3,7 : 1** sul nuovo fondo azzurro, e `artifice-dark` è 3,2 : 1 già
su bianco. Perciò l'occhiello di un `hero` resta del grigio secondario e il colore diventa una barretta
accanto. Le quattro famiglie hanno ognuna la sua coppia chiaro/scuro (`text-… dark:text-…`), e siccome
i fondi scuri portano `.dark` la variante giusta scatta da sé anche dentro una sezione scura.

`/staff/admin/ui-kit` mostra i quattro accenti senza che nessuno le aggiunga una sezione: gli esempi
dei quattro blocchi nel registry ne scelgono uno diverso ciascuno.

## Perché non le altre cose che si potevano fare

- **Nessun colore libero**, di nuovo (piano 0.58): un colore scelto a mano non può promettere niente
  sul testo che gli finisce sopra, ed è la ragione per cui i fondi scuri sono quattro nomi e non una
  tavolozza.
- **Niente tricolore e nessuna identità italiana**: `CLAUDE.md` §3 — il codice non sa di essere
  italiano. Un insieme chiuso di famiglie del brand protegge anche da questo: non c'è un posto dove
  scrivere verde-bianco-rosso.
- **Niente veli sulle sezioni con foto**: le linee guida lo vietano già, e un velo è un colore che non
  è un token.

## Che cosa **non** si fa in questa passata

Le altre quattro proposte del 12 settembre, tutte rimandate e nessuna cominciata: **(5)** una colonna
`tone` sulla riga della categoria, perché i distintivi dicano qualcosa (vorrebbe una migrazione);
**(6)** invertire pagina e scheda (`#fff` contro `fuselage-100`), che sarebbe un quarto override;
**(7)** la famiglia d'accento scelta dalla divisione in `division.json`, che è (c) e fa crescere
`/api/me`; **(8)** il colore sui dati vivi — pip verde per chi è in frequenza, barre `ocean` sul
traffico.

## Costruito il 12 settembre — quello che il disegno non diceva

- **Il filetto di una scheda voleva `border-t-*` e non `border-*`.** La prima versione aveva una mappa
  sola, il colore su tutti e quattro i lati e la larghezza scelta dalla utility accanto: elegante, e
  sbagliata, perché una `Card` di Atmosphere ha già un filo su ogni lato — così la scheda intera
  diventava un contorno colorato. Bello, ma non è «un filetto sopra la scheda», e con quattro accenti
  nel catalogo una griglia di schede sarebbe stata una cosa urlata. Due mappe: `ACCENT_EDGE` per lo
  spigolo di chi ha larghezza su un lato solo (la spina di un `timeline`), `ACCENT_TOP` per chi ce
  l'ha su tutti. **Visto in un browser**, non dedotto.
- **Trovato sulla strada, corretto qui: il numero di un passo non passava AA.** Lo screenshot del
  fondo nuovo ha fatto venire il dubbio, e invece di calcolarlo è stato aggiunto un `timeline` alle
  sezioni che `contrast.spec.ts` misura: il numero nella pastiglia, 12 px, misura **4,15 : 1** su un
  fondo scuro e **3,96 : 1** su quello azzurro, dove AA chiede 4,5 — e falliva anche prima di oggi,
  su tutti i fondi scuri e su `muted`. Ora è `text-foreground`: quel numero non è testo secondario,
  è il segnaposto del passo. La spec se lo tiene, così la prossima volta lo dice da sé.
- **Il controllo dei titoli guarda dentro `main`.** Il nome della divisione sulla barra è un `h1`
  bianco per scelta, quindi «ogni titolo è del colore del testo» è falso per lui: il test misura la
  colonna di lettura. Lo ha detto fallendo, che è il modo giusto di scoprirlo.
- **La prova che l'override vince non è il rapporto di contrasto.** Le tre schermate misurate portano
  `h1`–`h3`, che sono testo grande e passerebbero a 3 : 1 anche col grigio di Atmosphere: il test
  sarebbe rimasto verde togliendo la riga, e il guasto sarebbe tornato su un `h5` che nessuno ha
  ancora scritto. Perciò accanto ai rapporti c'è l'asserzione che **un titolo è del colore del testo**,
  che è la regola e non la sua conseguenza.

Conto dopo: **375 Vitest** (quattro nuovi), **58 smoke** (due nuovi), **306 .NET unit**. Le 172 di
integrazione **non girate in locale** — Docker era spento — e l'unica riga di C# toccata è un valore
in più in `BlockDocumentWalker.Backgrounds`; il test che posta un fondo sconosciuto ne posta un altro
(`gradient`) e non è disturbato. Le fa la CI.

## Che cosa tocca

`web/src/styles/index.css` (una regola), `blocks/ContentRenderer.tsx` (due righe di `BACKGROUND`),
`blocks/envelope.ts` e `Content/BlockDocumentWalker.cs` (la coppia delle liste, `aurora`),
`features/content/SectionFrame.tsx` (due pastiglie), `blocks/schemas.ts` (l'insieme `ACCENTS` e una
proprietà su quattro schemi), `blocks/blocks.tsx` (due mappe e quattro punti di disegno),
`blocks/core.ts` (quattro esempi della galleria), `locales/{it,en}/common.json` (ventuno chiavi),
`docs/UI-GUIDELINES.md` §4 e la sezione dei fondi, il piano §16.C, `CLAUDE.md` §4.
Test: Vitest sui quattro blocchi e sulla striscia, `e2e/contrast.spec.ts` in tre punti.
**Nessun componente nuovo, nessun endpoint, nessuna migrazione.**
