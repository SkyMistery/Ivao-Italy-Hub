# Gli header di sicurezza, e una CSP che la suite smoke esegue davvero

**Data:** 12 settembre 2026 — nato dalla scelta di Carmine sul blocco interattivo: «invece io ti
direi C e prepara un piano per mettere una CSP. Decidi tu quale dei due fare prima».

**Stato:** **deciso da Carmine** (la forma, le direttive e il modo di verificarle sono quelli che gli
ho messo davanti prima di scrivere una riga; «procedi pure»). Costruito lo stesso giorno, branch
`m1/security-headers`. Piano 0.69. La nota gemella è
`2026-09-12-il-blocco-interattivo.md`, che viene dopo questa **di proposito**: la ragione per cui il
frame di quel blocco sarà servito da un endpoint invece che da un `srcdoc` è esattamente la CSP di
questa pagina, e costruire prima l'endpoint avrebbe voluto dire scoprire dopo se la CSP era
possibile.

## Che cosa si è trovato

**L'hub non mandava nessun header di sicurezza.** Nessuna CSP, nessun `X-Content-Type-Options`,
nessun `Referrer-Policy`, nessun `X-Frame-Options`. Verificato con un `grep` su tutto `src/` e poi in
un browser: la risposta di `/` usciva nuda. Non è un difetto introdotto da qualcosa, è una cosa che
non era mai stata fatta — M0 aveva costruito la protezione CSRF (`X-Requested-With`, il guardiano
cross-site), i proxy fidati e l'HSTS, e gli header della pagina non erano mai passati per nessuna
fase.

## Che cosa si fa

**Un file, `config/security.json`, letto da due server.** In produzione è ASP.NET a servire sia la
SPA sia l'API, quindi è lui a mandarli (`SecurityHeaders.cs`, un `Use` calcolato una volta
all'avvio). Ma il file lo legge anche `vite.config.ts` per il suo server di **preview** — ed è la
parte che conta: **la suite smoke gira contro la preview**, quindi tutti e 62 quei test ora girano
sotto la policy vera, invece che sotto niente.

La policy misurata, che è quella nel file:

```
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
img-src 'self' data:; font-src 'self'; media-src 'self'; connect-src 'self';
frame-src 'self' <i tre host dell'allowlist>; frame-ancestors 'none';
form-action 'self'; base-uri 'none'; object-src 'none'
```

più `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
`X-Frame-Options: DENY` e `Cross-Origin-Opener-Policy: same-origin`.

## Le due cose misurate invece che decise a tavolino

1. **`script-src 'self'` basta.** L'`index.html` costruito non ha **nessuno** script inline e nessuno
   stile inline: Vite emette un `<script type="module" src=…>` e tre `modulepreload`. Quindi niente
   nonce, niente hash, niente `'unsafe-inline'` sugli script — che è l'unica direttiva che, aperta,
   renderebbe decorativo tutto il resto.
2. **`style-src` ha bisogno di `'unsafe-inline'`, e non è una resa.** Con `style-src 'self'` il
   browser rifiuta **tre** applicazioni di stile, dai bundle di React e di Atmosphere: «Applying
   inline style violates … The action has been blocked». Provato anche `style-src-attr
   'unsafe-inline'`, che sarebbe la direttiva giusta per un attributo `style`: **non serve**, perché
   Chrome attribuisce a `style-src` anche le modifiche fatte via CSSOM (`node.style.setProperty`),
   che è come React scrive uno stile. Un hash non si applica agli stili di attributo senza
   `'unsafe-hashes'`, e un nonce nemmeno. Il rischio residuo di `style-src 'unsafe-inline'` è CSS
   iniettato — sgradevole, non eseguibile — ed è l'ordine di grandezza sotto a uno script.

## Sviluppo e produzione non hanno la stessa policy, e sta scritto dove si vede

Il server di **sviluppo** di Vite inietta un modulo suo e parla con sé stesso su un web socket:
`script-src` prende `'unsafe-inline'` e `connect-src` prende `ws:`. Sono due righe in
`vite.config.ts`, accanto alla policy stretta, con scritto perché. Niente di tutto ciò arriva in un
pacchetto: la **preview** — che è la build — gira sotto quella severa, ed è quella che i test
eseguono.

## Che cosa **non** si fa

- **Niente nonce e niente hash.** Servirebbero a coprire inline che non esistono: la SPA non ne ha.
- **Niente `report-uri`/`report-to`.** Non c'è un posto dove mandare i rapporti, e inventarlo
  significherebbe un endpoint, una tabella e una ritenzione. Al suo posto c'è una cosa più utile:
  `e2e/security.spec.ts` **guarda la console** mentre cammina sulle schermate e fallisce su una
  violazione. ⚠️ È l'unico modo in cui quel guasto si denuncia: un foglio di stile bloccato **non fa
  fallire nessuna asserzione** — la palette si apre lo stesso, il test passa lo stesso, e la pagina
  è sbagliata in silenzio.
- **Niente `upgrade-insecure-requests`**: in produzione ci sono già HSTS e il redirect a HTTPS.

## L'interruttore, e perché esiste

`contentSecurityPolicy.enabled` nel file. La ragione non è la timidezza: **la produzione si
raggiunge via FTP e non c'è una shell** (piano §2.5). Una direttiva che rompe un'installazione vera
dev'essere togliibile senza ricompilare e senza un deploy, cioè cambiando un file.

## Che cosa tocca

`config/security.json` (nuovo, **nel pacchetto**: `.csproj` e un controllo in CI),
`src/IvaoHub.Web/SecurityHeaders.cs` (nuovo), `HubPaths.SecurityFile`, tre righe di `Program.cs`,
`web/vite.config.ts` (le due policy), `docs/FORKING.md` (che cosa cambiare se la tua divisione
incornicia altri host). Test: **quattro e2e** — gli header sulla risposta, le direttive che non si
possono ammorbidire, il sito pubblico e il back-office senza una sola violazione — **due di
integrazione** (gli header sul filo, la policy uguale al file) e **un Vitest** che tiene insieme le
due metà dell'allowlist: ogni host che `embedSource` può incorniciare deve stare in `frame-src`, o
il blocco dice «permesso» e il browser rifiuta, in silenzio.

## Che cosa apre

L'endpoint del blocco interattivo, quando lo si scriverà, **sovrascrive** questi header sulla sua
risposta: `default-src 'none'`, `script-src 'unsafe-inline'`, e `frame-ancestors 'self'` invece di
`'none'` — altrimenti la nostra pagina non potrebbe incorniciare il nostro frame. Il middleware è
scritto apposta prima degli endpoint perché questo sia possibile, e nel file c'è il commento che lo
dice.
