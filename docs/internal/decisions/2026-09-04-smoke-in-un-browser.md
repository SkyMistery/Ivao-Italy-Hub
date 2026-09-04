# Lo smoke in un browser è bloccante, e non aspetta M1

**Data:** 4 settembre 2026, dopo il tag di M0.
**Stato:** decisa da Carmine («fai tutto, e fallo come hotfix sulla 0.1.0»).

## Che cosa serve

Un test che apra l'applicazione in un browser vero e fallisca la build se non si apre.

## Perché nessun meccanismo esistente basta

Perché è successo. Subito dopo il tag `v0.1.0-m0`, la prima apertura di
<http://localhost:5173> ha dato una pagina di errore: `` `Tooltip` must be used within
`TooltipProvider` ``. `DarkModeToggle` di Atmosphere si avvolge da sé in un `Tooltip` di Radix,
`main.tsx` non montava `TooltipProvider`, e un tooltip senza quel provider **lancia** invece di
degradare. `DarkModeToggle` sta in `Chrome.tsx`, che è il frame di tutti e tre i layout: **ogni
schermata dietro un layout era morta**, e il tag di chiusura di M0 puntava a un'applicazione che
non si apriva.

I 353 test .NET e i 74 Vitest erano verdi, e lo erano legittimamente. Il difetto non era in un
componente: era **nell'albero**. `harness.tsx` monta `I18nextProvider` e `QueryClientProvider`
attorno a un componente per volta, nessun test montava `Chrome`, e nessuno montava affatto i
provider dell'applicazione — tanto che `ThemeProvider`, la prima volta che l'abbiamo montato in un
test, ha chiesto un `window.matchMedia` che jsdom non ha e che nessuno aveva mai dovuto stubare.

Un test che compone i pezzi non esisteva, e non poteva esistere dentro i meccanismi che avevamo.

## Che cosa si tocca

Tre pezzi, in ordine di quanto sono difficili da aggirare:

1. **`web/src/app/Providers.tsx`** — l'albero dei provider diventa un componente, `HubProviders`,
   montato sia da `main.tsx` sia dal test. **Questo è il punto**: la prima versione del test
   elencava i provider per conto suo, ed era inutile — sarebbe rimasta verde con l'applicazione
   rotta, cioè esattamente la copia che diverge in silenzio contro cui l'HANDOFF §3 mette in
   guardia. Un provider si aggiunge lì e in nessun altro posto.
2. **`web/src/app/layouts/Chrome.test.tsx`** — monta `Shell` dentro `HubProviders` e dentro un
   router in memoria, perché i link dell'header sono `Link` di TanStack. Verificato togliendo
   `TooltipProvider`: il test fallisce. Un test di regressione che passa in entrambi i casi non è
   un test.
3. **`web/e2e/`, `playwright.config.ts`, `pnpm e2e`** — tre smoke su Chromium contro il **bundle di
   produzione** servito da `vite preview`. Verificato allo stesso modo: due dei tre falliscono
   senza la correzione.

## La deviazione dal design, esplicita

Il design §8 dice: Playwright «solo `pnpm e2e`, **non bloccante** in M0». Questa nota lo cambia in
**bloccante in CI**, e la ragione è l'incidente: uno smoke che non rompe la build non avrebbe
impedito niente di ciò che è successo: sarebbe stato verde per errore o rosso in silenzio, e il tag
sarebbe uscito comunque. Un test che non può fermare una release non è una rete, è un rapporto.

Seconda deviazione, più piccola: il design immaginava lo smoke **anche** su «una `/{slug}`
pubblicata dal seed di test», che richiede l'API e il database in piedi. Questo suite **non li
avvia**: risponde alle poche chiamate che la shell fa (`/api/me`) da `e2e/fixtures.ts`, e fallisce
apposta su qualsiasi altra chiamata `/api` invece di ingoiarla. La ragione è di proporzione — ciò
che ha morso è il front-end che non si assembla, e questo lo prende in quindici secondi senza
MariaDB in CI — ma **la metà mancante resta un debito**, scritta nell'HANDOFF §10: un giro
end-to-end con l'API vera e una pagina pubblicata da un seed è un pezzo di macchina più grosso e
non è stato costruito qui.

## Le alternative scartate

- **Lasciarlo non bloccante, come il design diceva.** Scartata dall'evidenza: la ragione per cui
  era non bloccante era che nessuno voleva che un test fragile fermasse una fase. Il costo di quel
  timore l'abbiamo appena misurato, ed è un tag di release su un'applicazione che non si apre.
- **Solo il test Vitest su `Chrome`, senza Playwright.** Prende *questo* difetto, e infatti c'è.
  Non prende ciò che jsdom non simula — un bundle che si comporta diversamente dopo minificazione
  e tree-shaking, un CSS che non arriva, un asset con il percorso sbagliato. Due reti a maglie
  diverse, e la seconda costa quindici secondi.
- **Uno smoke con lo stack intero (MariaDB, `dotnet run`, la SPA).** È la cosa giusta da avere, ed
  è il debito qui sopra. Costruirla dentro un hotfix avrebbe voluto dire coordinare porte, attese
  di `/health` e un servizio MariaDB in CI, cioè introdurre proprio la fragilità che ha spinto a
  dichiarare Playwright non bloccante in origine.

## Che cosa se ne impara, oltre alla correzione

Il difetto è stato invisibile per una ragione che vale la pena scrivere: **i test provavano i
pezzi, e nessuno provava la composizione**, mentre l'intero progetto è costruito sull'idea che le
schermate siano composizione. Era il punto cieco esattamente dove il sistema fa la sua scommessa
più grossa.

Anche la seconda stringa trovata qui appartiene alla stessa famiglia della revisione §16.E:
`DarkModeToggle` riceveva `aria-label` ma non `title`, e il tooltip visibile restava l'inglese di
Atmosphere. Come l'`aria-label="breadcrumb"` di ieri, era sopravvissuta perché **si vede solo
passandoci sopra**. Terza volta in due giorni che una stringa non tradotta si nasconde dietro «non
la si guarda mai»: se M1 vuole una rete per questa famiglia, il posto è lo smoke, non la revisione.
