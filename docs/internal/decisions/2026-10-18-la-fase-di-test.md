# La fase di test e di correzione, dopo gli eventi

**Data:** 18 ottobre 2026
**Stato:** **decisa da Carmine in chat al master**, 18 ottobre 2026 (§2). È una nota del master: dice come si lavora, non tocca il
codice. La lista di controllo della sicurezza è nel file accanto, `docs/internal/test/sicurezza-lista-di-controllo.md`.
**Regola applicata:** organizza la fase che segue M4 (piano §13); le regole di `CLAUDE.md` §0 (ruoli, una PR per volta, decisioni per
iscritto) valgono anche qui.

## 1. Che cosa serve

Finito M4 (le ultime fasi E14b, E15b ed E10r), prima della produzione su `it.ivao.aero` (nota `2026-10-18-la-produzione`) c'è una fase
**intensa di prove e correzioni**, fatta da due persone, Carmine e `dalberone`, e dalle sessioni di Claude. Serve dividerla bene e dire
come si segnala, si corregge e si consegna.

## 2. Che cosa ha deciso Carmine

1. **Ognuno corregge il suo codice e prova quello dell'altro** («Si, mi sembra giusto»): chi l'ha scritto non vede i buchi del suo.
2. **Il modello delle issue e le etichette** li prepara il master («si»).
3. **Nessuna scadenza fissa**: si consegna sulla prova quando c'è qualcosa e c'è tempo («Direi di procedere come abbiamo tempo e
   riusciamo a lavorare, senza scadenze fisse»).
4. **La sicurezza** (§4): `dalberone` attacca le parti che non ha scritto, su un'istanza locale; il master rivede il codice di tutto,
   Eventi e Training in testa; il master prepara la lista di controllo («si, va bene, prepara anche la lista»).

## 3. Chi prova che cosa

| | Prova soprattutto | Corregge |
|---|---|---|
| **Carmine** | **Eventi** e **Training** (i moduli di `dalberone`), da staff e da membro | il nucleo, il sito pubblico e il CMS, i Tour, le consegne — con le sue sessioni di lavoro |
| **`dalberone`** | il **nucleo**, il **sito pubblico e il CMS**, i **Tour**, il back office generale (permessi, grant, contenuti, media) | Eventi e Training |
| **Il master e le sessioni di Claude** | le prove automatiche che mancano (un giro e2e completo per modulo, con il banco di E10w), la velocità (la diagnostica della 0.9.2), la revisione del codice per la sicurezza e la privacy, il telefono (375 px) e l'accessibilità delle pagine principali | smista ogni segnalazione (la riproduce se può, le dà una gravità, la assegna al proprietario) e rivede le correzioni come sempre |

- **Dove**: sull'installazione di prova `test.it.ivao.aero`, che resta privata (solo staff, nota `2026-09-27-l-installazione-di-prova`).
- **I ruoli**: lo stesso giro si ripete da coordinatore, da capo di un FIR, da membro semplice e da visitatore. Da superadmin si vede
  tutto: **prima di cominciare** servono account o grant di prova per ciascun ruolo (li organizza Carmine, che ha i permessi).
- **Prima prova**: la prima prenotazione di prova di Carmine (il suo vincolo prima di aprire prenotazioni su un'installazione vera).

## 4. La sicurezza

- **Dove si attacca: un'istanza locale, mai la prova online.** `test.it.ivao.aero` sta sul server condiviso di Ivao.It, accanto a
  vIPI, dietro Cloudflare, con un database che il server non isola (nota `2026-09-27-i-dati-condivisi-senza-isolamento`): scansioni e
  tentativi in massa lì farebbero bloccare l'IP da Cloudflare, peserebbero sul pool condiviso con vIPI, e chiederebbero comunque il
  permesso di Ivao.It. Si attacca **lo stesso pacchetto della release, avviato in locale**, con MariaDB 11.4.10 in Docker,
  nell'ambiente **`Production`** e con il proprio client OAuth di prova (`CONTRIBUTING.md`): **mai l'ambiente `E2E`**, che apre
  `/e2e/signin` per costruzione. Sulla prova online solo controlli leggeri e non distruttivi (header, CSP, cookie, pagine riservate,
  robots).
- **Chi attacca che cosa**: `dalberone` il nucleo, l'accesso con IVAO, i token personali, il sito pubblico e il CMS, il blocco
  interattivo e la sua CSP, gli upload, i Tour; il master rivede il codice di tutto, con **Eventi e Training** in testa, e soprattutto i
  **permessi** (l'unico gestore, il grant su una riga, «chi ha interesse non decide», i dati di un membro visti da un altro, le API
  pubbliche).
- **La lista di controllo**: `docs/internal/test/sicurezza-lista-di-controllo.md` (OWASP adattato all'hub).
- ⚠️ **Il repository è pubblico**: una debolezza **sfruttabile** non va in una issue pubblica finché non è corretta. Si segnala in
  privato — un *security advisory* privato di GitHub (la «Private vulnerability reporting» del repository va accesa da Carmine nelle
  impostazioni) o, in attesa, in chat a Carmine —, si corregge in una PR che non spiega l'attacco, e la issue o l'advisory si pubblica
  dopo. Un indurimento senza attacco (un header in più, un limite) è una issue normale con l'etichetta `security`.
- Quello che è grave si corregge **prima della produzione**.

## 5. Come si segnala, si corregge e si consegna

- **Una issue per difetto**, con il modello `.github/ISSUE_TEMPLATE/bug.yml`: dove (pagina), con quale ruolo, i passi, che cosa ci si
  aspettava, che cosa è successo, la versione (in fondo alla pagina), una schermata.
- **Le etichette**: il modulo (`events`, `training`, `tours`, `core`, `site`) e la gravità (`blocker`: impedisce un giro intero o perde
  dati; `major`: una funzione sbagliata senza via d'uscita; `minor`: il resto), più `security` per gli indurimenti.
- **Una PR per difetto**, ramo `fix/<slug>` (nei moduli di M4 anche `m4/fix-<slug>`), con «Closes #N»: chi corregge è il proprietario
  (§3). Nessuna nota di decisione per un difetto, **salvo** che il difetto riveli una scelta da prendere: allora vale `CLAUDE.md` §5
  (caso (c), la domanda a Carmine).
- **Le consegne sulla prova** senza scadenze: quando c'è qualcosa e c'è tempo; ogni consegna è un tag e un pacchetto
  (`docs/DELIVERING.md`), e la versione in fondo alla pagina dice a chi prova che cosa sta provando.

## 6. Quando la fase è finita (proposta del master, da confermare con Carmine al momento)

- Nessuna issue `blocker` o `major` aperta, nessuna debolezza di sicurezza aperta.
- Ogni modulo provato da chi non l'ha scritto, in tutti i ruoli.
- Il giro e2e completo di Eventi, Training e Tour verde in CI.
- La diagnostica dice tempi accettabili (o si sa perché non lo sono e che cosa si fa).

## Da portare nel piano

- **§13**: la fase di test dopo M4, divisa come al §3, senza scadenze; poi la produzione.
- **§11.3** o **§15**: la sicurezza su un'istanza locale, mai sulla prova online; le debolezze sfruttabili in privato.
- **`CONTRIBUTING.md`** (è del maintainer: con il suo sì): una riga su come si segnala un difetto (il modello e le etichette) e una su
  come si segnala una debolezza di sicurezza.
